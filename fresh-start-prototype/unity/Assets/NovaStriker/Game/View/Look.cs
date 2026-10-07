// The look (look.js, and landmarks.js's ATMOS): what makes surfaces read like painted metal and gives each
// route its own mood.
//   LOOK: per route lighting (key, fill and rim lights, how much the environment reflects, exposure) and the
//     colour grade (lift, gamma, gain, saturation, contrast, vignette), applied by the Grade pass.
//   ATMOS: each route's fog, sky gradient, sun and sky-light colours.
//   EnvMaps: an environment per route for image-based lighting: the route's sky gradient with a bright key
//     panel where the sun is, a cool fill panel opposite, and strips of the route's neon. Unity takes it as a
//     reflection cubemap (blurred per mip, so rough surfaces get a soft reflection and glossy ones a sharp one)
//     and as the ambient light's spherical harmonics. It is computed directly rather than rendered.
//   Outlines: the inverted-hull outline shells round characters (Outline.shader).
//   Surfaces: the procedural texture sets for the level (colour detail, relief and roughness), drawn once with
//     Paint, and the world-projected UVs that tile them evenly over baked level geometry.
using System;
using System.Collections.Generic;
using NovaStriker.Game.Three;
using UnityEngine;
using UnityEngine.Rendering;

namespace NovaStriker.Game
{
    public sealed class LookDef
    {
        public float key, fill, rim, env, exposure, sat, contrast, vignette;
        public uint rimColor, keyPanel, fillPanel;
        public Vector3 lift, gamma, gain;
        public uint[] neon;
    }
    public sealed class AtmosDef { public uint fog, top, mid, bot, sun, hemi; public float near, far; }

    public static class Look
    {
        public static readonly Dictionary<string, LookDef> LOOK = new Dictionary<string, LookDef>
        {
            ["skyport"] = new LookDef
            {
                key = 2.3f, fill = 0.95f, rim = 1.6f, rimColor = 0xbfe6ff, env = 0.8f, exposure = 0.98f,
                lift = new Vector3(0.0f, 0.01f, 0.025f), gamma = new Vector3(1.0f, 1.0f, 0.98f), gain = new Vector3(1.02f, 1.0f, 0.98f), sat = 1.08f, contrast = 1.06f, vignette = 0.28f,
                neon = new uint[] { 0x5fd8ff, 0xffffff }, keyPanel = 0xfff1dc, fillPanel = 0x9fcfff,
            },
            ["foundry"] = new LookDef
            {
                key = 2.4f, fill = 0.85f, rim = 1.9f, rimColor = 0x8fb8ff, env = 0.75f, exposure = 0.95f,
                lift = new Vector3(0.01f, 0.0f, 0.0f), gamma = new Vector3(0.99f, 1.0f, 1.02f), gain = new Vector3(1.03f, 1.0f, 0.97f), sat = 1.06f, contrast = 1.1f, vignette = 0.34f,
                neon = new uint[] { 0xff8a2a, 0x3fc8ff }, keyPanel = 0xffc690, fillPanel = 0x5f86d0,
            },
            ["undercity"] = new LookDef
            {
                key = 2.1f, fill = 0.8f, rim = 2.2f, rimColor = 0xff5fd2, env = 0.75f, exposure = 0.95f,
                lift = new Vector3(0.005f, 0.0f, 0.02f), gamma = new Vector3(1.0f, 1.01f, 0.99f), gain = new Vector3(1.0f, 0.98f, 1.03f), sat = 1.06f, contrast = 1.1f, vignette = 0.4f,
                neon = new uint[] { 0xff3fb4, 0x4fe0ff, 0xb070ff }, keyPanel = 0xffb7c9, fillPanel = 0x6f7dff,
            },
        };
        public static readonly Dictionary<string, AtmosDef> ATMOS = new Dictionary<string, AtmosDef>
        {
            ["skyport"] = new AtmosDef { fog = 0xc6e2f4, near = 70, far = 260, top = 0x2b7fd3, mid = 0x7fbfee, bot = 0xd9eefa, sun = 0xffeed6, hemi = 0xd8ecff },
            ["foundry"] = new AtmosDef { fog = 0xe6c3a0, near = 50, far = 210, top = 0x3a4f78, mid = 0xd99a6a, bot = 0xf2c79a, sun = 0xffc690, hemi = 0xffe0c2 },
            ["undercity"] = new AtmosDef { fog = 0x8e8fb8, near = 45, far = 200, top = 0x1c2147, mid = 0x6d5f9e, bot = 0xd6a0b8, sun = 0xffb7c9, hemi = 0xbfc6ff },
        };

        // three.js colours are linear once made from hex
        public static Color Lin(uint hex) => Th.Hex(hex).linear;

        // ---- Environment maps ----
        public sealed class Env
        {
            public Cubemap cube;
            public SphericalHarmonicsL2 sh;   // the environment's radiance as ambient light (at intensity 1)
        }
        sealed class Panel { public Vector3 pos, n, ax, ay; public float hw, hh; public Color c; }

        public static Dictionary<string, Env> BuildEnvMaps(int size = 64)
        {
            var out_ = new Dictionary<string, Env>();
            foreach (var kv in LOOK)
            {
                var A = ATMOS[kv.Key]; var L = kv.Value;
                Color grey = Lin(0xe4e8ee);
                Color Soft(uint hex) => Color.Lerp(Lin(hex), grey, 0.45f) * 1.25f;
                Color top = Soft(A.top), mid = Soft(A.mid), bot = Soft(A.bot);
                var panels = new List<Panel>();
                void AddPanel(uint color, float intensity, float w, float h, Vector3 pos, float tint = 1)
                {
                    var c = Color.Lerp(Color.white, Lin(color), tint) * intensity;
                    // (a plane turned to face the origin, as Object3D.lookAt leaves it)
                    var n = (-pos).normalized;
                    var ax = Vector3.Cross(Vector3.up, n); ax = ax.sqrMagnitude < 1e-8f ? Vector3.right : ax.normalized;
                    var ay = Vector3.Cross(n, ax);
                    panels.Add(new Panel { pos = pos, n = n, ax = ax, ay = ay, hw = w / 2, hh = h / 2, c = c });
                }
                AddPanel(L.keyPanel, 4.5f, 26, 18, new Vector3(-22, 30, 26), 0.35f);
                AddPanel(L.fillPanel, 1.4f, 30, 14, new Vector3(30, 8, -18), 0.35f);
                for (int i = 0; i < L.neon.Length; i++)
                    for (int k = 0; k < 4; k++)
                    {
                        float a = (k / 4f + i * 0.13f) * Mathf.PI * 2;
                        AddPanel(L.neon[i], 3, 1.2f, 9 + (k % 2) * 6, new Vector3(Mathf.Cos(a) * 40, 4 + (k % 2) * 6, Mathf.Sin(a) * 40));
                    }
                // The radiance seen from the centre in a direction (three.js space)
                Color Radiance(Vector3 d)
                {
                    float h = d.y;
                    Color c = h > 0 ? Color.Lerp(mid, top, Mathf.SmoothStep(0, 1, Mathf.Clamp01(h / 0.7f)))
                        : Color.Lerp(mid, bot * 0.6f, Mathf.SmoothStep(0, 1, Mathf.Clamp01(-h / 0.6f)));
                    float best = 50;   // (the sky sphere's radius: nearer panels cover it)
                    foreach (var P in panels)
                    {
                        float dn = Vector3.Dot(d, P.n);
                        if (dn >= -1e-4f) continue;
                        float t = Vector3.Dot(P.pos, P.n) / dn;
                        if (t <= 0 || t >= best) continue;
                        var q = d * t - P.pos;
                        if (Mathf.Abs(Vector3.Dot(q, P.ax)) <= P.hw && Mathf.Abs(Vector3.Dot(q, P.ay)) <= P.hh) { best = t; c = P.c; }
                    }
                    return c;
                }

                var env = new Env();
                int mips = 1; for (int s = size; s > 1; s >>= 1) mips++;
                env.cube = new Cubemap(size, TextureFormat.RGBAHalf, true) { name = "env-" + kv.Key, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
                var rng = new System.Random(7);
                // a fixed set of jitter directions on the unit disc for the blurred mips
                const int NS = 40;
                var jit = new Vector2[NS];
                for (int i = 0; i < NS; i++) { float r = Mathf.Sqrt((i + 0.5f) / NS), a = i * 2.39996323f; jit[i] = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r); }
                for (int mip = 0; mip < mips; mip++)
                {
                    int n = Mathf.Max(1, size >> mip);
                    // the cone each mip stands for (Unity picks mip = roughness * (1.7 - 0.7 roughness) * 6)
                    float lod = mip / 6f, rough = (1.7f - Mathf.Sqrt(Mathf.Max(0, 1.7f * 1.7f - 2.8f * lod))) / 1.4f;
                    float cone = mip == 0 ? 0 : Mathf.Clamp(rough * rough * 1.6f + 0.6f * mip / (float)mips, 0.02f, 1.5f);
                    for (int f = 0; f < 6; f++)
                    {
                        var px = new Color[n * n];
                        for (int y = 0; y < n; y++)
                            for (int x = 0; x < n; x++)
                            {
                                float u = (x + 0.5f) / n * 2 - 1, v = (y + 0.5f) / n * 2 - 1;
                                var dU = FaceDir((CubemapFace)f, u, v).normalized;
                                var d3 = new Vector3(dU.x, dU.y, -dU.z);   // Unity -> three.js space
                                Color acc;
                                if (cone <= 0) acc = Radiance(d3);
                                else
                                {
                                    var t1 = Vector3.Cross(Mathf.Abs(d3.y) < 0.99f ? Vector3.up : Vector3.right, d3).normalized;
                                    var t2 = Vector3.Cross(d3, t1);
                                    acc = Color.black;
                                    for (int i = 0; i < NS; i++) acc += Radiance((d3 + (t1 * jit[i].x + t2 * jit[i].y) * cone).normalized);
                                    acc /= NS;
                                }
                                acc.a = 1;
                                px[y * n + x] = acc;
                            }
                        env.cube.SetPixels(px, (CubemapFace)f, mip);
                    }
                }
                env.cube.Apply(false, true);

                // Ambient: the environment's radiance projected onto spherical harmonics (Unity evaluates them as
                // the light a surface facing that way receives, as three.js does from the environment map)
                var sh = new SphericalHarmonicsL2();
                const int NA = 384;
                for (int i = 0; i < NA; i++)
                {
                    float yy = 1 - (i + 0.5f) / NA * 2, r = Mathf.Sqrt(1 - yy * yy), a = i * 2.39996323f;
                    var d3 = new Vector3(Mathf.Cos(a) * r, yy, Mathf.Sin(a) * r);
                    sh.AddDirectionalLight(new Vector3(d3.x, d3.y, -d3.z), Radiance(d3), 4f / NA);
                }
                env.sh = sh;
                out_[kv.Key] = env;
            }
            return out_;
        }
        // The direction through texel (u, v) of a cube face (Unity's layout: rows run top to bottom)
        static Vector3 FaceDir(CubemapFace f, float u, float v)
        {
            switch (f)
            {
                case CubemapFace.PositiveX: return new Vector3(1, -v, -u);
                case CubemapFace.NegativeX: return new Vector3(-1, -v, u);
                case CubemapFace.PositiveY: return new Vector3(u, 1, v);
                case CubemapFace.NegativeY: return new Vector3(u, -1, -v);
                case CubemapFace.PositiveZ: return new Vector3(u, -v, 1);
                default: return new Vector3(-u, -v, -1);
            }
        }
        // The hemisphere light's ambient (three.js HemisphereLight: sky colour above, ground below), as SH
        static SphericalHarmonicsL2? upSH, downSH;
        public static SphericalHarmonicsL2 Hemi(Color skyLin, Color groundLin, float intensity)
        {
            if (upSH == null)
            {
                SphericalHarmonicsL2 up = new SphericalHarmonicsL2(), down = new SphericalHarmonicsL2();
                const int N = 256;
                for (int i = 0; i < N; i++)
                {
                    float yy = 1 - (i + 0.5f) / N * 2, r = Mathf.Sqrt(1 - yy * yy), a = i * 2.39996323f;
                    var d = new Vector3(Mathf.Cos(a) * r, yy, Mathf.Sin(a) * r);
                    if (yy > 0) up.AddDirectionalLight(d, Color.white, 4f / N); else down.AddDirectionalLight(d, Color.white, 4f / N);
                }
                upSH = up; downSH = down;
            }
            // (three.js's hemisphere light is irradiance; as radiance it is its colour / pi)
            return Tint(upSH.Value, skyLin * (intensity / Mathf.PI)) + Tint(downSH.Value, groundLin * (intensity / Mathf.PI));
        }
        public static SphericalHarmonicsL2 Tint(SphericalHarmonicsL2 s, Color c)
        {
            var o = s;
            for (int k = 0; k < 9; k++) { o[0, k] = s[0, k] * c.r; o[1, k] = s[1, k] * c.g; o[2, k] = s[2, k] * c.b; }
            return o;
        }

        // ---- Outlines ----
        static readonly Dictionary<string, TMat> outlineMats = new Dictionary<string, TMat>();
        public static TMat OutlineMaterial(uint color = 0x0b0f18, float width = 0.022f)
        {
            string key = color + ":" + width.ToString("F5");
            if (outlineMats.TryGetValue(key, out var m)) return m;
            var mat = new Material(Templates.Outline);
            mat.SetColor("_BaseColor", Th.Hex(color)); mat.SetFloat("_Width", width);
            m = TMat.Raw(mat); m.userData["sharedOutline"] = true;
            outlineMats[key] = m;
            return m;
        }
        // Outlines on or off everywhere at once (off on Low quality)
        public static void ShowOutlines(bool on) { foreach (var m in outlineMats.Values) m.visible = on; }
        // Gives every visible, shadow-casting mesh under root an outline shell (skips glows, sprites and see-through
        // parts); returns the shells
        public static List<TMesh> AddOutlines(TObj root, uint color, float width)
        {
            var mat = OutlineMaterial(color, width); var add = new List<TMesh>();
            root.traverse(o =>
            {
                if (!(o is TMesh me) || o.outline || o.noOutline || !o.castShadow) return;
                var m = me.material;
                if (m == null || m.transparent || m.emissiveIntensity > 1 || m.kind == TMat.Kind.Basic || m.kind == TMat.Kind.Sprite) return;
                add.Add(me);
            });
            var shells = new List<TMesh>();
            foreach (var o in add)
            {
                var shell = new TMesh(o.geometry, mat) { outline = true }; shell.go.name = "outline"; shell.cast = false; shell.receive = false;
                o.add(shell); shells.Add(shell);
            }
            return shells;
        }

        // ---- Surface detail ----
        static long seed = 777;
        static float Rnd() { seed = seed * 16807 % 2147483647; return seed / 2147483647f; }
        public sealed class SurfaceSet { public Texture2D map, normalMap; public float[] rough; public int n; public readonly Dictionary<string, Texture2D> gloss = new Dictionary<string, Texture2D>(); }
        static readonly Dictionary<string, SurfaceSet> sets = new Dictionary<string, SurfaceSet>();
        static readonly List<(TMat m, string kind, float rough, float metal)> surfaced = new List<(TMat, string, float, float)>();

        static Paint HeightToNormal(Paint h, int n, float strength)
        {
            var o = new Paint(n, n);
            float H(int x, int y) => h.Get(((x + n) % n), ((y + n) % n)).r;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (H(x + 1, y) - H(x - 1, y)) * strength, dy = (H(x, y + 1) - H(x, y - 1)) * strength;
                    float l = Mathf.Sqrt(dx * dx + dy * dy + 1);
                    o.Set(x, y, new Color(-dx / l * 0.5f + 0.5f, dy / l * 0.5f + 0.5f, 1 / l * 0.5f + 0.5f, 1));
                }
            return o;
        }
        static void Grain(Paint g, int n, int count, float alpha, bool light)
        {
            for (int i = 0; i < count; i++)
            {
                float v = light ? 200 + Rnd() * 55 : Rnd() * 70;
                g.fillStyle = Rgba(v, v, v, alpha * Rnd());
                g.fillRect(Rnd() * n, Rnd() * n, 1 + Rnd() * 3, 1 + Rnd() * 3);
            }
        }
        static string Rgba(float r, float g, float b, float a) =>
            string.Format(System.Globalization.CultureInfo.InvariantCulture, "rgba({0},{1},{2},{3})", r, g, b, a);
        static string Rgb(float r, float g, float b) => Rgba(r, g, b, 1);

        // kind: "panel" (riveted wall plating), "floor" (deck plates with a grip pattern), "concrete" (poured, worn)
        static SurfaceSet DrawSurface(string kind, int n)
        {
            Paint hg = new Paint(n, n), dg = new Paint(n, n), rg = new Paint(n, n);
            hg.fillStyle = "#808080"; hg.fillRect(0, 0, n, n);
            dg.fillStyle = "#ffffff"; dg.fillRect(0, 0, n, n);
            rg.fillStyle = "#909090"; rg.fillRect(0, 0, n, n);
            int cells = 2; float cs = n / (float)cells;
            for (int cy = 0; cy < cells; cy++)
                for (int cx = 0; cx < cells; cx++)
                {
                    float x0 = cx * cs, y0 = cy * cs, tone = 236 + Rnd() * 19;
                    dg.fillStyle = Rgb(tone, tone, tone); dg.fillRect(x0 + 2, y0 + 2, cs - 4, cs - 4);
                    if (kind != "concrete")
                    {
                        hg.fillStyle = "#9a9a9a"; hg.fillRect(x0 + 6, y0 + 6, cs - 12, cs - 12);
                        hg.fillStyle = "#8d8d8d"; hg.fillRect(x0 + 10, y0 + 10, cs - 20, cs - 20);
                        hg.fillStyle = "#3a3a3a"; hg.fillRect(x0, y0, cs, 3); hg.fillRect(x0, y0, 3, cs);
                        dg.fillStyle = "rgba(40,46,56,0.55)"; dg.fillRect(x0, y0, cs, 3); dg.fillRect(x0, y0, 3, cs);
                        foreach (var (bx, by) in new[] { (14f, 14f), (cs - 14, 14f), (14f, cs - 14), (cs - 14, cs - 14) })
                        {
                            var grd = hg.createRadialGradient(x0 + bx, y0 + by, 0, x0 + bx, y0 + by, 5);
                            grd.addColorStop(0, "#e0e0e0"); grd.addColorStop(1, "#8d8d8d"); hg.fillStyle = grd; hg.beginPath(); hg.arc(x0 + bx, y0 + by, 5, 0, 7); hg.fill();
                            dg.fillStyle = "rgba(90,96,110,0.6)"; dg.beginPath(); dg.arc(x0 + bx, y0 + by, 3.5f, 0, 7); dg.fill();
                            rg.fillStyle = "#505050"; rg.beginPath(); rg.arc(x0 + bx, y0 + by, 4, 0, 7); rg.fill();
                        }
                        if (kind == "floor")
                        {
                            hg.fillStyle = "rgba(200,200,200,0.35)";
                            for (float yy = y0 + 22; yy < y0 + cs - 22; yy += 12)
                                for (float xx = x0 + 22 + ((yy / 12) % 2) * 6; xx < x0 + cs - 22; xx += 12)
                                {
                                    hg.save(); hg.translate(xx, yy); hg.rotate(Mathf.PI / 4); hg.fillRect(-3.5f, -1.2f, 7, 2.4f); hg.restore();
                                }
                        }
                        else if (Rnd() < 0.5f)
                        {
                            float vx = x0 + cs * 0.3f, vy = y0 + cs * 0.62f;
                            for (int k = 0; k < 5; k++) { hg.fillStyle = "#4a4a4a"; hg.fillRect(vx, vy + k * 7, cs * 0.4f, 3); dg.fillStyle = "rgba(30,34,42,0.5)"; dg.fillRect(vx, vy + k * 7, cs * 0.4f, 3); }
                        }
                    }
                    else
                    {
                        hg.fillStyle = "#6a6a6a"; hg.fillRect(x0, y0, cs, 2); hg.fillRect(x0, y0, 2, cs);
                        dg.fillStyle = "rgba(60,60,70,0.35)"; dg.fillRect(x0, y0, cs, 2); dg.fillRect(x0, y0, 2, cs);
                    }
                }
            for (int i = 0; i < (kind == "concrete" ? 26 : 14); i++)
            {
                float x = Rnd() * n, y = Rnd() * n, rad = 20 + Rnd() * 70;
                var grd = dg.createRadialGradient(x, y, 0, x, y, rad);
                grd.addColorStop(0, Rgba(70, 74, 84, 0.08f + Rnd() * 0.1f)); grd.addColorStop(1, "rgba(70,74,84,0)"); dg.fillStyle = grd; dg.fillRect(x - rad, y - rad, rad * 2, rad * 2);
                var rr = rg.createRadialGradient(x, y, 0, x, y, rad); rr.addColorStop(0, "rgba(220,220,220,0.4)"); rr.addColorStop(1, "rgba(220,220,220,0)"); rg.fillStyle = rr; rg.fillRect(x - rad, y - rad, rad * 2, rad * 2);
            }
            Grain(hg, n, kind == "concrete" ? 9000 : 3000, 0.25f, Rnd() < 0.5f); Grain(dg, n, 4000, 0.08f, false);
            if (kind == "concrete")
                for (int i = 0; i < 6; i++)
                {
                    hg.strokeStyle = "rgba(60,60,60,0.7)"; hg.lineWidth = 1; hg.beginPath(); float x = Rnd() * n, y = Rnd() * n; hg.moveTo(x, y);
                    for (int k = 0; k < 8; k++) { x += (Rnd() - 0.5f) * 40; y += (Rnd() - 0.5f) * 40; hg.lineTo(x, y); }
                    hg.stroke();
                }
            var S = new SurfaceSet { n = n, rough = new float[n * n] };
            S.map = dg.ToTexture(true, true);
            S.normalMap = HeightToNormal(hg, n, kind == "concrete" ? 3 : 6).ToTexture(false, true);
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) S.rough[y * n + x] = rg.Get(x, y).r;
            return S;
        }
        public static SurfaceSet Surface(string kind)
        {
            if (!sets.TryGetValue(kind, out var s)) { s = LoadSurface(kind) ?? DrawSurface(kind, 512); sets[kind] = s; }
            return s;
        }
        // A surface set made by Art/Blender/make_textures.py (paint, rubber, tread): Resources/NovaStriker/Env/<kind>,
        // three 512 x 512 planes of bytes (height, then albedo detail, then roughness), top row first. Raw bytes
        // rather than images, so no import setting can turn the normals into colour.
        static SurfaceSet LoadSurface(string kind)
        {
            var src = Resources.Load<TextAsset>("NovaStriker/Env/" + kind);
            if (src == null) return null;
            const int n = 512; var b = src.bytes;
            if (b.Length < n * n * 3) return null;
            float H(int x, int y) => b[((y + n) % n) * n + (x + n) % n] / 255f;
            var col = new Color32[n * n]; var nrm = new Color32[n * n]; var S = new SurfaceSet { n = n, rough = new float[n * n] };
            const float strength = 6;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    int row = (n - 1 - y) * n + x;
                    byte a = b[n * n + y * n + x]; col[row] = new Color32(a, a, a, 255);
                    float dx = (H(x + 1, y) - H(x - 1, y)) * strength, dy = (H(x, y + 1) - H(x, y - 1)) * strength, l = Mathf.Sqrt(dx * dx + dy * dy + 1);
                    nrm[row] = new Color32((byte)((-dx / l * 0.5f + 0.5f) * 255), (byte)((dy / l * 0.5f + 0.5f) * 255), (byte)((1 / l * 0.5f + 0.5f) * 255), 255);
                    S.rough[y * n + x] = b[2 * n * n + y * n + x] / 255f;
                }
            S.map = new Texture2D(n, n, TextureFormat.RGBA32, true, false) { wrapMode = TextureWrapMode.Repeat, anisoLevel = 8, name = kind + "-map" };
            S.map.SetPixels32(col); S.map.Apply(true, true);
            S.normalMap = new Texture2D(n, n, TextureFormat.RGBA32, true, true) { wrapMode = TextureWrapMode.Repeat, anisoLevel = 8, name = kind + "-normal" };
            S.normalMap.SetPixels32(nrm); S.normalMap.Apply(true, true);
            return S;
        }
        // The soft round contact shadow (Resources/NovaStriker/Env/shadow: one 512 x 512 plane of alpha)
        static Texture2D shadowTex;
        public static Texture2D ShadowTexture()
        {
            if (shadowTex != null) return shadowTex;
            var src = Resources.Load<TextAsset>("NovaStriker/Env/shadow"); const int n = 512;
            var px = new Color32[n * n];
            if (src != null && src.bytes.Length >= n * n)
                for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) px[(n - 1 - y) * n + x] = new Color32(255, 255, 255, src.bytes[y * n + x]);
            shadowTex = new Texture2D(n, n, TextureFormat.RGBA32, true, false) { wrapMode = TextureWrapMode.Clamp, name = "contact-shadow" };
            shadowTex.SetPixels32(px); shadowTex.Apply(true, true);
            return shadowTex;
        }
        // three.js scales its roughness map by the material's roughness; Unity reads smoothness from the alpha of
        // a metallic/smoothness map, so each (roughness, metalness) gets its own copy
        static Texture2D Gloss(SurfaceSet S, float rough, float metal)
        {
            string key = rough.ToString("F3") + ":" + metal.ToString("F3");
            if (S.gloss.TryGetValue(key, out var t)) return t;
            int n = S.n; var data = new Color32[n * n];
            byte mb = (byte)Mathf.RoundToInt(Mathf.Clamp01(metal) * 255);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    data[(n - 1 - y) * n + x] = new Color32(mb, 0, 0, (byte)Mathf.RoundToInt(Mathf.Clamp01(1 - S.rough[y * n + x] * rough) * 255));
            t = new Texture2D(n, n, TextureFormat.RGBA32, true, true) { wrapMode = TextureWrapMode.Repeat, anisoLevel = 8 };
            t.SetPixels32(data); t.Apply(true, true);
            S.gloss[key] = t;
            return t;
        }
        static bool relief = true;
        // Low quality keeps the colour detail but drops the relief and roughness maps
        public static void SurfaceRelief(bool on)
        {
            relief = on;
            foreach (var (m, kind, rough, metal) in surfaced)
            {
                var S = Surface(kind);
                m.normalMap = on ? S.normalMap : null;
                m.roughnessMap = on ? Gloss(S, rough, metal) : null;
                if (!on) m.roughness = rough;
            }
        }
        // Gives a level material a surface set, tiled every `tile` m in world space
        public static TMat ApplySurface(TMat mat, string kind, float tile = 2.5f, float normal = 0.7f)
        {
            var S = Surface(kind);
            mat.map = S.map;
            if (relief) { mat.normalMap = S.normalMap; mat.roughnessMap = Gloss(S, mat.roughness, mat.metalness); }
            mat.normalScale = normal;
            surfaced.Add((mat, kind, mat.roughness, mat.metalness));
            mat.userData["worldUV"] = tile; mat.userData["surfaceKind"] = kind;
            return mat;
        }
        // Box-projected UVs from world positions (for baked geometry, already in world space)
        public static void WorldUVs(Mesh geo, float tile)
        {
            var p = geo.vertices; var nrm = geo.normals; var uv = new Vector2[p.Length];
            for (int i = 0; i < p.Length; i++)
            {
                float ax = Mathf.Abs(nrm[i].x), ay = Mathf.Abs(nrm[i].y), az = Mathf.Abs(nrm[i].z);
                // (positions are in Unity space: three.js's z is -z here)
                float x = p[i].x, y = p[i].y, z = -p[i].z, u, v;
                if (ay >= ax && ay >= az) { u = x; v = z; } else if (ax >= az) { u = z; v = y; } else { u = x; v = y; }
                uv[i] = new Vector2(u / tile, v / tile);
            }
            geo.uv = uv;
            geo.RecalculateTangents();
        }
    }
}
