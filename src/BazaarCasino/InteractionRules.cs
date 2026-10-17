using System;

namespace BazaarCasino
{
    public enum SnapshotKind { Ignore,Table,Slot,Race }
    public static class SnapshotRoutes
    {
        // Unity deserialization can instantiate empty nested classes for null
        // fields. Route by the explicit message tag, never by nested nullness.
        public static SnapshotKind Classify(int kind,int machine,bool hasRace,int minimum,int maximum)
        {
            if(machine>0&&machine<=3)return SnapshotKind.Slot;
            if(kind==1||kind==2)return SnapshotKind.Table;
            if(kind==3&&hasRace&&minimum>0&&maximum>=minimum)return SnapshotKind.Race;
            return SnapshotKind.Ignore;
        }
    }
    public sealed class InteractionReleaseGate
    {
        private double until;
        private bool waitingForRelease;
        public void Close(double now){until=now+.2;waitingForRelease=true;}
        public void Observe(bool pressed){if(!pressed)waitingForRelease=false;}
        public bool Blocked(double now)=>waitingForRelease||now<until;
    }
    public static class CaseBatchRules
    {
        public static bool ValidCount(int count)=>count==1||count==5||count==10;
        public static int Total(int price,int count)
        {
            if(price<1||!ValidCount(count)||(long)price*count>int.MaxValue)throw new ArgumentOutOfRangeException();
            return price*count;
        }
    }
}
