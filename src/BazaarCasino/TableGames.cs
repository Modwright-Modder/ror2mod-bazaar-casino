using System;
using System.Collections.Generic;
using System.Linq;

namespace BazaarCasino
{
    public static class ReelLayout
    {
        public static float Lower(int row) => row-1+.06f;
        public static float Upper(int row) => row-.06f;
    }
    public enum TableKind : byte { Blackjack=1, Roulette=2 }
    public static class RoundTiming { public const double BettingSeconds=5, ResultsSeconds=1; }
    public enum TablePhase { Betting, Playing, Results }
    public sealed class SharedSeat
    {
        public uint id;
        public string name;
        public int slot, wager, payout, activeHand, handWager, secondWager;
        public bool firstDone, secondDone, splitAces;
        public readonly List<int> secondCards = new List<int>();
        public List<int> ActiveCards => activeHand==1?secondCards:cards;
        public int ActiveStake => activeHand==1?secondWager:handWager;
        public bool done, paid;
        public string result;
        public readonly List<int> cards = new List<int>();
        public RouletteBet[] bets = Array.Empty<RouletteBet>();
    }
    // Host-owned rules independent of Unity. Gold debits/refunds are performed by
    // the adapter only after these state/range/turn checks succeed.
    public sealed class SharedBlackjack
    {
        public const int Capacity=8;
        public readonly List<SharedSeat> seats=new List<SharedSeat>();
        public readonly List<int> dealer=new List<int>(), deck=new List<int>();
        public TablePhase phase;
        public int round;
        public uint turn;
        public double deadline;
        public SharedSeat Find(uint id)=>seats.FirstOrDefault(s=>s.id==id);
        public bool Join(uint id,string name)
        {
            if(Find(id)!=null)return true;
            if(phase!=TablePhase.Betting || seats.Count>=Capacity)return false;
            int slot=Enumerable.Range(0,Capacity).First(i=>seats.All(s=>s.slot!=i));
            seats.Add(new SharedSeat{id=id,name=name,slot=slot});return true;
        }
        public bool CanBet(uint id)=>phase==TablePhase.Betting && Find(id)!=null;
        public bool CanStart=>phase==TablePhase.Betting && seats.Any(s=>s.wager>0);
        public void Bet(uint id,int wager,double now)
        {
            if(!CanBet(id)||wager<0)throw new InvalidOperationException("Betting is closed");
            Find(id).wager=wager;
            if(wager>0 && deadline==0)deadline=now+RoundTiming.BettingSeconds;
            if(seats.All(s=>s.wager==0))deadline=0;
        }
        public bool CanAct(uint id)=>phase==TablePhase.Playing && turn==id && Find(id)!=null && !Find(id).done;
        public bool CanDouble(uint id)=>CanAct(id)&&Find(id).ActiveCards.Count==2&&!Find(id).splitAces;
        public bool CanSplit(uint id)=>CanAct(id)&&Find(id).secondCards.Count==0&&Find(id).cards.Count==2&&Find(id).cards[0]%13==Find(id).cards[1]%13;
        public int ActionStake(uint id)=>Find(id)?.ActiveStake??0;
        public void Start(Random random,double now)
        {
            if(!CanStart)return;
            round++;phase=TablePhase.Playing;dealer.Clear();deck.Clear();
            for(int i=0;i<312;i++)deck.Add(i%52);
            for(int i=deck.Count-1;i>0;i--){int j=random.Next(i+1),c=deck[i];deck[i]=deck[j];deck[j]=c;}
            foreach(var seat in seats){seat.cards.Clear();seat.secondCards.Clear();seat.activeHand=0;seat.handWager=seat.wager;seat.secondWager=0;seat.firstDone=false;seat.secondDone=false;seat.splitAces=false;seat.done=seat.wager==0;seat.payout=0;seat.paid=false;seat.result=null;}
            for(int i=0;i<2;i++){foreach(var seat in seats.Where(s=>s.wager>0).OrderBy(s=>s.slot))seat.cards.Add(Draw());dealer.Add(Draw());}
            foreach(var seat in seats)if(seat.wager>0&&Games.Natural(seat.cards))seat.done=true;
            if(Games.Natural(dealer)){foreach(var seat in seats)seat.done=true;Settle(now);}
            else Next(now);
        }
        private int Draw(){int last=deck.Count-1,c=deck[last];deck.RemoveAt(last);return c;}
        public bool Hit(uint id,double now)
        {
            if(!CanAct(id))return false;var seat=Find(id);seat.ActiveCards.Add(Draw());
            if(Games.Value(seat.ActiveCards)>=21)Complete(seat,now);return true;
        }
        public bool Stand(uint id,double now)
        {
            if(!CanAct(id))return false;Complete(Find(id),now);return true;
        }
        public bool Double(uint id,double now)
        {
            if(!CanDouble(id))return false;var seat=Find(id);int extra=seat.ActiveStake;
            seat.wager+=extra;if(seat.activeHand==0)seat.handWager+=extra;else seat.secondWager+=extra;
            seat.ActiveCards.Add(Draw());Complete(seat,now);return true;
        }
        // One split into two hands; split aces receive one card each and stand.
        // A 21 after splitting pays 1:1, never the natural blackjack bonus.
        public bool Split(uint id,double now)
        {
            if(!CanSplit(id))return false;var seat=Find(id);
            seat.secondCards.Add(seat.cards[1]);seat.cards.RemoveAt(1);
            seat.secondWager=seat.handWager;seat.wager+=seat.secondWager;
            seat.splitAces=seat.cards[0]%13==0;
            seat.cards.Add(Draw());seat.secondCards.Add(Draw());
            seat.firstDone=seat.splitAces||Games.Value(seat.cards)==21;
            seat.secondDone=seat.splitAces||Games.Value(seat.secondCards)==21;
            Next(now);return true;
        }
        private void Complete(SharedSeat seat,double now)
        {if(seat.activeHand==0)seat.firstDone=true;else seat.secondDone=true;Next(now);}
        public int Leave(uint id,double now)
        {
            var seat=Find(id);if(seat==null)return 0;
            if(phase==TablePhase.Betting){int refund=seat.wager;seats.Remove(seat);if(seats.All(s=>s.wager==0))deadline=0;return refund;}
            if(phase==TablePhase.Playing){seat.firstDone=seat.secondDone=seat.done=true;if(turn==id)Next(now);}return 0;
        }
        private void Next(double now)
        {
            foreach(var seat in seats.Where(s=>s.wager>0&&!s.done))
            {
                if(!seat.firstDone)seat.activeHand=0;
                else if(seat.secondCards.Count>0&&!seat.secondDone)seat.activeHand=1;
                else seat.done=true;
            }
            var next=seats.Where(s=>s.wager>0&&!s.done).OrderBy(s=>s.slot).FirstOrDefault();
            if(next==null){Settle(now);return;}turn=next.id;deadline=now+25;
        }
        private int HandPayout(List<int> cards,int stake,bool naturalEligible,out string result)
        {
            int player=Games.Value(cards),d=Games.Value(dealer);bool natural=naturalEligible&&Games.Natural(cards);
            if(player>21){result="Bust";return 0;}
            if(Games.Natural(dealer)){result=natural?"Push":"Dealer blackjack";return natural?stake:0;}
            if(natural){result="Blackjack!";return stake+stake*3/2;}
            if(d>21||player>d){result="You win";return stake*2;}
            if(player==d){result="Push";return stake;}
            result="Dealer wins";return 0;
        }
        private void Settle(double now)
        {
            if(phase!=TablePhase.Playing)return;
            if(seats.Any(s=>s.wager>0&&(Games.Value(s.cards)<=21||s.secondCards.Count>0&&Games.Value(s.secondCards)<=21))&&!Games.Natural(dealer))while(Games.Value(dealer)<17)dealer.Add(Draw());
            foreach(var seat in seats.Where(s=>s.wager>0))
            {
                bool split=seat.secondCards.Count>0;
                seat.payout=HandPayout(seat.cards,seat.handWager,!split,out string first);
                seat.result=first;
                if(split){seat.payout+=HandPayout(seat.secondCards,seat.secondWager,false,out string second);seat.result="Hand 1: "+first+" · Hand 2: "+second;}
                seat.firstDone=seat.secondDone=seat.done=true;
            }
            phase=TablePhase.Results;turn=0;deadline=now+RoundTiming.ResultsSeconds;
        }
        public void Tick(double now,Random rng)
        {
            if(deadline<=0||now<deadline)return;
            if(phase==TablePhase.Betting)Start(rng,now);
            else if(phase==TablePhase.Playing)Stand(turn,now);
            else Reset();
        }
        public void Finish(double now){if(phase==TablePhase.Playing){foreach(var seat in seats)seat.done=true;Settle(now);}}
        public IEnumerable<SharedSeat> TakePayments()
        {
            if(phase!=TablePhase.Results)yield break;
            foreach(var seat in seats.Where(s=>s.wager>0&&!s.paid)){seat.paid=true;yield return seat;}
        }
        public void Reset(){phase=TablePhase.Betting;deadline=0;turn=0;dealer.Clear();foreach(var s in seats){s.wager=0;s.payout=0;s.cards.Clear();s.secondCards.Clear();s.activeHand=0;s.handWager=s.secondWager=0;s.firstDone=s.secondDone=s.splitAces=false;s.done=false;s.paid=false;s.result=null;}}
    }
    public sealed class SharedRoulette
    {
        public readonly List<SharedSeat> seats=new List<SharedSeat>();
        public TablePhase phase;
        public int round,seed,number=-1;
        public double deadline,started;
        public RouletteSpin spin;
        public SharedSeat Find(uint id)=>seats.FirstOrDefault(s=>s.id==id);
        public bool Join(uint id,string name)
        {
            if(Find(id)!=null)return true;
            if(phase!=TablePhase.Betting||seats.Count>=8)return false;
            seats.Add(new SharedSeat{id=id,name=name,slot=Enumerable.Range(0,8).First(i=>seats.All(s=>s.slot!=i))});return true;
        }
        public bool CanBet(uint id)=>phase==TablePhase.Betting&&Find(id)!=null;
        public bool CanStart=>phase==TablePhase.Betting&&seats.Any(s=>s.wager>0);
        public void Bet(uint id,RouletteBet[] bets,int total,double now)
        {
            if(!CanBet(id)||total<0)throw new InvalidOperationException("Betting is closed");
            var seat=Find(id);seat.bets=bets.Select(b=>new RouletteBet{choice=b.choice,number=b.number,amount=b.amount}).ToArray();seat.wager=total;
            if(total>0&&deadline==0)deadline=now+RoundTiming.BettingSeconds;if(seats.All(s=>s.wager==0))deadline=0;
        }
        public void Start(int launchSeed,double now)
        {
            if(!CanStart)return;round++;seed=launchSeed;started=now;spin=new RouletteSpin(seed);number=-1;
            phase=TablePhase.Playing;deadline=now+spin.duration;foreach(var seat in seats){seat.paid=false;seat.payout=0;seat.result=null;}
        }
        public int Leave(uint id)
        {
            var seat=Find(id);if(seat==null||phase!=TablePhase.Betting)return 0;
            int refund=seat.wager;seats.Remove(seat);if(seats.All(s=>s.wager==0))deadline=0;return refund;
        }
        public void Finish(double now)
        {
            if(phase!=TablePhase.Playing)return;number=spin.number;phase=TablePhase.Results;deadline=now+RoundTiming.ResultsSeconds;
            foreach(var seat in seats){seat.payout=RouletteBets.Payout(seat.bets,number);seat.result="Returned "+seat.payout+" gold";}
        }
        public void Tick(double now,Random rng)
        {
            if(deadline<=0||now<deadline)return;
            if(phase==TablePhase.Betting)Start(rng.Next(),now);else if(phase==TablePhase.Playing)Finish(now);else Reset();
        }
        public IEnumerable<SharedSeat> TakePayments()
        {
            if(phase!=TablePhase.Results)yield break;foreach(var seat in seats.Where(s=>s.wager>0&&!s.paid)){seat.paid=true;yield return seat;}
        }
        public void Reset(){phase=TablePhase.Betting;deadline=0;number=-1;foreach(var s in seats){s.wager=0;s.payout=0;s.paid=false;s.bets=Array.Empty<RouletteBet>();s.result=null;}}
    }
    [Serializable] public class TableSeatState
    {
        public uint id;public string name;public int slot,wager,payout,total,activeHand,handWager,secondWager,secondTotal;public int[] secondCards;
        public bool done,seated,returnRoot,hasAnchor;public uint bodyId;public float returnX,returnY,returnZ,anchorX,anchorY,anchorZ,anchorYaw;public string result;public int[] cards;public RouletteBet[] bets;
    }
    [Serializable] public class TableSnapshot
    {
        public RaceSnapshot race;
        public int kind,phase,round,revision,spinSeed,number=-1;
        public uint turn,sceneId;
        public float remaining,elapsed,x,y,z,yaw;
        public bool lounge;
        public float roomX,roomY,roomZ,roomYaw,doorX,doorY,doorZ,doorYaw;
        public int machine,slotNet;
        public uint slotSpin;
        public int[] slotReels;
        public TableSeatState[] seats=Array.Empty<TableSeatState>();
        public int[] dealer=Array.Empty<int>();
    }
    public static class TableSnapshots
    {
        private static TableSeatState[] Seats(IEnumerable<SharedSeat> seats)=>seats.OrderBy(s=>s.slot).Select(s=>new TableSeatState{id=s.id,name=s.name,slot=s.slot,wager=s.wager,payout=s.payout,done=s.done,result=s.result,total=s.cards.Count==0?0:Games.Value(s.cards),cards=s.cards.ToArray(),activeHand=s.activeHand,handWager=s.handWager,secondWager=s.secondWager,secondCards=s.secondCards.ToArray(),secondTotal=s.secondCards.Count==0?0:Games.Value(s.secondCards),bets=s.bets}).ToArray();
        public static TableSnapshot Blackjack(SharedBlackjack table)=>new TableSnapshot
        {
            kind=1,phase=(int)table.phase,round=table.round,turn=table.turn,seats=Seats(table.seats),
            dealer=table.phase==TablePhase.Playing?new[]{table.dealer[0],-1}:table.dealer.ToArray()
        };
        public static TableSnapshot Roulette(SharedRoulette table)=>new TableSnapshot
        {kind=2,phase=(int)table.phase,round=table.round,spinSeed=table.seed,number=table.number,seats=Seats(table.seats)};
    }
}
