using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace BazaarCasino
{
    internal sealed class CasinoArt : IDisposable
    {
        private static readonly Dictionary<string,Sprite> sprites = new Dictionary<string,Sprite>();
        internal Sprite Get(string name)
        {
            if(sprites.TryGetValue(name,out var sprite)) return sprite;
            var assembly=typeof(CasinoArt).Assembly;
            using(var stream=assembly.GetManifestResourceStream("BazaarCasino.Assets."+name+".png"))
            {
                if(stream==null) throw new FileNotFoundException("Missing embedded casino artwork: "+name);
                using(var buffer=new MemoryStream())
                {
                    stream.CopyTo(buffer);
                    var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
                    if(!ImageConversion.LoadImage(texture,buffer.ToArray(),true)) throw new IOException("Invalid casino artwork: "+name);
                    texture.filterMode=FilterMode.Bilinear;
                    sprite=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f),100);
                    sprites[name]=sprite; return sprite;
                }
            }
        }
        internal static void WarmAll()
        {
            var art=new CasinoArt();art.Get("back");art.Get("chip");art.Get("ball");
            for(int i=0;i<6;i++)art.Get("slot"+i);
            for(int i=0;i<52;i++)art.Get("card"+i);
        }
        // Scene/UI wrappers borrow the shared sprites. Only plugin shutdown owns disposal.
        public void Dispose() { }
        internal static void ReleaseShared()
        {foreach(var sprite in sprites.Values){UnityEngine.Object.Destroy(sprite.texture);UnityEngine.Object.Destroy(sprite);}sprites.Clear();}
    }
}
