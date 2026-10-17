using System;
using BazaarCasino;

static class LoungeTravelTests
{
    internal static void Run(Action<bool,string> check)
    {
        var gate=new LoungeTravelGate();
        check(!gate.Complete(1,10,20,10,20,true,-147,-24,-23,90),"Unsolicited travel response cannot teleport a player");
        gate.Begin(1,10,20);
        check(!gate.Complete(2,10,20,10,20,true,-147,-24,-23,90)&&gate.Pending,"Unrelated response cannot consume pending door travel");
        check(gate.Complete(1,10,20,10,20,true,-147,-24,-23,90)&&!gate.Pending,"Matching host approval lets the owning client apply travel");
        check(!gate.Complete(1,10,20,10,20,true,-147,-24,-23,90),"Duplicate host response cannot teleport twice");
        gate.Begin(2,10,20);
        check(!gate.Complete(2,10,20,10,20,false,0,0,0,0)&&!gate.Pending,"Host refusal cannot move the client");
        foreach(var ids in new[]{new uint[]{11,20,10,20},new uint[]{10,21,10,20},new uint[]{10,20,11,20},new uint[]{10,20,10,21}})
        {
            gate.Begin(3,10,20);
            check(!gate.Complete(3,ids[0],ids[1],ids[2],ids[3],true,0,0,0,0),"Travel is bound to the original body and scene on both peers");
        }
        foreach(double invalid in new[]{double.NaN,double.PositiveInfinity,double.NegativeInfinity})
            for(int axis=0;axis<4;axis++)
            {
                var pose=new double[]{1,2,3,90};pose[axis]=invalid;gate.Begin(4,10,20);
                check(!gate.Complete(4,10,20,10,20,true,pose[0],pose[1],pose[2],pose[3]),"Non-finite destination or rotation cannot move a player");
            }
        gate.Begin(5,10,20);gate.Cancel();
        check(!gate.Complete(5,10,20,10,20,true,0,0,0,0),"Cancelled or timed-out travel cannot execute later");
        gate.Begin(6,10,20);gate.Begin(7,10,20);
        check(!gate.Complete(6,10,20,10,20,true,0,0,0,0)&&gate.Pending,"Older door response cannot replace a newer request");
        check(gate.Complete(7,10,20,10,20,true,0,0,0,0),"Newer door request remains valid after an old reply");
        foreach(var ids in new[]{new uint[]{0,20},new uint[]{10,0}})
        {
            gate.Begin(8,ids[0],ids[1]);
            check(!gate.Complete(8,ids[0],ids[1],ids[0],ids[1],true,0,0,0,0),"Unspawned body or stage cannot authorize travel");
        }
    }
}
