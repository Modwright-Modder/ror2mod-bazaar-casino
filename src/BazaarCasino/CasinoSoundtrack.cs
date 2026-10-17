using System;
using System.Globalization;
using System.IO;
using System.Linq;
using BepInEx.Configuration;
using RoR2;
using UnityEngine;

namespace BazaarCasino
{
    internal sealed class CasinoSoundtrack : MonoBehaviour
    {
        private ConfigEntry<bool> enabledMusic;
        private ConfigEntry<float> volume;
        private AudioSource source;
        private AudioClip clip;
        private AudioListener ownedListener;
        private bool paused;
        private bool focusedAudioOnly=true;
        private float nextVolumeRead,nextListenerCheck,cachedVolume=1;
        internal void Configure(ConfigEntry<bool> enabledMusic,ConfigEntry<float> volume)
        {this.enabledMusic=enabledMusic;this.volume=volume;MusicController.pickTrackHook+=PickTrack;}
        internal void Warm()
        {
            if(clip||!enabledMusic.Value||Application.isBatchMode)return;
            try
            {
                var assembly=typeof(CasinoSoundtrack).Assembly;
                var name=assembly.GetManifestResourceNames().First(n=>n.EndsWith("casino-lounge.wav",StringComparison.Ordinal));
                using(var stream=assembly.GetManifestResourceStream(name))using(var reader=new BinaryReader(stream))
                {
                    if(new string(reader.ReadChars(4))!="RIFF")throw new InvalidDataException("Expected RIFF audio");
                    reader.ReadInt32();if(new string(reader.ReadChars(4))!="WAVE")throw new InvalidDataException("Expected WAVE audio");
                    int channels=0,rate=0,bits=0;byte[] pcm=null;
                    while(stream.Position+8<=stream.Length)
                    {
                        var id=new string(reader.ReadChars(4));int length=reader.ReadInt32();long next=stream.Position+length+(length&1);
                        if(id=="fmt "){int format=reader.ReadInt16();channels=reader.ReadInt16();rate=reader.ReadInt32();reader.ReadInt32();reader.ReadInt16();bits=reader.ReadInt16();if(format!=1)throw new InvalidDataException("Expected PCM audio");}
                        if(id=="data")pcm=reader.ReadBytes(length);
                        stream.Position=next;
                    }
                    if(pcm==null||bits!=16||channels<1||rate<8000)throw new InvalidDataException("Unsupported casino audio");
                    var samples=new float[pcm.Length/2];for(int i=0;i<samples.Length;i++)samples[i]=(short)(pcm[i*2]|pcm[i*2+1]<<8)/32768f;
                    clip=AudioClip.Create("Bazaar After Hours",samples.Length/channels,channels,rate,false);clip.SetData(samples,0);
                }
                source=gameObject.AddComponent<AudioSource>();source.playOnAwake=false;source.loop=true;source.spatialBlend=0;source.volume=0;source.clip=clip;
                Debug.Log("[BazaarCasino] Casino BGM preloaded: original 40-second lounge loop.");
            }
            catch(Exception error){Debug.LogError("[BazaarCasino] Casino BGM unavailable: "+error.Message);}
        }
        private bool Inside=>CasinoPlugin.Instance&&CasinoPlugin.Instance.InLounge;
        private void PickTrack(MusicController controller,ref MusicTrackDef track)
        {if(Inside&&clip&&enabledMusic.Value&&volume.Value>0)track=null;}
        private float GameMusicVolume()
        {
            if(Time.unscaledTime<nextVolumeRead)return cachedVolume;
            nextVolumeRead=Time.unscaledTime+.25f;
            var text=RoR2.Console.instance?.FindConVar("volume_music")?.GetString();
            focusedAudioOnly=RoR2.Console.instance?.FindConVar("audio_focused_only")?.GetString()!="0";
            cachedVolume=float.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out float value)?Mathf.Clamp01(value/100f):1;return cachedVolume;
        }
        private void Update()
        {
            if(!source){if(Inside)Warm();return;}
            float target=Inside&&enabledMusic.Value?volume.Value*GameMusicVolume():0;
            if(!Application.isFocused&&focusedAudioOnly)target=0;
            source.volume=Mathf.MoveTowards(source.volume,target,Time.unscaledDeltaTime*.35f);
            if(source.volume>.001f)
            {
                if(!ownedListener&&Time.unscaledTime>=nextListenerCheck)
                {
                    nextListenerCheck=Time.unscaledTime+1;
                    if(!FindObjectsOfType<AudioListener>().Any(l=>l&&l.enabled)){var camera=CasinoBillboard.ViewCamera;if(camera)ownedListener=camera.gameObject.AddComponent<AudioListener>();}
                }
                if(target>0&&!source.isPlaying&&!paused)source.Play();
                if(Time.timeScale==0&&!paused){source.Pause();paused=true;}
                if(Time.timeScale>0&&paused){source.UnPause();paused=false;}
            }
            else if(target==0)
            {source.Stop();paused=false;if(ownedListener){Destroy(ownedListener);ownedListener=null;}}
        }
        private void OnDestroy()
        {MusicController.pickTrackHook-=PickTrack;if(source)source.Stop();if(ownedListener)Destroy(ownedListener);if(clip)Destroy(clip);}
    }
    public sealed partial class CasinoPlugin
    {
        private CasinoSoundtrack soundtrack;
        internal bool InLounge
        {
            get{var body=LocalMaster?LocalMaster.GetBody():null;return InBazaar&&lounge&&body&&lounge.Contains(body.corePosition);}
        }
        private void ConfigureSoundtrack()
        {
            soundtrack=gameObject.AddComponent<CasinoSoundtrack>();
            soundtrack.Configure(Config.Bind("Audio","Enabled",true,"Play the original casino lounge soundtrack while inside the lounge."),
                Config.Bind("Audio","MusicVolume",.4f,new ConfigDescription("Casino BGM volume multiplied by the game's current music volume; fades on entry/exit.",new AcceptableValueRange<float>(0,1))));
        }
    }
}
