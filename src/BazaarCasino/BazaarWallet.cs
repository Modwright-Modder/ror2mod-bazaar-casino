using BepInEx.Configuration;
using RoR2;
using UnityEngine.Networking;

namespace BazaarCasino
{
    public sealed partial class CasinoPlugin
    {
        private ConfigEntry<bool> keepBazaarGold;
        private void ConfigureBazaarWallet()
        {
            keepBazaarGold=Config.Bind("Economy","KeepGoldOnBazaarEntry",true,
                "Preserve run gold when travelling to the Bazaar, without converting it to XP. Bigger Bazaar may still manage its own transfer. Other stage exits retain native money conversion.");
            On.RoR2.ConvertPlayerMoneyToExperience.FixedUpdate+=PreserveBazaarGold;
        }
        private void PreserveBazaarGold(On.RoR2.ConvertPlayerMoneyToExperience.orig_FixedUpdate orig,ConvertPlayerMoneyToExperience self)
        {
            var exit=self.GetComponent<SceneExitController>();
            if(NetworkServer.active&&keepBazaarGold.Value&&exit&&exit.destinationScene&&exit.destinationScene.baseSceneName=="bazaar")
            {
                // The exit waits for this collector to finish. Removing it before
                // its first burst preserves everyone's wallet and awards no duplicate XP.
                Destroy(self);return;
            }
            orig(self);
        }
        private void StopBazaarWallet()=>On.RoR2.ConvertPlayerMoneyToExperience.FixedUpdate-=PreserveBazaarGold;
    }
}
