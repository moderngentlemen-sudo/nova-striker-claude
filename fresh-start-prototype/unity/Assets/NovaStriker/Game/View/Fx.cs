// Visual effects (fx.js): particles, glints, telegraph markers, projectiles, barriers, lasers, scarf. The pools
// and primitives every effect module draws with live here; the event reactions are in FxEvents.cs and the
// per-frame syncing of world objects in FxSync.cs.
using System.Collections.Generic;
using NovaStriker.Game.Three;
using NovaStriker.Sim;
using UnityEngine;
using static NovaStriker.Sim.Cfg;

namespace NovaStriker.Game
{
    public sealed class Part { public float life, max = 1, drag = 0.9f, grav, size = 0.3f, grow = 1, op = 0.5f; public Vector3 v; }
    public sealed class FxSpriteItem { public TMesh s; public float life, max = 1, grow = 1, @base = 1, sx = 1; public bool normal; }
    public sealed class FxTextures { public Texture2D glow, star, ring, jag; }

    public sealed partial class Fx
    {
        public readonly TObj scene;
        public readonly Dictionary<Player, Rig> rigs;
        public readonly FxTextures tex = new FxTextures();
        public View view;   // (set by the view: the camera, alpha)

        // Particles (one draw) and smoke (another, ordinary blending)
        const int N = 1600, SN = 500;
        readonly Part[] parts = new Part[N], sparts = new Part[SN];
        readonly Vector3[] pPos = new Vector3[N], sPos = new Vector3[SN];
        readonly Color[] pCol = new Color[N], sCol = new Color[SN];
        readonly float[] pSize = new float[N], pAlpha = new float[N], sSize = new float[SN], sAlpha = new float[SN];
        int pi, si;
        DynMesh points, smokePts;
        readonly List<FxSpriteItem> sprites = new List<FxSpriteItem>();
        readonly FxSpriteItem deniedSprite;
        sealed class Ring { public TMesh m; public float life, max, r0, r1, op; }
        readonly List<Ring> rings = new List<Ring>();

        public readonly Ghosts ghosts;
        public readonly ChargeFX charge;
        public readonly SweepTrails trails;
        public readonly AegisFX aegis;
        public readonly BeamFX beam;
        public readonly SubFX sub;
        public readonly UltFX ult;
        public readonly RamFX ram;
        public readonly FixFX fixfx;
        public readonly NovaShieldFX nshield;
        public readonly FxText texts;
        readonly EmissionClock legacyClock = new EmissionClock();

        public Fx(TObj scene, Dictionary<Player, Rig> rigs)
        {
            this.scene = scene; this.rigs = rigs;
            ParticleBudget.Register(() => { int n = 0; foreach (var p in parts) if (p != null && p.life > 0) n++; foreach (var p in sparts) if (p != null && p.life > 0) n++; return n; }, excess => { int dropped = 0; foreach (var p in sparts) if (p != null && p.life > 0 && dropped < excess) { p.life = 0; dropped++; } foreach (var p in parts) if (p != null && p.life > 0 && dropped < excess) { p.life = 0; dropped++; } return dropped; });
            tex.glow = CanvasTex(64, (g, s) =>
            {
                var r = g.createRadialGradient(s / 2f, s / 2f, 0, s / 2f, s / 2f, s / 2f);
                r.addColorStop(0, "rgba(255,255,255,1)"); r.addColorStop(0.35f, "rgba(255,255,255,0.55)"); r.addColorStop(1, "rgba(255,255,255,0)");
                g.fillStyle = r; g.fillRect(0, 0, s, s);
            });
            tex.star = CanvasTex(128, (g, s) =>
            {
                g.translate(s / 2f, s / 2f); g.fillStyle = "#fff";
                for (int i = 0; i < 2; i++)
                {
                    g.beginPath(); g.moveTo(0, -s * 0.48f); g.lineTo(s * 0.05f, 0); g.lineTo(0, s * 0.48f); g.lineTo(-s * 0.05f, 0); g.closePath(); g.fill();
                    g.rotate(Mathf.PI / 2);
                }
                var r = g.createRadialGradient(0, 0, 0, 0, 0, s * 0.18f); r.addColorStop(0, "rgba(255,255,255,1)"); r.addColorStop(1, "rgba(255,255,255,0)");
                g.fillStyle = r; g.beginPath(); g.arc(0, 0, s * 0.18f, 0, Mathf.PI * 2); g.fill();
            });
            tex.ring = CanvasTex(128, (g, s) => { g.strokeStyle = "#fff"; g.lineWidth = s * 0.07f; g.beginPath(); g.arc(s / 2f, s / 2f, s * 0.4f, 0, Mathf.PI * 2); g.stroke(); });
            tex.jag = CanvasTex(128, (g, s) =>
            {
                g.fillStyle = "rgba(255,255,255,0)"; g.fillRect(0, 0, s, s); g.fillStyle = "#fff";
                for (int i = 0; i < 4; i++) { float x = i * s / 4f; g.beginPath(); g.moveTo(x, s); g.lineTo(x + s / 8f, s * 0.25f); g.lineTo(x + s / 4f, s); g.closePath(); g.fill(); }
            }, repeat: true);

            for (int i = 0; i < N; i++) parts[i] = new Part();
            for (int i = 0; i < SN; i++) sparts[i] = new Part { size = 0.5f, grow = 1, op = 0.5f };
            deniedSprite = new FxSpriteItem { s = new TMesh(Geo.Plane(1, 1), new TMat(TMat.Kind.Basic)) };
            deniedSprite.s.visible = false; scene.add(deniedSprite.s);
            points = QuadPool(N, new TMat(TMat.Kind.Basic) { map = tex.glow, vertexColors = true, transparent = true, depthWrite = false, blending = Blending.Additive, fog = false }, "particles");
            smokePts = QuadPool(SN, new TMat(TMat.Kind.Basic) { map = tex.glow, vertexColors = true, transparent = true, depthWrite = false, fog = false }, "smoke");
            smokePts.obj.RenderOrder = 1;
            for (int i = 0; i < 72; i++)
            {
                var s = Three.Sprite.Make(new TMat(TMat.Kind.Sprite) { map = tex.star, depthWrite = false, blending = Blending.Additive });
                s.visible = false; scene.add(s); sprites.Add(new FxSpriteItem { s = s });
            }
            texts = new FxText(this);
            BuildProjectileTemplates();
            ghosts = new Ghosts(scene);
            charge = new ChargeFX(this);
            trails = new SweepTrails(scene); aegis = new AegisFX(this); beam = new BeamFX(this);
            sub = new SubFX(this); ult = new UltFX(this); ram = new RamFX(this); fixfx = new FixFX(this); nshield = new NovaShieldFX(this);
            for (int i = 0; i < 10; i++)
            {
                var m = new TMesh(Geo.Plane(2, 2), new TMat(TMat.Kind.Basic) { map = tex.ring, transparent = true, opacity = 0, blending = Blending.Additive, depthWrite = false, side = Side.Double });
                m.rotation.x = -Mathf.PI / 2; m.visible = false; scene.add(m); rings.Add(new Ring { m = m });
            }
        }

        public static Texture2D CanvasTex(int size, System.Action<Paint, int> draw, bool repeat = false)
        {
            var c = new Paint(size, size); draw(c, size);
            return c.ToTexture(true, repeat);
        }

        // A pool of camera-facing quads (gl.POINTS in the prototype)
        DynMesh QuadPool(int n, TMat mat, string name)
        {
            var tris = new int[n * 6];
            for (int i = 0; i < n; i++) { int a = i * 4; tris[i * 6] = a; tris[i * 6 + 1] = a + 1; tris[i * 6 + 2] = a + 2; tris[i * 6 + 3] = a; tris[i * 6 + 4] = a + 2; tris[i * 6 + 5] = a + 3; }
            mat.side = Side.Double;
            var d = new DynMesh(n * 4, tris, mat, true, true, name);
            for (int i = 0; i < n; i++) { d.uv[i * 4] = new Vector2(0, 0); d.uv[i * 4 + 1] = new Vector2(1, 0); d.uv[i * 4 + 2] = new Vector2(1, 1); d.uv[i * 4 + 3] = new Vector2(0, 1); }
            scene.add(d.obj);
            return d;
        }
        // gl_PointSize = size * 300 / depth (pixels at a 1080-line screen), as world-space quads facing the camera
        void FillQuads(DynMesh d, int n, Vector3[] pos, Color[] col, float[] size, float[] alpha)
        {
            var cam = view.camera; var ct = cam.transform;
            Vector3 right = S.FromUnity(ct.right), up = S.FromUnity(ct.up), fwd = S.FromUnity(ct.forward), cp = S.FromUnity(ct.position);
            bool ortho = cam.orthographic;
            float perPx = ortho ? 2 * cam.orthographicSize / 1080f : 2 * Mathf.Tan(cam.fieldOfView * Mathf.Deg2Rad / 2) / 1080f;
            for (int i = 0; i < n; i++)
            {
                float a = alpha[i];
                if (a <= 0) { d.pos[i * 4] = d.pos[i * 4 + 1] = d.pos[i * 4 + 2] = d.pos[i * 4 + 3] = Vector3.zero; continue; }
                var p = pos[i];
                float depth = Mathf.Max(0.05f, Vector3.Dot(p - cp, fwd));
                float px = size[i] * 300 / depth, half = px * (ortho ? perPx : perPx * depth) / 2;
                Vector3 r = right * half, u = up * half;
                d.Set(i * 4, p - r - u); d.Set(i * 4 + 1, p + r - u); d.Set(i * 4 + 2, p + r + u); d.Set(i * 4 + 3, p - r + u);
                var c = col[i]; c.a = a;
                d.col[i * 4] = d.col[i * 4 + 1] = d.col[i * 4 + 2] = d.col[i * 4 + 3] = c;
            }
            d.Upload();
        }

        // ---- Smoke or dust puffs (ordinary blending): they grow as they fade ----
        public void Smoke(double x, double y, string color, float n = 6, float speed = 2, float size = 0.6f, float life = 0.6f,
            float depth = 0.15f, float? dir = null, float spread = 1, float drag = 0.92f, float grav = -0.6f, float grow = 1.8f, float op = 0.5f)
        {
            var c = S.Lin(color); var w = W(x, y, depth); var f = Level.Frame(x);
            for (int i = 0; i < n; i++)
            {
                if (FxCfg.Smoke == "off" || !ParticleBudget.Admit(true)) break;
                var P = sparts[si]; int idx = si; si = (si + 1) % SN;
                float a = dir.HasValue ? dir.Value + (S.Rnd() - 0.5f) * (spread == 0 ? 1 : spread) : S.Rnd() * Mathf.PI * 2;
                float sp = speed * (0.4f + S.Rnd() * 0.8f);
                P.v = S.Dir(x, Mathf.Cos(a) * sp, Mathf.Sin(a) * sp);
                P.v.x += (float)f.nx * (S.Rnd() - 0.5f) * sp * 0.3f; P.v.z += (float)f.nz * (S.Rnd() - 0.5f) * sp * 0.3f;
                sPos[idx] = w; sCol[idx] = c;
                P.life = P.max = life * (0.7f + S.Rnd() * 0.6f); P.size = size * (0.7f + S.Rnd() * 0.6f);
                P.drag = drag; P.grav = grav; P.grow = grow; P.op = op;
            }
        }
        void UpdateSmoke(float dt)
        {
            for (int i = 0; i < SN; i++)
            {
                var P = sparts[i];
                if (FxCfg.Smoke == "off") P.life = 0;
                if (P.life <= 0) { sAlpha[i] = 0; continue; }
                P.life -= dt; P.v *= Mathf.Pow(P.drag, dt * 60); P.v.y -= P.grav * dt;
                sPos[i] += P.v * dt;
                float k = Mathf.Max(0, P.life / P.max);
                sAlpha[i] = P.op * Mathf.Min(1, k * 1.6f); sSize[i] = P.size * (1 + (P.grow - 1) * (1 - k));
            }
        }

        // One particle at a world-space point (three.js space); the caller sets its velocity, drag and gravity
        readonly Part denied = new Part();
        public Part Particle(Vector3 v, Color lin, float size, float life)
        {
            if (!ParticleBudget.Admit()) return denied;
                var P = parts[pi]; int idx = pi; pi = (pi + 1) % N;
            pPos[idx] = v; pCol[idx] = lin;
            P.life = P.max = life; P.size = size; P.drag = 0.9f; P.grav = 0; P.v = Vector3.zero;
            return P;
        }
        public Part Particle(Vector3 v, string color, float size, float life) => Particle(v, S.Lin(color), size, life);

        // A flat shockwave ring on the ground at a sim point, growing from r0 to r1 m
        public void GroundRing(double x, double y, string color, float r0, float r1, float life, float opacity = 0.9f) => GroundRing(x, y, S.Lin(color), r0, r1, life, opacity);
        public void GroundRing(double x, double y, Color lin, float r0, float r1, float life, float opacity = 0.9f)
        {
            if (!ParticleBudget.Advancing) return;
            Ring it = null;
            foreach (var q in rings) if (q.life <= 0) { it = q; break; }
            if (it == null) { it = rings[0]; foreach (var q in rings) if (q.life < it.life) it = q; }
            it.m.position.copy(W(x, y + 0.05, 0)); it.m.material.colorLin = lin;
            it.life = life; it.max = life; it.r0 = r0; it.r1 = r1; it.op = opacity; it.m.scale.setScalar(r0); it.m.visible = true;
        }
        void UpdateRings(float dt)
        {
            foreach (var it in rings)
            {
                if (it.life <= 0) { it.m.visible = false; continue; }
                it.life -= dt; float k = 1 - Mathf.Max(0, it.life) / it.max, e = 1 - (1 - k) * (1 - k);
                it.m.scale.setScalar(it.r0 + (it.r1 - it.r0) * e); it.m.material.opacity = it.op * (1 - k);
            }
        }

        // ---- Ground dust ----
        // The floor under a sim point, if it is within `reach` m below
        public double? FloorUnder(double x, double y, double reach = 1.2)
        {
            double g = Level.GroundBelow(x, y + 0.25, Mathf.Clamp(Mathf.RoundToInt((float)(effectDepth / LevelFeatures.LANE_W)), -1, 1));
            return !double.IsNegativeInfinity(g) && y - g <= reach ? g : (double?)null;
        }
        const string DUST = "#b9c1cb";
        static readonly float[] BOTH = { 0, Mathf.PI };
        // Dust thrown along the floor: `dirs` are angles (0 = forward along +x, PI = back), strength 0-1+
        public bool Dust(double x, double y, float strength = 0.5f, float[] dirs = null, float reach = 1.2f, string color = null, float spread = 0.45f, float op = 0.5f, bool noRing = false)
        {
            var g = FloorUnder(x, y, reach); if (g == null) return false;
            float k = strength;
            foreach (var dir in dirs ?? BOTH)
                Smoke(x, g.Value + 0.12, color ?? DUST, Mathf.Max(1, Mathf.Round(2 + 7 * k)), 1.5f + 6 * k, 0.4f + 0.35f * k, 0.45f + 0.35f * k,
                    dir: dir + (dir == Mathf.PI / 2 ? 0 : dir > Mathf.PI / 2 ? -0.12f : 0.12f), spread: spread, drag: 0.88f, grav: -0.35f, grow: 2.2f, op: op);
            if (k >= 0.6f && !noRing) GroundRing(x, g.Value, "#eef2f6", 0.3f, 1 + 1.6f * k, 0.3f, 0.55f);
            return true;
        }
        public void DustSwirl(double x, double y, float k, string color = DUST)
        {
            var g = FloorUnder(x, y, 0.4); if (g == null) return;
            int side = S.Rnd() < 0.5f ? -1 : 1; float d = 0.3f + S.Rnd() * 0.5f;
            Smoke(x + side * d, g.Value + 0.1, color, 1, 1 + 2.5f * k, 0.3f + 0.25f * k, 0.5f, dir: side > 0 ? 0.25f : Mathf.PI - 0.25f, spread: 0.6f, grav: -0.8f, grow: 2.2f, op: 0.35f + 0.2f * k);
        }

        // ---- Particles and sprites ----
        public void Burst(double x, double y, string color, float n = 10, float speed = 5, float size = 0.35f, float life = 0.35f, float? dir = null, float spread = 1, float drag = 0.9f, float grav = 0)
            => Burst(x, y, S.Lin(color), n, speed, size, life, dir, spread, drag, grav);
        public void Burst(double x, double y, Color c, float n = 10, float speed = 5, float size = 0.35f, float life = 0.35f, float? dir = null, float spread = 1, float drag = 0.9f, float grav = 0)
        {
            var w = W(x, y, 0); var f = Level.Frame(x);
            for (int i = 0; i < n; i++)
            {
                if (!ParticleBudget.Admit()) break;
                var P = parts[pi]; int idx = pi; pi = (pi + 1) % N;
                float a = dir.HasValue ? dir.Value + (S.Rnd() - 0.5f) * (spread == 0 ? 1 : spread) : S.Rnd() * Mathf.PI * 2;
                float sp = speed * (0.4f + S.Rnd() * 0.8f);
                P.v = S.Dir(x, Mathf.Cos(a) * sp, Mathf.Sin(a) * sp);
                P.v.x += (float)f.nx * (S.Rnd() - 0.5f) * sp * 0.4f; P.v.z += (float)f.nz * (S.Rnd() - 0.5f) * sp * 0.4f;
                pPos[idx] = w; pCol[idx] = c;
                P.life = P.max = life * (0.7f + S.Rnd() * 0.6f); P.size = size * (0.6f + S.Rnd() * 0.8f);
                P.drag = drag; P.grav = grav;
            }
        }
        public Texture2D Tex(string name) => name == "glow" ? tex.glow : name == "ring" ? tex.ring : name == "jag" ? tex.jag : tex.star;
        public FxSpriteItem Sprite(double x, double y, string texName, string color, float size, float life, float grow = 1.6f, float depth = 0.3f, float sx = 1, float rot = 0)
            => Sprite(x, y, texName, S.Lin(color), size, life, grow, depth, sx, rot);
        public FxSpriteItem Sprite(double x, double y, string texName, Color lin, float size, float life, float grow = 1.6f, float depth = 0.3f, float sx = 1, float rot = 0)
        {
            if (!ParticleBudget.Advancing) return deniedSprite;
            FxSpriteItem it = null;
            foreach (var q in sprites) if (q.life <= 0) { it = q; break; }
            if (it == null) { it = sprites[0]; foreach (var q in sprites) if (q.life < it.life) it = q; }
            if (it.normal) { it.s.material.blending = Blending.Additive; it.normal = false; }
            it.s.material.map = Tex(texName); it.s.material.colorLin = lin; it.s.material.rotation = rot;
            it.s.position.copy(W(x, y, depth)); it.life = it.max = life; it.grow = grow; it.@base = size; it.sx = sx; it.s.visible = true;
            return it;
        }
        // A slash mark: a long thin glint across a hit, at an angle
        public void SlashMark(double x, double y, string color, float len, float rot, float life = 0.14f) => Sprite(x, y, "star", color, len * 0.55f, life, 1.25f, 0.45f, 2.6f, rot);
        // Floating words over a hit ("CRIT"), drawn by FxText on the screen over the world
        public void PopText(double x, double y, string text, string color, float life = 0.7f) => texts.Pop(x, y, text, color, life, effectDepth);
        public void Glyph(Enemy e, string ch, string color, float life = 0.9f) => texts.Glyph(e, ch, color, life);
        // Particles at a world-space point, drifting upward (embers)
        public void BurstAt(Vector3 v, string color, float n = 1, float speed = 1, float size = 0.2f, float life = 0.3f)
        {
            var c = S.Lin(color);
            for (int i = 0; i < n; i++)
            {
                if (!ParticleBudget.Admit()) break;
                var P = parts[pi]; int idx = pi; pi = (pi + 1) % N;
                P.v = new Vector3((S.Rnd() - 0.5f) * speed, S.Rnd() * speed, (S.Rnd() - 0.5f) * speed);
                pPos[idx] = v; pCol[idx] = c;
                P.life = P.max = life * (0.7f + S.Rnd() * 0.6f); P.size = size * (0.6f + S.Rnd() * 0.8f); P.drag = 0.9f; P.grav = -1.5f;
            }
        }
        // A short-lived ball of fire drawn with ordinary blending (reads on bright backgrounds), bright core on top
        public void Fireball(double x, double y, string color, float size, float life)
        {
            var it = Sprite(x, y, "glow", color, size, life, 2.2f, 0.35f);
            it.s.material.blending = Blending.Normal; it.normal = true;
            Sprite(x, y, "glow", "#fff4d6", size * 0.55f, life * 0.7f, 1.8f, 0.4f);
        }

        void UpdateParticles(float dt)
        {
            for (int i = 0; i < N; i++)
            {
                var P = parts[i];
                if (P.life <= 0) { pAlpha[i] = 0; continue; }
                P.life -= dt; P.v *= Mathf.Pow(P.drag, dt * 60); P.v.y -= P.grav * dt;
                pPos[i] += P.v * dt;
                float k = Mathf.Max(0, P.life / P.max);
                pAlpha[i] = k; pSize[i] = P.size * (0.5f + k * 0.5f);
            }
        }
        void UpdateSprites(float dt)
        {
            foreach (var it in sprites)
            {
                if (it.life <= 0) { it.s.visible = false; continue; }
                it.life -= dt; float k = 1 - Mathf.Max(0, it.life) / it.max;
                float sc = it.@base * (1 + (it.grow - 1) * k); it.s.scale.set(sc * it.sx, sc, 1); it.s.material.opacity = 1 - k;
            }
        }

        // ---- Per-frame update ----
        public void Update(float dt, World world, View view)
        {
            this.view = view;
            int steps = legacyClock.TakeSteps(dt);
            bool advancing = ParticleBudget.Advancing;
            try {
                ParticleBudget.Advancing = advancing && steps > 0;
                for (int i = 0; i < Mathf.Max(1, steps); i++) UpdateStep(steps > 0 ? 1f / 60 : 0, world, view);
            } finally { ParticleBudget.Advancing = advancing; }
            // Camera-facing buffers are uploaded once even when two effect steps fit in a rendered frame.
            FillQuads(smokePts, SN, sPos, sCol, sSize, sAlpha);
            FillQuads(points, N, pPos, pCol, pSize, pAlpha);
            int feedback=0;foreach(var s in sprites)if(s.life>0)feedback++;foreach(var r in rings)if(r.life>0)feedback++;
            PerfOverlay.Feedback = feedback; PerfOverlay.ChargeFlashes = charge.ActiveFlashes;
            PerfOverlay.Lights = view.vfx.lights.Active + Sparks.ActiveLights;
        }
        void UpdateStep(float dt, World world, View view)
        {
            renderDt = dt;
            UpdateRings(dt);
            UpdateSmoke(dt);
            UpdateGhosts(world, view);
            ghosts.Update(dt);
            RocketTrails(world);
            WallGrit(world);
            charge.Update(dt, world, view);
            UpdateParticles(dt);
            UpdateSprites(dt);
            UpdateTelegraphs(world);
            trails.Update(dt, world, view.rigs);
            aegis.Update(dt, world);
            beam.Update(dt, world, view);
            sub.Update(dt, world, view); ult.Update(dt, world, view); ram.Update(dt, world, view); fixfx.Update(dt, world, view); nshield.Update(dt, world, view);
            UpdateSlashes(world);
            SlideFx(world, view);
            PoundFx(world, view, dt);
            GroundFx(world, view, dt);
            SyncBossLasers(world);
            BossDamage(world);
            UpdateBooms(dt);
            SyncProjectiles(world, view.alpha);
            foreach (var pr in new List<Projectile>(projectileClock.Keys)) if (pr.dead || !world.projectiles.Contains(pr)) projectileClock.Remove(pr);
            SyncBarriers(world);
            SyncLasers(world);
            SyncShockwaves(world);
            SyncScarves(dt, world, view);
            SyncSnares(world);
            SyncSnaredRings(world);
            SkateSparks(world);
            UpdateMarks(dt);
            ThrusterJets(world, view);
            texts.Update(dt, view);
        }
    }
}
