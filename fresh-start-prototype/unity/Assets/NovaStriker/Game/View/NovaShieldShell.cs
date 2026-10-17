// The tiles of Nova's absorbing shield (NovaShieldFX draws them): a curved shell of hexagonal hard-light tiles
// around his chest, facing his aim and wrapping the arc his block covers (World.GuardFaces). Blue when empty, the
// tiles turn gold one by one as the charge grows, starting where hits land and spreading out from the front; at
// the 150% maximum the whole shell is gold and burns hard. Hits send a ripple over it, worn stability breaks tiles
// off its edges, and it assembles from the edges in when raised.
// It works in the shell's own frame (a = along his aim, b = across it in the play plane, c = toward the camera) and
// uses no Unity types, so a headless preview can run the same code. Each frame it gives every tile a colour, an
// opacity, a size and how far it stands off the shell; NovaShieldFX turns that into one mesh.
using System;

namespace NovaStriker.Game
{
    public sealed class NovaShieldShell
    {
        // The shell: its radius around his chest (wider and taller than him), the hex size (circumradius on its
        // widest row) and the gap between tiles
        public const float R = 1.3f, TILE = 0.165f, GAP = 0.93f;
        // World.GuardFaces blocks what is ahead of a line 0.25 m behind his chest: at the shell's radius that line
        // meets it a little past straight up and straight down, so the shell wraps that far
        public const float BACK = 0.25f;
        public static readonly float THETA_MAX = MathF.PI / 2 + MathF.Asin(BACK / R), PHI_MAX = 50 * MathF.PI / 180;
        const float RIPPLE_SPEED = 6.5f, RIPPLE_W = 0.26f, RIPPLE_LIFE = 0.6f, ASSEMBLE = 0.07f;
        public const int RIPPLES = 4;
        public const byte CRUMBLE = 0, SCATTER = 1, SHATTER = 2;
        // A tile's corners (pointy along the shell's height), unit circumradius
        public static readonly float[] HX = new float[6], HY = new float[6];
        static NovaShieldShell() { for (int k = 0; k < 6; k++) { float q = MathF.PI / 6 + k * MathF.PI / 3; HX[k] = MathF.Cos(q); HY[k] = MathF.Sin(q); } }

        public readonly int n;
        // Layout: each tile's angles around (th) and across (ph) the shell, its scale (tiles shrink toward the
        // shell's top and bottom edges), its direction from his chest (da, db, dc), its two tangents (along th:
        // ta, tb; along ph: pa, pb, pc), how near the shell's edge it is (edge: 0 at the edge, 1 deep inside; rim:
        // 1 on the outer ring), when it turns gold (order: 0 first) and when it breaks off (breakRank: 0 first)
        public readonly float[] th, ph, cs, da, db, dc, ta, tb, pa, pb, pc, edge, rim, order, breakRank, phase;
        // State
        public readonly float[] gold, heat, gate, flare, pres, appear;
        public readonly bool[] missing;
        public float t, perfectGlow, maxK, fillK;
        readonly float[] rAge = new float[RIPPLES], rStr = new float[RIPPLES], rDist;
        int rNext; uint seed = 0x9e3779b9;
        // Colours (linear), set by the view
        public float blueR = 0.12f, blueG = 0.65f, blueB = 1, goldR = 1, goldG = 0.46f, goldB = 0.09f;
        // Out, per tile, each Step: linear HDR colour, opacity, distance off the shell (m) and size (x TILE * cs)
        public readonly float[] outR, outG, outB, outA, lift, size;
        // Tiles that came off since the view last looked (it turns them into drifting debris), and how
        public int detN; public readonly int[] detIdx; public readonly byte[] detKind;

        public NovaShieldShell()
        {
            float psiMax = MathF.Log(MathF.Tan(MathF.PI / 4 + PHI_MAX / 2)), vMax = R * psiMax, uMax = R * THETA_MAX;
            float du = MathF.Sqrt(3) * TILE, dv = 1.5f * TILE;
            int rows = (int)(vMax / dv), cols = (int)(uMax / du) + 2;
            // two passes: count, then fill
            int count = 0;
            for (int pass = 0; pass < 2; pass++)
            {
                int i = 0;
                for (int j = -rows; j <= rows; j++)
                    for (int q = -cols; q <= cols; q++)
                    {
                        float v = j * dv, u = (q + ((j & 1) != 0 ? 0.5f : 0)) * du;
                        if (MathF.Abs(u) > uMax + 0.02f || MathF.Abs(v) > vMax) continue;
                        if (pass == 1)
                        {
                            float the = u / R, phi = 2 * MathF.Atan(MathF.Exp(v / R)) - MathF.PI / 2;   // (inverse Mercator: the tiles keep their shape)
                            th[i] = the; ph[i] = phi; cs[i] = MathF.Cos(phi);
                            da[i] = MathF.Cos(phi) * MathF.Cos(the); db[i] = MathF.Cos(phi) * MathF.Sin(the); dc[i] = MathF.Sin(phi);
                            ta[i] = -MathF.Sin(the); tb[i] = MathF.Cos(the);
                            pa[i] = -MathF.Sin(phi) * MathF.Cos(the); pb[i] = -MathF.Sin(phi) * MathF.Sin(the); pc[i] = MathF.Cos(phi);
                            float m = MathF.Min(uMax - MathF.Abs(u), vMax - MathF.Abs(v));
                            rim[i] = m < 0.2f ? 1 : m < 0.45f ? 0.45f : 0;
                            edge[i] = Clamp01(m / 1.1f);
                            float ang = MathF.Acos(Math.Clamp(da[i], -1, 1));
                            order[i] = Clamp01(0.86f * ang / THETA_MAX + 0.14f * Rnd());
                            breakRank[i] = 0.75f * edge[i] + 0.25f * Rnd();
                            phase[i] = Rnd();
                        }
                        i++;
                    }
                if (pass == 0)
                {
                    count = i; n = count;
                    th = new float[n]; ph = new float[n]; cs = new float[n]; da = new float[n]; db = new float[n]; dc = new float[n];
                    ta = new float[n]; tb = new float[n]; pa = new float[n]; pb = new float[n]; pc = new float[n];
                    edge = new float[n]; rim = new float[n]; order = new float[n]; breakRank = new float[n]; phase = new float[n];
                    gold = new float[n]; heat = new float[n]; gate = new float[n]; flare = new float[n]; pres = new float[n]; appear = new float[n];
                    missing = new bool[n];
                    outR = new float[n]; outG = new float[n]; outB = new float[n]; outA = new float[n]; lift = new float[n]; size = new float[n];
                    detIdx = new int[n * 3]; detKind = new byte[n * 3];
                    rDist = new float[RIPPLES * n];
                }
            }
            for (int r = 0; r < RIPPLES; r++) rAge[r] = 99;
        }

        public float Rnd() { seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5; return (seed & 0xffffff) / 16777216f; }
        static float Hash(int i, int f) { uint h = (uint)(i * 73856093) ^ (uint)(f * 19349663); h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15; return (h & 0xffff) / 65536f; }
        static float Clamp01(float x) => x < 0 ? 0 : x > 1 ? 1 : x;
        float lastThr;
        // How much of the shell's edge has broken away at this stability (0..1 of it)
        static float BreakThreshold(float stab) => Clamp01((0.8f - stab) / 0.8f) * 0.6f;

        void Detach(int i, byte kind) { if (detN < detIdx.Length) { detIdx[detN] = i; detKind[detN] = kind; detN++; } }

        // Raised: every whole tile flies in, the outer ones first
        public void Raise()
        {
            for (int i = 0; i < n; i++)
            {
                missing[i] = breakRank[i] < lastThr;
                pres[i] = 0; appear[i] = 0.085f * edge[i] + 0.03f * Rnd();
            }
        }
        // Lowered (or a Level 4 beam dropped it): the tiles scatter; broken: they shatter outward
        public void Lower(bool shatter)
        {
            for (int i = 0; i < n; i++)
            {
                if (pres[i] > 0.05f && !missing[i]) Detach(i, shatter ? SHATTER : SCATTER);
                pres[i] = 0; appear[i] = 0;
            }
        }
        // A block at angle hth from his aim (on the shell's middle row): a ripple out from it, the tiles there
        // flaring, and the gold the hit brings spreading from it
        public void Hit(float hth, float str, bool perfect)
        {
            int r = rNext; rNext = (rNext + 1) % RIPPLES;
            rAge[r] = 0; rStr[r] = str;
            float ca = MathF.Cos(hth), sa = MathF.Sin(hth);
            for (int i = 0; i < n; i++)
            {
                float d = R * MathF.Acos(Math.Clamp(da[i] * ca + db[i] * sa, -1, 1));
                rDist[r * n + i] = d;
                float h = MathF.Exp(-(d / 0.75f) * (d / 0.75f)); if (h > heat[i]) heat[i] = h;
                gate[i] = d / RIPPLE_SPEED;
                if (d < 0.4f) flare[i] = MathF.Max(flare[i], str * (1 - d / 0.4f));
            }
            if (perfect) perfectGlow = 1;
        }
        public void FlareAll(float v) { for (int i = 0; i < n; i++) flare[i] = MathF.Max(flare[i], v * (0.6f + 0.4f * phase[i])); }

        // absorb: p.absorb (0..150); stab: stability 0..1; up: the shield is raised; cam*: toward the camera, in the
        // shell's frame
        public void Step(float dt, float absorb, float stab, bool up, float camA, float camB, float camC)
        {
            t += dt;
            float k = Clamp01(absorb / 150); fillK = k;
            // How far the gold has filled: 86% of the tiles by 100%, all of them well before 150%
            float F = absorb <= 100 ? 0.86f * absorb / 100 : 0.86f + 0.36f * (absorb - 100) / 50;
            maxK += ((absorb >= 149.5f ? 1 : 0) - maxK) * Clamp01(dt * 4);
            perfectGlow = MathF.Max(0, perfectGlow - dt * 2.5f);
            float thr = BreakThreshold(stab); lastThr = thr;
            for (int r = 0; r < RIPPLES; r++) rAge[r] += dt;
            // a bright band sweeps across the shell now and then
            float sweep = (t / 1.9f) % 1 * (2 * THETA_MAX + 1.2f) - THETA_MAX - 0.6f;
            int frame = (int)(t * 30);
            float charge = (1 + 0.55f * k) * (1 + maxK * (0.9f + 0.3f * MathF.Sin(t * 10)));
            for (int i = 0; i < n; i++)
            {
                heat[i] = MathF.Max(0, heat[i] - dt * 0.12f);
                gate[i] -= dt;
                float target = Clamp01((F - order[i] * (1 - 0.4f * heat[i])) / 0.1f);
                if (gate[i] <= 0)
                {
                    float was = gold[i];
                    gold[i] = target > gold[i] ? MathF.Min(target, gold[i] + dt * 5) : MathF.Max(target, gold[i] - dt * 1.6f);
                    if (was < 0.5f && gold[i] >= 0.5f) flare[i] = MathF.Max(flare[i], 0.7f);   // (a tile flips gold with a flash)
                }
                flare[i] = MathF.Max(0, flare[i] - dt * 5);
                if (!up) missing[i] = breakRank[i] < thr;
                else if (!missing[i])
                {
                    if (appear[i] > 0) appear[i] -= dt;
                    else pres[i] = MathF.Min(1, pres[i] + dt / ASSEMBLE);
                }
                float rip = 0;
                for (int r = 0; r < RIPPLES; r++)
                {
                    if (rAge[r] >= RIPPLE_LIFE) continue;
                    float d = rDist[r * n + i], x = (d - RIPPLE_SPEED * rAge[r]) / RIPPLE_W;
                    rip += rStr[r] * (1 - rAge[r] / RIPPLE_LIFE) * MathF.Exp(-x * x) / (1 + 0.6f * d);
                }
                float dot = da[i] * camA + db[i] * camB + dc[i] * camC, fres = 1 - MathF.Abs(dot);
                float sw = (th[i] - sweep) / 0.2f;
                float shimmer = 1 + 0.1f * MathF.Sin(t * (2.5f + 3 * phase[i]) + phase[i] * 40) + 0.35f * MathF.Exp(-sw * sw);
                bool crack = thr > 0 && breakRank[i] < thr + 0.12f;
                float h = Hash(i, frame), flick = h < (crack ? 0.35f : 0.012f) ? (crack ? 0.25f : 0.55f) : 1;
                float I = (0.32f + 0.6f * fres * fres + 0.45f * rim[i]) * shimmer * charge + 2.6f * rip + 2.4f * flare[i] + 0.5f * perfectGlow;
                float gk = MathF.Max(gold[i], MathF.Min(1, 1.6f * perfectGlow));   // (a perfect block flashes the whole shell gold)
                float br = blueR + (goldR - blueR) * gk, bg = blueG + (goldG - blueG) * gk, bb = blueB + (goldB - blueB) * gk;
                float w = Clamp01(0.75f * flare[i] * (1 - 0.6f * perfectGlow) + 0.4f * rip + (crack ? 0.25f : 0));
                outR[i] = (br + (1 - br) * w) * I; outG[i] = (bg + (1 - bg) * w) * I; outB[i] = (bb + (1 - bb) * w) * I;
                float e = pres[i] * pres[i] * (3 - 2 * pres[i]);
                outA[i] = e * flick;
                lift[i] = 0.07f * rip + (1 - e) * 0.45f;
                size[i] = (0.25f + 0.75f * e) * (1 + 0.1f * flare[i]);
                // worn down while up: an edge tile cracks off
                if (up && !missing[i] && breakRank[i] < thr)
                {
                    missing[i] = true;
                    if (pres[i] > 0.5f) Detach(i, CRUMBLE);
                    pres[i] = 0;
                }
            }
        }
    }
}
