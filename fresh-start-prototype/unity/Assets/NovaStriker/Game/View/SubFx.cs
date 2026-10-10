// Nova's secondary weapons, his dodge and the Solar Uppercut (subfx.js): chain lightning, the Gravity Well,
// enemies crackling with a chain's shock, slowed by a perfect dodge or caught in a well, the dodge's afterimages
// and the uppercut's boot jets. Presentation only.
using System.Collections.Generic;
using NovaStriker.Game.Three;
using NovaStriker.Sim;
using UnityEngine;
using static NovaStriker.Sim.Cfg;

namespace NovaStriker.Game
{
    public sealed class SubFX
    {
        static string GOLD => CHARS["nova"].energy;
        const string PHASE = "#cfeeff", SLOW = "#9fdcff";
        readonly Fx fx; readonly TObj scene; float t;
        sealed class Bolt { public Strip glow, core; public float life, max = 1; public List<(double x, double y)> pts = new List<(double, double)>(); public float level; public double depth; }
        readonly List<Bolt> bolts = new List<Bolt>();
        readonly Color chainTint;
        readonly Mesh coreGeo, discGeo; readonly TMat coreMat, discMat; readonly Texture2D discTex;
        sealed class WellM { public TObj g; public TMesh core, disc, rim, halo, glow; public float spin; }
        readonly Dictionary<Well, WellM> wells = new Dictionary<Well, WellM>();
        readonly Dictionary<Enemy, TMesh> auras = new Dictionary<Enemy, TMesh>();
        readonly Dictionary<Player, double> lastSt = new Dictionary<Player, double>();
        readonly Dictionary<Player, double> ghostTick = new Dictionary<Player, double>();

        public SubFX(Fx fx)
        {
            this.fx = fx; scene = fx.scene;
            for (int i = 0; i < 6; i++) bolts.Add(new Bolt { glow = new Strip(scene, 6), core = new Strip(scene, 7) });
            chainTint = S.Lin(SUB_LOOK["chain"].tint);
            // The well's accretion disc: bright at its inner edge, streaked with arcs so its spin shows
            discTex = Fx.CanvasTex(256, (g, s) =>
            {
                float c = s / 2f, r0 = s * 0.5f * 0.55f;
                var grad = g.createRadialGradient(c, c, r0, c, c, s / 2f);
                grad.addColorStop(0, "rgba(255,250,235,1)"); grad.addColorStop(0.25f, "rgba(255,214,140,0.8)"); grad.addColorStop(1, "rgba(255,170,60,0)");
                g.fillStyle = grad; g.beginPath(); g.arc(c, c, s / 2f, 0, Mathf.PI * 2); g.arc(c, c, r0, 0, Mathf.PI * 2, true); g.fill();
                g.lineCap = "round";
                for (int i = 0; i < 26; i++)
                {
                    float r = r0 + Random.value * (s / 2f - r0) * 0.85f, a = Random.value * Mathf.PI * 2;
                    g.strokeStyle = string.Format(System.Globalization.CultureInfo.InvariantCulture, "rgba(255,{0},{1},{2})", 200 + Mathf.Floor(Random.value * 55), 120 + Mathf.Floor(Random.value * 100), 0.35f + Random.value * 0.6f);
                    g.lineWidth = 1 + Random.value * 3; g.beginPath(); g.arc(c, c, r, a, a + 0.3f + Random.value * 1.1f); g.stroke();
                }
            });
            coreGeo = Geo.Sphere(1, 20, 14); coreMat = new TMat(TMat.Kind.Basic) { colorHex = 0x000000 };
            discGeo = Geo.Ring(0.55f, 1, 48, 1);
            discMat = new TMat(TMat.Kind.Basic) { map = discTex, transparent = true, blending = Blending.Additive, depthWrite = false, side = Side.Double, colorLin = new Color(1.6f, 1.3f, 1) };
        }

        public void OnEvent(Ev ev)
        {
            var F = fx;
            switch (ev.type)
            {
                case "subSwitch":
                {
                    var p = ev.p; var L = SUB_LOOK[ev.sub];
                    F.PopText(p.x, p.y + p.h + 0.7, L.name.ToUpperInvariant(), L.tint, 0.6f);
                    F.Sprite(p.x + p.facing * 0.35, p.y + 1.05, "ring", L.tint, 0.5f, 0.2f, 2); F.Burst(p.x + p.facing * 0.35, p.y + 1.05, L.tint, 6, 2.5f, 0.25f, 0.25f);
                    break;
                }
                case "grenadeThrow": F.Burst(ev.x, ev.y, SUB_LOOK["grenade"].tint, 6 + 3 * (float)ev.level, 4, 0.22f, 0.2f); F.Sprite(ev.x, ev.y, "ring", "#ffe2b8", 0.35f + 0.08f * (float)ev.level, 0.14f, 2); break;
                case "bounce":
                    if (ev.sp > 5) { F.Burst(ev.x, ev.y, "#ffe2b8", 4, 3, 0.18f, 0.18f, dir: Mathf.PI / 2, spread: 2); F.Dust(ev.x, ev.y, 0.12f, null, reach: 0.4f, noRing: true); }
                    break;
                case "frag":
                {
                    float k = (float)ev.level + (ev.perfect ? 1 : 0), r = (float)ev.r; string c = SUB_LOOK["grenade"].tint;
                    F.Fireball(ev.x, ev.y + 0.2, "#ff8a2e", 0.8f + r * 0.45f, 0.24f + 0.03f * k);
                    F.Sprite(ev.x, ev.y + 0.1, "star", "#ffffff", 0.8f + r * 0.5f, 0.14f, 1.5f);
                    F.Sprite(ev.x, ev.y + 0.1, "ring", "#ffe2b8", r * 0.9f, 0.26f, 2.4f);
                    F.Burst(ev.x, ev.y + 0.2, c, 16 + 6 * k, 7 + r * 2, 0.34f, 0.4f, grav: 8); F.Burst(ev.x, ev.y + 0.2, "#5d6674", 8 + 3 * k, 6, 0.26f, 0.6f, dir: Mathf.PI / 2, spread: 2.2f, grav: 18);
                    F.Smoke(ev.x, ev.y + 0.3, "#7d8692", 4 + 2 * k, 1.4f, 0.6f + 0.12f * k, 0.8f, dir: Mathf.PI / 2, spread: 1.6f, grav: -0.9f, op: 0.5f);
                    F.Dust(ev.x, ev.y, 0.35f + 0.12f * k, null, reach: 1 + r * 0.4f);
                    break;
                }
                case "cluster": F.Sprite(ev.x, ev.y + 0.3, "star", "#ffffff", 1.8f, 0.18f, 1.6f); F.Burst(ev.x, ev.y + 0.3, "#ffd28a", 18, 9, 0.3f, 0.3f, dir: Mathf.PI / 2, spread: 2.6f, grav: 10); break;
                case "chain": Chain(ev); break;
                case "discThrow": F.Sprite(ev.x, ev.y, "ring", SUB_LOOK["disc"].tint, 0.5f + 0.1f * (float)ev.level, 0.16f, 2.2f); F.Burst(ev.x, ev.y, "#fff1c9", 6 + 2 * (float)ev.level, 4, 0.2f, 0.2f); break;
                case "discRecall": F.Sprite(ev.x, ev.y, "ring", "#ffffff", 0.6f, 0.18f, 2.2f); break;
                case "discCatch": F.Sprite(ev.x, ev.y, "star", "#fff4d6", 0.9f, 0.12f, 1.4f); F.Burst(ev.x, ev.y, SUB_LOOK["disc"].tint, 8, 3, 0.2f, 0.22f); break;
                case "discFade": F.Burst(ev.x, ev.y, SUB_LOOK["disc"].tint, 10, 3, 0.2f, 0.3f); break;
                case "wellLaunch": F.Sprite(ev.x, ev.y, "glow", GOLD, 0.8f, 0.16f, 1.6f); break;
                case "wellOpen":
                {
                    float r = (float)ev.r;
                    F.Sprite(ev.x, ev.y, "ring", "#ffe7b0", r * 2.2f, 0.28f, 0.15f); F.Sprite(ev.x, ev.y, "star", "#ffffff", 1.4f, 0.14f, 1.3f);
                    for (int i = 0; i < 24; i++)
                    {
                        float a = S.Rnd() * Mathf.PI * 2, rr = r * (0.7f + S.Rnd() * 0.4f); var w = fx.W(ev.x + Mathf.Cos(a) * rr, ev.y + Mathf.Sin(a) * rr, 0.2);
                        var P = F.Particle(w, S.Rnd() < 0.4f ? "#ffffff" : GOLD, 0.2f, 0.3f); P.v = S.Dir(ev.x, -Mathf.Cos(a) * rr * 3.4f, -Mathf.Sin(a) * rr * 3.4f); P.drag = 0.96f;
                    }
                    break;
                }
                case "wellCollapse":
                {
                    float k = ev.level != 0 ? (float)ev.level : 1, r = (float)ev.r;
                    F.Sprite(ev.x, ev.y, "star", "#ffffff", 2 + 0.5f * k, 0.2f, 1.6f); F.Fireball(ev.x, ev.y, "#ffd27a", 1 + 0.4f * k, 0.3f);
                    F.Sprite(ev.x, ev.y, "ring", "#ffffff", r * 0.6f, 0.35f, 3.2f); F.Sprite(ev.x, ev.y, "ring", GOLD, r * 0.4f, 0.45f, 4);
                    F.Burst(ev.x, ev.y, GOLD, 24 + 8 * k, 10 + 2 * k, 0.35f, 0.45f, grav: 4); F.Burst(ev.x, ev.y, "#ffffff", 12 + 4 * k, 7, 0.24f, 0.3f);
                    F.Dust(ev.x, ev.y, 0.5f + 0.15f * k, null, reach: 2.2f);
                    break;
                }
                case "dodge":
                {
                    var p = ev.p;
                    F.Burst(p.x - ev.dx * 0.3, p.y + 0.9, PHASE, 10, 4, 0.22f, 0.25f, dir: ev.dx > 0 ? Mathf.PI : 0, spread: 1.2f);
                    if (!ev.air) F.Dust(p.x, p.y, 0.3f, new[] { ev.dx > 0 ? Mathf.PI : 0 }, noRing: true);
                    break;
                }
                case "perfectDodge":
                    F.Sprite(ev.x, ev.y, "star", "#ffffff", 2.2f, 0.22f, 1.6f); F.Sprite(ev.x, ev.y, "ring", PHASE, 1.2f, 0.5f, 5.5f); F.Sprite(ev.x, ev.y, "ring", "#ffffff", 0.8f, 0.3f, 4);
                    F.GroundRing(ev.p.x, ev.p.y, SLOW, 0.4f, 7, 0.7f, 0.8f);
                    F.Burst(ev.x, ev.y, PHASE, 30, 7, 0.3f, 0.7f, drag: 0.95f);
                    break;
                case "riseBlast":
                    F.Sprite(ev.x, ev.y, "star", "#ffffff", 2.2f, 0.18f, 1.5f); F.Sprite(ev.x, ev.y, "glow", "#ffd27a", 2.2f, 0.22f, 1.8f);
                    F.Sprite(ev.x, ev.y, "ring", "#fff1c9", (float)ev.r * 0.8f, 0.3f, 3);
                    F.Burst(ev.x, ev.y, GOLD, 26, 9, 0.32f, 0.4f, grav: 6); F.Burst(ev.x, ev.y, "#ffffff", 12, 7, 0.22f, 0.25f);
                    break;
            }
        }

        // ---- Chain lightning ----
        void Chain(Ev ev)
        {
            var F = fx; Bolt b = bolts.Find(q => q.life <= 0);
            if (b == null) { b = bolts[0]; foreach (var q in bolts) if (q.life < b.life) b = q; }
            b.pts.Clear(); foreach (var q in ev.pts) b.pts.Add((q.x, q.y));
            b.depth = ev.depth ?? 0; b.level = (float)ev.level; b.life = b.max = 0.2f + 0.05f * b.level + (ev.perfect ? 0.08f : 0);
            string c = SUB_LOOK["chain"].tint; var s0 = ev.pts[0];
            F.Sprite(s0.x, s0.y, "star", "#fff6cc", 0.5f + 0.1f * b.level, 0.1f, 1.4f);
            for (int i = 1; i < ev.pts.Count; i++)
            {
                var q = ev.pts[i];
                if (q.fizzle) { F.Burst(q.x, q.y, c, 8, 4, 0.2f, 0.22f); continue; }
                F.Sprite(q.x, q.y, "star", "#fff6cc", 0.6f + 0.1f * b.level, 0.12f, 1.4f); F.Sprite(q.x, q.y, "ring", c, 0.35f + 0.08f * b.level, 0.16f, 2);
                F.Burst(q.x, q.y, S.Rnd() < 0.5f ? "#ffffff" : c, 8 + 2 * b.level, 6 + b.level, 0.18f, 0.22f);
            }
        }
        // A jagged path through the bolt's points, redrawn every frame so the bolt flickers
        List<Vector3> Jag(Bolt b)
        {
            var o = new List<Vector3>(); var P = b.pts;
            for (int i = 0; i < P.Count - 1 && o.Count < 70; i++)
            {
                var a = P[i]; var c = P[i + 1]; double dx = c.x - a.x, dy = c.y - a.y, len = JMath.Hypot(dx, dy); if (len == 0) len = 1;
                int m = Mathf.Max(2, Mathf.Min(10, (int)System.Math.Ceiling(len / 0.45))); double nx = -dy / len, ny = dx / len;
                for (int j = i > 0 ? 1 : 0; j <= m && o.Count < 72; j++)
                {
                    double u = (double)j / m, amp = j == 0 || j == m ? 0 : (0.16 + 0.05 * b.level) * System.Math.Sin(System.Math.PI * u) * (S.Rnd() * 2 - 1) * System.Math.Min(1.6, len / 2);
                    o.Add(S.W(a.x + dx * u + nx * amp, a.y + dy * u + ny * amp, b.depth + 0.25));
                }
            }
            return o;
        }

        // ---- Gravity wells ----
        WellM MakeWell(Well w)
        {
            var g = Group.Make();
            var core = new TMesh(coreGeo, coreMat); var disc = new TMesh(discGeo, discMat);
            TMesh Sp(Texture tex, string col, float op) { var s = Three.Sprite.Make(new TMat(TMat.Kind.Sprite) { map = tex, colorCss = col, blending = Blending.Additive, depthWrite = false, opacity = op }); return s; }
            var rim = Sp(fx.tex.ring, "#ffe7b0", 0.8f); var halo = Sp(fx.tex.ring, GOLD, 0.09f); var glow = Sp(fx.tex.glow, GOLD, 1);
            disc.RenderOrder = 5; rim.RenderOrder = 5; halo.RenderOrder = 4; glow.RenderOrder = 5;
            g.add(glow); g.add(core); g.add(disc); g.add(rim); g.add(halo); scene.add(g);
            var M = new WellM { g = g, core = core, disc = disc, rim = rim, halo = halo, glow = glow, spin = S.Rnd() * 6 };
            wells[w] = M; return M;
        }
        void SyncWells(World world, float alpha, float dt)
        {
            var seen = new HashSet<Well>(); var F = fx;
            foreach (var w in world.wells)
            {
                seen.Add(w);
                if (!wells.TryGetValue(w, out var M)) M = MakeWell(w);
                double x = w.px + (w.x - w.px) * alpha, y = w.py + (w.y - w.py) * alpha;
                M.g.position.copy(S.W(x, y, w.lane * LevelFeatures.LANE_W + 0.2));
                if (w.phase == "orb")
                {
                    M.core.scale.setScalar(0.13f); M.glow.scale.setScalar(0.9f + 0.15f * Mathf.Sin(t * 30)); M.disc.visible = M.rim.visible = M.halo.visible = false;
                    if (S.Rnd() < 0.7f) F.Burst(x, y, S.Rnd() < 0.5f ? "#ffffff" : GOLD, 1, 1, 0.16f, 0.2f);
                    continue;
                }
                float L = (float)w.level, open = Mathf.Min(1, (float)w.t / 8), left = Mathf.Max(0, (float)(w.life - w.t)), fade = Mathf.Min(1, left / 12);
                float pulse = left < 20 ? 1 + 0.15f * Mathf.Sin(t * 40) : 1;
                M.core.scale.setScalar((0.26f + 0.06f * L) * open * pulse);
                M.disc.visible = M.rim.visible = M.halo.visible = true;
                M.spin += dt * (7 + 2 * L);
                M.disc.rotation.set(1.18f, 0, M.spin); M.disc.scale.setScalar((1.4f + 0.22f * L) * open * (0.5f + 0.5f * fade));
                M.rim.scale.setScalar((0.85f + 0.12f * L) * open); M.glow.scale.setScalar((1.6f + 0.3f * L) * open);
                M.halo.scale.setScalar((float)w.r * 2 * open); M.halo.material.rotation = t * 0.6f;
                for (int i = 0; i < 3 + L; i++)
                {
                    float a = S.Rnd() * Mathf.PI * 2, r = (float)w.r * (0.55f + S.Rnd() * 0.45f); var wp = S.W(x + Mathf.Cos(a) * r, y + Mathf.Sin(a) * r, w.lane * LevelFeatures.LANE_W + 0.2);
                    var P = F.Particle(wp, S.Rnd() < 0.3f ? "#ffffff" : GOLD, 0.14f, 0.4f);
                    float s = r * 2.4f; P.v = S.Dir(x, -Mathf.Cos(a) * s - Mathf.Sin(a) * s * 0.8f, -Mathf.Sin(a) * s + Mathf.Cos(a) * s * 0.8f); P.drag = 0.97f;
                }
            }
            foreach (var kv in new List<KeyValuePair<Well, WellM>>(wells)) if (!seen.Contains(kv.Key)) { kv.Value.g.DestroyOwnedMaterials(); wells.Remove(kv.Key); }
        }

        public void Update(float dt, World world, View view)
        {
            t += dt;
            var F = fx; var cam = view.camPos;
            foreach (var b in bolts)
            {
                if (b.life <= 0) { b.glow.mesh.visible = b.core.mesh.visible = false; continue; }
                b.life -= dt;
                float k = Mathf.Max(0, b.life / b.max), fl = 0.55f + S.Rnd() * 0.45f; var pts = Jag(b); var c = chainTint;
                if (pts.Count < 2) continue;
                b.glow.Build(pts, cam, i => (0.14f + 0.035f * b.level) * (0.6f + 0.4f * k), i => new Vector4(c.r * 1.3f, c.g * 1.15f, c.b * 0.7f, 0.5f * k * fl));
                b.core.Build(pts, cam, i => 0.035f + 0.01f * b.level, i => new Vector4(1.9f, 1.85f, 1.6f, k * fl));
            }
            SyncWells(world, view.alpha, dt);
            var seen = new HashSet<Enemy>();
            foreach (var e in world.enemies)
            {
                if (e.dead) continue;
                if (e.shockT > 0 && S.Rnd() < 0.55f)
                {
                    var w = S.W(e.x + (S.Rnd() - 0.5f) * e.w, e.y + S.Rnd() * e.h, LevelFeatures.Depth(e) + 0.3);
                    var P = F.Particle(w, S.Rnd() < 0.5f ? "#ffffff" : SUB_LOOK["chain"].tint, 0.14f, 0.12f); P.v = new Vector3((S.Rnd() - 0.5f) * 6, (S.Rnd() - 0.5f) * 6, 0); P.drag = 0.8f;
                }
                if (e.wellT > 0 && S.Rnd() < 0.5f) F.Burst(e.x, e.y + e.h * 0.5, GOLD, 1, 2.5f, 0.16f, 0.3f);
                if (e.slowT > 0)
                {
                    seen.Add(e);
                    if (!auras.TryGetValue(e, out var s))
                    {
                        s = Three.Sprite.Make(new TMat(TMat.Kind.Sprite) { map = fx.tex.ring, colorCss = SLOW, blending = Blending.Additive, depthWrite = false, opacity = 0.7f });
                        s.RenderOrder = 5; scene.add(s); auras[e] = s;
                    }
                    s.position.copy(S.W(e.x, e.y + e.h * 0.5, LevelFeatures.Depth(e) + 0.35)); s.scale.setScalar((float)System.Math.Max(e.w, e.h) * 1.35f);
                    s.material.rotation = -t * 1.2f; s.material.opacity = 0.65f * Mathf.Min(1, (float)e.slowT / 20);
                    if (S.Rnd() < 0.3f) { var P = F.Particle(S.W(e.x + (S.Rnd() - 0.5f) * e.w, e.y + S.Rnd() * e.h, LevelFeatures.Depth(e) + 0.3), SLOW, 0.12f, 0.8f); P.v = new Vector3(0, 0.35f, 0); P.drag = 1; }
                }
            }
            foreach (var kv in new List<KeyValuePair<Enemy, TMesh>>(auras)) if (!seen.Contains(kv.Key)) { kv.Value.DestroyOwnedMaterials(); auras.Remove(kv.Key); }
            foreach (var p in world.players)
            {
                var rig = fx.RigOf(p); if (rig == null || !rig.root.visible) continue;
                if (p.state == "dodge" && p.dodge != null && p.dodge.t % 2 == 0 && p.dodge.t <= 12 && (!ghostTick.TryGetValue(p, out var gt) || gt != world.tick))
                {
                    ghostTick[p] = world.tick;
                    F.ghosts.Spawn(rig, S.Lin(p.dodge.perfect ? "#ffffff" : PHASE), p.dodge.perfect ? 0.55f : 0.34f, 0.24f);
                }
                var m = p.move; bool rising = p.state == "attack" && p.moveId == "nova_rise" && m != null;
                bool hadLast = lastSt.TryGetValue(p, out var last); lastSt[p] = rising ? p.st : -1;
                if (!rising) continue;
                if (p.st >= m.su && hadLast && last < m.su)
                {
                    F.Sprite(p.x, p.y + 1.6, "glow", "#ffd27a", 3.6f, 0.3f, 1.1f, 0.2f, 0.22f);
                    F.GroundRing(p.x, p.y, "#fff1c9", 0.3f, 1.8f, 0.3f, 0.9f); F.Dust(p.x, p.y, 0.5f, null, noRing: true);
                }
                if (p.st >= m.su && p.st < m.su + m.ac)
                {
                    foreach (var dz in new[] { -0.13f, 0.13f })
                    {
                        var w = S.W(p.x + (S.Rnd() - 0.5f) * 0.12f, p.y - 0.05, dz);
                        var P = F.Particle(w, S.Rnd() < 0.5f ? "#fff1c9" : GOLD, 0.34f, 0.2f); P.v = new Vector3((S.Rnd() - 0.5f) * 0.8f, -7 - S.Rnd() * 4, (S.Rnd() - 0.5f) * 0.8f); P.drag = 0.86f;
                    }
                    if (S.Rnd() < 0.5f) F.Smoke(p.x, p.y - 0.2, "#8e97a3", 1, 0.8f, 0.4f, 0.5f, dir: -Mathf.PI / 2, spread: 1, op: 0.35f);
                }
            }
        }
    }
}
