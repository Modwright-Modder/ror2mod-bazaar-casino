using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using RoR2;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BazaarCasino
{
    internal sealed partial class CasinoUI
    {
        private GameObject tablePanel;
        private RectTransform tableFrame,sharedCardLayer;
        private TableKind selectedTable;
        private TextMeshProUGUI tableClock,tableNotice,tableWallet,tableHover;
        private string tableMessage="Choose your wager. ESC stands up.";
        private int tableBet=10,tableDrawnRevision=-1,tableDraftRound=-1,sharedCardRound=-1,draftVersion,sentDraftVersion;
        private bool draftDirty,betInFlight,topTableView;
        private float nextDraftSubmit;
        private readonly List<RouletteBet> tableDraft=new List<RouletteBet>();
        private readonly Dictionary<string,int> sharedCardValues=new Dictionary<string,int>();
        private readonly Dictionary<string,Image> sharedCardImages=new Dictionary<string,Image>();
        private readonly List<Tuple<Button,bool,bool>> sharedButtons=new List<Tuple<Button,bool,bool>>();
        private uint MyId=>CasinoPlugin.LocalMaster?CasinoPlugin.LocalMaster.GetComponent<UnityEngine.Networking.NetworkIdentity>().netId.Value:0;
        private CasinoSeat MySeat=>CasinoPlugin.LocalMaster&&CasinoPlugin.LocalMaster.GetBody()?CasinoPlugin.LocalMaster.GetBody().GetComponent<CasinoSeat>():null;
        internal bool IsSharedTableOpen(TableKind kind)=>tablePanel&&tablePanel.activeSelf&&selectedTable==kind;
        internal void OpenSharedTable(TableKind kind)
        {
            if(!Init())return;Close();selectedTable=kind;topTableView=kind==TableKind.Roulette;
            tableBet=Mathf.Clamp(tableBet,Plugin.MinimumBet,Plugin.MaximumBet);
            if(!tablePanel)
            {
                tablePanel=Rect(canvasRoot.transform,"SharedTableOverlay").gameObject;
                Stretch((RectTransform)tablePanel.transform);tablePanel.SetActive(false);
                tablePanel.AddComponent<MPEventSystemProvider>().fallBackToMainEventSystem=true;
                tablePanel.AddComponent<CursorOpener>();
                sharedCardLayer=Rect(tablePanel.transform,"SharedHandCards");Stretch(sharedCardLayer);
                // An empty RectTransform does not cover the world or intercept clicks.
                tableFrame=Rect(tablePanel.transform,"IntegratedTableControls");Stretch(tableFrame);
            }
            tablePanel.SetActive(true);tableDrawnRevision=-1;tableDraftRound=-1;sharedCardRound=-1;
            draftDirty=betInFlight=false;draftVersion=sentDraftVersion=0;tableDraft.Clear();sharedCardValues.Clear();
            tableMessage=kind==TableKind.Roulette?"Choose a chip · Click felt to add gold · Right-click to remove":"Choose a chip · Add gold or click your chip on the table · Right-click to remove";
            DrawTableControls();Plugin.Send(ActionKind.TableJoin,table:kind);
        }
        internal void CloseSharedTable(bool leave=true)
        {
            bool wasOpen=tablePanel&&tablePanel.activeSelf;
            if(wasOpen&&leave)interactionGate.Close(Time.unscaledTime);
            if(Plugin){Plugin.TableObject(selectedTable)?.PreviewChips(0,null);Plugin.TableObject(selectedTable)?.HoverSpot();}
            draftDirty=betInFlight=false;
            ClearSharedCards();
            if(tablePanel){ReleaseCasinoFocus(tablePanel);tablePanel.SetActive(false);}
            if(wasOpen&&leave&&Plugin&&CasinoPlugin.InBazaar)Plugin.RequestTableExit(selectedTable);
        }
        private void ReleaseCasinoFocus(GameObject panel)
        {
            if(!panel||!panel.activeInHierarchy)return;
            ClearCasinoSelection(panel);
            if(isActiveAndEnabled)StartCoroutine(ClearSelectionAfterClick(panel));
        }
        private void ClearCasinoSelection(GameObject panel)
        {
            if(!panel)return;
            foreach(var system in MPEventSystem.readOnlyInstancesList)
            {
                var selected=system.currentSelectedGameObject;
                if(selected&&selected.transform.IsChildOf(panel.transform))system.SetSelectedObject(null);
            }
        }
        private IEnumerator ClearSelectionAfterClick(GameObject panel)
        {yield return null;if(panel&&!panel.activeInHierarchy)ClearCasinoSelection(panel);}
        internal void TableResponse(string message)
        {
            if(!tablePanel||!tablePanel.activeSelf)return;
            tableMessage=message;
            if(betInFlight)
            {
                betInFlight=false;
                if(message=="Table updated.")
                {
                    draftDirty=draftVersion!=sentDraftVersion;
                    tableMessage="Chips placed. The countdown starts with the first chip.";
                }
                else if(message=="Please wait for the current action."){draftDirty=true;nextDraftSubmit=Time.unscaledTime+.35f;}
                else draftDirty=false; // Reconcile rejected edits against the authoritative ACK snapshot.
            }
            // ReceiveTable already rendered the acknowledged blackjack snapshot.
            // Rebuilding the same cards here would cancel their deal/flip animations
            // immediately on the host and again when a client's ACK arrives.
            var snapshot=Plugin.Snapshot(selectedTable);
            if(selectedTable==TableKind.Blackjack&&message=="Table updated."&&snapshot?.phase==(int)TablePhase.Betting)
                tableMessage="Gold on table: "+(snapshot.seats.FirstOrDefault(s=>s.id==MyId)?.wager??0).ToString("N0")+" · Add more until betting closes";
            if(selectedTable==TableKind.Blackjack&&snapshot!=null&&snapshot.phase!=(int)TablePhase.Betting&&tableDrawnRevision==snapshot.revision)
            {
                if(tableNotice)tableNotice.text=tableMessage;
                foreach(var entry in sharedButtons)if(entry.Item1)entry.Item1.interactable=entry.Item2&&(entry.Item3||!Plugin.Waiting);
            }
            else DrawTableControls();
        }
        internal void SharedRequestTimedOut()
        {
            if(!tablePanel||!tablePanel.activeSelf)return;
            betInFlight=draftDirty=false;
            tableMessage="Host did not respond. Chips refreshed from the last server state; check all players use the same Casino version.";
            DrawTableControls();
        }
        internal void ReceiveTable(TableSnapshot snapshot)
        {
            if(!IsSharedTableOpen((TableKind)snapshot.kind))return;
            if(tableDrawnRevision!=snapshot.revision)DrawTableControls();
        }
        private void LateUpdate()
        {
            if(!tablePanel||!tablePanel.activeSelf)return;
            if(!CasinoPlugin.InBazaar||!Plugin){CloseSharedTable();return;}
            if(Input.GetKeyDown(KeyCode.Escape)){CloseFromEscape();return;}
            var snapshot=Plugin.Snapshot(selectedTable);var mine=snapshot?.seats.FirstOrDefault(s=>s.id==MyId);
            if(MySeat)MySeat.TopView=topTableView;
            if(tableWallet)tableWallet.text="Gold: "+(CasinoPlugin.LocalMaster?CasinoPlugin.LocalMaster.money.ToString("N0"):"—");
            if(snapshot!=null&&tableClock)
            {
                float left=Plugin.TableTimeLeft(selectedTable);
                tableClock.text=snapshot.phase==(int)TablePhase.Betting?(left>0?"BETS CLOSE IN "+Mathf.CeilToInt(left)+"s":betInFlight||draftDirty?"PLACING CHIPS…":"Place a wager to start the 15s countdown"):
                    snapshot.phase==(int)TablePhase.Playing?(selectedTable==TableKind.Roulette?"BALL SPINNING · "+Mathf.CeilToInt(left)+"s":(snapshot.turn==MyId?"YOUR TURN":"Turn: "+snapshot.seats.FirstOrDefault(s=>s.id==snapshot.turn)?.name)+" · "+Mathf.CeilToInt(left)+"s"):
                    "Next round in "+Mathf.CeilToInt(left)+"s";
            }
            if(mine?.seated!=true||snapshot.phase!=(int)TablePhase.Betting){Plugin.TableObject(selectedTable)?.HoverSpot();return;}
            bool overUI=MPEventSystem.readOnlyInstancesList.Any(e=>e&&e.IsPointerOverGameObject());
            var camera=CameraRigController.readOnlyInstancesList.FirstOrDefault(r=>r&&r.targetBody==(CasinoPlugin.LocalMaster?CasinoPlugin.LocalMaster.GetBody():null))?.sceneCam??Camera.main;
            var table=Plugin.TableObject(selectedTable);
            if(selectedTable==TableKind.Blackjack)
            {
                bool hovering=camera&&table&&!overUI&&table.TryBlackjackBetRay(camera.ScreenPointToRay(Input.mousePosition),mine.slot);
                table?.HoverSpot(hovering?100+mine.slot:-1);
                if(hovering&&(Input.GetMouseButtonDown(0)||Input.GetMouseButtonDown(1)))ChangeBlackjackWager(Input.GetMouseButtonDown(1));
                return;
            }
            if(camera&&table&&!overUI&&table.TryBetRay(camera.ScreenPointToRay(Input.mousePosition),out int choice,out int number))
            {
                table.HoverSpot(choice,number);
                if(tableHover)tableHover.text=RouletteLayout.Name(choice,number)+" · Click +"+tableBet+" gold · Right-click −"+tableBet+" · On spot: "+(tableDraft.FirstOrDefault(b=>b.choice==choice&&b.number==number)?.amount??0);
                if(Input.GetMouseButtonDown(0)||Input.GetMouseButtonDown(1))EditTableChip(choice,number,Input.GetMouseButtonDown(1));
            }
            else {table?.HoverSpot();if(tableHover)tableHover.text="Click felt: place chip · Right-click: remove chip · No confirmation needed";}
            if(draftDirty&&!betInFlight&&!Plugin.Waiting&&Time.unscaledTime>=nextDraftSubmit)
            {
                betInFlight=true;sentDraftVersion=draftVersion;nextDraftSubmit=Time.unscaledTime+.22f;
                // Absolute spot totals make retries/idempotent reconciliation safe;
                // subsequent clicks stay queued until this request is acknowledged.
                Plugin.Send(ActionKind.TableBet,bets:CopyDraft(),table:selectedTable);
            }
        }
        private RouletteBet[] CopyDraft()=>tableDraft.Select(b=>new RouletteBet{choice=b.choice,number=b.number,amount=b.amount}).ToArray();
        private void EditTableChip(int choice,int number,bool remove)
        {
            var mine=Plugin.Snapshot(selectedTable)?.seats.FirstOrDefault(s=>s.id==MyId);
            var spot=tableDraft.FirstOrDefault(b=>b.choice==choice&&b.number==number);
            if(remove)
            {
                if(spot==null)return;spot.amount=Math.Max(0,spot.amount-tableBet);if(spot.amount==0)tableDraft.Remove(spot);
            }
            else
            {
                long total=(long)tableDraft.Sum(b=>b.amount)+tableBet;
                if(total>Plugin.MaximumBet||(ulong)total>(ulong)CasinoPlugin.LocalMaster.money+(uint)(mine?.wager??0))
                {tableMessage="Insufficient gold or table limit reached.";DrawTableControls();return;}
                if(spot==null){spot=new RouletteBet{choice=choice,number=number};tableDraft.Add(spot);}spot.amount+=tableBet;
            }
            draftVersion++;draftDirty=true;tableMessage=RouletteLayout.Name(choice,number)+": "+(spot?.amount??0)+" gold · sending to host…";
            DrawTableControls();
        }
        private void ChangeBlackjackWager(bool remove)
        {
            if(Plugin.Waiting)return;
            var snapshot=Plugin.Snapshot(TableKind.Blackjack);var mine=snapshot?.seats.FirstOrDefault(s=>s.id==MyId);
            if(mine?.seated!=true||snapshot.phase!=(int)TablePhase.Betting)return;
            try
            {
                int next=WagerRules.Edit(mine.wager,tableBet,remove,Plugin.MinimumBet,Plugin.MaximumBet,CasinoPlugin.LocalMaster.money);
                if(next==mine.wager)return;
                TableAction(ActionKind.TableBet,next,(remove?"Refunding ":"Adding ")+Math.Abs(next-mine.wager).ToString("N0")+" gold…");
            }
            catch(Exception error){tableMessage=error.Message;DrawTableControls();}
        }
        private void TableAction(ActionKind action,int wager=0,string message=null)
        {
            if(Plugin.Waiting)return;tableMessage=message??"Processing…";if(tableNotice)tableNotice.text=tableMessage;
            Plugin.Send(action,wager,table:selectedTable);
            foreach(var entry in sharedButtons)if(entry.Item1)entry.Item1.interactable=entry.Item2&&(entry.Item3||!Plugin.Waiting);
        }
        private void DrawTableControls()
        {
            if(!tablePanel||!tablePanel.activeSelf)return;
            tableBet=Mathf.Clamp(tableBet,Plugin.MinimumBet,Plugin.MaximumBet);
            foreach(Transform child in tableFrame){child.gameObject.SetActive(false);Destroy(child.gameObject);}
            sharedButtons.Clear();
            tableHover=null;
            var snapshot=Plugin.Snapshot(selectedTable);tableDrawnRevision=snapshot?.revision??-1;
            var mine=snapshot?.seats.FirstOrDefault(s=>s.id==MyId);
            if(snapshot!=null&&(snapshot.round!=tableDraftRound||!draftDirty&&!betInFlight))
            {
                tableDraft.Clear();if(mine?.bets!=null)foreach(var b in mine.bets)tableDraft.Add(new RouletteBet{choice=b.choice,number=b.number,amount=b.amount});
                tableDraftRound=snapshot.round;draftDirty=false;
            }
            Plugin.TableObject(selectedTable)?.PreviewChips(MyId,snapshot?.phase==(int)TablePhase.Betting&&(draftDirty||betInFlight)?CopyDraft():null);
            PlaceText(selectedTable.ToString().ToUpperInvariant(),30,Gold,.30f,.945f,.70f,.99f);
            tableWallet=PlaceText("Gold: —",22,Gold,.025f,.93f,.19f,.98f);
            tableClock=PlaceText("Connecting to host…",23,Color.white,.23f,.887f,.77f,.941f);
            TableButton(tableFrame,topTableView?"Seat View":"Top View",.79f,.93f,.88f,.98f,()=>{topTableView=!topTableView;if(MySeat)MySeat.TopView=topTableView;DrawTableControls();},true,true);
            TableButton(tableFrame,"Stand up / Exit",.895f,.93f,.985f,.98f,()=>CloseSharedTable(),true,true);
            tableNotice=PlaceText(tableMessage,18,Color.white,.20f,.009f,.80f,.052f);
            bool joined=mine?.seated==true,betting=snapshot?.phase==(int)TablePhase.Betting;
            if(!joined)
            {
                ClearSharedCards();
                PlaceText("Watch the table. Join when betting opens.",23,Gold,.25f,.10f,.75f,.17f);
                TableButton(tableFrame,"Join next round",.42f,.05f,.58f,.10f,()=>TableAction(ActionKind.TableJoin),betting);return;
            }
            if(selectedTable==TableKind.Blackjack)DrawSharedBlackjack(snapshot,mine,betting);
            else
            {
                string status=snapshot.phase==(int)TablePhase.Results?"NUMBER "+snapshot.number+" · Returned "+mine.payout.ToString("N0")+" gold · Net "+(mine.payout-mine.wager).ToString("+0;-0;0"):
                    "On table: "+(betting?tableDraft.Sum(b=>b.amount):mine.wager).ToString("N0")+" gold";
                PlaceText(status,24,Gold,.23f,.105f,.77f,.15f);
                if(betting)
                {
                    TableWager(.18f,.055f,.62f);
                    TableButton(tableFrame,"Spin now",.64f,.055f,.74f,.105f,()=>TableAction(ActionKind.TableStart),mine.wager>0&&!draftDirty&&!betInFlight,false,true);
                    TableButton(tableFrame,"Refund all",.755f,.055f,.87f,.105f,()=>{tableDraft.Clear();draftVersion++;draftDirty=true;tableMessage="Refund requested…";DrawTableControls();},true,true);
                    tableHover=PlaceText("Click felt: place chip · Right-click: remove chip · No confirmation needed",18,Color.white,.22f,.15f,.78f,.19f);
                }
            }
        }
        private void DrawSharedBlackjack(TableSnapshot snapshot,TableSeatState mine,bool betting)
        {
            if(sharedCardRound!=snapshot.round){ClearSharedCards();sharedCardRound=snapshot.round;}
            string players=string.Join("\n",snapshot.seats.Select(s=>(s.id==snapshot.turn?"▶ ":"")+s.name+" · "+s.wager));
            var names=PlaceText(players,18,Muted,.015f,.53f,.17f,.83f);names.enableWordWrapping=true;
            if(betting)
            {
                ClearSharedCards();
                PlaceText("Gold on table: "+mine.wager.ToString("N0"),25,Gold,.30f,.155f,.70f,.20f);
                TableWager(.18f,.055f,.62f);
                TableButton(tableFrame,"Add "+tableBet.ToString("N0")+" gold",.64f,.055f,.755f,.105f,()=>ChangeBlackjackWager(false),
                    (long)mine.wager+tableBet<=Plugin.MaximumBet&&CasinoPlugin.LocalMaster.money>=(uint)tableBet,false,true);
                TableButton(tableFrame,"Refund all",.77f,.055f,.88f,.105f,()=>TableAction(ActionKind.TableBet,0),mine.wager>0);
                TableButton(tableFrame,"Deal now",.43f,.108f,.57f,.153f,()=>TableAction(ActionKind.TableStart),snapshot.seats.All(s=>s.wager>0));
                return;
            }
            var dealer=snapshot.dealer??Array.Empty<int>();bool hidden=dealer.Any(c=>c<0);
            PlaceText("Total wager paid: "+mine.wager.ToString("N0")+" gold",20,Gold,.02f,.87f,.22f,.92f);
            PlaceText("DEALER · "+(hidden?Games.Value(dealer.Where(c=>c>=0))+" + ?":Games.Value(dealer).ToString()),22,Gold,.30f,.845f,.70f,.885f);
            SharedCardRow("dealer",dealer,.50f,.68f,.56f);
            bool split=mine.secondCards?.Length>0,turn=snapshot.phase==(int)TablePhase.Playing&&snapshot.turn==MyId&&!mine.done;
            if(split)
            {
                PlaceText((mine.activeHand==0&&turn?"▶ ":"")+"HAND 1 · "+mine.total+" · "+mine.handWager+" gold",23,mine.activeHand==0?Gold:Color.white,.18f,.325f,.45f,.38f);
                PlaceText((mine.activeHand==1&&turn?"▶ ":"")+"HAND 2 · "+mine.secondTotal+" · "+mine.secondWager+" gold",23,mine.activeHand==1?Gold:Color.white,.47f,.325f,.75f,.38f);
                SharedCardRow("first",mine.cards,.32f,.135f,.28f);SharedCardRow("second",mine.secondCards,.62f,.135f,.28f);
            }
            else
            {
                PlaceText("YOUR HAND · "+mine.total+" · "+mine.wager+" gold",24,Gold,.25f,.325f,.75f,.38f);
                SharedCardRow("first",mine.cards,.50f,.135f,.55f);
            }
            var cards=mine.activeHand==1?mine.secondCards:mine.cards;int stake=mine.activeHand==1?mine.secondWager:mine.handWager;
            bool two=cards?.Length==2,canSplit=two&&!split&&cards[0]%13==cards[1]%13;
            bool aces=split&&mine.cards[0]%13==0;
            TableButton(tableFrame,"Hit",.81f,.325f,.93f,.38f,()=>TableAction(ActionKind.TableHit),turn,false,true);
            TableButton(tableFrame,"Stand",.81f,.26f,.93f,.315f,()=>TableAction(ActionKind.TableStand),turn);
            TableButton(tableFrame,"Double +"+stake,.81f,.195f,.93f,.25f,()=>TableAction(ActionKind.TableDouble),turn&&two&&!aces&&CasinoPlugin.LocalMaster.money>=(uint)stake);
            TableButton(tableFrame,"Split +"+stake,.81f,.13f,.93f,.185f,()=>TableAction(ActionKind.TableSplit),turn&&canSplit&&CasinoPlugin.LocalMaster.money>=(uint)stake);
            if(snapshot.phase==(int)TablePhase.Results)
                PlaceText(mine.result+" · Returned "+mine.payout+" · Net "+(mine.payout-mine.wager).ToString("+0;-0;0"),24,mine.payout>mine.wager?Gold:Color.white,.18f,.06f,.78f,.12f);
            else PlaceText(turn?"25 seconds per hand · Use the buttons beside your cards":"Watch the dealer and other players while waiting for your turn",18,Muted,.22f,.07f,.77f,.12f);
        }
        private void SharedCardRow(string key,int[] cards,float centre,float y,float width)
        {
            if(cards==null||cards.Length==0)return;
            float step=Mathf.Min(.056f,(width-.052f)/Math.Max(1,cards.Length-1));
            for(int i=0;i<cards.Length;i++)
            {
                float x=centre+(i-(cards.Length-1)*.5f)*step;
                string id=key+i;
                if(!sharedCardImages.TryGetValue(id,out var image)||!image)
                    sharedCardImages[id]=image=Artwork(sharedCardLayer,"SharedPlayingCard",cards[i]<0?"back":"card"+cards[i]);
                Anchors(image.rectTransform,x-.026f,y,x+.026f,y+.185f);
                bool changed=!sharedCardValues.TryGetValue(id,out int previous)||previous!=cards[i];
                sharedCardValues[id]=cards[i];if(changed)StartCoroutine(SharedDealCard(image,cards[i],previous<0,i*.055f));
            }
        }
        private void ClearSharedCards()
        {
            foreach(var image in sharedCardImages.Values)if(image){image.gameObject.SetActive(false);Destroy(image.gameObject);}
            sharedCardImages.Clear();sharedCardValues.Clear();
        }
        private IEnumerator SharedDealCard(Image image,int card,bool flip,float delay)
        {
            var rect=image.rectTransform;var home=rect.anchoredPosition;
            image.sprite=art.Get("back");rect.localScale=Vector3.zero;
            for(float time=0;time<delay;time+=Time.unscaledDeltaTime){if(!image)yield break;yield return null;}
            for(float time=0;time<.45f;time+=Time.unscaledDeltaTime)
            {
                if(!image)yield break;float p=Mathf.Clamp01(time/.45f),ease=1-Mathf.Pow(1-p,3);
                rect.anchoredPosition=home+new Vector2(120*(1-ease),75*(1-ease));
                rect.localScale=new Vector3(flip?Mathf.Abs(2*p-1):ease,ease,1);
                if(p>.5f)image.sprite=art.Get(card<0?"back":"card"+card);yield return null;
            }
            if(image){rect.anchoredPosition=home;rect.localScale=Vector3.one;image.sprite=art.Get(card<0?"back":"card"+card);}
        }
        private TextMeshProUGUI PlaceText(string text,float size,Color color,float x0,float y0,float x1,float y1)
        {var label=Label(tableFrame,text,size,color);Anchors(label.rectTransform,x0,y0,x1,y1);return label;}
        private void TableWager(float x,float y,float end)
        {
            float w=end-x;
            int minimum=Plugin.MinimumBet,maximum=Plugin.MaximumBet;
            TableButton(tableFrame,"−",x,y,x+w*.07f,y+.05f,()=>{tableBet=Math.Max(minimum,tableBet-minimum);DrawTableControls();},tableBet>minimum,true);
            PlaceText("Chip: "+tableBet.ToString("N0")+" gold",20,Gold,x+w*.075f,y,x+w*.28f,y+.05f);
            TableButton(tableFrame,"+",x+w*.29f,y,x+w*.36f,y+.05f,()=>{tableBet=(int)Math.Min(maximum,(long)tableBet+minimum);DrawTableControls();},tableBet<maximum,true);
            var values=WagerRules.Denominations(minimum,maximum);
            for(int i=0;i<values.Length;i++)
            {
                int value=values[i];float start=.38f+i*.155f;
                TableButton(tableFrame,value.ToString("N0"),x+w*start,y,x+w*(start+.145f),y+.05f,()=>{tableBet=value;DrawTableControls();},value>=Plugin.MinimumBet&&value<=Plugin.MaximumBet,true,tableBet==value);
            }
        }
        private Button TableButton(Transform parent,string text,float x0,float y0,float x1,float y1,UnityEngine.Events.UnityAction action,bool enabled,bool local=false,bool primary=false)
        {
            var rect=Rect(parent,"SharedTableButton");Anchors(rect,x0,y0,x1,y1);
            var image=rect.gameObject.AddComponent<Image>();image.color=new Color(.08f,.14f,.18f,.90f);
            var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=image;button.interactable=enabled&&(local||!Plugin.Waiting);button.onClick.AddListener(action);
            sharedButtons.Add(Tuple.Create(button,enabled,local));
            var label=Label(rect,text,21,Color.white);Anchors(label.rectTransform,.055f,.06f,.945f,.94f);label.enableWordWrapping=false;label.enableAutoSizing=true;label.fontSizeMin=14;label.fontSizeMax=21;
            TableButtonStyle.Apply(button,image,label,primary);return button;
        }
    }
}
