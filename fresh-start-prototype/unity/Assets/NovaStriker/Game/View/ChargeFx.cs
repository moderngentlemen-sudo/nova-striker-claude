// Charge and launch presentation (chargefx.js). While a shot charges, energy gathers at the muzzle as a growing
// orb pulled in from around it, and each attachment has its own look (Lance: streaks drawn in along the aim and a
// sight line at level 3; Volley: one orbiting mote per dart; Arc: a heavy shell core and a dotted trajectory;
// Prism: a spinning crystal). Level-ups flash, the Perfect Release window pulses white, and the release throws a
// muzzle flash, a shockwave ring along the aim and sparks. Also here: ribbon trails behind charged projectiles,
// the rocket apex marker, Echo's laser sight, and the charged-dash aura. Presentation only.
using System.Collections.Generic;
using NovaStriker.Game.Three;
using NovaStriker.Sim;
using UnityEngine;
using static NovaStriker.Sim.Cfg;

namespace NovaStriker.Game
{
    public sealed class ChargeFX
    {
        readonly Fx fx; readonly TObj scene;
        float t;
        static readonly Dictionary<string, (int n, float w)> TRAIL = new Dictionary<string, (int, float)>
        {
            ["lance"] = (8, 0.13f), ["rail"] = (11, 0.2f), ["dart"] = (6, 0.05f), ["shell"] = (10, 0.12f), ["prism"] = (8, 0.12f), ["shard"] = (4, 0.045f),
            ["rifle"] = (7, 0.055f), ["markShot"] = (11, 0.12f), ["deflected"] = (9, 0.12f), ["breach"] = (10, 0.16f), ["hotRivet"] = (6, 0.06f),
        };
        static TMat ribbonMat;

        // A camera-facing strip through the last few positions of something fast, fading toward the tail
        sealed class Ribbon
        {
            public readonly int n; public readonly float width; public readonly Color color; public readonly List<Vector3> pts = new List<Vector3>();
            public float fade = 1; public bool orphan; public readonly DynMesh dm;
            public Ribbon(TObj scene, int n, float width, Color color)
            {
                this.n = n; this.width = width; this.color = color;
                dm = new DynMesh(n * 2, DynMesh.StripTris(n), ribbonMat, true, false, "ribbon");
                dm.obj.RenderOrder = 3; scene.add(dm.obj);
            }
            public void Push(Vector3 v) { pts.Insert(0, v); if (pts.Count > n) pts.RemoveAt(pts.Count - 1); }
            public void Rebuild(Vector3 cam)
            {
                int m = pts.Count;
                for (int i = 0; i < n; i++)
                {
                    int j = Mathf.Min(i, m - 1);
                    if (m == 0) { for (int q = 0; q < n * 2; q++) { dm.pos[q] = Vector3.zero; dm.col[q] = Color.clear; } break; }
                    var p = pts[j];
                    var dir = pts[Mathf.Max(0, j - 1)] - pts[Mathf.Min(m - 1, j + 1)];
                    if (dir.sqrMagnitude < 1e-8f) dir = new Vector3(1, 0, 0); dir.Normalize();
                    var toCam = (cam - p).normalized;
                    float w = i < m ? width * (1 - (float)i / n) : 0;
                    var side = Vector3.Cross(dir, toCam).normalized * w;
                    dm.Set(i * 2, p + side); dm.Set(i * 2 + 1, p - side);
                    float k = i < m ? (1 - (float)i / (n - 1)) * fade : 0;
                    var c = new Color(color.r * k, color.g * k, color.b * k, 1);
                    dm.col[i * 2] = c; dm.col[i * 2 + 1] = c;
                }
                dm.Upload();
            }
            public void Dispose() => dm.obj.DestroyOwnedMaterials();
        }

        sealed class Apex { public TObj group; public TMesh bar, star; public List<TMesh> ticks; public TMesh beam; public TMat mat; }
        sealed class St
        {
            public TMesh orb, core, halo, crystal, sight, laser, laserDot, aura, land;
            public TMesh[] motes;
            public DynMesh dots; public int dotN;
            public Apex apex; public float spin;
        }
        readonly Dictionary<Player, St> state = new Dictionary<Player, St>();
        readonly Dictionary<Projectile, Ribbon> trails = new Dictionary<Projectile, Ribbon>();
        sealed class Flash { public TMesh m; public float life, max, @base = 1, size, grow, r0 = float.NaN, r1, travel; public Vector3 dir, from; public bool sprite, dead; }
        readonly List<Flash> flashes = new List<Flash>();
        public const int MaxFlashes = 80;
        public int ActiveFlashes => flashes.Count;
        void Add(Flash flash) {
            if (flashes.Count >= MaxFlashes) { var old=flashes[0];flashes.RemoveAt(0);Retire(old); }
            flashes.Add(flash);
        }
        static void Retire(Flash flash) {var material=flash.m.material;flash.m.destroy();material.DestroyIfUnused();flash.dead=true;}

        public ChargeFX(Fx fx)
        {
            this.fx = fx; scene = fx.scene;
            if (ribbonMat == null) ribbonMat = new TMat(TMat.Kind.Basic) { vertexColors = true, transparent = true, blending = Blending.Additive, depthWrite = false, side = Side.Double, fog = false };
        }

        TMesh MakeSprite(string tex, Blending blend = Blending.Additive)
        {
            var s = Three.Sprite.Make(new TMat(TMat.Kind.Sprite) { map = fx.Tex(tex), depthWrite = false, blending = blend });
            s.visible = false; s.RenderOrder = 4; scene.add(s); return s;
        }
        public TMesh Line(string color, float radius = 0.02f)
        {
            var m = new TMesh(Geo.Cylinder(radius, radius, 1, 6, 1, true), new TMat(TMat.Kind.Basic) { colorCss = color, transparent = true, opacity = 0.5f, blending = Blending.Additive, depthWrite = false });
            m.visible = false; m.RenderOrder = 3; scene.add(m); return m;
        }
        // Stretch a line mesh between two world points (three.js space)
        public void Span(TMesh m, Vector3 a, Vector3 b)
        {
            var d = b - a; float len = d.magnitude;
            if (len < 1e-3f) { m.visible = false; return; }
            m.position.copy(a + d * 0.5f); m.scale.set(m.scale.x == 0 ? 1 : m.scale.x, len, m.scale.z == 0 ? 1 : m.scale.z);
            m.SetQuaternion(ThQ.FromUnitVectors(Vector3.up, d / len)); m.visible = true;
        }
        public void AddFlash(TMesh m, float life, float @base) => Add(new Flash { m = m, life = life, max = life, @base = @base });

        St Of(Player p)
        {
            if (state.TryGetValue(p, out var Sx)) return Sx;
            Sx = new St
            {
                orb = MakeSprite("glow", Blending.Normal), core = MakeSprite("glow"), halo = MakeSprite("ring"),
                sight = Line("#fff1c9", 0.018f), laser = Line("#ff9a1f", 0.016f), laserDot = MakeSprite("glow"),
            };
            Sx.motes = new TMesh[9]; for (int i = 0; i < 9; i++) Sx.motes[i] = MakeSprite("glow", Blending.Normal);
            Sx.crystal = new TMesh(Geo.Octahedron(0.15f), new TMat { colorCss = "#fff0c8", emissiveCss = "#ffd889", emissiveIntensity = 1.6f, roughness = 0.15f, metalness = 0.3f });
            Sx.crystal.visible = false; Sx.crystal.RenderOrder = 6; scene.add(Sx.crystal);
            // Arc trajectory preview: a row of dots (small camera-facing quads)
            const int n = 22; Sx.dotN = n;
            var tris = new int[n * 6]; for (int i = 0; i < n; i++) { int a = i * 4; tris[i * 6] = a; tris[i * 6 + 1] = a + 1; tris[i * 6 + 2] = a + 2; tris[i * 6 + 3] = a; tris[i * 6 + 4] = a + 2; tris[i * 6 + 5] = a + 3; }
            Sx.dots = new DynMesh(n * 4, tris, new TMat(TMat.Kind.Basic) { colorCss = ATTACH_LOOK["arc"].tint, map = fx.tex.glow, transparent = true, opacity = 0.95f, depthWrite = false, side = Side.Double }, false, true, "arc-dots");
            for (int i = 0; i < n; i++) { Sx.dots.uv[i * 4] = new Vector2(0, 0); Sx.dots.uv[i * 4 + 1] = new Vector2(1, 0); Sx.dots.uv[i * 4 + 2] = new Vector2(1, 1); Sx.dots.uv[i * 4 + 3] = new Vector2(0, 1); }
            Sx.dots.visible = false; scene.add(Sx.dots.obj);
            Sx.aura = new TMesh(Geo.Plane(2.4f, 2.4f), new TMat(TMat.Kind.Basic) { map = fx.tex.ring, transparent = true, opacity = 0.6f, blending = Blending.Additive, depthWrite = false, side = Side.Double });
            Sx.aura.rotation.x = -Mathf.PI / 2; Sx.aura.visible = false; scene.add(Sx.aura);
            Sx.land = new TMesh(Geo.Plane(2, 2), new TMat(TMat.Kind.Basic) { map = fx.tex.ring, colorCss = ATTACH_LOOK["arc"].tint, transparent = true, opacity = 0.55f, blending = Blending.Additive, depthWrite = false, side = Side.Double });
            Sx.land.rotation.x = -Mathf.PI / 2; Sx.land.visible = false; scene.add(Sx.land);
            var apex = Group.Make(); apex.visible = false; scene.add(apex);
            var lineMat = new TMat(TMat.Kind.Basic) { colorCss = "#ffe2a8", transparent = true, opacity = 0.85f, blending = Blending.Additive, depthWrite = false, side = Side.Double };
            var bar = new TMesh(Geo.Plane(1.1f, 0.06f), lineMat); apex.add(bar);
            var star = Three.Sprite.Make(new TMat(TMat.Kind.Sprite) { map = fx.tex.star, colorCss = "#fff1c9", depthWrite = false, blending = Blending.Additive });
            star.scale.setScalar(0.55f); apex.add(star);
            var ticks = new List<TMesh>();
            for (int i = 0; i < 4; i++) { var m = new TMesh(Geo.Plane(0.45f, 0.035f), lineMat.clone()); scene.add(m); m.visible = false; ticks.Add(m); }
            Sx.apex = new Apex { group = apex, bar = bar, star = star, ticks = ticks, beam = Line("#ffe2a8", 0.012f), mat = lineMat };
            state[p] = Sx;
            return Sx;
        }

        // Where the charge gathers: Nova's bracer muzzle, the tip of Echo's rifle, or in front of the chest
        public Vector3 Muzzle(Player p, Rig rig)
        {
            if (rig != null && (p.@char == "nova" || p.@char == "ram" || p.@char == "fix") && rig.extra.muzzle != null) return rig.extra.muzzle.worldPos;
            if (rig != null && p.@char == "echo" && rig.extra.staffTip != null && rig.extra.handStaff.visible) return rig.extra.staffTip.worldPos;
            return S.W(p.x + p.facing * 0.45, p.y + p.h * 0.62, LevelFeatures.Depth(p) + 0.25);
        }

        void Dispose(St Sx)
        {
            foreach (var o in new TObj[] { Sx.orb, Sx.core, Sx.halo, Sx.crystal, Sx.sight, Sx.laser, Sx.laserDot, Sx.aura, Sx.land, Sx.apex.group, Sx.apex.beam }) o.DestroyOwnedMaterials();
            Sx.dots.obj.DestroyOwnedMaterials();
            foreach (var m in Sx.motes) m.DestroyOwnedMaterials(); foreach (var m in Sx.apex.ticks) m.DestroyOwnedMaterials();
        }
        void Hide(St Sx)
        {
            Sx.orb.visible = Sx.core.visible = Sx.halo.visible = Sx.crystal.visible = Sx.sight.visible = Sx.laser.visible = Sx.laserDot.visible = false;
            Sx.dots.visible = Sx.aura.visible = Sx.apex.group.visible = Sx.apex.beam.visible = Sx.land.visible = false;
            foreach (var m in Sx.motes) m.visible = false; foreach (var m in Sx.apex.ticks) m.visible = false;
        }

        public void Update(float dt, World world, View view)
        {
            t += dt;
            var seen = new HashSet<Player>();
            foreach (var p in world.players)
            {
                seen.Add(p);
                var Sx = Of(p); var rig = fx.RigOf(p);
                Hide(Sx);
                if (rig == null || p.state == "downed" || p.state == "dead" || !rig.root.visible) continue;
                if (p.@char == "nova") NovaCharge(p, Sx, rig, world, view);
                else if (p.@char == "echo") EchoRifle(p, Sx, rig, world);
                else WeaponCharge(p, Sx, rig);
                if (p.state == "dashCharge") DashAura(p, Sx, rig);
            }
            foreach (var kv in new List<KeyValuePair<Player, St>>(state)) if (!seen.Contains(kv.Key)) { Dispose(kv.Value); state.Remove(kv.Key); }
            UpdateTrails(dt, view.camPos);
            UpdateFlashes(dt);
        }

        static Color Lin(string c) => S.Lin(c);
        static string Hex(Color lin) => "#" + ColorUtility.ToHtmlStringRGB(lin.gamma);

        // ---- Nova ----
        void NovaCharge(Player p, St Sx, Rig rig, World world, View view)
        {
            bool mk = PlayerSim.marksman(p); var C = MARKSMAN.charge; var B = MARKSMAN.burst.charge;
            string stage = PlayerSim.ChargeStage(p), bstage = mk ? PlayerSim.BurstStage(p) : "";
            float f = mk ? Mathf.Min(1, (float)(p.chargeT / C[2])) : Mathf.Min(1, (float)(p.chargeT / NOVA.charge2));
            float fb = mk ? Mathf.Min(1, (float)(p.burstT / B[2])) : 0;
            double L4 = MARKSMAN.beam.at; bool l4 = mk && p.chargeT >= L4; float f4 = mk ? Mathf.Clamp01((float)((p.chargeT - C[2]) / (L4 - C[2]))) : 0;
            int level = l4 ? 4 : mk ? (p.chargeT >= C[2] ? 3 : p.chargeT >= C[1] ? 2 : p.chargeT >= C[0] ? 1 : 0) : (p.chargeT >= NOVA.charge2 ? 2 : p.chargeT >= NOVA.charge1 ? 1 : 0);
            if (!l4) ApexMarker(p, Sx, world, view);
            if (f <= 0 && fb <= 0) return;
            bool burst = fb > f; float k = Mathf.Max(f, fb) + (burst ? 0 : 0.45f * f4); bool perfect = stage == "perfect" || bstage == "perfect";
            string attach = mk ? p.attachment : "lance"; bool over = p.overcharge > 0;
            string tint = burst ? (SUB_LOOK.TryGetValue(p.sub, out var sl) ? sl.tint : SUB_LOOK["scatter"].tint) : mk ? ATTACH_LOOK[attach].tint : CHARS["nova"].energy;
            if (over || l4) tint = Hex(Color.Lerp(Lin(tint), Color.white, l4 ? 0.55f : 0.3f));
            var at = Muzzle(p, rig);
            float pulse = 1 + Mathf.Sin(t * (perfect || l4 ? 55 : 18)) * (perfect ? 0.22f : l4 ? 0.14f : 0.06f * k);
            float big = !burst && attach == "arc" ? 1.3f : !burst && attach == "prism" && !l4 ? 0.6f : 1;
            Sx.orb.position.copy(at); Sx.orb.material.colorCss = perfect || l4 ? "#fff4d6" : burst ? (p.sub == "scatter" ? "#ff9f40" : tint) : attach == "prism" ? "#ffd889" : tint;
            Sx.orb.scale.setScalar((0.16f + 0.55f * k) * big * pulse); Sx.orb.material.opacity = 0.65f + 0.3f * Mathf.Min(1, k); Sx.orb.visible = true;
            Sx.core.position.copy(at); Sx.core.material.colorCss = "#ffffff"; Sx.core.scale.setScalar((0.1f + 0.3f * k) * big * pulse); Sx.core.visible = true;
            if (level >= 2 || perfect || (burst && fb >= B[1] / B[2]))
            {
                Sx.halo.position.copy(at); Sx.halo.material.colorCss = perfect || l4 ? "#ffffff" : tint;
                Sx.halo.scale.setScalar((0.5f + 0.5f * k) * big * (perfect ? 1.3f + Mathf.Sin(t * 40) * 0.2f : l4 ? 1.5f + Mathf.Sin(t * 24) * 0.12f : 1)); Sx.halo.material.rotation = t * (l4 ? 9 : 3);
                Sx.halo.material.opacity = perfect || l4 ? 0.95f : 0.55f; Sx.halo.visible = true;
            }
            if (!burst && mk && f4 > 0)
            {
                for (int i = 0; i < 1 + f4 * 3; i++)
                {
                    if (S.Rnd() > 0.35f + 0.5f * f4) continue;
                    float a = S.Rnd() * Mathf.PI * 2, r = 0.25f + 0.3f * f4; var P = fx.Particle(at, S.Rnd() < 0.6f ? "#ffffff" : tint, 0.1f + 0.08f * f4, 0.07f);
                    P.v = S.Dir(p.x, Mathf.Cos(a) * r / 0.07f, Mathf.Sin(a) * r / 0.07f); P.drag = 0.8f; P.grav = 0;
                }
            }
            if (l4) BeamPreview(p, Sx, at);
            float rate = 0.6f + (burst ? 2 : Mathf.Min(level, 3)) * 0.9f + (perfect ? 2 : 0) + (l4 ? 2.5f : 0) + (over ? 0.8f : 0);
            var dir = S.Dir(p.x, p.aimX, p.aimY).normalized;
            for (int i = 0; i < rate; i++)
            {
                if (S.Rnd() > rate - i) break;
                if (!burst && attach == "lance")
                {
                    float d = 1.1f + S.Rnd() * 1.3f, j = (S.Rnd() - 0.5f) * 0.5f;
                    Inward(at, dir.x * d, dir.y * d + j, dir.z * d, tint, 0.13f, 0.11f);
                }
                else
                {
                    float a = S.Rnd() * Mathf.PI * 2, r = 0.8f + S.Rnd() * 0.6f; var tv = S.Dir(p.x, Mathf.Cos(a) * r, Mathf.Sin(a) * r);
                    Inward(at, tv.x, tv.y, tv.z, S.Rnd() < 0.3f ? "#ffffff" : tint, 0.15f, 0.2f);
                }
            }
            if (burst || l4) return;
            if (attach == "lance" && level >= 3)
            {
                Span(Sx.sight, at, at + dir * 6); Sx.sight.material.opacity = perfect ? 0.85f : 0.35f; Sx.sight.material.colorCss = perfect ? "#ffffff" : "#fff1c9"; Sx.sight.scale.x = Sx.sight.scale.z = 1;
            }
            else if (attach == "volley")
            {
                int n = (int)MARKSMAN.volley.darts[perfect ? 4 : Mathf.Max(1, level)]; float r = 0.28f + 0.12f * k;
                for (int i = 0; i < n; i++)
                {
                    float a = t * 7 + i * Mathf.PI * 2 / n; var m = Sx.motes[i];
                    var o = S.Dir(p.x, Mathf.Cos(a) * r, Mathf.Sin(a) * r);
                    m.position.copy(at + o); m.material.colorCss = level > 0 ? "#ffc04d" : "#9aa6b8"; m.scale.setScalar(level > 0 ? 0.2f : 0.12f); m.visible = true;
                }
            }
            else if (attach == "arc" && level >= 1) ArcPreview(p, Sx, level, perfect);
            else if (attach == "prism")
            {
                Sx.spin += 0.12f + 0.2f * k;
                Sx.crystal.position.copy(at + dir * 0.25f); Sx.crystal.rotation.set(Sx.spin * 0.7f, Sx.spin, 0); Sx.crystal.scale.setScalar(0.7f + 1.6f * k); Sx.crystal.visible = true;
                if (S.Rnd() < 0.08f + 0.2f * k) FlashAt(at + new Vector3((S.Rnd() - 0.5f) * 0.4f, (S.Rnd() - 0.5f) * 0.4f, 0.05f), "star", ATTACH_LOOK["prism"].tint, 0.25f + 0.3f * k, 0.12f, 1.2f);
            }
        }

        // Level 4 is ready: a flickering white guide line where the beam will go, to the first wall
        void BeamPreview(Player p, St Sx, Vector3 at)
        {
            var c = PlayerSim.Chest(p); var h = Level.RayCast(c.x + p.aimX * 0.6, c.y + p.aimY * 0.6, p.aimX, p.aimY, MARKSMAN.beam.range);
            var end = S.W(h.x, h.y, LevelFeatures.Depth(p) + 0.25);
            Span(Sx.sight, at, end); Sx.sight.material.colorCss = "#ffffff";
            Sx.sight.material.opacity = 0.35f + 0.3f * Mathf.Abs(Mathf.Sin(t * 22)); Sx.sight.scale.x = Sx.sight.scale.z = 1.4f;
        }

        // A particle that flies from `at + (ox, oy, oz)` into `at` over its life
        public void Inward(Vector3 at, float ox, float oy, float oz, string color, float size, float life)
        {
            var P = fx.Particle(new Vector3(at.x + ox, at.y + oy, at.z + oz), color, size, life);
            P.v = new Vector3(-ox / life, -oy / life, -oz / life); P.drag = 1; P.grav = 0;
        }

        // Dotted path of the Arc shell for the current aim, until it meets something solid
        void ArcPreview(Player p, St Sx, int level, bool perfect)
        {
            double cx = p.x, cy = p.y + p.h * 0.62;
            double dx = p.aimX, dy = p.aimY + MARKSMAN.arc.lift, m = JMath.Hypot(dx, dy); if (m == 0) m = 1;
            double x = cx + p.aimX * 0.7, y = cy + p.aimY * 0.7, vx = dx / m * MARKSMAN.arc.speed, vy = dy / m * MARKSMAN.arc.speed;
            const double dt = 0.045;
            var pts = new List<Vector3>(); bool hit = false; int i = 0;
            for (; i < Sx.dotN; i++)
            {
                for (int s = 0; s < 3 && !hit; s++) { vy -= MARKSMAN.arc.gravity * dt / 3; x += vx * dt / 3; y += vy * dt / 3; if (Level.PointInSolid(x, y)) hit = true; }
                pts.Add(S.W(x, y, LevelFeatures.Depth(p) + 0.1));
                if (hit) { i++; break; }
            }
            float sz = 0.18f + 0.04f * level;
            var cam = fx.view.camera.transform; Vector3 right = S.FromUnity(cam.right) * (sz / 2), up = S.FromUnity(cam.up) * (sz / 2);
            for (int j = 0; j < Sx.dotN; j++)
            {
                var c = pts[Mathf.Min(j, pts.Count - 1)];
                Sx.dots.Set(j * 4, c - right - up); Sx.dots.Set(j * 4 + 1, c + right - up); Sx.dots.Set(j * 4 + 2, c + right + up); Sx.dots.Set(j * 4 + 3, c - right + up);
            }
            Sx.dots.Upload();
            Sx.dots.obj.material.colorCss = perfect ? "#fff1c9" : "#ff8a1f"; Sx.dots.visible = true;
            if (hit)
            {
                float r = (float)(MARKSMAN.arc.levels[level].r * (perfect ? MARKSMAN.arc.perfectRadius : 1));
                Sx.land.position.copy(S.W(x, y + 0.05, LevelFeatures.Depth(p) + 0)); Sx.land.scale.setScalar(r * (1 + Mathf.Sin(t * 12) * 0.04f));
                Sx.land.material.colorCss = perfect ? "#ffffff" : ATTACH_LOOK["arc"].tint; Sx.land.visible = true;
            }
        }

        // Rocket apex marker: while he charges aiming at his feet, a line at the height he would reach
        void ApexMarker(Player p, St Sx, World world, View view)
        {
            var pv = world.RocketPreview(p);
            if (pv == null) return;
            var A = Sx.apex; float r = (float)((pv.apex - p.y) / System.Math.Max(0.01, pv.h));
            A.group.position.copy(S.W(pv.x, pv.apex + 0.02, LevelFeatures.Depth(p) + 0.3)); A.group.FaceCamera(view.camera);
            string col = pv.perfect ? "#ffffff" : ATTACH_LOOK[p.attachment].tint;
            A.mat.colorCss = col; A.mat.opacity = pv.perfect ? 0.95f : 0.7f + 0.2f * Mathf.Sin(t * 10);
            A.star.material.colorCss = col; A.star.scale.setScalar(pv.perfect ? 0.8f + Mathf.Sin(t * 40) * 0.15f : 0.5f);
            A.group.visible = true;
            Vector3 head = S.W(p.x, p.y + p.h + 0.15, LevelFeatures.Depth(p) + 0.3), top = S.W(pv.x, pv.apex - 0.05, LevelFeatures.Depth(p) + 0.3);
            Span(A.beam, head, top); A.beam.material.opacity = 0.28f;
            var C = MARKSMAN.charge;
            double[] hs = { PlayerSim.RocketHeight(C[0], p.attachment, false), PlayerSim.RocketHeight(C[1], p.attachment, false), PlayerSim.RocketHeight(C[2], p.attachment, false), PlayerSim.RocketHeight(C[2], p.attachment, true) };
            for (int i = 0; i < 4; i++)
            {
                var m = A.ticks[i]; double y = p.y + hs[i] * r;
                m.position.copy(S.W(pv.x, y, LevelFeatures.Depth(p) + 0.3)); m.FaceCamera(view.camera);
                m.material.colorCss = i == 3 ? "#ffffff" : "#ffe2a8"; m.material.opacity = y <= pv.apex + 0.05 ? 0.55f : 0.22f; m.visible = true;
            }
        }

        // ---- Echo: the sniper's laser follows the exact line the shot will take ----
        void EchoRifle(Player p, St Sx, Rig rig, World world)
        {
            if (p.rifleT < HUNTER.rifle.raise || p.state == "attack") return;
            bool ready = p.rifleCd == 0; float k = (float)PlayerSim.RifleFocus(p.rifleT); bool full = k >= 1;
            var at = Muzzle(p, rig); var c = PlayerSim.Chest(p); double x0 = c.x + p.aimX * 0.9, y0 = c.y + p.aimY * 0.9;
            double tEnd = Level.RayCast(x0, y0, p.aimX, p.aimY, HUNTER.rifle.range).t; Enemy hitE = null;
            foreach (var e in world.enemies)
            {
                if (e.dead) continue;
                var hb = Combat.Hurtbox(e); var h = Level.RayBoxT(x0, y0, p.aimX, p.aimY, hb.x0 - 0.06, hb.y0 - 0.06, hb.x1 + 0.06, hb.y1 + 0.06);
                if (h != null && h.Value.t < tEnd) { tEnd = h.Value.t; hitE = e; }
            }
            bool crit = hitE != null && y0 + p.aimY * (tEnd + 0.15) > hitE.y + hitE.h * HUNTER.rifle.critZone;
            var end = S.W(x0 + p.aimX * tEnd, y0 + p.aimY * tEnd, LevelFeatures.Depth(p) + 0.25);
            Span(Sx.laser, at, end);
            const string red = "#ff2414"; string col = full ? red : "#ff9a1f";
            bool search = hitE == null; float flick = search ? (S.Rnd() < 0.18f ? 0.15f : 0.55f + S.Rnd() * 0.45f) : 1;
            Sx.laser.material.colorCss = col;
            Sx.laser.material.opacity = !ready ? 0.1f : (full ? 0.85f : 0.2f + 0.45f * k) * flick;
            Sx.laser.scale.x = Sx.laser.scale.z = full ? 1.6f : 3.4f - 2.6f * k;
            Sx.laserDot.position.copy(end);
            Sx.laserDot.material.map = crit ? fx.tex.star : fx.tex.glow;
            Sx.laserDot.material.colorCss = full ? red : hitE != null ? "#ffffff" : "#ff9a1f"; Sx.laserDot.material.opacity = flick;
            Sx.laserDot.material.rotation = crit ? t * 4 : 0;
            Sx.laserDot.scale.setScalar((hitE != null ? 0.45f : 0.25f) * (full ? 1.4f : 1) * (crit ? 1.9f : 1)); Sx.laserDot.visible = ready;
            Sx.orb.position.copy(at); Sx.orb.material.colorCss = full ? red : CHARS["echo"].energy;
            Sx.orb.scale.setScalar((0.12f + 0.3f * k) * (full ? 1.2f + Mathf.Sin(t * 30) * 0.15f : 1)); Sx.orb.material.opacity = ready ? 0.9f : 0.35f; Sx.orb.visible = true;
            if (full && ready) { Sx.halo.position.copy(at); Sx.halo.material.colorCss = red; Sx.halo.scale.setScalar(0.55f + Mathf.Sin(t * 18) * 0.08f); Sx.halo.material.rotation = t * 2; Sx.halo.material.opacity = 0.8f; Sx.halo.visible = true; }
            if (ready && S.Rnd() < 0.3f + 0.5f * k)
            {
                float a = S.Rnd() * Mathf.PI * 2, r = 0.5f + S.Rnd() * 0.4f; var tv = S.Dir(p.x, Mathf.Cos(a) * r, Mathf.Sin(a) * r);
                Inward(at, tv.x, tv.y, tv.z, full ? red : CHARS["echo"].energy, 0.12f, 0.16f);
            }
        }

        // ---- RAM's cannon and Fix's rivet gun: an orb at the muzzle that grows through the levels ----
        void WeaponCharge(Player p, St Sx, Rig rig)
        {
            var C = p.@char == "ram" ? RAM.cannon.charge : FIX.rivet.charge;
            if (!(p.chargeT > 0)) return;
            float k = Mathf.Min(1, (float)(p.chargeT / C[2])); int level = p.chargeT >= C[2] ? 3 : p.chargeT >= C[1] ? 2 : p.chargeT >= C[0] ? 1 : 0;
            string tint = CHARS[p.@char].energy; var at = Muzzle(p, rig); float pulse = 1 + Mathf.Sin(t * 18) * 0.06f * k;
            Sx.orb.position.copy(at); Sx.orb.material.colorCss = level >= 3 ? "#ffffff" : tint; Sx.orb.scale.setScalar((0.14f + 0.5f * k) * pulse * (p.@char == "ram" ? 1.3f : 1));
            Sx.orb.material.opacity = 0.65f + 0.3f * k; Sx.orb.visible = true;
            Sx.core.position.copy(at); Sx.core.material.colorCss = "#ffffff"; Sx.core.scale.setScalar((0.08f + 0.26f * k) * pulse); Sx.core.visible = true;
            if (level >= 2) { Sx.halo.position.copy(at); Sx.halo.material.colorCss = tint; Sx.halo.scale.setScalar(0.5f + 0.5f * k); Sx.halo.material.rotation = t * 3; Sx.halo.material.opacity = 0.55f; Sx.halo.visible = true; }
            for (int i = 0; i < 1 + level; i++)
            {
                float a = S.Rnd() * Mathf.PI * 2, r = 0.7f + S.Rnd() * 0.5f; var tv = S.Dir(p.x, Mathf.Cos(a) * r, Mathf.Sin(a) * r);
                Inward(at, tv.x, tv.y, tv.z, S.Rnd() < 0.3f ? "#ffffff" : tint, 0.13f, 0.2f);
            }
        }

        // ---- Both: charged dash ----
        void DashAura(Player p, St Sx, Rig rig)
        {
            var C = DASH_CHARGE.charge; double tt = p.dashChargeT;
            if (tt < DASH_CHARGE.tap) return;
            int level = tt >= C[2] ? 3 : tt >= C[1] ? 2 : tt >= C[0] ? 1 : 0; float k = Mathf.Min(1, (float)(tt / C[2]));
            var col = Color.Lerp(Lin(CHARS[p.@char].energy), Color.white, level >= 3 ? 0.55f : level * 0.15f);
            Sx.aura.position.copy(S.W(p.x, p.y + 0.04, LevelFeatures.Depth(p) + 0));
            Sx.aura.material.colorLin = col; Sx.aura.material.opacity = 0.35f + 0.45f * k;
            Sx.aura.scale.setScalar((0.55f + 0.5f * k) * (1 + Mathf.Sin(t * (10 + level * 8)) * 0.06f)); Sx.aura.visible = true;
            var at = S.W(p.x, p.y + 0.75, LevelFeatures.Depth(p) + 0.1);
            for (int i = 0; i < 1 + level * 1.5f; i++)
            {
                float a = S.Rnd() * Mathf.PI * 2, r = 1.0f + S.Rnd() * 0.6f; var tv = S.Dir(p.x, Mathf.Cos(a) * r, Mathf.Sin(a) * r * 0.8f);
                Inward(at, tv.x, tv.y, tv.z, S.Rnd() < 0.3f ? "#ffffff" : Hex(col), 0.16f, 0.22f);
            }
        }

        // ---- Events ----
        public void LevelUp(Player p, Rig rig, float level, string kind)
        {
            var at = kind == "dash" ? S.W(p.x, p.y + 0.8, LevelFeatures.Depth(p) + 0.2) : Muzzle(p, rig);
            string tint = kind == "burst" ? (SUB_LOOK.TryGetValue(p.sub, out var sl) ? sl.tint : SUB_LOOK["scatter"].tint)
                : kind == "dash" || kind == "pound" || p.@char == "ram" || p.@char == "fix" ? CHARS[p.@char].energy : kind == "rifle" ? CHARS["echo"].energy
                : PlayerSim.marksman(p) ? ATTACH_LOOK[p.attachment].tint : CHARS["nova"].energy;
            bool top = level >= 3;
            FlashAt(at, "ring", top ? "#ffffff" : tint, 0.35f + level * 0.18f, 0.18f, 3.2f);
            FlashAt(at, "star", "#ffffff", 0.35f + level * 0.25f, 0.14f, 1.5f);
            if (level >= 4) { FlashAt(at, "glow", "#fff1c9", 1.6f, 0.2f, 1.8f); ShockRing(at, new Vector3(0, 0, 1), "#ffffff", 0.3f, 1.4f, 0.25f, 0); }
            for (int i = 0; i < 8 + level * 5; i++)
            {
                float a = S.Rnd() * Mathf.PI * 2, sp = 3 + level * 1.5f; var P = fx.Particle(at, S.Rnd() < 0.4f ? "#ffffff" : tint, 0.16f, 0.22f);
                P.v = S.Dir(p.x, Mathf.Cos(a) * sp, Mathf.Sin(a) * sp); P.drag = 0.88f; P.grav = 0;
            }
        }

        // What a release event carries
        public struct Rel { public float level; public bool perfect, rifle, cannon, rivet, mark, beam; public string attach; public double? ax, ay; }
        // A charged shot leaves: flash, a shockwave ring along the aim, sparks, and for a full Lance a white line
        public void Release(Rel ev, Player p, Rig rig)
        {
            float L = ev.level; bool perfect = ev.perfect; string attach = ev.attach ?? "lance";
            var at = Muzzle(p, rig);
            string tint = ev.rifle ? CHARS["echo"].energy : ev.cannon ? CHARS["ram"].energy : ev.rivet ? CHARS["fix"].energy : ATTACH_LOOK.TryGetValue(attach, out var al) ? al.tint : CHARS["nova"].energy;
            var dir = S.Dir(p.x, ev.ax ?? p.aimX, ev.ay ?? p.aimY).normalized;
            float s = (perfect ? 1.5f : 1) * (0.7f + L * 0.35f);
            FlashAt(at, "star", "#ffffff", 0.9f * s, 0.12f, 1.4f);
            FlashAt(at, "glow", perfect ? "#ffffff" : tint, 1.4f * s, 0.16f, 1.8f);
            ShockRing(at, dir, perfect ? "#ffffff" : tint, 0.25f * s, 1.1f * s, 0.2f + 0.03f * L, 1.2f + 0.4f * L);
            for (int i = 0; i < 10 + L * 7 + (perfect ? 10 : 0); i++)
            {
                float spread = 0.35f + S.Rnd() * 0.5f, sp = 7 + S.Rnd() * 9 + L * 2;
                var P = fx.Particle(at, S.Rnd() < 0.35f ? "#ffffff" : tint, 0.18f + 0.05f * L, 0.2f + S.Rnd() * 0.12f);
                P.v = dir * sp + new Vector3((S.Rnd() - 0.5f) * sp * spread, (S.Rnd() - 0.5f) * sp * spread, (S.Rnd() - 0.5f) * sp * spread * 0.5f);
                P.drag = 0.86f; P.grav = 3;
            }
            if (!ev.beam && ((attach == "lance" && (L >= 3 || perfect)) || ev.mark))
            {
                var m = Line(perfect ? "#ffffff" : ev.mark ? "#ffe0b0" : "#fff1c9", perfect ? 0.06f : 0.04f);
                Span(m, at, at + dir * (ev.mark ? 22 : 16));
                Add(new Flash { m = m, life = 0.14f, max = 0.14f, @base = 0.9f });
            }
        }

        // A ring that faces along `dir`, grows from r0 to r1 and moves `travel` m forward as it fades
        public void ShockRing(Vector3 at, Vector3 dir, string color, float r0, float r1, float life, float travel)
        {
            if (!ParticleBudget.Advancing) return;
            var m = new TMesh(Geo.Ring(0.72f, 1, 32), new TMat(TMat.Kind.Basic) { colorCss = color, transparent = true, opacity = 0.8f, blending = Blending.Additive, depthWrite = false, side = Side.Double });
            m.position.copy(at); m.SetQuaternion(ThQ.FromUnitVectors(new Vector3(0, 0, 1), dir.normalized)); m.scale.setScalar(r0); m.RenderOrder = 4;
            scene.add(m);
            Add(new Flash { m = m, life = life, max = life, @base = 0.8f, r0 = r0, r1 = r1, dir = dir, travel = travel, from = at });
        }
        public void FlashAt(Vector3 at, string texName, string color, float size, float life, float grow)
        {
            if (!ParticleBudget.Advancing) return;
            var s = MakeSprite(texName); s.material.colorCss = color; s.position.copy(at); s.scale.setScalar(size); s.visible = true;
            Add(new Flash { m = s, life = life, max = life, @base = 1, size = size, grow = grow, sprite = true });
        }
        void UpdateFlashes(float dt)
        {
            foreach (var f in flashes)
            {
                f.life -= dt; float k = 1 - Mathf.Max(0, f.life) / f.max;
                f.m.material.opacity = f.@base * (1 - k) * (1 - k * 0.3f);
                if (f.sprite) f.m.scale.setScalar(f.size * (1 + (f.grow - 1) * k));
                if (!float.IsNaN(f.r0)) { f.m.scale.setScalar(f.r0 + (f.r1 - f.r0) * (1 - (1 - k) * (1 - k))); f.m.position.copy(f.from + f.dir * (f.travel * k)); }
                if (f.life <= 0) Retire(f);
            }
            flashes.RemoveAll(f => f.dead);
        }

        // ---- Ribbon trails behind charged projectiles ----
        public bool WantsTrail(Projectile pr)
        {
            if (pr.deflected) return true;
            if (pr.team != "p" || pr.kind == null || !TRAIL.ContainsKey(pr.kind)) return false;
            return pr.kind == "rifle" || pr.kind == "markShot" || pr.level >= 1;
        }
        public void Trail(Projectile pr, Vector3 pos)
        {
            if (!trails.TryGetValue(pr, out var r))
            {
                var (n, w) = TRAIL[pr.deflected ? "deflected" : pr.kind]; double lv = pr.deflected ? 1 : (pr.level != 0 ? pr.level : 1);
                string col = pr.reflected || pr.kind == "breach" ? CHARS["ram"].energy : pr.kind == "hotRivet" ? "#ff9a4a" : pr.deflected ? CHARS["echo"].energy : pr.perfect ? "#fff4d6"
                    : pr.kind == "rifle" || pr.kind == "markShot" ? (pr.kind == "markShot" ? "#ffc070" : CHARS["echo"].energy)
                    : pr.kind == "dart" ? ATTACH_LOOK["volley"].tint : pr.kind == "shell" ? ATTACH_LOOK["arc"].tint : pr.kind == "prism" || pr.kind == "shard" ? ATTACH_LOOK["prism"].tint : ATTACH_LOOK["lance"].tint;
                r = new Ribbon(scene, n, w * (0.8f + 0.15f * (float)lv) * (pr.perfect ? 1.3f : 1), Lin(col));
                trails[pr] = r;
            }
            r.Push(pos);
        }
        // Trails whose projectile is no longer in the world (a zone reset clears them without killing them)
        public void OrphanUnseen(HashSet<Projectile> seen) { foreach (var kv in trails) if (!seen.Contains(kv.Key)) kv.Value.orphan = true; }
        void UpdateTrails(float dt, Vector3 cam)
        {
            if (FxCfg.Trails == "off" || FxCfg.Projectiles != "classic") { foreach (var r in trails.Values) r.Dispose(); trails.Clear(); return; }
            foreach (var kv in new List<KeyValuePair<Projectile, Ribbon>>(trails))
            {
                var pr = kv.Key; var r = kv.Value;
                if (pr.dead || r.orphan)
                {
                    r.orphan = true; if (r.pts.Count > 0) r.pts.RemoveAt(r.pts.Count - 1); r.fade -= dt * 6;
                    if (r.pts.Count == 0 || r.fade <= 0) { r.Dispose(); trails.Remove(pr); continue; }
                }
                r.Rebuild(cam);
            }
        }
    }
}
