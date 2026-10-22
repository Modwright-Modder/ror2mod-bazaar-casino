using System;
using System.Collections.Generic;
using System.Linq;
using RoR2;
using TMPro;
using UnityEngine;

namespace BazaarCasino
{
    // Full-size native Hermit Crab visuals replay the host timeline in a
    // dedicated arena. Model animation is sampled from the original game clips.
    internal sealed class WorldCrabRace : MonoBehaviour,IInteractable
    {
        private static readonly Color[] Colors={new Color(1,.45f,.32f),new Color(.35f,.75f,1),new Color(.65f,1,.4f),new Color(.9f,.5f,1)};
        private static readonly string[] Names={"Rusty","Shelldon","Digger","Grumpy"};
        private readonly Transform[] crabs=new Transform[CrabRun.Count];
        private readonly NativeHermitVisual[] visuals=new NativeHermitVisual[CrabRun.Count];
        private bool modelFailure;
        internal bool ModelsReady=>visuals.All(v=>v);
        internal bool ModelsUnavailable=>modelFailure||NativeHermitAssets.Failed;
        internal Vector3 InteractionPosition=>transform.TransformPoint(new Vector3(0,1,LoungeLayout.BettingZ));
        internal bool Overview;
        internal bool BettingView=true;
        private readonly Renderer[] flags=new Renderer[CrabRun.Count];
        private MachineCanvas panel;
        private TextMeshProUGUI heading,clock,stake,receipt,betButton,potential;
        private TextMeshPro scoreboard;
        private readonly TextMeshProUGUI[] laneButtons=new TextMeshProUGUI[CrabRun.Count];
        private CrabRun replay;
        private RaceSnapshot snapshot;
        private float receivedAt;
        private int replaySeed,replayRound=-1;
        internal RaceSnapshot State=>snapshot;
        internal string ClockText=>clock?clock.text:"";
        internal static WorldCrabRace Create(CasinoLounge room)
        {
            var root=new GameObject("HermitDerby");root.transform.SetParent(room.transform,false);root.transform.localPosition=new Vector3(0,0,LoungeLayout.RaceZ);
            root.layer=LayerIndex.defaultLayer.intVal;root.AddComponent<EntityLocator>().entity=root;
            var hit=root.AddComponent<BoxCollider>();hit.isTrigger=true;hit.center=new Vector3(0,1.2f,LoungeLayout.BettingZ);hit.size=new Vector3(6f,2.4f,1.1f);
            var race=root.AddComponent<WorldCrabRace>();race.Build(room);return race;
        }
        private void Build(CasinoLounge room)
        {
            var wood=new Color(.13f,.18f,.2f);var sand=new Color(.37f,.31f,.21f);var gold=new Color(.94f,.74f,.3f);
            room.Cube("DerbyTrackBase",new Vector3(0,.30f,0),new Vector3(LoungeLayout.TrackWidth,.6f,LoungeLayout.TrackLength+1),wood,true,transform);
            room.Cube("DerbySand",new Vector3(0,.62f,0),new Vector3(LoungeLayout.TrackWidth-.3f,.04f,LoungeLayout.TrackLength+.6f),sand,false,transform);
            for(int lane=0;lane<CrabRun.Count;lane++)
            {
                float x=(lane-1.5f)*LoungeLayout.LaneWidth;
                room.Cube("LaneMarker",new Vector3(x,.66f,-LoungeLayout.TrackLength/2-.1f),new Vector3(2.8f,.03f,.22f),Colors[lane],false,transform);
                var marker=room.Label((lane+1).ToString(),new Vector3(x,.68f,-LoungeLayout.TrackLength/2-.1f),3,new Vector2(2,2),Quaternion.Euler(90,0,0),transform);
                marker.color=Colors[lane];
            }
            foreach(float x in new[]{-6.4f,-3.2f,0,3.2f,6.4f})room.Cube("LaneDivider",new Vector3(x,.66f,0),new Vector3(.04f,.035f,LoungeLayout.TrackLength+.4f),gold,false,transform);
            for(int i=0;i<16;i++)room.Cube("FinishChecker",new Vector3(-6.4f+(i+.5f)*.8f,.67f,LoungeLayout.TrackLength/2),new Vector3(.8f,.03f,.4f),i%2==0?Color.white:Color.black,false,transform);
            foreach(float x in new[]{-7f,7f})room.Cube("FinishGatePost",new Vector3(x,3,14.8f),new Vector3(.25f,6,.25f),gold,false,transform);
            room.Cube("FinishGate",new Vector3(0,6,14.8f),new Vector3(14.3f,.25f,.3f),gold,false,transform);
            scoreboard=room.Label("HERMIT DERBY",new Vector3(0,7.2f,14.8f),5,new Vector2(17,2),Quaternion.identity,transform);
            scoreboard.gameObject.AddComponent<CasinoBillboard>();
            var desk=room.Cube("DerbyBettingDesk",new Vector3(0,.65f,LoungeLayout.BettingZ),new Vector3(6f,1.3f,.8f),wood,true,transform);
            var highlight=gameObject.AddComponent<Highlight>();highlight.targetRenderer=desk.GetComponent<Renderer>();highlight.highlightColor=Highlight.HighlightColor.interactive;
            panel=new MachineCanvas(transform,"DerbyControls3D",new Vector3(0,1.55f,LoungeLayout.BettingZ-.47f),new Vector2(1000,430),.0058f);
            heading=panel.Text("HERMIT DERBY · HIGH STAKES",30,gold,0,.87f,1,1);
            clock=panel.Text("Waiting for host…",21,Color.white,0,.73f,.78f,.87f);
            panel.Button(9,"V · View",18,.79f,.73f,1,.86f);
            for(int i=0;i<CrabRun.Count;i++)laneButtons[i]=panel.Button(i,(i+1)+" · "+Names[i],24,i*.25f,.50f,(i+1)*.25f-.01f,.70f);
            panel.Button(5,"−",24,0,.31f,.12f,.46f);stake=panel.Text("Stake: —",24,gold,.13f,.31f,.47f,.46f);
            panel.Button(6,"+",24,.48f,.31f,.60f,.46f);betButton=panel.Button(7,"BET — GOLD",22,.61f,.31f,.99f,.46f);
            potential=panel.Text("Win returns 4×, including your stake",18,gold,0,.22f,1,.30f);
            panel.Button(8,"Cancel / Refund",19,0,.02f,.27f,.22f);receipt=panel.Text("Four equal-odds crabs",17,Color.white,.28f,.02f,.79f,.22f);
            panel.Button(4,"ESC · Exit",19,.80f,.02f,1,.22f);
        }
        private void CreateCrabs()
        {
            NativeHermitAssets.Warm();
            if(ModelsReady||modelFailure||!NativeHermitAssets.Ready)return;
            try
            {
                for(int lane=0;lane<CrabRun.Count;lane++)
                {
                    var root=new GameObject("Racer "+Names[lane]);root.transform.SetParent(transform,false);
                    crabs[lane]=root.transform;visuals[lane]=NativeHermitAssets.Create(root.transform,lane);
                    var color=GetComponentInParent<CasinoLounge>().Cube("Lane pennant",new Vector3(0,3.5f,0),new Vector3(.35f,.12f,.35f),Colors[lane],false,root.transform);
                    flags[lane]=color.GetComponent<Renderer>();
                }
            }
            catch(Exception error){modelFailure=true;Debug.LogError("[BazaarCasino] Native Hermit Crab setup failed: "+error);}
        }
        internal void View(out Vector3 position,out Vector3 target,out float fov)
        {
            if(BettingView)
            {position=transform.TransformPoint(new Vector3(0,4.8f,LoungeLayout.BettingZ-7));target=transform.TransformPoint(new Vector3(0,1.5f,LoungeLayout.BettingZ));fov=62;return;}
            if(Overview)
            {position=transform.TransformPoint(new Vector3(0,15.2f,0));target=transform.TransformPoint(new Vector3(0,.6f,.01f));fov=108;return;}
            if(ModelsReady)
            {
                float pack=(crabs.Min(c=>c.localPosition.z)+crabs.Max(c=>c.localPosition.z))*.5f;
                position=transform.TransformPoint(new Vector3(12,8,Mathf.Clamp(pack-4,-18,15)));
                target=transform.TransformPoint(new Vector3(0,1.3f,pack+1));fov=68;return;
            }
            position=transform.TransformPoint(new Vector3(0,4.8f,LoungeLayout.BettingZ-7));
            target=transform.TransformPoint(new Vector3(0,1.5f,LoungeLayout.BettingZ));fov=62;
        }
        internal void Receive(RaceSnapshot state)
        {
            if(state==null||state.minimum<=0||state.maximum<state.minimum||snapshot!=null&&state.revision<snapshot.revision)return;
            snapshot=state;receivedAt=Time.unscaledTime;
            if(state.phase!=0&&(replayRound!=state.round||replaySeed!=state.seed))
            {replay=new CrabRun(state.seed);replaySeed=state.seed;replayRound=state.round;}
            if(state.phase==0)replay=null;
        }
        private void LateUpdate()
        {
            double elapsed=snapshot==null?0:snapshot.elapsed+Time.unscaledTime-receivedAt;
            CreateCrabs();
            if(ModelsReady)for(int i=0;i<CrabRun.Count;i++)
            {
                var pose=replay?.Sample(i,elapsed)??new CrabMotion{phase=CrabMotionPhase.Idle};
                crabs[i].localPosition=new Vector3((i-1.5f)*LoungeLayout.LaneWidth,.66f,-LoungeLayout.TrackLength/2+pose.position);
                visuals[i].Sample(pose);
                bool winner=snapshot?.phase==2&&snapshot.winner==i;
                flags[i].transform.localScale=new Vector3(.35f,.12f,.35f)*(winner?1.4f+Mathf.Sin(Time.unscaledTime*8)*.2f:1);
            }
            UpdateClock();
        }
        private void UpdateClock()
        {
            double remaining=Math.Max(0,(snapshot?.remaining??0)-(Time.unscaledTime-receivedAt));
            clock.text=NativeHermitAssets.Failed||modelFailure?"Native Hermit Crab unavailable":!ModelsReady?"Loading native Hermit Crabs…":snapshot==null?"Waiting for host…":snapshot.phase==1?"RACING · They might dig in any moment!":snapshot.phase==2?"WINNER · "+Names[Mathf.Clamp(snapshot.winner,0,3)]+" · Next bets in "+Math.Ceiling(remaining)+"s":remaining>0?"STARTS IN "+Math.Ceiling(remaining)+"s · Betting open":"Place a wager to start the 5s countdown";
            scoreboard.text=snapshot?.phase==2?"WINNER · "+Names[Mathf.Clamp(snapshot.winner,0,3)]:snapshot?.phase==1?"HERMIT DERBY · RACING":remaining>0?"HERMIT DERBY · STARTS IN "+Math.Ceiling(remaining)+"s":"HERMIT DERBY";
        }
        internal int Hover(Ray ray,bool enabled)=>panel.Hover(ray,enabled&&ModelsReady,true);
        internal void ControlState(int lane,int wager,uint gold,uint owner,string message)
        {
            bool betting=snapshot?.phase==0&&ModelsReady;
            heading.text="HERMIT DERBY · GOLD "+gold.ToString("N0");
            UpdateClock();
            for(int i=0;i<4;i++){laneButtons[i].text=(i==lane?"▶ ":"")+(i+1)+" · "+Names[i];laneButtons[i].color=Colors[i];}
            stake.text="Stake: "+wager.ToString("N0")+" gold";betButton.text=betting?"BET "+wager.ToString("N0")+" GOLD":"BETTING CLOSED";
            potential.text="If win: "+(wager*4L).ToString("N0")+" gold returned (includes "+wager.ToString("N0")+" stake) · Profit +"+(wager*3L).ToString("N0");
            var ticket=snapshot?.tickets?.FirstOrDefault(t=>t.id==owner);
            receipt.text=message??(ticket!=null?(snapshot.phase==2?"Returned "+ticket.payout.ToString("N0")+" gold":"Paid "+ticket.wager.ToString("N0")+" gold · "+Names[ticket.lane]):"Gold "+gold.ToString("N0")+" · Min "+(snapshot?.minimum??0).ToString("N0"));
        }
        public string GetContextString(Interactor actor)=>CasinoText.Get("Bet on Hermit Derby — high stakes");
        public Interactability GetInteractability(Interactor actor)=>CasinoPlugin.InBazaar?Interactability.Available:Interactability.Disabled;
        public void OnInteractionBegin(Interactor actor)=>CasinoPlugin.Instance?.OpenRace(actor,this);
        public bool ShouldIgnoreSpherecastForInteractibility(Interactor actor)=>false;
        public bool ShouldShowOnScanner()=>true;
        public bool ShouldProximityHighlight()=>true;
    }
}
