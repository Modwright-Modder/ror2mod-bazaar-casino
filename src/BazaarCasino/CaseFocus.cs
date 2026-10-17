using System.Linq;
using RoR2;
using RoR2.UI;
using UnityEngine;

namespace BazaarCasino
{
    internal sealed partial class CasinoUI
    {
        private GameObject casePanel;
        private WorldCaseMachine activeCase;
        private string caseMessage;
        internal bool IsCaseOpen=>casePanel&&casePanel.activeSelf&&activeCase;
        internal bool IsWorldMachineFocused=>IsSlotFocused||IsCaseOpen||IsRaceFocused;
        private void BuildCaseOverlay()
        {
            if(casePanel)return;
            casePanel=Rect(canvasRoot.transform,"CacheCursorLifetime").gameObject;Stretch((RectTransform)casePanel.transform);casePanel.SetActive(false);
            casePanel.AddComponent<MPEventSystemProvider>().fallBackToMainEventSystem=true;casePanel.AddComponent<CursorOpener>();
        }
        internal void OpenCase(WorldCaseMachine machine)
        {
            if(!machine||!CasinoPlugin.InBazaar||!CasinoPlugin.LocalMaster||!Init())return;
            Close();activeCase=machine;BuildCaseOverlay();HideMachineAvatar();casePanel.SetActive(true);machine.SetFocus(true);
            caseMessage=null;machine.Preview(State);AttachSlotCamera();Plugin.Send(ActionKind.State);
        }
        private void CaseResponse(CasinoState state)
        {
            activeCase?.Receive(state);
            caseMessage=state.game==5||state.message=="Welcome to the Bazaar Casino."?null:state.message;
        }
        private void UpdateCaseOverlay()
        {
            if(!casePanel||!casePanel.activeSelf)return;
            var body=CasinoPlugin.LocalMaster?CasinoPlugin.LocalMaster.GetBody():null;
            if(!CasinoPlugin.InBazaar||!activeCase||!body||!body.healthComponent||!body.healthComponent.alive||SceneExitController.isRunning){Close();return;}
            AttachSlotCamera();var state=State;
            bool enabled=state?.caseEnabled==true&&state.casePrice>0&&state.caseWeights?.Sum()>0&&!activeCase.IsOpening&&!Plugin.Waiting;
            var camera=CasinoBillboard.ViewCamera;
            bool overUI=MPEventSystem.readOnlyInstancesList.Any(e=>e&&e.IsPointerOverGameObject());
            int control=activeCase.HoverControls(camera?camera.ScreenPointToRay(Input.mousePosition):default,camera&&!overUI&&enabled);
            if(Input.GetMouseButtonDown(0))
            {
                if(control==4){Close();return;}
                if(enabled&&(control==1||control==2||control==3)){activeCase.SelectCount(control==1?1:control==2?5:10);caseMessage=null;}
                if(control==0&&enabled&&CasinoPlugin.LocalMaster.money>=(uint)CaseBatchRules.Total(state.casePrice,activeCase.SelectedCount))
                {caseMessage="Waiting for host…";Plugin.Send(ActionKind.CaseRedeem,CaseBatchRules.Total(state.casePrice,activeCase.SelectedCount),number:activeCase.SelectedCount);}
            }
            string message=caseMessage;
            if(!activeCase.IsOpening&&!Plugin.Waiting&&state?.caseEnabled==true&&state.casePrice>0&&CasinoPlugin.LocalMaster.money<(uint)CaseBatchRules.Total(state.casePrice,activeCase.SelectedCount))message="Not enough gold";
            activeCase.ControlState(state,CasinoPlugin.LocalMaster.money,Plugin.Waiting,message);
        }
        private void CloseCaseOverlay()
        {
            if(activeCase)activeCase.SetFocus(false);activeCase=null;
            if(casePanel){ReleaseCasinoFocus(casePanel);casePanel.SetActive(false);}
        }
        internal void CaseTimedOut(){if(IsCaseOpen)caseMessage="No host reply · Reopen to refresh";}
    }
}
