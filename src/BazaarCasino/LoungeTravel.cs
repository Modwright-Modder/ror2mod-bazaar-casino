using System;

namespace BazaarCasino
{
    // Match host authorization to the same request, body and scene that asked
    // for travel. Late replies cannot move a respawned player or another run.
    public sealed class LoungeTravelGate
    {
        private uint request,body,scene;
        public bool Pending { get; private set; }
        public void Begin(uint requestId,uint bodyId,uint sceneId)
        {request=requestId;body=bodyId;scene=sceneId;Pending=true;}
        public bool Matches(uint requestId)=>Pending&&request==requestId;
        public void Cancel()=>Pending=false;
        public bool Complete(uint requestId,uint replyBody,uint replyScene,uint currentBody,uint currentScene,bool approved,double x,double y,double z,double yaw)
        {
            if(!Matches(requestId))return false;
            Pending=false;
            return approved&&body!=0&&scene!=0&&replyBody==body&&currentBody==body&&replyScene==scene&&currentScene==scene&&
                Finite(x)&&Finite(y)&&Finite(z)&&Finite(yaw);
        }
        private static bool Finite(double value)=>!double.IsNaN(value)&&!double.IsInfinity(value);
    }
}
