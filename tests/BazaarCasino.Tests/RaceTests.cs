using System;
using System.Linq;
using BazaarCasino;

static partial class RaceChecks
{
    public static void Run(Action<bool,string> check)
    {
        int stops=0;var wins=new int[4];
        for(int seed=0;seed<1000;seed++)
        {
            var run=new CrabRun(seed);var copy=new CrabRun(seed);wins[run.Winner]++;
            check(run.Duration>7&&run.Duration<35,"Race duration has a bounded suspense window");
            check(run.Winner==copy.Winner&&run.finish.SequenceEqual(copy.finish),"Host and late clients replay the identical race");
            check(run.Duration==run.finish.Min(),"Winning crab is the first to finish its run/burrow timeline");
            for(int lane=0;lane<4;lane++)
            {
                float previous=0;
                for(double time=0;time<=run.finish[lane]+.1;time+=.1)
                {
                    float position=run.Position(lane,time,out float buried);
                    var pose=run.Sample(lane,time);
                    check(position==pose.position&&buried==pose.burrow&&pose.progress>=0,"Native clip replay shares the authoritative position/phase clock");
                    check(pose.phase==CrabMotionPhase.Run||pose.phase==CrabMotionPhase.Idle||pose.progress<=1,"Native dig clip samples remain in range");
                    check(position>=previous&&position<=CrabRun.Length,"Crabs never move backwards or beyond the finish line");
                    check(buried>=0&&buried<=1,"Burrow depth remains bounded");
                    check(Math.Abs(position-copy.Position(lane,time,out _))<.00001,"Client animation agrees with the host at any clock sample");
                    if(buried>.5f){check(Math.Abs(position-previous)<.0001,"Buried crab pauses movement");check(pose.phase!=CrabMotionPhase.Run,"Paused racers use their native dig/buried/out clips");stops++;}
                    previous=position;
                }
            }
        }
        check(stops>1000&&wins.All(n=>n>180&&n<320),"All four lanes have equal distributions and frequent annoying burrows");
        var table=new SharedCrabRace();
        check(!table.SetBet(0,"bad",0,500,0)&&!table.SetBet(1,"bad",4,500,0)&&!table.SetBet(1,"bad",0,-1,0),"Invalid player, lane and stake rejected without creating a ticket");
        check(table.SetBet(1,"Survivor",2,500,10)&&table.deadline==15,"First stake opens a visible 5 second countdown");
        check(table.SetBet(1,"Survivor",1,1000,12)&&table.tickets.Count==1&&table.deadline==15,"Replacing a wager does not create duplicate tickets or extend countdown");
        check(table.SetBet(1,"Survivor",1,0,13)&&table.tickets.Count==0&&table.deadline==0,"Last cancellation closes the round before it starts");
        for(uint id=1;id<=4;id++)table.SetBet(id,"Player",(int)id-1,500,20);
        table.Tick(24,42);check(table.phase==TablePhase.Betting,"No premature race start");
        table.Tick(25,42);check(table.phase==TablePhase.Playing&&table.round==1,"First deadline launches the shared race");
        check(!table.SetBet(1,"late",0,0,36),"Committed wagers cannot be refunded or changed during the race");
        check(!table.TakePayments().Any(),"No early prize before the finish");
        table.Tick(table.deadline,42);
        check(table.phase==TablePhase.Results&&table.winner==table.run.Winner,"Host resolves the same finish seen on the track");
        check(table.tickets.Sum(t=>t.payout)==2000,"Exactly one of four equal stakes returns four times the wager");
        check(table.TakePayments().Count()==4&&!table.TakePayments().Any(),"Each winner/loser receipt settles once only");
        var json=System.Text.Json.JsonSerializer.Serialize(new TableSnapshot{kind=3,race=new RaceSnapshot{round=table.round,seed=table.seed,winner=table.winner,tickets=table.tickets.ToArray()}},new System.Text.Json.JsonSerializerOptions{IncludeFields=true});
        var snapshot=System.Text.Json.JsonSerializer.Deserialize<TableSnapshot>(json,new System.Text.Json.JsonSerializerOptions{IncludeFields=true});
        check(snapshot.race.tickets.Length==4&&snapshot.race.winner==table.winner,"Shared snapshot carries participants and authoritative winner");
        table.Tick(table.deadline,42);check(table.phase==TablePhase.Betting&&table.tickets.Count==0&&table.winner==-1,"Result window resets tickets before the next race");
        table.SetBet(1,"Player",0,500,100);table.Tick(115,12);table.Finish(116);
        check(table.phase==TablePhase.Results&&table.TakePayments().Count()==1&&!table.TakePayments().Any(),"Stage exit can finish a committed race without double payout");
        Console.WriteLine("Hermit Derby: deterministic run/burrow replay, lane fairness, countdown, cancellation, immutable committed wagers, stage settlement and exactly-once payouts.");
    }
}
