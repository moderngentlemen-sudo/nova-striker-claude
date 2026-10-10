// The view (render.js): the scene, level geometry on the (curving) gameplay path, the camera, lights and the
// post chain. Presentation only: it reads the simulation and never changes it.
//   Lights: a key (sun, with shadows), a hemisphere fill (as part of the ambient light) and a rim light, at the
//     prototype's intensities (three.js's physically based lights are irradiance; Unity's are radiance, so /pi).
//   Image-based lighting: one environment per route (Look.BuildEnvMaps) as the reflection cubemap and ambient.
//   Post: URP Bloom (in a Volume made here; tone mapping off), then the Grade pass (Grade.shader, drawn by the
//     renderer's Full Screen Pass feature that the editor setup adds): ACES tone mapping and exposure, the route's
//     grade, shockwaves, and the impact frame.
//   Static geometry is merged into one mesh per material and shadow flag (Bake / FlushBaked).
using System.Collections.Generic;
using System.Linq;
using NovaStriker.Game.Three;
using Sprite = NovaStriker.Game.Three.Sprite;
using NovaStriker.Sim;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using static NovaStriker.Sim.Cfg;

namespace NovaStriker.Game
{
    public sealed class View
    {
        const float TURN_TIME = 0.08f;   // s for a character to swing round to face the other way
        // The camera: how far it leads ahead of the team's running (s of their speed, and at most m), and how fast
        // it eases into the lead
        const float LEAD_PER = 0.22f, LEAD_MAX = 2.2f, LEAD_RATE = 1.8f;

        public readonly TObj scene;
        public readonly Camera camera;
        public Vector3 camPos;   // (three.js space)
        public float alpha = 1, time, hitPause;
        public readonly Dictionary<Player, Rig> rigs = new Dictionary<Player, Rig>();
        readonly Dictionary<Enemy, EnemyRig> enemyRigs = new Dictionary<Enemy, EnemyRig>();
        public readonly Fx fx;
        public Breakables breakables;
        public World world;
        public int w = 1280, h = 720;

        double camX = 0, camY = 3, camDist = 16;
        float trauma, bloomKick, punch, impactCd, dim, lead;
        double camTick = -1; Cam camPrev, camCur;

        readonly Light sun, rim;
        Ambience ambience; TMesh sunGlow;
        public Sparks sparks; PlanarReflection reflection;
        public Vfx vfx; PostFx post; LevelFx levelFx;
        readonly List<TMesh> farClouds = new List<TMesh>();
        Color hemiSky, hemiGround = Look.Lin(0x7a6f63);
        float hemiIntensity = 0.95f;
        readonly Dictionary<string, Look.Env> envMaps;
        string envRoute = "skyport";
        bool? lowLook;
        readonly Volume volume; readonly Bloom bloom;
        readonly Material grade;
        readonly TMat skyMat;
        Color skyTop, skyMid, skyBot, fogColor;
        public Color SkyTop => skyTop; public Color SkyBot => skyBot;   // (the clouds' and gulls' ambient light)
        float fogNear, fogFar;

        sealed class LookState { public float key, fill, rim, env, exposure, sat, contrast, vignette; public Color rimColor; public Vector3 lift, gamma, gain; }
        LookState look;

        sealed class Wave { public float u, v, t, dur, k; }
        readonly List<Wave> waves = new List<Wave>();
        readonly Vector4[] waveVec = new Vector4[4];

        sealed class Impact { public float t, dur, @base, cx, cy, k, seed; }
        Impact impact;
        Player impactBy;
        (float t, double x, double y, float k, Player by)? pendingImpact;

        readonly List<(TMesh mesh, string tag)> gateMeshes = new List<(TMesh, string)>();
        readonly List<(TMesh pod, float speed)> pods = new List<(TMesh, float)>();

        public View()
        {
            scene = Group.Make(name: "NovaStriker Scene");
            // The camera: perspective (or orthographic, Settings: Camera); the prototype's near and far planes
            var cgo = new GameObject("Camera");
            camera = cgo.AddComponent<Camera>();
            camera.nearClipPlane = 0.5f; camera.farClipPlane = 600; camera.fieldOfView = (float)SETTINGS.fov;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            camera.allowHDR = true;
            var cd = camera.GetUniversalAdditionalCameraData();
            cd.renderPostProcessing = true; cd.renderShadows = true; cd.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cgo.AddComponent<AudioListener>();

            // Lights. Unity's light colours are sRGB (it linearises them); its intensities are radiance
            sun = MakeLight("Sun", 0xffeed6, 2.1f, true);
            rim = MakeLight("Rim", 0xa9dbff, 1.4f, false);
            SetLightDir(rim, new Vector3(14, 12, -26), Vector3.zero);
            hemiSky = Look.Lin(0xd8ecff);

            // Fog (linear, as three.js's) and the ambient and reflections, set per route in UpdateLook
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
            fogColor = Look.Lin(0xc6e2f4); fogNear = 70; fogFar = 260;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.skybox = null;
            envMaps = Look.BuildEnvMaps();

            // Post: bloom (the prototype's UnrealBloomPass: strength 0.65, radius 0.5, threshold 1.5 linear), no
            // tone mapping here (the Grade pass does three.js's ACES itself)
            var vgo = new GameObject("Post Volume");
            volume = vgo.AddComponent<Volume>(); volume.isGlobal = true; volume.priority = 10;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(Mathf.Pow(1.5f, 1 / 2.2f)); bloom.intensity.Override(0.65f); bloom.scatter.Override(0.7f);
            bloom.highQualityFiltering.Override(true);
            var tm = profile.Add<Tonemapping>(true); tm.mode.Override(TonemappingMode.None);
            volume.sharedProfile = profile;
            grade = Templates.Grade;

            fx = new Fx(scene, rigs) { view = this };
            skyMat = TMat.Raw(new Material(Templates.Sky));
            BuildSky(); BuildBackdrop();
            ambience = new Ambience(this, sun, sunGlow);
            foreach (var c in farClouds) ambience.AddCloud(c);
            BuildLevel(); BuildProps();
            foreach (var zone in new[] { "gym", "arena", "tower", "skyline", "foundry", "undercity" }) ZoneDressing.Build(this, zone);
            Landmarks.Build(this); FlushBaked();
            sparks = new Sparks(scene, camera); reflection = new PlanarReflection(this, camera);
            vfx = new Vfx(this); levelFx = new LevelFx(this);
            post = new PostFx(volume.sharedProfile, bloom, sunGlow.position.v);
            breakables = new Breakables(scene, fx);
            new GameObject("Perf Overlay").AddComponent<PerfOverlay>();
            Resize(Screen.width, Screen.height);
        }

        static Light MakeLight(string name, uint color, float threeIntensity, bool shadows)
        {
            var go = new GameObject(name);
            var l = go.AddComponent<Light>();
            l.type = LightType.Directional; l.color = Th.Hex(color); l.intensity = threeIntensity / Mathf.PI;
            l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            l.shadowBias = 0.05f; l.shadowNormalBias = 0.4f;
            return l;
        }
        // A directional light shining from `from` toward `to` (three.js space)
        static void SetLightDir(Light l, Vector3 from, Vector3 to)
        {
            var d = S.ToUnity(to) - S.ToUnity(from);
            l.transform.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
        }

        public void Resize(int width, int height)
        {
            w = Mathf.Max(1, width); h = Mathf.Max(1, height);
            grade.SetVector("_Res", new Vector4(w, h, 0, 0));
        }

        // ---- Baking: static geometry merged into one mesh per material (and shadow flag) ----
        sealed class BakeSet { public TMat mat; public bool cast; public readonly List<CombineInstance> parts = new List<CombineInstance>(); }
        readonly Dictionary<(TMat, bool), BakeSet> baked = new Dictionary<(TMat, bool), BakeSet>();
        // A piece of static geometry at a three.js position, Euler rotation (XYZ) and scale
        public void Bake(Mesh geo, TMat mat, Vector3 pos, Vector3 rot = default, Vector3? scale = null, bool cast = true)
        {
            var key = (mat, cast);
            if (!baked.TryGetValue(key, out var set)) baked[key] = set = new BakeSet { mat = mat, cast = cast };
            set.parts.Add(new CombineInstance { mesh = geo, transform = Matrix4x4.TRS(Th.P(pos), Th.Quat(rot.x, rot.y, rot.z), scale ?? Vector3.one) });
        }
        public void FlushBaked()
        {
            const float CELL = 48;   // m: baked geometry is merged per material and per cell of the ground plan, so whatever
                                     // is off screen is culled instead of drawing one mesh that spans the whole level
            foreach (var set in baked.Values)
            {
                var cells = new Dictionary<(int, int), List<CombineInstance>>();
                foreach (var ci in set.parts)
                {
                    var p = ci.transform.GetColumn(3);
                    var key = (Mathf.FloorToInt(p.x / CELL), Mathf.FloorToInt(p.z / CELL));
                    if (!cells.TryGetValue(key, out var list)) cells[key] = list = new List<CombineInstance>();
                    list.Add(ci);
                }
                foreach (var list in cells.Values)
                    for (int i = 0; i < list.Count; i += 512)
                    {
                        var merged = new Mesh { name = "baked", indexFormat = IndexFormat.UInt32 };
                        merged.CombineMeshes(list.Skip(i).Take(512).ToArray(), true, true, false);
                        merged.colors = null; merged.uv2 = null;
                        if (set.mat.userData.TryGetValue("worldUV", out var tile)) Look.WorldUVs(merged, (float)tile);   // (surface detail tiles in world space)
                        else merged.RecalculateTangents();
                        merged.RecalculateBounds();
                        merged.Optimize();               // (vertex cache order)
                        merged.UploadMeshData(true);     // (the GPU keeps it; no copy stays in memory)
                        var m = new TMesh(merged, set.mat) { cast = set.cast, receive = true };
                        m.go.isStatic = true;
                        scene.add(m);
                    }
            }
            baked.Clear();
        }

        // ---- Environment ----
        void BuildSky()
        {
            var sky = new TMesh(Geo.Sphere(420, 32, 16), skyMat) { cast = false, receive = false, noOutline = true };
            scene.add(sky);
            skyTop = Look.Lin(0x2b7fd3); skyMid = Look.Lin(0x7fbfee); skyBot = Look.Lin(0xd9eefa);
            var glowMat = TMat.Sprite(fx.tex.glow, 0xfff3d6); glowMat.depthWrite = false; glowMat.fog = false;
            sunGlow = Sprite.Make(glowMat); sunGlow.scale.set(90, 90, 1); sunGlow.position.set(-160, 130, -300); scene.add(sunGlow);
        }

        void BuildBackdrop()
        {
            var cloudMat = TMat.Sprite(fx.tex.glow, 0xeef5fa); cloudMat.opacity = 0.5f; cloudMat.depthWrite = false;
            for (int i = 0; i < 44; i++)
            {
                var s = Sprite.Make(cloudMat);
                float a = S.Rnd() * Mathf.PI * 2, r = 170 + S.Rnd() * 160;
                s.position.set(60 + Mathf.Cos(a) * r, -12 + S.Rnd() * 28, -40 + Mathf.Sin(a) * r * 0.8f);
                float k = 30 + S.Rnd() * 60; s.scale.set(k * 1.8f, k, 1); scene.add(s); farClouds.Add(s);
            }
            // Cloud sea below Skyport
            var sea = new TMesh(Geo.Plane(1200, 1200), TMat.Std(0xeef6fb, 1)) { receive = true };
            sea.rotation.x = -Mathf.PI / 2; sea.position.y = -34; scene.add(sea);
            // Distant spires
            var spireMat = TMat.Std(0xc3d7ea, 0.55f);
            var glowMat = TMat.Std(0x7fe3ff); glowMat.emissiveHex = 0x5fd8ff; glowMat.emissiveIntensity = 1.4f;
            var spots = new (float x, float z)[] { (-30, -120), (20, -150), (55, -105), (85, -175), (130, -135), (-70, -170), (175, -110), (215, -160), (250, -95), (10, -210), (110, -220), (290, -150) };
            foreach (var (x, z) in spots)
            {
                float hh = 45 + S.Rnd() * 65, ww = 4 + S.Rnd() * 6;
                Bake(Geo.RoundedBox(ww, hh, ww, 3, 1.5f), spireMat, new Vector3(x, hh / 2 - 34, z), cast: false);
                Bake(Geo.Box(ww * 1.02f, 0.5f, ww * 1.02f), glowMat, new Vector3(x, hh - 38, z), cast: false);
                Bake(Geo.Cylinder(ww * 1.3f, ww * 1.3f, 1.2f, 24), spireMat, new Vector3(x, hh * 0.6f - 34, z), cast: false);
            }
            // Transit rails with moving pods (the civilization's advanced transit)
            var railMat = TMat.Std(0xbfd0df, 0.4f);
            var podMat = TMat.Std(0xffffff, 0.3f); podMat.emissiveHex = 0x5fd8ff; podMat.emissiveIntensity = 0.25f;
            foreach (var (y, z, speed) in new (float, float, float)[] { (22, -70, 14), (30, -92, -10), (16, -115, 18) })
            {
                Bake(Geo.Cylinder(0.25f, 0.25f, 600, 8), railMat, new Vector3(60, y, z), new Vector3(0, 0, Mathf.PI / 2), cast: false);
                for (int i = 0; i < 3; i++)
                {
                    var pod = new TMesh(Geo.Capsule(1.2f, 6, 4, 12), podMat); pod.rotation.z = Mathf.PI / 2;
                    pod.position.set(-200 + i * 160, y - 1.4f, z); scene.add(pod); pods.Add((pod, speed));
                }
            }
            // The Storm Spire tower the path wraps around (CP-08 test)
            var towerMat = TMat.Std(0xeef3f8, 0.4f);
            float R = (float)Level.ARC_R, tx = (float)Level.TOWER_CENTER_X, tz = (float)Level.TOWER_CENTER_Z;
            Bake(Geo.Cylinder(R - 3.4f, R - 2.4f, 110, 48), towerMat, new Vector3(tx, 20, tz), cast: false);
            for (int i = 0; i < 12; i++)
            {
                float a = i / 12f * Mathf.PI * 2;
                Bake(Geo.Box(0.3f, 90, 0.3f), glowMat, new Vector3(tx + Mathf.Sin(a) * (R - 3.3f), 25, tz + Mathf.Cos(a) * (R - 3.3f)), cast: false);
            }
            foreach (var y in new float[] { -4, 18, 40, 62 })
                Bake(Geo.Torus(R - 2.8f, 0.35f, 8, 48), glowMat, new Vector3(tx, y, tz), new Vector3(Mathf.PI / 2, 0, 0), cast: false);
        }

        // How deep (toward the camera and away) a level box is drawn
        public static float DepthFor(LevelBox b) => b.type == 'o' ? 2.6f : (b.tag == "panel" || b.tag == "column" || b.tag == "pillar") ? 1.8f : b.type == 'g' ? 3.2f : 4.4f;
        void BuildLevel()
        {
            var cap = TMat.Std(0xd9dfe7, 0.5f); var body = TMat.Std(0x5f7897, 0.5f); var dark = TMat.Std(0x46596f, 0.55f);   // (glossy: the sky city's sheen)
            var trim = TMat.Std(0x7fe3ff); trim.emissiveHex = 0x4fd6ff; trim.emissiveIntensity = 2.0f;
            var gate = TMat.Std(0xff2e7e); gate.emissiveHex = 0xff2e7e; gate.emissiveIntensity = 1.6f; gate.transparent = true; gate.opacity = 0.45f; gate.depthWrite = false;
            // Surface detail (Look): riveted plating on the walls, grip deck on the tops, plating on the platforms
            Look.ApplySurface(body, "panel", 3); Look.ApplySurface(dark, "panel", 2.5f); Look.ApplySurface(cap, "floor", 2.2f, 0.5f);
            foreach (var b in Level.BOXES)
            {
                if (b.type == 'd') continue;   // (breakable pieces have their own meshes: Breakables)
                if (b.id >= Level.EXTRA_ID) continue;   // (the level features' boxes come and go: LevelFx draws them)
                float depth = DepthFor(b), hgt = (float)(b.y1 - b.y0);
                var segs = new List<(double, double)>();
                bool curved = Level.CurvedSpan(b.x0, b.x1);
                if (!curved) segs.Add((b.x0, b.x1));
                else { int n = (int)System.Math.Ceiling((b.x1 - b.x0) / 0.9); for (int i = 0; i < n; i++) segs.Add((b.x0 + (b.x1 - b.x0) * i / n, b.x0 + (b.x1 - b.x0) * (i + 1) / n)); }
                foreach (var (x0, x1) in segs)
                {
                    double xm = (x0 + x1) / 2; float wd = (float)(x1 - x0) * (curved ? 1.04f : 1), yaw = S.YawAt(xm);
                    void Place(Mesh geo, TMat mat, double y, float dz = 0, bool cast = true) => Bake(geo, mat, S.W(xm, y, dz), new Vector3(0, yaw, 0), null, cast);
                    if (b.type == 'g')
                    {
                        var g = new TMesh(Geo.Box(wd, hgt, depth), gate) { cast = false };
                        g.position.copy(S.W(xm, b.y0 + hgt / 2)); g.rotation.y = yaw; scene.add(g);
                        gateMeshes.Add((g, b.tag));
                        continue;
                    }
                    if (b.tag == "bound") { Place(Geo.Box(wd, hgt, depth), dark, b.y0 + hgt / 2); continue; }
                    float capH = Mathf.Min(0.22f, hgt * 0.4f);
                    var bodyGeo = curved || b.type == 'o' ? Geo.Box(wd, hgt - capH, depth) : Geo.RoundedBox(wd, hgt - capH, depth, 2, 0.12f);
                    Place(bodyGeo, b.type == 'o' ? dark : body, b.y0 + (hgt - capH) / 2, 0, b.type != 's' || hgt < 10);
                    var capGeo = Geo.Copy(curved ? Geo.Box(wd, capH, depth + 0.1f) : Geo.RoundedBox(wd + 0.08f, capH, depth + 0.1f, 2, 0.06f));
                    var uv = capGeo.uv;
                    for (int i = 0; i < uv.Length; i++) uv[i] = new Vector2(uv[i].x * Mathf.Max(1, wd / 2), uv[i].y * Mathf.Max(1, (depth + 0.1f) / 2));
                    capGeo.uv = uv;
                    Place(capGeo, cap, b.y1 - capH / 2);
                    if (b.tag != "tunnel" && b.tag != "panel") Place(Geo.Box(wd, 0.06f, 0.06f), trim, b.y1 - capH - 0.05f, depth / 2 + 0.02f);
                    if (b.type == 'o') Place(Geo.Box(wd * 0.9f, 0.05f, depth * 0.8f), trim, b.y0 - 0.01f);
                }
            }
        }

        void BuildProps()
        {
            var white = TMat.Std(0xf1f4f7, 0.5f); var navy = TMat.Std(0x2b4f7e, 0.6f);
            var leafA = TMat.Std(0x5fb36a, 0.9f); var leafB = TMat.Std(0x3f8f58, 0.9f);
            var lamp = TMat.Std(0x9ff0ff); lamp.emissiveHex = 0x5fe0ff; lamp.emissiveIntensity = 2.2f;
            var cloth = new[] { 0x2fb5c9u, 0x2b5d9bu, 0xf1f4f7u }.Select(c => { var m = TMat.Std(c, 0.8f); m.side = Side.Double; return m; }).ToArray();
            void Add(Mesh geo, TMat mat, float x, float y, float z, bool cast = true, Vector3? scale = null) => Bake(geo, mat, new Vector3(x, y, z), default, scale, cast);
            var potGeo = Geo.RoundedBox(1.4f, 0.9f, 1.4f, 2, 0.2f); var leafGeo = Geo.Icosahedron(0.75f, 1);
            // A lower back terrace holds the props so they sit below and behind character silhouettes
            const float Y = -1.1f;
            Add(Geo.Box(108, 5, 8), TMat.Std(0x55708f, 0.75f), 43.5f, Y - 2.5f, -6.4f, false);
            Add(Geo.Box(108, 0.2f, 8.1f), TMat.Std(0xc9d2dd, 0.85f), 43.5f, Y - 0.1f, -6.4f, false);
            for (int x = -4; x < 96; x += 11)
            {
                const float z = -5.2f;
                Add(potGeo, white, x, Y + 0.45f, z);
                Add(leafGeo, leafA, x - 0.2f, Y + 1.25f, z, true, new Vector3(1, 0.8f, 1));
                Add(leafGeo, leafB, x + 0.35f, Y + 1.05f, z + 0.2f, true, new Vector3(0.7f, 0.7f, 0.7f));
                Add(Geo.Cylinder(0.07f, 0.09f, 5.5f, 8), navy, x + 5, Y + 2.75f, z - 2, false);
                Add(Geo.Sphere(0.22f, 12, 10), lamp, x + 5, Y + 5.6f, z - 2, false);
                // ((x / 11 + 10) % 3 | 0, with x / 11 fractional for x = -4)
                ambience.Banner(cloth[(int)((x / 11f + 10) % 3)], x + 5.07f, Y + 3.9f, z - 2, 1.1f, 2.6f);   // (it ripples: Ambience)
            }
            // Flags on tall poles at the back of the gym's terrace, streaming in the breeze
            foreach (var (fx, k) in new[] { (6f, 0), (30f, 1), (52f, 2) })
            {
                Add(Geo.Cylinder(0.06f, 0.08f, 8.2f, 10), white, fx, Y + 4.1f, -9.8f);
                Add(Geo.Sphere(0.12f, 10, 8), lamp, fx, Y + 8.25f, -9.8f, false);
                ambience.Banner(cloth[k], fx + 0.06f, Y + 7.3f, -9.8f, 2.4f, 1.4f, 1.6f);
            }
            var archGeo = Geo.Torus(9, 0.55f, 10, 40, Mathf.PI);
            foreach (var x in new float[] { 8, 48, 88 }) Add(archGeo, white, x, -1, -16, false);

            // Skyline Relay dressing: antenna masts behind the rooftops, and the relay beacon at the route's end
            foreach (var (x, y, hh) in new (float, float, float)[] { (195, 15.6f, 6), (212, 15.6f, 8), (226, 12.6f, 5), (252, 18.6f, 6), (262, 18.6f, 7), (294, 18.6f, 7) })
            {
                var b = S.W(x, y, -2.9);
                Add(Geo.Cylinder(0.1f, 0.15f, hh, 10), navy, b.x, y + hh / 2, b.z, false);
                Add(Geo.Sphere(0.24f, 12, 10), lamp, b.x, y + hh + 0.15f, b.z, false);
            }
            var bc = S.W(310, 18.6, -1.2);
            Add(Geo.Cylinder(0.45f, 0.85f, 14, 16), white, bc.x, 18.6f + 7, bc.z);
            foreach (var k in new[] { 0.3f, 0.55f, 0.8f })
                Bake(Geo.Torus(1.45f - k * 0.6f, 0.12f, 8, 32), lamp, new Vector3(bc.x, 18.6f + 14 * k, bc.z), new Vector3(Mathf.PI / 2, 0, 0), null, false);
            var bm = TMat.Sprite(fx.tex.glow, 0x9ff0ff); bm.depthWrite = false;
            var beacon = Sprite.Make(bm); beacon.scale.set(9, 9, 1); beacon.position.set(bc.x, 18.6f + 14.6f, bc.z); scene.add(beacon);
        }

        // ---- Entities ----
        static void DisposeRig(TObj root) => root.destroy();

        void SyncEntities(World world, float a, float dt)
        {
            float t = time;
            var seen = new HashSet<Player>();
            foreach (var p in world.players)
            {
                seen.Add(p);
                rigs.TryGetValue(p, out var rig);
                if (rig == null || rig.@char != p.@char)
                {
                    if (rig != null) { rig.skin?.Dispose(); DisposeRig(rig.root); }
                    rig = Rigs.BuildPlayerRig(p.@char);
                    if (p.@char == "ram") { Look.Wear(rig.mats.@base); Look.Wear(rig.mats.trim); }   // (his battle-worn armour)
                    var ringMat = TMat.Basic(0xffffff); ringMat.colorCss = PLAYER_COLORS[p.slot]; ringMat.transparent = true; ringMat.opacity = 0.65f; ringMat.depthWrite = false;
                    var ring = new TMesh(Geo.Ring(0.42f, 0.55f, 32), ringMat) { noOutline = true }; ring.rotation.x = -Mathf.PI / 2; ring.position.y = 0.03f;
                    ring.scale.setScalar(Mathf.Max(1, (float)CHARS[p.@char].width / 0.72f));   // RAM stands in a wider ring
                    rig.root.add(ring); rig.ring = ring;
                    rig.shells = Look.AddOutlines(rig.root, 0x0b0f18, 0.016f);
                    scene.add(rig.root); rigs[p] = rig;
                }
                double x = p.prevX + (p.x - p.prevX) * a, y = p.prevY + (p.y - p.prevY) * a;
                rig.root.position.copy(S.W(x, y, LevelFeatures.Depth(p)));   // (depth lanes: back, middle or front)
                // Turning round: the rig swings through the turn over TURN_TIME with a twist of the body, instead of
                // snapping to the mirrored pose in one frame
                if (rig.turn == null) rig.turn = (float)p.facing;
                float was = rig.turn.Value, step = dt * 2 / TURN_TIME;
                rig.turn = was + Mathf.Clamp((float)p.facing - was, -step, step);
                float turn = rig.turn.Value, tw = 1 - Mathf.Abs(turn), spin = turn > was ? 1 : turn < was ? -1 : 0;
                rig.root.rotation.y = S.YawAt(x) - spin * tw * 0.9f;
                rig.flip.scale.x = Mathf.Abs(turn) < 0.12f ? (turn != 0 ? Mathf.Sign(turn) : (float)p.facing) * 0.12f : turn;
                Anim.AnimatePlayer(rig, p, dt, t);
                // A 3D model drawn over the rig, where one exists and Settings asks for models (Models)
                bool model = SETTINGS.charModels == "models" && ModelSkin.HasModel(p.@char);
                if (model && rig.skin == null) rig.skin = new ModelSkin(rig, p.@char);
                rig.skin?.Update(p, dt, model && !rig.skin.failed);
                if (rig.helmetKnock) { rig.helmetKnock = false; HelmetFx.Knock(rig, p); }
                // Echo is gone during Thousand Cuts until every cut lands at once; a dodging Nova flickers like a hologram
                bool cutting = p.state == "ult" && p.ultRun != null && p.ultRun.kind == "echo" && p.ultRun.t < p.ultRun.fin;
                bool phasing = p.state == "dodge" && p.dodge != null && p.dodge.t <= 11 && Mathf.FloorToInt(t * 30) % 3 == 0;
                rig.root.visible = p.state != "dead" && !cutting && !phasing && !(p.mercy > 0 && p.state != "downed" && p.state != "ult" && Mathf.FloorToInt(t * 14) % 2 == 0);
                rig.ring.visible = p.state != "downed";
                bool inked = rig.cloak < 0.05f;
                if (rig.inked != inked) { rig.inked = inked; foreach (var s in rig.shells) s.visible = inked; }
            }
            foreach (var p in rigs.Keys.ToList()) if (!seen.Contains(p)) { var rig = rigs[p]; rig.skin?.Dispose(); DisposeRig(rig.root); rigs.Remove(p); }

            var seenE = new HashSet<Enemy>();
            foreach (var e in world.enemies)
            {
                seenE.Add(e);
                if (!enemyRigs.TryGetValue(e, out var R)) { R = EnemyRigs.Build(e.type); Look.AddOutlines(R.root, 0x12060c, 0.018f); scene.add(R.root); enemyRigs[e] = R; }
                double x = e.prevX + (e.x - e.prevX) * a, y = e.prevY + (e.y - e.prevY) * a;
                R.root.position.copy(S.W(x, y, LevelFeatures.Depth(e)));
                R.root.rotation.y = S.YawAt(x);
                EnemyRigs.Animate(R, e, dt, t);
                if (e.tagged > 0 && R.tag == null)
                {
                    var tm = TMat.Sprite(fx.tex.star, 0xffa53a); tm.depthWrite = false;
                    R.tag = Sprite.Make(tm); R.tag.scale.set(0.7f, 0.7f, 1); R.tag.position.set(0, (float)e.h + 0.5f, 0); R.root.add(R.tag);
                }
                if (R.tag != null) R.tag.visible = e.tagged > 0 && !e.dead;
                StunMarker(R, e, t);
            }
            foreach (var e in enemyRigs.Keys.ToList()) if (!seenE.Contains(e)) { DisposeRig(enemyRigs[e].root); enemyRigs.Remove(e); }

            foreach (var (mesh, tag) in gateMeshes)
            {
                mesh.visible = Level.GATES[tag];
                mesh.material.opacity = 0.35f + 0.12f * Mathf.Sin(t * 6);
            }
            foreach (var (pod, speed) in pods)
            {
                pod.position.x += speed * dt;
                if (pod.position.x > 260) pod.position.x = -220; if (pod.position.x < -220) pod.position.x = 260;
            }
        }

        // Stunned (Echo's spin, or a stagger): three stars circle the enemy's head for as long as it lasts, the orbit
        // tightening as the time runs out and the stars fading in the last fifth
        void StunMarker(EnemyRig R, Enemy e, float t)
        {
            bool on = !e.dead && (e.state == "stagger" || (e.state == "hitstun" && e.dizzy)) && e.stun > 0;
            if (!on) { if (R.dizzy != null) R.dizzy.visible = false; return; }
            if (R.dizzy == null)
            {
                R.dizzy = Group.Make(); R.stars = new List<(TMesh, TMesh)>();
                // each star has a dark backing a little larger than itself, so it reads on bright floors and dark ones
                for (int i = 0; i < 3; i++)
                {
                    var bm = TMat.Sprite(fx.tex.star, 0x2a1800); bm.depthWrite = false; bm.renderQueueOffset = 5;
                    var sm = TMat.Sprite(fx.tex.star, 0xffc21a); sm.depthWrite = false; sm.renderQueueOffset = 6;
                    var back = Sprite.Make(bm); var s = Sprite.Make(sm);
                    R.dizzy.add(back); R.dizzy.add(s); R.stars.Add((s, back));
                }
                R.root.add(R.dizzy);
            }
            float left = Mathf.Max(0, 1 - (float)(e.st / e.stun)), r = (0.28f + 0.32f * left) * Mathf.Max(0.8f, (float)e.w), fade = Mathf.Min(1, left * 5);
            R.dizzy.visible = true; R.dizzy.position.set(0, (float)e.h + 0.28f, 0);
            for (int i = 0; i < R.stars.Count; i++)
            {
                var (s, back) = R.stars[i];
                float a = t * 5.5f + i * Mathf.PI * 2 / 3;
                s.position.set(Mathf.Cos(a) * r, Mathf.Sin(a * 2) * 0.05f, Mathf.Sin(a) * r); back.position.copy(s.position.v);
                float k = 0.5f + 0.08f * Mathf.Sin(t * 12 + i); s.scale.set(k, k, 1); back.scale.set(k * 1.35f, k * 1.35f, 1);
                s.material.opacity = fade; back.material.opacity = fade * 0.55f;
            }
        }

        // ---- Camera ----
        void UpdateCamera(World world, float dt)
        {
            // The frame follows the sim's camera target interpolated between ticks (alpha), so on a 120/144 Hz screen it
            // glides instead of stepping 60 times a second; and it leads a little ahead of where the team is running
            if (world.tick != camTick)
            {
                camPrev = camCur ?? Copy(world.cam); camCur = Copy(world.cam); camTick = world.tick;
            }
            var P0 = camPrev ?? world.cam; var P1 = camCur ?? world.cam;
            double Tx = P0.x + (P1.x - P0.x) * alpha, Ty = P0.y + (P1.y - P0.y) * alpha, Td = P0.dist + (P1.dist - P0.dist) * alpha;
            float rate = 5.5f;
            var act = world.players.Where(p => p.state != "dead" && p.state != "downed").ToList();
            if (act.Count > 0)
            {
                double vx = act.Sum(p => p.vx) / act.Count, spread = act.Max(p => p.x) - act.Min(p => p.x);
                float want = Mathf.Clamp((float)vx * LEAD_PER, -LEAD_MAX, LEAD_MAX) * Mathf.Max(0, 1 - (float)spread / 8);
                lead += (want - lead) * (1 - Mathf.Exp(-dt * LEAD_RATE));
                if (world.ultCast == null) Tx += lead;
            }
            // An ultimate's call pushes in on whoever is calling it; while it plays out the frame eases back
            var U = world.ultCast;
            if (U != null && U.members.Count > 0)
            {
                int n = U.members.Count; double mx = U.members.Sum(m => m.x) / n, my = U.members.Sum(m => m.y) / n + 1.1;
                if (U.phase == "cast") { Tx = mx; Ty = my + 0.4; Td = System.Math.Max(8.5, Td * 0.6); rate = 9; }
                else if (U.phase == "run") { Tx = Tx * 0.7 + mx * 0.3; Ty = Ty * 0.7 + my * 0.3; Td *= 1.04; }
            }
            float k = 1 - Mathf.Exp(-dt * rate);
            // Vertical follow speeds up the further behind it falls, so a big launch never leaves the frame
            float ky = 1 - Mathf.Exp(-dt * (rate + Mathf.Max(0, Mathf.Abs((float)(Ty - camY)) - 1.2f) * 5));
            camX += (Tx - camX) * k; camY += (Ty - camY) * ky; camDist += (Td - camDist) * k;
            trauma = Mathf.Max(0, trauma - dt * 1.8f);
            if (world.players.Any(p => p.state == "beam" && p.beam != null)) trauma = Mathf.Max(trauma, 0.22f);   // the beam shakes the frame the whole time
            if (world.players.Any(p => p.state == "ult" && p.ultRun != null && p.ultRun.segs != null)) trauma = Mathf.Max(trauma, 0.4f);   // and Supernova far more
            punch *= Mathf.Exp(-dt * 10); bloomKick = Mathf.Max(0, bloomKick - dt * 3.2f);
            // Each route has its own light (Look.ATMOS): it blends in as the camera arrives
            string route = Level.RouteAt(camX).id;
            var A = Look.ATMOS.TryGetValue(route, out var at) ? at : Look.ATMOS["skyport"];
            float ka = 1 - Mathf.Exp(-dt * 2.5f);
            fogColor = Color.Lerp(fogColor, Look.Lin(A.fog), ka); fogNear += (A.near - fogNear) * ka; fogFar += (A.far - fogFar) * ka;
            skyTop = Color.Lerp(skyTop, Look.Lin(A.top), ka); skyMid = Color.Lerp(skyMid, Look.Lin(A.mid), ka); skyBot = Color.Lerp(skyBot, Look.Lin(A.bot), ka);
            sunLin = Color.Lerp(sunLin, Look.Lin(A.sun), ka); hemiSky = Color.Lerp(hemiSky, Look.Lin(A.hemi), ka);
            RenderSettings.fogColor = fogColor.gamma; RenderSettings.fogStartDistance = fogNear; RenderSettings.fogEndDistance = fogFar;
            var sm = skyMat.m; sm.SetVector("_Top", skyTop); sm.SetVector("_Mid", skyMid); sm.SetVector("_Bot", skyBot);
            sun.color = sunLin.gamma;
            UpdateLook(route, ka);

            var f = Level.Frame(camX);
            var lookAt = new Vector3((float)f.px, (float)camY, (float)f.pz);
            bool ortho = SETTINGS.camera == "ortho";
            float d = (float)camDist;
            var pos = new Vector3((float)(f.px + f.nx * d), (float)camY + d * 0.1f + punch, (float)(f.pz + f.nz * d));
            lookAt.y += punch * 0.6f;   // a blast under a player thumps the whole frame down, then it settles
            if (SETTINGS.shake && trauma > 0)
            {
                float s = trauma * trauma * 0.35f;
                pos.x += (S.Rnd() - 0.5f) * s; pos.y += (S.Rnd() - 0.5f) * s;
            }
            camPos = pos;
            var ct = camera.transform;
            ct.position = S.ToUnity(pos);
            ct.rotation = Quaternion.LookRotation(S.ToUnity(lookAt) - ct.position, Vector3.up);
            camera.orthographic = ortho;
            if (ortho) camera.orthographicSize = d * Mathf.Tan((float)SETTINGS.fov * Mathf.PI / 360);
            else camera.fieldOfView = (float)SETTINGS.fov;
            // Keep the shadow frustum centred on the action
            SetLightDir(sun, new Vector3(lookAt.x - 18, 30 + lookAt.y, lookAt.z + 22), lookAt);
        }
        Color sunLin = Look.Lin(0xffeed6);
        int rendererIndex;
        static Cam Copy(Cam c) => new Cam { x = c.x, y = c.y, dist = c.dist, halfW = c.halfW, halfH = c.halfH };

        // The route's look (Look.LOOK) blends in with its atmosphere: light intensities, the rim light's colour,
        // reflections, exposure and the grade. The environment map itself switches with the route.
        void UpdateLook(string id, float k)
        {
            var L = Look.LOOK.TryGetValue(id, out var l) ? l : Look.LOOK["skyport"];
            if (look == null) look = new LookState
            {
                key = L.key, fill = L.fill, rim = L.rim, env = L.env, exposure = L.exposure, sat = L.sat, contrast = L.contrast, vignette = L.vignette,
                rimColor = Look.Lin(L.rimColor), lift = L.lift, gamma = L.gamma, gain = L.gain,
            };
            var Sx = look;
            float Lr(float cur, float to) => cur + (to - cur) * k;
            Sx.key = Lr(Sx.key, L.key); Sx.fill = Lr(Sx.fill, L.fill); Sx.rim = Lr(Sx.rim, L.rim); Sx.env = Lr(Sx.env, L.env);
            Sx.exposure = Lr(Sx.exposure, L.exposure); Sx.sat = Lr(Sx.sat, L.sat); Sx.contrast = Lr(Sx.contrast, L.contrast); Sx.vignette = Lr(Sx.vignette, L.vignette);
            Sx.rimColor = Color.Lerp(Sx.rimColor, Look.Lin(L.rimColor), k);
            Sx.lift = Vector3.Lerp(Sx.lift, L.lift, k); Sx.gamma = Vector3.Lerp(Sx.gamma, L.gamma, k); Sx.gain = Vector3.Lerp(Sx.gain, L.gain, k);
            if (envRoute != id && envMaps.ContainsKey(id)) envRoute = id;
            sun.intensity = Sx.key / Mathf.PI; hemiIntensity = Sx.fill; rim.intensity = Sx.rim / Mathf.PI; rim.color = Sx.rimColor.gamma;
            // Ambient: the hemisphere light plus the environment's radiance; reflections from the environment
            var env = lowLook == true ? null : envMaps[envRoute];
            var sh = Look.Hemi(hemiSky, hemiGround, hemiIntensity);
            if (env != null) { sh += env.sh * Sx.env; RenderSettings.customReflectionTexture = env.cube; RenderSettings.reflectionIntensity = Sx.env; }
            else RenderSettings.reflectionIntensity = 0;
            RenderSettings.ambientProbe = sh;
            bool low = SETTINGS.quality == "low";
            grade.SetFloat("_Exposure", Sx.exposure);
            // (Low quality draws without the grade, as the prototype does: tone mapping only)
            grade.SetVector("_Lift", low ? Vector3.zero : Sx.lift); grade.SetVector("_Gamma", low ? Vector3.one : Sx.gamma); grade.SetVector("_Gain", low ? Vector3.one : Sx.gain);
            grade.SetFloat("_Sat", low ? 1 : Sx.sat); grade.SetFloat("_Contrast", low ? 1 : Sx.contrast); grade.SetFloat("_Vignette", low ? 0 : Sx.vignette);
        }

        // A blast's kick: a bloom swell (and Phase 4's screen effects), and the flocks scatter
        public void Kick(float k) { bloomKick = Mathf.Max(bloomKick, k); post?.Kick(k); }
        public void Startle(float k) => ambience?.Startle(k);

        // A shockwave: a ring that bends the picture outward from a big hit (Grade.shader, at most four)
        public void Shockwave(double x, double y, float strength = 1, float dur = 0.5f)
        {
            ambience?.Startle(strength);
            if (SETTINGS.quality == "low" || !SETTINGS.shake) return;
            var s = ScreenOf(x, y);
            if (!s.vis) return;
            if (waves.Count >= 4) waves.RemoveAt(0);
            waves.Add(new Wave { u = s.x / Mathf.Max(1, w), v = 1 - s.y / Mathf.Max(1, h), t = 0, dur = dur, k = strength });
        }
        void UpdateWaves(float dt)
        {
            waves.RemoveAll(wv => (wv.t += dt) >= wv.dur);
            for (int i = 0; i < 4; i++)
            {
                if (i >= waves.Count) { waveVec[i] = Vector4.zero; continue; }
                var wv = waves[i]; float f = wv.t / wv.dur;
                waveVec[i] = new Vector4(wv.u, wv.v, 0.04f + f * 0.55f * Mathf.Min(1.4f, wv.k), wv.k * (1 - f) * (1 - f));
            }
            grade.SetVectorArray("_Waves", waveVec);
        }

        public void OnEvent(Ev ev)
        {
            fx.OnEvent(ev, world);
            string T = ev.type;
            if (T == "boxChip" || T == "boxBreak" || T == "liftBounce") breakables.OnEvent(ev);
            if (T == "laneHop" || T.StartsWith("hazard")) levelFx.OnEvent(ev);
            if (T == "boxBreak") trauma = Mathf.Min(1, trauma + (ev.box.tag == "pillar" ? 0.4f : ev.box.tag == "glass" ? 0.12f : 0.2f));
            // Shockwaves from the heaviest blows (an impact frame adds its own in StartImpact)
            if (T == "boxBreak" && ev.box.tag == "pillar") Shockwave(ev.x, ev.y, 0.7f);
            else if (T == "ramSlam") Shockwave(ev.x, ev.y, 1);
            else if (T == "slam" && ev.e != null) Shockwave(ev.e.x, ev.e.y, 0.8f);
            else if (T == "poundLand" && ev.level >= 2) Shockwave(ev.x, ev.y, 0.5f + 0.25f * (float)ev.level);
            else if (T == "kineticRelease" && ev.k >= 0.5) Shockwave(ev.x, ev.y, 0.6f + (float)ev.k * 0.5f);
            float shake = Shake(ev);
            if (shake != 0) trauma = Mathf.Min(1, trauma + shake);
            // Big releases light the whole frame for a moment (bloom) and the rocket jump thumps the camera
            float glow = Glow(ev);
            if (glow != 0) bloomKick = Mathf.Min(1.4f, bloomKick + glow);
            float power = ev.power != 0 ? (float)ev.power : 0.5f;
            if (T == "rocketJump") punch = Mathf.Min(punch, -(0.25f + 0.5f * power));
            if (T == "poundLand") punch = Mathf.Min(punch, -(0.15f + 0.12f * (float)ev.level));   // the frame thumps down with the landing
            if (T == "kill" && ev.e.type == "brute") trauma = Mathf.Min(1, trauma + 0.6f);
            // The big moments get an impact frame, in the colour of whoever set it off (if the setting asks for that)
            impactBy = ev.p ?? (ev.owner as Player) ?? ev.by ?? (ev.members != null && ev.members.Count > 0 ? ev.members[0] : null);
            if (T == "impact" || (T == "armorBreak" && ev.left == 0) || (T == "parry" && ev.perfect && ev.heavy)) StartImpact(ev.x, ev.y, T == "impact" ? 1 : 0.8f);
            else if (T == "kill" && (ev.e.type == "brute" || ev.e.boss)) StartImpact(ev.x, ev.y, 1);
            else if (T == "poundLand" && ev.level >= 3) StartImpact(ev.x, ev.y + 0.6, 1);
            else if (T == "snipe" && ev.full && ev.crits > 0) StartImpact(ev.x1, ev.y1, 0.8f);
            else if (T == "bossPhase" || T == "bossDown") StartImpact(ev.x, ev.y, 1.2f);
            else if (T == "ultNova") StartImpact(ev.x, ev.y, 1.4f, true);
            else if (T == "ultFinisher") StartImpact(ev.x, ev.y, 1.2f, true);
            else if (T == "teamFinisher") pendingImpact = (0.42f, ev.x, ev.y, 1.5f, impactBy);   // when the eclipse shatters
            else if (T == "perfectDodge") StartImpact(ev.x, ev.y, 0.6f);
            else if (T == "ramSplat" && ev.n >= 2) StartImpact(ev.x, ev.y, 0.9f);
            else if (T == "kineticRelease" && ev.k >= 0.8) StartImpact(ev.x, ev.y, 0.9f);
            else if (T == "perfectGuard" && ev.heavy) StartImpact(ev.x, ev.y, 0.7f);
            else if (T == "nshieldBlock" && ev.perfect && ev.heavy) StartImpact(ev.x, ev.y, 0.7f);
            else if (T == "ramSlam") StartImpact(ev.x, ev.y, 1.4f, true);
            else if (T == "podLand") StartImpact(ev.x, ev.y + 1, 1.1f, true);
            if (T == "ultNova") punch = Mathf.Min(punch, -0.6f);
            if (T == "ramSplat" || T == "leapLand" || T == "quake") punch = Mathf.Min(punch, -0.3f);
            if (T == "ramSlam" || T == "podLand") punch = Mathf.Min(punch, -0.6f);
        }

        static float Shake(Ev ev)
        {
            float level = (float)ev.level, lv1 = level != 0 ? level : 1, power = ev.power != 0 ? (float)ev.power : 0.5f;
            switch (ev.type)
            {
                case "armorBreak": return 0.5f; case "slam": return 0.45f; case "guardBreak": return 0.3f; case "impact": return 0.5f;
                case "ambush": return 0.35f; case "challenge": return 0.2f; case "playerHit": return ev.heavy ? 0.35f : 0.15f;
                case "blast": return 0.14f + lv1 * 0.06f + (ev.perfect ? 0.1f : 0);
                case "burst": return ev.charged ? 0.06f + level * 0.04f : 0.05f;
                case "perfectRelease": return 0.1f;
                case "splash": return level != 0 ? 0.04f + level * 0.03f : 0;
                case "rocketJump": return 0.2f + 0.42f * power;
                case "enemyBlast": return 0.3f; case "chargeCrash": return 0.3f;
                case "shot": return level != 0 ? 0.03f + level * 0.04f : 0;
                case "dash": return level != 0 ? 0.04f + level * 0.05f : 0;
                case "dashLevel": return 0.02f + level * 0.02f;
                case "chargeLevel": return level >= 4 ? 0.1f : level >= 3 ? 0.04f : 0;
                case "walljump": return 0.03f;
                case "land": return ev.vy < -16 ? Mathf.Min(0.3f, (float)(-ev.vy - 16) * 0.025f) : 0;
                case "snipe": return 0.08f + 0.16f * (float)ev.f;
                case "crit": return 0.05f; case "deflect": return ev.perfect ? 0.12f : 0.05f;
                case "dashSlash": return 0.04f * (ev.tier != 0 ? (float)ev.tier : 1);
                case "crescent": return 0.06f; case "pogo": return 0.04f;
                case "poundLand": return 0.22f + 0.12f * level; case "poundDrop": return 0.03f; case "aegisHit": return 0.05f; case "beamStart": return 0.3f;
                case "aegisOff": return ev.why == "break" ? 0.3f : ev.why == "detonate" ? 0.4f : 0;
                case "bossSlam": return ev.big ? 0.55f : 0.35f; case "bossPhase": return 0.6f; case "bossDown": return 0.9f; case "bossCrash": return 0.45f; case "bossIntro": return 0.15f;
                case "frag": return 0.12f + 0.05f * level; case "cluster": return 0.1f; case "chain": return 0.04f + 0.03f * level;
                case "wellOpen": return 0.08f; case "wellCollapse": return 0.18f + 0.06f * lv1; case "riseBlast": return 0.14f; case "perfectDodge": return 0.2f;
                case "ultCast": return 0.35f; case "ultJoin": return 0.3f; case "ultNova": return 0.95f; case "ultCut": return 0.06f; case "ultFinisher": return 0.8f; case "teamFinisher": return 0.3f;
                // RAM and Fix
                case "guardBlock": return ev.heavy ? 0.2f : 0.07f; case "perfectGuard": return 0.18f; case "rampartBreak": return 0.45f;
                case "kineticRelease": return 0.25f + 0.5f * (float)ev.k;
                case "rush": return level != 0 ? 0.05f + 0.06f * level : 0.04f;
                case "plowCatch": return 0.08f; case "ramSplat": return 0.55f; case "ramBonk": return 0.3f; case "wallUp": return 0.18f;
                case "wallDown": return ev.broken ? 0.25f : 0; case "wallHit": return 0.04f; case "leapLand": return 0.4f; case "provoke": return 0.3f;
                case "quake": return 0.4f; case "upliftBlast": return 0.2f; case "linkHit": return 0.05f; case "ramSlam": return 1; case "fortify": return 0.2f;
                case "padBounce": return 0.05f; case "rivetBlast": return 0.12f; case "sparkRing": return 0.2f; case "podLand": return 0.7f; case "overhaulPulse": return 0.3f;
                case "gadgetEnd": return ev.why == "broken" ? 0.1f : 0;
                // Nova's absorbing shield
                case "nshieldBlock": return ev.perfect ? 0.16f : ev.heavy ? 0.18f : 0.06f; case "nshieldBreak": return 0.4f; case "parryStun": return 0.1f;
            }
            return 0;
        }
        static float Glow(Ev ev)
        {
            float level = (float)ev.level, power = ev.power != 0 ? (float)ev.power : 0.5f;
            switch (ev.type)
            {
                case "rocketJump": return 0.45f + 0.75f * power; case "perfectRelease": return 0.4f;
                case "dash": return level >= 3 ? 0.35f : 0; case "shot": return level >= 3 ? 0.22f : 0; case "blast": return level >= 3 ? 0.15f : 0;
                case "snipe": return ev.full ? 0.3f : 0.08f; case "beamStart": return 0.6f; case "chargeLevel": return level >= 4 ? 0.3f : 0;
                case "aegisOff": return ev.why == "detonate" ? 0.5f : ev.why == "break" ? 0.3f : 0;
                case "poundLand": return level >= 2 ? 0.2f + 0.1f * level : 0; case "bossPhase": return 0.6f; case "bossDown": return 1;
                case "wellCollapse": return 0.25f; case "riseBlast": return 0.2f; case "perfectDodge": return 0.35f; case "ultCast": return 0.6f; case "ultJoin": return 0.5f;
                case "ultNova": return 1.4f; case "ultFinisher": return 1; case "teamFinisher": return 1.4f; case "chain": return 0.08f * (1 + level);
                case "perfectGuard": return 0.3f; case "kineticRelease": return 0.3f + 0.7f * (float)ev.k; case "ramSplat": return 0.5f; case "quake": return 0.3f;
                case "upliftBlast": return 0.25f; case "ramSlam": return 1.3f; case "podLand": return 0.8f; case "overhaulPulse": return 0.7f; case "overhaulDone": return 0.6f;
                case "fortify": return 0.4f; case "wallUp": return 0.25f; case "leapLand": return 0.3f; case "provoke": return 0.35f; case "sparkRing": return 0.3f;
                case "gadgetUp": return 0.15f; case "powerUp": return 0.15f;
                case "nshieldBlock": return (ev.perfect ? 0.25f : 0.06f) + 0.12f * (float)ev.k + (ev.max ? 0.4f : 0); case "nshieldBreak": return 0.25f;
            }
            return 0;
        }

        // Impact frame: a flash, then the chosen look (Settings: Impact frame style; the seven in Grade.shader)
        // spreading from the hit, a zoom punch and colour split, easing back out. Its key colour is the look's own,
        // or (Settings: Impact frame colour) the player colour or character colour of whoever set it off. In play, a
        // short hit-pause holds the simulation (GameMain) while it runs. Its length is the Impact frame duration
        // setting; the opening flash always stays a few frames long. At most one every 0.9 s (or one at a time, when
        // they run longer than that). The same pass dims the world while an ultimate is called.
        public void StartImpact(double x, double y, float strength = 1, bool force = false)
        {
            ambience?.Startle(strength);
            if (!SETTINGS.impactFrames || (impactCd > 0 && !force)) return;
            var s = ScreenOf(x, y);
            float @base = 0.26f + 0.12f * strength, len = Mathf.Clamp((float)SETTINGS.impactDuration, 0.3f, 5);
            impact = new Impact { t = 0, @base = @base, dur = Mathf.Clamp(@base * len / 0.38f, 0.3f, 5), cx = s.x / Mathf.Max(1, w), cy = 1 - s.y / Mathf.Max(1, h), k = strength, seed = S.Rnd() * 100 };
            string style = System.Array.IndexOf(IMPACT_STYLES, SETTINGS.impactStyle) >= 0 ? SETTINGS.impactStyle : "scifi";
            var by = impactBy;
            if (by == null && world != null) { double bd = double.MaxValue; foreach (var q in world.players) { double dd = System.Math.Sqrt((q.x - x) * (q.x - x) + (q.y - y) * (q.y - y)); if (dd < bd) { bd = dd; by = q; } } }
            string mode = SETTINGS.impactColor, col = by != null && mode == "player" ? PLAYER_COLORS[by.slot] : by != null && mode == "character" ? CHARS[by.@char].energy : null;
            grade.SetFloat("_Style", System.Array.IndexOf(IMPACT_STYLES, style));
            grade.SetVector("_Accent", S.Lin(col ?? IMPACT_ACCENT[style])); grade.SetFloat("_Tinted", col != null ? 1 : 0);
            Shockwave(x, y, 0.6f + 0.4f * strength, 0.55f);
            impactCd = Mathf.Max(0.9f, impact.dur); hitPause = 0.05f + 0.05f * strength;
            trauma = Mathf.Min(1, trauma + 0.25f * strength); bloomKick = Mathf.Min(1.4f, bloomKick + 0.4f * strength);
        }
        void UpdateImpact(float dt, World world)
        {
            impactCd = Mathf.Max(0, impactCd - dt);
            var I = impact; var C = world?.ultCast;
            // An ultimate's call drains the colour from the world; it comes back as the ultimate plays out
            // (and the world darkens again under a team finisher's eclipse, until it shatters)
            float dimTo = C != null ? (C.phase == "cast" ? 0.85f : C.phase == "run" ? 0.3f : C.t >= 8 && C.t < 34 ? 0.8f : 0.12f) : 0;
            dim += (dimTo - dim) * (1 - Mathf.Exp(-dt * (dimTo > dim ? 14 : 5)));
            if (dim < 0.01f && dimTo == 0) dim = 0;
            grade.SetFloat("_Dim", dim); grade.SetFloat("_InkTime", time);
            if (I == null) { grade.SetFloat("_Amount", 0); grade.SetFloat("_Invert", 0); grade.SetFloat("_Zoom", 0); grade.SetFloat("_Split", 0); grade.SetFloat("_Glitch", 0); return; }
            I.t += dt; float k = Mathf.Min(1, I.t / I.dur);
            grade.SetVector("_Center", new Vector4(I.cx, I.cy, 0, 0)); grade.SetFloat("_Seed", I.seed);
            float kf = I.t / I.@base;   // (the flash runs on the frame's base length, not the stretched one)
            grade.SetFloat("_Invert", kf < 0.1f ? 1 : Mathf.Max(0, 1 - (kf - 0.1f) / 0.06f));
            grade.SetFloat("_Amount", k < 0.78f ? 1 : Mathf.Max(0, 1 - (k - 0.78f) / 0.22f));
            grade.SetFloat("_Zoom", 0.07f * I.k * (1 - k) * (1 - k)); grade.SetFloat("_Split", 0.008f * I.k * (1 - k));
            grade.SetFloat("_Ring", 0.05f + k * 1.25f); grade.SetFloat("_Glitch", Mathf.Max(0, 1 - k * 2.4f) * Mathf.Min(1, I.k)); grade.SetFloat("_Phase", k);
            if (I.t >= I.dur) impact = null;
        }

        public void Render(World world, float a, float dt)
        {
            time += dt; alpha = a;
            this.world = world;
            if (pendingImpact is var (pt, px, py, pk, pby))
            {
                pt -= dt;
                if (pt <= 0) { pendingImpact = null; impactBy = pby; StartImpact(px, py, pk, true); punch = Mathf.Min(punch, -0.6f); trauma = 1; }
                else pendingImpact = (pt, px, py, pk, pby);
            }
            if (Screen.width != w || Screen.height != h) Resize(Screen.width, Screen.height);
            SyncEntities(world, a, dt);
            HelmetFx.Update(dt);
            UpdateCamera(world, dt);
            ambience.Update(dt, camera.transform.position.x);
            reflection.Update(); sparks.Update(dt); vfx.Update(dt); if (world != null) levelFx.Update(dt, world, time); post.Update(dt, ambience.Sky, ambience.Cover); ZoneDressing.Animate(time);
            fx.Update(dt, world, this);
            breakables.Update(dt, world);
            UpdateImpact(dt, world);
            // Low quality: no outlines, no reflections, no surface relief, no shadows, no bloom
            bool low = SETTINGS.quality == "low";
            if (lowLook != low) { lowLook = low; Look.ShowOutlines(!low); Look.SurfaceRelief(!low); }
            sun.shadows = low ? LightShadows.None : LightShadows.Soft;
            UpdateWaves(dt);
            bloom.active = !low;
            bloom.intensity.Override(0.65f + bloomKick * 0.9f);
            // Ultra adds ambient occlusion: URP's Screen Space Ambient Occlusion, on the second renderer (the
            // editor setup makes both)
            int renderer = SETTINGS.quality == "ultra" ? 1 : 0;
            if (renderer != rendererIndex) { rendererIndex = renderer; camera.GetUniversalAdditionalCameraData().SetRenderer(renderer); }
            UpdateHdr();
        }

        // HDR output (Settings): switch the display's HDR mode to match the setting (where the display offers it),
        // and tell the Grade pass how URP is handing it the frame (its paper white, peak and colour gamut)
        bool? hdrAsked;
        void UpdateHdr()
        {
            var H = HDROutputSettings.main;
            bool avail = H != null && H.available, want = SETTINGS.hdr && avail;
            if (avail && hdrAsked != want && !H.HDRModeChangeRequested)
            {
                hdrAsked = want;
                if (H.active != want) H.RequestHDRModeChange(want);
            }
            bool on = avail && H.active;
            grade.SetFloat("_Hdr", on ? 1 : 0);
            if (!on) return;
            float paper = Mathf.Max(80, H.paperWhiteNits), peak = H.maxToneMapLuminance > 0 ? H.maxToneMapLuminance : 1000;
            var g = H.displayColorGamut;
            grade.SetFloat("_HdrPaperWhite", paper); grade.SetFloat("_HdrPeak", Mathf.Max(paper, peak));
            grade.SetFloat("_HdrGamut", g == ColorGamut.Rec2020 || g == ColorGamut.HDR10 || g == ColorGamut.DolbyHDR ? 1 : 0);
        }

        // Sim point -> pixel position on the screen, from the top left (as the prototype's CSS pixels)
        public (float x, float y, bool vis) ScreenOf(double x, double y)
        {
            var v = camera.WorldToScreenPoint(S.ToUnity(S.W(x, y)));
            float sx = v.x * w / Mathf.Max(1, camera.pixelWidth), sy = v.y * h / Mathf.Max(1, camera.pixelHeight);
            return (sx, h - sy, v.z > 0 && v.z < camera.farClipPlane);
        }
        public double[] AimFromMouse(float mx, float my, Player p)
        {
            var c = ScreenOf(p.x, p.y + p.h * 0.62);
            float dx = mx - c.x, dy = -(my - c.y), m = Mathf.Sqrt(dx * dx + dy * dy);
            return m < 6 ? null : new double[] { dx / m, dy / m };
        }
    }
}
