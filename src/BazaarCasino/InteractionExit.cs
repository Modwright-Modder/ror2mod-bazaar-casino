using RoR2.UI;
using UnityEngine;

namespace BazaarCasino
{
    public sealed partial class CasinoPlugin
    {
        private void CasinoEventUpdate(On.RoR2.UI.MPEventSystem.orig_Update original,MPEventSystem system)
        {
            // MPEventSystem normally processes the pause action after UI input.
            // Consume only the closing frame, regardless of MonoBehaviour order.
            if(ui&&ui.EscapeConsumedFrame==Time.frameCount)return;
            bool cancel=Input.GetKeyDown(KeyCode.Escape)||(system.player!=null&&system.player.GetButtonDown(25));
            if(ui&&ui.IsAnyCasinoOpen&&cancel){ui.CloseFromEscape();return;}
            original(system);
        }
    }
    internal sealed partial class CasinoUI
    {
        internal int EscapeConsumedFrame {get;private set;}=-1;
        private readonly InteractionReleaseGate interactionGate=new InteractionReleaseGate();
        internal bool InteractionBlocked=>interactionGate.Blocked(Time.unscaledTime);
        internal bool CanOpenInteraction=>!IsAnyCasinoOpen&&!InteractionBlocked;
        private void ObserveInteractionRelease()
        {
            var player=RoR2.LocalUserManager.GetFirstLocalUser()?.inputPlayer;
            interactionGate.Observe(Input.GetMouseButton(0)||Input.GetKey(KeyCode.E)||(player!=null&&player.GetButton(5)));
        }
        internal bool IsAnyCasinoOpen=>IsOpen||tablePanel&&tablePanel.activeSelf;
        internal void CloseFromEscape(){if(EscapeConsumedFrame==Time.frameCount)return;EscapeConsumedFrame=Time.frameCount;if(WagerDialogOpen)CloseWagerDialog();else Close();}
    }
}
