using RoR2;
using UnityEngine.Networking;

[assembly: HG.Reflection.SearchableAttribute.OptIn]

namespace BazaarCasino
{
    public sealed partial class CasinoPlugin
    {
        [ConCommand(commandName="modwright_blueportal",flags=ConVarFlags.None,helpText="Host-only: move the active run to the Bazaar. No gold is added.")]
        private static void BluePortalCommand(ConCommandArgs args)
        {
            if(args.Count!=0){args.Log("Usage: modwright_blueportal");return;}
            args.Log(ExecuteManualCommand(ManualCommand.BluePortal,0,args.senderMaster));
        }
        [ConCommand(commandName="modwright_gold",flags=ConVarFlags.None,helpText="Host-only: add gold to your current character. Usage: modwright_gold 5000")]
        private static void GoldCommand(ConCommandArgs args)
        {
            if(args.Count!=1){args.Log("Usage: modwright_gold <amount>");return;}
            int amount=args.GetArgInt(0);
            if(amount<1||amount>1000000){args.Log("Choose an amount from 1 to 1,000,000 gold.");return;}
            args.Log(ExecuteManualCommand(ManualCommand.Gold,amount,args.senderMaster));
        }
        private static string ExecuteManualCommand(ManualCommand command,int amount,CharacterMaster master)
        {
            if(!Instance||!NetworkServer.active||!Run.instance)return "Start a run as host first. These commands are host-only.";
            if(Run.instance.isGameOverServer)return "This run has ended. Start a new run before using casino test commands.";
            if(command==ManualCommand.BluePortal)
            {
                if(InBazaar)return "Already in the Bazaar.";
                if(SceneExitController.isRunning)return "A stage transition is already in progress.";
                var bazaar=SceneCatalog.GetSceneDefFromSceneName("bazaar");
                if(!bazaar)return "Bazaar scene is unavailable.";
                Instance.ui.Close();Run.instance.AdvanceStage(bazaar);
                return "Moving to the Bazaar; no gold added.";
            }
            if(command!=ManualCommand.Gold||amount<1||amount>1000000)return "Invalid casino command.";
            if(!master)master=LocalMaster;
            if(!master)return "No local character master is available.";
            uint before=master.money;Credit(master,amount);
            return "Added "+(master.money-before).ToString("N0")+" gold. Current gold: "+master.money.ToString("N0");
        }
        private void SubmitCasinoChat(On.RoR2.UI.ChatBox.orig_SubmitChat orig,RoR2.UI.ChatBox chat)
        {
            if(!chat.inputField||!ManualCommandText.TryParse(chat.inputField.text,out var command,out int amount,out string error))
            {orig(chat);return;}
            chat.inputField.text="";chat.SetShowInput(false);
            string result=error??ExecuteManualCommand(command,amount,LocalMaster);
            // Local feedback only; the debug shortcut is not sent as a chat/say
            // packet and remote players cannot use it to control the host.
            Chat.AddMessage("<color=#E8CD80>[Casino]</color> "+result);
        }
    }
}
