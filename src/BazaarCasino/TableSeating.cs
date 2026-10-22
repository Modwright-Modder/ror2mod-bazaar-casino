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
        private static bool PauseSeatedState(EntityStateMachine machine)
        {
            if(!InBazaar||!machine)return false;
            var seat=machine.GetComponent<CasinoSeat>();
            return seat&&seat.PausesMovementState;
        }
        private void SuppressSeatStateUpdate(On.RoR2.EntityStateMachine.orig_ManagedUpdate orig,EntityStateMachine self)
        {if(!PauseSeatedState(self))orig(self);}
        private void SuppressSeatStateFixedUpdate(On.RoR2.EntityStateMachine.orig_ManagedFixedUpdate orig,EntityStateMachine self,float deltaTime)
        {if(!PauseSeatedState(self))orig(self,deltaTime);}
        private void SuppressSeatUpdate(On.RoR2.PlayerCharacterMasterController.orig_Update orig,PlayerCharacterMasterController self)
        {
            // Keep native button buffers polling/suppressing until released.
            // Skipping this while focused leaves the old E press buffered.
            orig(self);
            var body=self.master?self.master.GetBody():null;var seat=body?body.GetComponent<CasinoSeat>():null;
            if(seat&&seat.IsSeated){seat.ClearInput();return;}
            if(ui&&(ui.IsAnyCasinoOpen||ui.InteractionBlocked)&&self.master==LocalMaster)ClearSlotInput(body);
        }
        private void SuppressSeatFixedUpdate(On.RoR2.PlayerCharacterMasterController.orig_FixedUpdate orig,PlayerCharacterMasterController self)
        {
            orig(self);
            var body=self.master?self.master.GetBody():null;var seat=body?body.GetComponent<CasinoSeat>():null;
            if(seat&&seat.IsSeated){seat.ClearInput();return;}
            if(ui&&(ui.IsAnyCasinoOpen||ui.InteractionBlocked)&&self.master==LocalMaster)ClearSlotInput(body);
        }
        private readonly Dictionary<uint,Vector3> standingPositions=new Dictionary<uint,Vector3>();
        private readonly Dictionary<uint,SeatAnchor> seatAnchors=new Dictionary<uint,SeatAnchor>();
        private static void ClearSlotInput(CharacterBody body)
        {
            if(!body||!body.inputBank)return;var bank=body.inputBank;
            bank.moveVector=Vector3.zero;bank.skill1.PushState(false);bank.skill2.PushState(false);bank.skill3.PushState(false);bank.skill4.PushState(false);
            bank.jump.PushState(false);bank.sprint.PushState(false);bank.interact.PushState(false);bank.activateEquipment.PushState(false);bank.activateKeyItem.PushState(false);bank.ping.PushState(false);
            if(body.characterMotor){body.characterMotor.velocity=Vector3.zero;body.characterMotor.rootMotion=Vector3.zero;}
            var rigidMotor=body.GetComponent<RigidbodyMotor>();
            if(rigidMotor)rigidMotor.moveVector=Vector3.zero;
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
                if(!SeatPlacement.Accept(seat.bodyId,body.netId.Value,seat.slot,seat.returnRoot,seat.returnX,seat.returnY,seat.returnZ))continue;
                if(!seat.hasAnchor)continue;
                SeatAnchor anchor;
                try{anchor=new SeatAnchor(seat.bodyId,seat.slot,seat.anchorX,seat.anchorY,seat.anchorZ,seat.anchorYaw);}
                catch(ArgumentException){continue;}
                if(seatedBodies.TryGetValue(seat.id,out var existing)&&existing)
                {
                    if(existing.Body==body&&existing.Kind==kind)continue;
                    existing.Release(false);seatedBodies.Remove(seat.id);
                }
                var component=body.gameObject.AddComponent<CasinoSeat>();
                try
                {
                    component.Enter(body,table,anchor,new Vector3(seat.returnX,seat.returnY,seat.returnZ));
                    seatedBodies[seat.id]=component;
                }
                catch(Exception error)
                {
                    Destroy(component);
                    Logger.LogError("Could not apply casino seat to "+body.name+": "+error);
                    if(seat.id==LocalMasterId){ui.Notify("Could not apply this seat. Your movement has been restored.");ui.CloseSharedTable();}
                }
            }
        }
        private void ReleaseSeating(uint id,bool returnToFloor)
        {
            if(seatedBodies.TryGetValue(id,out var seat)){if(seat)seat.Release(returnToFloor);seatedBodies.Remove(id);}
        }
        private void ClearSeating(bool returnToFloor)
        {
            foreach(var seat in seatedBodies.Values)if(seat)seat.Release(returnToFloor);
            seatedBodies.Clear();standingPositions.Clear();seatAnchors.Clear();
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
        private bool active,motorEnabled,directionEnabled,animatorEnabled,posePending,posed,rigidMotorEnabled,rigidKinematic,rigidDirectionEnabled,rigidGravity;
        internal bool IsSeated=>active;
        internal bool PausesMovementState=>active&&CasinoPlugin.InBazaar&&anchor!=null&&Body&&Body.healthComponent&&anchor.Keeps(Body.netId.Value,ConnectionAlive,Body.healthComponent.alive,SceneExitController.isRunning);
        internal bool TopView;
        private Vector3 standing,rootOffset,modelPosition;
        private Quaternion modelRotation;
        private CharacterMotor motor;
        private CharacterDirection direction;
        private RigidbodyMotor rigidMotor;
        private RigidbodyDirection rigidDirection;
        private Rigidbody rigid;
        private RigidbodyConstraints rigidConstraints;
        private RigidbodyInterpolation rigidInterpolation;
        private Transform hipsBone;
        private Transform model;
        private Animator animator;
        private readonly Dictionary<Transform,Quaternion> bones=new Dictionary<Transform,Quaternion>();
        private readonly List<CameraRigController> cameras=new List<CameraRigController>();
        private SeatAnchor anchor;
        private bool ownTeleport,rigidCollisions;
        private float nextPinDiagnostic;
        private Vector3 modelOffset,rigidOffset;
        private Quaternion modelFacing,rigidFacing;
        private readonly Dictionary<Behaviour,bool> extraMotors=new Dictionary<Behaviour,bool>();
        private readonly Dictionary<Collider,bool> collisions=new Dictionary<Collider,bool>();
        internal bool BlocksForeignTeleport=>PausesMovementState&&!ownTeleport;
        internal static float Clearance(CharacterBody body)
        {
            var motor=body.characterMotor;var rigidMotor=body.GetComponent<RigidbodyMotor>();
            var rigid=rigidMotor&&rigidMotor.rigid?rigidMotor.rigid:body.GetComponent<Rigidbody>();
            float bottom=0;
            foreach(var collider in body.GetComponentsInChildren<Collider>())
                if(collider&&collider.enabled&&!collider.isTrigger&&(!rigid||collider.attachedRigidbody==rigid))
                    bottom=Mathf.Max(bottom,((Component)body).transform.position.y-collider.bounds.min.y);
            return SeatPlacement.RootHeight(motor?motor.capsuleHeight:0,bottom);
        }
        internal void Enter(CharacterBody body,WorldTable target,SeatAnchor assigned,Vector3 returnPosition)
        {
            Body=body;table=target;anchor=assigned;slot=assigned.Slot;standing=returnPosition;
            motor=body.characterMotor;direction=body.characterDirection;
            rigidMotor=body.GetComponent<RigidbodyMotor>();rigidDirection=body.GetComponent<RigidbodyDirection>();
            rigid=rigidMotor&&rigidMotor.rigid?rigidMotor.rigid:body.GetComponent<Rigidbody>();
            rootOffset=Vector3.up*(anchor.Y-target.ChairFoot(slot).y);
            // Capture everything before modifying movement, so failed entry can
            // restore flying and nonstandard survivors without changing state.
            if(motor)motorEnabled=motor.enabled;
            if(direction)directionEnabled=direction.enabled;
            if(rigidMotor)rigidMotorEnabled=rigidMotor.enabled;
            if(rigidDirection)rigidDirectionEnabled=rigidDirection.enabled;
            if(rigid){rigidKinematic=rigid.isKinematic;rigidGravity=rigid.useGravity;rigidConstraints=rigid.constraints;rigidInterpolation=rigid.interpolation;rigidCollisions=rigid.detectCollisions;
                rigidOffset=((Component)body).transform.InverseTransformPoint(rigid.position);rigidFacing=Quaternion.Inverse(((Component)body).transform.rotation)*rigid.rotation;}
            foreach(var component in body.GetComponentsInChildren<MonoBehaviour>(true))
                if(component&&component is IPhysMotor&&component!=motor&&component!=rigidMotor)extraMotors[component]=component.enabled;
            foreach(var collider in body.GetComponentsInChildren<Collider>(true))
                if(collider&&!collider.isTrigger)collisions[collider]=collider.enabled;
            model=body.modelLocator?body.modelLocator.modelTransform:null;
            if(model)
            {
                modelPosition=model.localPosition;modelRotation=model.localRotation;
                modelOffset=((Component)body).transform.InverseTransformPoint(model.position);modelFacing=Quaternion.Inverse(((Component)body).transform.rotation)*model.rotation;
                animator=body.modelLocator.modelAnimator?body.modelLocator.modelAnimator:model.GetComponent<Animator>();
                if(animator)animatorEnabled=animator.enabled;
            }
            TopView=target.Kind==TableKind.Roulette;active=true;posePending=true;
            try
            {
                // Reset interpolation on the owner; a host teleport alone is
                // ignored by that owner. Retain the exact current entity state.
                Camera.onPreCull+=BeforeSeatRender;
                if(Util.HasEffectiveAuthority(body.gameObject))
                {
                    ownTeleport=true;
                    try{TeleportHelper.TeleportGameObject(body.gameObject,new Vector3(anchor.X,anchor.Y,anchor.Z),Quaternion.Euler(0,anchor.Yaw,0),true);}
                    finally{ownTeleport=false;}
                }
                LockBody();AttachCamera();
                if(Util.HasEffectiveAuthority(body.gameObject))Debug.Log("[BazaarCasino] Local seat applied: "+Kind+", slot "+slot+", body "+body.netId.Value+" ("+body.name+"), root "+((Component)body).transform.position+", clearance "+rootOffset.y+", rigid motor "+(rigidMotor!=null)+", rigid direction "+(rigidDirection!=null)+", camera overrides "+cameras.Count);
            }
            catch{Release(true);throw;}
        }
        private bool ConnectionAlive=>NetworkServer.active||NetworkClient.allClients.Any(c=>c!=null&&c.isConnected);
        private void AttachCamera()
        {
            if(Body!= (CasinoPlugin.LocalMaster?CasinoPlugin.LocalMaster.GetBody():null))return;
            foreach(var rig in CameraRigController.readOnlyInstancesList)
                if(rig&&rig.targetBody==Body&&!rig.IsOverrideCam(this))
                {rig.SetOverrideCam(this,.1f);if(!cameras.Contains(rig))cameras.Add(rig);}
        }
        private void LockBody()
        {
            Vector3 position=((Component)Body).transform.position;float yaw=((Component)Body).transform.eulerAngles.y;
            anchor.Pin(ref position.x,ref position.y,ref position.z,ref yaw);
            var facing=Quaternion.Euler(0,yaw,0);
            foreach(var pair in extraMotors)if(pair.Key)pair.Key.enabled=false;
            foreach(var pair in collisions)if(pair.Key)pair.Key.enabled=false;
            if(motor){motor.enabled=false;motor.velocity=Vector3.zero;motor.rootMotion=Vector3.zero;if(motor.Motor)motor.Motor.SetPosition(position);}
            if(rigidMotor){rigidMotor.enabled=false;rigidMotor.moveVector=Vector3.zero;}
            if(rigidDirection){rigidDirection.enabled=false;rigidDirection.aimDirection=facing*Vector3.forward;}
            if(rigid)
            {
                if(!rigid.isKinematic){rigid.velocity=Vector3.zero;rigid.angularVelocity=Vector3.zero;}
                rigid.isKinematic=true;rigid.useGravity=false;rigid.constraints=RigidbodyConstraints.FreezeAll;rigid.interpolation=RigidbodyInterpolation.None;rigid.detectCollisions=false;
            }
            ((Component)Body).transform.SetPositionAndRotation(position,facing);
            // Follow the motor's actual Rigidbody, which need not be the body root.
            if(rigid){rigid.position=((Component)Body).transform.TransformPoint(rigidOffset);rigid.rotation=facing*rigidFacing;}
            if(model){model.SetPositionAndRotation(((Component)Body).transform.TransformPoint(modelOffset),facing*modelFacing);
                if(posed&&hipsBone)model.position+=Vector3.up*(table.transform.position.y+.78f-hipsBone.position.y);}
            if(direction){direction.enabled=false;direction.pitch=0;direction.yaw=facing.eulerAngles.y;direction.moveVector=Vector3.zero;}
            if(animator)animator.enabled=false;
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
        internal void PinNow(string reason)
        {
            if(!PausesMovementState||!table)return;
            LockBody();
            if(Time.unscaledTime>=nextPinDiagnostic)
            {nextPinDiagnostic=Time.unscaledTime+3;Debug.Log("[BazaarCasino] Seat anchor enforced: body "+Body.netId.Value+", "+reason+", anchor "+new Vector3(anchor.X,anchor.Y,anchor.Z));}
        }
        private void BeforeSeatRender(Camera camera)
        {if(PausesMovementState&&table){LockBody();AttachCamera();}}
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
        private void FixedUpdate()
        {
            if(!active)return;
            if(!PausesMovementState||!table||!ConnectionAlive)return;
            // Runs after movement FixedUpdates but before the physics solver.
            // A render-only lock lets flight forces/network poses escape between frames.
            LockBody();
        }
        private void LateUpdate()
        {
            if(!active)return;
            if(!CasinoPlugin.InBazaar||!table||!Body||!Body.healthComponent||!Body.healthComponent.alive||anchor==null||Body.netId.Value!=anchor.BodyId||SceneExitController.isRunning||!ConnectionAlive)
            {Release(CasinoPlugin.InBazaar&&!SceneExitController.isRunning,reason:!ConnectionAlive?"network disconnected":"body, table or scene changed");return;}
            LockBody();
            if(posePending){posePending=false;Pose();}
            if(posed&&hipsBone&&model)model.position+=Vector3.up*(table.transform.position.y+.78f-hipsBone.position.y);
            AttachCamera();
        }
        internal void Release(bool returnToFloor,bool destroy=true,string reason="seat closed or host released it")
        {
            if(!active)return;active=false;
            Camera.onPreCull-=BeforeSeatRender;
            foreach(var rig in cameras)if(rig&&rig.IsOverrideCam(this))rig.SetOverrideCam(null,.35f);
            cameras.Clear();
            foreach(var pair in bones)if(pair.Key)pair.Key.localRotation=pair.Value;bones.Clear();
            if(model){model.localPosition=modelPosition;model.localRotation=modelRotation;}
            if(animator)animator.enabled=animatorEnabled;
            if(direction)direction.enabled=directionEnabled;
            if(motor){motor.enabled=motorEnabled;motor.velocity=Vector3.zero;motor.rootMotion=Vector3.zero;}
            if(rigidMotor)rigidMotor.enabled=rigidMotorEnabled;
            if(rigidDirection)rigidDirection.enabled=rigidDirectionEnabled;
            foreach(var pair in extraMotors)if(pair.Key)pair.Key.enabled=pair.Value;extraMotors.Clear();
            foreach(var pair in collisions)if(pair.Key)pair.Key.enabled=pair.Value;collisions.Clear();
            if(rigid){rigid.isKinematic=rigidKinematic;rigid.useGravity=rigidGravity;rigid.constraints=rigidConstraints;rigid.interpolation=rigidInterpolation;rigid.detectCollisions=rigidCollisions;}
            if(returnToFloor&&Body&&Body.healthComponent&&Body.healthComponent.alive)
            {
                // Only authority sends a teleport; observers restore their replica silently.
                if(Util.HasEffectiveAuthority(Body.gameObject))TeleportHelper.TeleportGameObject(Body.gameObject,standing,((Component)Body).transform.rotation,false);
                else {((Component)Body).transform.position=standing;if(motor&&motor.Motor)motor.Motor.SetPosition(standing);}
                if(rigid)rigid.position=rigid.transform.position;
            }
            if(Body&&Util.HasEffectiveAuthority(Body.gameObject))Debug.Log("[BazaarCasino] Local seat released: "+Kind+", body "+Body.netId.Value+", reason "+reason+", root "+((Component)Body).transform.position);
            if(destroy)Destroy(this);
        }
        public void GetCameraState(CameraRigController rig,ref CameraState state)
        {
            if(!table)return;table.TableCamera(TopView,out var position,out var target);
            state.position=position;state.rotation=Quaternion.LookRotation(target-position,TopView?table.transform.forward:Vector3.up);state.fov=TopView?55:58;
        }
        public bool IsUserLookAllowed(CameraRigController rig)=>!active;
        public bool IsUserControlAllowed(CameraRigController rig)=>!active;
        public bool IsHudAllowed(CameraRigController rig)=>!active;
        private void OnDestroy(){Release(false,false);}
    }
}
