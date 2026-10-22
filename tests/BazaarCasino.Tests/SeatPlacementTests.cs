using System;
using BazaarCasino;

static class SeatPlacementTests
{
    internal static void Run(Action<bool,string> check)
    {
        // HEL-P's native flight state adds downward velocity for the same E
        // input used to enter a table. Its state must stop ticking while seated,
        // even though EntityStateMachine itself remains enabled and unchanged.
        float descend=0;int stateTicks=0;
        for(int frame=0;frame<200;frame++)
            if(!SeatPlacement.PauseMovement(true,true,true,false)){stateTicks++;descend-=10;}
        check(stateTicks==0&&descend==0,"Held interact cannot run HEL-P's descending flight state during a seat");
        check(!SeatPlacement.PauseMovement(false,true,true,false),"Standing up immediately returns entity-state updates to the game");
        check(!SeatPlacement.PauseMovement(true,false,true,false),"Death states are never trapped by the seating pause");
        check(!SeatPlacement.PauseMovement(true,true,false,false),"Flight runs normally outside Bazaar");
        check(!SeatPlacement.PauseMovement(true,true,true,true),"Scene exit is not blocked by the seat state gate");
        for(int slot=0;slot<8;slot++)
            check(SeatPlacement.Accept(12,12,slot,true,-12,84,16),"Every valid host-assigned chair accepts the same living body");
        check(!SeatPlacement.Accept(0,0,0,true,0,0,0),"An unresolved network body cannot be seated");
        check(!SeatPlacement.Accept(12,13,0,true,0,84,0),"A stale snapshot cannot seat a respawned body");
        check(!SeatPlacement.Accept(12,12,-1,true,0,84,0)&&!SeatPlacement.Accept(12,12,8,true,0,84,0),"Invalid chair indexes rejected before touching movement");
        check(!SeatPlacement.Accept(12,12,0,false,0,84,0),"Old clients/host snapshots cannot reinterpret foot coordinates as root coordinates");
        foreach(double bad in new[]{double.NaN,double.PositiveInfinity,double.NegativeInfinity})
        {
            check(!SeatPlacement.Accept(12,12,0,true,bad,84,0),"Non-finite return X rejected");
            check(!SeatPlacement.Accept(12,12,0,true,0,bad,0),"Non-finite return Y rejected");
            check(!SeatPlacement.Accept(12,12,0,true,0,84,bad),"Non-finite return Z rejected");
        }
        check(Math.Abs(SeatPlacement.RootHeight(2,8)-1.12f)<.001,"Capsule survivor uses capsule height rather than oversized accessory bounds");
        check(Math.Abs(SeatPlacement.RootHeight(0,1.8f)-1.92f)<.001,"Rigidbody Tank root clears the collider bottom instead of embedding into the floor");
        check(Math.Abs(SeatPlacement.RootHeight(0,-2)-.12f)<.001,"Roots below the collider retain minimum floor clearance");
        check(SeatPlacement.RootHeight(0,float.NaN)==.12f&&SeatPlacement.RootHeight(0,float.PositiveInfinity)==.12f,"Invalid collider measurements cannot teleport outside the map");
    }
}
