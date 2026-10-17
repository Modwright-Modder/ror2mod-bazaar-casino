using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BazaarCasino
{
    // Shared table controls have a visible pointer/focus state and a compact
    // pill shape. No native HUD skin or gameplay input settings are modified.
    internal sealed class TableButtonStyle : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,IPointerDownHandler,IPointerUpHandler,ISelectHandler,IDeselectHandler
    {
        private static Sprite shape;
        private Button button;
        private TextMeshProUGUI label;
        private Outline border;
        private Image image;
        private bool hover,selected,pressed,primary;
        internal static void Apply(Button button,Image image,TextMeshProUGUI label,bool primary)
        {
            if(!shape)
            {
                const int size=48;const float radius=12;
                var texture=new Texture2D(size,size,TextureFormat.RGBA32,false);var pixels=new Color[size*size];
                for(int y=0;y<size;y++)for(int x=0;x<size;x++)
                {
                    float dx=Mathf.Max(0,Mathf.Abs(x-(size-1)*.5f)-((size-1)*.5f-radius));
                    float dy=Mathf.Max(0,Mathf.Abs(y-(size-1)*.5f)-((size-1)*.5f-radius));
                    pixels[y*size+x]=new Color(1,1,1,Mathf.Clamp01(radius-Mathf.Sqrt(dx*dx+dy*dy)+.5f));
                }
                texture.SetPixels(pixels);texture.Apply();
                shape=Sprite.Create(texture,new Rect(0,0,size,size),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(14,14,14,14));
            }
            image.sprite=shape;image.type=Image.Type.Sliced;
            // New/rebuilt controls must start at their real tint. Native ColorTint
            // begins its tween at white, creating a flash after every click/ACK.
            button.transition=Selectable.Transition.None;
            image.CrossFadeColor(Color.white,0,true,true);
            image.color=button.interactable?BaseColor(primary):new Color(.055f,.08f,.10f,.48f);
            var style=button.gameObject.AddComponent<TableButtonStyle>();style.button=button;style.label=label;style.primary=primary;style.image=image;
            style.border=button.gameObject.AddComponent<Outline>();style.border.effectColor=Color.white;style.border.effectDistance=new Vector2(1.3f,-1.3f);style.border.enabled=false;
        }
        private static Color BaseColor(bool primary)=>primary?new Color(.72f,.56f,.22f,.98f):new Color(.065f,.105f,.14f,.94f);
        internal void SetPrimary(bool value){primary=value;image.color=BaseColor(primary);}
        public void OnPointerEnter(PointerEventData e){hover=true;}
        public void OnPointerExit(PointerEventData e){hover=pressed=false;}
        public void OnPointerDown(PointerEventData e){pressed=true;}
        public void OnPointerUp(PointerEventData e){pressed=false;}
        public void OnSelect(BaseEventData e){selected=true;}
        public void OnDeselect(BaseEventData e){selected=pressed=false;}
        private void Update()
        {
            bool active=button&&button.interactable;
            Color color=!active?new Color(.055f,.08f,.10f,.48f):pressed?new Color(.11f,.19f,.23f,1):hover||selected?
                (primary?new Color(.95f,.78f,.36f,1):new Color(.19f,.31f,.38f,1)):BaseColor(primary);
            image.color=Color.Lerp(image.color,color,1-Mathf.Exp(-Time.unscaledDeltaTime*20));
            border.enabled=active&&(hover||selected);
            float scale=active&&pressed ? .98f : active&&hover ? 1.025f : 1;
            transform.localScale=Vector3.MoveTowards(transform.localScale,Vector3.one*scale,Time.unscaledDeltaTime*.9f);
            label.color=!active?new Color(.48f,.56f,.61f):primary&&!hover&&!selected?new Color(.06f,.08f,.10f):Color.white;
        }
        internal static void Release()
        {if(shape){var texture=shape.texture;Destroy(shape);Destroy(texture);shape=null;}}
    }
}
