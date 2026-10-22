using System;
using System.IO;
using Path=System.IO.Path;
using System.Text.RegularExpressions;
using BepInEx.Configuration;
using BepInEx.Logging;
using RoR2;
using TMPro;
using UnityEngine;

namespace BazaarCasino
{
    internal static class CasinoText
    {
        [Serializable] private sealed class Pack { public Entry[] entries=null; }
        [Serializable] private sealed class Entry { public string key=null;public string text=null; }
        private static ConfigEntry<string> selection;
        private static string folder,loaded;
        private static int checkedFrame=-1;
        private static ManualLogSource log;
        private static TranslationCatalog catalog=new TranslationCatalog();
        internal static int Revision { get;private set; }
        internal static void Initialize(ConfigFile config,string location,ManualLogSource logger)
        {
            folder=Path.Combine(Path.GetDirectoryName(location),"language");log=logger;
            selection=config.Bind("Localization","Language","auto","Local UI language: auto follows the game; or enter a language file name such as en, id or es. Place UTF-8 JSON translations beside the DLL in language/. Missing/invalid entries fall back to English. Restart after editing files.");
            loaded=null;checkedFrame=-1;
        }
        internal static void Refresh()
        {
            if(checkedFrame==Time.frameCount)return;checkedFrame=Time.frameCount;
            string code=selection?.Value??"auto";
            if(string.Equals(code,"auto",StringComparison.OrdinalIgnoreCase))code=Language.currentLanguage?.name??"en";
            if(!Regex.IsMatch(code,@"\A[A-Za-z0-9_-]{1,32}\z"))code="en";
            code=code.ToLowerInvariant();if(loaded==code)return;
            loaded=code;catalog=new TranslationCatalog();Revision++;
            if(folder==null)return;
            // Regional packs overlay their base language; English is the source fallback.
            int separator=code.IndexOfAny(new[]{'-','_'});
            if(separator>0)Load(code.Substring(0,separator));
            Load(code);
        }
        private static void Load(string code)
        {
            string path=Path.Combine(folder,code+".json");if(!File.Exists(path))return;
            try
            {
                if(new FileInfo(path).Length>1024*1024)throw new InvalidDataException("Language file exceeds 1 MB.");
                var pack=JsonUtility.FromJson<Pack>(File.ReadAllText(path,System.Text.Encoding.UTF8));
                if(pack?.entries==null||pack.entries.Length>4096)throw new InvalidDataException("Invalid language entries.");
                int invalid=0;
                foreach(var entry in pack.entries)
                    if(entry==null||!catalog.Add(entry.key,entry.text))invalid++;
                if(invalid>0)log?.LogWarning("Skipped "+invalid+" invalid translation entries in "+path);
            }
            catch(Exception error){log?.LogWarning("Cannot load casino language "+path+": "+error.Message);}
        }
        internal static string Get(string source){Refresh();return catalog.Translate(source);}
        internal static void Bind(TMP_Text label)
        {if(label&&!label.GetComponent<CasinoLocalizedLabel>())label.gameObject.AddComponent<CasinoLocalizedLabel>();}
    }
    internal sealed class CasinoLocalizedLabel : MonoBehaviour
    {
        private TMP_Text label;
        private string raw,shown;
        private int revision=-1;
        private void Awake()=>label=GetComponent<TMP_Text>();
        private void LateUpdate()
        {
            if(!label)return;CasinoText.Refresh();
            if(label.text!=shown)raw=label.text;
            if(raw==null)return;
            // Get is cached for repeated wallet/countdown values and English fallbacks.
            if(label.text!=shown||revision!=CasinoText.Revision){shown=CasinoText.Get(raw);revision=CasinoText.Revision;label.text=shown;}
        }
    }
}
