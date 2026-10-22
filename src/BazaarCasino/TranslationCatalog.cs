using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace BazaarCasino
{
    // Pure presentation catalog: messages/protocol identifiers stay unchanged.
    public sealed class TranslationCatalog
    {
        private sealed class Pattern { public string key;public Regex regex;public string value;public int weight; }
        private readonly Dictionary<string,string> exact=new Dictionary<string,string>(StringComparer.Ordinal);
        private readonly List<Pattern> patterns=new List<Pattern>();
        private readonly Dictionary<string,string> cache=new Dictionary<string,string>(StringComparer.Ordinal);
        private static readonly Regex Slot=new Regex(@"\{([0-9]+)\}",RegexOptions.CultureInvariant);
        public bool Add(string key,string value)
        {
            if(string.IsNullOrEmpty(key)||value==null||key.Length>4096||value.Length>16384)return false;
            var slots=Slot.Matches(key).Cast<Match>().Select(m=>m.Value).Distinct().OrderBy(s=>s).ToArray();
            var targets=Slot.Matches(value).Cast<Match>().Select(m=>m.Value).Distinct().OrderBy(s=>s).ToArray();
            if(!slots.SequenceEqual(targets))return false;
            if(slots.Any(slot=>!int.TryParse(slot.Substring(1,slot.Length-2),out int id)||id>31))return false;
            cache.Clear();patterns.RemoveAll(p=>p.key==key);exact.Remove(key);
            if(key==value)return true;
            if(slots.Length==0){exact[key]=value;return true;}
            if(slots.Length>32||Slot.Replace(key,"").Length<3)return false;
            var regex=new StringBuilder("\\A");var ids=new List<int>();int offset=0;
            foreach(Match match in Slot.Matches(key))
            {
                regex.Append(Regex.Escape(key.Substring(offset,match.Index-offset)));
                int id=int.Parse(match.Groups[1].Value,System.Globalization.CultureInfo.InvariantCulture);
                if(ids.Contains(id))regex.Append(@"\k<p"+id+">");
                else{regex.Append("(?<p"+id+">.*?)");ids.Add(id);}
                offset=match.Index+match.Length;
            }
            regex.Append(Regex.Escape(key.Substring(offset))).Append("\\z");
            patterns.Add(new Pattern{key=key,regex=new Regex(regex.ToString(),RegexOptions.Singleline|RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(10)),value=value,weight=Slot.Replace(key,"").Length});
            patterns.Sort((a,b)=>b.weight.CompareTo(a.weight));return true;
        }
        public string Translate(string source)=>Translate(source,0);
        private string Translate(string source,int depth)
        {
            if(string.IsNullOrEmpty(source)||depth>4)return source;
            if(exact.TryGetValue(source,out var exactValue))return exactValue;
            if(cache.TryGetValue(source,out var cached))return cached;
            string result=source;
            foreach(var pattern in patterns)
            {
                Match match;
                try{match=pattern.regex.Match(source);}catch(RegexMatchTimeoutException){continue;}
                if(!match.Success)continue;
                // Replace in one pass so captured player/item text containing braces stays literal.
                result=Slot.Replace(pattern.value,m=>Translate(match.Groups["p"+m.Groups[1].Value].Value,depth+1));break;
            }
            if(cache.Count>=2048)cache.Clear();cache[source]=result;return result;
        }
    }
}
