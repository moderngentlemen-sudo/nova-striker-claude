// Set dressing and breakables (landmarks.js). Presentation only: reads the level data and the sim, never changes it.
//   Landmarks.Build(view): the 3D set pieces that make the routes' paths read as places in space, placed from the
//     path pieces themselves (Level.SEGS): whatever an arc wraps round gets a structure at its centre (the
//     Foundry's reactor core inside the helix, its furnace dome, the Undercity's cooling tower), an arc that bends
//     toward the camera gets a curved wall behind it, and the straights get their surroundings (foundry machinery
//     and pipes; city blocks with lit windows; the transit rail and a train).
//   Breakables: the breakable pieces' meshes (crate, barricade, glass, pillar: Level.DESTRUCT), shaking and
//     darkening as they take damage, and the debris they burst into (a pool of chunks that bounce and settle).
using System.Collections.Generic;
using System.Linq;
using NovaStriker.Game.Three;
using Sprite = NovaStriker.Game.Three.Sprite;
using NovaStriker.Sim;
using UnityEngine;

namespace NovaStriker.Game
{
    public static class Landmarks
    {
        // The prototype's own seeded generator (Park-Miller), so the dressing stands where it did there
        static double seed = 1234;
        internal static float Rnd() { seed = (seed * 16807) % 2147483647; return (float)(seed / 2147483647); }

        // A texture of lit windows (the Undercity's towers)
        static Texture2D WindowTex()
        {
            var g = new Paint(64, 128); g.fillStyle = "#1b1f33"; g.fillRect(0, 0, 64, 128);
            for (int y = 4; y < 128; y += 10) for (int x = 4; x < 64; x += 12)
            {
                bool on = Rnd() < 0.42f; g.fillStyle = on ? (Rnd() < 0.7f ? "#ffd9a0" : "#9fe7ff") : "#262b45"; g.fillRect(x, y, 7, 5);
            }
            return g.ToTexture(true, true);
        }
        // Hazard stripes (barricades)
        internal static Texture2D StripeTex()
        {
            var g = new Paint(64, 64); g.fillStyle = "#2c3442"; g.fillRect(0, 0, 64, 64); g.fillStyle = "#ffc23a";
            for (int i = -64; i < 128; i += 22) { g.beginPath(); g.moveTo(i, 0); g.lineTo(i + 11, 0); g.lineTo(i + 11 - 64, 64); g.lineTo(i - 64, 64); g.fill(); }
            return g.ToTexture(true, true);
        }
        // Planks (crates)
        internal static Texture2D PlankTex()
        {
            var g = new Paint(64, 64); g.fillStyle = "#c98b4a"; g.fillRect(0, 0, 64, 64);
            g.strokeStyle = "#8a5a2b"; g.lineWidth = 3; g.strokeRect(2, 2, 60, 60);
            g.beginPath(); g.moveTo(4, 4); g.lineTo(60, 60); g.stroke();
            g.fillStyle = "rgba(0,0,0,0.12)"; for (int y = 0; y < 64; y += 13) g.fillRect(0, y, 64, 2);
            return g.ToTexture(true, false);
        }

        static TMat Glow(uint color, uint emissive, float k) { var m = TMat.Std(color); m.emissiveHex = emissive; m.emissiveIntensity = k; return m; }

        public static void Build(View view)
        {
            void Add(Mesh geo, TMat mat, float x, float y, float z, float ry = 0, float rx = 0, float rz = 0, bool cast = false) =>
                view.Bake(geo, mat, new Vector3(x, y, z), new Vector3(rx, ry, rz), null, cast);
            Vector3 At(double x, double y, double depth) => S.W(x, y, depth);
            var steel = TMat.Std(0x6b6f7a, 0.55f, 0.4f); var rust = TMat.Std(0x8a5a3c, 0.8f, 0.2f); var core = TMat.Std(0xd8dde4, 0.35f, 0.3f);
            var molten = Glow(0xff8a2a, 0xff6a10, 2.4f); var hot = Glow(0xffd28a, 0xffa040, 2.0f);
            var concrete = TMat.Std(0x8d8fa3, 0.9f);
            var windowMap = WindowTex();
            var windows = TMat.Std(0xffffff, 0.8f); windows.map = windowMap; windows.emissiveHex = 0xffffff; windows.emissiveMap = windowMap; windows.emissiveIntensity = 1.3f;
            var neon = Glow(0xff7ad9, 0xff4fc8, 2.2f); var cyan = Glow(0x7fe3ff, 0x4fd6ff, 2.0f);
            view.neon.Add((neon, 2.2f)); view.neon.Add((cyan, 2.0f));
            var train = Glow(0xe9edf3, 0x4fd6ff, 0.15f); train.roughness = 0.35f;
            // Surface detail (Look): plating on the steelwork and the reactor core, rusted plate, poured concrete
            Look.ApplySurface(steel, "panel", 3.5f); Look.ApplySurface(rust, "panel", 3, 0.9f); Look.ApplySurface(core, "panel", 4, 0.5f); Look.ApplySurface(concrete, "concrete", 4, 0.6f);
            const float PI = Mathf.PI;

            foreach (var g in Level.SEGS)
            {
                bool foundry = g.route == "foundry";
                float Cx = (float)g.Cx, Cz = (float)g.Cz;
                if (g.kind == "arc" && g.s > 0)
                {
                    // Whatever the path wraps round: a structure at the arc's centre
                    float R = (float)g.r - 4.2f;
                    if (foundry && g.r < 18)
                    {
                        // The reactor core inside the helix: a tall drum with glowing rings at each turn of the climb
                        Add(Geo.Cylinder(R, R + 0.8f, 70, 48), core, Cx, 18, Cz);
                        for (float y = -2; y < 50; y += 5.5f) Add(Geo.Torus(R + 0.35f, 0.28f, 8, 48), molten, Cx, y, Cz, rx: PI / 2);
                        for (int i = 0; i < 10; i++) { float a = i / 10f * PI * 2; Add(Geo.Box(0.5f, 64, 0.5f), hot, Cx + Mathf.Sin(a) * (R + 0.2f), 18, Cz + Mathf.Cos(a) * (R + 0.2f)); }
                        Add(Geo.Cylinder(R * 0.55f, R, 6, 32), steel, Cx, 56, Cz);
                    }
                    else if (foundry)
                    {
                        // The furnace dome the path bends round, venting heat
                        Add(Geo.Sphere(R, 32, 18, 0, PI * 2, 0, PI / 2), rust, Cx, -2, Cz);
                        Add(Geo.Cylinder(R, R, 4, 32), steel, Cx, -4, Cz);
                        for (int i = 0; i < 6; i++)
                        {
                            float a = i / 6f * PI * 2;
                            Add(Geo.Cylinder(0.9f, 1.2f, 26, 12), steel, Cx + Mathf.Sin(a) * R * 0.5f, 11, Cz + Mathf.Cos(a) * R * 0.5f);
                            Add(Geo.Cylinder(1.0f, 1.0f, 0.6f, 12), molten, Cx + Mathf.Sin(a) * R * 0.5f, 24.2f, Cz + Mathf.Cos(a) * R * 0.5f);
                        }
                    }
                    else
                    {
                        // The cooling tower the stair winds round: a hyperboloid shell
                        var pts = new Vector2[13];
                        for (int i = 0; i <= 12; i++) { float t = i / 12f; pts[i] = new Vector2(R * (1 - 0.32f * Mathf.Sin(t * PI * 0.95f)), -20 + t * 56); }
                        var shell = TMat.Std(0xa7a9bd, 0.85f); shell.side = Side.Double;
                        Add(Geo.Lathe(pts, 48, key: "tower" + R), shell, Cx, 0, Cz);
                        foreach (var y in new float[] { 8, 24 }) Add(Geo.Torus(R * (y < 20 ? 0.74f : 0.7f) + 0.2f, 0.18f, 6, 48), neon, Cx, y, Cz, rx: PI / 2);
                    }
                }
                // Along every piece: what stands behind the path (and, on a bend toward the camera, a wall that curves with it)
                for (double x = g.x0; x < g.x1; x += foundry ? 3.2 : 5)
                {
                    float ry = S.YawAt(x); bool back = g.kind == "arc" && g.s < 0;
                    if (foundry)
                    {
                        if (back)
                        {
                            var p = At(x, 2, -7.5); Add(Geo.Box(3.4f, 22, 1.4f), steel, p.x, p.y, p.z, ry);
                            if (Rnd() < 0.5f) { var q = At(x, 6 + Rnd() * 8, -6.6); Add(Geo.Cylinder(0.35f, 0.35f, 3.4f, 8), molten, q.x, q.y, q.z, ry + PI / 2, PI / 2); }
                        }
                        else if (Rnd() < 0.55f)
                        {
                            float d = -(10 + Rnd() * 14), hh = 8 + Rnd() * 22; var p = At(x, -10 + hh / 2, d);
                            Add(Geo.Box(2.6f, hh, 2.6f), Rnd() < 0.5f ? steel : rust, p.x, p.y, p.z, ry);
                            if (Rnd() < 0.5f) { var q = At(x, -10 + hh + 0.4f, d); Add(Geo.Cylinder(0.8f, 0.8f, 0.8f, 10), molten, q.x, q.y, q.z); }
                        }
                        // the molten channel far below the walkways
                        var m = At(x, -9, -1); Add(Geo.Box(3.4f, 0.4f, 5), molten, m.x, m.y, m.z, ry);
                    }
                    else
                    {
                        if (back) { float hh = 26 + Rnd() * 18; var p = At(x, -6 + hh / 2, -9); Add(Geo.Box(4.6f, hh, 4), windows, p.x, p.y, p.z, ry); }
                        else
                        {
                            // (both depths are drawn before either block, as the prototype's array literal does)
                            float d1 = -(12 + Rnd() * 10), d2 = -(30 + Rnd() * 20);
                            foreach (var d in new[] { d1, d2 })
                            {
                                float hh = 20 + Rnd() * 50, ww = 5 + Rnd() * 5; var p = At(x, -20 + hh / 2, d);
                                var geo = Geo.Copy(Geo.Box(ww, hh, ww)); var uv = geo.uv;
                                for (int i = 0; i < uv.Length; i++) uv[i] = new Vector2(uv[i].x * ww / 6, uv[i].y * hh / 12);
                                geo.uv = uv;
                                Add(geo, windows, p.x, p.y, p.z, ry);
                                if (Rnd() < 0.25f) { var q = At(x, -20 + hh + 0.3f, d); Add(Geo.Box(ww * 0.9f, 0.3f, 0.3f), neon, q.x, q.y, q.z, ry); }
                            }
                        }
                    }
                }
            }
            // Lift pads on the foundry floor: a glowing disc with a faint column of light up to the platform it serves
            foreach (var L in Level.LIFTS)
            {
                var p = At(L.x, L.y + 0.06, 0); Add(Geo.Cylinder(0.9f, 1.0f, 0.12f, 24), hot, p.x, p.y, p.z);
                var c = At(L.x, L.y + 0.14, 0); Add(Geo.Torus(0.7f, 0.06f, 6, 24), cyan, c.x, c.y, c.z, rx: PI / 2);
                var bm = TMat.Sprite(view.fx.tex.glow, 0xffc070); bm.opacity = 0.22f; bm.blending = Blending.Additive; bm.depthWrite = false;
                var beam = Sprite.Make(bm); beam.position.copy(At(L.x, (L.y + L.top) / 2, 0)); beam.scale.set(1.2f, (float)(L.top - L.y), 1); view.scene.add(beam);
            }
            // The Undercity's transit line: a rail overhead along it, and a train standing on the far track
            foreach (var g in Level.SEGS.Where(q => q.route == "undercity" && q.kind == "line" && q.x1 - q.x0 > 70))
            {
                for (double x = g.x0; x < g.x1; x += 2.5)
                {
                    var r = At(x, 9, -1.5); Add(Geo.Box(2.6f, 0.3f, 0.5f), concrete, r.x, r.y, r.z, S.YawAt(x));
                    var s = At(x, 9.2, -1.5); Add(Geo.Box(2.6f, 0.08f, 0.12f), cyan, s.x, s.y - 0.25f, s.z, S.YawAt(x));
                    if (JMath.Round(x - g.x0) % 10 == 0) { var c = At(x, 1, -6.5); Add(Geo.Box(0.4f, 14, 0.4f), concrete, c.x, 2, c.z); }
                }
                double t0 = g.x0 + 20;
                for (int i = 0; i < 3; i++) { var t = At(t0 + i * 9, 1.5, -5); Add(Geo.Capsule(1.4f, 6.4f, 4, 12), train, t.x, 1.6f, t.z, S.YawAt(t0 + i * 9), 0, PI / 2); }
            }
        }
    }

    // ---- Breakables ----
    public sealed class Breakables
    {
        readonly TObj scene; readonly Fx fx;
        sealed class Piece { public TObj g; public float shake; public Vector3 home; public List<(TMesh m, TMat own)> tinted = new List<(TMesh, TMat)>(); public float k = -1; }
        readonly Dictionary<LevelBox, Piece> meshes = new Dictionary<LevelBox, Piece>();
        readonly TMat crate, barricade, glass, frame, pillar, cap;
        float t;

        sealed class Bit { public float life, max, x, y, z, vx, vy, vz, s, rx, ry, wx, wy; public double sx; public bool glass; public Transform tr; public MeshRenderer mr; }
        const int N = 220;
        int di;
        readonly Bit[] chunks = new Bit[N];
        readonly MaterialPropertyBlock mpb = new MaterialPropertyBlock();

        public Breakables(TObj scene, Fx fx)
        {
            this.scene = scene; this.fx = fx;
            crate = TMat.Std(0xffffff, 0.85f); crate.map = Landmarks.PlankTex();
            barricade = TMat.Std(0xffffff, 0.6f, 0.3f); barricade.map = Landmarks.StripeTex();
            glass = TMat.Std(0xbfe8ff, 0.05f, 0.1f); glass.transparent = true; glass.opacity = 0.38f; glass.depthWrite = false;
            frame = TMat.Std(0x5f7897, 0.6f); pillar = TMat.Std(0xb9c2cc, 0.8f);
            cap = TMat.Std(0x7fe3ff); cap.emissiveHex = 0x4fd6ff; cap.emissiveIntensity = 1.6f;
            foreach (var b in Level.BOXES)
            {
                if (b.type != 'd') continue;
                float w = (float)(b.x1 - b.x0), h = (float)(b.y1 - b.y0); double xm = (b.x0 + b.x1) / 2;
                var g = Group.Make(name: "breakable " + b.tag);
                if (b.tag == "crate") g.add(new TMesh(Geo.RoundedBox(w, h, Mathf.Max(w, 1), 2, 0.05f), crate));
                else if (b.tag == "barricade") g.add(new TMesh(Geo.RoundedBox(w, h, 2.6f, 2, 0.08f), barricade));
                else if (b.tag == "glass")
                {
                    g.add(new TMesh(Geo.Box(w * 0.6f, h, 3.4f), glass));
                    foreach (var y in new[] { -h / 2, h / 2 }) { var f = new TMesh(Geo.Box(w, 0.12f, 3.5f), frame); f.position.y = y; g.add(f); }
                }
                else
                {
                    g.add(new TMesh(Geo.Cylinder(w * 0.5f, w * 0.58f, h, 16), pillar));
                    var c = new TMesh(Geo.Box(w * 1.25f, 0.18f, w * 1.25f), cap); c.position.y = h / 2 - 0.09f; g.add(c);
                }
                var P = new Piece { g = g };
                g.traverse(o => { if (o is TMesh m) { m.cast = b.tag != "glass"; m.receive = true; if (m.material != glass && m.material != cap) P.tinted.Add((m, null)); } });
                g.position.copy(S.W(xm, b.y0 + h / 2)); g.rotation.y = S.YawAt(xm);
                P.home = g.position.v;
                scene.add(g); meshes[b] = P;
            }
            // Debris: a pool of chunks (one material; each chunk tinted to what it came from)
            var debrisMat = TMat.Std(0xffffff, 0.7f);
            var cube = Geo.Box(1, 1, 1);
            var root = new GameObject("debris").transform; root.SetParent(scene.tr, false);
            for (int i = 0; i < N; i++)
            {
                var go = new GameObject("chunk"); go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = cube;
                var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = debrisMat.m;
                go.SetActive(false);
                chunks[i] = new Bit { tr = go.transform, mr = mr };
            }
        }

        static float R() => Landmarks.Rnd();

        public void OnEvent(Ev ev)
        {
            var F = fx; var b = ev.box;
            if (ev.type == "liftBounce") { F.Sprite(ev.x, ev.y + 0.3, "ring", "#ffc070", 1.4f, 0.3f, 2.4f); F.Burst(ev.x, ev.y + 0.2, "#ffd28a", 18, 6, 0.24f, 0.35f, Mathf.PI / 2, 0.8f); return; }
            if (ev.type == "boxChip")
            {
                if (meshes.TryGetValue(b, out var M)) M.shake = 1;
                var D = Level.DESTRUCT[b.tag];
                F.Burst(ev.x, ev.y, ev.hard ? "#ffffff" : D.color, ev.hard ? 4 : 6, 4, 0.18f, 0.3f, grav: 12);
                if (b.tag == "glass") F.Sprite(ev.x, ev.y, "star", "#ffffff", 0.6f, 0.1f, 1.4f);
            }
            else if (ev.type == "boxBreak")
            {
                var D = Level.DESTRUCT[b.tag]; double w = b.x1 - b.x0, h = b.y1 - b.y0;
                int n = b.tag == "glass" ? 26 : b.tag == "crate" ? 14 : b.tag == "barricade" ? 18 : 22;
                bool isGlass = b.tag == "glass";
                for (int i = 0; i < n; i++)
                {
                    double x = b.x0 + R() * w, y = b.y0 + R() * h; var by = ev.by;
                    float push = by != null ? (x - by.x > 0 ? 1 : x - by.x < 0 ? -1 : 1) : (R() < 0.5f ? -1 : 1);
                    float depth = (R() - 0.5f) * (isGlass ? 2 : 1.2f), vx = push * (2 + R() * 5), vy = 2 + R() * 6, vz = (R() - 0.5f) * 4;
                    Chunk(x, y, depth, vx, vy, vz, isGlass ? 0.06f + R() * 0.18f : 0.12f + R() * 0.22f, D.color, isGlass);
                }
                F.Smoke(ev.x, ev.y, b.tag == "crate" ? "#b59a7a" : "#9aa3ae", isGlass ? 2 : 8, 1.6f, 0.6f, 0.8f, op: 0.45f, grow: 2.2f);
                if (isGlass) { F.Burst(ev.x, ev.y, "#e8f8ff", 20, 7, 0.16f, 0.3f, grav: 10); F.Sprite(ev.x, ev.y, "star", "#ffffff", 1.6f, 0.14f, 1.5f); }
                else F.Dust(ev.x, b.y0, 0.6f, new[] { 0f, Mathf.PI });
                if (b.tag == "pillar") F.Sprite(ev.x, ev.y, "ring", "#ffffff", 1.4f, 0.3f, 2.4f);
                if (b.loot != null) F.Sprite(ev.x, ev.y, "glow", "#ffe9a8", 1.6f, 0.4f, 1.6f);
            }
        }

        void Chunk(double x, double y, float depth, float vx, float vy, float vz, float s, string color, bool isGlass)
        {
            var C = chunks[di]; di = (di + 1) % N;
            var w = S.W(x, y, depth); var d = S.Dir(x, vx, vy);
            C.life = C.max = isGlass ? 1.4f : 4.5f; C.x = w.x; C.y = w.y; C.z = w.z; C.vx = d.x; C.vy = d.y; C.vz = d.z + vz * 0.3f;
            C.s = s; C.sx = x; C.rx = R() * 6; C.ry = R() * 6; C.wx = (R() - 0.5f) * 14; C.wy = (R() - 0.5f) * 14; C.glass = isGlass;
            var col = S.Lin(color) * (0.75f + R() * 0.4f);
            mpb.SetVector("_BaseColor", new Vector4(col.r, col.g, col.b, 1));
            C.mr.SetPropertyBlock(mpb);
            C.tr.gameObject.SetActive(true);
        }

        public void Update(float dt, World world)
        {
            t += dt;
            foreach (var kv in meshes)
            {
                var b = kv.Key; var M = kv.Value;
                M.g.visible = !b.broken;
                if (b.broken) continue;
                M.shake = Mathf.Max(0, M.shake - dt * 6);
                var home = M.home;
                if (M.shake > 0) { home.x += (R() - 0.5f) * 0.12f * M.shake; home.z += (R() - 0.5f) * 0.12f * M.shake; }
                M.g.position.copy(home);
                float k = (float)(b.hp / Level.DESTRUCT[b.tag].hp);   // darker as it takes damage
                if (k != M.k)
                {
                    M.k = k;
                    for (int i = 0; i < M.tinted.Count; i++)
                    {
                        var (m, own) = M.tinted[i];
                        if (own == null) { own = m.material.clone(); m.material = own; M.tinted[i] = (m, own); }
                        float v = 0.55f + 0.45f * k; own.colorLin = new Color(v, v, v);
                    }
                }
            }
            for (int i = 0; i < N; i++)
            {
                var C = chunks[i];
                if (C.life <= 0) { if (C.max > 0) { C.tr.gameObject.SetActive(false); C.max = 0; } continue; }
                C.life -= dt; C.vy -= 20 * dt;
                C.x += C.vx * dt; C.y += C.vy * dt; C.z += C.vz * dt; C.rx += C.wx * dt; C.ry += C.wy * dt;
                float floor = (float)Level.GroundBelow(C.sx, C.y + 0.4);
                if (C.y - C.s / 2 < floor && C.y > floor - 1) { C.y = floor + C.s / 2; C.vy = Mathf.Abs(C.vy) * 0.3f; C.vx *= 0.6f; C.vz *= 0.6f; C.wx *= 0.6f; C.wy *= 0.6f; }
                float fade = Mathf.Min(1, C.life / 0.6f);
                C.tr.localPosition = Th.P(C.x, C.y, C.z);
                C.tr.localRotation = Th.Quat(C.rx, C.ry, 0);
                C.tr.localScale = new Vector3(C.s * fade, C.s * (C.glass ? 0.25f : 1) * fade, C.s * fade);
            }
        }
    }
}
