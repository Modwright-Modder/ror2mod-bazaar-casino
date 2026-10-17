using System;
using System.Collections;
using System.Diagnostics;
using System.Linq;
using RoR2;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Networking;

namespace BazaarCasino
{
    public sealed partial class CasinoPlugin
    {
        private Coroutine preparingScene;
        private int preparedSceneHandle=-1;
        private bool sceneVisualWarmed;
        private void PrepareLoadedScene(Scene scene,LoadSceneMode mode)
        {
            if(scene.name!="bazaar")return;
            preparedSceneHandle=-1;sceneVisualWarmed=false;
            if(InBazaar)PrepareBazaarNow();
            if(preparingScene==null)preparingScene=StartCoroutine(PrepareWhenSceneReady());
        }
        private void PrepareSceneDirector(SceneDirector director)
        {
            if(!InBazaar)return;
            PrepareBazaarNow();
            if(preparingScene==null&&worldTables.Count<2)preparingScene=StartCoroutine(PrepareWhenSceneReady());
        }
        private IEnumerator PrepareWhenSceneReady()
        {
            // Retry only startup dependencies for up to five seconds, once per frame.
            // Neither camera proximity nor visiting the cave triggers creation.
            float until=Time.unscaledTime+5;
            while(Time.unscaledTime<until)
            {
                yield return null;
                var loaded=SceneManager.GetSceneByName("bazaar");
                if(!Instance||!loaded.IsValid()||!loaded.isLoaded)break;
                if(!InBazaar||SceneManager.GetActiveScene().name!="bazaar")continue;
                PrepareBazaarNow();
                if(worldTables.Count==2&&sceneVisualWarmed)break;
            }
            preparingScene=null;
        }
        private void PrepareBazaarNow()
        {
            if(!InBazaar||SceneManager.GetActiveScene().name!="bazaar")return;
            int handle=SceneManager.GetActiveScene().handle;
            if(previousRun!=Run.instance)
            {
                ClearTables(false);sessions.Clear();previousRun=Run.instance;
            }
            wasBazaar=true;
            if(preparedSceneHandle!=handle)
            {
                preparedSceneHandle=handle;
                var watch=Stopwatch.StartNew();CasinoArt.WarmAll();NativeHermitAssets.Warm();
                if(soundtrack)soundtrack.Warm();
                if(ui)ui.Prepare();
                Logger.LogInfo("Bazaar scene preload: shared artwork and UI ready in "+watch.ElapsedMilliseconds+"ms.");
            }
            if(NetworkServer.active)
            {
                Physics.SyncTransforms();
                if(worldTables.Count<2){nextTableSpawn=0;UpdateTables();}
                if(lounge&&spawnDealer.Value&&!dealer)dealer=DealerNPC.Spawn(lounge);
                if(worldTables.Count==2)BroadcastTables();
            }
            WarmSceneVisuals();
        }
        private void WarmSceneVisuals()
        {
            if(sceneVisualWarmed||!lounge||worldTables.Count<2)return;
            if(lounge.Race&&!lounge.Race.ModelsReady&&!lounge.Race.ModelsUnavailable)return;
            sceneVisualWarmed=true;
            // Force glyph/Canvas generation now, then render offscreen to pay first-use
            // shader/mesh costs at scene startup instead of the cave entrance.
            var watch=Stopwatch.StartNew();GameObject root=null;RenderTexture target=null;
            try
            {
                foreach(var label in lounge.GetComponentsInChildren<TMP_Text>(true))label.ForceMeshUpdate(true);
                foreach(var table in worldTables.Values)
                    foreach(var label in table.GetComponentsInChildren<TMP_Text>(true))label.ForceMeshUpdate(true);
                Canvas.ForceUpdateCanvases();
                if(!Application.isBatchMode)
                {
                    root=new GameObject("CasinoScenePreloadCamera");var camera=root.AddComponent<Camera>();camera.enabled=false;
                    target=RenderTexture.GetTemporary(256,256,16);camera.targetTexture=target;
                    camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
                    camera.nearClipPlane=.1f;camera.farClipPlane=100;camera.fieldOfView=85;
                    camera.cullingMask=LayerIndex.defaultLayer.mask|LayerIndex.world.mask;
                    camera.transform.position=lounge.transform.TransformPoint(new Vector3(0,9,-25));
                    camera.transform.LookAt(lounge.transform.TransformPoint(new Vector3(0,2,8)));camera.Render();
                    camera.transform.position=lounge.Entrance.transform.position-lounge.Entrance.transform.forward*5+Vector3.up*2.5f;
                    camera.transform.LookAt(lounge.Entrance.transform.position+Vector3.up*2);camera.Render();
                }
                Logger.LogInfo("Bazaar scene preload: lounge, tables, broker and first render ready in "+watch.ElapsedMilliseconds+"ms.");
            }
            catch(Exception error){Logger.LogWarning("Casino render preload skipped: "+error.Message);}
            finally
            {
                if(root){root.GetComponent<Camera>().targetTexture=null;Destroy(root);}
                if(target)RenderTexture.ReleaseTemporary(target);
            }
        }
        private void StopScenePreparation()
        {if(preparingScene!=null){StopCoroutine(preparingScene);preparingScene=null;}preparedSceneHandle=-1;}
    }
}
