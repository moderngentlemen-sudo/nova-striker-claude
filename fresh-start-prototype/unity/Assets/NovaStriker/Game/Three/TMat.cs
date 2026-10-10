// three.js materials over URP. MeshStandardMaterial is URP Lit, MeshPhysicalMaterial (the clear-lacquered
// armour) is URP Complex Lit, MeshBasicMaterial and SpriteMaterial are NovaStriker/Unlit. Each starts as a copy
// of a template material in Resources/NovaStriker (made by the editor setup with the keywords it needs, so a
// build keeps those shader variants), and the setters write straight through, so the prototype's code that
// pulses an emissive or fades an opacity ports as it is.
// Colours: hex colours are sRGB, as in three.js; base colours go through SetColor (Unity linearises them, as
// three does), and emissive radiance is written already linear (times its intensity) so HDR values survive.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NovaStriker.Game.Three
{
    public enum Side { Front, Back, Double }
    public enum Blending { Normal, Additive }

    public sealed class TMat
    {
        public enum Kind { Standard, Physical, Basic, Sprite }
        public readonly Kind kind;
        public Material m { get; private set; }
        public readonly Dictionary<string, object> userData = new Dictionary<string, object>();
        internal readonly List<TMesh> owners = new List<TMesh>();

        Color _colorLin = Color.white, _emissiveLin = Color.black;   // linear (three.js keeps colours linear too)
        float _emissiveIntensity = 1, _roughness = 1, _metalness = 0, _opacity = 1, _clearcoat, _clearcoatRoughness, _rotation, _normalScale = 1;
        bool _transparent, _depthWrite = true, _depthTest = true, _vertexColors, _fog = true, _visible = true;
        Side _side = Side.Front;
        Blending _blending = Blending.Normal;
        Texture _map, _normalMap, _roughnessMap, _emissiveMap;
        Vector2 _repeat = Vector2.one, _offset = Vector2.zero;

        public TMat(Kind kind = Kind.Standard)
        {
            this.kind = kind;
            if (kind == Kind.Sprite) { _transparent = true; _side = Side.Double; }
            Build();
        }
        // A material made outside this wrapper (outline shells, the sky): drawn as it is
        TMat(Material raw) { kind = Kind.Basic; m = raw; fixedMaterial = true; }
        readonly bool fixedMaterial;
        public static TMat Raw(Material raw) => new TMat(raw);
        public static TMat Std(uint color, float roughness = 1, float metalness = 0) => new TMat(Kind.Standard) { colorHex = color, roughness = roughness, metalness = metalness };
        public static TMat Basic(uint color) => new TMat(Kind.Basic) { colorHex = color };
        public static TMat Sprite(Texture map, uint color = 0xffffff) => new TMat(Kind.Sprite) { map = map, colorHex = color };
        public TMat clone()
        {
            var c = new TMat(kind)
            {
                _colorLin = _colorLin, _emissiveLin = _emissiveLin, _emissiveIntensity = _emissiveIntensity, _roughness = _roughness, _metalness = _metalness,
                _opacity = _opacity, _clearcoat = _clearcoat, _clearcoatRoughness = _clearcoatRoughness, _rotation = _rotation, _normalScale = _normalScale,
                _transparent = _transparent, _depthWrite = _depthWrite, _depthTest = _depthTest, _vertexColors = _vertexColors, _fog = _fog,
                _side = _side, _blending = _blending, _map = _map, _normalMap = _normalMap, _emissiveMap = _emissiveMap, _roughnessMap = _roughnessMap, _repeat = _repeat, _offset = _offset,
            };
            c.Build();
            foreach (var kv in userData) c.userData[kv.Key] = kv.Value;
            if (rim != null) c.rim = new Material(rim);
            return c;
        }

        // ---- three.js material properties ----
        // sRGB colours (as hex and CSS colours are), or linear ones (colours computed the way three.js does, which
        // may go past 1 for a glow)
        public Color color { get => _colorLin.gamma; set { _colorLin = value.linear; ApplyColor(); } }
        public uint colorHex { set => color = Th.Hex(value); }
        public string colorCss { set => color = Th.Hex(value); }
        public Color colorLin { get => _colorLin; set { _colorLin = value; ApplyColor(); } }
        public Color emissive { get => _emissiveLin.gamma; set { _emissiveLin = value.linear; ApplyEmission(); } }
        public uint emissiveHex { set => emissive = Th.Hex(value); }
        public string emissiveCss { set => emissive = Th.Hex(value); }
        public Color emissiveLin { get => _emissiveLin; set { _emissiveLin = value; ApplyEmission(); } }
        public bool hasEmissive => _emissiveLin.maxColorComponent > 0;
        public float emissiveIntensity { get => _emissiveIntensity; set { _emissiveIntensity = value; ApplyEmission(); } }
        public float roughness { get => _roughness; set { _roughness = value; ApplySurface(); } }
        public float metalness { get => _metalness; set { _metalness = value; ApplySurface(); } }
        public float clearcoat { get => _clearcoat; set { _clearcoat = value; ApplySurface(); } }
        public float clearcoatRoughness { get => _clearcoatRoughness; set { _clearcoatRoughness = value; ApplySurface(); } }
        public float opacity { get => _opacity; set { _opacity = value; ApplyColor(); } }
        public bool transparent { get => _transparent; set { if (_transparent != value) { _transparent = value; Build(); } } }
        public bool depthWrite { get => _depthWrite; set { _depthWrite = value; ApplyState(); } }
        public bool depthTest { get => _depthTest; set { _depthTest = value; ApplyState(); } }
        public Side side { get => _side; set { _side = value; ApplyState(); } }
        public Blending blending { get => _blending; set { _blending = value; ApplyState(); } }
        public bool vertexColors { get => _vertexColors; set { _vertexColors = value; if (IsUnlit && !fixedMaterial) m.SetFloat("_VertexColors", value ? 1 : 0); } }
        public bool fog { get => _fog; set { _fog = value; if (IsUnlit) m.SetFloat("_Fog", value ? 1 : 0); } }
        public float rotation { get => _rotation; set { _rotation = value; if (IsUnlit) m.SetFloat("_Rotation", value); } }
        public Texture map { get => _map; set { _map = value; ApplyMaps(); } }
        public Texture normalMap { get => _normalMap; set { _normalMap = value; ApplyMaps(); } }
        public Texture emissiveMap { get => _emissiveMap; set { _emissiveMap = value; ApplyMaps(); } }
        public Texture roughnessMap { get => _roughnessMap; set { _roughnessMap = value; ApplyMaps(); } }
        public float normalScale { get => _normalScale; set { _normalScale = value; ApplyMaps(); } }
        public Vector2 repeat { get => _repeat; set { _repeat = value; ApplyMaps(); } }
        public Vector2 offset { get => _offset; set { _offset = value; ApplyMaps(); } }
        // (a material switched off hides every mesh that uses it: the outline shells on Low quality)
        public bool visible { get => _visible; set { _visible = value; foreach (var o in owners) o.Refresh(); } }
        public int renderQueueOffset;

        // The rim pass (rigs.js addRim), drawn over every mesh with this material
        public Material rim;
        public void AddRim(Color c, float strength = 0.45f, float power = 2.4f)
        {
            rim = new Material(Templates.Rim);
            rim.SetColor("_RimColor", c); rim.SetFloat("_RimStrength", strength); rim.SetFloat("_RimPower", power);
            rim.SetFloat("_BodyAlpha", 1);
            userData["rimBase"] = strength; userData["rimCol"] = c;
            foreach (var o in owners) o.Refresh();
        }
        public void SetRim(float strength, float alpha, Color? col = null)
        {
            if (rim == null) return;
            rim.SetFloat("_RimStrength", strength); rim.SetFloat("_RimAlpha", alpha);
            if (col.HasValue) rim.SetColor("_RimColor", col.Value);
        }

        bool IsUnlit => kind == Kind.Basic || kind == Kind.Sprite;
        bool Fixed => fixedMaterial;

        // ---- Building the Unity material ----
        void Build()
        {
            Material t;
            if (IsUnlit) t = Templates.Unlit;
            else if (kind == Kind.Physical) t = _transparent ? Templates.CoatT : Templates.Coat;
            else if (_normalMap != null || _roughnessMap != null) t = _transparent ? Templates.SurfT : Templates.Surf;
            else t = _transparent ? Templates.LitT : Templates.Lit;
            var old = m;
            m = new Material(t);
            if (IsUnlit && kind == Kind.Sprite) m.SetFloat("_Billboard", 1);
            ApplyAll();
            if (old != null) { Object.Destroy(old); foreach (var o in owners) o.Refresh(); }
        }
        void ApplyAll()
        {
            ApplyState(); ApplyColor(); ApplyEmission(); ApplySurface(); ApplyMaps();
            if (IsUnlit) { m.SetFloat("_VertexColors", _vertexColors ? 1 : 0); m.SetFloat("_Fog", _fog ? 1 : 0); m.SetFloat("_Rotation", _rotation); }
        }
        void ApplyColor()
        {
            if (fixedMaterial) return;
            m.SetVector("_BaseColor", new Vector4(_colorLin.r, _colorLin.g, _colorLin.b, _opacity));
            if (rim != null) rim.SetFloat("_BodyAlpha", _transparent ? _opacity : 1);
        }
        void ApplyEmission()
        {
            if (fixedMaterial) return;
            if (IsUnlit) return;
            var e = _emissiveLin * _emissiveIntensity;
            m.SetVector("_EmissionColor", new Vector4(e.r, e.g, e.b, 1));
        }
        void ApplySurface()
        {
            if (fixedMaterial) return;
            if (IsUnlit) return;
            m.SetFloat("_Smoothness", 1 - _roughness);
            m.SetFloat("_Metallic", _metalness);
            if (kind == Kind.Physical)
            {
                m.SetFloat("_ClearCoatMask", _clearcoat);
                m.SetFloat("_ClearCoatSmoothness", 1 - _clearcoatRoughness);
            }
        }
        void ApplyMaps()
        {
            if (fixedMaterial) return;
            m.SetTexture("_BaseMap", _map != null ? _map : Texture2D.whiteTexture);
            m.SetTextureScale("_BaseMap", _repeat); m.SetTextureOffset("_BaseMap", _offset);
            if (IsUnlit) return;
            bool surf = _normalMap != null || _roughnessMap != null;
            bool tplSurf = m.IsKeywordEnabled("_NORMALMAP");
            if (surf != tplSurf && kind == Kind.Standard) { Build(); return; }
            m.SetTexture("_EmissionMap", _emissiveMap != null ? _emissiveMap : Texture2D.whiteTexture);
            m.SetTextureScale("_EmissionMap", _repeat);
            if (_normalMap != null) { m.SetTexture("_BumpMap", _normalMap); m.SetFloat("_BumpScale", _normalScale); }
            // (the roughness map is packed as Unity's metallic/smoothness map: smoothness in alpha)
            if (_roughnessMap != null) { m.SetTexture("_MetallicGlossMap", _roughnessMap); m.SetFloat("_Smoothness", 1); }
        }
        void ApplyState()
        {
            if (fixedMaterial) return;
            int src, dst;
            if (_blending == Blending.Additive) { src = (int)BlendMode.SrcAlpha; dst = (int)BlendMode.One; }
            else if (_transparent) { src = (int)BlendMode.SrcAlpha; dst = (int)BlendMode.OneMinusSrcAlpha; }
            else { src = (int)BlendMode.One; dst = (int)BlendMode.Zero; }
            bool see = _transparent || _blending == Blending.Additive;
            m.SetFloat("_SrcBlend", src); m.SetFloat("_DstBlend", dst);
            m.SetFloat("_SrcBlendAlpha", see ? (int)BlendMode.One : src); m.SetFloat("_DstBlendAlpha", see ? (int)BlendMode.OneMinusSrcAlpha : dst);
            m.SetFloat("_ZWrite", _depthWrite ? 1 : 0);
            m.SetFloat("_ZTest", _depthTest ? (int)CompareFunction.LessEqual : (int)CompareFunction.Always);
            m.SetFloat("_Cull", _side == Side.Front ? (int)CullMode.Back : _side == Side.Back ? (int)CullMode.Front : (int)CullMode.Off);
            if (!IsUnlit)
            {
                m.SetFloat("_Surface", see ? 1 : 0);
                m.SetFloat("_Blend", _blending == Blending.Additive ? 2 : 0);
                if (see) { m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.SetOverrideTag("RenderType", "Transparent"); }
                else { m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.SetOverrideTag("RenderType", "Opaque"); }
            }
            m.renderQueue = (see ? (int)RenderQueue.Transparent : (int)RenderQueue.Geometry) + renderQueueOffset;
        }
    }

    // The template materials (Resources/NovaStriker/*.mat, made by Editor/NovaSetup.cs). In the editor, a missing
    // template is made on the spot from its shader.
    public static class Templates
    {
        static Material lit, litT, surf, surfT, coat, coatT, unlit, outline, rim, sky, grade, aegis, reflect, pAdd, pAlpha, pLit;
        public static Material Lit => lit ??= Load("Lit", "Universal Render Pipeline/Lit", false, false);
        public static Material LitT => litT ??= Load("LitTransparent", "Universal Render Pipeline/Lit", true, false);
        public static Material Surf => surf ??= Load("LitSurface", "Universal Render Pipeline/Lit", false, true);
        public static Material SurfT => surfT ??= Load("LitSurfaceTransparent", "Universal Render Pipeline/Lit", true, true);
        public static Material Coat => coat ??= Load("Coat", "Universal Render Pipeline/Complex Lit", false, false, true);
        public static Material CoatT => coatT ??= Load("CoatTransparent", "Universal Render Pipeline/Complex Lit", true, false, true);
        public static Material Unlit => unlit ??= Load("Unlit", "NovaStriker/Unlit", false, false);
        public static Material Outline => outline ??= Load("Outline", "NovaStriker/Outline", false, false);
        public static Material Rim => rim ??= Load("Rim", "NovaStriker/Rim", false, false);
        public static Material Sky => sky ??= Load("Sky", "NovaStriker/Sky", false, false);
        public static Material Grade => grade ??= Load("Grade", "NovaStriker/Grade", false, false);
        public static Material Aegis => aegis ??= Load("Aegis", "NovaStriker/Aegis", false, false);
        public static Material Reflect => reflect ??= Load("Reflect", "NovaStriker/Reflect", false, false);
        // Particle materials (URP's particle shaders): additive glow, alpha-blended, and lit (smoke that takes the sun)
        public const string PARTICLE_UNLIT = "Universal Render Pipeline/Particles/Unlit", PARTICLE_LIT = "Universal Render Pipeline/Particles/Lit";
        public static Material ParticleAdd => pAdd ??= LoadParticle("ParticleAdd", PARTICLE_UNLIT);
        public static Material ParticleAlpha => pAlpha ??= LoadParticle("ParticleAlpha", PARTICLE_UNLIT);
        public static Material ParticleLit => pLit ??= LoadParticle("ParticleLit", PARTICLE_LIT);
        static Material LoadParticle(string name, string shader)
        {
            var m = Resources.Load<Material>("NovaStriker/" + name);
            if (m != null) return m;
            m = new Material(Shader.Find(shader)) { name = name };
            ConfigureParticle(m, name);
            return m;
        }
        // The keywords and blend state each particle template is saved with (shared with the editor setup): see-through,
        // soft where it meets geometry (the depth texture), additive or alpha blended, double sided
        public static void ConfigureParticle(Material m, string name)
        {
            bool additive = name == "ParticleAdd";
            m.SetFloat("_Surface", 1); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.SetOverrideTag("RenderType", "Transparent");
            m.SetFloat("_ZWrite", 0); m.SetFloat("_Cull", 0);
            m.SetFloat("_Blend", additive ? 2 : 0);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            m.SetFloat("_SrcBlendAlpha", (float)(additive ? BlendMode.Zero : BlendMode.One)); m.SetFloat("_DstBlendAlpha", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            m.SetFloat("_SoftParticlesEnabled", 1); m.SetFloat("_SoftParticlesNearFadeDistance", 0); m.SetFloat("_SoftParticlesFarFadeDistance", 0.8f);
            m.SetVector("_SoftParticleFadeParams", new Vector4(0, 1 / 0.8f, 0, 0)); m.EnableKeyword("_SOFTPARTICLES_ON");
            m.SetVector("_CameraFadeParams", new Vector4(0, Mathf.Infinity, 0, 0));
            m.renderQueue = (int)RenderQueue.Transparent;
            m.enableInstancing = true;
        }

        static Material Load(string name, string shader, bool transparent, bool surface, bool coat = false)
        {
            var m = Resources.Load<Material>("NovaStriker/" + name);
            if (m != null) return m;
            m = new Material(Shader.Find(shader)) { name = name };
            Configure(m, transparent, surface, coat);
            return m;
        }
        // The keywords each template is saved with (shared with the editor setup)
        public static void Configure(Material m, bool transparent, bool surface, bool coat)
        {
            if (!m.shader.name.StartsWith("Universal Render Pipeline")) return;
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            m.SetVector("_EmissionColor", Vector4.zero);
            if (surface) { m.EnableKeyword("_NORMALMAP"); m.EnableKeyword("_METALLICSPECGLOSSMAP"); m.SetFloat("_SmoothnessTextureChannel", 0); }
            if (coat) { m.SetFloat("_ClearCoat", 1); m.EnableKeyword("_CLEARCOAT"); }
            if (transparent)
            {
                m.SetFloat("_Surface", 1); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.SetOverrideTag("RenderType", "Transparent"); m.renderQueue = (int)RenderQueue.Transparent;
                m.SetFloat("_ZWrite", 0); m.SetFloat("_SrcBlend", (int)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            }
        }
    }
}
