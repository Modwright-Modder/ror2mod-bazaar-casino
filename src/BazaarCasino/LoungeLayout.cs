using System;
using System.Collections.Generic;

namespace BazaarCasino
{
    public static class BazaarEntranceLayout
    {
        // Native Bazaar coordinates at the dark end of the wall alcove.
        // Live character feet/facing and averaged Seer transforms are unsuitable
        // anchors: they can differ during scene initialization or with other mods.
        public const float AnchorX=-147.46f,AnchorZ=-23.46f,ProbeY=-25.3f;
        public const float SideX=.977001f,SideZ=.213235f;
        public static IEnumerable<SeatPoint> Candidates()
        {
            foreach(float along in new[]{0f,.5f,-.5f,1f,-1f})
                foreach(float across in new[]{0f,-.35f,.35f})
                    yield return new SeatPoint(AnchorX-SideZ*along+SideX*across,AnchorZ+SideX*along+SideZ*across);
        }
        public static bool Contains(float x,float z)
        {
            float dx=x-AnchorX,dz=z-AnchorZ;
            return Math.Abs(-SideZ*dx+SideX*dz)<=1.01f&&Math.Abs(SideX*dx+SideZ*dz)<=.36f;
        }
    }
    public static class LoungeLayout
    {
        public const float Width=60,Depth=60,Height=16;
        public const float DoorZ=-28,ArrivalZ=-25.5f,RaceZ=10,LaneWidth=3.2f,TrackWidth=14,TrackLength=28,BettingZ=-15.6f;
        public static SeatPoint Table(TableKind kind)=>new SeatPoint(kind==TableKind.Blackjack?-15f:15f,-12);
        public static bool Contains(float x,float y,float z)=>Math.Abs(x)<Width/2&&Math.Abs(z)<Depth/2&&y>-.5f&&y<Height+.5f;
    }
}
