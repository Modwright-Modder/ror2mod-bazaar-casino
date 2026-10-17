using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BazaarCasino
{
    internal sealed partial class CasinoUI
    {
        private readonly CasinoArt art = new CasinoArt();
        private readonly List<Image> playerCards = new List<Image>(), dealerCards = new List<Image>();
        private int[] shownPlayer = Array.Empty<int>(), shownDealer = Array.Empty<int>();
        private TextMeshProUGUI dealerTitle, playerTitle;
        private readonly List<RouletteBet> chips = new List<RouletteBet>();
        private RectTransform wheel, ball, lever;
        private TextMeshProUGUI wheelResult;
        private readonly List<ReelView> reels = new List<ReelView>();
        private sealed class ReelView { internal RectTransform strip; internal readonly List<Image> symbols = new List<Image>(); }
        private Image Artwork(Transform parent, string name, string asset)
        {
            var image=Rect(parent,name).gameObject.AddComponent<Image>(); image.sprite=art.Get(asset); image.preserveAspect=true; image.raycastTarget=false; return image;
        }
        private void RememberCards(CasinoState state)
        { shownPlayer=state?.player?.ToArray() ?? Array.Empty<int>(); shownDealer=state?.dealer?.ToArray() ?? Array.Empty<int>(); }
        private void DrawBlackjack()
        {
            var felt=Rect(content,"BlackjackFelt"); Stretch(felt); felt.gameObject.AddComponent<Image>().color=new Color(.045f,.19f,.15f);
            var rules=Label(content,"6 decks  •  Dealer stands on all 17s  •  Blackjack pays 3:2",17,Muted); Anchors(rules.rectTransform,0,.92f,1,1);
            bool active=State?.handActive==true;
            dealerTitle=Label(content,"DEALER"+(!active && State?.dealer!=null ? "  ·  "+State.dealerTotal : ""),20,Gold);
            Anchors(dealerTitle.rectTransform,0,.83f,1,.92f);
            DrawCards(State?.dealer,.53f,.82f,dealerCards);
            playerTitle=Label(content,"YOUR HAND"+(State?.player!=null ? "  ·  "+State.playerTotal : ""),20,Gold); Anchors(playerTitle.rectTransform,0,.42f,1,.52f);
            DrawCards(State?.player,.12f,.41f,playerCards);
            var deck=Artwork(content,"Deck","back"); Anchors(deck.rectTransform,.89f,.48f,.97f,.78f); deck.rectTransform.localEulerAngles=new Vector3(0,0,-12);
            if(active)
            {
                PlaceButton("Hit",.10f,0,.32f,.105f,()=>Act(ActionKind.Hit));
                PlaceButton("Stand",.39f,0,.61f,.105f,()=>Act(ActionKind.Stand));
                var dbl=PlaceButton("Double",.68f,0,.90f,.105f,()=>Act(ActionKind.Double)); dbl.interactable=!animating&&!Plugin.Waiting&&State.canDouble;
            }
            else PlaceButton("Deal — "+bet.ToString("N0")+" gold",.30f,0,.70f,.105f,()=>Act(ActionKind.Deal));
        }
        private void DrawCards(int[] cards,float y0,float y1,List<Image> images)
        {
            images.Clear(); if(cards==null || cards.Length==0) return;
            float width=Mathf.Min(.087f,.76f/cards.Length),start=.48f-cards.Length*width/2;
            for(int i=0;i<cards.Length;i++)
            {
                var image=Artwork(content,"PlayingCard",cards[i]<0 ? "back" : "card"+cards[i]);
                Anchors(image.rectTransform,start+i*width+.003f,y0,start+(i+1)*width-.003f,y1); images.Add(image);
            }
        }
        private IEnumerator DealCard(Image image,int card,bool flipOnly=false)
        {
            image.gameObject.SetActive(true); var rect=image.rectTransform;
            if(flipOnly)
            {
                image.sprite=art.Get("back");
                for(float t=0;t<.42f;t+=Time.unscaledDeltaTime)
                {
                    float p=t/.42f; rect.localScale=new Vector3(Mathf.Abs(1-2*p),1,1);
                    if(p>=.5f) image.sprite=art.Get(card<0 ? "back" : "card"+card);
                    yield return null;
                }
            }
            else
            {
                image.sprite=art.Get(card<0 ? "back" : "card"+card);
                for(float t=0;t<.34f;t+=Time.unscaledDeltaTime)
                {
                    float p=1-Mathf.Pow(1-Mathf.Clamp01(t/.34f),3);
                    rect.anchoredPosition=new Vector2(500,140)*(1-p);
                    rect.localEulerAngles=new Vector3(0,0,-25*(1-p)); rect.localScale=Vector3.one*(.65f+.35f*p);
                    yield return null;
                }
            }
            rect.anchoredPosition=Vector2.zero; rect.localScale=Vector3.one; rect.localEulerAngles=Vector3.zero;
            yield return new WaitForSecondsRealtime(.10f);
        }
        private IEnumerator AnimateCards(CasinoState state)
        {
            if(pageIndex!=0) yield break;
            dealerTitle.text="DEALER"; playerTitle.text="YOUR HAND";
            // A new deal can coincidentally repeat the same two cards.
            bool fresh=lastAction==ActionKind.Deal;
            int oldPlayer=fresh ? 0 : shownPlayer.Length, oldDealer=fresh ? 0 : shownDealer.Length;
            for(int i=oldPlayer;i<playerCards.Count;i++) playerCards[i].gameObject.SetActive(false);
            for(int i=oldDealer;i<dealerCards.Count;i++) dealerCards[i].gameObject.SetActive(false);
            bool reveal=!fresh && shownDealer.Length>1 && shownDealer[1]<0 && state.dealer.Length>1 && state.dealer[1]>=0;
            if(reveal) dealerCards[1].sprite=art.Get("back");
            if(fresh)
            {
                for(int i=0;i<2;i++)
                {
                    yield return DealCard(playerCards[i],state.player[i]);
                    yield return DealCard(dealerCards[i],i==1 ? -1 : state.dealer[i]);
                }
                if(!state.handActive) yield return DealCard(dealerCards[1],state.dealer[1],true);
            }
            else
            {
                for(int i=oldPlayer;i<playerCards.Count;i++) yield return DealCard(playerCards[i],state.player[i]);
                if(reveal) yield return DealCard(dealerCards[1],state.dealer[1],true);
                for(int i=Math.Max(2,oldDealer);i<dealerCards.Count;i++) yield return DealCard(dealerCards[i],state.dealer[i]);
            }
            RememberCards(state);
        }
        private ActionKind lastAction;
        private void DrawRoulette()
        {
            var instructions=Label(content,"Click a spot to add a chip • Right-click to remove one • Each chip = "+bet.ToString("N0")+" gold • Single zero",16,Muted);
            Anchors(instructions.rectTransform,0,.93f,1,1);
            var holder=Rect(content,"WheelHolder"); Anchors(holder,0,.22f,.30f,.93f);
            var area=Rect(holder,"WheelArea"); Stretch(area);
            var aspect=area.gameObject.AddComponent<AspectRatioFitter>(); aspect.aspectMode=AspectRatioFitter.AspectMode.FitInParent; aspect.aspectRatio=1;
            wheel=Rect(area,"Rotor"); Stretch(wheel);
            if(!wheelTexture) wheelTexture=MakeWheel(); wheel.gameObject.AddComponent<RawImage>().texture=wheelTexture;
            for(int i=0;i<37;i++)
            {
                float a=i*Mathf.PI*2/37;
                var label=Label(wheel,Games.Wheel[i].ToString(),14,Color.white);
                label.rectTransform.anchorMin=label.rectTransform.anchorMax=new Vector2(.5f+Mathf.Sin(a)*.43f,.5f+Mathf.Cos(a)*.43f);
                label.rectTransform.sizeDelta=new Vector2(22,22);
                label.rectTransform.localEulerAngles=new Vector3(0,0,-i*360f/37);
            }
            wheelResult=Label(area,lastWheel<0 ? "SPIN" : lastWheel.ToString(),34,Gold); Anchors(wheelResult.rectTransform,.3f,.32f,.7f,.68f);
            // Fixed bowl deflectors above the rotating pocket ring.
            for(int i=0;i<8;i++)
            {
                float angle=i*Mathf.PI/4;
                var deflector=Rect(area,"BowlDeflector"); deflector.anchorMin=deflector.anchorMax=new Vector2(.5f+Mathf.Sin(angle)*.452f,.5f+Mathf.Cos(angle)*.452f);
                deflector.sizeDelta=new Vector2(3,7);deflector.localEulerAngles=new Vector3(0,0,-i*45);
                var image=deflector.gameObject.AddComponent<Image>();image.color=Gold;image.raycastTarget=false;
            }
            ball=Artwork(area,"Ball","ball").rectTransform; ball.anchorMin=ball.anchorMax=new Vector2(.5f,.5f); ball.sizeDelta=new Vector2(11,11);
            ball.gameObject.SetActive(animating || lastWheel>=0);
            if(lastWheel>=0) PositionBall(new BallFrame(0,Array.IndexOf(Games.Wheel,lastWheel)*Math.PI*2/37,.8,0));
            var table=Rect(content,"RouletteTable"); Anchors(table,.32f,.22f,1,.91f);
            table.gameObject.AddComponent<Image>().color=new Color(.045f,.24f,.17f);
            // Three rows in the reference image: 3,6,… / 2,5,… / 1,4,…. European 0.
            Spot(table,"0",6,0,0,.34f,.065f,.98f,new Color(.07f,.40f,.25f));
            for(int col=0;col<12;col++) for(int row=0;row<3;row++)
            {
                int number=col*3+3-row; float x=.07f+col*.0712f,y=.765f-row*.215f;
                Spot(table,number.ToString(),6,number,x,y,x+.068f,y+.205f,Games.IsRed(number) ? new Color(.64f,.12f,.17f) : new Color(.055f,.07f,.09f));
            }
            for(int row=0;row<3;row++)
            { float y=.765f-row*.215f; Spot(table,"2:1",12-row,0,.93f,y,.998f,y+.205f,new Color(.08f,.29f,.21f)); }
            for(int i=0;i<3;i++) Spot(table,new[]{"1st 12","2nd 12","3rd 12"}[i],7+i,0,.07f+i*.285f,.175f,.07f+(i+1)*.285f-.005f,.325f,new Color(.08f,.29f,.21f));
            string[] labels={"1–18","Even","Red","Black","Odd","19–36"}; int[] choices={4,2,0,1,3,5};
            for(int i=0;i<6;i++) Spot(table,labels[i],choices[i],0,.07f+i*.1425f,.01f,.07f+(i+1)*.1425f-.004f,.16f,
                i==2 ? new Color(.64f,.12f,.17f) : i==3 ? new Color(.055f,.07f,.09f) : new Color(.08f,.29f,.21f));
            var total=Label(content,"On table: "+chips.Sum(c=>c.amount).ToString("N0")+" gold",20,Gold); Anchors(total.rectTransform,.32f,.115f,.65f,.21f);
            PlaceButton("Undo chip",.02f,.015f,.15f,.105f,UndoChip);
            PlaceButton("Clear",.17f,.015f,.29f,.105f,()=>{ if(animating||Plugin.Waiting)return; chips.Clear(); chipHistory.Clear(); DrawPage(); });
            var spinButton=PlaceButton("SPIN",.68f,.025f,.99f,.15f,SpinRoulette); spinButton.interactable=!animating&&!Plugin.Waiting&&chips.Count>0;
        }
        private readonly List<Tuple<int,int,int>> chipHistory = new List<Tuple<int,int,int>>();
        private void SpinRoulette()
        {
            if(chips.Count==0 || animating || Plugin.Waiting) return;
            lastAction=ActionKind.Roulette; Notify("Launching ball…");
            Plugin.Send(ActionKind.Roulette,0,0,0,chips.Select(c=>new RouletteBet { choice=c.choice,number=c.number,amount=c.amount }).ToArray());
        }
        private void UndoChip()
        {
            if(animating||Plugin.Waiting||chipHistory.Count==0)return;
            var entry=chipHistory[chipHistory.Count-1]; chipHistory.RemoveAt(chipHistory.Count-1); RemoveChip(entry.Item1,entry.Item2,entry.Item3); DrawPage();
        }
        private void RemoveChip(int choice,int number,int amount)
        {
            var spot=chips.FirstOrDefault(c=>c.choice==choice&&c.number==number);
            if(spot!=null) { spot.amount-=Math.Min(spot.amount,amount); if(spot.amount==0) chips.Remove(spot); }
        }
        private void Spot(Transform parent,string text,int choice,int number,float x0,float y0,float x1,float y1,Color color)
        {
            var rect=Rect(parent,"Spot_"+choice+"_"+number); Anchors(rect,x0,y0,x1,y1);
            var image=rect.gameObject.AddComponent<Image>(); image.color=color;
            var outline=rect.gameObject.AddComponent<Outline>(); outline.effectColor=new Color(.8f,.85f,.77f,.6f); outline.effectDistance=new Vector2(1,-1);
            var button=rect.gameObject.AddComponent<Button>(); button.targetGraphic=image;
            button.interactable=!animating&&!Plugin.Waiting;
            var label=Label(rect,text,17,Color.white); Anchors(label.rectTransform,0,.12f,1,.95f); label.enableAutoSizing=true;label.fontSizeMin=11;label.fontSizeMax=17;
            button.onClick.AddListener(()=>AddChip(choice,number));
            var right=rect.gameObject.AddComponent<CasinoRightClick>(); right.clicked=()=>
            {
                if(animating||Plugin.Waiting)return;
                int index=chipHistory.FindLastIndex(c=>c.Item1==choice&&c.Item2==number);
                if(index>=0){ var entry=chipHistory[index];chipHistory.RemoveAt(index);RemoveChip(choice,number,entry.Item3);DrawPage(); }
            };
            var placed=chips.FirstOrDefault(c=>c.choice==choice&&c.number==number);
            if(placed!=null)
            {
                var coin=Artwork(rect,"PlacedChip","chip"); Anchors(coin.rectTransform,.38f,0,.99f,.59f);
                var amount=Label(coin.transform,Compact(placed.amount),11,new Color(.12f,.08f,.025f)); Stretch(amount.rectTransform); amount.fontStyle=FontStyles.Bold;
            }
        }
        private static string Compact(int amount) => amount>=1000000 ? (amount/1000000f).ToString("0.#")+"m" : amount>=1000 ? (amount/1000f).ToString("0.#")+"k" : amount.ToString();
        private void AddChip(int choice,int number)
        {
            if(animating||Plugin.Waiting)return;
            long total=(long)chips.Sum(c=>c.amount)+bet;
            if(total>(State?.maxBet ?? 100000) || total>(CasinoPlugin.LocalMaster ? CasinoPlugin.LocalMaster.money : 0)) { Notify("Not enough gold or table limit reached."); return; }
            if(State?.handActive==true){Notify("Finish your blackjack hand first.");return;}
            var spot=chips.FirstOrDefault(c=>c.choice==choice&&c.number==number);
            if(spot==null) { spot=new RouletteBet { choice=choice,number=number }; chips.Add(spot); }
            spot.amount+=bet; chipHistory.Add(Tuple.Create(choice,number,bet)); DrawPage();
            var badge=content.GetComponentsInChildren<CasinoRightClick>().FirstOrDefault(c=>c.name=="Spot_"+choice+"_"+number)?.transform.Find("PlacedChip") as RectTransform;
            if(badge)StartCoroutine(DropChip(badge));
        }
        private IEnumerator DropChip(RectTransform chip)
        {
            for(float t=0;t<.22f;t+=Time.unscaledDeltaTime)
            {
                if(!chip)yield break;
                float p=Mathf.Clamp01(t/.22f);chip.anchoredPosition=new Vector2(0,(1-p)*(1-p)*50);chip.localScale=Vector3.one*(1+.2f*Mathf.Sin(p*Mathf.PI));yield return null;
            }
            if(chip){chip.anchoredPosition=Vector2.zero;chip.localScale=Vector3.one;}
        }
        private void PositionBall(BallFrame frame)
        {
            if(!ball||!wheel) return;
            // Polar angle measured clockwise from twelve o'clock; Unity Z rotates CCW.
            wheel.localEulerAngles=new Vector3(0,0,-frame.wheel*Mathf.Rad2Deg);
            ball.anchorMin=ball.anchorMax=new Vector2(.5f+Mathf.Sin(frame.angle)*frame.radius*.5f,.5f+Mathf.Cos(frame.angle)*frame.radius*.5f);
            ball.anchoredPosition=Vector2.zero;
        }
        private IEnumerator AnimateWheel(CasinoState state)
        {
            var physics=new RouletteSpin(state.spinSeed);
            if(physics.number!=state.wheelNumber) Debug.LogWarning("[BazaarCasino] Roulette replay differs from authoritative result.");
            float start=Time.unscaledTime; int index=0;
            while(Time.unscaledTime-start<physics.duration)
            {
                float t=Time.unscaledTime-start;
                while(index+1<physics.frames.Count && physics.frames[index+1].time<=t) index++;
                var a=physics.frames[index]; var b=physics.frames[Math.Min(index+1,physics.frames.Count-1)];
                float p=b.time>a.time ? Mathf.Clamp01((t-a.time)/(b.time-a.time)) : 1;
                PositionBall(new BallFrame(t,Mathf.Lerp(a.angle,b.angle,p),Mathf.Lerp(a.radius,b.radius,p),Mathf.Lerp(a.wheel,b.wheel,p)));
                if(wheelResult) wheelResult.text="…";
                yield return null;
            }
            PositionBall(physics.frames[physics.frames.Count-1]);
            lastWheel=state.wheelNumber; if(wheelResult)wheelResult.text=lastWheel.ToString();
            // Leave the ball and rotor in their final physical positions for the result.
        }
        private void DrawSlots()
        {
            reels.Clear();
            var cabinet=Rect(content,"SlotCabinet"); Anchors(cabinet,.10f,.17f,.88f,.98f); cabinet.gameObject.AddComponent<Image>().color=new Color(.11f,.12f,.19f);
            var border=cabinet.gameObject.AddComponent<Outline>();border.effectColor=Gold;border.effectDistance=new Vector2(3,-3);
            var title=Label(cabinet,"BAZAAR JACKPOT",31,Gold); Anchors(title.rectTransform,.05f,.80f,.95f,.99f);
            for(int i=0;i<3;i++)
            {
                var viewport=Rect(cabinet,"ReelWindow");Anchors(viewport,.08f+i*.29f,.16f,.34f+i*.29f,.78f);
                viewport.gameObject.AddComponent<Image>().color=new Color(.045f,.065f,.09f);
                viewport.gameObject.AddComponent<RectMask2D>();
                var reel=new ReelView { strip=Rect(viewport,"SlidingReel") };Stretch(reel.strip);reels.Add(reel);
                for(int row=0;row<3;row++)
                {
                    var icon=Artwork(reel.strip,"ReelSymbol","slot"+(lastReels==null ? i : lastReels[i]));
                    // One cell per symbol. The previous upper bound made each
                    // image 1.88 cells tall and centred on the cell boundary.
                    Anchors(icon.rectTransform,.08f,ReelLayout.Lower(row),.92f,ReelLayout.Upper(row));reel.symbols.Add(icon);
                }
            }
            var line=Rect(cabinet,"PayLine");Anchors(line,.07f,.468f,.93f,.472f);line.gameObject.AddComponent<Image>().color=new Color(1,.83f,.35f,.35f);line.GetComponent<Image>().raycastTarget=false;
            var hint=Label(cabinet,"3 matching symbols win • Any pair returns your wager",15,Muted);Anchors(hint.rectTransform,.02f,.025f,.98f,.13f);
            var rail=Rect(content,"LeverRail");Anchors(rail,.92f,.33f,.937f,.72f);rail.gameObject.AddComponent<Image>().color=Gold;
            lever=Rect(content,"PullLever");Anchors(lever,.895f,.63f,.965f,.82f);
            var knob=lever.gameObject.AddComponent<Image>();knob.sprite=art.Get("ball");knob.color=new Color(.95f,.28f,.36f);knob.preserveAspect=true;
            var pull=lever.gameObject.AddComponent<Button>();pull.targetGraphic=knob;pull.onClick.AddListener(()=>Act(ActionKind.Slots));pull.interactable=!animating&&!Plugin.Waiting;
            var pullLabel=Label(content,"PULL",16,Gold);Anchors(pullLabel.rectTransform,.885f,.25f,.98f,.33f);
            var payouts=Label(content,"Cherry 3×   Lemon 5×   Bell 8×   Star 12×   Seven 25×   Moon 60×",17,Muted);Anchors(payouts.rectTransform,.02f,.09f,.98f,.16f);
            PlaceButton("Pull lever — "+bet.ToString("N0")+" gold",.30f,0,.70f,.085f,()=>Act(ActionKind.Slots));
        }
        private IEnumerator AnimateReels(CasinoState state)
        {
            if(pageIndex!=2)yield break;
            Canvas.ForceUpdateCanvases(); var rng=new System.Random(state.requestId.GetHashCode());
            int[,] strips=new int[3,43];
            for(int i=0;i<3;i++)for(int k=0;k<43;k++)strips[i,k]=rng.Next(6);
            float[] durations={2.6f,3.15f,3.7f};int[] turns={24,31,38};
            for(int i=0;i<3;i++)strips[i,turns[i]]=state.reels[i];
            bool[] stopped=new bool[3]; float start=Time.unscaledTime;
            while(Time.unscaledTime-start<3.85f)
            {
                float t=Time.unscaledTime-start;
                if(lever)
                {
                    float pull=t<.25f ? t/.25f : 1-Mathf.Clamp01((t-.25f)/.4f);
                    lever.anchoredPosition=new Vector2(0,-115*pull);lever.localEulerAngles=new Vector3(0,0,10*pull);
                }
                for(int i=0;i<reels.Count;i++)
                {
                    float p=Mathf.Clamp01(t/durations[i]), distance=turns[i]*(1-Mathf.Pow(1-p,3));
                    int cell=Mathf.Min(turns[i],Mathf.FloorToInt(distance));float fraction=distance-cell;
                    // The next symbol above the payline slides downward into its place.
                    for(int row=0;row<3;row++) reels[i].symbols[row].sprite=art.Get("slot"+strips[i,Mathf.Clamp(cell+row-1,0,42)]);
                    reels[i].strip.anchoredPosition=new Vector2(0,-fraction*reels[i].strip.rect.height);
                    if(p>=1 && !stopped[i]){stopped[i]=true; StartCoroutine(ReelBounce(reels[i].strip));}
                }
                yield return null;
            }
            lastReels=state.reels; if(lever){lever.anchoredPosition=Vector2.zero;lever.localEulerAngles=Vector3.zero;}
            foreach(var reel in reels)reel.strip.anchoredPosition=Vector2.zero;
        }
        private IEnumerator ReelBounce(RectTransform strip)
        {
            for(float t=0;t<.16f;t+=Time.unscaledDeltaTime)
            { if(!strip)yield break; strip.localScale=new Vector3(1,1+.05f*Mathf.Sin(t/.16f*Mathf.PI),1);yield return null; }
            if(strip)strip.localScale=Vector3.one;
        }
        private IEnumerator AnimateGame(CasinoState state)
        {
            if(state.game==1)yield return AnimateCards(state);
            else if(state.game==2)yield return AnimateWheel(state);
            else yield return AnimateReels(state);
            Notify(ResultText(state));
            bool completed=state.game!=1 || !state.handActive;
            if(completed && state.payout>state.wager)
                yield return Celebrate(state.game==3&&state.reels.Distinct().Count()==1&&state.reels[0]>=4,state.payout-state.wager);
            else if(completed && state.payout==state.wager && state.payout>0)
                yield return ResultBanner("WAGER RETURNED",Muted,.7f);
            animating=false;animation=null; RememberCards(state);
            // Rebuild interactive controls after animation. Roulette preserves the
            // final dynamics on its existing wheel instead of resetting its rotation.
            if(state.game==2 && pageIndex==1)
            {
                foreach(var button in content.GetComponentsInChildren<Button>(true)) button.interactable=true;
            }
            else DrawPage();
        }
        private IEnumerator ResultBanner(string text,Color color,float duration)
        {
            var banner=Label(content,text,40,color);Anchors(banner.rectTransform,.20f,.43f,.80f,.62f);banner.fontStyle=FontStyles.Bold;
            banner.outlineWidth=.2f;banner.outlineColor=new Color(0,0,0,.9f);
            for(float t=0;t<duration;t+=Time.unscaledDeltaTime)
            { banner.rectTransform.localScale=Vector3.one*(1+.07f*Mathf.Sin(t*10));yield return null; }
            Destroy(banner.gameObject);
        }
        private IEnumerator Celebrate(bool jackpot,int net)
        {
            var effects=Rect(content,"WinEffects");Stretch(effects);
            var overlay=effects.gameObject.AddComponent<Image>();overlay.color=new Color(1,.8f,.25f,.08f);overlay.raycastTarget=false;
            var banner=Label(effects,(jackpot ? "JACKPOT!" : "YOU WIN")+"\n+"+net.ToString("N0")+" GOLD",jackpot?48:39,Gold);
            Anchors(banner.rectTransform,.20f,.37f,.80f,.67f);banner.enableWordWrapping=true;banner.fontStyle=FontStyles.Bold;banner.outlineWidth=.25f;banner.outlineColor=Color.black;
            var random=new System.Random(); var particles=new List<RectTransform>();
            int count=jackpot?58:28;
            var speed=new float[count];var phase=new float[count];
            for(int i=0;i<count;i++)
            {
                var coin=Artwork(effects,"WinCoin", "chip");var rect=coin.rectTransform;rect.anchorMin=rect.anchorMax=new Vector2((float)random.NextDouble(),1.15f);rect.sizeDelta=new Vector2(18+random.Next(16),18+random.Next(16));
                particles.Add(rect);speed[i]=170+random.Next(280);phase[i]=(float)random.NextDouble()*6;
            }
            float duration=jackpot?2.4f:1.5f;
            for(float t=0;t<duration;t+=Time.unscaledDeltaTime)
            {
                overlay.color=new Color(1,.8f,.25f,.04f+.08f*Mathf.Abs(Mathf.Sin(t*6)));
                banner.rectTransform.localScale=Vector3.one*(1+.065f*Mathf.Sin(t*8)*Mathf.Exp(-t));
                for(int i=0;i<count;i++) {particles[i].anchoredPosition=new Vector2(Mathf.Sin(t*3+phase[i])*35,-t*speed[i]);particles[i].localEulerAngles=new Vector3(0,0,t*100+phase[i]*90);}
                yield return null;
            }
            effects.gameObject.SetActive(false);Destroy(effects.gameObject);
        }
    }
    internal sealed class CasinoRightClick : MonoBehaviour, UnityEngine.EventSystems.IPointerClickHandler
    {
        internal Action clicked;
        public void OnPointerClick(UnityEngine.EventSystems.PointerEventData e) { if(e.button==UnityEngine.EventSystems.PointerEventData.InputButton.Right)clicked?.Invoke(); }
    }
}
