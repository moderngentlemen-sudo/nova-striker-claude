// The effect textures (Art/Blender/make_fx_textures.py writes them as PNG data in Resources/NovaStriker/Fx/<name>.png
// .bytes, so no import setting can change them): loaded once and cached. Data textures (normals, the distortion
// ripple, cloud puffs) are linear; the rest are colour.
using System.Collections.Generic;
using UnityEngine;

namespace NovaStriker.Game
{
    public static class FxTex
    {
        static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();
        static readonly HashSet<string> LINEAR = new HashSet<string> { "ripple", "cloudpuff" };

        public static Texture2D Get(string name)
        {
            if (cache.TryGetValue(name, out var t)) return t;
            var src = Resources.Load<TextAsset>("NovaStriker/Fx/" + name + ".png");
            if (src != null)
            {
                t = new Texture2D(2, 2, TextureFormat.RGBA32, true, LINEAR.Contains(name)) { name = "fx-" + name };
                if (t.LoadImage(src.bytes, false))
                {
                    t.wrapMode = name == "streak" ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                    t.anisoLevel = 4; t.Apply(true, true);
                }
                else { Object.Destroy(t); t = null; }
            }
            if (t == null) Debug.LogWarning("Nova Striker: effect texture " + name + " is missing (Resources/NovaStriker/Fx)");
            cache[name] = t;
            return t;
        }
    }
}
