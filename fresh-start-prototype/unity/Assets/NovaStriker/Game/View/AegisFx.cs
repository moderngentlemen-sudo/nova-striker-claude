// Nova's hard-light Aegis (aegisfx.js): a faceted dome of light panels around him. Hits send a ripple across it
// and crack the panels around the impact (main fractures, then branches, then fine crazing as the damage grows);
// badly cracked panels splinter off as shards, clusters go as its strength drops past two thirds and one third,
// and when it breaks every remaining panel shatters outward. A detonation blasts the panels out; running out of
// time dissolves them. The energy each hit puts into the shield streams into his bracer (Overcharge).
// The prototype flies the shards in a vertex shader; here each frame's panel positions are computed on the CPU
// (1,280 panels) and Aegis.shader draws them.
using System.Collections.Generic;
using NovaStriker.Game.Three;
using NovaStriker.Sim;
using UnityEngine;
using static NovaStriker.Sim.Cfg;

namespace NovaStriker.Game
{
    public sealed class AegisFX
    {
        const int DETAIL = 3;
        static string GOLD => CHARS["nova"].energy;
        readonly Fx fx; readonly TObj scene;
        readonly Texture2D tex;
        readonly Dictionary<Player, Dome> domes = new Dictionary<Player, Dome>();
        float t;

        // Crack texture: brightness is the order a line appears in, so one threshold per panel reveals more of it
        static Texture2D CrackTexture(int size = 256)
        {
            var g = new Paint(size, size); g.fillStyle = "#000"; g.fillRect(0, 0, size, size);
            g.globalCompositeOperation = "lighten"; g.lineCap = "round"; g.lineJoin = "round";
            long seed = 7; float Rnd() { seed = seed * 16807 % 2147483647; return seed / 2147483647f; }
            void Walk(float x, float y, float a, int n, float len, float width, int v, List<(float, float, float)> keep)
            {
                g.strokeStyle = "rgb(" + v + "," + v + "," + v + ")"; g.lineWidth = width; g.beginPath(); g.moveTo(x, y);
                var pts = new List<(float, float, float)>();
                for (int i = 0; i < n; i++) { a += (Rnd() - 0.5f) * 1.1f; x += Mathf.Cos(a) * len * (0.6f + Rnd() * 0.8f); y += Mathf.Sin(a) * len * (0.6f + Rnd() * 0.8f); g.lineTo(x, y); pts.Add((x, y, a)); }
                g.stroke(); if (keep != null) keep.AddRange(pts);
            }
            var mains = new List<(float x, float y, float a)>();
            for (int i = 0; i < 70; i++) { float x0 = Rnd() * size, y0 = Rnd() * size, a0 = Rnd() * 6.28f; Walk(x0, y0, a0, 2 + (int)(Rnd() * 2), size / 26f, 1, 95, null); }
            for (int i = 0; i < 6; i++) { float x0 = Rnd() * size, y0 = Rnd() * size, a0 = Rnd() * 6.28f; Walk(x0, y0, a0, 9, size / 11f, 2.4f, 255, mains); }
            for (int i = 0; i < 26; i++) { var m = mains[(int)(Rnd() * mains.Count)]; float da = (Rnd() < 0.5f ? 1 : -1) * (0.6f + Rnd() * 0.8f); Walk(m.x, m.y, m.a + da, 4, size / 18f, 1.6f, 175, null); }
            return g.ToTexture(false, true);
        }

        // The dome's shared shape: subdivided icosahedron faces, each panel's crack-texture patch and its centre
        static List<Vector3> faces3; static Vector2[] uvBase; static Vector3[] centers; static int F;
        static void BaseGeometry()
        {
            if (faces3 != null) return;
            faces3 = Geo.IcosahedronFaces(DETAIL);
            for (int i = 0; i < faces3.Count; i++) faces3[i] = faces3[i].normalized;
            F = faces3.Count / 3; uvBase = new Vector2[F * 3]; centers = new Vector3[F];
            long seed = 3; float Rnd() { seed = seed * 16807 % 2147483647; return seed / 2147483647f; }
            var corners = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0.87f) };
            for (int f = 0; f < F; f++)
            {
                centers[f] = (faces3[f * 3] + faces3[f * 3 + 1] + faces3[f * 3 + 2]) / 3;
                float ou = Rnd(), ov = Rnd(), s = 0.3f, ang = Rnd() * 6.28f;
                for (int k = 0; k < 3; k++)
                {
                    var c = corners[k]; float ru = c.x * Mathf.Cos(ang) - c.y * Mathf.Sin(ang), rv = c.x * Mathf.Sin(ang) + c.y * Mathf.Cos(ang);
                    uvBase[f * 3 + k] = new Vector2(ou + ru * s, ov + rv * s);
                }
            }
        }

        sealed class Dome
        {
            public TMesh mesh; public Mesh m; public Material mat;
            public Vector3[] pos, nrm, bary, info, objN; public Vector2[] uv;
            public float[] crack, shardT; public Vector3[] shardPos, vel, spin;
            public byte[] gone; public float[] fc;
            public string state = "off"; public float fadeT, formT; public int hitI, stage;
            public Vector3 P; public float yaw, radius = (float)AEGIS.radius, life = 0.6f, grav = 10, alpha = 1, weak;
            public readonly Vector4[] hits = { new Vector4(0, 1, 0, -99), new Vector4(0, 1, 0, -99), new Vector4(0, 1, 0, -99), new Vector4(0, 1, 0, -99) };
        }

        public AegisFX(Fx fx) { this.fx = fx; scene = fx.scene; tex = CrackTexture(); }

        Dome MakeDome()
        {
            BaseGeometry();
            int n = F * 3;
            var d = new Dome
            {
                pos = new Vector3[n], nrm = new Vector3[n], bary = new Vector3[n], info = new Vector3[n], objN = new Vector3[n], uv = new Vector2[n],
                crack = new float[F], shardT = new float[F], shardPos = new Vector3[F], vel = new Vector3[F], spin = new Vector3[F], gone = new byte[F], fc = new float[F],
            };
            var tris = new int[n];
            for (int f = 0; f < F; f++)
            {
                // (three.js fronts are counter-clockwise; once mirrored they need the other order)
                tris[f * 3] = f * 3; tris[f * 3 + 1] = f * 3 + 2; tris[f * 3 + 2] = f * 3 + 1;
                for (int k = 0; k < 3; k++)
                {
                    int v = f * 3 + k;
                    d.bary[v] = k == 0 ? new Vector3(1, 0, 0) : k == 1 ? new Vector3(0, 1, 0) : new Vector3(0, 0, 1);
                    d.uv[v] = uvBase[v]; d.objN[v] = centers[f].normalized;
                }
            }
            d.m = new Mesh { name = "aegis" }; d.m.MarkDynamic();
            d.m.vertices = d.pos; d.m.normals = d.nrm; d.m.uv = d.uv; d.m.SetUVs(1, new List<Vector3>(d.bary)); d.m.SetUVs(2, new List<Vector3>(d.info)); d.m.SetUVs(3, new List<Vector3>(d.objN));
            d.m.triangles = tris; d.m.bounds = new Bounds(Vector3.zero, Vector3.one * 100000);
            d.mat = new Material(Templates.Aegis);
            d.mat.SetTexture("_CrackTex", tex); d.mat.SetColor("_Color", Th.Hex(GOLD)); d.mat.SetColor("_Hot", Th.Hex("#fff6e0"));
            d.mesh = new TMesh(d.m, TMat.Raw(d.mat)); d.mesh.go.name = "aegis"; d.mesh.RenderOrder = 5; d.mesh.visible = false; scene.add(d.mesh);
            Reset(d);
            return d;
        }
        static void Reset(Dome d)
        {
            for (int f = 0; f < F; f++) { d.shardT[f] = -1; d.crack[f] = 0; d.fc[f] = 0; d.gone[f] = 0; d.vel[f] = Vector3.zero; d.spin[f] = Vector3.zero; }
            for (int i = 0; i < 4; i++) d.hits[i].w = -99;
        }
        Dome Of(Player p) { if (!domes.TryGetValue(p, out var d)) { d = MakeDome(); domes[p] = d; } return d; }

        // The shield's centre (his chest) and its turn about the vertical
        static void Place(Player p, Dome d) { d.P = S.W(p.x, p.y + p.h * 0.62, 0); d.yaw = S.YawAt(p.x); }
        static Quaternion Rot(Dome d) => Quaternion.AngleAxis(d.yaw * Mathf.Rad2Deg, Vector3.up);   // (three.js space)
        // A sim-plane direction (dx, dy) at x as a unit vector in the dome's own space
        static Vector3 LocalDir(Player p, Dome d, double dx, double dy) => Quaternion.Inverse(Rot(d)) * S.Dir(p.x, dx, dy).normalized;

        void Detach(Dome d, int f, float now, float speed, float life, float spread = 1.2f, float up = 1)
        {
            if (d.gone[f] != 0) return;
            d.gone[f] = 1;
            var R = Rot(d);
            var wc = d.P + R * (centers[f] * (float)AEGIS.radius);
            var nrm = R * centers[f].normalized;
            d.vel[f] = new Vector3(nrm.x * speed + (S.Rnd() - 0.5f) * spread, nrm.y * speed + (S.Rnd() - 0.2f) * spread * up, nrm.z * speed + (S.Rnd() - 0.5f) * spread);
            var ax = new Vector3(S.Rnd() - 0.5f, S.Rnd() - 0.5f, S.Rnd() - 0.5f); float am = ax.magnitude; if (am == 0) am = 1; float rate = 6 + S.Rnd() * 10;
            d.spin[f] = ax / am * rate; d.shardPos[f] = wc; d.shardT[f] = now;
            d.life = life;
        }

        public void OnEvent(Ev ev)
        {
            var p = ev.p;
            switch (ev.type)
            {
                case "aegisOn":
                {
                    var d = Of(p); Reset(d); d.state = "up"; d.formT = 0; d.stage = 0; d.mesh.visible = true; d.alpha = 0; d.weak = 0;
                    Place(p, d);
                    double cx = p.x, cy = p.y + p.h * 0.62; float R = (float)AEGIS.radius;
                    fx.Sprite(cx, cy, "ring", "#fff1c9", R * 1.4f, 0.3f, 1.6f);
                    fx.Sprite(cx, cy, "glow", GOLD, R * 2.2f, 0.22f, 1.2f);
                    fx.Burst(cx, cy, "#fff1c9", 22, 5, 0.26f, 0.3f);
                    break;
                }
                case "aegisHit":
                {
                    var d = Of(p); if (d.state != "up") break;
                    Place(p, d);
                    var L = LocalDir(p, d, ev.dx, ev.dy); float now = t;
                    d.hits[d.hitI] = new Vector4(L.x, L.y, L.z, now); d.hitI = (d.hitI + 1) % 4;
                    float amount = 0.35f + (float)(ev.dmg / AEGIS.hp) * 3.2f, cos = Mathf.Cos(0.95f), floor = (1 - (float)ev.frac) * 0.42f;
                    for (int f = 0; f < F; f++)
                    {
                        if (d.gone[f] != 0) continue;
                        float dot = Vector3.Dot(centers[f], L), c = d.fc[f];
                        if (dot > cos) { float k = (dot - cos) / (1 - cos); c += amount * k * k * (0.7f + S.Rnd() * 0.6f); }
                        c = Mathf.Max(c, floor * (0.6f + 0.8f * ((f * 0.618f) % 1)));
                        d.fc[f] = c; d.crack[f] = c;
                        if (c > 1.2f) Detach(d, f, now, 2.2f + S.Rnd() * 1.5f, 0.55f, 1.6f);
                    }
                    int stage = ev.frac < 0.34 ? 2 : ev.frac < 0.67 ? 1 : 0;
                    if (stage > d.stage)
                    {
                        d.stage = stage;
                        var order = new List<int>(); for (int f = 0; f < F; f++) if (d.gone[f] == 0) order.Add(f);
                        order.Sort((a, b) => d.fc[b].CompareTo(d.fc[a]));
                        for (int i = 0; i < Mathf.Min(order.Count, 10 + stage * 8); i++) Detach(d, order[i], now, 2.5f + S.Rnd() * 2, 0.6f, 2);
                        fx.Sprite(ev.x, ev.y, "star", "#ffffff", 1.3f, 0.16f, 1.4f);
                    }
                    d.weak = ev.frac < 0.3 ? 1 : ev.frac < 0.5 ? 0.4f : 0;
                    float ax = Mathf.Atan2((float)ev.dy, (float)ev.dx);
                    fx.Burst(ev.x, ev.y, "#fff6e0", 10 + Mathf.Round((float)ev.dmg), 6 + (float)ev.dmg * 0.3f, 0.22f, 0.28f, dir: ax, spread: 2.2f, grav: 6);
                    fx.Sprite(ev.x, ev.y, "star", "#fff1c9", 0.6f + (float)ev.dmg * 0.04f, 0.14f, 1.5f);
                    Absorb(p, ev.x, ev.y, 8 + Mathf.Round((float)ev.dmg * 0.8f));
                    break;
                }
                case "aegisOff":
                {
                    if (!domes.TryGetValue(p, out var d) || d.state != "up") break;
                    Place(p, d);
                    float now = t; double cx = ev.x, cy = ev.y; float R = (float)AEGIS.radius;
                    if (ev.why == "break")
                    {
                        d.grav = 12;
                        for (int f = 0; f < F; f++) Detach(d, f, now, 3 + S.Rnd() * 4, 0.85f, 2.5f, 1.5f);
                        d.fadeT = 0.9f;
                        fx.Sprite(cx, cy, "star", "#ffffff", R * 2.4f, 0.2f, 1.5f);
                        fx.Sprite(cx, cy, "ring", "#fff1c9", R * 1.2f, 0.35f, 3.2f);
                        fx.Burst(cx, cy, "#fff6e0", 40, 11, 0.24f, 0.45f, grav: 9);
                        fx.Burst(cx, cy, GOLD, 26, 8, 0.3f, 0.5f, grav: 7);
                    }
                    else if (ev.why == "detonate")
                    {
                        d.grav = 2;
                        for (int f = 0; f < F; f++) Detach(d, f, now, 9 + S.Rnd() * 5, 0.42f, 1.5f, 0.5f);
                        d.fadeT = 0.5f;
                        fx.Sprite(cx, cy, "ring", "#ffffff", R * 1.1f, 0.32f, 3.6f);
                        fx.Sprite(cx, cy, "glow", GOLD, R * 3.2f, 0.25f, 1.6f);
                        fx.Sprite(cx, cy, "star", "#ffffff", R * 2.8f, 0.18f, 1.4f);
                        fx.GroundRing(p.x, p.y, "#fff1c9", 0.4f, (float)AEGIS.detonate.r * 1.3f, 0.35f, 0.9f);
                        fx.Burst(cx, cy, "#fff1c9", 36, 14, 0.3f, 0.35f);
                        Absorb(p, cx, cy, 16);
                    }
                    else
                    {
                        d.grav = -1.5f;
                        for (int f = 0; f < F; f++) Detach(d, f, now + S.Rnd() * 0.15f, 0.4f + S.Rnd() * 0.4f, 0.4f, 0.6f, 0.5f);
                        d.fadeT = 0.6f;
                    }
                    d.state = "fading";
                    break;
                }
            }
        }

        // Energy absorbed by the shield flows into the bracer: sparks that fly from (x, y) to his muzzle
        void Absorb(Player p, double x, double y, float n)
        {
            var rig = fx.RigOf(p); var to = rig != null && rig.extra.muzzle != null ? rig.extra.muzzle.worldPos : S.W(p.x, p.y + 1.1, 0.25);
            var from = S.W(x, y, 0.2);
            for (int i = 0; i < n; i++)
            {
                float life = 0.16f + S.Rnd() * 0.14f; var o = new Vector3((S.Rnd() - 0.5f) * 0.5f, (S.Rnd() - 0.5f) * 0.5f, (S.Rnd() - 0.5f) * 0.3f);
                var P = fx.Particle(from + o, S.Rnd() < 0.5f ? "#ffffff" : "#ffe2a8", 0.17f, life);
                P.v = (to - from - o) / life; P.drag = 1; P.grav = 0;
            }
        }

        // Each frame: lay out every panel (on the dome, or flying as a shard) and update the shader's inputs
        void Build(Dome d)
        {
            var R = Rot(d);
            for (int f = 0; f < F; f++)
            {
                var c = centers[f];
                for (int k = 0; k < 3; k++)
                {
                    int v = f * 3 + k; var lp = faces3[v];
                    Vector3 wpos, wn; float fade;
                    if (d.shardT[f] >= 0)
                    {
                        float tt = Mathf.Max(0, t - d.shardT[f]), sp = d.spin[f].magnitude;
                        var Rq = Quaternion.AngleAxis(sp * tt * Mathf.Rad2Deg, sp > 0 ? d.spin[f] / sp : Vector3.up);
                        var off = R * ((lp - c) * d.radius);
                        wpos = d.shardPos[f] + d.vel[f] * tt + new Vector3(0, -0.5f * d.grav * tt * tt, 0) + Rq * off * (1 - 0.5f * Mathf.Clamp01(tt / d.life));
                        wn = Rq * (R * c.normalized);
                        fade = Mathf.Clamp01(1 - tt / d.life);
                    }
                    else { wpos = d.P + R * (lp * d.radius); wn = R * lp; fade = 1; }
                    d.pos[v] = S.ToUnity(wpos); d.nrm[v] = S.ToUnity(wn);
                    d.info[v] = new Vector3(d.crack[f], d.shardT[f] >= 0 ? 1 : 0, fade);
                }
            }
            d.m.vertices = d.pos; d.m.normals = d.nrm; d.m.SetUVs(2, new List<Vector3>(d.info));
            d.m.bounds = new Bounds(Vector3.zero, Vector3.one * 100000);
            d.mat.SetFloat("_T", t); d.mat.SetFloat("_Alpha", d.alpha); d.mat.SetFloat("_Weak", d.weak);
            d.mat.SetVectorArray("_Hits", d.hits);
        }

        public void Update(float dt, World world)
        {
            t += dt;
            var seen = new HashSet<Player>();
            foreach (var p in world.players)
            {
                seen.Add(p);
                domes.TryGetValue(p, out var d);
                if (p.aegis != null && d != null && d.state == "up")
                {
                    Place(p, d);
                    d.formT += dt;
                    float k = Mathf.Min(1, d.formT / 0.12f), frac = (float)(p.aegis.hp / p.aegis.max), tLeft = (float)p.aegis.t / 60;
                    d.radius = (float)AEGIS.radius * (0.7f + 0.3f * (1 - (1 - k) * (1 - k))) * (1 + Mathf.Sin(t * 5) * 0.008f);
                    d.alpha = k * (tLeft < 1 ? 0.55f + 0.45f * (Mathf.Sin(t * 30) > 0 ? 1 : 0) : 1) * (0.75f + 0.25f * frac);
                    d.mesh.visible = p.state != "dead" && p.state != "downed";
                }
                else if (d != null && d.state == "up" && p.aegis == null) { d.state = "off"; d.mesh.visible = false; }
                if (p.overcharge > 0 && p.state != "dead" && p.state != "downed")
                {
                    var rig = fx.RigOf(p);
                    if (rig != null && rig.root.visible && rig.extra.muzzle != null && S.Rnd() < 0.25f + (float)p.overcharge / 160)
                    {
                        var P = fx.Particle(rig.extra.muzzle.worldPos, S.Rnd() < 0.5f ? "#ffffff" : "#ffe2a8", 0.12f + S.Rnd() * 0.08f, 0.14f + S.Rnd() * 0.1f);
                        P.v = new Vector3((S.Rnd() - 0.5f) * 4, S.Rnd() * 3, (S.Rnd() - 0.5f) * 2); P.drag = 0.86f; P.grav = 4;
                    }
                }
            }
            foreach (var kv in new List<KeyValuePair<Player, Dome>>(domes))
            {
                var d = kv.Value;
                if (d.state == "fading") { d.fadeT -= dt; if (d.fadeT <= 0) { d.state = "off"; d.mesh.visible = false; } }
                if (d.mesh.visible) Build(d);
                if (!seen.Contains(kv.Key) && d.state != "fading") { d.mesh.destroy(); domes.Remove(kv.Key); }
            }
        }
    }
}
