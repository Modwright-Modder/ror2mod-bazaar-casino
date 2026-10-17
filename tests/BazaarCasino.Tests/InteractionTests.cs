using System;
using System.Linq;
using BazaarCasino;

static class InteractionChecks
{
    public static void Run(Action<bool,string> check)
    {
        // Simulate Unity constructing a zero-valued nested race from a null
        // member on unrelated table/slot packets. Message tags remain reliable.
        check(SnapshotRoutes.Classify(1,0,true,0,0)==SnapshotKind.Table,"Blackjack packets with an empty nested race stay blackjack packets");
        check(SnapshotRoutes.Classify(2,0,true,0,0)==SnapshotKind.Table,"Roulette snapshots cannot reset the Derby minimum to zero");
        for(int machine=1;machine<=3;machine++)check(SnapshotRoutes.Classify(0,machine,true,0,0)==SnapshotKind.Slot,"Slot replay cannot be swallowed by a default nested race");
        check(SnapshotRoutes.Classify(3,0,true,500,100000)==SnapshotKind.Race,"A tagged, valid race reaches the Derby");
        check(SnapshotRoutes.Classify(3,0,true,0,0)==SnapshotKind.Ignore&&SnapshotRoutes.Classify(3,0,false,500,100000)==SnapshotKind.Ignore,"Malformed/default race state cannot overwrite a valid wager");
        check(SnapshotRoutes.Classify(0,0,true,500,100000)==SnapshotKind.Ignore,"Nested state alone never selects a message route");
        var gate=new InteractionReleaseGate();gate.Observe(false);
        check(!gate.Blocked(1),"First explicit interaction is available");
        gate.Close(10);gate.Observe(true);
        check(gate.Blocked(10)&&gate.Blocked(20),"Held E or the exit click cannot immediately reopen a machine");
        gate.Observe(false);check(!gate.Blocked(20),"A fresh interaction works after release");
        gate.Close(30);gate.Observe(false);
        check(gate.Blocked(30.1)&&!gate.Blocked(30.3),"Close has a short bounded settling guard, without requiring another click");
        foreach(int count in new[]{1,5,10})foreach(int price in new[]{1,50,123,100000000})
            check(CaseBatchRules.Total(price,count)==(long)price*count,"Batch quote is exactly the per-case scaled price times its count");
        foreach(int count in new[]{-1,0,2,4,6,11,100})check(!CaseBatchRules.ValidCount(count),"Only advertised 1x/5x/10x batches can be purchased");
        bool rejected=false;try{CaseBatchRules.Total(int.MaxValue,10);}catch(ArgumentOutOfRangeException){rejected=true;}
        check(rejected,"Overflowing batch price cannot reach the wallet");
        foreach(int price in new[]{50,123,250,1000,100000000})
        {
            int minimum=WagerRules.ScaledMinimum(10,price,100000);
            check(minimum==Math.Min(100000,price),"Minimum gold tracks scaled Cache price and respects the configured maximum");
            var chips=WagerRules.Denominations(minimum,100000);
            check(chips[0]==minimum&&chips.All(v=>v>=minimum&&v<=100000)&&chips.Distinct().Count()==chips.Length,"Scaled chip presets are valid, unique and within table limits");
            check(WagerRules.Edit(0,minimum,false,minimum,100000,(uint)minimum)==minimum,"Exactly the scaled minimum can be reserved and paid");
        }
        check(WagerRules.ScaledMinimum(200,50,100000)==200,"Custom minimum remains a floor above Cache pricing");
        foreach(int count in new[]{1,5,10})
        {
            float previousBottom=1;
            for(int row=0;row<count;row++)
            {
                CaseLayout.RowBounds(count,row,out float bottom,out float top);
                check(bottom>=0&&top<=1&&bottom<top&&top<=previousBottom+.000001f,"Every batch reel stays clipped inside one fixed screen without overlap");
                check(Math.Abs((top-bottom)*CaseLayout.WindowPixels-(count==1?230:72))<.001f,"Reels retain readable heights without resizing the cabinet or camera");
                previousBottom=bottom;
            }
        }
        foreach(string relic in new[]{"BelmontMorningStar","BelmontCrissaegrim","BelmontHolyCross","BelmontWingedBoots","BelmontBrotherSoul"})
            check(!GachaRules.NormalReward(relic,false,false,false,false),"Survivor-only Belmont relics cannot be sold as general Cache prizes");
        check(GachaRules.NormalReward("Syringe",false,false,false,false)&&GachaRules.NormalReward("ModdedNormalRed",false,false,false,false),"Ordinary base and modded items remain eligible");
        check(!GachaRules.NormalReward("Special",true,false,false,false)&&!GachaRules.NormalReward("Special",false,true,false,false)&&!GachaRules.NormalReward("Scrap",false,false,true,false)&&!GachaRules.NormalReward("NoCommand",false,false,false,true),"Unique, no-drop, scrap and Command-blocked rewards are excluded");
        var betting=new SharedBlackjack();betting.Join(1,"Gold player");uint wallet=1000;int wager=0;
        foreach(int chip in new[]{50,100,500})
        {
            int next=WagerRules.Edit(wager,chip,false,10,100000,wallet);wallet-=(uint)(next-wager);wager=next;betting.Bet(1,wager,10);
            check(wager+wallet==1000&&betting.Find(1).wager==wager,"Adding another chip after betting debits only its gold delta");
            check(betting.deadline==25,"Adding chips does not extend the shared betting countdown");
            betting.Bet(1,wager,11);check(betting.Find(1).wager==wager,"Absolute host wager acknowledgments remain idempotent");
        }
        int reduced=WagerRules.Edit(wager,100,true,10,100000,wallet);wallet+=(uint)(wager-reduced);wager=reduced;
        check(wallet+wager==1000&&wager==550,"Removing a blackjack chip refunds its gold delta");
        check(WagerRules.Edit(10,50,true,10,100000,0)==0,"Removing more than the current stake clears it safely");
        rejected=false;try{WagerRules.Edit(950,100,false,10,100000,50);}catch(InvalidOperationException){rejected=true;}check(rejected,"Adding a chip cannot exceed available gold");
        rejected=false;try{WagerRules.Edit(990,50,false,10,1000,1000);}catch(InvalidOperationException){rejected=true;}check(rejected,"Adding a chip cannot exceed the table limit");
        foreach(int count in new[]{1,5,10})
        {
            float previous=0;
            for(int row=0;row<count;row++)
            {
                check(GachaRules.ReelElapsed(0,row,count)==0,"No batch reel advances before purchase");
                check(GachaRules.ReelElapsed(SpinTiming.Case,row,count)==SpinTiming.Case,"Every paid reel is settled exactly when the host dispenses the batch");
                check(GachaRules.ReelElapsed(SpinTiming.Case+30,row,count)==SpinTiming.Case,"Reopening a receipt shows every final prize without spinning or purchasing again");
                previous=0;
                for(float time=0;time<SpinTiming.Case;time+=.031f)
                {
                    float current=GachaRules.ReelElapsed(time,row,count);
                    check(current>=previous&&current>=0&&current<=SpinTiming.Case,"Each independent row remains bounded and moves forward");
                    if(row>0)check(current<=GachaRules.ReelElapsed(time,row-1,count),"Later rows start/stop slightly after the preceding row");
                    previous=current;
                }
                float offset=GachaRules.CardOffset(630,23,0,SpinTiming.Case,count==1?144:105);
                check(Math.Abs(offset+(23+.5f)*(count==1?144:105)-315)<.001,"The final item of each reel lands at its own central marker");
            }
        }
        Console.WriteLine("Interaction regressions: tagged Unity snapshot routing, exit/held-input release guard and safe 1x/5x/10x batch quotes.");
    }
}
