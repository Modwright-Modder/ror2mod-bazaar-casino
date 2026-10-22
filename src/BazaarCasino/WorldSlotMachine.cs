using System;
using System.Collections;
using System.Collections.Generic;
using RoR2;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BazaarCasino
{
    internal sealed class WorldSlotMachine : MonoBehaviour,IInteractable
    {
        internal int Index;
        private readonly CasinoArt art=new CasinoArt();
        private readonly List<RectTransform> strips=new List<RectTransform>();
        private readonly List<Image[]> symbols=new List<Image[]>();
        private Transform lever;
        private BoxCollider leverHitbox;
        private Highlight leverOutline,cabinetOutline;
        private MachineCanvas controls;
        private TextMeshProUGUI physicalWager,physicalWallet;
        private bool focused;
        internal bool HitLever(Ray ray)=>leverHitbox&&leverHitbox.Raycast(ray,out _,12);
        internal void HoverLever(bool hover){if(leverOutline)leverOutline.isOn=hover;}
        internal int HoverControls(Ray ray,bool enabled)=>controls.Hover(ray,enabled,focused);
        internal void SetFocus(bool value)
        {focused=value;if(!value){HoverLever(false);controls.Hover(default,false);physicalWallet.text="E · Play     ESC · Exit";}}
        internal void ControlState(int wager,uint gold,string notice)
        {
            physicalWager.text="WAGER "+wager.ToString("N0");physicalWallet.text="Gold "+gold.ToString("N0");
            if(!spinning&&notice!=null)result.text=notice;
        }
        private void LateUpdate(){if(focused&&cabinetOutline)cabinetOutline.isOn=false;}
        private TextMeshPro result;
        private Light winLight;
        private bool spinning;
        internal bool IsSpinning=>spinning;
        internal string DisplayResult=>result?result.text:"";
        private uint lastSpin;
        private Coroutine animation;
        internal static WorldSlotMachine Create(CasinoLounge room,int index,Vector3 position)
        {
            var root=new GameObject("LoungeSlotMachine"+index);root.transform.SetParent(room.transform,false);root.transform.localPosition=position;root.layer=LayerIndex.defaultLayer.intVal;
            root.AddComponent<EntityLocator>().entity=root;
            var collider=root.AddComponent<BoxCollider>();collider.center=new Vector3(0,1.8f,0);collider.size=new Vector3(2.8f,3.6f,2);collider.isTrigger=true;
            var machine=root.AddComponent<WorldSlotMachine>();machine.Index=index;machine.Build(room);return machine;
        }
        private void Build(CasinoLounge room)
        {
            var gold=new Color(.95f,.72f,.28f);var dark=new Color(.11f,.16f,.25f);
            var cabinet=room.Cube("SlotCabinet",new Vector3(0,1.9f,0),new Vector3(2.6f,3.8f,1.6f),dark,true,transform);
            var highlight=cabinetOutline=gameObject.AddComponent<Highlight>();highlight.targetRenderer=cabinet.GetComponent<Renderer>();highlight.highlightColor=Highlight.HighlightColor.interactive;
            room.Cube("GoldMarquee",new Vector3(0,3.8f,0),new Vector3(2.85f,.18f,1.8f),gold,false,transform);
            room.Cube("ReelFrame",new Vector3(0,2.15f,-.84f),new Vector3(2.35f,1.2f,.1f),gold,false,transform);
            room.Cube("ControlPanel",new Vector3(0,1.19f,-.95f),new Vector3(2.45f,.72f,.55f),dark,true,transform);
            room.Label("BAZAAR JACKPOT",new Vector3(0,3.25f,-.82f),1.65f,new Vector2(2.5f,.65f),Quaternion.identity,transform);
            result=room.Label("E · PLAY SLOTS",new Vector3(0,.57f,-.82f),1.35f,new Vector2(2.4f,.50f),Quaternion.identity,transform);
            var canvasRoot=new GameObject("WorldReels",typeof(RectTransform));canvasRoot.transform.SetParent(transform,false);canvasRoot.transform.localPosition=new Vector3(0,2.15f,-.905f);canvasRoot.transform.localScale=Vector3.one*.01f;
            var canvas=canvasRoot.AddComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
            ((RectTransform)canvasRoot.transform).sizeDelta=new Vector2(225,105);
            // No GraphicRaycaster: this display does not capture player input.
            for(int column=0;column<3;column++)
            {
                var viewport=Rect(canvasRoot.transform,"ReelViewport",new Vector2((column-1)*73,0),new Vector2(65,94));
                var background=viewport.gameObject.AddComponent<Image>();background.color=new Color(.035f,.05f,.07f);background.raycastTarget=false;
                viewport.gameObject.AddComponent<RectMask2D>();
                var strip=Rect(viewport,"ReelStrip",Vector2.zero,new Vector2(65,94));strips.Add(strip);
                var images=new Image[3];
                for(int row=0;row<3;row++)
                {
                    var rect=Rect(strip,"Symbol",new Vector2(0,(row-1)*94),new Vector2(57,82));
                    images[row]=rect.gameObject.AddComponent<Image>();images[row].sprite=art.Get("slot"+column);images[row].preserveAspect=true;images[row].raycastTarget=false;
                }
                symbols.Add(images);
            }
            var arm=new GameObject("SlotLever");lever=arm.transform;lever.SetParent(transform,false);lever.localPosition=new Vector3(1.5f,1.55f,0);
            arm.AddComponent<EntityLocator>().entity=gameObject;
            leverHitbox=arm.AddComponent<BoxCollider>();leverHitbox.isTrigger=true;leverHitbox.center=new Vector3(0,.6f,0);leverHitbox.size=new Vector3(.7f,1.5f,.7f);
            room.Cube("LeverStem",new Vector3(0,.5f,0),new Vector3(.12f,1,.12f),gold,false,lever);
            var knob=GameObject.CreatePrimitive(PrimitiveType.Sphere);knob.name="LeverKnob";knob.transform.SetParent(lever,false);knob.transform.localPosition=Vector3.up;knob.transform.localScale=Vector3.one*.32f;
            var knobCollider=knob.GetComponent<Collider>();knobCollider.enabled=false;Destroy(knobCollider);knob.GetComponent<Renderer>().sharedMaterial=room.Cube("LeverBase",Vector3.zero,new Vector3(.3f,.2f,.3f),gold,false,lever).GetComponent<Renderer>().sharedMaterial;
            leverOutline=arm.AddComponent<Highlight>();leverOutline.highlightColor=Highlight.HighlightColor.custom;leverOutline.CustomColor=Color.white;leverOutline.strength=1;
            leverOutline.SetTargetRendererList(new List<Renderer>(arm.GetComponentsInChildren<Renderer>()));leverOutline.isOn=false;
            controls=new MachineCanvas(transform,"SlotControls3D",new Vector3(0,1.19f,-1.235f),new Vector2(225,64),.01f);
            physicalWager=controls.Text("WAGER 10",10.5f,new Color(.95f,.77f,.35f),.30f,.4f,.70f,.98f);
            controls.Button(0,"−",12,0,.4f,.13f,.94f);controls.Button(1,"÷10",9,.15f,.4f,.28f,.94f);
            controls.Button(2,"×10",9,.72f,.4f,.85f,.94f);controls.Button(3,"+",12,.87f,.4f,1,.94f);
            physicalWallet=controls.Text("E · Play",7.8f,Color.white,0,.02f,.65f,.31f);
            controls.Button(4,"ESC · Exit",7.8f,.69f,.02f,1,.31f);
            var paytable=new MachineCanvas(transform,"SlotPaytable3D",new Vector3(0,.18f,-.835f),new Vector2(225,25),.01f);
            paytable.Text("Cherry 3×  Lemon 5×  Bell 8×  Star 12×  Seven 25×  Moon 60×",5.8f,Color.white,0,.48f,1,1);
            paytable.Text("3 matching symbols win · A pair returns your wager",6,Color.white,0,0,1,.48f);
            var lamp=new GameObject("SlotWinLight");lamp.transform.SetParent(transform,false);lamp.transform.localPosition=new Vector3(0,4,-.4f);
            winLight=lamp.AddComponent<Light>();winLight.type=LightType.Point;winLight.range=7;winLight.color=gold;winLight.intensity=.8f;winLight.shadows=LightShadows.None;
        }
        private static RectTransform Rect(Transform parent,string name,Vector2 position,Vector2 size)
        {
            var root=new GameObject(name,typeof(RectTransform));var rect=(RectTransform)root.transform;rect.SetParent(parent,false);rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=position;rect.sizeDelta=size;return rect;
        }
        internal void Reveal(uint spin,int[] reels,int net,float elapsed)
        {
            if(spin<=lastSpin||reels==null||reels.Length!=3||Array.Exists(reels,s=>s<0||s>5))return;
            lastSpin=spin;if(animation!=null)StopCoroutine(animation);winLight.intensity=.8f;result.transform.localScale=Vector3.one;animation=StartCoroutine(Animate(spin,reels,net,elapsed));
        }
        private IEnumerator Animate(uint spin,int[] reels,int net,float elapsed)
        {
            spinning=true;result.text="SPINNING…";var random=new System.Random(spin.GetHashCode());int[,] sequence=new int[3,43];
            float[] durations={2.6f,3.15f,3.7f};int[] turns={24,31,38};
            for(int i=0;i<3;i++){for(int cell=0;cell<43;cell++)sequence[i,cell]=random.Next(6);sequence[i,turns[i]]=reels[i];}
            float start=Time.unscaledTime-Mathf.Clamp(elapsed,0,10);
            while(Time.unscaledTime-start<SpinTiming.Reels)
            {
                float t=Time.unscaledTime-start;
                float pull=t<.25f?t/.25f:1-Mathf.Clamp01((t-.25f)/.4f);lever.localRotation=Quaternion.Euler(-55*pull,0,0);
                for(int i=0;i<3;i++)
                {
                    float progress=Mathf.Clamp01(t/durations[i]);float distance=turns[i]*(1-Mathf.Pow(1-progress,3));int cell=Mathf.Min(turns[i],Mathf.FloorToInt(distance));float fraction=distance-cell;
                    for(int row=0;row<3;row++)symbols[i][row].sprite=art.Get("slot"+sequence[i,Mathf.Clamp(cell+row-1,0,42)]);
                    strips[i].anchoredPosition=new Vector2(0,-fraction*94);
                }
                yield return null;
            }
            for(int i=0;i<3;i++){strips[i].anchoredPosition=Vector2.zero;symbols[i][1].sprite=art.Get("slot"+reels[i]);}
            lever.localRotation=Quaternion.identity;
            bool jackpot=reels[0]==reels[1]&&reels[1]==reels[2]&&reels[0]>=4;
            result.text=jackpot?"JACKPOT!\n+"+net+" GOLD":net>0?"WIN! +"+net+" GOLD":net==0?"WAGER RETURNED":"NO MATCH";
            spinning=false; // Ready as soon as the result is visible; effects continue independently.
            for(float t=Mathf.Max(0,Time.unscaledTime-start-SpinTiming.Reels);t<SpinTiming.Celebration;t+=Time.unscaledDeltaTime)
            {winLight.intensity=net>0?1+3*Mathf.Abs(Mathf.Sin(t*12)):.8f;result.transform.localScale=Vector3.one*(net>0?1+.05f*Mathf.Sin(t*9):1);yield return null;}
            winLight.intensity=.8f;result.transform.localScale=Vector3.one;spinning=false;animation=null;
        }
        public string GetContextString(Interactor actor)=>CasinoText.Get("Play Slots — Bazaar Jackpot");
        public Interactability GetInteractability(Interactor actor)=>CasinoPlugin.InBazaar&&!spinning?Interactability.Available:Interactability.Disabled;
        public void OnInteractionBegin(Interactor actor)=>CasinoPlugin.Instance?.OpenSlotMachine(actor,this);
        public bool ShouldIgnoreSpherecastForInteractibility(Interactor actor)=>false;
        public bool ShouldShowOnScanner()=>true;
        public bool ShouldProximityHighlight()=>true;
        private void OnDestroy(){art.Dispose();}
    }
}
