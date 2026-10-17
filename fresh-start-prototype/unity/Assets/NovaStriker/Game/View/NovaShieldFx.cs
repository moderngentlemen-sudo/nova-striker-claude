// Nova's absorbing shield (Settings: Nova's LT move = shield; Unity build only): a personal energy shield, a curved
// shell of hexagonal hard-light tiles hugging him in front, facing his aim and wrapping exactly the arc the sim
// blocks (NovaShieldShell keeps the tiles; this draws them as one mesh and adds the particles). It starts as a blue
// honeycomb (#5fd4ff) and turns gold (his energy colour) tile by tile as the charge grows, from where hits land
// and out from the front: almost all gold at 100%, all of it burning hard at the 150% maximum. Every block sends
// a ripple across it from the impact with a flash and sparks (a perfect block flashes the whole shell gold); worn
// thin, tiles crack off its edges and drift away; broken, it shatters outward. Raised, it assembles from the edges
// in; lowered (or dropped by a Level 4 beam), its tiles scatter. A soft aura grows around him with the stored
// energy (with Anim.AbsorbGlow on his suit), so he visibly powers up with each hit it takes. Presentation only.
using System.Collections.Generic;
using NovaStriker.Game.Three;
using NovaStriker.Sim;
using UnityEngine;
using static NovaStriker.Sim.Cfg;

namespace NovaStriker.Game
{
    public sealed class NovaShieldFX
    {
        const string BLUE = "#5fd4ff", PALE = "#e0f6ff", WHITE = "#ffffff", HOT = "#b8ecff", GOLD_PALE = "#ffe7b0";
        readonly Fx fx; readonly TObj scene; float t;
        static Texture2D tileTex;
        readonly Color blueLin, goldLin;
        Vfx V => fx.view?.vfx;

        sealed class Pane
        {
            public NovaShieldShell shell; public DynMesh mesh; public int n;
            public TMesh halo, aura; public TMat haloM, auraM;
            public float k, gx = 1, gy, fleck; public int level; public bool wasUp, drawn;
            // the shell's frame this frame (three.js space): centre, along his aim, across it, toward the camera
            public Vector3 O, F, U, Z;
            // tiles broken off, drifting away (world space)
            public Vector3[] dC, dV, dT, dP; public Color[] dCol; public float[] dAge, dLife, dSpin, dGrav, dSize; public byte[] dKind; public int dNext;
        }
        readonly Dictionary<Player, Pane> panes = new Dictionary<Player, Pane>();
        readonly HashSet<Player> seen = new HashSet<Player>();
        readonly List<Player> gone = new List<Player>();

        public NovaShieldFX(Fx fx)
        {
            this.fx = fx; scene = fx.scene;
            blueLin = S.Lin(BLUE); goldLin = S.Lin(CHARS["nova"].energy);
            if (tileTex == null) tileTex = HexTileTex();
        }

        // One hex tile's brightness from its centre (u = 0) to its edge (u = 1): a dim fill, a bright edge line
        // and a thin dark gap (the tile's fan carries u, so the pattern follows the hexagon)
        static Texture2D HexTileTex()
        {
            const int W = 64;
            var tx = new Texture2D(W, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "nova shield tile" };
            var px = new Color32[W * 4];
            for (int x = 0; x < W; x++)
            {
                float u = x / (W - 1f), line = (u - 0.86f) / 0.055f;
                float v = Mathf.Min(1, 0.16f + 0.24f * u * u * u + 0.95f * Mathf.Exp(-line * line));
                if (u > 0.96f) v *= Mathf.Clamp01((1 - u) / 0.04f);
                for (int y = 0; y < 4; y++) px[y * W + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(v * 255));
            }
            tx.SetPixels32(px); tx.Apply(false, true);
            return tx;
        }

        Pane PaneOf(Player p)
        {
            if (panes.TryGetValue(p, out var P)) return P;
            var sh = new NovaShieldShell();
            sh.blueR = blueLin.r; sh.blueG = blueLin.g; sh.blueB = blueLin.b; sh.goldR = goldLin.r; sh.goldG = goldLin.g; sh.goldB = goldLin.b;
            int n = sh.n, slots = n * 2;   // (the tiles on the shell, then as many drifting pieces)
            var tris = new int[slots * 18];
            for (int s = 0; s < slots; s++)
                for (int k = 0; k < 6; k++) { int o = s * 18 + k * 3; tris[o] = s * 7; tris[o + 1] = s * 7 + 1 + k; tris[o + 2] = s * 7 + 1 + (k + 1) % 6; }
            var mat = new TMat(TMat.Kind.Basic) { map = tileTex, vertexColors = true, transparent = true, blending = Blending.Additive, depthWrite = false, side = Side.Double, fog = false };
            var dm = new DynMesh(slots * 7, tris, mat, true, true, "nova shield");
            for (int s = 0; s < slots; s++) { dm.uv[s * 7] = new Vector2(0, 0.5f); for (int k = 1; k < 7; k++) dm.uv[s * 7 + k] = new Vector2(1, 0.5f); }
            dm.obj.RenderOrder = 5; dm.visible = false; scene.add(dm.obj);
            P = new Pane
            {
                shell = sh, mesh = dm, n = n,
                dC = new Vector3[n], dV = new Vector3[n], dT = new Vector3[n], dP = new Vector3[n], dCol = new Color[n],
                dAge = new float[n], dLife = new float[n], dSpin = new float[n], dGrav = new float[n], dSize = new float[n], dKind = new byte[n],
            };
            P.haloM = new TMat(TMat.Kind.Sprite) { map = fx.tex.glow, colorCss = BLUE, transparent = true, opacity = 0, depthWrite = false, blending = Blending.Additive, fog = false };
            P.halo = Three.Sprite.Make(P.haloM); P.halo.visible = false; P.halo.RenderOrder = 4; scene.add(P.halo);
            P.auraM = new TMat(TMat.Kind.Sprite) { map = fx.tex.glow, colorCss = BLUE, transparent = true, opacity = 0, depthWrite = false, blending = Blending.Additive, fog = false };
            P.aura = Three.Sprite.Make(P.auraM); P.aura.visible = false; P.aura.RenderOrder = 4; scene.add(P.aura);
            panes[p] = P; return P;
        }

        // The shell's frame: centred on his chest, turned to his (smoothed) aim on the play path
        static void Anchor(Player p, Pane P)
        {
            var c = PlayerSim.Chest(p); var f = Level.Frame(c.x);
            P.O = S.W(c.x, c.y, 0);
            P.F = S.Dir(c.x, P.gx, P.gy).normalized; P.U = S.Dir(c.x, -P.gy, P.gx).normalized;
            P.Z = new Vector3((float)f.nx, 0, (float)f.nz).normalized;
        }
        static void SnapAim(Player p, Pane P) { P.gx = (float)p.guardDir.x; P.gy = (float)p.guardDir.y; }
        // The point on the shell's middle row at angle a from his aim, and that angle for a sim point
        static Vector3 OnShell(Pane P, float a, float r = NovaShieldShell.R) => P.O + (P.F * Mathf.Cos(a) + P.U * Mathf.Sin(a)) * r;
        static float AngleOf(Player p, Pane P, double x, double y)
        {
            var c = PlayerSim.Chest(p); float dx = (float)(x - c.x), dy = (float)(y - c.y);
            float a = Mathf.Atan2(dx * -P.gy + dy * P.gx, dx * P.gx + dy * P.gy), m = NovaShieldShell.THETA_MAX;
            return Mathf.Clamp(a, -m, m);
        }
        // A sim point in front of his chest along the shell's aim, k of the way out to it
        static (double, double) SimFront(Player p, Pane P, float k) { var c = PlayerSim.Chest(p); return (c.x + P.gx * NovaShieldShell.R * k, c.y + P.gy * NovaShieldShell.R * k); }
        Color TileCol(Pane P, int i) => new Color(P.shell.outR[i], P.shell.outG[i], P.shell.outB[i], 1);
        // The shell's overall colour now: blue, through the fill, to gold
        Color ShellCol(Pane P) => Color.Lerp(blueLin, goldLin, Mathf.Clamp01(P.shell.fillK * 1.35f));
        static Color Hdr(Color lin, float k) { var c = lin * k; c.a = 1; return c; }
        static int Count(float n) => Mathf.Max(1, Mathf.RoundToInt(n * FxCfg.Amount));
        static Vector3 Rnd3() => Random.insideUnitSphere;
        Vector3 TileDir(Pane P, int i) { var s = P.shell; return P.F * s.da[i] + P.U * s.db[i] + P.Z * s.dc[i]; }

        // Tiles that came off the shell become drifting pieces: cracked off the edge, scattered as it is lowered, or
        // shattered when it breaks
        void TakeDetached(Pane P)
        {
            var s = P.shell;
            for (int q = 0; q < s.detN; q++)
            {
                int i = s.detIdx[q]; byte kind = s.detKind[q]; int d = P.dNext; P.dNext = (P.dNext + 1) % P.n;
                var N = TileDir(P, i);
                P.dC[d] = P.O + N * (NovaShieldShell.R + s.lift[i]);
                P.dT[d] = P.F * s.ta[i] + P.U * s.tb[i]; P.dP[d] = P.F * s.pa[i] + P.U * s.pb[i] + P.Z * s.pc[i];
                P.dSize[d] = NovaShieldShell.TILE * s.cs[i] * NovaShieldShell.GAP * Mathf.Max(0.6f, s.size[i]);
                var col = TileCol(P, i); col.a = Mathf.Max(0.6f, s.outA[i]);
                P.dAge[d] = 0; P.dKind[d] = kind;
                if (kind == NovaShieldShell.SHATTER)
                {
                    P.dV[d] = N * (3.5f + Random.value * 3.5f) + Rnd3() * 1.5f + Vector3.up; P.dGrav[d] = 9; P.dLife[d] = 0.5f + Random.value * 0.4f; P.dSpin[d] = (Random.value - 0.5f) * 24;
                    col = Color.Lerp(col, Color.white * col.maxColorComponent, 0.4f) * 1.6f;
                }
                else if (kind == NovaShieldShell.SCATTER)
                {
                    P.dV[d] = N * (0.8f + Random.value) + Rnd3() * 0.4f; P.dGrav[d] = 0; P.dLife[d] = 0.2f + Random.value * 0.15f; P.dSpin[d] = (Random.value - 0.5f) * 12;
                }
                else
                {
                    P.dV[d] = N * (0.6f + Random.value * 0.7f) + P.dT[d] * ((Random.value - 0.5f) * 0.8f); P.dGrav[d] = 3; P.dLife[d] = 0.7f + Random.value * 0.4f; P.dSpin[d] = (Random.value - 0.5f) * 8;
                    col *= 1.3f;
                    var v = V; if (v != null) for (int j = 0; j < 3; j++) v.spark.Emit(P.dC[d], P.dT[d] * ((Random.value - 0.5f) * 5) + N, 0.035f, 0.15f + Random.value * 0.1f, Hdr(Color.Lerp(col, Color.white, 0.5f), 3));
                }
                P.dCol[d] = col;
            }
            s.detN = 0;
        }

        // The energy of a blocked hit streams from where it struck into his chest
        void Stream(Player p, Vector3 from, float n, bool perfect, Color col)
        {
            var c = PlayerSim.Chest(p); var to = S.W(c.x, c.y, 0.2);
            Color hot = Color.Lerp(col, Color.white, 0.5f);
            for (int i = 0; i < n; i++)
            {
                float life = 0.2f + S.Rnd() * 0.18f; var o = new Vector3((S.Rnd() - 0.5f) * 0.6f, (S.Rnd() - 0.5f) * 0.6f, (S.Rnd() - 0.5f) * 0.3f);
                var P = fx.Particle(from + o, perfect && S.Rnd() < 0.5f ? goldLin : S.Rnd() < 0.5f ? hot : col, 0.15f + S.Rnd() * 0.08f, life);
                P.v = (to - from - o) / life; P.drag = 1; P.grav = 0;
            }
        }

        public void OnEvent(Ev ev)
        {
            var F = fx; var p = ev.p; var v = V;
            switch (ev.type)
            {
                case "nshieldOn":
                {
                    var P = PaneOf(p); SnapAim(p, P); Anchor(p, P);
                    var (sx, sy) = SimFront(p, P, 0.8f); var col = ShellCol(P); P.shell.FlareAll(0.35f);
                    F.Sprite(sx, sy, "ring", col, 0.9f, 0.18f, 2.2f);
                    F.Burst(sx, sy, Color.Lerp(col, Color.white, 0.6f), 8, 4, 0.22f, 0.22f, dir: Mathf.Atan2(P.gy, P.gx), spread: 1.4f);
                    break;
                }
                case "nshieldBlock":
                {
                    var P = PaneOf(p); if (!P.wasUp) SnapAim(p, P); Anchor(p, P);
                    float d = (float)ev.dmg, k = (float)ev.k;
                    float a = AngleOf(p, P, ev.x, ev.y); var at = OnShell(P, a); var N = (at - P.O).normalized;
                    P.shell.Hit(a, ev.perfect ? 1.2f : ev.heavy ? 1.3f : 0.7f + Mathf.Min(0.5f, d * 0.04f), ev.perfect);
                    var rig = fx.RigOf(p); if (rig != null) rig.absorbFlash = Mathf.Min(1.5f, rig.absorbFlash + (ev.perfect ? 1.2f : 0.6f + d * 0.02f));
                    var col = ShellCol(P);
                    // the impact: a flash, sparks thrown off the shell, a light and a shimmer of heat
                    if (v != null)
                    {
                        v.flash.Emit(at, Vector3.zero, ev.heavy ? 1.6f : 1.0f, 0.08f, Hdr(Color.Lerp(col, Color.white, 0.6f), 4));
                        int ns = Count(8 + Mathf.Min(10, d * 0.6f) + (ev.heavy ? 8 : 0));
                        for (int i = 0; i < ns; i++) v.spark.Emit(at, N * (3 + Random.value * 4) + Rnd3() * 3, 0.04f + Random.value * 0.03f, 0.2f + Random.value * 0.25f, Hdr(Color.Lerp(col, Color.white, Random.value * 0.6f), 3 + Random.value * 2));
                        v.lights.Flash(at, Hdr(col, 1), ev.heavy ? 3.5f : 2.2f, 3.5f, 0.14f);
                        if (FxCfg.Distortion) v.haze.Emit(at, Vector3.zero, ev.heavy ? 1.6f : 1.1f, 0.3f, new Color(1, 1, 1, 0.6f));
                    }
                    else F.Burst(ev.x, ev.y, ev.heavy ? Color.white : Color.Lerp(col, Color.white, 0.5f), ev.heavy ? 16 : 9, ev.heavy ? 8 : 5, 0.28f, 0.26f, dir: Mathf.Atan2(P.gy, P.gx) + a, spread: 1.6f, grav: 5);
                    F.Sprite(ev.x, ev.y, "star", WHITE, (ev.heavy ? 1.2f : 0.75f) + k * 0.4f, 0.12f, 1.4f);
                    Stream(p, at, 8 + Mathf.Min(14, d * 0.9f) + (ev.perfect ? 8 : 0), ev.perfect, col);
                    if (ev.perfect)
                    {
                        var c0 = OnShell(P, 0); var (sx, sy) = SimFront(p, P, 0.7f);
                        F.Sprite(sx, sy, "star", goldLin * 1.6f, 2.0f, 0.18f, 1.5f); F.Sprite(sx, sy, "ring", goldLin, 1.4f, 0.3f, 3.2f);
                        if (v != null) { v.flash.Emit(c0, Vector3.zero, 2.4f, 0.12f, Hdr(goldLin, 5)); v.lights.Flash(c0, Hdr(goldLin, 1), 5, 5, 0.25f); }
                        else F.Burst(ev.x, ev.y, goldLin, 22, 9, 0.3f, 0.32f);
                        F.PopText(ev.x, ev.y + 0.6, "PERFECT", GOLD_PALE, 0.6f);
                    }
                    // Each quarter of the gauge he fills: a pulse out from him (and a word at the top)
                    int level = (int)ev.level;
                    if (level > P.level || ev.full || ev.max)
                    {
                        var c = PlayerSim.Chest(p); var lc = Color.Lerp(blueLin, goldLin, Mathf.Clamp01(level / 4f));
                        F.Sprite(c.x, c.y, "ring", Color.Lerp(lc, Color.white, 0.3f), 1.0f + level * 0.2f, 0.35f, 2.6f);
                        F.Sprite(c.x, c.y, "glow", lc, 1.6f + level * 0.4f, 0.3f, 1.4f);
                        if (ev.max)
                        {
                            P.shell.FlareAll(1);
                            F.PopText(c.x, c.y + 1.3, "MAX POWER", WHITE, 1.0f); F.Burst(c.x, c.y, goldLin, 30, 7, 0.3f, 0.5f);
                            if (v != null) v.lights.Flash(P.O, Hdr(goldLin, 1), 6, 6, 0.35f);
                        }
                        else if (ev.full) { P.shell.FlareAll(0.6f); F.PopText(c.x, c.y + 1.3, "FULL POWER", GOLD_PALE, 0.9f); }
                    }
                    P.level = level;
                    break;
                }
                case "nshieldBreak":
                {
                    var P = PaneOf(p); Anchor(p, P);
                    P.shell.Lower(true); TakeDetached(P); P.wasUp = false;
                    var c0 = OnShell(P, 0, NovaShieldShell.R * 0.7f);
                    if (v != null)
                    {
                        v.flash.Emit(c0, Vector3.zero, 3, 0.1f, Hdr(Color.white, 4)); v.lights.Flash(c0, Hdr(ShellCol(P), 1), 5, 5, 0.25f);
                        for (int i = 0; i < Count(24); i++) v.spark.Emit(c0, Rnd3() * 7 + P.F * 3, 0.05f, 0.3f + Random.value * 0.3f, Hdr(Color.Lerp(ShellCol(P), Color.white, 0.5f), 4));
                    }
                    var c = PlayerSim.Chest(p);
                    F.Sprite(c.x, c.y, "ring", WHITE, 1.4f, 0.35f, 3); F.Sprite(c.x, c.y, "star", WHITE, 1.8f, 0.16f, 1.5f);
                    F.PopText(c.x, c.y + 1.0, "BROKEN", "#ff8aa8", 0.8f);
                    break;
                }
                case "nshieldReady": { var c = PlayerSim.Chest(p); F.Sprite(c.x, c.y, "ring", BLUE, 1.0f, 0.3f, 2.4f); F.Burst(c.x, c.y, PALE, 10, 4, 0.22f, 0.3f); break; }
                case "absorbSpill":
                {
                    // Half the stored energy spills out of him as falling motes
                    var c = PlayerSim.Chest(p); var P = PaneOf(p); P.level = (int)System.Math.Floor(p.absorb / 25);
                    F.Burst(c.x, c.y, ShellCol(P), 16, 5, 0.25f, 0.5f, grav: 9); F.Burst(c.x, c.y, "#9aa6b8", 6, 3, 0.22f, 0.4f, grav: 6);
                    break;
                }
                case "parryStun":
                {
                    var e = ev.e; if (e == null) break;
                    double ex = e.x, ey = e.y + e.h + 0.3;
                    F.Sprite(ex, ey, "star", WHITE, 1.1f, 0.22f, 1.6f); F.Sprite(ex, ey, "ring", "#ffe36b", 0.8f, 0.3f, 2.4f);
                    F.Burst(ex, ey, "#ffe36b", 12, 4, 0.22f, 0.35f);
                    F.PopText(ex, ey + 0.4, "STUNNED", "#ffe36b", 0.7f);
                    break;
                }
            }
        }

        public void Update(float dt, World world, View view)
        {
            t += dt;
            seen.Clear();
            foreach (var p in world.players)
            {
                if (p.@char != "nova") continue;
                var rig = fx.RigOf(p); bool vis = rig != null && rig.root.visible && p.state != "dead";
                bool any = p.state == "nshield" || p.absorb > 0 || panes.ContainsKey(p);
                if (!any) continue;
                var P = PaneOf(p); seen.Add(p);
                bool up = vis && p.state == "nshield";
                if (up && !P.wasUp) SnapAim(p, P);
                else
                {
                    float q = 1 - Mathf.Exp(-dt * 28);
                    P.gx += ((float)p.guardDir.x - P.gx) * q; P.gy += ((float)p.guardDir.y - P.gy) * q;
                    float m = Mathf.Sqrt(P.gx * P.gx + P.gy * P.gy); if (m > 1e-4f) { P.gx /= m; P.gy /= m; } else { P.gx = (float)p.facing; P.gy = 0; }
                }
                Anchor(p, P);
                var sh = P.shell;
                if (up && !P.wasUp) sh.Raise();
                else if (!up && P.wasUp) sh.Lower(false);
                P.wasUp = up;
                P.k += ((up ? 1 : 0) - P.k) * (1 - Mathf.Exp(-dt * (up ? 30 : 14)));
                // toward the camera, in the shell's frame (for the fresnel rim)
                var camW = view != null ? S.FromUnity(view.camera.transform.position) : P.O + P.Z * 10;
                var toCam = (camW - P.O).normalized;
                float stab = Mathf.Max(0, (float)(p.nshieldStab / NOVA_SHIELD.stability));
                sh.Step(dt, (float)p.absorb, stab, up, Vector3.Dot(toCam, P.F), Vector3.Dot(toCam, P.U), Vector3.Dot(toCam, P.Z));
                TakeDetached(P);
                DrawShell(p, P, vis, dt);
                Flecks(P, up, dt);

                float g = rig != null ? rig.absorbGlow : Mathf.Clamp((float)p.absorb / 100, 0, 1.5f);
                var col = ShellCol(P);
                // The haze: a soft glow behind the shell
                if (vis && P.k > 0.02f)
                {
                    P.halo.position.copy(P.O + P.F * (NovaShieldShell.R * 0.55f) - P.Z * 0.1f);
                    P.halo.scale.setScalar(NovaShieldShell.R * 2.6f * (1 + 0.04f * Mathf.Sin(t * 5)));
                    P.haloM.colorLin = col; P.haloM.opacity = (0.08f + 0.16f * sh.fillK + 0.22f * sh.maxK + 0.25f * sh.perfectGlow) * P.k;
                    P.halo.visible = true;
                }
                else P.halo.visible = false;
                // The aura: a soft glow around him that grows and breathes with the energy he holds
                float flash = rig != null ? rig.absorbFlash : 0;
                if (vis && (g > 0.01f || flash > 0.01f))
                {
                    var c = PlayerSim.Chest(p); float breathe = 1 + 0.12f * Mathf.Sin(t * (2.2f + 2.5f * g));
                    P.aura.position.copy(S.W(c.x, c.y, 0.1f));
                    P.aura.scale.setScalar((1.6f + 1.4f * g) * breathe);
                    P.auraM.colorLin = col;
                    P.auraM.opacity = Mathf.Min(0.6f, 0.05f + 0.2f * g + 0.15f * flash) * breathe;
                    P.aura.visible = true;
                    // at higher levels, motes of light rise off him
                    if (S.Rnd() < g * g * 0.5f * dt * 60)
                    {
                        var q = fx.Particle(S.W(c.x + (S.Rnd() - 0.5f) * 0.8f, p.y + S.Rnd() * p.h, 0.2f), S.Rnd() < 0.5f ? Color.Lerp(col, Color.white, 0.4f) : col, 0.08f + S.Rnd() * 0.06f, 0.5f + S.Rnd() * 0.4f);
                        q.v = new Vector3(0, 0.8f + S.Rnd() * 0.8f, 0); q.drag = 1; q.grav = 0;
                    }
                }
                else P.aura.visible = false;
            }
            gone.Clear();
            foreach (var kv in panes)
                if (!seen.Contains(kv.Key))
                {
                    var P = kv.Value; P.mesh.visible = P.halo.visible = P.aura.visible = false; P.drawn = false;
                    if (!world.players.Contains(kv.Key)) gone.Add(kv.Key);
                }
            foreach (var p in gone) panes.Remove(p);
        }

        // One hex into the mesh: slot s, centred at C with its two tangents, a half-size and a turn
        static void Hex(DynMesh dm, int s, Vector3 C, Vector3 T, Vector3 B, float hs, float rot, Color col)
        {
            int v = s * 7; dm.Set(v, C); dm.col[v] = col;
            float cr = Mathf.Cos(rot), sr = Mathf.Sin(rot);
            for (int k = 0; k < 6; k++)
            {
                float x = NovaShieldShell.HX[k], y = NovaShieldShell.HY[k], rx = x * cr - y * sr, ry = x * sr + y * cr;
                dm.Set(v + 1 + k, C + (T * rx + B * ry) * hs); dm.col[v + 1 + k] = col;
            }
        }
        static void Hide(DynMesh dm, int s) { int v = s * 7; for (int k = 0; k < 7; k++) { dm.pos[v + k] = Vector3.zero; dm.col[v + k] = Color.clear; } }

        void DrawShell(Player p, Pane P, bool vis, float dt)
        {
            var sh = P.shell; var dm = P.mesh; int n = P.n; bool any = false;
            // on the ground, the tiles that reach the floor fade into it rather than cut through
            float floor = p.onGround ? (float)p.y : -1e6f;
            for (int i = 0; i < n; i++)
            {
                float a = vis ? sh.outA[i] : 0;
                if (a < 0.003f) { Hide(dm, i); continue; }
                var N = TileDir(P, i); var C = P.O + N * (NovaShieldShell.R + sh.lift[i]);
                a *= Mathf.Clamp01((C.y - floor) / 0.2f + 0.1f);
                if (a < 0.003f) { Hide(dm, i); continue; }
                var T = P.F * sh.ta[i] + P.U * sh.tb[i]; var B = P.F * sh.pa[i] + P.U * sh.pb[i] + P.Z * sh.pc[i];
                Hex(dm, i, C, T, B, NovaShieldShell.TILE * sh.cs[i] * NovaShieldShell.GAP * sh.size[i], 0, new Color(sh.outR[i], sh.outG[i], sh.outB[i], a));
                any = true;
            }
            for (int d = 0; d < n; d++)
            {
                int s = n + d;
                if (P.dAge[d] >= P.dLife[d]) { if (P.drawn) Hide(dm, s); continue; }
                P.dAge[d] += dt;
                if (P.dAge[d] >= P.dLife[d] || !vis) { P.dAge[d] = P.dLife[d]; Hide(dm, s); continue; }
                P.dV[d].y -= P.dGrav[d] * dt; P.dV[d] *= 1 - Mathf.Min(1, dt * (P.dKind[d] == NovaShieldShell.SHATTER ? 0.8f : 2.5f));
                P.dC[d] += P.dV[d] * dt;
                float k = P.dAge[d] / P.dLife[d], fade = (1 - k) * (1 - k);
                if (P.dKind[d] == NovaShieldShell.CRUMBLE && Random.value < 0.25f) fade *= 0.4f;   // (a dying tile sputters)
                var col = P.dCol[d]; col.a *= fade;
                Hex(dm, s, P.dC[d], P.dT[d], P.dP[d], P.dSize[d] * (1 - 0.5f * k), P.dSpin[d] * P.dAge[d], col);
                any = true;
            }
            if (any || P.drawn) dm.Upload();
            dm.visible = any; P.drawn = any;
        }

        // Electric flecks: sparks crawl off the shell's rim, more and hotter as it fills
        void Flecks(Pane P, bool up, float dt)
        {
            var v = V; var sh = P.shell; if (!up) return;
            P.fleck += dt * (2 + 8 * sh.fillK + 16 * sh.maxK) * FxCfg.Amount;
            int tries = 0;
            while (P.fleck >= 1 && tries++ < 8)
            {
                int i = Random.Range(0, P.n);
                if (sh.rim[i] < 0.4f || sh.outA[i] < 0.5f) continue;
                P.fleck -= 1;
                var N = TileDir(P, i); var C = P.O + N * NovaShieldShell.R; var T = P.F * sh.ta[i] + P.U * sh.tb[i];
                var col = TileCol(P, i); col = Color.Lerp(col / Mathf.Max(0.01f, col.maxColorComponent), Color.white, 0.35f);
                var vel = T * ((Random.value < 0.5f ? -1 : 1) * (2 + Random.value * 2)) + N * 0.6f;
                if (v != null)
                {
                    v.spark.Emit(C, vel, 0.035f, 0.12f + Random.value * 0.1f, Hdr(col, 4));
                    if (sh.maxK > 0.5f && Random.value < 0.4f) v.flash.Emit(C, Vector3.zero, 0.35f, 0.05f, Hdr(col, 3));
                }
                else { var q = fx.Particle(C, col, 0.07f, 0.15f); q.v = vel; q.drag = 1; q.grav = 0; }
            }
            if (P.fleck > 1) P.fleck = 1;
        }
    }
}
