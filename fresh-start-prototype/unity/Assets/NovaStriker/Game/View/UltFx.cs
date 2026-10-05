// The ultimates (ultfx.js). The call: a pillar of light and rising energy around each caster (the view dims the
// rest of the world). Nova's Supernova: light gathering into a small sun over him, the colossal beam he steers,
// then the nova. Echo's Thousand Cuts: a storm of cuts, each leaving an afterimage of him mid-strike beside its
// target, then every cut at once. A team ultimate ends in an eclipse over the screen that shatters into light.
using System.Collections.Generic;
using NovaStriker.Game.Three;
using NovaStriker.Sim;
using UnityEngine;
using static NovaStriker.Sim.Cfg;

namespace NovaStriker.Game
{
    public sealed class UltFX
    {
        const string CYAN = "#7fe3ff";
        static string GOLD => CHARS["nova"].energy;
        static string ORANGE => CHARS["echo"].energy;
        readonly Fx fx; readonly TObj scene; float t;
        sealed class B { public Strip halo, sheath, core; public TMesh sun, sunCore, flare; public readonly List<Vector3> pts = new List<Vector3>(); public float ringT; }
        readonly Dictionary<Player, B> beams = new Dictionary<Player, B>();
        readonly List<(float t, System.Action fn)> pending = new List<(float, System.Action)>();
        readonly Dictionary<Player, (double x, double y)> lastCut = new Dictionary<Player, (double, double)>();
        sealed class Eclipse { public TMesh disc, corona, ring; public float t = -1; public double x, y; }
        readonly Eclipse eclipse;

        public UltFX(Fx fx)
        {
            this.fx = fx; scene = fx.scene;
            // The eclipse: a solid dark disc over a corona of light, with a thin bright rim
            var solid = Fx.CanvasTex(128, (g, s) =>
            {
                var grad = g.createRadialGradient(64, 64, 54, 64, 64, 64);
                grad.addColorStop(0, "rgba(255,255,255,1)"); grad.addColorStop(1, "rgba(255,255,255,0)");
                g.fillStyle = grad; g.beginPath(); g.arc(64, 64, 64, 0, Mathf.PI * 2); g.fill();
                g.fillStyle = "#ffffff"; g.beginPath(); g.arc(64, 64, 54, 0, Mathf.PI * 2); g.fill();
            });
            TMesh Sp(Texture map, Blending blend, uint color, int order)
            {
                var s = Three.Sprite.Make(new TMat(TMat.Kind.Sprite) { map = map, colorHex = color, depthWrite = false, depthTest = false, blending = blend });
                s.visible = false; s.RenderOrder = order; scene.add(s); return s;
            }
            eclipse = new Eclipse { disc = Sp(solid, Blending.Normal, 0x02040a, 12), corona = Sp(fx.tex.glow, Blending.Additive, 0xbff4ff, 11), ring = Sp(fx.tex.ring, Blending.Additive, 0xffffff, 12) };
        }
        B BeamOf(Player p)
        {
            if (beams.TryGetValue(p, out var b)) return b;
            TMesh Sp(Texture tex) { var s = Three.Sprite.Make(new TMat(TMat.Kind.Sprite) { map = tex, depthWrite = false, blending = Blending.Additive }); s.visible = false; s.RenderOrder = 7; scene.add(s); return s; }
            b = new B { halo = new Strip(scene, 4), sheath = new Strip(scene, 5), core = new Strip(scene, 6), sun = Sp(fx.tex.glow), sunCore = Sp(fx.tex.glow), flare = Sp(fx.tex.star) };
            beams[p] = b; return b;
        }
        void After(float secs, System.Action fn) => pending.Add((secs, fn));

        public void OnEvent(Ev ev)
        {
            var F = fx;
            switch (ev.type)
            {
                case "ultCast": case "ultJoin":
                {
                    var p = ev.p; double x = p.x, y = p.y; string col = CHARS[p.@char].energy;
                    F.Sprite(x, y + 3, "glow", CYAN, 7.5f, 0.9f, 1.08f, 0.1f, 0.16f); F.Sprite(x, y + 3, "glow", "#ffffff", 7, 0.5f, 1.05f, 0.12f, 0.05f);
                    F.Sprite(x, y + 1, "star", "#ffffff", 3.2f, 0.3f, 1.6f); F.Sprite(x, y + 1, "ring", CYAN, 1.2f, 0.6f, 4.5f);
                    F.GroundRing(x, y, CYAN, 0.4f, 4.5f, 0.8f, 0.9f); F.GroundRing(x, y, col, 0.3f, 3, 0.6f, 0.8f);
                    F.Burst(x, y + 0.6, CYAN, 40, 7, 0.3f, 0.8f, dir: Mathf.PI / 2, spread: 1.4f, drag: 0.94f, grav: -4);
                    F.Burst(x, y + 1, col, 24, 5, 0.26f, 0.6f);
                    F.Dust(x, y, 0.8f);
                    break;
                }
                case "ultRun": foreach (var m in ev.members) F.Sprite(m.x, m.y + 1, "star", "#ffffff", 2.4f, 0.2f, 1.5f); break;
                case "ultBegin":
                    if (ev.kind == "echo")
                    {
                        var p = ev.p; F.Sprite(p.x, p.y + 1, "ring", ORANGE, 1.1f, 0.3f, 3); F.Burst(p.x, p.y + 1, ORANGE, 30, 8, 0.3f, 0.4f); F.Burst(p.x, p.y + 1, "#ffffff", 14, 6, 0.22f, 0.3f);
                        lastCut[p] = (p.x, p.y + 1);
                    }
                    break;
                case "ultNova":
                {
                    double x = ev.x, y = ev.y; float r = (float)ev.r;
                    F.Sprite(x, y, "star", "#ffffff", r * 1.6f, 0.35f, 1.6f); F.Sprite(x, y, "glow", "#fff4d6", r * 1.8f, 0.45f, 1.5f);
                    F.Fireball(x, y, "#ffd27a", r * 0.9f, 0.5f);
                    foreach (var (sz, life, g) in new[] { (r * 0.5f, 0.45f, 4.4f), (r * 0.35f, 0.65f, 6f), (r * 0.2f, 0.8f, 8f) }) F.Sprite(x, y, "ring", "#ffffff", sz, life, g);
                    F.Sprite(x, y, "ring", CYAN, r * 0.4f, 0.7f, 5.5f);
                    F.Burst(x, y, GOLD, 90, 18, 0.45f, 0.8f, grav: 4); F.Burst(x, y, "#ffffff", 50, 14, 0.3f, 0.5f);
                    var fl = F.FloorUnder(x, y, 4);
                    if (fl != null) { F.GroundRing(x, fl.Value, "#fff1c9", 0.5f, r * 1.3f, 0.6f, 0.95f); F.Dust(x, fl.Value, 1.4f, null, reach: 4); }
                    F.Smoke(x, y, "#8e97a3", 14, 2.5f, 1.2f, 1.4f, op: 0.45f, grow: 2.6f, grav: -0.8f);
                    break;
                }
                case "ultCut": Cut(ev); break;
                case "ultFinisher":
                {
                    var p = ev.p;
                    if (ev.flourish) { F.Sprite(p.x, p.y + 1, "ring", ORANGE, 2, 0.4f, 3.6f); F.SlashMark(p.x, p.y + 1, "#fff1d6", 9, 0.1f, 0.3f); F.Burst(p.x, p.y + 1, ORANGE, 40, 12, 0.35f, 0.5f); break; }
                    foreach (var e in ev.targets)
                    {
                        double x = e.x, y = e.y + e.h * 0.55;
                        F.SlashMark(x, y, "#fff1d6", 3.6f, 0.7f, 0.28f); F.SlashMark(x, y, ORANGE, 3.6f, -0.7f, 0.28f); F.SlashMark(x, y, "#ffffff", 4.4f, 0, 0.2f);
                        F.Sprite(x, y, "star", "#ffffff", 2.4f, 0.2f, 1.5f); F.Burst(x, y, ORANGE, 30, 10, 0.34f, 0.45f); F.Burst(x, y, "#ffffff", 14, 8, 0.24f, 0.3f);
                    }
                    double mid = p.x;
                    if (ev.targets.Count > 0) { mid = 0; foreach (var e in ev.targets) mid += e.x; mid /= ev.targets.Count; }
                    F.SlashMark(mid, p.y + 1.2, "#ffffff", 26, 0, 0.3f);
                    F.Sprite(p.x, p.y + 1, "ring", ORANGE, 1.4f, 0.35f, 3.4f); F.Burst(p.x, p.y + 1, ORANGE, 20, 6, 0.28f, 0.35f);
                    break;
                }
                case "ultEnd": F.Sprite(ev.p.x, ev.p.y + 1, "ring", CYAN, 0.9f, 0.3f, 2.8f); break;
                case "teamFinisher": TeamFinisher(ev); break;
            }
        }

        // One of Echo's cuts: a long slash across the target, an afterimage of him beside it mid-strike, sparks,
        // and a streak of light from where the last cut was
        void Cut(Ev ev)
        {
            var F = fx; var p = ev.p; double x = ev.x, y = ev.y; double side = -ev.dir; var rig = fx.RigOf(p);
            F.SlashMark(x, y, "#fff1d6", 3 + S.Rnd(), (float)ev.ang, 0.2f); F.SlashMark(x, y, ORANGE, 2.2f, (float)ev.ang + 0.25f, 0.16f);
            F.Sprite(x, y, "star", "#ffffff", 1.1f, 0.12f, 1.4f);
            F.Burst(x, y, S.Rnd() < 0.5f ? "#ffffff" : ORANGE, 12, 9, 0.24f, 0.24f);
            double gx = x + side * 1.1, gy = ev.e.y;
            if (rig != null)
            {
                // Stand the (hidden) rig beside the target for a moment and leave a ghost of it there
                var pos = rig.root.position.v; float rot = rig.root.rotation.y, fl = rig.flip.scale.x;
                rig.root.position.copy(S.W(gx, gy, 0)); rig.flip.scale.x = (float)-side;
                F.ghosts.Spawn(rig, S.Lin(ORANGE) * 1.8f, 0.6f, 0.3f);
                rig.root.position.copy(pos); rig.root.rotation.y = rot; rig.flip.scale.x = fl;
            }
            if (lastCut.TryGetValue(p, out var last))
            {
                const int n = 6;
                for (int i = 0; i <= n; i++) { double u = (double)i / n; F.Burst(last.x + (gx - last.x) * u, last.y + (gy + 1 - last.y) * u, i % 2 == 1 ? "#ffffff" : ORANGE, 1, 1, 0.18f, 0.16f); }
            }
            lastCut[p] = (gx, gy + 1);
        }

        // The team finisher: an eclipse fills the screen, its corona flares, then it shatters into light
        void TeamFinisher(Ev ev)
        {
            var E = eclipse; var F = fx;
            E.t = 0; E.x = ev.x; E.y = ev.y;
            double x = ev.x, y = ev.y; var chars = ev.chars;
            After(0.42f, () =>
            {
                F.Sprite(x, y, "star", "#ffffff", 14, 0.45f, 1.6f); F.Sprite(x, y, "glow", "#ffffff", 16, 0.5f, 1.4f);
                foreach (var (sz, life, g) in new[] { (3f, 0.5f, 5f), (2f, 0.7f, 7f), (1.2f, 0.9f, 10f) }) F.Sprite(x, y, "ring", "#ffffff", sz, life, g);
                F.Sprite(x, y, "ring", CYAN, 2.4f, 0.9f, 7);
                F.Burst(x, y, CYAN, 120, 22, 0.45f, 0.9f); F.Burst(x, y, "#ffffff", 60, 16, 0.3f, 0.6f);
                if (chars != null) foreach (var c in chars) F.Burst(x, y, CHARS[c].energy, 50, 18, 0.38f, 0.7f);
            });
        }

        public void Update(float dt, World world, View view)
        {
            t += dt;
            var F = fx; var cam = view.camPos;
            for (int i = 0; i < pending.Count; i++) pending[i] = (pending[i].t - dt, pending[i].fn);
            var due = pending.FindAll(q => q.t <= 0); pending.RemoveAll(q => q.t <= 0);
            foreach (var q in due) q.fn();
            var E = eclipse;
            if (E.t >= 0)
            {
                E.t += dt; float k = E.t, grow = Mathf.Min(1, k / 0.3f), gone = k > 0.42f ? Mathf.Max(0, 1 - (k - 0.42f) / 0.12f) : 1;
                foreach (var s in new[] { E.disc, E.corona, E.ring }) { s.position.copy(S.W(E.x, E.y, 1.5)); s.visible = gone > 0; }
                E.disc.scale.setScalar(6.4f * grow); E.disc.material.opacity = gone;
                E.corona.scale.setScalar(11.5f * grow * (1 + 0.05f * Mathf.Sin(t * 40))); E.corona.material.opacity = gone;
                E.ring.scale.setScalar(8 * grow); E.ring.material.opacity = 0.9f * gone; E.ring.material.rotation = t * 2;
                if (gone <= 0) { E.t = -1; foreach (var s in new[] { E.disc, E.corona, E.ring }) s.visible = false; }
            }
            var U = world.ultCast;
            if (U != null && U.phase == "cast")
                foreach (var m in U.members)
                    for (int i = 0; i < 3; i++)
                    {
                        var w = S.W(m.x + (S.Rnd() - 0.5f) * 1.6f, m.y + S.Rnd() * 0.4f, (S.Rnd() - 0.5f) * 0.8f);
                        var P = F.Particle(w, S.Rnd() < 0.5f ? CYAN : "#ffffff", 0.18f, 0.5f); P.v = new Vector3(0, 5 + S.Rnd() * 4, 0); P.drag = 0.97f;
                    }
            // Nova's Supernova
            foreach (var p in world.players)
            {
                var R = p.ultRun; bool live = p.state == "ult" && R != null && R.kind == "nova";
                if (!live && !beams.ContainsKey(p)) continue;
                var Bm = BeamOf(p);
                if (!live) { foreach (var s in new TObj[] { Bm.halo.mesh, Bm.sheath.mesh, Bm.core.mesh, Bm.sun, Bm.sunCore, Bm.flare }) s.visible = false; continue; }
                double cx = p.x, cy = p.y + p.h * 0.62;
                if (R.t <= ULT.nova.gather + 2)
                {
                    float k = Mathf.Min(1, (float)(R.t / ULT.nova.gather)); var at = S.W(cx, cy + 1.1, 0.3);
                    Bm.sun.position.copy(at); Bm.sun.material.colorCss = GOLD; Bm.sun.scale.setScalar(0.4f + 2.4f * k * (1 + 0.08f * Mathf.Sin(t * 50))); Bm.sun.visible = true;
                    Bm.sunCore.position.copy(at); Bm.sunCore.material.colorCss = "#ffffff"; Bm.sunCore.scale.setScalar(0.2f + 1.2f * k); Bm.sunCore.visible = true;
                    for (int i = 0; i < 4; i++)
                    {
                        float a = S.Rnd() * Mathf.PI * 2, r = 2.5f + S.Rnd() * 1.5f; var tv = S.Dir(cx, Mathf.Cos(a) * r, Mathf.Sin(a) * r);
                        var P = F.Particle(at + tv, S.Rnd() < 0.4f ? "#ffffff" : GOLD, 0.2f, 0.28f); P.v = tv * -3.4f; P.drag = 1;
                    }
                }
                else { Bm.sun.visible = false; Bm.sunCore.visible = false; }
                if (R.segs == null) { Bm.halo.mesh.visible = Bm.sheath.mesh.visible = Bm.core.mesh.visible = Bm.flare.visible = false; continue; }
                var g = R.segs[0]; double len = JMath.Hypot(g.x1 - g.x0, g.y1 - g.y0); int n = System.Math.Min(60, (int)System.Math.Ceiling(len / 0.8)); var pts = Bm.pts;
                pts.Clear();
                for (int i = 0; i <= n; i++) pts.Add(S.W(g.x0 + (g.x1 - g.x0) * i / n, g.y0 + (g.y1 - g.y0) * i / n, 0.25));
                float bt = (float)(R.t - ULT.nova.gather), open = Mathf.Min(1, bt / 6), close = Mathf.Min(1, ((float)ULT.nova.beam - bt) / 8), bse = Mathf.Max(0, open * close), W = (float)ULT.nova.width;
                Bm.halo.Build(pts, cam, i => W * 1.9f * bse * Mathf.Min(1, 0.35f + i * 0.2f), i => new Vector4(0.3f, 0.72f, 1.0f, 0.22f * Mathf.Min(1, 0.3f + i * 0.2f)));
                Bm.sheath.Build(pts, cam, i => W * bse * Mathf.Min(1, 0.4f + i * 0.25f) * (1 + 0.14f * Mathf.Sin(t * 34 - i * 0.7f) + 0.05f * Mathf.Sin(t * 87 + i)), i => new Vector4(1.35f, 0.92f, 0.38f, 0.62f));
                Bm.core.Build(pts, cam, i => W * 0.4f * bse * (0.9f + S.Rnd() * 0.2f) * Mathf.Min(1, 0.5f + i * 0.3f), i => new Vector4(2.1f, 2.0f, 1.75f, 1));
                Bm.flare.position.copy(pts[0]); Bm.flare.material.colorCss = "#ffffff"; Bm.flare.scale.setScalar((2.4f + S.Rnd()) * bse); Bm.flare.material.rotation = t * 5; Bm.flare.visible = true;
                for (int i = 0; i < 8; i++)
                {
                    int j = Mathf.FloorToInt(S.Rnd() * (pts.Count - 1)); var P = F.Particle(pts[j], S.Rnd() < 0.5f ? "#ffffff" : GOLD, 0.24f, 0.16f);
                    P.v = (pts[j + 1] - pts[j]).normalized * (30 + S.Rnd() * 16); P.v.x += (S.Rnd() - 0.5f) * 6; P.v.y += (S.Rnd() - 0.5f) * 6; P.drag = 0.95f;
                }
                Bm.ringT -= dt;
                if (Bm.ringT <= 0) { Bm.ringT = 0.06f; F.charge.ShockRing(pts[0], S.Dir(p.x, R.dx, R.dy).normalized, "#ffffff", 0.5f, 1.6f, 0.26f, 7); }
                for (double d = 1; d < len; d += 2.2)
                {
                    if (S.Rnd() > 0.2f) continue;
                    double x = g.x0 + (g.x1 - g.x0) * d / len, y = g.y0 + (g.y1 - g.y0) * d / len; var fl = F.FloorUnder(x, y, 2.5);
                    if (fl != null) F.Smoke(x, fl.Value + 0.1, "#b9c1cb", 1, 3, 0.4f, 0.5f, dir: g.x1 >= g.x0 ? 0.35f : Mathf.PI - 0.35f, spread: 0.6f, grav: -0.5f, op: 0.35f);
                }
            }
            foreach (var p in new List<Player>(beams.Keys)) if (!world.players.Contains(p)) { var Bm = beams[p]; foreach (var s in new TObj[] { Bm.halo.mesh, Bm.sheath.mesh, Bm.core.mesh, Bm.sun, Bm.sunCore, Bm.flare }) s.destroy(); beams.Remove(p); }
        }
    }
}
