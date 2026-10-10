// Fix's effects, and the boosts she gives everyone (fixfx.js): the Patch Beam, her gadgets (the Patch Pylon and
// its field, the Sentry turning to its target, the Amp Coil arcing to teammates in its field, Jack-Up's spring
// pad) with their level lights, power-up capsules, Scrap flying to her, Hot Rivets, and Overhaul's supply pod and
// pulses of repair. On every player: a shimmer of Plating, Overclock's sparks. Presentation only.
using System.Collections.Generic;
using NovaStriker.Game.Three;
using NovaStriker.Sim;
using UnityEngine;
using static NovaStriker.Sim.Cfg;

namespace NovaStriker.Game
{
    public sealed class FixFX
    {
        static string MINT => CHARS["fix"].energy;
        const string PALE = "#d8fff0", WHITE = "#ffffff", CYAN = "#6fe3ff", PLATE = "#a9c8ff", HAZARD = "#ffd23f";
        readonly Fx fx; readonly TObj scene; float t; float noScrapT = -9;
        readonly Dictionary<string, TMat> M = new Dictionary<string, TMat>();
        readonly Dictionary<string, TMat> capMats = new Dictionary<string, TMat>();
        readonly Dictionary<string, TMat> pickupGlows = new Dictionary<string, TMat>(), pickupBeams = new Dictionary<string, TMat>();
        sealed class Beam { public Strip glow, core; public readonly List<Vector3> pts = new List<Vector3>(); public float k; }
        readonly Dictionary<Player, Beam> beams = new Dictionary<Player, Beam>();
        sealed class GM { public TObj root, ring, core, field, head, pod, spring, top; public List<TMesh> pips = new List<TMesh>(); public List<TMesh> rings; public string kind; public float born, hit, pop, boing; }
        readonly Dictionary<Gadget, GM> gadgets = new Dictionary<Gadget, GM>();
        readonly Dictionary<Pickup, TObj> pickups = new Dictionary<Pickup, TObj>();
        readonly HashSet<Player> seenBeams = new HashSet<Player>();
        readonly HashSet<Gadget> seenGadgets = new HashSet<Gadget>();
        readonly HashSet<Pickup> seenPickups = new HashSet<Pickup>();
        readonly List<Player> removedBeams = new List<Player>(4);
        readonly List<Gadget> removedGadgets = new List<Gadget>();
        readonly List<Pickup> removedPickups = new List<Pickup>();
        readonly List<(double x, double y)> arcPath = new List<(double, double)>(8);
        sealed class Pod { public TObj g; public float t, drop, fade; public bool alive, landed; public double x, y; }
        readonly Dictionary<Player, Pod> pods = new Dictionary<Player, Pod>();
        sealed class Arc { public Strip glow, core; public float life, max; public List<Vector3> pts; public Color color; }
        readonly List<Arc> arcs = new List<Arc>();

        public FixFX(Fx fx)
        {
            this.fx = fx; scene = fx.scene;
            TMat Std(string c, float r = 0.4f, float m = 0.3f) => new TMat { colorCss = c, roughness = r, metalness = m };
            TMat Glow(string c, float k = 2.6f) => new TMat { colorCss = c, emissiveCss = c, emissiveIntensity = k, roughness = 0.3f };
            M["body"] = Std("#e9ecef"); M["dark"] = Std("#2a2e36", 0.6f, 0.2f); M["teal"] = Std(CHARS["fix"].trim, 0.45f, 0.2f); M["hazard"] = Glow(HAZARD, 0.4f); M["mint"] = Glow(MINT); M["cyan"] = Glow(CYAN); M["amber"] = Glow("#ffcf5a");
            M["off"] = Std("#3a404a", 0.5f, 0.2f);
            M["field"] = new TMat(TMat.Kind.Basic) { map = fx.tex.ring, colorCss = MINT, transparent = true, opacity = 0.4f, blending = Blending.Additive, depthWrite = false, side = Side.Double };
            M["coilField"] = new TMat(TMat.Kind.Basic) { map = fx.tex.ring, colorCss = CYAN, transparent = true, opacity = 0.4f, blending = Blending.Additive, depthWrite = false, side = Side.Double };
            foreach (var k in new List<string>(FIX.powers) { "ultcell", "fury" }) capMats[k] = Glow(FIX_LOOK[k].tint, 1.8f);
            foreach (var material in M.Values) material.retained = true;
            foreach (var material in capMats.Values) material.retained = true;
        }

        // ---- Gadget models ----
        GM Build(Gadget g)
        {
            var root = Group.Make();
            TMesh Add(Mesh geo, TMat mat, float x = 0, float y = 0, float z = 0) { var m = new TMesh(geo, mat, x, y, z); m.cast = true; root.add(m); return m; }
            var G = new GM { root = root, kind = g.kind, born = t };
            if (g.kind == "pylon")
            {
                for (int i = 0; i < 3; i++) { float a = i * 2.1f; var leg = Add(Geo.Cylinder(0.025f, 0.035f, 0.5f, 6), M["dark"], Mathf.Cos(a) * 0.18f, 0.2f, Mathf.Sin(a) * 0.18f); leg.rotation.set(Mathf.Sin(a) * 0.4f, 0, -Mathf.Cos(a) * 0.4f); }
                Add(Geo.Cylinder(0.12f, 0.16f, 0.18f, 12), M["teal"], 0, 0.42f);
                Add(Geo.Capsule(0.09f, 0.5f, 4, 12), M["body"], 0, 0.82f);
                G.ring = Add(Geo.Torus(0.16f, 0.035f, 8, 20), M["mint"], 0, 1.12f); G.ring.rotation.x = Mathf.PI / 2;
                G.core = Add(Geo.Sphere(0.09f, 12, 10), M["mint"], 0, 1.26f);
                var field = new TMesh(Geo.Plane(2, 2), M["field"]); field.rotation.x = -Mathf.PI / 2; field.position.y = 0.04f; root.add(field); G.field = field;
            }
            else if (g.kind == "sentry")
            {
                for (int i = 0; i < 3; i++) { float a = i * 2.1f; var leg = Add(Geo.Cylinder(0.025f, 0.03f, 0.55f, 6), M["dark"], Mathf.Cos(a) * 0.2f, 0.22f, Mathf.Sin(a) * 0.2f); leg.rotation.set(Mathf.Sin(a) * 0.5f, 0, -Mathf.Cos(a) * 0.5f); }
                Add(Geo.Cylinder(0.08f, 0.1f, 0.16f, 10), M["teal"], 0, 0.5f);
                var head = Group.Make(0, 0.74f, 0); root.add(head);
                head.add(new TMesh(Geo.Box(0.34f, 0.22f, 0.26f), M["body"]));
                var stripe = new TMesh(Geo.Box(0.36f, 0.04f, 0.27f), M["hazard"]); stripe.position.y = -0.06f; head.add(stripe);
                var barrel = new TMesh(Geo.Cylinder(0.04f, 0.05f, 0.36f, 10), M["dark"]); barrel.rotation.z = -Mathf.PI / 2; barrel.position.set(0.3f, 0.02f, 0); head.add(barrel);
                var eye = new TMesh(Geo.Sphere(0.045f, 10, 8), M["amber"]); eye.position.set(0.17f, 0.05f, 0.08f); head.add(eye);
                var pod = new TMesh(Geo.Box(0.16f, 0.12f, 0.12f), M["teal"]); pod.position.set(-0.05f, 0.16f, -0.08f); pod.visible = false; head.add(pod);
                G.head = head; G.pod = pod;
            }
            else if (g.kind == "coil")
            {
                Add(Geo.Cylinder(0.2f, 0.26f, 0.14f, 14), M["dark"], 0, 0.07f);
                Add(Geo.Cylinder(0.05f, 0.07f, 1.2f, 10), M["teal"], 0, 0.72f);
                G.rings = new List<TMesh>();
                float[] ys = { 0.45f, 0.75f, 1.05f };
                for (int i = 0; i < 3; i++) { var r = Add(Geo.Torus(0.2f - i * 0.04f, 0.03f, 8, 20), M["cyan"], 0, ys[i]); r.rotation.x = Mathf.PI / 2; G.rings.Add(r); }
                G.core = Add(Geo.Sphere(0.13f, 14, 10), M["cyan"], 0, 1.42f);
                var field = new TMesh(Geo.Plane(2, 2), M["coilField"]); field.rotation.x = -Mathf.PI / 2; field.position.y = 0.05f; root.add(field); G.field = field;
            }
            else
            {
                Add(Geo.Cylinder(0.5f, 0.55f, 0.06f, 18), M["dark"], 0, 0.03f);
                G.spring = Group.Make(); root.add(G.spring);
                for (int i = 0; i < 4; i++) { var r = new TMesh(Geo.Torus(0.28f, 0.03f, 6, 18), M["hazard"]); r.rotation.x = Mathf.PI / 2; r.position.y = 0.08f + i * 0.05f; G.spring.add(r); }
                G.top = Add(Geo.Cylinder(0.46f, 0.46f, 0.05f, 18), M["mint"], 0, 0.3f);
            }
            if (g.kind != "pad") for (int i = 0; i < 3; i++) G.pips.Add(Add(Geo.Sphere(0.035f, 8, 6), M["off"], -0.1f + i * 0.1f, g.kind == "sentry" ? 0.42f : 0.3f, 0.2f));
            scene.add(root);
            return G;
        }
        TObj PickupMesh(string kind, bool level)
        {
            TMat SpriteMaterial(Dictionary<string, TMat> cache, float opacity)
            {
                if (!cache.TryGetValue(kind, out var material)) cache[kind] = material = new TMat(TMat.Kind.Sprite) {
                    map = fx.tex.glow, colorCss = FIX_LOOK[kind].tint, opacity = opacity,
                    blending = Blending.Additive, depthWrite = false, retained = true,
                };
                return material;
            }
            var g = Group.Make(); var body = new TMesh(Geo.Capsule(0.13f, 0.22f, 4, 12), capMats[kind]); body.rotation.z = Mathf.PI / 2; g.add(body);
            if (level)
            {
                g.scale.setScalar(1.7f);
                var beam = Three.Sprite.Make(SpriteMaterial(pickupBeams, 0.35f));
                beam.scale.set(0.35f, 3.2f, 1); beam.position.y = 1.2f; g.add(beam);
            }
            var band = new TMesh(Geo.Torus(0.15f, 0.03f, 6, 16), M["body"]); band.rotation.y = Mathf.PI / 2; g.add(band);
            var glow = Three.Sprite.Make(SpriteMaterial(pickupGlows, 1));
            glow.scale.setScalar(0.9f); g.add(glow);
            scene.add(g);
            return g;
        }

        public void OnEvent(Ev ev)
        {
            var F = fx; var p = ev.p;
            switch (ev.type)
            {
                case "gadgetSelect": case "powerSelect":
                {
                    var L = FIX_LOOK[ev.kind]; F.PopText(p.x, p.y + p.h + 0.7, L.name.ToUpperInvariant(), L.tint, 0.6f);
                    F.Sprite(p.x + p.facing * 0.3, p.y + 1.0, "ring", L.tint, 0.5f, 0.2f, 2); break;
                }
                case "gadgetDeploy":
                {
                    var rig = F.RigOf(p); if (rig != null) rig.craneT = 0.7f;
                    var g = ev.g; F.Burst(g.x, g.gy + 0.4, HAZARD, 14, 4, 0.22f, 0.3f, dir: Mathf.PI / 2, spread: 1.8f, grav: 8); F.Sprite(g.x, g.gy + 0.5, "ring", MINT, 0.9f, 0.25f, 2.4f);
                    F.PopText(g.x, g.gy + 1.9, FIX_LOOK[g.kind].name.ToUpperInvariant(), FIX_LOOK[g.kind].tint, 0.7f);
                    break;
                }
                case "gadgetLand": F.Dust(ev.g.x, ev.g.y, 0.3f, null, noRing: true); break;
                case "gadgetUp":
                {
                    var g = ev.g; F.Sprite(g.x, g.y + 0.8, "star", WHITE, 1.6f, 0.18f, 1.5f); F.Sprite(g.x, g.y + 0.8, "ring", MINT, 1.2f, 0.3f, 2.8f);
                    F.Burst(g.x, g.y + 0.8, MINT, 24, 6, 0.28f, 0.4f); F.PopText(g.x, g.y + 2.0, "LEVEL " + g.level, MINT, 0.75f);
                    if (gadgets.TryGetValue(g, out var G)) G.pop = 1;
                    break;
                }
                case "gadgetWrench": { var g = ev.g; F.Burst(g.x, g.y + 0.6, ev.max ? MINT : HAZARD, 12, 6, 0.2f, 0.25f, grav: 10); F.Sprite(g.x, g.y + 0.6, "star", WHITE, 0.9f, 0.12f, 1.4f); break; }
                case "gadgetHit": { if (gadgets.TryGetValue(ev.g, out var G)) G.hit = 1; F.Burst(ev.g.x, ev.g.y + 0.6, "#ffd2e4", 6, 4, 0.2f, 0.2f); break; }
                case "gadgetEnd":
                {
                    var g = ev.g;
                    if (ev.why == "broken") { F.Burst(g.x, g.y + 0.5, "#5d6674", 18, 7, 0.28f, 0.6f, grav: 14); F.Burst(g.x, g.y + 0.6, HAZARD, 14, 6, 0.24f, 0.35f); F.Smoke(g.x, g.y + 0.5, "#7d8692", 6, 1.2f, 0.5f, 0.8f, op: 0.45f); }
                    else F.Burst(g.x, g.y + 0.5, g.kind == "coil" ? CYAN : MINT, 12, 3, 0.24f, 0.4f, dir: Mathf.PI / 2, spread: 1.2f);
                    break;
                }
                case "sentryShot": F.Sprite(ev.x, ev.y, "star", "#fff1c9", 0.5f, 0.06f, 1.3f); break;
                case "sentryRocket": F.Smoke(ev.x, ev.y, "#8e97a3", 3, 1, 0.35f, 0.5f, op: 0.45f); F.Burst(ev.x, ev.y, "#ffcf5a", 8, 4, 0.22f, 0.2f); break;
                case "padPlace": F.Dust(ev.x, ev.y, 0.5f); F.Sprite(ev.x, ev.y + 0.2, "ring", MINT, 0.9f, 0.25f, 2.4f); break;
                case "padBounce":
                {
                    F.Sprite(ev.x, ev.y + 0.3, "ring", MINT, 1.1f, 0.25f, 2.6f); F.Burst(ev.x, ev.y + 0.3, MINT, 16, 7, 0.24f, 0.3f, dir: Mathf.PI / 2, spread: 1.2f);
                    if (ev.g != null && gadgets.TryGetValue(ev.g, out var G)) G.boing = 1;
                    break;
                }
                case "powerToss": F.Sprite(ev.x, ev.y, "ring", FIX_LOOK[ev.kind].tint, 0.5f, 0.16f, 2); break;
                case "powerUp":
                {
                    var q = ev.p; var c = PlayerSim.Chest(q); var L = FIX_LOOK[ev.kind];
                    F.Sprite(c.x, c.y, "ring", L.tint, 1.4f, 0.35f, 2.6f); F.Burst(c.x, c.y, L.tint, 24, 5, 0.28f, 0.45f); F.PopText(c.x, c.y + 1.3, L.name.ToUpperInvariant(), L.tint, 0.75f);
                    break;
                }
                case "powerFade": F.Burst(ev.x, ev.y, FIX_LOOK[ev.kind].tint, 8, 2, 0.2f, 0.3f); break;
                case "noScrap":
                    if (t - noScrapT < 0.6f) break;
                    noScrapT = t; F.PopText(p.x, p.y + p.h + 0.7, ev.spot ? "NO FLOOR" : "NEED SCRAP", "#ffb4a0", 0.6f);
                    break;
                case "scrap":
                {
                    var c = PlayerSim.Chest(p);
                    for (int i = 0; i < 10; i++)
                    {
                        var w = S.W(ev.x + (S.Rnd() - 0.5f) * 0.6f, ev.y + (S.Rnd() - 0.5f) * 0.6f, (ev.depth ?? 0) + 0.3); var to = S.W(c.x, c.y, LevelFeatures.Depth(p) + 0.3);
                        var pt = F.Particle(w, S.Rnd() < 0.5f ? HAZARD : "#c8cdd4", 0.16f, 0.45f); pt.v = (to - w) * 2.2f; pt.v.y += 2 + S.Rnd() * 2; pt.drag = 0.97f;
                    }
                    break;
                }
                case "rivetStick": F.Sprite(ev.x, ev.y, "star", "#ffcf9a", 0.6f, 0.1f, 1.4f); F.Burst(ev.x, ev.y, "#ffb070", 6, 4, 0.16f, 0.2f, grav: 8); break;
                case "rivetBlast":
                {
                    double x = ev.x, y = ev.y; float r = (float)ev.r;
                    F.Fireball(x, y, "#ff9a4a", 0.6f + r * 0.4f, 0.22f); F.Sprite(x, y, "ring", MINT, r * 0.9f, 0.25f, 2.4f); F.Sprite(x, y, "star", WHITE, r * 0.7f, 0.14f, 1.4f);
                    F.Burst(x, y, HAZARD, 16 + 6 * (ev.level != 0 ? (float)ev.level : 1), 7 + r * 2, 0.3f, 0.35f, grav: 8); F.Dust(x, y, 0.3f, null, reach: 1 + r * 0.4f);
                    break;
                }
                case "sparkRing":
                    F.Sprite(ev.x, ev.y, "ring", CYAN, (float)ev.r * 0.8f, 0.3f, 3); F.Sprite(ev.x, ev.y, "star", WHITE, 1.4f, 0.14f, 1.5f);
                    for (int i = 0; i < 26; i++) { float a = S.Rnd() * Mathf.PI * 2; F.Burst(ev.x + Mathf.Cos(a) * 0.3f, ev.y + Mathf.Sin(a) * 0.3f, S.Rnd() < 0.5f ? WHITE : CYAN, 1, 9, 0.2f, 0.25f, dir: a, spread: 0.2f); }
                    ArcBurst(ev.x, ev.y, (float)ev.r, ev.depth ?? 0);
                    break;
                case "repairPulse": F.GroundRing(ev.x, ev.y, MINT, 0.3f, (float)ev.r, 0.35f, 0.8f); F.Burst(ev.x, ev.y + 0.5, MINT, 14, 4, 0.24f, 0.4f, dir: Mathf.PI / 2, spread: 1.6f); break;
                case "patchOn": case "patchTarget": if (ev.q != null) { var c = PlayerSim.Chest(ev.q); F.Sprite(c.x, c.y, "ring", MINT, 1.0f, 0.25f, 2.2f); } break;
                case "podCall":
                {
                    var P = PodOf(p); P.x = ev.x; P.y = ev.y; P.t = 0; P.drop = (float)ev.ticks / 60; P.landed = false; P.alive = true;
                    F.GroundRing(ev.x, ev.y, MINT, 0.5f, 2.4f, (float)ev.ticks / 60, 0.8f);
                    break;
                }
                case "podLand":
                {
                    double x = ev.x, y = ev.y; var P = PodOf(p); P.landed = true; P.t = 0;
                    F.Sprite(x, y + 1, "star", WHITE, 4, 0.3f, 1.6f); F.Fireball(x, y + 0.6, "#9ff5d0", 2.2f, 0.35f);
                    F.GroundRing(x, y, WHITE, 0.5f, 4.5f, 0.45f, 0.9f); F.GroundRing(x, y, MINT, 0.4f, 3.4f, 0.4f, 0.9f);
                    F.Dust(x, y, 1.3f, null, reach: 2); F.Burst(x, y + 0.4, "#5d6674", 24, 10, 0.32f, 0.7f, dir: Mathf.PI / 2, spread: 2.4f, grav: 18);
                    F.Smoke(x, y + 0.6, "#8e97a3", 10, 2, 1.0f, 1.1f, op: 0.45f, grow: 2.4f, grav: -0.8f);
                    break;
                }
                case "overhaulPulse":
                {
                    double x = ev.x, y = ev.y; float k = 1 + (float)ev.i * 0.25f;
                    foreach (var (sz, life, g) in new[] { (2.5f * k, 0.5f, 6f), (1.6f * k, 0.7f, 8f) }) F.Sprite(x, y, "ring", MINT, sz, life, g);
                    F.Sprite(x, y, "ring", WHITE, 1.2f * k, 0.4f, 7); F.Sprite(x, y, "glow", PALE, 5 * k, 0.4f, 1.6f);
                    F.Burst(x, y, MINT, 60, 16, 0.36f, 0.6f); F.Burst(x, y, WHITE, 24, 10, 0.26f, 0.4f);
                    var fl = F.FloorUnder(x, y, 3); if (fl != null) F.GroundRing(x, fl.Value, MINT, 0.6f, 9 * k, 0.6f, 0.85f);
                    break;
                }
                case "overhaulDone":
                {
                    if (pods.TryGetValue(p, out var P)) P.fade = 0.5f;
                    F.Sprite(ev.x, ev.y + 1.2, "star", WHITE, 3, 0.25f, 1.6f);
                    break;
                }
            }
        }
        Pod PodOf(Player p)
        {
            if (pods.TryGetValue(p, out var P)) return P;
            var g = Group.Make();
            var shell = new TMesh(Geo.Capsule(0.55f, 1.0f, 6, 16), M["body"]); shell.position.y = 1.1f; g.add(shell);
            var band = new TMesh(Geo.Cylinder(0.58f, 0.58f, 0.18f, 18), M["teal"]); band.position.y = 1.0f; g.add(band);
            var lights = new TMesh(Geo.Torus(0.58f, 0.04f, 6, 24), M["mint"]); lights.rotation.x = Mathf.PI / 2; lights.position.y = 1.5f; g.add(lights);
            for (int i = 0; i < 3; i++) { var fin = new TMesh(Geo.Box(0.08f, 0.6f, 0.4f), M["dark"]); float a = i * 2.1f; fin.position.set(Mathf.Cos(a) * 0.55f, 0.45f, Mathf.Sin(a) * 0.55f); fin.rotation.y = -a; g.add(fin); }
            var stripe = new TMesh(Geo.Box(0.06f, 0.9f, 0.02f), M["hazard"]); stripe.position.set(0, 1.2f, 0.56f); g.add(stripe);
            g.visible = false; scene.add(g);
            P = new Pod { g = g }; pods[p] = P;
            return P;
        }
        // A crackle of short arcs from a point
        void ArcBurst(double x, double y, float r, double depth)
        {
            for (int i = 0; i < 4; i++)
            {
                float a = S.Rnd() * Mathf.PI * 2, d = r * (0.6f + S.Rnd() * 0.4f); var pts = arcPath; pts.Clear();
                for (int j = 0; j <= 6; j++) { float u = j / 6f; bool mid = u > 0 && u < 1; pts.Add((x + Mathf.Cos(a) * d * u + (S.Rnd() - 0.5f) * 0.3f * (mid ? 1 : 0), y + Mathf.Sin(a) * d * u + (S.Rnd() - 0.5f) * 0.3f * (mid ? 1 : 0))); }
                AddArc(pts, CYAN, 0.12f, depth);
            }
        }
        void AddArc(List<(double x, double y)> pts, string color, float life, double depth, double? endDepth = null)
        {
            var A = arcs.Find(a => a.life <= 0);
            if (A == null) { if (arcs.Count >= 10) A = arcs[0]; else { A = new Arc { glow = new Strip(scene, 5), core = new Strip(scene, 6) }; arcs.Add(A); } }
            if (A.pts == null) A.pts = new List<Vector3>(8); A.pts.Clear();
            for (int i = 0; i < pts.Count; i++) { var q = pts[i]; double u = i / (double)System.Math.Max(1, pts.Count - 1); A.pts.Add(S.W(q.x, q.y, depth + ((endDepth ?? depth) - depth) * u + .3)); }
            A.life = A.max = life; A.color = S.Lin(color);
        }

        public void Update(float dt, World world, View view)
        {
            t += dt;
            var F = fx; var cam = view.camPos;
            // ---- The Patch Beam ----
            var seenB = seenBeams; seenB.Clear();
            foreach (var p in world.players)
            {
                if (p.@char != "fix" || p.state != "patch" || p.patch == null) continue;
                var rig = F.RigOf(p); if (rig == null || !rig.root.visible) continue;
                seenB.Add(p);
                if (!beams.TryGetValue(p, out var B)) { B = new Beam { glow = new Strip(scene, 4), core = new Strip(scene, 5) }; beams[p] = B; }
                B.k = Mathf.Min(1, B.k + dt * 8);
                var tip = rig.extra.weldTip.worldPos; var q = p.patch.target;
                if (q == null)
                {
                    B.glow.mesh.visible = B.core.mesh.visible = false;
                    for (int i = 0; i < 3; i++) { var pt = F.Particle(tip, S.Rnd() < 0.6f ? "#fff6d0" : HAZARD, 0.12f, 0.3f); pt.v = new Vector3((S.Rnd() - 0.5f) * 4, S.Rnd() * 3, (S.Rnd() - 0.5f) * 2); pt.grav = 9; pt.drag = 0.95f; }
                    continue;
                }
                var end = S.W(q.x, q.y + q.h * (q.state == "downed" ? 0.3 : 0.55), LevelFeatures.Depth(q) + 0.25); const int n = 18; var pts = B.pts; pts.Clear();
                for (int i = 0; i <= n; i++)
                {
                    float u = (float)i / n; var w = Vector3.Lerp(tip, end, u); float s = Mathf.Sin(u * Mathf.PI);
                    w.y += s * (0.35f + 0.1f * Mathf.Sin(t * 9)) + Mathf.Sin(t * 31 + u * 14) * 0.05f * s;
                    pts.Add(w);
                }
                float k = B.k; bool downed = q.state == "downed";
                B.glow.Build(pts, cam, i => (0.2f + 0.06f * Mathf.Sin(t * 20 + i)) * k, i => downed ? new Vector4(1.2f, 1.0f, 0.5f, 0.45f) : new Vector4(0.25f, 1.0f, 0.7f, 0.5f));
                B.core.Build(pts, cam, i => 0.045f * k, i => downed ? new Vector4(2.0f, 1.8f, 1.2f, 1) : new Vector4(0.9f, 2.0f, 1.5f, 1));
                if (S.Rnd() < 0.6f)
                {
                    var w = S.W(q.x + (S.Rnd() - 0.5f) * q.w, q.y + S.Rnd() * q.h, LevelFeatures.Depth(q) + 0.3); var pt = F.Particle(w, S.Rnd() < 0.5f ? PALE : MINT, 0.18f, 0.5f);
                    pt.v = new Vector3(0, 1.6f, 0); pt.drag = 0.98f;
                }
                if (S.Rnd() < 0.4f) { var pt = F.Particle(tip, WHITE, 0.14f, 0.12f); pt.v = new Vector3((S.Rnd() - 0.5f) * 3, S.Rnd() * 2, 0); }
            }
            removedBeams.Clear();
            foreach (var kv in beams) if (!seenB.Contains(kv.Key)) { var B = kv.Value; B.glow.mesh.visible = B.core.mesh.visible = false; B.k = 0; if (!world.players.Contains(kv.Key)) { B.glow.mesh.DestroyOwnedMaterials(); B.core.mesh.DestroyOwnedMaterials(); removedBeams.Add(kv.Key); } }
            foreach (var p in removedBeams) beams.Remove(p);
            // ---- Gadgets ----
            var seenG = seenGadgets; seenG.Clear();
            foreach (var g in world.gadgets)
            {
                seenG.Add(g);
                using var origin = F.AtDepth(g.lane * LevelFeatures.LANE_W);
                if (!gadgets.TryGetValue(g, out var G)) { G = Build(g); gadgets[g] = G; }
                double y = g.py + (g.y - g.py) * view.alpha;
                G.root.position.copy(S.W(g.x, y, g.lane * LevelFeatures.LANE_W));
                G.root.rotation.y = S.YawAt(g.x);
                float age = t - G.born, grow = Mathf.Min(1, age / 0.25f); G.pop = Mathf.Max(0, G.pop - dt * 3);
                G.hit = Mathf.Max(0, G.hit - dt * 6);
                G.root.scale.setScalar(grow * (1 + 0.25f * G.pop));
                float endK = Mathf.Min(1, (float)(g.life - g.t) / 45);
                G.root.visible = endK > 0.05f && (endK > 0.5f || Mathf.FloorToInt(t * 12) % 2 == 0);
                for (int i = 0; i < G.pips.Count; i++) G.pips[i].material = i < g.level ? (g.kind == "coil" ? M["cyan"] : M["mint"]) : M["off"];
                if (g.kind == "pylon")
                {
                    float r = (float)FIX.gadget["pylon"].r[(int)g.level - 1];
                    G.ring.rotation.z += dt * 2; G.core.scale.setScalar(1 + 0.15f * Mathf.Sin(t * 5));
                    G.field.position.z = -(float)(g.lane * LevelFeatures.LANE_W); G.field.scale.set(r, S.AreaSpan(g.x, r * 2) / 2, 1); M["field"].opacity = 0.22f + 0.1f * Mathf.Sin(t * 3);
                    if (S.Rnd() < 0.25f) F.Burst(g.x + (S.Rnd() - 0.5f) * r * 1.4f, g.y + 0.2, MINT, 1, 1, 0.16f, 0.7f, dir: Mathf.PI / 2, spread: 0.2f);
                }
                else if (g.kind == "sentry")
                {
                    double aim = g.aim != 0 ? g.aim : 1;
                    float want = Mathf.Atan2((float)g.aimY, Mathf.Abs((float)aim) < 1e-3f ? 1e-3f : (float)aim);
                    G.head.rotation.z += ((g.aim < 0 ? Mathf.PI - want : want) - G.head.rotation.z) * Mathf.Min(1, dt * 14);
                    G.head.rotation.y = g.aim < 0 ? Mathf.PI : 0;
                    G.pod.visible = g.level >= 3;
                }
                else if (g.kind == "coil")
                {
                    float r = (float)FIX.gadget["coil"].r[(int)g.level - 1];
                    for (int i = 0; i < G.rings.Count; i++) { G.rings[i].rotation.z += dt * (2 + i); G.rings[i].scale.setScalar(1 + 0.1f * Mathf.Sin(t * 8 + i)); }
                    G.field.position.z = -(float)(g.lane * LevelFeatures.LANE_W); G.field.scale.set(r, S.AreaSpan(g.x, r * 2) / 2, 1); M["coilField"].opacity = 0.2f + 0.1f * Mathf.Sin(t * 6);
                    if (S.Rnd() < 0.08f)
                    {
                        double tx = g.x, ty = g.y + 1.42;
                        foreach (var q in world.players)
                        {
                            if (q.state == "dead" || JMath.Hypot(q.x - g.x, q.y + q.h * 0.5 - (g.y + 0.8)) > r) continue;
                            var c = PlayerSim.Chest(q); var pts = arcPath; pts.Clear();
                            for (int j = 0; j <= 7; j++) { double u = j / 7.0; bool mid = j > 0 && j < 7; pts.Add((tx + (c.x - tx) * u + (mid ? (S.Rnd() - 0.5f) * 0.4f : 0), ty + (c.y - ty) * u + (mid ? (S.Rnd() - 0.5f) * 0.4f : 0))); }
                            AddArc(pts, CYAN, 0.1f, g.lane * LevelFeatures.LANE_W, LevelFeatures.Depth(q));
                        }
                    }
                }
                else if (g.kind == "pad")
                {
                    G.boing = Mathf.Max(0, G.boing - dt * 5);
                    G.spring.scale.y = 1 + 1.6f * G.boing; G.top.position.y = 0.3f + 0.3f * G.boing;
                }
                if (G.hit > 0) G.root.position.x += (S.Rnd() - 0.5f) * 0.04f * G.hit;
            }
            removedGadgets.Clear(); foreach (var kv in gadgets) if (!seenG.Contains(kv.Key)) removedGadgets.Add(kv.Key);
            foreach (var g in removedGadgets) { gadgets[g].root.DestroyOwnedMaterials(); gadgets.Remove(g); }
            // ---- Arcs ----
            foreach (var A in arcs)
            {
                if (A.life <= 0) { A.glow.mesh.visible = A.core.mesh.visible = false; continue; }
                A.life -= dt; float k = Mathf.Max(0, A.life / A.max); var c = A.color;
                A.glow.Build(A.pts, cam, i => 0.1f, i => new Vector4(c.r, c.g, c.b, 0.5f * k));
                A.core.Build(A.pts, cam, i => 0.03f, i => new Vector4(1.6f, 1.8f, 2, k));
            }
            // ---- Power-up capsules ----
            var seenK = seenPickups; seenK.Clear();
            foreach (var k in world.pickups)
            {
                seenK.Add(k);
                double depth = k.lane * LevelFeatures.LANE_W;
                if (k.target != null) depth += (LevelFeatures.Depth(k.target) - depth) * Mathf.Min(1, (float)k.t / 12);
                using var origin = F.AtDepth(depth);
                if (!pickups.TryGetValue(k, out var m)) { m = PickupMesh(k.kind, k.level); pickups[k] = m; }
                float a = view.alpha; double x = k.px + (k.x - k.px) * a, y = k.py + (k.y - k.py) * a;
                m.position.copy(S.W(x, y + (k.rest ? 0.12 + Mathf.Sin(t * 4 + k.id) * 0.06 : 0), depth + 0.15));
                m.rotation.y += dt * 3; m.visible = k.life > 90 || Mathf.FloorToInt(t * 10) % 2 == 0;
                if (!k.rest && S.Rnd() < 0.7f) F.Burst(x, y, FIX_LOOK[k.kind].tint, 1, 0.5f, 0.18f, 0.25f);
            }
            removedPickups.Clear(); foreach (var kv in pickups) if (!seenK.Contains(kv.Key)) removedPickups.Add(kv.Key);
            foreach (var k in removedPickups) { pickups[k].DestroyOwnedMaterials(); pickups.Remove(k); }
            // ---- Boosts on everyone ----
            foreach (var p in world.players)
            {
                using var origin = F.AtDepth(LevelFeatures.Depth(p));
                if (p.state == "dead") continue;
                var rig = F.RigOf(p); if (rig == null || !rig.root.visible) continue;
                if (p.plate > 0.5 && S.Rnd() < 0.18f + (float)p.plate / 200)
                {
                    float a = S.Rnd() * Mathf.PI * 2; var c = PlayerSim.Chest(p); double r = p.h * 0.5; var w = S.W(c.x + Mathf.Cos(a) * r * 0.6, c.y + Mathf.Sin(a) * r, LevelFeatures.Depth(p) + 0.35);
                    var pt = F.Particle(w, PLATE, 0.2f, 0.3f); pt.v = new Vector3(0, 0.3f, 0); pt.drag = 0.9f;
                }
                if (p.overclockT > 0 && S.Rnd() < 0.5f)
                {
                    var w = S.W(p.x + (S.Rnd() - 0.5f) * p.w, p.y + S.Rnd() * p.h, LevelFeatures.Depth(p) + 0.35); var pt = F.Particle(w, S.Rnd() < 0.5f ? WHITE : CYAN, 0.16f, 0.3f);
                    pt.v = new Vector3(0, 3 + S.Rnd() * 2, 0); pt.drag = 0.92f;
                }
                if (p.overclockT > 0 && p.onGround && Mathf.Floor(t * 2.2f) != Mathf.Floor((t - dt) * 2.2f)) F.GroundRing(p.x, p.y, CYAN, 0.3f, 1.2f, 0.28f, 0.55f);
                if (p.ampK > 1 && S.Rnd() < 0.12f) { var w = S.W(p.x, p.y + p.h * 0.5, LevelFeatures.Depth(p) + 0.35); var pt = F.Particle(w, CYAN, 0.12f, 0.25f); pt.v = new Vector3((S.Rnd() - 0.5f) * 2, 1.5f, 0); }
            }
            // ---- Overhaul's supply pod ----
            foreach (var kv in pods)
            {
                var p = kv.Key; var P = kv.Value;
                if (!P.alive) { P.g.visible = false; continue; }
                P.t += dt;
                if (P.fade > 0) { P.fade -= dt; if (P.fade <= 0) { P.alive = false; P.g.visible = false; F.Burst(P.x, P.y + 1, MINT, 20, 4, 0.26f, 0.5f, dir: Mathf.PI / 2, spread: 1.6f); continue; } }
                float u = P.landed ? 1 : Mathf.Min(1, P.t / Mathf.Max(0.05f, P.drop));
                double h = (1 - u) * (1 - u) * 22;
                P.g.position.copy(S.W(P.x, P.y + h, 0));
                P.g.rotation.y = S.YawAt(P.x); P.g.visible = true;
                P.g.scale.setScalar(P.fade > 0 ? Mathf.Max(0.01f, P.fade / 0.5f) : 1);
                if (!P.landed) { F.Burst(P.x, P.y + h + 2.2, "#fff1c9", 2, 3, 0.4f, 0.2f, dir: Mathf.PI / 2, spread: 0.4f); F.Smoke(P.x, P.y + h + 2.4, "#8e97a3", 1, 1, 0.5f, 0.6f, op: 0.4f); }
                else if (S.Rnd() < 0.3f) F.Burst(P.x, P.y + 1.6, MINT, 1, 2, 0.2f, 0.4f, dir: Mathf.PI / 2, spread: 0.6f);
                if (!world.players.Contains(p)) { P.alive = false; P.g.visible = false; }
            }
        }
    }
}
