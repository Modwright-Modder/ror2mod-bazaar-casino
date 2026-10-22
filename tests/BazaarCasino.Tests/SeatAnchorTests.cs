using System;
using BazaarCasino;

static class SeatAnchorTests
{
    internal static void Run(Action<bool,string> check)
    {
        var anchor=new SeatAnchor(132,0,-90.07f,61.53f,-5.03f,90);
        // Reproduce a custom motor/old network pose throwing a seated drone
        // back into the Bazaar. The host must preserve its lease and root pose.
        foreach(var position in new[]{new[]{-88.34f,64.66f,-5.14f,180f},new[]{-123f,-23f,-43f,0f},new[]{float.NaN,float.NegativeInfinity,999f,360f}})
        {
            float x=position[0],y=position[1],z=position[2],yaw=position[3];
            check(anchor.Keeps(132,true,true,false),"A living connected body retains its seat despite arbitrary motor displacement");
            anchor.Pin(ref x,ref y,ref z,ref yaw);
            check(x==-90.07f&&y==61.53f&&z==-5.03f&&yaw==90,"A changed, OOB or non-finite movement sample is corrected to the immutable host root pose");
            // A foreign teleport is rejected while the lease is valid; native
            // normal teleport behavior is restored as soon as the seat ends.
            check(SeatPlacement.PauseMovement(anchor.Keeps(132,true,true,false),true,true,false),"Foreign teleports remain blocked after displacement");
        }
        check(!anchor.Keeps(133,true,true,false),"A respawned body cannot inherit the old anchor");
        check(!anchor.Keeps(132,false,true,false),"Disconnect releases the seat");
        check(!anchor.Keeps(132,true,false,false),"Death releases the seat");
        check(!anchor.Keeps(132,true,true,true),"Scene exit releases the seat");
        foreach(float bad in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity})
        {
            bool rejected=false;try{new SeatAnchor(132,0,bad,61,-5,90);}catch(ArgumentException){rejected=true;}
            check(rejected,"Invalid anchor coordinates cannot pin a character outside the map");
        }
        foreach(int slot in new[]{-1,8})
        {
            bool rejected=false;try{new SeatAnchor(132,slot,0,60,0,0);}catch(ArgumentException){rejected=true;}
            check(rejected,"Invalid seat indexes rejected");
        }
    }
}
