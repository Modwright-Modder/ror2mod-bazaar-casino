using RoR2.UI;

namespace BazaarCasino
{
    public sealed partial class CasinoPlugin
    {
        private void LateUpdate()
        {
            if(!InBazaar||!ui||!ui.IsAnyCasinoOpen)return;
            var master=LocalMaster;if(!master)return;
            foreach(var hud in HUD.readOnlyInstanceList)
            {
                if(!hud||hud.localUserViewer?.cachedMaster!=master||!hud.moneyText||hud.moneyText.isActiveAndEnabled)continue;
                // Fixed casino cameras hide the native HUD. Keep its own wallet
                // animation ticking while hidden: it plays Play_UI_coin at the
                // game's normal cadence and consumes the pending count-up now,
                // instead of replaying it only when the player leaves the seat.
                hud.moneyText.targetValue=unchecked((int)master.money);
                hud.moneyText.Update();
            }
        }
    }
}
