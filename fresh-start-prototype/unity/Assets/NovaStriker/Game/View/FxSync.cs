// World objects drawn by the effects layer (fx.js): projectiles, Nova's barriers, sniper lasers, shockwaves,
// Echo's scarf, snares and snared bands, telegraph markers and mortar landing marks; and the floating words.
using System.Collections.Generic;
using NovaStriker.Game.Three;
using NovaStriker.Sim;
using UnityEngine;
using UnityEngine.UI;
using static NovaStriker.Sim.Cfg;

namespace NovaStriker.Game
{
    public sealed partial class Fx
    {
        // ---- Telegraphs and markers ----
        sealed class Tele { public Enemy e; public string cat; public float ticks, t; }
        readonly List<Tele> telegraphs = new List<Tele>();
        sealed class Marker { public Enemy e; public TMesh mesh; public float t, ticks; public bool dead; }
        readonly List<Marker> markers = new List<Marker>();
        sealed class Mark { public TMesh mesh; public float life, max; public bool dead; }
        readonly List<Mark> marks = new List<Mark>();

        void UpdateTelegraphs(World world)
        {
            foreach (var tg in telegraphs)
            {
                var e = tg.e; tg.t++;
                double ex = e.x + e.facing * e.w * 0.35, ey = e.y + e.h * 0.8;
                bool pop = tg.cat == "standard" ? (tg.t - 1 == 0 || tg.t - 1 == tg.ticks - 7) : tg.cat == "heavy" ? (tg.t - 1 == 0 || tg.t - 1 == 7 || tg.t - 1 == tg.ticks - 7) : false;
                if (pop) Sprite(ex, ey, "star", tg.cat == "heavy" ? "#fff3cf" : "#ffffff", tg.cat == "heavy" ? 1.5f : 1.0f, 0.2f, 1.2f, 0.8f);
                if (tg.cat == "unblockable" && tg.t == 1 && e.type != "mortar" && !e.flier) AddMarker(e, tg.ticks);
            }
            telegraphs.RemoveAll(tg => !(tg.t < tg.ticks && !tg.e.dead));
            foreach (var mk in markers)
            {
                mk.t++; float k = mk.t / mk.ticks;
                mk.mesh.material.opacity = 0.35f + 0.45f * Mathf.Abs(Mathf.Sin(mk.t * 0.35f));
                mk.mesh.material.offset = new Vector2(-mk.t * 0.04f, 0);
                mk.mesh.scale.x = 0.3f + 0.7f * Mathf.Min(1, k * 2);
                if (mk.t >= mk.ticks || mk.e.dead) { mk.mesh.destroy(); mk.dead = true; }
            }
            markers.RemoveAll(m => m.dead);
        }
        // Mortar landing marker: a pulsing magenta ring on the ground where the shell will burst
        void AddLandingMark(double x, double y, float r, float secs)
        {
            var mat = new TMat(TMat.Kind.Basic) { map = tex.ring, colorCss = HOSTILE, transparent = true, opacity = 0.7f, depthWrite = false, blending = Blending.Additive, side = Side.Double };
            var mesh = new TMesh(Geo.Plane(r * 2.4f, r * 2.4f), mat);
            mesh.position.copy(S.W(x, y + 0.04, 0)); mesh.rotation.x = -Mathf.PI / 2;
            scene.add(mesh);
            marks.Add(new Mark { mesh = mesh, life = secs, max = secs });
        }
        void UpdateMarks(float dt)
        {
            foreach (var m in marks)
            {
                m.life -= dt; float k = 1 - Mathf.Max(0, m.life) / m.max;
                m.mesh.material.opacity = 0.35f + 0.5f * k * (0.6f + 0.4f * Mathf.Sin(k * 40));
                m.mesh.scale.setScalar(1.25f - 0.25f * k);
                if (m.life <= 0) { m.mesh.destroy(); m.dead = true; }
            }
            marks.RemoveAll(m => m.dead);
        }
        void AddMarker(Enemy e, float ticks)
        {
            float len = e.type == "post" ? 7 : 12;
            var mat = new TMat(TMat.Kind.Basic) { map = tex.jag, repeat = new Vector2(len / 1.2f, 1), colorCss = HOSTILE, transparent = true, opacity = 0.6f, depthWrite = false, blending = Blending.Additive, side = Side.Double };
            var mesh = new TMesh(Geo.Plane(len, 0.5f), mat);
            mesh.position.copy(S.W(e.x, e.y + 0.26, 0.9)); mesh.rotation.y = S.YawAt(e.x);
            scene.add(mesh); markers.Add(new Marker { e = e, mesh = mesh, ticks = ticks });
        }

        // ---- Projectiles ----
        Dictionary<string, System.Func<TObj>> tmpl;
        readonly Dictionary<string, TObj> protos = new Dictionary<string, TObj>();
        readonly Dictionary<Projectile, TObj> projMeshes = new Dictionary<Projectile, TObj>();
        readonly Dictionary<Projectile, (bool defl, float spin, double blink)> projState = new Dictionary<Projectile, (bool, float, double)>();
        static readonly HashSet<string> SPIN = new HashSet<string> { "std", "heavy", "snare", "shell", "prism", "mortar", "grenade", "bomblet" };

        void BuildProjectileTemplates()
        {
            TMat Em(string c, float i = 3) => new TMat { colorCss = c, emissiveCss = c, emissiveIntensity = i, roughness = 0.3f };
            TMesh M(Mesh g, TMat m, float x = 0, float y = 0, float z = 0) => new TMesh(g, m, x, y, z);
            TObj Gr(params TObj[] kids) { var g = Group.Make(); foreach (var k in kids) g.add(k); return g; }
            string nova = CHARS["nova"].energy, echo = CHARS["echo"].energy;
            var build = new Dictionary<string, System.Func<TObj>>
            {
                ["shot"] = () => M(Geo.Capsule(0.08f, 0.34f, 3, 8), Em(nova, 4)),
                ["lance"] = () => M(Geo.Capsule(0.13f, 0.9f, 3, 8), Em(nova, 5)),
                ["rail"] = () => M(Geo.Capsule(0.15f, 1.9f, 3, 8), Em("#fff1d0", 6)),
                ["bolt"] = () => M(Geo.Capsule(0.07f, 0.4f, 3, 8), Em(echo, 4)),
                ["tracer"] = () => Gr(M(Geo.Torus(0.16f, 0.04f, 6, 14), Em(echo, 5)), M(Geo.Capsule(0.05f, 0.3f, 3, 8), Em(echo, 5))),
                ["snare"] = () =>
                {
                    var g = Gr(M(Geo.Torus(0.22f, 0.05f, 6, 16), Em(echo, 4)));
                    for (int i = 0; i < 3; i++) g.add(M(Geo.Sphere(0.07f, 8, 6), Em("#fff1d0", 4), Mathf.Cos(i * 2.1f) * 0.22f, Mathf.Sin(i * 2.1f) * 0.22f, 0));
                    return g;
                },
                ["std"] = () => M(Geo.Octahedron(0.2f), Em(HOSTILE, 3)),
                ["heavy"] = () => Gr(M(Geo.Octahedron(0.34f), Em(HOSTILE, 3)), M(Geo.Sphere(0.14f, 10, 8), Em("#ffffff", 4))),
                ["dart"] = () => M(Geo.Capsule(0.05f, 0.3f, 3, 6), Em(ATTACH_LOOK["volley"].tint, 5)),
                ["shell"] = () => Gr(M(Geo.Sphere(0.17f, 14, 10), Em(ATTACH_LOOK["arc"].tint, 3.5f)), M(Geo.Torus(0.23f, 0.03f, 6, 18), Em("#fff1d0", 4))),
                ["prism"] = () => M(Geo.Octahedron(0.22f), Em(ATTACH_LOOK["prism"].tint, 4)),
                ["shard"] = () => M(Geo.Tetrahedron(0.12f), Em(ATTACH_LOOK["prism"].tint, 5)),
                ["pellet"] = () => M(Geo.Sphere(0.07f, 8, 6), Em("#ffcf7a", 5)),
                ["rifle"] = () => M(Geo.Capsule(0.045f, 0.5f, 3, 6), Em(echo, 5)),
                ["markShot"] = () => Gr(M(Geo.Capsule(0.07f, 0.8f, 3, 8), Em("#ffe0b0", 6)), M(Geo.Torus(0.16f, 0.025f, 6, 16), Em(echo, 5))),
                ["mortar"] = () => Gr(M(Geo.Sphere(0.28f, 14, 10), Em("#2b2f3a", 0.2f)), M(Geo.Torus(0.29f, 0.05f, 6, 18), Em(HOSTILE, 4))),
                ["wave"] = () =>
                {
                    TMat Glow(string c, float k, float op) => new TMat(TMat.Kind.Basic) { colorLin = S.Lin(c) * k, transparent = true, opacity = op, side = Side.Double, depthWrite = false };
                    var outer = Geo.Translated(Geo.Ring(0.5f, 0.86f, 28, 1, Mathf.PI / 2 - 1.15f, 2.3f), 0, -0.72f, 0);
                    var edge = Geo.Translated(Geo.Ring(0.74f, 0.86f, 28, 1, Mathf.PI / 2 - 1.05f, 2.1f), 0, -0.72f, 0);
                    return Gr(M(outer, Glow(echo, 2.2f, 0.8f)), M(edge, Glow("#fff6e0", 3, 1)));
                },
                ["missile"] = () => Gr(M(Geo.Capsule(0.1f, 0.42f, 3, 8), Em(HOSTILE, 3)), M(Geo.Sphere(0.09f, 8, 6), Em("#ffffff", 4), 0, 0.28f)),
                ["grenade"] = () =>
                {
                    var b = M(Geo.Torus(0.175f, 0.035f, 6, 18), Em(SUB_LOOK["grenade"].tint, 4)); b.rotation.x = Mathf.PI / 2;
                    return Gr(M(Geo.Sphere(0.17f, 14, 10), new TMat { colorCss = "#2d3240", roughness = 0.45f, metalness = 0.4f }), b);
                },
                ["bomblet"] = () => M(Geo.Sphere(0.1f, 10, 8), Em(SUB_LOOK["grenade"].tint, 4)),
                ["disc"] = () =>
                {
                    var face = M(Geo.Cylinder(0.4f, 0.4f, 0.04f, 28), new TMat(TMat.Kind.Basic) { colorLin = S.Lin(SUB_LOOK["disc"].tint) * 1.1f, transparent = true, opacity = 0.4f, depthWrite = false });
                    var rim = M(Geo.Torus(0.4f, 0.045f, 6, 28), Em(SUB_LOOK["disc"].tint, 2.6f)); rim.rotation.x = Mathf.PI / 2;
                    var hub = M(Geo.Torus(0.16f, 0.03f, 6, 18), Em("#fff1c9", 2.4f)); hub.rotation.x = Mathf.PI / 2;
                    return Gr(face, rim, hub);
                },
                ["deflected"] = () => Gr(M(Geo.Octahedron(0.24f), Em(echo, 4)), M(Geo.Sphere(0.11f, 8, 6), Em("#ffffff", 4))),
                ["reflected"] = () => Gr(M(Geo.Octahedron(0.26f), Em(CHARS["ram"].energy, 4)), M(Geo.Sphere(0.12f, 8, 6), Em("#ffffff", 4))),
                ["slug"] = () => M(Geo.Capsule(0.13f, 0.3f, 3, 10), Em("#d6e8ff", 3.5f)),
                ["breach"] = () =>
                {
                    var r = M(Geo.Torus(0.3f, 0.04f, 6, 18), Em("#ffffff", 4)); r.rotation.x = Mathf.PI / 2;
                    return Gr(M(Geo.Capsule(0.2f, 0.6f, 3, 12), Em(CHARS["ram"].energy, 4)), r);
                },
                ["rivet"] = () => M(Geo.Capsule(0.04f, 0.2f, 3, 6), Em("#ffe2a8", 3)),
                ["hotRivet"] = () => Gr(M(Geo.Capsule(0.07f, 0.26f, 3, 8), Em("#ff8a3a", 4)), M(Geo.Sphere(0.1f, 8, 6), Em(CHARS["fix"].energy, 3))),
                ["sentryBolt"] = () => M(Geo.Capsule(0.05f, 0.24f, 3, 6), Em("#ffcf5a", 4)),
                ["sentryRocket"] = () => Gr(M(Geo.Capsule(0.08f, 0.3f, 3, 8), Em("#ffcf5a", 2.5f)), M(Geo.Sphere(0.07f, 8, 6), Em("#ffffff", 4), 0, 0.2f)),
            };
            // Each look is built once (its materials shared); projectiles are copies that share them
            tmpl = new Dictionary<string, System.Func<TObj>>();
            foreach (var kv in build)
            {
                string k = kv.Key; var make = kv.Value;
                tmpl[k] = () =>
                {
                    if (!protos.TryGetValue(k, out var proto)) { proto = make(); proto.visible = false; protos[k] = proto; }
                    return CloneObj(proto);
                };
            }
        }
        // A copy sharing geometry and materials (Object3D.clone)
        static TObj CloneObj(TObj o)
        {
            TObj c = o is TMesh m ? new TMesh(m.geometry, m.material) : (TObj)Group.Make();
            c.position.copy(o.position.v); c.rotation.set(o.rotation.x, o.rotation.y, o.rotation.z); c.scale.copy(o.scale.v);
            c.castShadow = o.castShadow; if (c is TMesh cm) cm.cast = o.castShadow;
            foreach (var k in o.children) c.add(CloneObj(k));
            c.visible = true;
            return c;
        }

        void SyncProjectiles(World world, float alpha)
        {
            var seen = new HashSet<Projectile>();
            foreach (var pr in world.projectiles)
            {
                seen.Add(pr);
                projMeshes.TryGetValue(pr, out var m);
                projState.TryGetValue(pr, out var st);
                if (m != null && pr.deflected && !st.defl) { m.destroy(); m = null; }
                if (m == null)
                {
                    string key = pr.deflected ? (pr.reflected ? "reflected" : "deflected") : pr.kind != null && tmpl.ContainsKey(pr.kind) ? pr.kind : "std";
                    m = tmpl[key](); st = (pr.deflected, 0, double.NaN); scene.add(m); projMeshes[pr] = m;
                }
                if (pr.stuck != null && pr.stuck.t % (pr.stuck.t < 12 ? 3 : 8) == 0) Sprite(pr.x, pr.y, "glow", pr.stuck.t < 12 ? "#ffffff" : "#ff9a4a", 0.5f, 0.08f, 1.2f);
                double x = pr.px + (pr.x - pr.px) * alpha, y = pr.py + (pr.y - pr.py) * alpha;
                m.position.copy(S.W(x, y, 0.1));
                var d = S.Dir(x, pr.vx, pr.vy).normalized;
                if (pr.kind == "disc")
                {
                    st.spin += 0.55f;
                    m.rotation.set(Mathf.PI / 2 - 0.35f, st.spin, 0); m.scale.setScalar((float)pr.r / 0.4f);
                }
                else if (pr.kind != null && SPIN.Contains(pr.kind)) { if (!pr.rest) { m.rotation.x += 0.2f; m.rotation.y += 0.15f; } }
                else if (d.sqrMagnitude > 0) m.SetQuaternion(ThQ.FromUnitVectors(Vector3.up, d));
                if (pr.kind == "grenade" && (pr.ttl < 24 ? pr.ttl % 4 == 0 : pr.ttl % 10 == 0) && st.blink != pr.ttl)
                {
                    st.blink = pr.ttl; Sprite(x, y, "glow", pr.ttl < 24 ? "#ff5a3a" : SUB_LOOK["grenade"].tint, 0.55f, 0.08f, 1.2f);
                }
                if (pr.amplified) m.scale.setScalar(1.35f);
                projState[pr] = st;
                if (charge.WantsTrail(pr)) charge.Trail(pr, m.position.v);
                if (pr.kind == "sentryRocket" && S.Rnd() < 0.7f) Smoke(x - pr.vx * 0.012, y - pr.vy * 0.012, "#8e97a3", 1, 0.3f, 0.25f, 0.4f, op: 0.4f, grav: -0.3f);
                if (pr.kind == "missile" && S.Rnd() < 0.8f) Smoke(x - pr.vx * 0.012, y - pr.vy * 0.012, "#8e97a3", 1, 0.4f, 0.35f, 0.5f, op: 0.45f, grav: -0.3f);
                if (pr.kind == "wave") { for (int i = 0; i < 3; i++) Burst(x - System.Math.Sign(pr.vx) * 0.2, y + (S.Rnd() - 0.5f) * 1.3f, S.Rnd() < 0.4f ? "#ffffff" : ECHO_ORANGE, 1, 1.5f, 0.24f, 0.2f); }
                else if (S.Rnd() < (pr.kind == "pellet" ? 0.25f : 0.6f)) Burst(x, y, TrailColor(pr), 1, 0.6f, pr.kind == "rail" ? 0.5f : 0.22f, 0.18f);
            }
            foreach (var kv in new List<KeyValuePair<Projectile, TObj>>(projMeshes))
                if (!seen.Contains(kv.Key)) { kv.Value.destroy(); projMeshes.Remove(kv.Key); projState.Remove(kv.Key); }
            charge.OrphanUnseen(seen);
        }
        static string KindTint(string kind)
        {
            switch (kind)
            {
                case "dart": return ATTACH_LOOK["volley"].tint;
                case "shell": return ATTACH_LOOK["arc"].tint;
                case "prism": case "shard": return ATTACH_LOOK["prism"].tint;
                case "pellet": return "#ffcf7a";
                case "grenade": case "bomblet": return SUB_LOOK["grenade"].tint;
                case "disc": return SUB_LOOK["disc"].tint;
                case "slug": return "#cfe6ff";
                case "breach": return CHARS["ram"].energy;
                case "rivet": return "#ffe2a8";
                case "hotRivet": return "#ff9a4a";
                case "sentryBolt": case "sentryRocket": return "#ffcf5a";
            }
            return null;
        }
        static string TrailColor(Projectile pr)
        {
            if (pr.reflected) return CHARS["ram"].energy;
            if (pr.deflected || pr.kind == "wave") return ECHO_ORANGE;
            if (pr.team == "e") return HOSTILE;
            if (pr.kind == "bolt" || pr.kind == "tracer" || pr.kind == "rifle" || pr.kind == "markShot") return ECHO_ORANGE;
            return KindTint(pr.kind) ?? NOVA_GOLD;
        }

        // ---- Nova's barriers ----
        readonly Dictionary<Barrier, TMesh> barrierMeshes = new Dictionary<Barrier, TMesh>();
        void SyncBarriers(World world)
        {
            var seen = new HashSet<Barrier>();
            foreach (var b in world.barriers)
            {
                if (b.kind == "rampart") continue;
                seen.Add(b);
                if (!barrierMeshes.TryGetValue(b, out var m))
                {
                    var mat = new TMat { colorCss = "#fff0cc", emissiveCss = NOVA_GOLD, emissiveIntensity = 2.2f, transparent = true, opacity = 0.55f, depthWrite = false, side = Side.Double };
                    m = new TMesh(Geo.Box((float)b.half * 2, 0.22f, 1.6f), mat);
                    var T = S.Dir(b.x, -b.ny, b.nx).normalized; var Nn = S.Dir(b.x, b.nx, b.ny).normalized; var Z = Vector3.Cross(T, Nn).normalized;
                    m.SetQuaternion(ThQ.FromBasis(T, Nn, Z));
                    m.position.copy(S.W(b.x, b.y, 0));
                    scene.add(m); barrierMeshes[b] = m;
                }
                float k = (float)(b.ttl / b.max);
                m.material.opacity = 0.25f + 0.4f * k + (b.ttl % 10 < 5 && k < 0.25f ? 0.2f : 0);
            }
            foreach (var kv in new List<KeyValuePair<Barrier, TMesh>>(barrierMeshes)) if (!seen.Contains(kv.Key)) { kv.Value.destroy(); barrierMeshes.Remove(kv.Key); }
        }

        // ---- Sniper lasers ----
        readonly Dictionary<Enemy, TMesh> lasers = new Dictionary<Enemy, TMesh>();
        void SyncLasers(World world)
        {
            var seen = new HashSet<Enemy>();
            foreach (var e in world.enemies)
            {
                if (e.type != "sniper" || e.dead || (e.state != "aim" && e.state != "lock")) continue;
                seen.Add(e);
                if (!lasers.TryGetValue(e, out var m))
                {
                    m = new TMesh(Geo.Cylinder(0.025f, 0.025f, 1, 6), new TMat(TMat.Kind.Basic) { colorCss = HOSTILE, transparent = true, opacity = 0.8f, depthWrite = false, blending = Blending.Additive });
                    scene.add(m); lasers[e] = m;
                }
                double sx = e.x + e.facing * 1.3, sy = e.y + 1.35;
                double dx = e.aimX - sx, dy = e.aimY - sy, d = JMath.Hypot(dx, dy); if (d == 0) d = 1;
                double len = 26, ex = sx + dx / d * len, ey = sy + dy / d * len;
                Vector3 a = S.W(sx, sy, 0.25), b = S.W(ex, ey, 0.25);
                m.position.copy((a + b) * 0.5f);
                m.scale.set(e.state == "lock" ? 2.6f : 1, Vector3.Distance(a, b), e.state == "lock" ? 2.6f : 1);
                m.SetQuaternion(ThQ.FromUnitVectors(Vector3.up, (b - a).normalized));
                m.material.colorCss = e.state == "lock" ? "#ffffff" : HOSTILE;
            }
            foreach (var kv in new List<KeyValuePair<Enemy, TMesh>>(lasers)) if (!seen.Contains(kv.Key)) { kv.Value.destroy(); lasers.Remove(kv.Key); }
        }

        // ---- Shockwaves ----
        readonly Dictionary<Shockwave, TMesh> shockMeshes = new Dictionary<Shockwave, TMesh>();
        void SyncShockwaves(World world)
        {
            var seen = new HashSet<Shockwave>();
            foreach (var s in world.shockwaves)
            {
                seen.Add(s);
                string col = s.team == "p" ? CHARS["ram"].energy : HOSTILE;
                if (!shockMeshes.TryGetValue(s, out var m))
                {
                    m = new TMesh(Geo.Cone(0.45f, 1.1f, 4), new TMat { colorCss = col, emissiveCss = col, emissiveIntensity = 3, transparent = true, opacity = 0.85f });
                    scene.add(m); shockMeshes[s] = m;
                }
                m.position.copy(S.W(s.x, s.y + 0.5, 0.2));
                m.rotation.y += 0.4f;
                if (S.Rnd() < 0.8f) Burst(s.x, s.y + 0.2, col, 2, 3, 0.35f, 0.25f, dir: Mathf.PI / 2, spread: 1.5f);
                if (s.team == "p" && S.Rnd() < 0.6f) Dust(s.x, s.y, 0.3f, new[] { s.dir > 0 ? 0 : Mathf.PI }, noRing: true, op: 0.45f);
            }
            foreach (var kv in new List<KeyValuePair<Shockwave, TMesh>>(shockMeshes)) if (!seen.Contains(kv.Key)) { kv.Value.destroy(); shockMeshes.Remove(kv.Key); }
        }

        // ---- Echo's nano-scarf: a spring chain that becomes the lash ----
        sealed class Scarf { public DynMesh dm; public TMat mat; public Dictionary<string, TMat> mats; public string mode; public Vector3[] pts, prev; }
        readonly Dictionary<Player, Scarf> scarves = new Dictionary<Player, Scarf>();
        void SyncScarves(float dt, World world, View view)
        {
            var seen = new HashSet<Player>();
            foreach (var p in world.players)
            {
                if (p.@char != "echo") continue;
                var rig = RigOf(p); if (rig == null) continue;
                seen.Add(p);
                if (!scarves.TryGetValue(p, out var Sc)) Sc = MakeScarf(p, rig);
                var anchor = rig.collar.worldPos;
                var pts = Sc.pts; int n = pts.Length;
                bool fast = JMath.Hypot(p.vx, p.vy) > 10 || p.state == "dash" || p.state == "zip";
                float seg = fast ? 0.16f : 0.12f;
                pts[0] = anchor;
                var reel = p.leash != null && !p.leash.e.dead ? p.leash.e : null;
                if (((p.state == "lash" || p.state == "zip") && p.lash != null) || reel != null)
                {
                    double tx = reel != null ? reel.x : p.lash.tx, ty = reel != null ? reel.y + reel.h * 0.55 : p.lash.ty;
                    float Llen = reel != null ? 1 : (float)p.lash.len; var tgt = S.W(tx, ty, 0.2);
                    float k = p.state == "zip" || reel != null ? 1 : Llen;
                    for (int i = 1; i < n; i++) { float u = (float)i / (n - 1) * k; Sc.prev[i] = pts[i]; pts[i] = Vector3.Lerp(anchor, tgt, u); }
                }
                else
                {
                    float g = fast ? -2 : -9.5f;
                    var back = S.Dir(p.x, -p.facing, 0);
                    float sway = Mathf.Sin(view.camPos.x * 0.2f + Time.time * 1000 * 0.003f) * 0.0012f;
                    for (int i = 1; i < n; i++)
                    {
                        var v = (pts[i] - Sc.prev[i]) * 0.92f;
                        Sc.prev[i] = pts[i]; pts[i] += v; pts[i].y += g * dt * dt;
                        pts[i] += back * (0.0016f * i); pts[i].y += sway * i;
                    }
                    for (int it = 0; it < 3; it++)
                        for (int i = 1; i < n; i++)
                        {
                            var a = pts[i - 1]; float d = Vector3.Distance(pts[i], a); if (d == 0) d = 1e-4f;
                            pts[i] = (pts[i] - a) * (seg / d) + a;
                        }
                }
                // Rebuild the ribbon facing the camera
                var cam = view.camPos;
                for (int i = 0; i < n; i++)
                {
                    var a = pts[Mathf.Max(0, i - 1)]; var b = pts[Mathf.Min(n - 1, i + 1)];
                    var dir = (b - a).normalized; var toCam = (cam - pts[i]).normalized;
                    var side = Vector3.Cross(dir, toCam).normalized * (0.09f * (1 - (float)i / n * 0.55f));
                    Sc.dm.Set(i * 2, pts[i] + side); Sc.dm.Set(i * 2 + 1, pts[i] - side);
                }
                Sc.dm.Upload(true);
                string mode = p.scarfMode ?? "tether";
                if (Sc.mode != mode) { Sc.mode = mode; Sc.dm.obj.material = Sc.mats[mode]; }
                if (mode == "flare")
                {
                    Sc.mats["flare"].emissiveIntensity = 1.2f + Mathf.Sin(Time.time * 1000 * 0.009f) * 0.35f;
                    if (S.Rnd() < 0.45f) BurstAt(pts[1 + Mathf.FloorToInt(S.Rnd() * (n - 1))], ECHO_ORANGE, 1, 1.2f, 0.18f, 0.45f);
                }
                else if (mode == "veil") Sc.mats["veil"].opacity = 1 - 0.8f * rig.cloak;
                else Sc.mat.emissiveIntensity = fast || p.state == "lash" || reel != null ? 1.6f : 0.5f;
                Sc.dm.visible = rig.root.visible;
            }
            foreach (var kv in new List<KeyValuePair<Player, Scarf>>(scarves)) if (!seen.Contains(kv.Key)) { kv.Value.dm.obj.destroy(); scarves.Remove(kv.Key); }
        }
        Scarf MakeScarf(Player p, Rig rig)
        {
            const int n = 14;
            var texA = CanvasTex(64, (g, s) => { g.fillStyle = "#ecebe6"; g.fillRect(0, 0, s, s); g.fillStyle = "#ff9a1f"; g.fillRect(0, 0, s * 0.12f, s); g.fillRect(s * 0.88f, 0, s * 0.12f, s); });
            var veilTex = CanvasTex(64, (g, s) => { g.fillStyle = "#7d8896"; g.fillRect(0, 0, s, s); g.fillStyle = "#e4f1ff"; g.fillRect(0, 0, s * 0.1f, s); g.fillRect(s * 0.9f, 0, s * 0.1f, s); });
            // Only the edges glow; the off-white fabric stays unlit
            var edges = CanvasTex(64, (g, s) => { g.fillStyle = "#000"; g.fillRect(0, 0, s, s); g.fillStyle = "#fff"; g.fillRect(0, 0, s * 0.12f, s); g.fillRect(s * 0.88f, 0, s * 0.12f, s); });
            var mat = new TMat { map = texA, emissiveCss = "#ff9a1f", emissiveMap = edges, emissiveIntensity = 1.2f, side = Side.Double, roughness = 0.6f };
            var mats = new Dictionary<string, TMat>
            {
                ["tether"] = mat,
                ["veil"] = new TMat { map = veilTex, emissiveCss = "#cde8ff", emissiveMap = edges, emissiveIntensity = 0.8f, side = Side.Double, roughness = 0.4f, transparent = true },
                ["flare"] = new TMat { colorCss = "#ff9f3a", emissiveCss = "#ff7a00", emissiveIntensity = 1.3f, side = Side.Double, roughness = 0.5f },
            };
            var dm = new DynMesh(n * 2, DynMesh.StripTris(n), mat, false, true, "scarf");
            for (int i = 0; i < n; i++) { dm.uv[i * 2] = new Vector2(0, (float)i / (n - 1)); dm.uv[i * 2 + 1] = new Vector2(1, (float)i / (n - 1)); }
            dm.obj.cast = true; scene.add(dm.obj);
            var a = rig.collar.worldPos;
            var pts = new Vector3[n]; var prev = new Vector3[n];
            for (int i = 0; i < n; i++) { pts[i] = a + new Vector3(0, -i * 0.1f, 0); prev[i] = pts[i]; }
            var Sc = new Scarf { dm = dm, mat = mat, mats = mats, mode = "tether", pts = pts, prev = prev };
            scarves[p] = Sc; return Sc;
        }

        // ---- Snares and snared bands ----
        readonly Dictionary<Snare, (TObj g, TMesh ring)> snareMeshes = new Dictionary<Snare, (TObj, TMesh)>();
        void SyncSnares(World world)
        {
            var seen = new HashSet<Snare>();
            foreach (var s in world.snares)
            {
                seen.Add(s);
                if (!snareMeshes.TryGetValue(s, out var m))
                {
                    var g = Group.Make();
                    g.add(new TMesh(Geo.Cylinder(0.4f, 0.46f, 0.08f, 20), new TMat { colorCss = "#2a2a31", roughness = 0.5f }));
                    var ring = new TMesh(Geo.Torus(0.42f, 0.04f, 6, 24), new TMat { colorCss = ECHO_ORANGE, emissiveCss = ECHO_ORANGE, emissiveIntensity = 2 });
                    ring.rotation.x = Mathf.PI / 2; ring.position.y = 0.06f; g.add(ring);
                    g.position.copy(S.W(s.x, s.y + 0.04, 0.35)); scene.add(g); m = (g, ring); snareMeshes[s] = m;
                }
                bool armed = s.armT <= 0;
                m.ring.material.emissiveIntensity = armed ? 1.6f + Mathf.Sin(Time.time * 1000 * 0.012f) * 0.9f : 0.4f;
            }
            foreach (var kv in new List<KeyValuePair<Snare, (TObj g, TMesh ring)>>(snareMeshes)) if (!seen.Contains(kv.Key)) { kv.Value.g.destroy(); snareMeshes.Remove(kv.Key); }
        }
        readonly Dictionary<Enemy, TObj> bands = new Dictionary<Enemy, TObj>();
        void SyncSnaredRings(World world)
        {
            var seen = new HashSet<Enemy>();
            foreach (var e in world.enemies)
            {
                if (e.dead || e.state != "snared") continue;
                seen.Add(e);
                if (!bands.TryGetValue(e, out var g))
                {
                    g = Group.Make();
                    var mat = new TMat { colorCss = ECHO_ORANGE, emissiveCss = ECHO_ORANGE, emissiveIntensity = 2.4f, transparent = true, opacity = 0.85f };
                    foreach (var f in new[] { 0.25f, 0.55f }) { var r = new TMesh(Geo.Torus((float)e.w * 0.75f, 0.04f, 6, 24), mat); r.rotation.x = Mathf.PI / 2; r.position.y = (float)e.h * f; g.add(r); }
                    scene.add(g); bands[e] = g;
                }
                g.position.copy(S.W(e.x, e.y, 0));
                g.rotation.y += 0.08f;
            }
            foreach (var kv in new List<KeyValuePair<Enemy, TObj>>(bands)) if (!seen.Contains(kv.Key)) { kv.Value.destroy(); bands.Remove(kv.Key); }
        }
    }

    // Floating words and enemy status glyphs: drawn over the world on a screen-space canvas (the prototype's
    // sprites have depth testing off, so they always draw on top), sized as the world-space sprite would be
    public sealed class FxText
    {
        sealed class Item { public Text t; public Outline o; public double x, y; public float life, max, aspect = 1; public Enemy e; public bool glyph; }
        readonly List<Item> texts = new List<Item>(), glyphs = new List<Item>();
        readonly RectTransform root;
        readonly Font font;
        public FxText(Fx fx)
        {
            var go = new GameObject("fx-text", typeof(Canvas), typeof(CanvasScaler));
            var c = go.GetComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = 5;
            root = go.GetComponent<RectTransform>();
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
        Item Make(bool glyph)
        {
            var go = new GameObject(glyph ? "glyph" : "word", typeof(RectTransform));
            go.transform.SetParent(root, false);
            var t = go.AddComponent<Text>(); t.font = font; t.alignment = TextAnchor.MiddleCenter; t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.fontStyle = glyph ? FontStyle.Bold : FontStyle.BoldAndItalic; t.raycastTarget = false;
            var o = go.AddComponent<Outline>(); o.effectColor = new Color(12 / 255f, 16 / 255f, 26 / 255f, 0.95f);
            go.SetActive(false);
            return new Item { t = t, o = o, glyph = glyph };
        }
        public void Pop(double x, double y, string text, string color, float life)
        {
            Item it = texts.Find(q => q.life <= 0);
            if (it == null) { if (texts.Count < 8) { it = Make(false); texts.Add(it); } else { it = texts[0]; foreach (var q in texts) if (q.life < it.life) it = q; } }
            it.t.text = text; it.t.color = Th.Hex(color); it.x = x; it.y = y; it.life = it.max = life; it.t.gameObject.SetActive(true);
        }
        public void Glyph(Enemy e, string ch, string color, float life)
        {
            Item it = glyphs.Find(q => q.e == e && q.life > 0) ?? glyphs.Find(q => q.life <= 0);
            if (it == null) { if (glyphs.Count < 12) { it = Make(true); glyphs.Add(it); } else it = glyphs[0]; }
            it.t.text = ch; it.t.color = Th.Hex(color); it.e = e; it.life = it.max = life; it.t.gameObject.SetActive(true);
        }
        // Screen position and pixel height of a world-space sprite of height h at a sim point
        static bool Place(View view, Vector3 world3, float h, out Vector2 at, out float px)
        {
            var cam = view.camera; var w = S.ToUnity(world3);
            var a = cam.WorldToScreenPoint(w); var b = cam.WorldToScreenPoint(w + cam.transform.up * h);
            at = a; px = Mathf.Abs(b.y - a.y);
            return a.z > 0;
        }
        public void Update(float dt, View view)
        {
            float scale = root.lossyScale.x > 0 ? 1 / root.lossyScale.x : 1;
            foreach (var it in texts)
            {
                if (it.life <= 0) { if (it.t.gameObject.activeSelf) it.t.gameObject.SetActive(false); continue; }
                it.life -= dt; float k = 1 - Mathf.Max(0, it.life) / it.max;
                float pop = k < 0.12f ? 0.6f + 4 * k : 1.08f - 0.08f * Mathf.Min(1, (k - 0.12f) * 4), h = 0.8f * pop;
                bool vis = Place(view, S.W(it.x, it.y + k * 0.6, 0.6), h, out var at, out var px);
                it.t.gameObject.SetActive(vis);
                // (the word's glyphs fill about 84/128 of the prototype's sprite height)
                it.t.fontSize = Mathf.Max(4, Mathf.RoundToInt(px * 84f / 128f * scale));
                it.t.rectTransform.position = at;
                it.o.effectDistance = new Vector2(1, -1) * Mathf.Max(1, px * 0.05f * scale);
                var c = it.t.color; c.a = Mathf.Clamp01(Mathf.Max(0, it.life) / 0.2f); it.t.color = c;
            }
            foreach (var it in glyphs)
            {
                if (it.life <= 0) { if (it.t.gameObject.activeSelf) it.t.gameObject.SetActive(false); continue; }
                it.life -= dt;
                var e = it.e; if (e == null || e.dead) { it.life = 0; continue; }
                float k = 1 - Mathf.Max(0, it.life) / it.max, sc = 0.8f * Mathf.Min(1, k / 0.12f);
                bool vis = Place(view, S.W(e.x, e.y + e.h + 0.6 + k * 0.25, 0.4), sc, out var at, out var px);
                it.t.gameObject.SetActive(vis);
                it.t.fontSize = Mathf.Max(4, Mathf.RoundToInt(px * 0.8f * scale));
                it.t.rectTransform.position = at;
                it.o.effectDistance = new Vector2(1, -1) * Mathf.Max(1, px * 0.06f * scale);
                var c = it.t.color; c.a = Mathf.Clamp01(Mathf.Max(0, it.life) / 0.25f); it.t.color = c;
            }
        }
    }
}
