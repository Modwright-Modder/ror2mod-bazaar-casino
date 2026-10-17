using System;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace BazaarCasino
{
    // Import the real FBX, material and clips at runtime. Body prefabs contain
    // unloaded skin placeholders in current RoR2; they are not visual templates.
    internal static class NativeHermitAssets
    {
        private static bool started,reported;
        private static AsyncOperationHandle<GameObject> model;
        private static AsyncOperationHandle<RuntimeAnimatorController> controller;
        private static AsyncOperationHandle<Material> material;
        internal static bool Ready=>started&&model.Status==AsyncOperationStatus.Succeeded&&controller.Status==AsyncOperationStatus.Succeeded&&material.Status==AsyncOperationStatus.Succeeded;
        internal static void Warm()
        {
            if(started)return;started=true;
            model=Addressables.LoadAssetAsync<GameObject>("aa99185cf4814e649a49d671c6ff3c8e");
            controller=Addressables.LoadAssetAsync<RuntimeAnimatorController>("a6e1a964939f00d499b8a492ef8fa9c6");
            material=Addressables.LoadAssetAsync<Material>("RoR2/Base/HermitCrab/matHermitCrab.mat");
        }
        internal static bool Failed
        {
            get
            {
                if(!started)return false;
                bool failed=model.Status==AsyncOperationStatus.Failed||controller.Status==AsyncOperationStatus.Failed||material.Status==AsyncOperationStatus.Failed;
                if(failed&&!reported){reported=true;Debug.LogError("[BazaarCasino] Native Hermit Crab model/animation/material could not load. Derby betting stays disabled; no substitute crab is spawned.");}
                return failed;
            }
        }
        internal static NativeHermitVisual Create(Transform parent,int lane)
        {
            if(!Ready)throw new InvalidOperationException("Native Hermit Crab assets are still loading.");
            var clone=UnityEngine.Object.Instantiate(model.Result,parent,false);
            clone.name="Native Hermit Crab "+(lane+1);clone.transform.localPosition=Vector3.zero;clone.transform.localRotation=Quaternion.identity;
            foreach(var child in clone.GetComponentsInChildren<Transform>(true))child.gameObject.layer=RoR2.LayerIndex.defaultLayer.intVal;
            var animator=clone.GetComponentInChildren<Animator>(true);
            if(!animator||!animator.avatar)throw new InvalidOperationException("Native Hermit Crab FBX lacks its Animator/avatar.");
            animator.applyRootMotion=false;animator.fireEvents=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            animator.runtimeAnimatorController=null;
            foreach(var renderer in clone.GetComponentsInChildren<Renderer>(true))renderer.sharedMaterial=material.Result;
            var visual=clone.AddComponent<NativeHermitVisual>();visual.Initialize(animator,controller.Result.animationClips);
            visual.Sample(new CrabMotion{phase=CrabMotionPhase.Idle});
            var renderers=clone.GetComponentsInChildren<Renderer>(true);
            var bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
            float scale=2.4f/Mathf.Max(.1f,Mathf.Max(bounds.size.x,bounds.size.z));
            clone.transform.localScale*=scale;
            clone.transform.localPosition=Vector3.up*(-(bounds.min.y-clone.transform.position.y)*scale);
            Debug.Log("[BazaarCasino] Native Hermit Crab "+(lane+1)+" ready: real mesh/avatar/material, RunForward, BurrowIn, Burrowed and BurrowOut clips.");
            return visual;
        }
        internal static void Release()
        {
            if(!started)return;
            if(model.IsValid())Addressables.Release(model);if(controller.IsValid())Addressables.Release(controller);if(material.IsValid())Addressables.Release(material);
            started=reported=false;
        }
    }
    internal sealed class NativeHermitVisual : MonoBehaviour
    {
        private PlayableGraph graph;
        private AnimationMixerPlayable mixer;
        private readonly AnimationClipPlayable[] clips=new AnimationClipPlayable[5];
        private readonly float[] lengths=new float[5];
        private int current=-1,previous=-1;
        private float transition;
        internal void Initialize(Animator animator,AnimationClip[] nativeClips)
        {
            graph=PlayableGraph.Create("Casino Native Hermit Crab");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            mixer=AnimationMixerPlayable.Create(graph,5);
            var names=new[]{"RunForward","BurrowIn","Burrowed","BurrowOut","Idle, Single"};
            for(int i=0;i<names.Length;i++)
            {
                var clip=nativeClips.FirstOrDefault(c=>c&&c.name.EndsWith("|"+names[i],StringComparison.Ordinal));
                if(!clip)throw new InvalidOperationException("Native Hermit Crab clip missing: "+names[i]);
                lengths[i]=clip.length;clips[i]=AnimationClipPlayable.Create(graph,clip);clips[i].SetApplyFootIK(false);clips[i].SetApplyPlayableIK(false);
                graph.Connect(clips[i],0,mixer,i);mixer.SetInputWeight(i,0);
            }
            var output=AnimationPlayableOutput.Create(graph,"Native Hermit Crab pose",animator);output.SetSourcePlayable(mixer);graph.Play();
        }
        internal void Sample(CrabMotion pose)
        {
            if(!graph.IsValid())return;
            int index=(int)pose.phase;
            if(current!=index){previous=current;current=index;transition=Time.unscaledTime;}
            float blend=previous<0?1:Mathf.Clamp01((Time.unscaledTime-transition)/.08f);
            for(int i=0;i<clips.Length;i++)mixer.SetInputWeight(i,i==current?blend:i==previous?1-blend:0);
            double time=pose.phase==CrabMotionPhase.Run?pose.progress%1*lengths[index]:pose.progress*lengths[index];
            clips[index].SetTime(time);graph.Evaluate(0);
        }
        private void OnDestroy(){if(graph.IsValid())graph.Destroy();}
    }
}
