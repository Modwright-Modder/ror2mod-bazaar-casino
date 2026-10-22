using System;
using System.Linq;
using RoR2;
using RoR2.UI;
using UnityEngine;
using UnityEngine.Networking;

namespace BazaarCasino
{
    internal sealed partial class CasinoUI
    {
        private GameObject racePanel;
        private WorldCrabRace activeRace;
        private int raceLane,raceWager;
        private bool raceAllIn;
        private string raceMessage;
        private TMPro.TextMeshProUGUI raceViewStatus;
        private void SetRaceView(int view)
        {
            if(!activeRace)return;
            activeRace.BettingView=view==0;activeRace.Overview=view==2;
        }
        private void ToggleRaceView()=>SetRaceView(activeRace&&activeRace.Overview&&!activeRace.BettingView?0:2);
        internal bool IsRaceFocused=>racePanel&&racePanel.activeSelf&&activeRace;
        internal void OpenRace(WorldCrabRace machine)
        {
            if(!machine||!CasinoPlugin.InBazaar||!CasinoPlugin.LocalMaster||!Init())return;
            Close();raceAllIn=false;activeRace=machine;machine.Overview=false;machine.BettingView=true;
            if(!racePanel)
            {
                racePanel=Rect(canvasRoot.transform,"DerbyCursorLifetime").gameObject;Stretch((RectTransform)racePanel.transform);racePanel.SetActive(false);
                racePanel.AddComponent<MPEventSystemProvider>().fallBackToMainEventSystem=true;racePanel.AddComponent<CursorOpener>();
                var back=Button(racePanel.transform,"Back to bets",()=>SetRaceView(0));Anchors((RectTransform)back.transform,.48f,.92f,.61f,.975f);
                var track=Button(racePanel.transform,"Track view",()=>SetRaceView(1));Anchors((RectTransform)track.transform,.62f,.92f,.74f,.975f);
                var top=Button(racePanel.transform,"Top view",()=>SetRaceView(2));Anchors((RectTransform)top.transform,.75f,.92f,.87f,.975f);
                var exit=Button(racePanel.transform,"Exit · ESC",Close);Anchors((RectTransform)exit.transform,.88f,.92f,.98f,.975f);
                var custom=Button(racePanel.transform,"Custom amount",()=>OpenWagerDialog("Derby wager",activeRace.State?.minimum??Plugin.MinimumBet,Plugin.MaximumBet,RaceBudget,(amount,all)=>{raceWager=amount;raceAllIn=all;},raceWager));Anchors((RectTransform)custom.transform,.02f,.92f,.19f,.975f);
                var allIn=Button(racePanel.transform,"All In",()=>{raceAllIn=TryAllIn(RaceBudget(),Plugin.MaximumBet,out raceWager,out raceMessage);});Anchors((RectTransform)allIn.transform,.20f,.92f,.31f,.975f);
                raceViewStatus=Label(racePanel.transform,"",22,Gold);Anchors(raceViewStatus.rectTransform,.18f,.845f,.82f,.90f);
            }
            raceWager=Math.Max(machine.State?.minimum??10,raceWager);raceMessage=null;
            HideMachineAvatar();racePanel.SetActive(true);AttachSlotCamera();
        }
        internal void RaceResponse(string message){raceMessage=message=="Race wager updated."?null:message;}
        internal void RaceTimedOut(){if(IsRaceFocused)raceMessage="No host reply · reopen to refresh";}
        private void UpdateRaceOverlay()
        {
            if(!racePanel||!racePanel.activeSelf)return;
            if(!activeRace){Close();return;}
            var master=CasinoPlugin.LocalMaster;var body=master?master.GetBody():null;
            if(!CasinoPlugin.InBazaar||!body||!body.healthComponent||!body.healthComponent.alive||SceneExitController.isRunning){Close();return;}
            AttachSlotCamera();var state=activeRace.State;
            if(Input.GetKeyDown(KeyCode.V))ToggleRaceView();
            int minimum=state?.minimum??10,maximum=Math.Max(minimum,state?.maximum??100000);
            if(!raceAllIn)raceWager=Mathf.Clamp(raceWager,minimum,maximum);
            else if(RaceBudget()>0&&RaceBudget()<=(ulong)maximum)raceWager=(int)RaceBudget();
            var camera=CasinoBillboard.ViewCamera;
            bool overUI=WagerDialogOpen||MPEventSystem.readOnlyInstancesList.Any(e=>e&&e.IsPointerOverGameObject());
            int control=activeRace.Hover(camera&&!overUI?camera.ScreenPointToRay(Input.mousePosition):default,camera&&!overUI&&!Plugin.Waiting&&state?.phase==0);
            if(Input.GetMouseButtonDown(0))
            {
                if(control==4){Close();return;}
                if(control==9)ToggleRaceView();
                if(control>=0&&control<4){raceLane=control;raceMessage=null;}
                if(control==5){raceAllIn=false;raceWager=Math.Max(minimum,raceWager-minimum);}
                if(control==6){raceAllIn=false;raceWager=(int)Math.Min(maximum,(long)raceWager+minimum);}
                if(control==7||control==8){raceMessage="Waiting for host…";Plugin.Send(ActionKind.RaceBet,control==8?0:raceWager,raceLane,allIn:control==7&&raceAllIn);}
            }
            uint id=master.GetComponent<NetworkIdentity>()?.netId.Value??0;
            activeRace.ControlState(raceLane,raceWager,master.money,id,Plugin.Waiting?"Waiting for host…":raceMessage);
            if(raceViewStatus)raceViewStatus.text=activeRace.ClockText;
        }
        private void CloseRaceOverlay()
        {
            activeRace=null;
            if(racePanel){ReleaseCasinoFocus(racePanel);racePanel.SetActive(false);}
        }
    }
}
