using System.Collections.Generic;
using System.Linq;
using RoR2;
using RoR2.UI;
using UnityEngine;

namespace BazaarCasino
{
    internal sealed partial class CasinoUI : ICameraStateProvider
    {
        // Invisible screen roots only own cursor lifetime. Cabinets draw all
        // visible controls with world canvases and raycast their own panels.
        private GameObject slotPanel;
        private bool slotAllIn;
        private CharacterModel slotModel;
        private readonly Dictionary<Renderer,bool> machineAvatarRenderers=new Dictionary<Renderer,bool>();
        private readonly List<CameraRigController> slotCameras=new List<CameraRigController>();
        private string slotResponse="Click lever to spin";
        internal bool IsSlotFocused=>slotPanel&&slotPanel.activeSelf&&activeSlotMachine;
        private void OpenSlotOverlay(WorldSlotMachine machine)
        {
            if(!machine||!CasinoPlugin.InBazaar||!CasinoPlugin.LocalMaster||!Init())return;
            Close();slotAllIn=false;activeSlotMachine=machine;bet=Mathf.Clamp(bet,Plugin.MinimumBet,Plugin.MaximumBet);BuildSlotOverlay();HideMachineAvatar();
            slotResponse="Click lever to spin";slotPanel.SetActive(true);machine.SetFocus(true);AttachSlotCamera();Plugin.Send(ActionKind.State);
        }
        private void BuildSlotOverlay()
        {
            if(slotPanel)return;
            slotPanel=Rect(canvasRoot.transform,"SlotCursorLifetime").gameObject;Stretch((RectTransform)slotPanel.transform);slotPanel.SetActive(false);
            slotPanel.AddComponent<MPEventSystemProvider>().fallBackToMainEventSystem=true;slotPanel.AddComponent<CursorOpener>();
            var custom=Button(slotPanel.transform,"Custom amount",()=>OpenWagerDialog("Slot wager",Plugin.MinimumBet,Plugin.MaximumBet,()=>CasinoPlugin.LocalMaster.money,(amount,all)=>{bet=amount;slotAllIn=all;},bet));
            Anchors((RectTransform)custom.transform,.32f,.06f,.49f,.115f);
            var allIn=Button(slotPanel.transform,"All In",()=>SetSlotAllIn());Anchors((RectTransform)allIn.transform,.51f,.06f,.68f,.115f);
        }
        private void HideMachineAvatar()
        {
            var body=CasinoPlugin.LocalMaster?CasinoPlugin.LocalMaster.GetBody():null;
            slotModel=body&&body.modelLocator&&body.modelLocator.modelTransform?body.modelLocator.modelTransform.GetComponent<CharacterModel>():null;
            if(slotModel)slotModel.invisibilityCount++;
            // Child effects such as shield bubbles may be outside CharacterModel.
            // Render flags are cosmetic and are restored exactly on exit.
            if(body)foreach(var renderer in body.GetComponentsInChildren<Renderer>(true))
                if(renderer&&!machineAvatarRenderers.ContainsKey(renderer)){machineAvatarRenderers[renderer]=renderer.forceRenderingOff;renderer.forceRenderingOff=true;}
        }
        private void PullWorldSlot()
        {
            if(!IsSlotFocused||activeSlotMachine.IsSpinning||Plugin.Waiting)return;
            if(slotAllIn&&!TryAllIn(CasinoPlugin.LocalMaster.money,Plugin.MaximumBet,out bet,out slotResponse))return;
            slotResponse="Waiting for host…";Plugin.Send(ActionKind.Slots,bet,number:activeSlotMachine.Index+1,allIn:slotAllIn);
        }
        private void SlotResponse(CasinoState state)
        {
            // The world result appears when the reel animation settles; the
            // early payout ACK must not spoil or overwrite the visible result.
            if(state.game==3)slotResponse=null;
            else if(state.message!="Welcome to the Bazaar Casino.")slotResponse=state.message;
        }
        internal void SlotTimedOut(){if(IsSlotFocused)slotResponse="Host did not respond. Reopen machine.";}
        private void AttachSlotCamera()
        {
            var body=CasinoPlugin.LocalMaster?CasinoPlugin.LocalMaster.GetBody():null;
            foreach(var rig in CameraRigController.readOnlyInstancesList)
                if(rig&&body&&rig.targetBody==body&&!slotCameras.Contains(rig)&&!rig.hasOverride)
                {rig.SetOverrideCam(this,.35f);slotCameras.Add(rig);}
        }
        private void UpdateSlotOverlay()
        {
            if(!slotPanel||!slotPanel.activeSelf)return;
            var body=CasinoPlugin.LocalMaster?CasinoPlugin.LocalMaster.GetBody():null;
            if(!CasinoPlugin.InBazaar||!activeSlotMachine||!body||!body.healthComponent||!body.healthComponent.alive||SceneExitController.isRunning){Close();return;}
            AttachSlotCamera();bool busy=activeSlotMachine.IsSpinning||Plugin.Waiting||WagerDialogOpen;
            if(slotAllIn&&CasinoPlugin.LocalMaster.money>0&&CasinoPlugin.LocalMaster.money<=Plugin.MaximumBet)bet=(int)CasinoPlugin.LocalMaster.money;
            var camera=CasinoBillboard.ViewCamera;
            bool overUI=WagerDialogOpen||MPEventSystem.readOnlyInstancesList.Any(e=>e&&e.IsPointerOverGameObject());
            var ray=camera?camera.ScreenPointToRay(Input.mousePosition):default;
            int control=activeSlotMachine.HoverControls(ray,camera&&!overUI&&!busy);
            bool lever=camera&&!overUI&&activeSlotMachine.HitLever(ray);
            activeSlotMachine.HoverLever(lever&&!busy);
            if(Input.GetMouseButtonDown(0))
            {
                if(control==4){Close();return;}
                if(!busy)
                {
                    if(control==0)SetBet((long)bet-Plugin.MinimumBet);else if(control==1)SetBet(bet/10);
                    else if(control==2)SetBet((long)bet*10);else if(control==3)SetBet((long)bet+Plugin.MinimumBet);
                    else if(lever&&CasinoPlugin.LocalMaster.money>=bet)PullWorldSlot();
                }
            }
            string notice=Plugin.Waiting?"Waiting for host…":CasinoPlugin.LocalMaster.money<bet?"Not enough gold":slotResponse;
            activeSlotMachine.ControlState(bet,CasinoPlugin.LocalMaster.money,notice);
        }
        private void CloseSlotOverlay()
        {
            foreach(var rig in slotCameras)if(rig&&rig.IsOverrideCam(this))rig.SetOverrideCam(null,.35f);slotCameras.Clear();
            foreach(var pair in machineAvatarRenderers)if(pair.Key)pair.Key.forceRenderingOff=pair.Value;machineAvatarRenderers.Clear();
            if(slotModel){slotModel.invisibilityCount=Mathf.Max(0,slotModel.invisibilityCount-1);slotModel=null;}
            if(slotPanel){ReleaseCasinoFocus(slotPanel);slotPanel.SetActive(false);}
            if(activeSlotMachine)activeSlotMachine.SetFocus(false);activeSlotMachine=null;
        }
        public void GetCameraState(CameraRigController rig,ref CameraState state)
        {
            if(!IsWorldMachineFocused)return;
            if(IsRaceFocused)
            {
                activeRace.View(out var position,out var look,out float fov);state.position=position;
                state.rotation=Quaternion.LookRotation(look-position,activeRace.Overview?activeRace.transform.forward:Vector3.up);state.fov=fov;return;
            }
            bool slot=IsSlotFocused;var cabinet=slot?activeSlotMachine.transform:activeCase.transform;
            state.position=cabinet.TransformPoint(slot?new Vector3(.35f,2.25f,-4.7f):new Vector3(0,activeCase.CameraHeight+.05f,-activeCase.CameraDistance));
            var target=cabinet.TransformPoint(slot?new Vector3(.25f,1.95f,0):new Vector3(0,activeCase.CameraHeight,0));
            state.rotation=Quaternion.LookRotation(target-state.position,Vector3.up);state.fov=slot?52:45;
        }
        public bool IsUserLookAllowed(CameraRigController rig)=>!IsWorldMachineFocused;
        public bool IsUserControlAllowed(CameraRigController rig)=>!IsWorldMachineFocused;
        public bool IsHudAllowed(CameraRigController rig)=>!IsWorldMachineFocused;
    }
}
