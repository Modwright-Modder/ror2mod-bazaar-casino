using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BazaarCasino
{
    // Cabinet UI uses a world canvas for drawing and a ray/rectangle test for
    // input. It never adds a screen overlay or participates in the pause menu.
    internal sealed class MachineCanvas
    {
        internal readonly RectTransform Root;
        private readonly TMP_FontAsset font;
        private sealed class Control
        {
            internal int id;
            internal RectTransform rect;
            internal Image image;
            internal TextMeshProUGUI label;
            internal Outline outline;
        }
        private readonly List<Control> controls=new List<Control>();
        internal MachineCanvas(Transform parent,string name,Vector3 position,Vector2 size,float scale)
        {
            Root=Rect(parent,name);Root.localPosition=position;Root.localScale=Vector3.one*scale;Root.sizeDelta=size;
            var canvas=Root.gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
            // Unity's canvas front faces local -Z, matching the cabinet face.
            font=parent.GetComponentInChildren<TMP_Text>(true)?.font??TMP_Settings.defaultFontAsset;
        }
        internal static RectTransform Rect(Transform parent,string name)
        {
            var rect=(RectTransform)new GameObject(name,typeof(RectTransform)).transform;
            rect.SetParent(parent,false);rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);return rect;
        }
        internal static void Place(RectTransform rect,float x0,float y0,float x1,float y1)
        {rect.anchorMin=new Vector2(x0,y0);rect.anchorMax=new Vector2(x1,y1);rect.offsetMin=rect.offsetMax=Vector2.zero;}
        internal TextMeshProUGUI Text(Transform parent,string value,float size,Color color,TextAlignmentOptions alignment=TextAlignmentOptions.Center)
        {
            var label=Rect(parent,"Cabinet text").gameObject.AddComponent<TextMeshProUGUI>();
            label.font=font;label.text=value;label.fontSize=size;label.color=color;label.alignment=alignment;
            label.raycastTarget=false;label.enableWordWrapping=false;label.overflowMode=TextOverflowModes.Ellipsis;return label;
        }
        internal TextMeshProUGUI Text(string value,float size,Color color,float x0,float y0,float x1,float y1)
        {var text=Text(Root,value,size,color);Place(text.rectTransform,x0,y0,x1,y1);return text;}
        internal TextMeshProUGUI Button(int id,string caption,float size,float x0,float y0,float x1,float y1)
        {
            var rect=Rect(Root,"Cabinet control "+id);Place(rect,x0,y0,x1,y1);
            var image=rect.gameObject.AddComponent<Image>();image.color=new Color(.07f,.10f,.13f,.98f);image.raycastTarget=false;
            var outline=rect.gameObject.AddComponent<Outline>();outline.effectColor=Color.white;outline.effectDistance=new Vector2(1,-1);outline.enabled=false;
            var label=Text(rect,caption,size,Color.white);Place(label.rectTransform,.025f,.025f,.975f,.975f);
            controls.Add(new Control{id=id,rect=rect,image=image,label=label,outline=outline});return label;
        }
        internal int Hover(Ray ray,bool enabled,bool allowExit=false)
        {
            int hit=-1;
            if((enabled||allowExit)&&new Plane(Root.forward,Root.position).Raycast(ray,out float distance)&&distance>0&&distance<20)
            {
                var point=ray.GetPoint(distance);
                if(Root.rect.Contains((Vector2)Root.InverseTransformPoint(point)))
                    foreach(var control in controls)
                        if((enabled||allowExit&&control.id==4)&&control.rect.rect.Contains((Vector2)control.rect.InverseTransformPoint(point))){hit=control.id;break;}
            }
            foreach(var control in controls)
            {
                bool hover=control.id==hit;control.outline.enabled=hover;
                control.image.color=hover?new Color(.18f,.24f,.28f,.98f):new Color(.07f,.10f,.13f,.98f);
                control.label.color=enabled||control.id==4?Color.white:new Color(.48f,.52f,.55f);
            }
            return hit;
        }
    }
}
