// Weapon swing trails (trails.js) and afterimages (ghosts.js).
//   SweepTrails: a ribbon swept by a weapon edge (from partway along the weapon out to its tip) over the last
//     few frames, white-hot at the tip and fading toward the base and with age, smoothed (Catmull-Rom) so fast
//     spins read as clean arcs. Echo's blades and glaive, Nova's hard-light fists, RAM's shield, Fix's wrench.
//   Ghosts: translucent copies of a character's pose left behind by fast moves. At spawn, simple body parts are
//     placed with the live rig's joint transforms and baked into the ghost's own mesh (one draw per ghost).
using System.Collections.Generic;
using NovaStriker.Game.Three;
using NovaStriker.Sim;
using UnityEngine;
using static NovaStriker.Sim.Cfg;

namespace NovaStriker.Game
{
    public sealed class SweepTrails
    {
        const int MAXS = 14, SUB = 3, MAXP = (MAXS - 1) * SUB + 1;
        sealed class Smp { public Vector3 a, b; public float age; public bool fresh; }
        sealed class Sweep
        {
            public DynMesh dm; public readonly List<Smp> s = new List<Smp>(); public string key; public float life = 0.12f, gain = 1; public Color color, hot;
        }
        readonly List<Sweep> pool = new List<Sweep>();

        public SweepTrails(TObj scene, int n = 14)
        {
            var mat = new TMat(TMat.Kind.Basic) { vertexColors = true, transparent = true, depthWrite = false, side = Side.Double, fog = false };
            for (int i = 0; i < n; i++)
            {
                var dm = new DynMesh(MAXP * 2, DynMesh.StripTris(MAXP), mat, true, false, "sweep");
                dm.obj.RenderOrder = 3; dm.visible = false; scene.add(dm.obj);
                pool.Add(new Sweep { dm = dm });
            }
        }

        // Which weapon edges are sweeping for a player this frame, with the trail's life (s) and brightness
        static List<(string name, float life, float gain)> ActiveEdges(Player p, Rig rig)
        {
            string st = p.state; var m = p.move; var E = rig.extra.edges;
            if (E == null || E.Count == 0) return null;
            var o = new List<(string, float, float)>();
            if (p.@char == "echo")
            {
                bool hunter = SETTINGS.echoKit == "hunter";
                if (st == "dashslash") { if (p.st >= 1 && p.st <= DASH_SLASH.ticks) { o.Add(("bladeN", 0.17f, 1.3f)); o.Add(("bladeF", 0.17f, 1.3f)); return o; } return null; }
                if (st == "parry" && hunter && p.parryResult == null) { o.Add(("glaiveA", 0.09f, 0.7f)); o.Add(("glaiveB", 0.09f, 0.7f)); return o; }
                if (st == "pound" && p.pound != null)
                {
                    if (p.pound.phase == "drop") { o.Add(("glaiveA", 0.16f, 1.3f)); return o; }
                    if (p.pound.phase == "hold" && p.pound.t <= 10) { o.Add(("glaiveA", 0.12f, 1)); o.Add(("glaiveB", 0.12f, 0.9f)); return o; }
                    return null;
                }
                if (st != "attack" || m == null) return null;
                if (p.st < m.su - 1 || p.st > m.su + m.ac + 2) return null;
                if (m.blade)
                {
                    if (m.cross) { o.Add(("bladeN", 0.11f, 1)); o.Add(("bladeF", 0.11f, 1)); }
                    else o.Add((m.offhand && hunter ? "bladeF" : "bladeN", 0.11f, 1));
                    return o;
                }
                if (m.staff)
                {
                    bool big = m.spin || m.glaive || m.launcher || m.heavy;
                    if (m.spin) { o.Add(("glaiveA", 0.1f, 0.75f)); o.Add(("glaiveB", 0.1f, 0.6f)); return o; }
                    if (big) { o.Add(("glaiveA", 0.15f, 1.2f)); o.Add(("glaiveB", 0.13f, 0.9f)); } else o.Add(("glaiveA", 0.13f, 1));
                    return o;
                }
                return null;
            }
            if (p.@char == "ram" || p.@char == "fix")
            {
                string lead = p.@char == "ram" ? "shield" : "wrench";
                if (st == "pound" && p.pound != null) { if (p.pound.phase == "drop") { o.Add((lead, 0.16f, 1.3f)); return o; } return null; }
                if (st != "attack" || m == null || p.st < m.su - 1 || p.st > m.su + m.ac + 2) return null;
                if (m.fist) { o.Add(("fistF", 0.14f, 1.3f)); return o; }
                o.Add((lead, m.heavy || m.launcher ? 0.15f : 0.11f, m.heavy ? 1.3f : 1)); return o;
            }
            if (st == "pound" && p.pound != null) { if (p.pound.phase == "drop") { o.Add(("fistN", 0.16f, 1.4f)); return o; } return null; }
            if (st != "attack" || m == null || !m.fist) return null;
            if (p.st < m.su - 1 || p.st > m.su + m.ac + 2) return null;
            if (p.moveId == "nova_kair") { o.Add(("boot", 0.12f, 1)); return o; }
            o.Add((m.offhand ? "fistF" : "fistN", m.heavy ? 0.14f : 0.1f, m.heavy ? 1.3f : 1)); return o;
        }

        Sweep Get(string key)
        {
            var s = pool.Find(q => q.key == key);
            if (s != null) return s;
            s = pool.Find(q => q.s.Count == 0);
            if (s == null) { s = pool[0]; foreach (var q in pool) if (q.s.Count < s.s.Count) s = q; }
            s.key = key; s.s.Clear(); return s;
        }

        static Vector3 Cr(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float u)
        {
            float u2 = u * u, u3 = u2 * u;
            return 0.5f * (2 * p1 + (-p0 + p2) * u + (2 * p0 - 5 * p1 + 4 * p2 - p3) * u2 + (-p0 + 3 * p1 - 3 * p2 + p3) * u3);
        }

        public void Update(float dt, World world, Dictionary<Player, Rig> rigs)
        {
            foreach (var p in world.players)
            {
                rigs.TryGetValue(p, out var rig);
                var edges = rig != null && rig.root.visible && p.state != "downed" ? ActiveEdges(p, rig) : null;
                if (edges == null) continue;
                var col = S.Lin(CHARS[p.@char].energy);
                foreach (var (name, life, gain) in edges)
                {
                    if (!rig.extra.edges.TryGetValue(name, out var E)) continue;
                    var s = Get(p.slot + ":" + name);
                    s.life = life; s.gain = gain; s.color = col; s.hot = Color.Lerp(col, Color.white, 0.65f);
                    var tip = E.b.worldPos; var bas = Vector3.Lerp(E.a.worldPos, tip, E.from);
                    s.s.Insert(0, new Smp { a = bas, b = tip, fresh = true });
                    if (s.s.Count > MAXS) s.s.RemoveAt(s.s.Count - 1);
                }
            }
            foreach (var s in pool)
            {
                if (s.s.Count == 0) { s.dm.visible = false; continue; }
                foreach (var q in s.s) { if (!q.fresh) q.age += dt; q.fresh = false; }
                while (s.s.Count > 0 && s.s[s.s.Count - 1].age > s.life) s.s.RemoveAt(s.s.Count - 1);
                if (s.s.Count < 2) { s.dm.visible = false; if (s.s.Count == 0) s.key = null; continue; }
                Rebuild(s);
            }
        }

        void Rebuild(Sweep s)
        {
            var Sm = s.s; int n = Sm.Count, k = 0;
            void Put(Vector3 a, Vector3 b, float age)
            {
                if (k >= MAXP) return;
                float f = Mathf.Max(0, 1 - age / s.life), fa = f * f * Mathf.Min(1, s.gain);
                s.dm.Set(k * 2, a); s.dm.Set(k * 2 + 1, b);
                float g = 1.4f * s.gain;
                s.dm.col[k * 2] = new Color(s.color.r * g, s.color.g * g, s.color.b * g, 0.05f * fa);
                s.dm.col[k * 2 + 1] = new Color(s.hot.r * 2.2f * g, s.hot.g * 2.2f * g, s.hot.b * 2.2f * g, 0.95f * fa);
                k++;
            }
            for (int i = 0; i < n - 1; i++)
            {
                Smp p0 = Sm[Mathf.Max(0, i - 1)], p1 = Sm[i], p2 = Sm[i + 1], p3 = Sm[Mathf.Min(n - 1, i + 2)];
                for (int j = 0; j < SUB; j++)
                {
                    float u = (float)j / SUB;
                    Put(Cr(p0.a, p1.a, p2.a, p3.a, u), Cr(p0.b, p1.b, p2.b, p3.b, u), p1.age + (p2.age - p1.age) * u);
                }
            }
            Put(Sm[n - 1].a, Sm[n - 1].b, Sm[n - 1].age);
            for (int i = k; i < MAXP; i++)
            {
                s.dm.pos[i * 2] = s.dm.pos[(k - 1) * 2]; s.dm.pos[i * 2 + 1] = s.dm.pos[(k - 1) * 2 + 1];
                s.dm.col[i * 2] = s.dm.col[i * 2 + 1] = Color.clear;
            }
            s.dm.Upload(); s.dm.visible = true;
        }
        public void ClearAll() { foreach (var s in pool) { s.s.Clear(); s.key = null; s.dm.visible = false; } }
    }

    public sealed class Ghosts
    {
        readonly TObj scene; readonly int max;
        sealed class G { public TMesh root; public Mesh mesh; public TMat mat; public float life, max = 1, @base; public Vector3 center; public Vector3[] verts; }
        readonly List<G> pool = new List<G>();
        sealed class PartDef { public System.Func<Rig, TObj> joint; public Mesh geo; public Vector3 at; public Vector3[] v; public int[] tri; }
        List<PartDef> parts; int nVerts, nTris; int[] tris;

        public Ghosts(TObj scene, int max = 30) { this.scene = scene; this.max = max; }

        // Body parts: which rig joint carries each, and where the part sits in that joint's space
        void PartList()
        {
            Mesh Cap(float r, float len) => Geo.Capsule(r, Mathf.Max(0.01f, len), 3, 8);
            parts = new List<PartDef>
            {
                new PartDef { joint = r => r.hips, geo = Geo.Box(0.3f, 0.22f, 0.34f), at = new Vector3(0, 0.02f, 0) },
                new PartDef { joint = r => r.spine, geo = Geo.Box(0.36f, 0.52f, 0.44f), at = new Vector3(0.02f, 0.33f, 0) },
                new PartDef { joint = r => r.head, geo = Geo.Sphere(0.155f, 12, 8), at = new Vector3(0, 0.1f, 0) },
                new PartDef { joint = r => r.armN.top, geo = Cap(0.07f, 0.24f), at = new Vector3(0, -0.15f, 0) }, new PartDef { joint = r => r.armN.joint, geo = Cap(0.064f, 0.23f), at = new Vector3(0, -0.145f, 0) },
                new PartDef { joint = r => r.armF.top, geo = Cap(0.07f, 0.24f), at = new Vector3(0, -0.15f, 0) }, new PartDef { joint = r => r.armF.joint, geo = Cap(0.064f, 0.23f), at = new Vector3(0, -0.145f, 0) },
                new PartDef { joint = r => r.legN.top, geo = Cap(0.095f, 0.37f), at = new Vector3(0, -0.23f, 0) }, new PartDef { joint = r => r.legN.joint, geo = Cap(0.085f, 0.38f), at = new Vector3(0, -0.23f, 0) },
                new PartDef { joint = r => r.legF.top, geo = Cap(0.095f, 0.37f), at = new Vector3(0, -0.23f, 0) }, new PartDef { joint = r => r.legF.joint, geo = Cap(0.085f, 0.38f), at = new Vector3(0, -0.23f, 0) },
            };
            var t = new List<int>();
            foreach (var P in parts) { P.v = P.geo.vertices; P.tri = P.geo.triangles; foreach (var i in P.tri) t.Add(i + nVerts); nVerts += P.v.Length; }
            tris = t.ToArray(); nTris = tris.Length;
        }

        G Build()
        {
            if (parts == null) PartList();
            var mesh = new Mesh { name = "ghost" }; mesh.MarkDynamic();
            var verts = new Vector3[nVerts]; mesh.vertices = verts; mesh.triangles = tris; mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100000);
            // Ordinary blending and untone-mapped colour, so afterimages read as solid colour on bright scenes
            var mat = new TMat(TMat.Kind.Basic) { colorHex = 0xffffff, transparent = true, opacity = 0, depthWrite = false, fog = false };
            var root = new TMesh(mesh, mat); root.go.name = "ghost"; root.visible = false; root.RenderOrder = 2; scene.add(root);
            return new G { root = root, mesh = mesh, mat = mat, verts = verts };
        }

        // Leave a ghost in the rig's current pose
        public void Spawn(Rig rig, Color lin, float opacity = 0.4f, float life = 0.25f)
        {
            G g = pool.Find(q => q.life <= 0);
            if (g == null)
            {
                if (pool.Count < max) { g = Build(); pool.Add(g); }
                else { g = pool[0]; foreach (var q in pool) if (q.life < g.life) g = q; }
            }
            int o = 0;
            foreach (var P in parts)
            {
                var m = P.joint(rig).tr.localToWorldMatrix * Matrix4x4.Translate(Th.P(P.at));
                for (int i = 0; i < P.v.Length; i++) g.verts[o++] = m.MultiplyPoint3x4(P.v[i]);
            }
            g.mesh.vertices = g.verts; g.mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100000);
            g.center = rig.root.tr.position; g.root.tr.localPosition = Vector3.zero; g.root.tr.localScale = Vector3.one;
            g.mat.colorLin = lin; g.@base = opacity; g.mat.opacity = opacity; g.life = g.max = life; g.root.visible = true;
        }

        public void Update(float dt)
        {
            foreach (var g in pool)
            {
                if (g.life <= 0) { if (g.root.visible) g.root.visible = false; continue; }
                g.life -= dt;
                float k = Mathf.Max(0, g.life / g.max);
                g.mat.opacity = g.@base * k * k;
                // A slight swell about the body as it fades (in Unity space: the ghost's vertices are world points)
                float s = 1 + (1 - k) * 0.08f;
                g.root.tr.localScale = Vector3.one * s; g.root.tr.localPosition = g.center * (1 - s);
            }
        }
    }
}
