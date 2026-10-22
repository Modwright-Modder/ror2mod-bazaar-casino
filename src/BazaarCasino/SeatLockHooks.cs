using RoR2;
using UnityEngine;

namespace BazaarCasino
{
    public sealed partial class CasinoPlugin
    {
        private void GuardSeatTeleport(On.RoR2.TeleportHelper.orig_TeleportGameObject_GameObject_Vector3_Vector3_Quaternion_bool_bool orig,
            GameObject target,Vector3 position,Vector3 delta,Quaternion rotation,bool useRotation,bool authority)
        {
            var seat=InBazaar&&target?target.GetComponent<CasinoSeat>():null;
            if(seat&&seat.BlocksForeignTeleport){seat.PinNow("foreign teleport blocked");return;}
            orig(target,position,delta,rotation,useRotation,authority);
        }
        private void GuardSeatMapZone(On.RoR2.MapZone.orig_TeleportBody orig,MapZone zone,CharacterBody body)
        {
            var seat=InBazaar&&body?body.GetComponent<CasinoSeat>():null;
            if(seat&&seat.BlocksForeignTeleport){seat.PinNow("map-zone teleport blocked");return;}
            orig(zone,body);
        }
    }
}
