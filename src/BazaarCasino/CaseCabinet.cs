using System;
using System.Collections;
using System.Linq;
using RoR2;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BazaarCasino
{
    internal sealed class WorldCaseMachine : MonoBehaviour,IInteractable
    {
        private sealed class ReelRow {internal RectTransform strip;internal Image marker;internal float stride;}
        private MachineCanvas screen,controls,info;
        private RectTransform viewport;
        private ReelRow[] rows=Array.Empty<ReelRow>();
        private static readonly int[] BatchCounts={1,5,10};
        private TextMeshProUGUI openButton,wallet,odds,notice;
        private readonly TextMeshProUGUI[] batchButtons=new TextMeshProUGUI[3];
        private Transform cabinet,display,marquee;
        private readonly Transform[] trims=new Transform[2];
        private TextMeshPro title;
        private BoxCollider interaction;
        private const float cabinetHeight=CaseLayout.CabinetHeight;
        internal float CameraHeight=>cabinetHeight*.5f;
        internal float CameraDistance=>Mathf.Max(4.2f,cabinetHeight*.5f/Mathf.Tan(22.5f*Mathf.Deg2Rad)*1.12f);
        internal int SelectedCount {get;private set;}=1;
        internal void SelectCount(int count)
        {if(!IsOpening&&CaseBatchRules.ValidCount(count)&&count!=SelectedCount){SelectedCount=count;ShowPreview(lastState);}}
        private Highlight cabinetOutline;
        private uint receipt;
        private Coroutine animation;
        private bool focused;
        private int lastPrice;
        private bool lastAvailable;
        private CasinoState lastState;
        private int[] previewItems;
        internal bool IsOpening {get;private set;}

        internal static WorldCaseMachine Create(CasinoLounge room)
        {
            var root=new GameObject("LunarCacheMachine");root.transform.SetParent(room.transform,false);root.transform.localPosition=new Vector3(21,0,-27.5f);root.transform.localRotation=Quaternion.Euler(0,180,0);root.transform.localScale=Vector3.one*1.65f;
            root.layer=LayerIndex.defaultLayer.intVal;root.AddComponent<EntityLocator>().entity=root;
            var machine=root.AddComponent<WorldCaseMachine>();
            machine.interaction=root.AddComponent<BoxCollider>();machine.interaction.isTrigger=true;
            var body=room.Cube("CacheCabinet",new Vector3(0,1.65f,0),new Vector3(3.5f,3.3f,1.6f),new Color(.08f,.16f,.19f),true,root.transform);machine.cabinet=body.transform;
            machine.cabinetOutline=root.AddComponent<Highlight>();machine.cabinetOutline.targetRenderer=body.GetComponent<Renderer>();machine.cabinetOutline.highlightColor=Highlight.HighlightColor.interactive;
            machine.marquee=room.Cube("CacheMarquee",new Vector3(0,3.35f,0),new Vector3(3.65f,.18f,1.8f),new Color(.32f,.85f,.68f),false,root.transform).transform;
            machine.display=room.Cube("CacheDisplay",new Vector3(0,1.93f,-.86f),new Vector3(3.25f,1.36f,.14f),new Color(.04f,.1f,.11f),false,root.transform).transform;
            room.Cube("CacheControls",new Vector3(0,.94f,-.875f),new Vector3(3.25f,.8f,.14f),new Color(.05f,.13f,.15f),false,root.transform);
            for(int i=0;i<2;i++)machine.trims[i]=room.Cube("CacheTrim",new Vector3((i*2-1)*1.68f,1.65f,-.85f),new Vector3(.06f,2.8f,.1f),new Color(.4f,.9f,.72f),false,root.transform).transform;
            machine.title=room.Label("LUNAR CACHE",new Vector3(0,2.94f,-.92f),2,new Vector2(3.2f,.5f),Quaternion.identity,root.transform);
            machine.Build();return machine;
        }
        private void Build()
        {
            screen=new MachineCanvas(transform,"CaseScreen3D",new Vector3(0,1.93f,-.945f),new Vector2(630,230),.005f);
            viewport=MachineCanvas.Rect(screen.Root,"Physical case window");MachineCanvas.Place(viewport,0,0,1,1);
            var glass=viewport.gameObject.AddComponent<Image>();glass.color=new Color(.025f,.045f,.06f,1);glass.raycastTarget=false;viewport.gameObject.AddComponent<RectMask2D>();
            controls=new MachineCanvas(transform,"CacheControls3D",new Vector3(0,.94f,-.957f),new Vector2(630,148),.005f);
            for(int i=0;i<3;i++)batchButtons[i]=controls.Button(i+1,new[]{"1x","5x","10x"}[i],17,i*.24f,.73f,(i+1)*.24f-.02f,.98f);
            openButton=controls.Button(0,"OPEN CASE",17,.20f,.32f,.74f,.69f);
            controls.Button(4,"ESC · Exit",14,.77f,.32f,1,.69f);
            wallet=controls.Text("E · Use machine",13,Color.white,0,.02f,.37f,.26f);
            notice=controls.Text("Random item drops",13,Color.white,.38f,.02f,1,.26f);
            info=new MachineCanvas(transform,"CaseOdds3D",new Vector3(0,2.65f,-.95f),new Vector2(630,26),.005f);
            odds=info.Text("COMMON → LEGENDARY",14,new Color(.60f,.91f,.77f),0,0,1,1);
            ConfigureCabinet();
            ShowPreview(null);
        }
        private void ConfigureCabinet()
        {
            const float height=CaseLayout.WindowPixels;
            float centre=CaseLayout.WindowBottom+height*CaseLayout.PixelScale*.5f,top=CaseLayout.WindowBottom+height*CaseLayout.PixelScale;
            cabinet.localPosition=new Vector3(0,cabinetHeight*.5f,0);cabinet.localScale=new Vector3(3.5f,cabinetHeight,1.6f);
            display.localPosition=new Vector3(0,centre,-.86f);display.localScale=new Vector3(3.25f,height*.005f+.14f,.14f);
            marquee.localPosition=new Vector3(0,top+.85f,0);title.transform.localPosition=new Vector3(0,top+.44f,-.92f);
            for(int i=0;i<2;i++){trims[i].localPosition=new Vector3((i*2-1)*1.68f,cabinetHeight*.5f,-.85f);trims[i].localScale=new Vector3(.06f,cabinetHeight-.5f,.1f);}
            interaction.center=new Vector3(0,cabinetHeight*.5f,0);interaction.size=new Vector3(3.6f,cabinetHeight+.1f,2);
            screen.Root.localPosition=new Vector3(0,centre,-.945f);screen.Root.sizeDelta=new Vector2(630,height);
            info.Root.localPosition=new Vector3(0,top+.15f,-.95f);
        }
        private void BuildRows(int count)
        {
            foreach(Transform child in viewport){child.gameObject.SetActive(false);Destroy(child.gameObject);}
            rows=new ReelRow[count];
            for(int i=0;i<count;i++)
            {
                CaseLayout.RowBounds(count,i,out float bottom,out float top);
                var window=MachineCanvas.Rect(viewport,"Case reel "+(i+1));MachineCanvas.Place(window,0,bottom,1,top);
                var background=window.gameObject.AddComponent<Image>();background.color=new Color(.025f,.045f,.06f,1);background.raycastTarget=false;window.gameObject.AddComponent<RectMask2D>();
                var strip=MachineCanvas.Rect(window,"Item strip");strip.anchorMin=Vector2.zero;strip.anchorMax=new Vector2(0,1);strip.pivot=new Vector2(0,.5f);strip.sizeDelta=Vector2.zero;
                var marker=MachineCanvas.Rect(window,"Prize marker");MachineCanvas.Place(marker,.498f,.03f,.502f,.97f);var accent=marker.gameObject.AddComponent<Image>();accent.color=new Color(.95f,.77f,.35f);accent.raycastTarget=false;
                rows[i]=new ReelRow{strip=strip,marker=accent,stride=count==1?144:105};
            }
        }
        internal void SetFocus(bool value){focused=value;if(!value){controls.Hover(default,false);wallet.text="E · Use machine";}}
        private void LateUpdate(){if(focused&&cabinetOutline)cabinetOutline.isOn=false;}
        internal int HoverControls(Ray ray,bool enabled)=>controls.Hover(ray,enabled,focused);
        internal void ControlState(CasinoState state,uint gold,bool waiting,string message)
        {
            lastPrice=state?.casePrice??0;lastAvailable=state?.caseEnabled==true;
            wallet.text="Gold "+gold.ToString("N0");
            int total=state?.caseWeights?.Sum()??0;
            odds.text=total>0?string.Join("  ·  ",new[]{"Common","Uncommon","Legendary"}.Select((name,i)=>name+" "+(state.caseWeights[i]*100f/total).ToString("0.#")+"%")):"Loading reward pool…";
            openButton.text=IsOpening?"OPENING…":waiting?"WAITING…":state?.caseEnabled==true&&state.casePrice>0?"OPEN "+SelectedCount+"x · "+CaseBatchRules.Total(state.casePrice,SelectedCount).ToString("N0")+" GOLD":"UNAVAILABLE";
            for(int i=0;i<3;i++)batchButtons[i].text=(SelectedCount==BatchCounts[i]?"▶ ":"")+BatchCounts[i]+"x";
            if(message!=null&&!IsOpening){notice.text=message;notice.color=Color.white;}
        }
        internal void Preview(CasinoState state){lastState=state;if(receipt==0&&!IsOpening)ShowPreview(state);}
        private void ShowPreview(CasinoState state)
        {
            var items=state?.caseItems??Array.Empty<int>();
            if(rows.Length==SelectedCount&&previewItems!=null&&previewItems.SequenceEqual(items))return;
            previewItems=items.ToArray();
            BuildRows(SelectedCount);
            var pool=items.Select(i=>ItemCatalog.GetItemDef((ItemIndex)i)).Where(d=>d).ToArray();
            for(int row=0;row<rows.Length;row++)
            {
                for(int i=0;i<9;i++)AddCard(rows[row],pool.Length>0?pool[(i+row*3)%pool.Length]:null,i);
                rows[row].strip.anchoredPosition=new Vector2(-rows[row].stride,0);
            }
            notice.text="Choose a batch, then open";notice.color=Color.white;
        }
        private static Color Rarity(ItemDef item)
        {var tier=item?ItemTierCatalog.GetItemTierDef(item.tier):null;return tier?(Color)ColorCatalog.GetColor(tier.colorIndex):Color.white;}
        private void AddCard(ReelRow row,ItemDef item,int index)
        {
            var card=MachineCanvas.Rect(row.strip,"Reward card");card.anchorMin=Vector2.zero;card.anchorMax=new Vector2(0,1);card.pivot=new Vector2(0,.5f);
            card.sizeDelta=new Vector2(row.stride-8,-6);card.anchoredPosition=new Vector2(index*row.stride+4,0);
            var color=Rarity(item);var fill=card.gameObject.AddComponent<Image>();fill.color=new Color(color.r*.13f+.025f,color.g*.13f+.025f,color.b*.13f+.025f,1);fill.raycastTarget=false;
            var bar=MachineCanvas.Rect(card,"Rarity");MachineCanvas.Place(bar,0,.96f,1,1);var accent=bar.gameObject.AddComponent<Image>();accent.color=color;accent.raycastTarget=false;
            var image=MachineCanvas.Rect(card,"Item icon").gameObject.AddComponent<Image>();image.preserveAspect=true;image.raycastTarget=false;MachineCanvas.Place(image.rectTransform,.10f,.23f,.90f,.92f);CaseArtwork.Assign(image,item);
            var label=screen.Text(card,item?Language.GetString(item.nameToken):"?",rows.Length==1?16:10,Color.white);MachineCanvas.Place(label.rectTransform,.03f,.025f,.97f,.22f);
            label.enableWordWrapping=false;label.enableAutoSizing=true;label.fontSizeMin=rows.Length==1?12:8;label.fontSizeMax=rows.Length==1?16:10;
        }
        internal void Receive(CasinoState state)
        {
            if(state==null)return;lastState=state;
            if(state.caseToken==0){Preview(state);return;}
            if(state.caseToken==receipt)return;
            receipt=state.caseToken;if(animation!=null)StopCoroutine(animation);animation=StartCoroutine(Animate(state));
        }
        private IEnumerator Animate(CasinoState state)
        {
            var prizes=state.caseRewards?.Length>0?state.caseRewards:new[]{state.caseItem};
            var rewards=prizes.Select(i=>ItemCatalog.GetItemDef((ItemIndex)i)).ToArray();
            var available=(state.caseItems??Array.Empty<int>()).Select(i=>ItemCatalog.GetItemDef((ItemIndex)i)).Where(d=>d).ToArray();
            if(!CaseBatchRules.ValidCount(rewards.Length)||rewards.Any(d=>!d)||available.Length==0){notice.text="Collect rewards below";IsOpening=false;yield break;}
            IsOpening=true;previewItems=null;notice.text="Opening "+rewards.Length+" cases…";notice.color=Color.white;BuildRows(rewards.Length);const int resultPosition=23;
            var pools=new[]{available.Where(d=>d.tier==ItemTier.Tier1).ToArray(),available.Where(d=>d.tier==ItemTier.Tier2).ToArray(),available.Where(d=>d.tier==ItemTier.Tier3).ToArray()};
            var poolFlags=pools.Select(p=>p.Length>0).ToArray();
            var jitter=new float[rows.Length];
            for(int row=0;row<rows.Length;row++)
            {
                var random=new System.Random(unchecked(state.caseSeed+row*7919));
                for(int i=0;i<31;i++)
                {
                    var item=rewards[row];
                    if(i!=resultPosition){int chosen=GachaRules.ChooseTier(random.NextDouble(),state.caseWeights??new[]{70,27,3},poolFlags);var choices=chosen>=0?pools[chosen]:available;item=choices[random.Next(choices.Length)];}
                    AddCard(rows[row],item,i);
                }
                jitter[row]=((float)random.NextDouble()-.5f)*rows[row].stride*.12f;
            }
            float start=Time.unscaledTime-Mathf.Max(0,state.caseElapsed);
            do
            {
                float elapsed=Time.unscaledTime-start;
                for(int row=0;row<rows.Length;row++)
                {
                    float replay=GachaRules.ReelElapsed(elapsed,row,rows.Length);
                    rows[row].strip.anchoredPosition=new Vector2(GachaRules.CardOffset(630,resultPosition,jitter[row],replay,rows[row].stride),0);
                    if(replay>=SpinTiming.Case)rows[row].marker.color=Rarity(rewards[row]);
                }
                if(elapsed>=SpinTiming.Case)break;
                yield return null;
            }while(true);
            notice.text=rewards.Length+(state.caseCommand?" essences · collect below":" items · collect below");
            notice.color=rewards.Any(d=>d.tier==ItemTier.Tier3)?Rarity(rewards.First(d=>d.tier==ItemTier.Tier3)):Rarity(rewards[0]);
            IsOpening=false;animation=null;
            openButton.text=lastAvailable&&lastPrice>0?"OPEN "+SelectedCount+"x · "+CaseBatchRules.Total(lastPrice,SelectedCount).ToString("N0")+" GOLD":"OPEN CASE";
        }
        public string GetContextString(Interactor actor)=>CasinoText.Get("Use Lunar Cache — random item");
        public Interactability GetInteractability(Interactor actor)=>CasinoPlugin.InBazaar?Interactability.Available:Interactability.Disabled;
        public void OnInteractionBegin(Interactor actor)=>CasinoPlugin.Instance?.OpenCaseMachine(actor,this);
        public bool ShouldIgnoreSpherecastForInteractibility(Interactor actor)=>false;
        public bool ShouldShowOnScanner()=>true;
        public bool ShouldProximityHighlight()=>true;
    }
}
