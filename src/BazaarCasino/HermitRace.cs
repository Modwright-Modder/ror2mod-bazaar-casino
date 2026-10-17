using System;
using System.Collections.Generic;
using System.Linq;

namespace BazaarCasino
{
    public enum CrabMotionPhase { Run,DigIn,Buried,DigOut,Idle }
    public struct CrabMotion { public float position,burrow;public double progress;public CrabMotionPhase phase; }
    // Host and clients replay the same run/burrow timeline. The host owns the
    // clock, finish decision and wallet; clients only animate the track.
    public sealed class CrabRun
    {
        public const int Count=4,Legs=7;
        public const float Length=28;
        private readonly double[,] runs=new double[Count,Legs],rests=new double[Count,Legs];
        public readonly double[] finish=new double[Count];
        public int Winner {get;}
        public double Duration=>finish[Winner];
        public CrabRun(int seed)
        {
            var random=new Random(seed);
            for(int lane=0;lane<Count;lane++)for(int leg=0;leg<Legs;leg++)
            {
                runs[lane,leg]=1.1+random.NextDouble()*1.5;
                rests[lane,leg]=leg<Legs-1&&random.NextDouble()<.5?.7+random.NextDouble()*1.8:0;
                finish[lane]+=runs[lane,leg]+rests[lane,leg];
            }
            Winner=Enumerable.Range(0,Count).OrderBy(i=>finish[i]).ThenBy(i=>i).First();
        }
        public float Position(int lane,double elapsed,out float burrow)
        {var pose=Sample(lane,elapsed);burrow=pose.burrow;return pose.position;}
        public CrabMotion Sample(int lane,double elapsed)
        {
            if(lane<0||lane>=Count)throw new ArgumentOutOfRangeException(nameof(lane));
            double time=Math.Max(0,elapsed);
            for(int leg=0;leg<Legs;leg++)
            {
                if(time<runs[lane,leg])return new CrabMotion{position=(float)((leg+time/runs[lane,leg])*Length/Legs),phase=CrabMotionPhase.Run,progress=time*(Length/Legs/runs[lane,leg])/2};
                time-=runs[lane,leg];
                if(time<rests[lane,leg])
                {
                    // Ease down and up around a real movement pause.
                    double transition=Math.Min(.25,rests[lane,leg]/3),remaining=rests[lane,leg]-time;
                    var phase=time<transition?CrabMotionPhase.DigIn:remaining<transition?CrabMotionPhase.DigOut:CrabMotionPhase.Buried;
                    return new CrabMotion{position=(leg+1)*Length/Legs,phase=phase,
                        progress=phase==CrabMotionPhase.DigIn?time/transition:phase==CrabMotionPhase.DigOut?1-remaining/transition:0,
                        burrow=(float)Math.Min(1,Math.Min(time/transition,remaining/transition))};
                }
                time-=rests[lane,leg];
            }
            return new CrabMotion{position=Length,phase=CrabMotionPhase.Idle};
        }
    }
    [Serializable] public sealed class RaceTicket
    {
        public uint id;
        public string name;
        public int lane,wager,payout;
        public bool paid;
    }
    [Serializable] public sealed class RaceSnapshot
    {
        public int round,phase,seed,winner=-1,minimum,maximum,revision;
        public float elapsed,remaining;
        public RaceTicket[] tickets=Array.Empty<RaceTicket>();
    }
    public sealed class SharedCrabRace
    {
        public const int Multiplier=4;
        public readonly List<RaceTicket> tickets=new List<RaceTicket>();
        public TablePhase phase;
        public int round,seed,winner=-1;
        public double deadline,started;
        public CrabRun run;
        public RaceTicket Find(uint id)=>tickets.FirstOrDefault(t=>t.id==id);
        public bool CanBet=>phase==TablePhase.Betting;
        public bool SetBet(uint id,string name,int lane,int wager,double now)
        {
            if(!CanBet||id==0||lane<0||lane>=CrabRun.Count||wager<0||wager>int.MaxValue/Multiplier)return false;
            var ticket=Find(id);
            if(wager==0){if(ticket!=null)tickets.Remove(ticket);if(tickets.Count==0)deadline=0;return true;}
            if(ticket==null)tickets.Add(ticket=new RaceTicket{id=id});
            ticket.name=name;ticket.lane=lane;ticket.wager=wager;ticket.payout=0;ticket.paid=false;
            if(deadline==0)deadline=now+15;
            return true;
        }
        public void Tick(double now,int nextSeed)
        {
            if(phase==TablePhase.Betting)
            {
                if(deadline>0&&now>=deadline&&tickets.Count>0)
                {phase=TablePhase.Playing;round++;seed=nextSeed;run=new CrabRun(seed);started=now;deadline=now+run.Duration;}
            }
            else if(phase==TablePhase.Playing&&now>=deadline)Finish(now);
            else if(phase==TablePhase.Results&&now>=deadline)Reset();
        }
        public void Finish(double now)
        {
            if(phase!=TablePhase.Playing||run==null)return;
            winner=run.Winner;phase=TablePhase.Results;deadline=now+6;
            foreach(var ticket in tickets)ticket.payout=ticket.lane==winner?ticket.wager*Multiplier:0;
        }
        public IEnumerable<RaceTicket> TakePayments()
        {
            if(phase!=TablePhase.Results)yield break;
            foreach(var ticket in tickets.Where(t=>!t.paid)){ticket.paid=true;yield return ticket;}
        }
        public void Reset(){tickets.Clear();phase=TablePhase.Betting;deadline=started=0;winner=-1;run=null;seed=0;}
    }
}
