using RoR2;
using UnityEngine;
using UnityEngine.Rendering;

namespace BazaarCasino
{
    // The off-map lounge has no Bazaar baked probes. Supply its own ambient
    // probe and shadow-free fill only while its scene camera is being rendered.
    internal sealed class CasinoLighting : MonoBehaviour
    {
        private CasinoLounge room;
        private Light key,fill;
        private Camera rendering;
        private AmbientMode previousMode;
        private Color previousAmbient;
        private SphericalHarmonicsL2 previousProbe;
        internal static void Create(CasinoLounge room)
        {
            var lighting=room.gameObject.AddComponent<CasinoLighting>();lighting.room=room;
            lighting.key=lighting.Sun("Casino warm fill",new Vector3(50,-30,0),new Color(1,.91f,.78f),1.25f);
            lighting.fill=lighting.Sun("Casino cool fill",new Vector3(35,150,0),new Color(.7f,.84f,1),.65f);
        }
        private Light Sun(string name,Vector3 angles,Color color,float intensity)
        {
            var root=new GameObject(name);root.transform.SetParent(transform,false);root.transform.localRotation=Quaternion.Euler(angles);
            var light=root.AddComponent<Light>();light.type=LightType.Directional;light.color=color;light.intensity=intensity;
            light.shadows=LightShadows.None;light.renderMode=LightRenderMode.ForcePixel;light.enabled=false;return light;
        }
        private void OnEnable(){Camera.onPreCull+=BeforeRender;Camera.onPostRender+=AfterRender;}
        private void OnDisable(){Camera.onPreCull-=BeforeRender;Camera.onPostRender-=AfterRender;Restore();}
        private void BeforeRender(Camera camera)
        {
            if(rendering||!room||!camera)return;
            bool preload=camera.name=="CasinoScenePreloadCamera";
            if(!preload&&camera!=CasinoBillboard.ViewCamera)return;
            var body=CasinoPlugin.LocalMaster?CasinoPlugin.LocalMaster.GetBody():null;
            if(!room.Contains(camera.transform.position)&&!(body&&room.Contains(body.corePosition)))return;
            rendering=camera;previousMode=RenderSettings.ambientMode;previousAmbient=RenderSettings.ambientLight;previousProbe=RenderSettings.ambientProbe;
            float brightness=CasinoPlugin.Instance?.LoungeBrightness??1.3f;
            var ambient=new Color(.36f,.39f,.44f)*brightness;
            var probe=new SphericalHarmonicsL2();probe.AddAmbientLight(ambient);
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=ambient;RenderSettings.ambientProbe=probe;
            key.intensity=1.25f*brightness;fill.intensity=.65f*brightness;key.enabled=fill.enabled=true;
        }
        private void AfterRender(Camera camera){if(camera==rendering)Restore();}
        private void Restore()
        {
            if(key)key.enabled=false;if(fill)fill.enabled=false;
            if(!rendering)return;
            RenderSettings.ambientMode=previousMode;RenderSettings.ambientLight=previousAmbient;RenderSettings.ambientProbe=previousProbe;rendering=null;
        }
    }
}
