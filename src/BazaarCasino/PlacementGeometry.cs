using System;
using System.Collections.Generic;
using System.Linq;

namespace BazaarCasino
{
    public static class PlacementGeometry
    {
        public static SeatPoint RoomSide(SeatPoint newt,SeatPoint seers,SeatPoint facing)
        {
            float dx=seers.x-newt.x,dz=seers.z-newt.z;
            float length=(float)Math.Sqrt(dx*dx+dz*dz);
            if(length<1)throw new ArgumentException("Scene landmarks overlap.");
            var side=new SeatPoint(dz/length,-dx/length);
            return side.x*facing.x+side.z*facing.z<0?new SeatPoint(-side.x,-side.z):side;
        }
        public static IEnumerable<SeatPoint> Candidates(TableKind kind,SeatPoint newt,SeatPoint seers,SeatPoint facing)
        {
            var side=RoomSide(newt,seers,facing);
            float preferred=kind==TableKind.Blackjack?.38f:.72f;
            var target=new SeatPoint(newt.x+(seers.x-newt.x)*preferred+side.x*7,newt.z+(seers.z-newt.z)*preferred+side.z*7);
            var points=new List<SeatPoint>();
            // Stay between the landmarks, on the room-facing side, not past the Seers.
            for(int row=0;row<10;row++)for(int column=0;column<8;column++)
            {
                float along=.23f+row*.067f,across=4+column*2;
                points.Add(new SeatPoint(newt.x+(seers.x-newt.x)*along+side.x*across,newt.z+(seers.z-newt.z)*along+side.z*across));
            }
            return points.OrderBy(p=>(p.x-target.x)*(p.x-target.x)+(p.z-target.z)*(p.z-target.z));
        }
        public static bool TryPlatformHeight(float[] heights,out float level)
        {
            level=0;
            if(heights==null||heights.Length<5||heights.Any(h=>float.IsNaN(h)||float.IsInfinity(h)))return false;
            if(heights.Max()-heights.Min()>1.25f)return false;
            level=heights.Max()+.08f;return true;
        }
    }
}
