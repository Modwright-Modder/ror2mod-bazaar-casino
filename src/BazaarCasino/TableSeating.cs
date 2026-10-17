using System;
using System.Collections.Generic;
using System.Linq;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace BazaarCasino
{
    public sealed partial class CasinoPlugin
    {
        private void SuppressSeatUpdate(On.RoR2.PlayerCharacterMasterController.orig_Update orig,PlayerCharacterMasterController self)
        {
            // Keep native button buffers polling/suppressing until released.
            // Skipping this while focused leaves the old E press buffered.
            orig(self);
            var body=self.master?self.master.GetBody():null;var seat=body?body.GetComponent<CasinoSeat>():null;
            if(seat&&seat.IsSeated){seat.ClearInput();return;}
            if(ui&&(ui.IsWorldMachineFocused||ui.InteractionBlocked)&&self.master==LocalMaster)ClearSlotInput(body);
        }
        private void SuppressSeatFixedUpdate(On.RoR2.PlayerCharacterMasterController.orig_FixedUpdate orig,PlayerCharacterMasterController self)
        {
            orig(self);
            var body=self.master?self.master.GetBody():null;var seat=body?body.GetComponent<CasinoSeat>():null;
            if(seat&&seat.IsSeated){seat.ClearInput();return;}
            if(ui&&(ui.IsWorldMachineFocused||ui.InteractionBlocked)&&self.master==LocalMaster)ClearSlotInput(body);
        }
        private readonly Dictionary<uint,Vector3> standingPositions=new Dictionary<uint,Vector3>();
        private static void ClearSlotInput(CharacterBody body)
        {
            if(!body||!body.inputBank)return;var bank=body.inputBank;
            bank.moveVector=Vector3.zero;bank.skill1.PushState(false);bank.skill2.PushState(false);bank.skill3.PushState(false);bank.skill4.PushState(false);
            bank.jump.PushState(false);bank.sprint.PushState(false);bank.interact.PushState(false);bank.activateEquipment.PushState(false);bank.activateKeyItem.PushState(false);bank.ping.PushState(false);
            if(body.characterMotor){body.characterMotor.velocity=Vector3.zero;body.characterMotor.rootMotion=Vector3.zero;}
        }
        private readonly Dictionary<uint,CasinoSeat> seatedBodies=new Dictionary<uint,CasinoSeat>();
        private TableKind pendingTableExit;
        private float nextSeatExit;
        private static uint LocalMasterId=>LocalMaster&&LocalMaster.GetComponent<NetworkIdentity>()?LocalMaster.GetComponent<NetworkIdentity>().netId.Value:0;
        internal void RequestTableExit(TableKind kind)
        {
            pendingTableExit=kind;nextSeatExit=0;
            ReleaseSeating(LocalMasterId,true);
        }
        private void UpdateTableExit()
        {
            if(pendingTableExit==0||!InBazaar||Waiting||Time.unscaledTime<nextSeatExit)return;
            nextSeatExit=Time.unscaledTime+.35f;Send(ActionKind.TableLeave,table:pendingTableExit);
        }
        private void ApplySeating(TableSnapshot snapshot,WorldTable table)
        {
            var kind=(TableKind)snapshot.kind;
            foreach(var pair in seatedBodies.ToArray())
                if(!pair.Value || pair.Value.Kind==kind&&!snapshot.seats.Any(s=>s.id==pair.Key&&s.seated))
                {
                    if(pair.Value)pair.Value.Release(true);
                    seatedBodies.Remove(pair.Key);
                }
            foreach(var seat in snapshot.seats.Where(s=>s.seated))
            {
                if(seat.id==LocalMasterId&&(pendingTableExit==kind||!ui.IsSharedTableOpen(kind)))continue;
                var master=FindMaster(seat.id);var body=master?master.GetBody():null;
                if(!body||!body.healthComponent||!body.healthComponent.alive)continue;
                if(seatedBodies.TryGetValue(seat.id,out var existing)&&existing)
                {
                    if(existing.Body==body&&existing.Kind==kind){existing.Refresh();continue;}
                    existing.Release(false);seatedBodies.Remove(seat.id);
                }
                var component=body.gameObject.AddComponent<CasinoSeat>();
                component.Enter(body,table,seat.slot,new Vector3(seat.returnX,seat.returnY,seat.returnZ));
                seatedBodies[seat.id]=component;
            }
        }
        private void ReleaseSeating(uint id,bool returnToFloor)
        {
            if(seatedBodies.TryGetValue(id,out var seat)){if(seat)seat.Release(returnToFloor);seatedBodies.Remove(id);}
        }
        private void ClearSeating(bool returnToFloor)
        {
            foreach(var seat in seatedBodies.Values)if(seat)seat.Release(returnToFloor);
            seatedBodies.Clear();standingPositions.Clear();
        }
    }

    // Replicated by host snapshots. No local seat GameObject is sent through UNet.
    // Every modified component/property is restored when the seat is released.
    [DefaultExecutionOrder(10000)]
    internal sealed class CasinoSeat : MonoBehaviour,ICameraStateProvider
    {
        internal CharacterBody Body {get;private set;}
        internal TableKind Kind=>table?table.Kind:0;
        private WorldTable table;
        private int slot;
        private bool active,motorEnabled,directionEnabled,animatorEnabled,posePending,posed,rigidMotorEnabled,rigidKinematic;
        internal bool IsSeated=>active;
        internal bool TopView;
        private Vector3 standing,rootOffset,modelPosition;
        private Quaternion modelRotation;
        private CharacterMotor motor;
        private CharacterDirection direction;
        private RigidbodyMotor rigidMotor;
        private Rigidbody rigid;
        private Transform hipsBone;
        private Transform model;
        private Animator animator;
        private readonly Dictionary<Transform,Quaternion> bones=new Dictionary<Transform,Quaternion>();
        private readonly List<CameraRigController> cameras=new List<CameraRigController>();
        private float refreshed;
        internal void Enter(CharacterBody body,WorldTable target,int index,Vector3 returnPosition)
        {
            Body=body;table=target;slot=index;standing=returnPosition;
            rootOffset=((Component)body).transform.position-body.footPosition;
            motor=body.characterMotor;direction=body.characterDirection;
            rigidMotor=body.GetComponent<RigidbodyMotor>();rigid=body.GetComponent<Rigidbody>();
            if(motor){motorEnabled=motor.enabled;motor.velocity=Vector3.zero;motor.rootMotion=Vector3.zero;motor.enabled=false;}
            if(direction){directionEnabled=direction.enabled;direction.enabled=false;}
            if(rigidMotor){rigidMotorEnabled=rigidMotor.enabled;rigidMotor.enabled=false;}
            if(rigid){rigidKinematic=rigid.isKinematic;if(!rigidKinematic){rigid.velocity=Vector3.zero;rigid.angularVelocity=Vector3.zero;}rigid.isKinematic=true;}
            model=body.modelLocator?body.modelLocator.modelTransform:null;
            if(model)
            {
                modelPosition=model.localPosition;modelRotation=model.localRotation;
                animator=body.modelLocator.modelAnimator?body.modelLocator.modelAnimator:model.GetComponent<Animator>();
                if(animator){animatorEnabled=animator.enabled;animator.enabled=false;}
            }
            TopView=target.Kind==TableKind.Roulette;active=true;posePending=true;Refresh();LockBody();AttachCamera();
        }
        internal void Refresh()=>refreshed=Time.unscaledTime;
        private void AttachCamera()
        {
            if(Body!= (CasinoPlugin.LocalMaster?CasinoPlugin.LocalMaster.GetBody():null))return;
            foreach(var rig in CameraRigController.readOnlyInstancesList)
                if(rig&&rig.targetBody==Body&&!cameras.Contains(rig)&&!rig.hasOverride)
                {rig.SetOverrideCam(this,.4f);cameras.Add(rig);}
        }
        private void LockBody()
        {
            Vector3 position=table.ChairFoot(slot)+rootOffset;
            if(motor){motor.velocity=Vector3.zero;motor.rootMotion=Vector3.zero;if(motor.Motor)motor.Motor.SetPosition(position);}
            ((Component)Body).transform.position=position;
            var facing=table.ChairFacing(slot);
            if(direction){direction.pitch=0;direction.yaw=facing.eulerAngles.y;direction.moveVector=Vector3.zero;}
            else ((Component)Body).transform.rotation=facing;
            ClearInput();Body.isSprinting=false;
        }
        internal void ClearInput()
        {
            if(Body&&Body.inputBank)
            {
                var bank=Body.inputBank;bank.moveVector=Vector3.zero;bank.aimDirection=table?table.ChairFacing(slot)*Vector3.forward:((Component)Body).transform.forward;
                bank.skill1.PushState(false);bank.skill2.PushState(false);bank.skill3.PushState(false);bank.skill4.PushState(false);
                bank.jump.PushState(false);bank.sprint.PushState(false);bank.interact.PushState(false);
                bank.activateEquipment.PushState(false);bank.activateKeyItem.PushState(false);bank.ping.PushState(false);
            }
        }
        private Transform Bone(HumanBodyBones human,params string[] names)
        {
            if(animator&&animator.isHuman){var bone=animator.GetBoneTransform(human);if(bone)return bone;}
            if(!model)return null;
            return model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>names.Any(n=>string.Equals(t.name,n,StringComparison.OrdinalIgnoreCase)));
        }
        private void Remember(Transform bone){if(bone&&!bones.ContainsKey(bone))bones[bone]=bone.localRotation;}
        private void Pose()
        {
            if(!model)return;
            var hips=Bone(HumanBodyBones.Hips,"pelvis","hips","hip","mixamorig:Hips","mixamorigHips");
            var left=Bone(HumanBodyBones.LeftUpperLeg,"thigh.l","thigh_l","L_Thigh","upperleg.l","mixamorig:LeftUpLeg","mixamorigLeftUpLeg","LeftUpLeg","LeftUpperLeg","L_UpperLeg","thighL");
            var right=Bone(HumanBodyBones.RightUpperLeg,"thigh.r","thigh_r","R_Thigh","upperleg.r","mixamorig:RightUpLeg","mixamorigRightUpLeg","RightUpLeg","RightUpperLeg","R_UpperLeg","thighR");
            var leftShin=Bone(HumanBodyBones.LeftLowerLeg,"calf.l","calf_l","shin.l","L_Calf","mixamorig:LeftLeg","mixamorigLeftLeg","LeftLeg","LeftLowerLeg","L_LowerLeg","calfL");
            var rightShin=Bone(HumanBodyBones.RightLowerLeg,"calf.r","calf_r","shin.r","R_Calf","mixamorig:RightLeg","mixamorigRightLeg","RightLeg","RightLowerLeg","R_LowerLeg","calfR");
            var leftFoot=Bone(HumanBodyBones.LeftFoot,"foot.l","foot_l","L_Foot","mixamorig:LeftFoot","mixamorigLeftFoot","LeftFoot","footL");
            var rightFoot=Bone(HumanBodyBones.RightFoot,"foot.r","foot_r","R_Foot","mixamorig:RightFoot","mixamorigRightFoot","RightFoot","footR");
            // Non-biped survivors keep their native pose; do not distort an unknown rig.
            if(!hips||!left||!right||!leftShin||!rightShin)return;
            foreach(var bone in new[]{left,right,leftShin,rightShin,leftFoot,rightFoot})Remember(bone);
            hipsBone=hips;posed=true;
            model.position+=Vector3.up*(table.transform.position.y+.78f-hips.position.y);
            Vector3 forward=table.ChairFacing(slot)*Vector3.forward;
            Bend(left,leftShin,forward-Vector3.up*.06f);Bend(right,rightShin,forward-Vector3.up*.06f);
            Bend(leftShin,leftFoot,Vector3.down);Bend(rightShin,rightFoot,Vector3.down);
        }
        private static void Bend(Transform bone,Transform child,Vector3 vector)
        {if(bone&&child&&(child.position-bone.position).sqrMagnitude>.001f)bone.rotation=Quaternion.FromToRotation(child.position-bone.position,vector)*bone.rotation;}
        private void LateUpdate()
        {
            if(!active)return;
            if(!CasinoPlugin.InBazaar||!table||!Body||!Body.healthComponent||!Body.healthComponent.alive||SceneExitController.isRunning||Time.unscaledTime-refreshed>6)
            {Release(CasinoPlugin.InBazaar&&!SceneExitController.isRunning);return;}
            LockBody();
            if(posePending){posePending=false;Pose();}
            if(posed&&hipsBone&&model)model.position+=Vector3.up*(table.transform.position.y+.78f-hipsBone.position.y);
            AttachCamera();
        }
        internal void Release(bool returnToFloor,bool destroy=true)
        {
            if(!active)return;active=false;
            foreach(var rig in cameras)if(rig&&rig.IsOverrideCam(this))rig.SetOverrideCam(null,.35f);
            cameras.Clear();
            foreach(var pair in bones)if(pair.Key)pair.Key.localRotation=pair.Value;bones.Clear();
            if(model){model.localPosition=modelPosition;model.localRotation=modelRotation;}
            if(animator)animator.enabled=animatorEnabled;
            if(direction)direction.enabled=directionEnabled;
            if(motor){motor.enabled=motorEnabled;motor.velocity=Vector3.zero;motor.rootMotion=Vector3.zero;}
            if(rigid)rigid.isKinematic=rigidKinematic;
            if(rigidMotor)rigidMotor.enabled=rigidMotorEnabled;
            if(returnToFloor&&Body&&Body.healthComponent&&Body.healthComponent.alive)
            {
                // Only authority sends a teleport; observers restore their replica silently.
                if(NetworkServer.active||Util.HasEffectiveAuthority(Body.gameObject))TeleportHelper.TeleportBody(Body,standing,false);
                else {var position=standing+rootOffset;((Component)Body).transform.position=position;if(motor&&motor.Motor)motor.Motor.SetPosition(position);}
            }
            if(destroy)Destroy(this);
        }
        public void GetCameraState(CameraRigController rig,ref CameraState state)
        {
            if(!table)return;table.TableCamera(TopView,out var position,out var target);
            state.position=position;state.rotation=Quaternion.LookRotation(target-position,TopView?table.transform.forward:Vector3.up);state.fov=TopView?55:58;
        }
        public bool IsUserLookAllowed(CameraRigController rig)=>false;
        public bool IsUserControlAllowed(CameraRigController rig)=>false;
        public bool IsHudAllowed(CameraRigController rig)=>!active;
        private void OnDestroy(){Release(false,false);}
    }
}
