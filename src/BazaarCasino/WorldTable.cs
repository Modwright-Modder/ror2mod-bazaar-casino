using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using RoR2;
using TMPro;
using UnityEngine;

namespace BazaarCasino
{
    internal sealed class WorldTable : MonoBehaviour,IInteractable
    {
        internal TableKind Kind;
        private readonly CasinoArt art=new CasinoArt();
        private readonly List<Material> materials=new List<Material>();
        private readonly Dictionary<string,SpriteRenderer> cards=new Dictionary<string,SpriteRenderer>();
        private readonly Dictionary<string,int> cardValues=new Dictionary<string,int>();
        private readonly Dictionary<int,TextMeshPro> names=new Dictionary<int,TextMeshPro>();
        private Transform rotor,ball,chipRoot;
        private Transform hoverRoot;
        private Material betHoverMaterial;
        private int hoveredChoice=-1,hoveredNumber=-1;
        private uint previewOwner;
        private RouletteBet[] previewBets;
        private TextMeshPro title;
        private TMP_FontAsset tableFont;
        private TableSnapshot state;
        private RouletteSpin replay;
        private int replayRound=-1;
        private int celebratedRound=-1;
        private float receivedAt,replayElapsed;
        private static readonly Color Gold=new Color(.92f,.73f,.30f);
        private static readonly Color Felt=new Color(.035f,.24f,.16f);
        private static readonly Color[] SeatColors={new Color(1,.73f,.23f),new Color(.29f,.76f,1),new Color(.95f,.42f,.65f),new Color(.5f,1,.55f),new Color(.8f,.55f,1),new Color(1,.55f,.25f),new Color(.5f,1,1),new Color(1,.95f,.5f)};
        internal static string PlacementDiagnostic="Waiting for Bazaar scene setup.";
        internal static bool TryLocation(TableKind kind,Vector3[] occupied,out Vector3 position,out Quaternion rotation)
        {
            position=Vector3.zero;rotation=Quaternion.identity;
            var newt=UnityEngine.Object.FindObjectsOfType<CharacterBody>().FirstOrDefault(b=>b&&b.name.StartsWith("ShopkeeperBody"));
            if(!newt){PlacementDiagnostic="Shopkeeper body has not spawned.";return false;}
            var shops=UnityEngine.Object.FindObjectsOfType<PurchaseInteraction>();
            var dealers=UnityEngine.Object.FindObjectsOfType<DealerNPC>();
            var seers=UnityEngine.Object.FindObjectsOfType<SeerStationController>();
            if(seers.Length==0){PlacementDiagnostic="No active Seer pedestals yet.";return false;} // Wait for Bazaar setup, rather than spawning behind Newt.
            var centre=seers.Aggregate(Vector3.zero,(sum,seer)=>sum+seer.transform.position)/seers.Length;
            Vector3 facing=newt.characterDirection?newt.characterDirection.forward:((Component)newt).transform.forward;
            var newtPoint=new SeatPoint(newt.footPosition.x,newt.footPosition.z);
            var seerPoint=new SeatPoint(centre.x,centre.z);
            var facingPoint=new SeatPoint(facing.x,facing.z);
            var roomSide=PlacementGeometry.RoomSide(newtPoint,seerPoint,facingPoint);
            var front=new Vector3(roomSide.x,0,roomSide.z);
            var yaw=Quaternion.LookRotation(-front);
            float width=kind==TableKind.Blackjack?7.8f:9.6f,depth=7.8f;
            var candidates=PlacementGeometry.Candidates(kind,newtPoint,seerPoint,facingPoint).ToArray();
            var rejected=new Dictionary<string,int>();
            void Reject(string reason){rejected[reason]=rejected.TryGetValue(reason,out var n)?n+1:1;}
            foreach(var point in candidates)
            {
                var candidate=new Vector3(point.x,centre.y,point.z);
                if(!Physics.Raycast(candidate+Vector3.up*8,Vector3.down,out var floor,18,LayerIndex.world.mask,QueryTriggerInteraction.Ignore)||floor.normal.y<.75f){Reject("floor ray/slope");continue;}
                if(Vector3.Distance(floor.point,newt.footPosition)<7){Reject("Newt/stairs clearance");continue;}
                if(occupied.Any(p=>Vector3.Distance(p,floor.point)<10)){Reject("other table clearance");continue;}
                var inverse=Quaternion.Inverse(yaw);
                bool BlocksAccess(Vector3 p)
                {
                    var local=inverse*(p-floor.point);
                    // Include the front steps and access space around native shops.
                    return Mathf.Abs(local.y)<4&&Mathf.Abs(local.x)<width/2+1&&local.z>-depth/2-2.1f&&local.z<depth/2+1;
                }
                if(shops.Any(p=>p&&BlocksAccess(p.transform.position))||dealers.Any(p=>p&&BlocksAccess(p.transform.position))||seers.Any(p=>BlocksAccess(p.transform.position))){Reject("shop/pedestal/dealer clearance");continue;}
                var offsets=new List<Vector3>{Vector3.zero};
                foreach(float x in new[]{-width/2,0,width/2})foreach(float z in new[]{-depth/2,0,depth/2})offsets.Add(new Vector3(x,0,z));
                // Sample actual chair locations as well as the platform's perimeter.
                for(int slot=0;slot<8;slot++){var chair=SeatLayout.Chair(kind,slot);offsets.Add(new Vector3(chair.x,0,chair.z));}
                var heights=new List<float>();bool supported=true;
                foreach(var offset in offsets)
                {
                    if(!Physics.Raycast(floor.point+yaw*offset+Vector3.up*5,Vector3.down,out var edge,9,LayerIndex.world.mask,QueryTriggerInteraction.Ignore)||edge.normal.y<.70f){supported=false;break;}
                    heights.Add(edge.point.y);
                }
                if(!supported||!PlacementGeometry.TryPlatformHeight(heights.ToArray(),out float level)){Reject("platform support/cliff");continue;}
                var platform=new Vector3(floor.point.x,level,floor.point.z);
                if(Physics.CheckBox(platform+Vector3.up*1.3f,new Vector3(width/2,1.0f,depth/2),yaw,LayerIndex.world.mask,QueryTriggerInteraction.Ignore)){Reject("world obstacle");continue;}
                PlacementDiagnostic=kind+"; supported main-room platform at "+platform+"; sampled height spread="+(heights.Max()-heights.Min()).ToString("F2");
                position=platform;rotation=yaw;return true;
            }
            PlacementDiagnostic=kind+"; Newt="+newt.footPosition+"; Seers="+centre+"; roomSide="+front+"; candidates="+candidates.Length+"; rejected: "+string.Join(", ",rejected.Select(p=>p.Key+"="+p.Value));
            return false;
        }
        internal static WorldTable Create(TableKind kind,Vector3 position,Quaternion rotation)
        {
            var root=new GameObject("SharedCasino"+kind);root.transform.SetPositionAndRotation(position,rotation);root.layer=LayerIndex.defaultLayer.intVal;
            root.AddComponent<EntityLocator>().entity=root;
            try{var table=root.AddComponent<WorldTable>();table.Kind=kind;table.Build();return table;}
            catch{UnityEngine.Object.Destroy(root);throw;}
        }
        private void Build()
        {
            tableFont=Resources.FindObjectsOfTypeAll<TextMeshProUGUI>().FirstOrDefault(t=>t&&t.font)?.font;
            bool bj=Kind==TableKind.Blackjack;float width=bj?5.6f:8,depth=bj?3.4f:4;
            BuildPlatform(bj?7.8f:9.6f,7.8f);
            var collider=gameObject.AddComponent<BoxCollider>();collider.center=new Vector3(0,1.3f,0);collider.size=new Vector3(width,.45f,depth);collider.isTrigger=true;
            var solid=gameObject.AddComponent<BoxCollider>();solid.center=new Vector3(0,1.18f,0);solid.size=new Vector3(width,.30f,depth);
            var top=Part("WalnutTable",bj?PrimitiveType.Cylinder:PrimitiveType.Cube,new Vector3(0,1.22f,0),bj?new Vector3(width,.14f,depth):new Vector3(width,.30f,depth),new Color(.19f,.10f,.07f));
            Part("BrassRim",bj?PrimitiveType.Cylinder:PrimitiveType.Cube,new Vector3(0,1.38f,0),bj?new Vector3(width-.08f,.025f,depth-.08f):new Vector3(width-.08f,.05f,depth-.08f),Gold);
            Part("GreenFelt",bj?PrimitiveType.Cylinder:PrimitiveType.Cube,new Vector3(0,1.42f,0),bj?new Vector3(width-.24f,.025f,depth-.24f):new Vector3(width-.24f,.05f,depth-.24f),Felt);
            foreach(float x in new[]{-.35f,.35f})foreach(float z in new[]{-.32f,.32f})Part("TableLeg",PrimitiveType.Cylinder,new Vector3(x*width,.58f,z*depth),new Vector3(.22f,.58f,.22f),new Color(.11f,.09f,.08f));
            var highlight=gameObject.AddComponent<Highlight>();highlight.targetRenderer=top.GetComponent<Renderer>();highlight.highlightColor=Highlight.HighlightColor.interactive;
            title=Text("TableTitle",bj?"BLACKJACK • SHARED TABLE":"ROULETTE • SHARED TABLE",new Vector3(0,2.8f,0),3.2f,Gold,new Vector2(9,1.4f));
            chipRoot=new GameObject("TableChips").transform;chipRoot.SetParent(transform,false);
            BuildLighting(width,depth);
            for(int i=0;i<8;i++)
            {
                var chair=ChairLocal(i);
                Part("Seat"+i,PrimitiveType.Cylinder,chair+Vector3.up*.65f,new Vector3(.72f,.09f,.72f),SeatColors[i]*.7f);
                Part("SeatPedestal",PrimitiveType.Cylinder,chair+Vector3.up*.30f,new Vector3(.18f,.30f,.18f),new Color(.16f,.15f,.14f));
                var back=Part("SeatBack",PrimitiveType.Cube,chair+Vector3.up*1.15f+chair.normalized*.31f,new Vector3(.65f,.65f,.10f),SeatColors[i]*.55f);
                back.transform.localRotation=Quaternion.LookRotation(-chair.normalized);
            }
            if(bj)
            {
                for(int i=0;i<8;i++)
                {
                    var point=SeatPosition(i);
                    names[i]=Text("SeatName","",new Vector3(point.x,1.95f,point.z),1.8f,SeatColors[i],new Vector2(2.5f,.7f));
                }
                var deck=Sprite("Deck","back",new Vector3(0,1.51f,.5f),.43f,.61f);deck.transform.localEulerAngles=new Vector3(90,0,-8);
                var marking=Text("Rules","BLACKJACK PAYS 3:2\nDEALER STANDS ON 17",new Vector3(0,1.465f,-.22f),1.5f,Gold,new Vector2(2,.55f));marking.transform.localEulerAngles=new Vector3(90,0,0);
            }
            else BuildRoulette();
        }
        private void BuildPlatform(float width,float depth)
        {
            Part("LevelCasinoDeck",PrimitiveType.Cube,new Vector3(0,-.10f,0),new Vector3(width,.20f,depth),new Color(.12f,.16f,.19f));
            var deck=gameObject.AddComponent<BoxCollider>();deck.center=new Vector3(0,-.10f,0);deck.size=new Vector3(width,.20f,depth);
            for(int step=0;step<4;step++)
            {
                var centre=new Vector3(0,-.10f-.25f*step,-depth/2-.25f-.45f*step);
                var size=new Vector3(2.1f,.20f,.65f);
                Part("AccessStep",PrimitiveType.Cube,centre,size,new Color(.20f,.24f,.27f));
                var collider=gameObject.AddComponent<BoxCollider>();collider.center=centre;collider.size=size;
            }
            foreach(float x in new[]{-.42f,.42f})foreach(float z in new[]{-.42f,.42f})
            {
                var point=new Vector3(x*width,0,z*depth);
                if(Physics.Raycast(transform.TransformPoint(point)+Vector3.up*.1f,Vector3.down,out var floor,3,LayerIndex.world.mask,QueryTriggerInteraction.Ignore))
                {
                    float height=Mathf.Max(.05f,transform.position.y-floor.point.y);
                    Part("DeckSupport",PrimitiveType.Cylinder,point-Vector3.up*(height*.5f),new Vector3(.20f,height*.5f,.20f),Gold);
                }
            }
        }
        internal Vector3 ChairLocal(int slot){var p=SeatLayout.Chair(Kind,slot);return new Vector3(p.x,0,p.z);}
        internal Vector3 ChairFoot(int slot)=>transform.TransformPoint(ChairLocal(slot));
        internal Quaternion ChairFacing(int slot)=>transform.rotation*Quaternion.LookRotation(-ChairLocal(slot).normalized);
        internal void TableCamera(bool top,out Vector3 position,out Vector3 target)
        {
            // Both views remain above the felt and inside the lounge ceiling.
            // No chair-dependent side offset: pillars and avatars cannot sit in front.
            position=transform.TransformPoint(top?new Vector3(0,9.2f,0):new Vector3(0,5.7f,-5.0f));
            target=transform.TransformPoint(new Vector3(0,1.5f,0));
        }
        internal bool TryBetRay(Ray ray,out int choice,out int number)
        {
            choice=number=-1;if(Kind!=TableKind.Roulette)return false;
            var plane=new Plane(transform.up,transform.TransformPoint(new Vector3(0,1.50f,0)));
            if(!plane.Raycast(ray,out float distance)||distance<0)return false;
            var point=transform.InverseTransformPoint(ray.GetPoint(distance));
            return RouletteLayout.Hit(point.x,point.z,out choice,out number);
        }
        private static Vector3 BlackjackBetPosition(int slot)=>SeatPosition(slot)+new Vector3(0,.035f,-.28f);
        internal bool TryBlackjackBetRay(Ray ray,int slot)
        {
            if(Kind!=TableKind.Blackjack||slot<0||slot>=8)return false;
            var plane=new Plane(transform.up,transform.TransformPoint(new Vector3(0,1.54f,0)));
            if(!plane.Raycast(ray,out float distance)||distance<0)return false;
            var point=transform.InverseTransformPoint(ray.GetPoint(distance));var spot=BlackjackBetPosition(slot);
            return Math.Abs(point.x-spot.x)<.45f&&Math.Abs(point.z-spot.z)<.28f;
        }
        internal void PreviewChips(uint owner,RouletteBet[] bets)
        {previewOwner=owner;previewBets=bets;if(state!=null)DrawChips(state);}
        internal void HoverSpot(int choice=-1,int number=-1)
        {
            if(choice==hoveredChoice&&number==hoveredNumber)return;
            hoveredChoice=choice;hoveredNumber=number;
            if(hoverRoot){hoverRoot.gameObject.SetActive(false);Destroy(hoverRoot.gameObject);hoverRoot=null;}
            if(choice<0)return;
            bool blackjack=Kind==TableKind.Blackjack&&choice>=100&&choice<108;
            if(!blackjack&&Kind!=TableKind.Roulette)return;
            var p=blackjack?BlackjackBetPosition(choice-100):BetPosition(choice,number);var size=blackjack?new Vector3(.9f,0,.56f):new Vector3(RouletteLayout.Size(choice,number).x,0,RouletteLayout.Size(choice,number).z);
            if(!betHoverMaterial)
            {
                betHoverMaterial=new Material(materials[0]);betHoverMaterial.color=Color.white;materials.Add(betHoverMaterial);
                if(betHoverMaterial.HasProperty("_EmissionColor")){betHoverMaterial.EnableKeyword("_EMISSION");betHoverMaterial.SetColor("_EmissionColor",Color.white*.7f);}
            }
            hoverRoot=new GameObject("BetHoverOutline").transform;hoverRoot.SetParent(transform,false);hoverRoot.localPosition=p+Vector3.up*.018f;
            // Reuse one material; moving the pointer must not leak new materials.
            void Edge(Vector3 offset,Vector3 scale)
            {
                var obj=GameObject.CreatePrimitive(PrimitiveType.Cube);obj.transform.SetParent(hoverRoot,false);obj.transform.localPosition=offset;obj.transform.localScale=scale;
                var collider=obj.GetComponent<Collider>();collider.enabled=false;Destroy(collider);
                obj.GetComponent<Renderer>().sharedMaterial=betHoverMaterial;
            }
            foreach(float sign in new[]{-1f,1f})
            {
                Edge(new Vector3(sign*size.x*.5f,0,0),new Vector3(.012f,.008f,size.z));
                Edge(new Vector3(0,0,sign*size.z*.5f),new Vector3(size.x,.008f,.012f));
            }
        }
        private void BuildLighting(float width,float depth)
        {
            for(int i=0;i<2;i++)
            {
                var p=new Vector3((i==0?-1:1)*width*.43f,3.8f,depth*.38f);
                Part("LampPost",PrimitiveType.Cylinder,new Vector3(p.x,1.9f,p.z),new Vector3(.10f,1.9f,.10f),Gold);
                Part("LampShade",PrimitiveType.Cylinder,p,new Vector3(.8f,.10f,.8f),Gold);
                var bulb=Part("LampGlow",PrimitiveType.Sphere,p-Vector3.up*.18f,Vector3.one*.25f,new Color(1,.85f,.57f));
                var material=bulb.GetComponent<Renderer>().sharedMaterial;
                if(material.HasProperty("_EmissionColor")){material.EnableKeyword("_EMISSION");material.SetColor("_EmissionColor",new Color(1,.78f,.42f)*2);}
                var lamp=new GameObject("CasinoWarmLight");lamp.transform.SetParent(transform,false);lamp.transform.localPosition=p-Vector3.up*.35f;
                var light=lamp.AddComponent<Light>();light.type=LightType.Point;light.color=new Color(1,.88f,.68f);light.range=13;light.intensity=2.4f;light.shadows=LightShadows.None;light.renderMode=LightRenderMode.ForcePixel;
            }
            var fill=new GameObject("CasinoCoolFill");fill.transform.SetParent(transform,false);fill.transform.localPosition=Vector3.up*3;
            var fillLight=fill.AddComponent<Light>();fillLight.type=LightType.Point;fillLight.color=new Color(.40f,.74f,1);fillLight.range=10;fillLight.intensity=.8f;fillLight.shadows=LightShadows.None;
        }
        private GameObject Part(string name,PrimitiveType primitive,Vector3 position,Vector3 size,Color color,Transform parent=null)
        {
            var obj=GameObject.CreatePrimitive(primitive);obj.name=name;obj.transform.SetParent(parent?parent:transform,false);obj.transform.localPosition=position;obj.transform.localScale=size;
            var collider=obj.GetComponent<Collider>();if(collider){collider.enabled=false;Destroy(collider);}
            var renderer=obj.GetComponent<Renderer>();var material=new Material(renderer.sharedMaterial);material.color=color;renderer.sharedMaterial=material;materials.Add(material);return obj;
        }
        private TextMeshPro Text(string name,string text,Vector3 position,float size,Color color,Vector2 bounds,Transform parent=null)
        {
            var obj=new GameObject(name);obj.transform.SetParent(parent?parent:transform,false);obj.transform.localPosition=position;
            var label=obj.AddComponent<TextMeshPro>();if(tableFont)label.font=tableFont;
            label.text=text;label.fontSize=size;label.color=color;label.alignment=TextAlignmentOptions.Center;label.rectTransform.sizeDelta=bounds;return label;
        }
        private SpriteRenderer Sprite(string name,string asset,Vector3 position,float width,float height,Transform parent=null)
        {
            var obj=new GameObject(name);obj.transform.SetParent(parent?parent:transform,false);obj.transform.localPosition=position;obj.transform.localEulerAngles=new Vector3(90,0,0);
            var renderer=obj.AddComponent<SpriteRenderer>();renderer.sprite=art.Get(asset);
            obj.transform.localScale=new Vector3(width/renderer.sprite.bounds.size.x,height/renderer.sprite.bounds.size.y,1);return renderer;
        }
        private void BuildRoulette()
        {
            Vector3 centre=new Vector3(-2.4f,1.50f,0);
            Part("WheelBowl",PrimitiveType.Cylinder,centre,new Vector3(2.85f,.12f,2.85f),Gold);
            Part("OuterTrack",PrimitiveType.Cylinder,centre+Vector3.up*.13f,new Vector3(2.68f,.025f,2.68f),new Color(.055f,.08f,.12f));
            rotor=new GameObject("RouletteRotor").transform;rotor.SetParent(transform,false);rotor.localPosition=centre+Vector3.up*.17f;
            for(int i=0;i<37;i++)
            {
                float a=i*Mathf.PI*2/37;int number=Games.Wheel[i];
                var obj=new GameObject("Pocket"+number,typeof(MeshFilter),typeof(MeshRenderer));obj.transform.SetParent(rotor,false);
                var mesh=new Mesh();float lo=a-Mathf.PI/37,hi=a+Mathf.PI/37;
                mesh.vertices=new[]{new Vector3(Mathf.Sin(lo)*.69f,0,Mathf.Cos(lo)*.69f),new Vector3(Mathf.Sin(hi)*.69f,0,Mathf.Cos(hi)*.69f),new Vector3(Mathf.Sin(lo)*1.2f,0,Mathf.Cos(lo)*1.2f),new Vector3(Mathf.Sin(hi)*1.2f,0,Mathf.Cos(hi)*1.2f)};
                mesh.triangles=new[]{0,2,1,1,2,3};mesh.RecalculateNormals();obj.GetComponent<MeshFilter>().mesh=mesh;meshes.Add(mesh);
                var material=new Material(materials[0]);material.color=number==0?new Color(.04f,.42f,.23f):Games.IsRed(number)?new Color(.72f,.06f,.12f):new Color(.025f,.03f,.05f);
                obj.GetComponent<MeshRenderer>().sharedMaterial=material;materials.Add(material);
                var label=Text("Number",number.ToString(),new Vector3(Mathf.Sin(a)*1.06f,.012f,Mathf.Cos(a)*1.06f),1.2f,Color.white,new Vector2(.22f,.22f),rotor);
                label.transform.localEulerAngles=new Vector3(90,a*Mathf.Rad2Deg,0);
                var divider=Part("PocketDivider",PrimitiveType.Cube,new Vector3(Mathf.Sin(lo)*.86f,.02f,Mathf.Cos(lo)*.86f),new Vector3(.015f,.045f,.33f),Gold,rotor);divider.transform.localEulerAngles=new Vector3(0,lo*Mathf.Rad2Deg,0);
            }
            Part("RotorHub",PrimitiveType.Cylinder,new Vector3(0,.04f,0),new Vector3(1.35f,.06f,1.35f),new Color(.12f,.07f,.04f),rotor);
            Part("Spindle",PrimitiveType.Cylinder,new Vector3(0,.16f,0),new Vector3(.13f,.16f,.13f),Gold,rotor);
            for(int i=0;i<8;i++)
            {
                float a=i*Mathf.PI/4;Part("TrackDeflector",PrimitiveType.Sphere,centre+new Vector3(Mathf.Sin(a)*1.24f,.16f,Mathf.Cos(a)*1.24f),new Vector3(.065f,.05f,.065f),Gold);
            }
            ball=Part("RouletteBall",PrimitiveType.Sphere,centre+new Vector3(0,.24f,1.2f),Vector3.one*.08f,Color.white).transform;ball.gameObject.SetActive(false);
            for(int col=0;col<12;col++)for(int row=0;row<3;row++)
            {
                int number=col*3+3-row;Vector3 spot=BetPosition(6,number);
                Part("NumberSpot"+number,PrimitiveType.Cube,spot,new Vector3(.31f,.014f,.53f),Games.IsRed(number)?new Color(.65f,.06f,.10f):new Color(.025f,.04f,.05f));
                var label=Text("BetNumber",number.ToString(),spot+Vector3.up*.012f,1.2f,Color.white,new Vector2(.31f,.48f));label.transform.localEulerAngles=new Vector3(90,0,0);
            }
            Part("ZeroSpot",PrimitiveType.Cube,BetPosition(6,0),new Vector3(.30f,.014f,1.66f),Felt*1.4f);
            var zero=Text("Zero","0",BetPosition(6,0)+Vector3.up*.012f,2.8f,Color.white,new Vector2(.3f,1.5f));zero.transform.localEulerAngles=new Vector3(90,0,0);
            for(int choice=0;choice<=12;choice++)if(choice!=6)
            {
                string[] labels={"RED","BLACK","EVEN","ODD","1–18","19–36","","1st 12","2nd 12","3rd 12","1st col","2nd col","3rd col"};
                var p=BetPosition(choice,0);Part("OutsideSpot",PrimitiveType.Cube,p,new Vector3(choice<6?.61f:1.21f,.014f,.24f),Felt*1.4f);
                var text=Text("OutsideBet",labels[choice],p+Vector3.up*.012f,1.05f,Color.white,new Vector2(choice<6?.62f:1.22f,.24f));text.transform.localEulerAngles=new Vector3(90,0,0);
            }
        }
        private readonly List<Mesh> meshes=new List<Mesh>();
        private static Vector3 SeatPosition(int slot)
        {var p=SeatLayout.Chair(TableKind.Blackjack,slot);return new Vector3(p.x*.63f,1.51f,p.z*.45f);}
        private static Vector3 BetPosition(int choice,int number)
        {var p=RouletteLayout.Centre(choice,number);return new Vector3(p.x,1.48f,p.z);}
        internal void Receive(TableSnapshot snapshot)
        {
            bool changed=state==null||state.revision!=snapshot.revision||state.round!=snapshot.round;
            bool sameSpin=state!=null&&state.round==snapshot.round&&state.phase==snapshot.phase;
            replayElapsed=sameSpin?Mathf.Max(snapshot.elapsed,replayElapsed+Time.unscaledTime-receivedAt):snapshot.elapsed;
            receivedAt=Time.unscaledTime;
            if(state!=null&&state.round!=snapshot.round)ClearCards();
            state=snapshot;
            if(Kind==TableKind.Roulette&&snapshot.phase!=(int)TablePhase.Betting&&replayRound!=snapshot.round)
            {replay=new RouletteSpin(snapshot.spinSeed);replayRound=snapshot.round;}
            if(!changed)return;
            if(Kind==TableKind.Blackjack)
            {
                for(int i=0;i<snapshot.dealer.Length;i++)SetCard("Dealer"+i,snapshot.dealer[i],new Vector3((i-(snapshot.dealer.Length-1)*.5f)*.35f,1.52f,.93f),0);
                for(int i=0;i<8;i++)names[i].text="";
                foreach(var seat in snapshot.seats)
                {
                    if(seat.slot<0||seat.slot>=8)continue;
                    names[seat.slot].text=seat.name+"\n"+(snapshot.phase==(int)TablePhase.Results?seat.result+" · "+seat.payout:seat.wager>0?seat.total+" · "+seat.wager+" gold":"Waiting");
                    if(snapshot.turn==seat.id)names[seat.slot].text="<color=#FFFFFF>▶ "+names[seat.slot].text+"</color>";
                    Vector3 anchor=SeatPosition(seat.slot);float rotation=Mathf.Atan2(anchor.x,-anchor.z)*Mathf.Rad2Deg;
                    bool split=seat.secondCards?.Length>0;
                    for(int i=0;i<seat.cards.Length;i++)SetCard("Player"+seat.slot+"Card"+i,seat.cards[i],anchor+new Vector3((i-(seat.cards.Length-1)*.5f)*.22f,0,split?-.17f:0),rotation);
                    if(split)for(int i=0;i<seat.secondCards.Length;i++)SetCard("Player"+seat.slot+"Second"+i,seat.secondCards[i],anchor+new Vector3((i-(seat.secondCards.Length-1)*.5f)*.22f,.007f,.25f),rotation);
                }
                if(snapshot.phase==(int)TablePhase.Betting)ClearCards();
            }
            DrawChips(snapshot);
            if(snapshot.phase==(int)TablePhase.Results&&snapshot.round!=celebratedRound)
            {
                celebratedRound=snapshot.round;
                foreach(var seat in snapshot.seats.Where(s=>s.payout>s.wager))StartCoroutine(WinCoins(seat));
            }
        }
        private IEnumerator WinCoins(TableSeatState seat)
        {
            var effect=new GameObject("SharedTableWinCoins");effect.transform.SetParent(transform,false);
            Vector3 origin=Kind==TableKind.Blackjack?SeatPosition(seat.slot):new Vector3(1.3f,1.53f,0);
            var coins=new List<Transform>();
            for(int i=0;i<12;i++){var coin=Sprite("WinCoin","chip",origin,.13f,.13f,effect.transform);coin.color=SeatColors[seat.slot%8];coins.Add(coin.transform);}
            for(float t=0;t<1.6f;t+=Time.unscaledDeltaTime)
            {
                for(int i=0;i<coins.Count;i++)
                {
                    float a=i*Mathf.PI/6;
                    coins[i].localPosition=origin+new Vector3(Mathf.Sin(a)*t*.45f,Mathf.Max(0,t*1.8f-t*t),Mathf.Cos(a)*t*.35f);
                    coins[i].localEulerAngles=new Vector3(90+t*240,0,i*30+t*100);
                }
                yield return null;
            }
            Destroy(effect);
        }
        private void SetCard(string key,int value,Vector3 position,float rotation)
        {
            if(!cards.TryGetValue(key,out var card))
            {
                card=Sprite(key,value<0?"back":"card"+value,new Vector3(0,1.65f,.5f),.29f,.41f);cards[key]=card;cardValues[key]=value;
                StartCoroutine(FlyCard(card.transform,position,rotation));
            }
            else
            {
                card.transform.localPosition=position;
                if(cardValues[key]!=value){cardValues[key]=value;StartCoroutine(FlipCard(card,value));}
            }
        }
        private IEnumerator FlyCard(Transform card,Vector3 target,float yaw)
        {
            Vector3 start=card.localPosition;
            for(float t=0;t<.4f;t+=Time.unscaledDeltaTime){if(!card)yield break;float p=1-Mathf.Pow(1-t/.4f,3);card.localPosition=Vector3.Lerp(start,target,p)+Vector3.up*Mathf.Sin(p*Mathf.PI)*.3f;card.localEulerAngles=new Vector3(90,yaw,-15*(1-p));yield return null;}
            if(card){card.localPosition=target;card.localEulerAngles=new Vector3(90,yaw,0);}
        }
        private IEnumerator FlipCard(SpriteRenderer card,int value)
        {
            Vector3 original=card.transform.localScale;
            for(float t=0;t<.4f;t+=Time.unscaledDeltaTime){if(!card)yield break;float p=t/.4f;card.transform.localScale=new Vector3(original.x*Mathf.Abs(1-2*p),original.y,1);if(p>=.5f)card.sprite=art.Get(value<0?"back":"card"+value);yield return null;}
            if(card)card.transform.localScale=original;
        }
        private void ClearCards(){foreach(var card in cards.Values)if(card)Destroy(card.gameObject);cards.Clear();cardValues.Clear();}
        private void DrawChips(TableSnapshot snapshot)
        {
            foreach(Transform child in chipRoot){child.gameObject.SetActive(false);Destroy(child.gameObject);}
            foreach(var seat in snapshot.seats.Where(s=>Kind==TableKind.Blackjack&&s.seated||s.wager>0||previewBets!=null&&s.id==previewOwner))
            {
                if(Kind==TableKind.Blackjack)
                {
                    var p=BlackjackBetPosition(seat.slot);var chip=Sprite("PlayerChips","chip",p,.27f,.27f,chipRoot);chip.color=seat.wager>0?SeatColors[seat.slot%8]:new Color(.6f,.7f,.75f,.3f);
                    var amount=Text("BlackjackWager",seat.wager.ToString("N0"),p+Vector3.up*.025f,.95f,Color.black,new Vector2(.24f,.20f),chipRoot);
                    amount.enableAutoSizing=true;amount.fontSizeMin=.15f;amount.fontSizeMax=.95f;amount.enableWordWrapping=false;amount.overflowMode=TextOverflowModes.Truncate;
                    amount.transform.localEulerAngles=new Vector3(90,Mathf.Atan2(p.x,-p.z)*Mathf.Rad2Deg,0);
                }
                else foreach(var bet in (previewBets!=null&&seat.id==previewOwner&&snapshot.phase==(int)TablePhase.Betting?previewBets:seat.bets)??Array.Empty<RouletteBet>())
                {
                    var p=BetPosition(bet.choice,bet.number)+new Vector3((seat.slot%3-1)*.058f,.035f+(seat.slot/3)*.02f,(seat.slot%2)*.055f);
                    var chip=Sprite("PlayerChips","chip",p,.17f,.17f,chipRoot);chip.color=SeatColors[seat.slot%8];
                    var amount=Text("ChipAmount",bet.amount.ToString(),p+Vector3.up*.015f,.7f,Color.black,new Vector2(.24f,.20f),chipRoot);amount.transform.localEulerAngles=new Vector3(90,0,0);
                    amount.enableAutoSizing=true;amount.fontSizeMin=.15f;amount.fontSizeMax=.7f;amount.enableWordWrapping=false;amount.overflowMode=TextOverflowModes.Truncate;
                }
            }
        }
        private void Update()
        {
            if(state==null)return;
            float left=Mathf.Max(0,state.remaining-(Time.unscaledTime-receivedAt));
            string phase=state.phase==(int)TablePhase.Betting?(left>0?"Bets close in "+Mathf.CeilToInt(left)+"s":"E to join • Gold wagers"):
                state.phase==(int)TablePhase.Playing?(Kind==TableKind.Roulette?"Ball spinning…":"Turn: "+state.seats.FirstOrDefault(s=>s.id==state.turn)?.name+" · "+Mathf.CeilToInt(left)+"s"):
                Kind==TableKind.Roulette?"RESULT: "+state.number+" • Next round soon":"Round complete • Next round soon";
            title.text=(Kind==TableKind.Blackjack?"BLACKJACK":"ROULETTE")+"\n"+phase;
            if(Kind!=TableKind.Roulette||replay==null||state.phase==(int)TablePhase.Betting){if(ball)ball.gameObject.SetActive(false);return;}
            ball.gameObject.SetActive(true);
            float elapsed=state.phase==(int)TablePhase.Results?replay.duration:replayElapsed+Time.unscaledTime-receivedAt;
            int index=Mathf.Clamp(Mathf.FloorToInt(elapsed*30),0,replay.frames.Count-1);
            var frame=replay.frames[index];
            if(index+1<replay.frames.Count)
            {
                var next=replay.frames[index+1];float fraction=Mathf.Clamp01((elapsed-frame.time)/Mathf.Max(.001f,next.time-frame.time));
                frame=new BallFrame(elapsed,Mathf.Lerp(frame.angle,next.angle,fraction),Mathf.Lerp(frame.radius,next.radius,fraction),Mathf.Lerp(frame.wheel,next.wheel,fraction));
            }
            rotor.localEulerAngles=new Vector3(0,frame.wheel*Mathf.Rad2Deg,0);
            ball.localPosition=new Vector3(-2.4f+Mathf.Sin(frame.angle)*frame.radius*1.35f,1.72f+Mathf.Max(0,frame.radius-.8f)*.4f,Mathf.Cos(frame.angle)*frame.radius*1.35f);
        }
        private void LateUpdate()
        {
            var body=CasinoPlugin.LocalMaster?CasinoPlugin.LocalMaster.GetBody():null;
            var seat=body?body.GetComponent<CasinoSeat>():null;
            bool hide=seat&&seat.IsSeated&&seat.Kind==Kind;
            title.gameObject.SetActive(!hide);foreach(var label in names.Values)label.gameObject.SetActive(!hide);
            var camera=CasinoBillboard.ViewCamera;if(!camera)return;title.transform.rotation=camera.transform.rotation;
            foreach(var label in names.Values)label.transform.rotation=camera.transform.rotation;
        }
        public string GetContextString(Interactor actor)=>"Join "+Kind+" table — play together";
        public Interactability GetInteractability(Interactor actor)=>CasinoPlugin.InBazaar?Interactability.Available:Interactability.Disabled;
        public void OnInteractionBegin(Interactor actor)=>CasinoPlugin.Instance?.OpenTable(actor,this);
        public bool ShouldIgnoreSpherecastForInteractibility(Interactor actor)=>false;
        public bool ShouldShowOnScanner()=>true;
        public bool ShouldProximityHighlight()=>true;
        private void OnDestroy(){art.Dispose();foreach(var material in materials)if(material)Destroy(material);foreach(var mesh in meshes)if(mesh)Destroy(mesh);}
    }
}
