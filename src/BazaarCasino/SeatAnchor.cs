using System;

namespace BazaarCasino
{
    // One host-assigned root pose for a body. Movement samples never change it.
    public sealed class SeatAnchor
    {
        public readonly uint BodyId;
        public readonly int Slot;
        public readonly float X,Y,Z,Yaw;
        public SeatAnchor(uint body,int slot,float x,float y,float z,float yaw)
        {
            if(body==0||slot<0||slot>=8||!Finite(x)||!Finite(y)||!Finite(z)||!Finite(yaw))throw new ArgumentException("Invalid seat anchor");
            BodyId=body;Slot=slot;X=x;Y=y;Z=z;Yaw=yaw;
        }
        public bool Keeps(uint currentBody,bool connected,bool alive,bool sceneExiting)
            => currentBody==BodyId&&connected&&alive&&!sceneExiting;
        public void Pin(ref float x,ref float y,ref float z,ref float yaw)
        {x=X;y=Y;z=Z;yaw=Yaw;}
        private static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
    }
}
