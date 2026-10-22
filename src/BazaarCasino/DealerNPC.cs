using System.Collections.Generic;
using System.Linq;
using RoR2;
using TMPro;
using UnityEngine;

namespace BazaarCasino
{
    // Cosmetic local dealer. Native targeting/interaction opens the local UI; all
    // inventory and gold transactions continue through the authenticated protocol.
    internal sealed class DealerNPC : MonoBehaviour, IInteractable
    {
        private readonly List<Material> materials = new List<Material>();
        
        internal static GameObject Spawn(CasinoLounge room)
        {
            if(!room)return null;
            var root = new GameObject("CasinoLoungeItemBroker");
            root.transform.SetParent(room.transform,false);
            root.transform.localPosition=new Vector3(-21,.08f,-27.2f);
            root.transform.localRotation=Quaternion.identity;
            // Default belongs to CommonMasks.interactable for native E targeting.
            root.layer = LayerIndex.defaultLayer.intVal;
            root.AddComponent<EntityLocator>().entity = root;
            var collider = root.AddComponent<CapsuleCollider>(); collider.center = Vector3.up * 1.45f; collider.height = 3.2f; collider.radius = 0.8f; collider.isTrigger = true;
            var dealer = root.AddComponent<DealerNPC>(); dealer.BuildModel();
            Debug.Log("[BazaarCasino] Item broker spawned inside Casino Lounge at " + root.transform.position);
            return root;
        }
        private void BuildModel()
        {
            var blue = new Color(0.13f, 0.47f, 0.62f);
            var gold = new Color(0.88f, 0.66f, 0.22f);
            var dark = new Color(0.07f, 0.12f, 0.19f);
            Part("Pedestal", PrimitiveType.Cylinder, new Vector3(0,0.16f,0), new Vector3(1.4f,0.16f,1.4f), dark);
            Part("Robe", PrimitiveType.Capsule, new Vector3(0,1.18f,0), new Vector3(1.12f,0.82f,0.9f), blue);
            Part("Collar", PrimitiveType.Cylinder, new Vector3(0,2.02f,0), new Vector3(1.05f,0.09f,1.05f), gold);
            var face = Part("Mask", PrimitiveType.Sphere, new Vector3(0,2.42f,0), new Vector3(0.88f,0.9f,0.77f), dark);
            Part("LeftEye", PrimitiveType.Sphere, new Vector3(-0.18f,2.47f,0.36f), new Vector3(0.14f,0.12f,0.06f), gold);
            Part("RightEye", PrimitiveType.Sphere, new Vector3(0.18f,2.47f,0.36f), new Vector3(0.14f,0.12f,0.06f), gold);
            Part("HatBrim", PrimitiveType.Cylinder, new Vector3(0,2.84f,0), new Vector3(1.22f,0.05f,1.22f), gold);
            Part("Hat", PrimitiveType.Cylinder, new Vector3(0,3.01f,0), new Vector3(0.72f,0.16f,0.72f), blue);
            Part("LeftHand", PrimitiveType.Sphere, new Vector3(-0.62f,1.60f,0.17f), new Vector3(0.34f,0.36f,0.34f), gold);
            Part("RightHand", PrimitiveType.Sphere, new Vector3(0.62f,1.60f,0.17f), new Vector3(0.34f,0.36f,0.34f), gold);
            var highlight = gameObject.AddComponent<Highlight>(); highlight.targetRenderer = face.GetComponent<Renderer>(); highlight.highlightColor = Highlight.HighlightColor.interactive;
            var labelObj = new GameObject("DealerNameplate"); labelObj.transform.SetParent(transform, false); labelObj.transform.localPosition = Vector3.up * 3.65f;
            labelObj.transform.localRotation=Quaternion.Euler(0,180,0);
            labelObj.AddComponent<CasinoBillboard>();
            var label = labelObj.AddComponent<TextMeshPro>();
            var template = Resources.FindObjectsOfTypeAll<TextMeshProUGUI>().FirstOrDefault(t => t && t.font);
            if (template) label.font = template.font;
            CasinoText.Bind(label);label.text = "LUNAR BROKER\n<color=#E8BC54>E · Sell items for gold</color>"; label.fontSize = 3; label.alignment = TextAlignmentOptions.Center;
            label.rectTransform.sizeDelta = new Vector2(5,1.2f);
        }
        private GameObject Part(string name, PrimitiveType type, Vector3 position, Vector3 size, Color color)
        {
            var obj = GameObject.CreatePrimitive(type); obj.name = name; obj.transform.SetParent(transform, false); obj.transform.localPosition = position; obj.transform.localScale = size;
            var collider = obj.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
            var renderer = obj.GetComponent<Renderer>();
            var material = new Material(renderer.sharedMaterial); material.color = color; renderer.sharedMaterial = material; materials.Add(material);
            return obj;
        }
        public string GetContextString(Interactor activator) => CasinoText.Get("Sell Items — Lunar Broker");
        public Interactability GetInteractability(Interactor activator) => CasinoPlugin.InBazaar ? Interactability.Available : Interactability.Disabled;
        public void OnInteractionBegin(Interactor activator) => CasinoPlugin.Instance?.OpenDealer(activator, gameObject);
        public bool ShouldIgnoreSpherecastForInteractibility(Interactor activator) => false;
        public bool ShouldShowOnScanner() => true;
        public bool ShouldProximityHighlight() => true;
        private void OnDestroy() { foreach (var material in materials) if (material) Destroy(material); }
    }
}
