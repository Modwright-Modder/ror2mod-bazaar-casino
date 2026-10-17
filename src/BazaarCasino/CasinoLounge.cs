using System;
using System.Collections.Generic;
using System.Linq;
using RoR2;
using TMPro;
using UnityEngine;

namespace BazaarCasino
{
    // Geometry is recreated locally from the host's table snapshots. Door travel
    // is authorized by the host; no local GameObject is serialized through UNet.
    internal sealed class CasinoLounge : MonoBehaviour
    {
        internal CasinoLoungeDoor Entrance,Exit;
        internal readonly List<WorldSlotMachine> Slots=new List<WorldSlotMachine>();
        internal WorldCaseMachine Cache;
        internal WorldCrabRace Race;
        private readonly List<Material> materials=new List<Material>();
        private TMP_FontAsset font;
        internal static string Diagnostic;
        internal static CasinoLounge TrySpawn()
        {
            // Native scene anchors allow preparation before any character spawns.
            // Newt's body and the Seer controllers are not a loading prerequisite.
            var origin=new Vector3(-123.7f,-23.34f,-43.23f);
            var side=new Vector3(BazaarEntranceLayout.SideX,0,BazaarEntranceLayout.SideZ);
            var along=new Vector3(-BazaarEntranceLayout.SideZ,0,BazaarEntranceLayout.SideX);
            var rotation=Quaternion.LookRotation(-side);
            var shops=UnityEngine.Object.FindObjectsOfType<PurchaseInteraction>();
            Vector3 doorPosition=Vector3.zero;bool foundDoor=false;
            var entranceSide=new Vector3(BazaarEntranceLayout.SideX,0,BazaarEntranceLayout.SideZ);
            var entranceRotation=Quaternion.LookRotation(-entranceSide);
            // Keep the entrance at the far wall, independently of live landmarks.
            // If blocked, retry this small alcove instead of moving into the aisle.
            foreach(var candidate in BazaarEntranceLayout.Candidates())
            {
                var p=new Vector3(candidate.x,BazaarEntranceLayout.ProbeY,candidate.z);
                if(!Physics.Raycast(p+Vector3.up*8,Vector3.down,out var hit,18,LayerIndex.world.mask,QueryTriggerInteraction.Ignore)||hit.normal.y<.7f)continue;
                p=hit.point+Vector3.up*.08f;
                if(!BazaarEntranceLayout.Contains(p.x,p.z))continue;
                if(shops.Any(s=>Vector3.Distance(s.transform.position,p)<4))continue;
                if(Physics.CheckBox(p+Vector3.up*2.5f,new Vector3(1.7f,2.2f,.6f),entranceRotation,LayerIndex.world.mask,QueryTriggerInteraction.Ignore))continue;
                // Reserve a standing return point on the room-facing side.
                if(!Physics.Raycast(p+entranceSide*2.5f+Vector3.up*5,Vector3.down,out var landing,12,LayerIndex.world.mask,QueryTriggerInteraction.Ignore)||landing.normal.y<.7f)continue;
                if(Physics.CheckBox(landing.point+Vector3.up*1.48f,new Vector3(.8f,1.25f,.8f),entranceRotation,LayerIndex.world.mask,QueryTriggerInteraction.Ignore))continue;
                doorPosition=p;foundDoor=true;break;
            }
            if(!foundDoor){Diagnostic="Far-wall entrance alcove is blocked; retrying near (-147.46, -23.46), with no aisle fallback.";return null;}
            var zones=UnityEngine.Object.FindObjectsOfType<MapZone>().Where(z=>z&&z.isActiveAndEnabled).ToArray();
            foreach(float height in new[]{84f,78f,72f})foreach(float distance in new[]{32f,44f,56f})
            {
                var centre=origin+along*18+side*distance+Vector3.up*height;
                // Include roof/floor thickness. Do not punch through native geometry.
                if(Physics.CheckBox(centre+Vector3.up*(LoungeLayout.Height/2-.25f),new Vector3(LoungeLayout.Width/2+.5f,LoungeLayout.Height/2+.75f,LoungeLayout.Depth/2+.5f),rotation,LayerIndex.world.mask,QueryTriggerInteraction.Ignore))continue;
                bool safe=true;
                foreach(float x in new[]{-LoungeLayout.Width/2-.5f,0,LoungeLayout.Width/2+.5f})foreach(float z in new[]{-LoungeLayout.Depth/2-.5f,0,LoungeLayout.Depth/2+.5f})foreach(float y in new[]{-1f,LoungeLayout.Height+1})
                {
                    var probe=centre+rotation*new Vector3(x,y,z);
                    foreach(var zone in zones)
                    {
                        if(zone.IsOutOfBoundsMapZone()&&!zone.IsPointInsideMapZone(probe))safe=false;
                        if(zone.triggerType==MapZone.TriggerType.TriggerEnter&&zone.IsPointInsideMapZone(probe))safe=false;
                    }
                }
                if(!safe)continue;
                Diagnostic="Lounge at "+centre+"; fixed far-wall entrance at "+doorPosition;
                return Create(centre,rotation,doorPosition,entranceRotation);
            }
            Diagnostic="No clear lounge volume inside the Bazaar map bounds.";return null;
        }
        internal static CasinoLounge Create(Vector3 position,Quaternion rotation,Vector3 doorPosition,Quaternion doorRotation)
        {
            var root=new GameObject("BazaarCasinoLounge");root.transform.SetPositionAndRotation(position,rotation);
            try
            {
                var room=root.AddComponent<CasinoLounge>();room.Build();
                room.Entrance=CasinoLoungeDoor.Create(room,false,doorPosition,doorRotation);
                room.Entrance.transform.SetParent(root.transform,true);
                room.Exit=CasinoLoungeDoor.Create(room,true,root.transform.TransformPoint(new Vector3(0,0,LoungeLayout.DoorZ)),rotation);
                room.Exit.transform.SetParent(root.transform,true);return room;
            }
            catch{UnityEngine.Object.Destroy(root);throw;}
        }
        internal Vector3 TablePosition(TableKind kind){var p=LoungeLayout.Table(kind);return transform.TransformPoint(new Vector3(p.x,.08f,p.z));}
        internal Quaternion TableRotation=>transform.rotation*Quaternion.Euler(0,90,0);
        internal Vector3 Arrival=>transform.TransformPoint(new Vector3(0,.08f,LoungeLayout.ArrivalZ));
        internal bool Contains(Vector3 world){var p=transform.InverseTransformPoint(world);return LoungeLayout.Contains(p.x,p.y,p.z);}
        internal Vector3 ReturnPosition()
        {
            var p=Entrance.transform.position-Entrance.transform.forward*2.5f;
            return Physics.Raycast(p+Vector3.up*5,Vector3.down,out var floor,12,LayerIndex.world.mask,QueryTriggerInteraction.Ignore)?floor.point+Vector3.up*.08f:Entrance.transform.position;
        }
        private void Build()
        {
            CasinoLighting.Create(this);
            font=Resources.FindObjectsOfTypeAll<TextMeshProUGUI>().FirstOrDefault(t=>t&&t.font)?.font;
            Color stone=new Color(.095f,.15f,.21f),gold=new Color(.9f,.69f,.28f),blue=new Color(.09f,.28f,.39f);
            Cube("LoungeFloor",new Vector3(0,-.5f,0),new Vector3(LoungeLayout.Width,1,LoungeLayout.Depth),stone,true);
            Cube("Ceiling",new Vector3(0,LoungeLayout.Height+.5f,0),new Vector3(LoungeLayout.Width,1,LoungeLayout.Depth),stone,true);
            foreach(int sign in new[]{-1,1})
            {
                Cube("SideWall",new Vector3(sign*LoungeLayout.Width/2,LoungeLayout.Height/2,0),new Vector3(.5f,LoungeLayout.Height,LoungeLayout.Depth),stone,true);
                Cube("EndWall",new Vector3(0,LoungeLayout.Height/2,sign*LoungeLayout.Depth/2),new Vector3(LoungeLayout.Width,LoungeLayout.Height,.5f),stone,true);
                Cube("GoldWallBand",new Vector3(sign*(LoungeLayout.Width/2-.3f),1.5f,0),new Vector3(.12f,.12f,LoungeLayout.Depth-1),gold,false);
                Cube("GoldEndBand",new Vector3(0,1.5f,sign*(LoungeLayout.Depth/2-.3f)),new Vector3(LoungeLayout.Width-1,.12f,.12f),gold,false);
            }
            Cube("EntranceCarpet",new Vector3(0,.012f,-16),new Vector3(12,.024f,26),new Color(.06f,.32f,.30f),false);
            foreach(float x in new[]{-9f,9f})Cube("SpectatorWalkway",new Vector3(x,.014f,10),new Vector3(3,.028f,33),new Color(.05f,.25f,.24f),false);
            foreach(float x in new[]{-27f,27f})foreach(float z in new[]{-24f,-9f,6f,21f})
            {
                Cube("LunarPillar",new Vector3(x,7,z),new Vector3(.8f,14,.8f),blue,false);
                Cube("PillarCap",new Vector3(x,14,z),new Vector3(1.3f,.3f,1.3f),gold,false);
                var bulb=Cube("WallLight",new Vector3(x,7,z),new Vector3(.5f,.8f,.5f),gold,false);
                var light=bulb.AddComponent<Light>();light.type=LightType.Point;light.range=22;light.intensity=2.5f;light.color=new Color(.78f,.87f,1);light.shadows=LightShadows.None;
            }
            foreach(var point in new[]{new Vector3(-15,11,-12),new Vector3(15,11,-12),new Vector3(0,12,-20),new Vector3(-16,11,14),new Vector3(16,11,14)})
            {
                var lamp=Cube("CasinoChandelier",point,new Vector3(3.5f,.2f,3.5f),gold,false);
                Cube("ChandelierSuspension",new Vector3(point.x,13.5f,point.z),new Vector3(.12f,5,.12f),gold,false);
                var light=lamp.AddComponent<Light>();light.type=LightType.Point;light.range=25;light.intensity=3;light.color=new Color(1,.87f,.66f);light.shadows=LightShadows.None;
            }
            // TMP's readable face points towards local -Z: the room interior.
            Label("CASINO LOUNGE",new Vector3(0,10,29.65f),10,new Vector2(30,3),Quaternion.identity);
            for(int index=0;index<3;index++)
            {var machine=WorldSlotMachine.Create(this,index,new Vector3(-27.5f,0,-3+index*6));machine.transform.localRotation=Quaternion.Euler(0,-90,0);Slots.Add(machine);}
            Cache=WorldCaseMachine.Create(this);
            Cube("BrokerCounter",new Vector3(-21,.65f,-25.7f),new Vector3(2.8f,1.3f,1.1f),blue,true);
            foreach(float z in new[]{2f,10f,18f})
            {Cube("LoungeBench",new Vector3(26,.5f,z),new Vector3(3,1,4),blue,true);Cube("BenchBack",new Vector3(27,1.1f,z),new Vector3(.4f,1.7f,4),gold,true);}
            Race=WorldCrabRace.Create(this);
        }
        internal GameObject Cube(string name,Vector3 position,Vector3 size,Color color,bool solid,Transform parent=null)
        {
            var obj=GameObject.CreatePrimitive(PrimitiveType.Cube);obj.name=name;obj.transform.SetParent(parent?parent:transform,false);obj.transform.localPosition=position;obj.transform.localScale=size;
            obj.layer=solid?LayerIndex.world.intVal:LayerIndex.defaultLayer.intVal;
            if(!solid){var collider=obj.GetComponent<Collider>();collider.enabled=false;Destroy(collider);}
            var renderer=obj.GetComponent<Renderer>();var material=new Material(renderer.sharedMaterial);material.color=color;
            // Keep the new room readable even outside Bazaar's baked lighting.
            if(material.HasProperty("_EmissionColor")){material.EnableKeyword("_EMISSION");material.SetColor("_EmissionColor",color*.45f);}
            renderer.sharedMaterial=material;materials.Add(material);return obj;
        }
        internal TextMeshPro Label(string text,Vector3 position,float size,Vector2 bounds,Quaternion rotation,Transform parent=null)
        {
            var obj=new GameObject("LoungeSign");obj.transform.SetParent(parent?parent:transform,false);obj.transform.localPosition=position;obj.transform.localRotation=rotation;
            var label=obj.AddComponent<TextMeshPro>();if(font)label.font=font;label.text=text;label.color=new Color(.95f,.77f,.35f);label.fontSize=size;label.alignment=TextAlignmentOptions.Center;label.rectTransform.sizeDelta=bounds;
            return label;
        }
        private void OnDestroy(){foreach(var material in materials)if(material)Destroy(material);}
    }
    internal sealed class CasinoLoungeDoor : MonoBehaviour,IInteractable
    {
        internal CasinoLounge Room;
        internal bool IsExit;
        internal static CasinoLoungeDoor Create(CasinoLounge room,bool exit,Vector3 position,Quaternion rotation)
        {
            var root=new GameObject(exit?"CasinoLoungeExit":"CasinoLoungeEntrance");root.transform.SetPositionAndRotation(position,rotation);root.layer=LayerIndex.defaultLayer.intVal;
            root.AddComponent<EntityLocator>().entity=root;
            var collider=root.AddComponent<BoxCollider>();collider.center=new Vector3(0,2,0);collider.size=new Vector3(2.8f,4,.8f);collider.isTrigger=true;
            var door=root.AddComponent<CasinoLoungeDoor>();door.Room=room;door.IsExit=exit;
            var gold=new Color(.9f,.7f,.25f);
            foreach(int sign in new[]{-1,1})room.Cube("PortalFrame",new Vector3(sign*1.5f,2.2f,0),new Vector3(.25f,4.4f,.4f),gold,false,root.transform);
            room.Cube("PortalLintel",new Vector3(0,4.3f,0),new Vector3(3.25f,.25f,.4f),gold,false,root.transform);
            var pane=room.Cube("LunarDoor",new Vector3(0,2.1f,0),new Vector3(2.75f,4,.12f),new Color(.07f,.48f,.64f),false,root.transform);
            var material=pane.GetComponent<Renderer>().sharedMaterial;
            if(material.HasProperty("_EmissionColor")){material.EnableKeyword("_EMISSION");material.SetColor("_EmissionColor",new Color(.03f,.35f,.6f)*1.6f);}
            var light=pane.AddComponent<Light>();light.type=LightType.Point;light.color=new Color(.2f,.65f,1);light.range=8;light.intensity=1.8f;light.shadows=LightShadows.None;
            var portalLabel=room.Label(exit?"RETURN TO BAZAAR\nE · Exit lounge":"CASINO LOUNGE\nE · Enter",new Vector3(0,5.05f,0),3.2f,new Vector2(7,1.5f),Quaternion.identity,root.transform);
            portalLabel.gameObject.AddComponent<CasinoBillboard>();
            return door;
        }
        public string GetContextString(Interactor actor)=>IsExit?"Return to Bazaar":"Enter Casino Lounge";
        public Interactability GetInteractability(Interactor actor)=>CasinoPlugin.InBazaar&&Room?Interactability.Available:Interactability.Disabled;
        public void OnInteractionBegin(Interactor actor)=>CasinoPlugin.Instance?.UseLoungeDoor(actor,this);
        public bool ShouldIgnoreSpherecastForInteractibility(Interactor actor)=>false;
        public bool ShouldShowOnScanner()=>true;
        public bool ShouldProximityHighlight()=>true;
    }
    internal sealed class CasinoBillboard : MonoBehaviour
    {
        internal static Camera ViewCamera=>CameraRigController.readOnlyInstancesList.FirstOrDefault(r=>r&&r.localUserViewer!=null&&r.sceneCam)?.sceneCam??Camera.main;
        private void LateUpdate(){var camera=ViewCamera;if(camera)transform.rotation=camera.transform.rotation;}
    }
}
