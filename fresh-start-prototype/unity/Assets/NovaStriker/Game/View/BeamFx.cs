// The Level 4 beams (beamfx.js): Nova's, and RAM's broader Breach Beam in his blue. A white-hot core inside a
// surging sheath in the attachment's colour, following the sim's beam path (a Prism beam bounces once). It snaps
// open, flickers and pulses along its length, pushes rings down its path, streams sparks, flares at the bracer and
// throws sparks and smoke where it hits a wall; when it ends it narrows to nothing. Presentation only.
using System.Collections.Generic;
using NovaStriker.Game.Three;
using NovaStriker.Sim;
using UnityEngine;
using static NovaStriker.Sim.Cfg;

namespace NovaStriker.Game
{
    public sealed class BeamFX
    {
        readonly Fx fx; readonly TObj scene; float t;
        sealed class B
        {
            public Strip edge, sheath, core; public TMesh glow, core2, hit;
            public readonly List<Vector3> pts = new List<Vector3>(); public float age, end = -1, ringT, W; public Color tint; public bool ram;
            public List<BeamSeg> segs; public double dx, dy, px;
        }
        readonly Dictionary<Player, B> beams = new Dictionary<Player, B>();
        public BeamFX(Fx fx) { this.fx = fx; scene = fx.scene; }

        B Of(Player p)
        {
            if (beams.TryGetValue(p, out var b)) return b;
            TMesh Sp(Texture tex, Blending bl) { var s = Three.Sprite.Make(new TMat(TMat.Kind.Sprite) { map = tex, depthWrite = false, blending = bl }); s.visible = false; s.RenderOrder = 6; scene.add(s); return s; }
            b = new B { edge = new Strip(scene, 3), sheath = new Strip(scene, 4), core = new Strip(scene, 5), glow = Sp(fx.tex.glow, Blending.Normal), core2 = Sp(fx.tex.glow, Blending.Additive), hit = Sp(fx.tex.star, Blending.Additive) };
            beams[p] = b; return b;
        }

        public void OnEvent(Ev ev)
        {
            if (ev.type == "beamStart")
            {
                var b = Of(ev.p); b.age = 0; b.end = -1;
                b.tint = S.Lin(ev.attach == "breach" ? CHARS["ram"].energy : ev.attach != null && ATTACH_LOOK.ContainsKey(ev.attach) ? ATTACH_LOOK[ev.attach].tint : "#ffd889");
                b.ram = ev.p.@char == "ram"; b.W = (float)(b.ram ? RAM.beam.width : MARKSMAN.beam.width);
                if (ev.over) b.tint = Color.Lerp(b.tint, Color.white, 0.35f);
            }
            else if (ev.type == "beamEnd") { if (beams.TryGetValue(ev.p, out var b)) b.end = 0; }
        }

        // The beam's path in world points: along each sim segment every ~0.7 m
        static void Path(Player p, B b)
        {
            var pts = b.pts; pts.Clear();
            foreach (var g in p.beam.segs)
            {
                double len = JMath.Hypot(g.x1 - g.x0, g.y1 - g.y0); int n = System.Math.Max(1, (int)System.Math.Ceiling(len / 0.7));
                for (int i = pts.Count > 0 ? 1 : 0; i <= n && pts.Count < Strip.MAXP; i++) pts.Add(S.W(g.x0 + (g.x1 - g.x0) * i / n, g.y0 + (g.y1 - g.y0) * i / n, LevelFeatures.Depth(p) + 0.2));
            }
        }

        public void Update(float dt, World world, View view)
        {
            t += dt;
            var cam = view.camPos;
            foreach (var kv in new List<KeyValuePair<Player, B>>(beams))
            {
                var p = kv.Key; var b = kv.Value;
                bool live = p.beam != null && p.state == "beam" && p.beam.segs != null && p.beam.segs.Count > 0 && world.players.Contains(p);
                if (live) { b.age += dt; Path(p, b); b.segs = new List<BeamSeg>(p.beam.segs); b.dx = p.beam.dx; b.dy = p.beam.dy; b.px = p.x; }
                else if (b.end < 0 && b.pts.Count > 0) b.end = 0;
                if (b.end >= 0) b.end += dt;
                float closing = b.end >= 0 ? Mathf.Max(0, 1 - b.end / 0.16f) : 1;
                if (b.pts.Count == 0 || closing <= 0)
                {
                    b.edge.mesh.visible = b.sheath.mesh.visible = b.core.mesh.visible = b.glow.visible = b.core2.visible = b.hit.visible = false;
                    if (closing <= 0) b.pts.Clear();
                    if (!world.players.Contains(p) && closing <= 0) { foreach (var o in new TObj[] { b.edge.mesh, b.sheath.mesh, b.core.mesh, b.glow, b.core2, b.hit }) o.DestroyOwnedMaterials(); beams.Remove(p); }
                    continue;
                }
                float open = Mathf.Min(1, b.age / 0.07f), W = b.W; int n = b.pts.Count; var tint = b.tint;
                float bse = open * closing;
                if (b.ram) b.edge.Build(b.pts, cam, i => W * 1.3f * bse * Mathf.Min(1, 0.45f + i * 0.25f) * (1 + 0.1f * Mathf.Sin(t * 30 - i * 0.7f)),
                    i => new Vector4(tint.r * 0.25f, tint.g * 0.3f, tint.b * 0.55f, 0.75f * Mathf.Min(1, 0.4f + i * 0.3f) * (i == n - 1 ? 0.3f : 1)));
                b.edge.mesh.visible = b.ram;
                float sk = b.ram ? 1.05f : 1.5f;
                b.sheath.Build(b.pts, cam, i => W * 1.0f * bse * Mathf.Min(1, 0.45f + i * 0.25f) * (1 + 0.16f * Mathf.Sin(t * 38 - i * 0.9f) + 0.06f * Mathf.Sin(t * 91 + i)),
                    i => new Vector4(tint.r * sk, tint.g * sk, tint.b * sk, (b.ram ? 0.8f : 0.5f) * Mathf.Min(1, 0.4f + i * 0.3f) * (i == n - 1 ? 0.3f : 1)));
                float flick = 0.85f + S.Rnd() * 0.3f;
                b.core.Build(b.pts, cam, i => W * (b.ram ? 0.24f : 0.36f) * bse * flick * Mathf.Min(1, 0.5f + i * 0.3f), i => new Vector4(2.6f, 2.5f, 2.3f, 0.95f));
                Vector3 a = b.pts[0], e = b.pts[n - 1];
                b.glow.position.copy(a); b.glow.material.colorLin = tint; b.glow.scale.setScalar((1.0f + Mathf.Sin(t * 40) * 0.12f) * bse); b.glow.material.opacity = 0.7f; b.glow.visible = true;
                b.core2.position.copy(a); b.core2.material.colorCss = "#ffffff"; b.core2.scale.setScalar(0.9f * bse * flick); b.core2.visible = true;
                var endSeg = b.segs[b.segs.Count - 1];
                b.hit.visible = endSeg.wall; b.hit.position.copy(e); b.hit.material.colorLin = Color.Lerp(tint, Color.white, 0.5f);
                b.hit.scale.setScalar((1.2f + S.Rnd() * 0.6f) * bse); b.hit.material.rotation = t * 7;
                if (!live) continue;
                string tintCss = "#" + ColorUtility.ToHtmlStringRGB(tint.gamma);
                for (int i = 0; i < 4; i++)
                {
                    int j = Mathf.FloorToInt(S.Rnd() * (n - 1)); var q = b.pts[j];
                    var P = fx.Particle(q, S.Rnd() < 0.5f ? "#ffffff" : tintCss, 0.16f + S.Rnd() * 0.12f, 0.12f);
                    P.v = (b.pts[Mathf.Min(n - 1, j + 1)] - q).normalized * (26 + S.Rnd() * 14);
                    P.v.x += (S.Rnd() - 0.5f) * 3; P.v.y += (S.Rnd() - 0.5f) * 3; P.drag = 0.95f; P.grav = 0;
                }
                b.ringT -= dt;
                if (b.ringT <= 0)
                {
                    b.ringT = 0.07f;
                    fx.charge.ShockRing(a, S.Dir(b.px, b.dx, b.dy).normalized, tintCss, 0.14f, 0.38f, 0.2f, 4.5f);
                }
                foreach (var g in b.segs)
                {
                    if (!g.wall) continue;
                    float outA = Mathf.Atan2((float)g.ny, (float)g.nx);
                    fx.Burst(g.x1, g.y1, S.Rnd() < 0.5f ? "#ffffff" : tintCss, 3, 9, 0.2f, 0.25f, dir: outA, spread: 2.2f, grav: 12);
                    if (S.Rnd() < 0.35f) fx.Smoke(g.x1 + g.nx * 0.2, g.y1 + g.ny * 0.2, "#8e97a3", 1, 1.5f, 0.5f, 0.6f, dir: outA, spread: 1.4f, op: 0.4f);
                }
            }
            // a beam whose player has no entry yet appears on its start event (OnEvent makes it)
        }
    }
}
