// Nova's Chain drawn as real lightning (any Projectiles setting but Classic, which keeps the old strips). Each hop of
// the sim's chain (bracer → first enemy → the next...) is a jagged bolt made by midpoint displacement, with forks
// branching off it, drawn as line renderers: a white-hot core, a glow in the Chain's colour and a wide faint halo.
// The bolt races out hop by hop, then re-strikes several times over its short life: each strike makes a new jagged
// path at full brightness that dies back before the next. Every struck enemy gets a flash, a light, a ring and a spray
// of sparks; while it is stunned (the sim's shockT), small arcs crawl over it and it crackles. Points are in sim
// space (x along the path, y up) and mapped to the world with S.W, so the bolts follow the level's curves.
using NovaStriker.Game.Three;
using NovaStriker.Sim;
using UnityEngine;
using static NovaStriker.Sim.Cfg;

namespace NovaStriker.Game
{
    public sealed class CineLightning
    {
        const int STRANDS = 120, HOPS = 9, LV = 5, PTS = (1 << LV) + 1, FORKS = 3, FLV = 4, FPTS = (1 << FLV) + 1, CRAWLS = 28, CLV = 3, CPTS = (1 << CLV) + 1;
        const float HOP_DT = 0.022f;   // (how long the bolt takes to race along each hop)

        readonly View view; readonly Transform root;
        readonly Material mCore, mGlow, mHalo;
        sealed class Strand { public LineRenderer core, glow, halo; }
        readonly Strand[] pool = new Strand[STRANDS];
        int used, usedLast;

        sealed class Hop
        {
            public readonly Vector3[] main = new Vector3[PTS];
            public readonly Vector3[][] fork = { new Vector3[FPTS], new Vector3[FPTS], new Vector3[FPTS] };
            public int forks; public bool fizzle, lit;
            public double x0, y0, x1, y1;
        }
        sealed class Bolt
        {
            public bool live, perfect; public float t, life, level, depth, lastStrike, nextStrike, peak; public int hops;
            public readonly Hop[] hop = new Hop[HOPS];
            public Bolt() { for (int i = 0; i < HOPS; i++) hop[i] = new Hop(); }
        }
        readonly Bolt[] bolts = { new Bolt(), new Bolt(), new Bolt(), new Bolt() };
        sealed class Arc { public float life, max, w; public readonly Vector3[] pts = new Vector3[CPTS]; }
        readonly Arc[] crawls = new Arc[CRAWLS];
        int nextCrawl;

        public readonly Color tint;   // (the Chain's colour: SUB_LOOK)
        readonly Color hot, cool;
        static Texture2D soft;

        public CineLightning(View view)
        {
            this.view = view;
            root = new GameObject("chain lightning").transform;
            tint = S.Lin(SUB_LOOK["chain"].tint);
            hot = Color.Lerp(tint, Color.white, 0.8f); cool = new Color(0.75f, 0.88f, 1f);
            mCore = FxPool.Mat(Templates.ParticleAdd, Soft, Hdr(hot, 7)); mGlow = FxPool.Mat(Templates.ParticleAdd, Soft, Hdr(tint, 2.4f)); mHalo = FxPool.Mat(Templates.ParticleAdd, Soft, Hdr(tint, 0.55f));
            LineRenderer Line(string n, Material m, int order)
            {
                var go = new GameObject(n); go.transform.SetParent(root, false);
                var l = go.AddComponent<LineRenderer>();
                l.sharedMaterial = m; l.useWorldSpace = true; l.textureMode = LineTextureMode.Stretch; l.alignment = LineAlignment.View;
                l.numCapVertices = 2; l.numCornerVertices = 0; l.sortingOrder = order;
                l.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; l.receiveShadows = false;
                l.positionCount = 0; l.enabled = false;
                return l;
            }
            for (int i = 0; i < STRANDS; i++) pool[i] = new Strand { halo = Line("halo", mHalo, 0), glow = Line("glow", mGlow, 1), core = Line("core", mCore, 2) };
            for (int i = 0; i < CRAWLS; i++) crawls[i] = new Arc();
        }

        // Soft across the line, even along it
        static Texture2D Soft
        {
            get
            {
                if (soft != null) return soft;
                const int W = 4, H = 64;
                soft = new Texture2D(W, H, TextureFormat.RGBA32, false, true) { wrapMode = TextureWrapMode.Clamp, name = "lightning soft" };
                var px = new Color32[W * H];
                for (int y = 0; y < H; y++)
                {
                    float v = (y + 0.5f) / H * 2 - 1, a = Mathf.Clamp01(Mathf.Exp(-v * v * 5f) * 1.1f - 0.02f);
                    for (int x = 0; x < W; x++) px[y * W + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
                soft.SetPixels32(px); soft.Apply(false, true);
                return soft;
            }
        }

        static Color Hdr(Color c, float k, float a = 1) { var o = c * k; o.a = a; return o; }
        static int N(float n) => Mathf.Max(1, Mathf.RoundToInt(n * FxCfg.Amount));
        static Vector3 W(double x, double y, double depth) => S.W(x, y, depth);

        // ---- The bolt's shape: midpoint displacement ----
        static readonly float[] jx = new float[PTS], jy = new float[PTS], jd = new float[PTS];
        // A jagged line from a to b (sim space, depth d) into jx/jy/jd: 2^levels + 1 points. `amp` is the first
        // displacement as a share of the length; each level halves the segments and shrinks the displacement.
        static int Jag(double ax, double ay, double bx, double by, float d, int levels, float amp, float rough = 0.6f)
        {
            int n = (1 << levels) + 1;
            double dx = bx - ax, dy = by - ay, len = System.Math.Max(1e-3, System.Math.Sqrt(dx * dx + dy * dy));
            float nx = (float)(-dy / len), ny = (float)(dx / len);
            jx[0] = 0; jy[0] = 0; jd[0] = d; jx[n - 1] = (float)dx; jy[n - 1] = (float)dy; jd[n - 1] = d;   // (relative to a: keeps float precision far along the level)
            float disp = (float)len * amp;
            for (int step = n - 1; step > 1; step >>= 1)
            {
                int h = step >> 1;
                for (int i = h; i < n; i += step)
                {
                    float o = disp * (Random.value * 2 - 1);
                    jx[i] = (jx[i - h] + jx[i + h]) * 0.5f + nx * o; jy[i] = (jy[i - h] + jy[i + h]) * 0.5f + ny * o;
                    jd[i] = (jd[i - h] + jd[i + h]) * 0.5f + disp * 0.5f * (Random.value * 2 - 1);
                }
                disp *= rough;
            }
            return n;
        }
        static void ToWorld(double ax, double ay, int n, Vector3[] o) { for (int i = 0; i < n; i++) o[i] = Th.P(W(ax + jx[i], ay + jy[i], jd[i])); }

        // A new jagged path for one hop, with its forks
        void Shape(Bolt b, Hop h)
        {
            double dx = h.x1 - h.x0, dy = h.y1 - h.y0, len = System.Math.Sqrt(dx * dx + dy * dy);
            Jag(h.x0, h.y0, h.x1, h.y1, b.depth, LV, 0.16f + 0.015f * b.level);
            ToWorld(h.x0, h.y0, PTS, h.main);
            // forks leave from points along it, angled away and tapering out
            int want = Mathf.Min(FORKS, Mathf.RoundToInt((1 + 0.5f * b.level + Random.value) * Mathf.Min(1, FxCfg.Amount + 0.2f) * Mathf.Min(1, (float)len / 1.5f)));
            float ox0 = 0, oy0 = 0, od0 = 0, ox1 = 0, oy1 = 0, od1 = 0, ox2 = 0, oy2 = 0, od2 = 0;
            for (int f = 0; f < want; f++)
            {
                int k = PTS / 5 + Random.Range(0, PTS * 3 / 5);
                if (f == 0) { ox0 = jx[k]; oy0 = jy[k]; od0 = jd[k]; } else if (f == 1) { ox1 = jx[k]; oy1 = jy[k]; od1 = jd[k]; } else { ox2 = jx[k]; oy2 = jy[k]; od2 = jd[k]; }
            }
            h.forks = want;
            double a0 = System.Math.Atan2(dy, dx);
            for (int f = 0; f < want; f++)
            {
                float ox = f == 0 ? ox0 : f == 1 ? ox1 : ox2, oy = f == 0 ? oy0 : f == 1 ? oy1 : oy2, od = f == 0 ? od0 : f == 1 ? od1 : od2;
                double a = a0 + (Random.value < 0.5f ? -1 : 1) * (0.35 + Random.value * 0.5), fl = System.Math.Min(2.6, len * (0.2 + Random.value * 0.22) * (0.8 + 0.12 * b.level));
                double fx = h.x0 + ox, fy = h.y0 + oy;
                Jag(fx, fy, fx + System.Math.Cos(a) * fl, fy + System.Math.Sin(a) * fl, od, FLV, 0.24f);
                ToWorld(fx, fy, FPTS, h.fork[f]);
            }
        }

        // ---- Firing ----
        public void Fire(Ev ev)
        {
            if (ev.pts == null || ev.pts.Count < 2) return;
            Bolt b = null; foreach (var q in bolts) if (!q.live) { b = q; break; }
            if (b == null) { b = bolts[0]; foreach (var q in bolts) if (q.t / q.life > b.t / b.life) b = q; }
            float L = (float)ev.level;
            b.live = true; b.perfect = ev.perfect; b.level = L; b.t = 0; b.lastStrike = 0; b.nextStrike = 0; b.peak = 1;
            b.life = 0.34f + 0.07f * L + (ev.perfect ? 0.1f : 0);
            b.depth = 0.25f + (ev.p != null ? (float)(ev.p.laneTo * LevelFeatures.LANE_W) : 0);
            b.hops = Mathf.Min(HOPS, ev.pts.Count - 1);
            for (int i = 0; i < b.hops; i++)
            {
                var h = b.hop[i]; var a = ev.pts[i]; var c = ev.pts[i + 1];
                h.x0 = a.x; h.y0 = a.y; h.x1 = c.x; h.y1 = c.y; h.fizzle = c.fizzle; h.lit = false;
                Shape(b, h);
            }
            // the bracer flares and the frame jolts, harder at each level
            var V = view.vfx; var at = W(ev.pts[0].x, ev.pts[0].y, b.depth);
            V.flash.Emit(at, Vector3.zero, 1.1f + 0.25f * L, 0.08f, Hdr(hot, 6));
            V.glow.Emit(at, Vector3.zero, 1.6f + 0.3f * L, 0.18f, Hdr(tint, 2.4f));
            V.ring.Emit(at, Vector3.zero, 0.9f + 0.2f * L, 0.14f, Hdr(hot, 3));
            V.lights.Flash(at, Color.Lerp(tint, Color.white, 0.4f), 4 + 1.5f * L, 4 + L, 0.18f);
            for (int i = 0, n = N(10 + 4 * L); i < n; i++) V.spark.Emit(at, Random.onUnitSphere * (5 + Random.value * 6), 0.03f, 0.12f + Random.value * 0.18f, Hdr(Random.value < 0.5f ? cool : tint, 5));
            view.Flash(Color.Lerp(tint, Color.white, 0.6f), 0.1f + 0.06f * L + (ev.perfect ? 0.08f : 0));
            view.Kick(0.25f + 0.15f * L);
            view.ShakeAtLeast(0.12f + 0.07f * L);
        }

        // An enemy struck (or the surface it earths on): a flash, a light, a ring and a spray of sparks
        void Strike(Bolt b, Hop h)
        {
            var V = view.vfx; float L = b.level; var at = W(h.x1, h.y1, b.depth + 0.1f);
            if (h.fizzle)
            {
                V.flash.Emit(at, Vector3.zero, 0.9f, 0.07f, Hdr(hot, 5));
                V.glow.Emit(at, Vector3.zero, 1.2f, 0.16f, Hdr(tint, 2));
                for (int i = 0, n = N(12); i < n; i++) V.spark.Emit(at, (Random.onUnitSphere + Vector3.up * 0.6f) * (4 + Random.value * 6), 0.03f, 0.2f + Random.value * 0.3f, Hdr(Random.value < 0.5f ? cool : tint, 5));
                V.decals.Stamp(h.x1, h.y1, 0.7f, "scorch", 4, 0.6f, 0, Random.value * 6.3f);
                return;
            }
            float k = 1 + 0.25f * L + (b.perfect ? 0.3f : 0);
            V.flash.Emit(at, Vector3.zero, 1.4f * k, 0.09f, Hdr(Color.white, 7));
            V.glow.Emit(at, Vector3.zero, 2.4f * k, 0.25f, Hdr(tint, 1.8f));
            V.ring.Emit(at, Vector3.zero, 1.5f * k, 0.2f, Hdr(hot, 3.5f));
            V.lights.Flash(at, Color.Lerp(tint, Color.white, 0.35f), 5 * k, 4.5f * k, 0.25f);
            for (int i = 0, n = N(18 + 7 * L); i < n; i++)
            {
                var d = Random.onUnitSphere;
                V.spark.Emit(at + d * 0.15f, d * (6 + Random.value * 9) + Vector3.up * 1.5f, 0.03f + Random.value * 0.02f, 0.15f + Random.value * 0.35f, Hdr(Random.value < 0.45f ? cool : tint, 5));
            }
            for (int i = 0, n = N(4 + 2 * L); i < n; i++) V.streak.Emit(at, Random.onUnitSphere * (14 + Random.value * 10), 0.05f, 0.06f, Hdr(hot, 5));
            if (FxCfg.Distortion) V.haze.Emit(at, Vector3.zero, 1.6f * k, 0.22f, new Color(1, 1, 1, 0.6f));
            // the enemy's body lights up with arcs at once (then crawls while stunned: Crawl)
            for (int i = 0; i < 2 + (int)L; i++) AddCrawl(h.x1 + (Random.value - 0.5f) * 0.8f, h.y1 + (Random.value - 0.5f) * 1.0f, h.x1 + (Random.value - 0.5f) * 0.8f, h.y1 + (Random.value - 0.5f) * 1.0f, b.depth + 0.15f, 0.08f + Random.value * 0.06f, 1.2f);
        }

        // ---- Shocked enemies crackle: small arcs crawl over them, sparks fly (called each frame while shockT > 0) ----
        public void Crawl(Enemy e, float dt)
        {
            float rate = 14 * Mathf.Min(1, (float)e.shockT / 12) * Mathf.Max(0.4f, FxCfg.Amount);
            if (Random.value > rate * dt) return;
            double x0 = e.x + (Random.value - 0.5f) * e.w, y0 = e.y + Random.value * e.h;
            double a = Random.value * Mathf.PI * 2, r = 0.25 + Random.value * 0.5 * System.Math.Max(e.w, e.h);
            double x1 = System.Math.Max(e.x - e.w * 0.6, System.Math.Min(e.x + e.w * 0.6, x0 + System.Math.Cos(a) * r)), y1 = System.Math.Max(e.y, System.Math.Min(e.y + e.h, y0 + System.Math.Sin(a) * r));
            AddCrawl(x0, y0, x1, y1, 0.35f, 0.05f + Random.value * 0.05f, 1);
            var V = view.vfx; var at = W(x1, y1, 0.4f);
            V.flash.Emit(at, Vector3.zero, 0.35f, 0.05f, Hdr(hot, 5));
            for (int i = 0, n = N(4); i < n; i++) V.spark.Emit(at, Random.onUnitSphere * (3 + Random.value * 5) + Vector3.up, 0.025f, 0.1f + Random.value * 0.2f, Hdr(Random.value < 0.5f ? cool : tint, 5));
        }
        void AddCrawl(double x0, double y0, double x1, double y1, float depth, float life, float w)
        {
            var c = crawls[nextCrawl]; nextCrawl = (nextCrawl + 1) % CRAWLS;
            Jag(x0, y0, x1, y1, depth, CLV, 0.3f, 0.6f); ToWorld(x0, y0, CPTS, c.pts);
            c.life = c.max = life; c.w = w;
        }

        // ---- Each frame ----
        public void Update(float dt)
        {
            usedLast = used; used = 0;
            foreach (var b in bolts)
            {
                if (!b.live) continue;
                b.t += dt;
                if (b.t >= b.life) { b.live = false; continue; }
                // re-strikes: a fresh path at full brightness, dying back until the next
                if (b.t >= b.nextStrike)
                {
                    if (b.t > 0.001f) for (int i = 0; i < b.hops; i++) if (Random.value < 0.8f) Shape(b, b.hop[i]);
                    b.peak = b.t < 0.001f ? 1 : 0.65f + 0.35f * Random.value; b.lastStrike = b.t;
                    b.nextStrike = b.t + 0.035f + Random.value * 0.05f;
                }
                float since = b.t - b.lastStrike, end = Mathf.Clamp01((b.life - b.t) / (b.life * 0.35f));
                float bright = (0.18f + (b.peak - 0.18f) * Mathf.Exp(-since * 26)) * end * (0.85f + 0.15f * Random.value);
                float L = b.level, wk = 1 + 0.18f * L + (b.perfect ? 0.25f : 0);
                for (int i = 0; i < b.hops; i++)
                {
                    float appear = i * HOP_DT; if (b.t < appear) break;
                    var h = b.hop[i];
                    if (!h.lit && b.t >= appear + HOP_DT) { h.lit = true; Strike(b, h); }
                    int n = Mathf.Clamp(Mathf.CeilToInt((b.t - appear) / HOP_DT * PTS), 2, PTS);   // (the leader racing along it)
                    // a struck hop is brightest just after it lands
                    float a = Mathf.Min(1, bright * (h.lit && b.t - appear < 0.08f ? 1.3f : 1));
                    Draw(h.main, n, 0.045f * wk, 0.24f * wk, 0.7f * wk, a, 1);
                    if (n < PTS) continue;
                    for (int f = 0; f < h.forks; f++) Draw(h.fork[f], FPTS, 0.03f * wk, 0.15f * wk, 0, a * 0.75f, 0.15f);
                }
            }
            foreach (var c in crawls)
            {
                if (c.life <= 0) continue;
                c.life -= dt;
                float a = Mathf.Clamp01(c.life / c.max) * (0.6f + 0.4f * Random.value);
                Draw(c.pts, CPTS, 0.022f * c.w, 0.11f * c.w, 0, a, 0.4f);
            }
            for (int i = used; i < usedLast; i++) { var s = pool[i]; s.core.enabled = s.glow.enabled = s.halo.enabled = false; }
        }

        void Draw(Vector3[] pts, int n, float wCore, float wGlow, float wHalo, float a, float taper)
        {
            if (used >= STRANDS || a <= 0.01f) return;
            var s = pool[used++];
            var c0 = new Color(1, 1, 1, a); var c1 = new Color(1, 1, 1, a * (taper < 1 ? 0.4f : 1));
            void Set(LineRenderer l, float w)
            {
                l.enabled = true; l.positionCount = n; l.SetPositions(pts);
                l.startWidth = w; l.endWidth = w * taper; l.startColor = c0; l.endColor = c1;
            }
            Set(s.core, wCore); Set(s.glow, wGlow);
            if (wHalo > 0) Set(s.halo, wHalo); else s.halo.enabled = false;
        }

        public void Hide() { foreach (var b in bolts) b.live = false; foreach (var c in crawls) c.life = 0; Update(0); }
    }
}
