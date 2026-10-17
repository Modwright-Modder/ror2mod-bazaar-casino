using System;

namespace BazaarCasino
{
    public readonly struct SeatPoint
    {
        public readonly float x,z;
        public SeatPoint(float x,float z){this.x=x;this.z=z;}
    }
    public static class SeatLayout
    {
        public static SeatPoint Chair(TableKind kind,int slot)
        {
            if(slot<0||slot>=8)throw new ArgumentOutOfRangeException(nameof(slot));
            if(kind==TableKind.Blackjack)
            {
                double angle=(-100+slot*200.0/7)*Math.PI/180;
                return new SeatPoint((float)Math.Sin(angle)*3.45f,-(float)Math.Cos(angle)*2.70f);
            }
            if(kind!=TableKind.Roulette)throw new ArgumentOutOfRangeException(nameof(kind));
            return new SeatPoint(-3+2*(slot%4),slot<4?-2.85f:2.85f);
        }
    }
}
