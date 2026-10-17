using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace BazaarCasino
{
    public sealed partial class CasinoPlugin
    {
        private readonly LoungeTravelGate loungeTravel=new LoungeTravelGate();
        private static bool IsLoungeAction(ActionKind action)=>action==ActionKind.LoungeEnter||action==ActionKind.LoungeExit;
        private void BeginLoungeTravel(CasinoRequest request,CharacterMaster master)
        {
            if(!IsLoungeAction(request.action))return;
            var body=master.GetBody();
            loungeTravel.Begin(request.requestId,body?body.netId.Value:0,Stage.instance?Stage.instance.netId.Value:0);
        }
        private void DeliverLoungeTravel(CasinoState state)
        {
            if(!loungeTravel.Matches(state.requestId))return;
            var body=LocalMaster?LocalMaster.GetBody():null;
            bool accepted=loungeTravel.Complete(state.requestId,state.loungeBodyId,state.loungeSceneId,
                body?body.netId.Value:0,Stage.instance?Stage.instance.netId.Value:0,
                state.loungeMoved&&body&&body.healthComponent&&body.healthComponent.alive&&InBazaar&&!SceneExitController.isRunning&&
                    (NetworkServer.active||Util.HasEffectiveAuthority(body.gameObject)),
                state.loungeX,state.loungeY,state.loungeZ,state.loungeYaw);
            if(accepted)
            {
                // Native TeleportHelper.HandleTeleport skips the owning client.
                // The host already moved its replica; the owner must apply the
                // authorized destination locally and publish its native teleport.
                if(!NetworkServer.active&&Util.HasEffectiveAuthority(body.gameObject))
                {
                    TeleportHelper.TeleportBody(body,new Vector3(state.loungeX,state.loungeY,state.loungeZ),false);
                    if(body.characterMotor)body.characterMotor.velocity=Vector3.zero;
                    var facing=Quaternion.Euler(0,state.loungeYaw,0);
                    if(body.characterDirection)body.characterDirection.forward=facing*Vector3.forward;
                    else ((Component)body).transform.rotation=facing;
                }
                Logger.LogInfo("Lounge travel acknowledged for local player (request "+state.requestId+").");
            }
            else ShowLoungeTravelError(state.loungeMoved?"Lounge travel expired because your character or scene changed. Press E again.":state.message);
        }
        private void ShowLoungeTravelError(string message)
        {
            message=message??"Lounge travel failed. Press E again.";
            ui.Notify(message);
            Chat.AddMessage("<color=#E8CD80>[Casino]</color> "+message);
            Logger.LogWarning("Lounge travel: "+message);
        }
    }
}
