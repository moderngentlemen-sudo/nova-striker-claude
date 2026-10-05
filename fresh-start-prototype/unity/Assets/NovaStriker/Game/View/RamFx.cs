// RAM's effects (ramfx.js): the Rampart's hard-light pane where the sim's shield stands (it flares when it blocks,
// cracks where each hit lands, the cracks spreading as its Integrity runs low and healing as it comes back, chips
// off shards as it weakens and shatters into them when it breaks; pushed along the floor it grinds out sparks);
// the Bulwark Wall; the Ram Charge's hard-light wedge, streaks and dust; the Guardian Link's tether; the Kinetic
// Release's cone of force; the Hydraulic Uplift's jets; Provoke's roar; Siege Breaker's ram's head; the sparks his
// charge throws up; and the craters his big impacts leave in floors and walls. Presentation only.
using System.Collections.Generic;
using NovaStriker.Game.Three;
using NovaStriker.Sim;
using UnityEngine;
using static NovaStriker.Sim.Cfg;

namespace NovaStriker.Game
{
    public sealed class RamFX
    {
        static string BLUE => CHARS["ram"].energy;
        const string PALE = "#cfe6ff", WHITE = "#ffffff";
        static readonly string[] SPARKS = { "#fff1c4", "#ffc24a", "#ff8a1f", "#ff6a00" };
        const float CR_LIFE = 9, CR_FADE = 2.5f, CR_GLOW = 1.2f; const int CR_POOL = 10;
        readonly Fx fx; readonly TObj scene; float t;
        readonly Texture2D hexT, crackedT; readonly Texture2D[] crackTex; readonly (Texture2D b, Texture2D g)[] craterTex;
        readonly TMat paneM, crackM, wallM, wallCrackM, wedgeM, edgeM;

        sealed class Crack { public TMesh m; public float o, u, w, size, rot, at = -1; }
        sealed class Pane { public TMesh face, crack, rim; public float flash, k, frac = 1; public readonly List<Crack> cracks = new List<Crack>(); }
        readonly Dictionary<Player, Pane> panes = new Dictionary<Player, Pane>();
        sealed class Wall { public TMesh face, crack, rimA, rimB, top, bas; public TMat faceM, crackM2, edgeM2; public float flash, born; public TMesh[] meshes; }
        readonly Dictionary<Barrier, Wall> walls = new Dictionary<Barrier, Wall>();
        readonly Dictionary<Player, TMesh> wedges = new Dictionary<Player, TMesh>();
        sealed class Link { public Strip glow, core; public readonly List<Vector3> pts = new List<Vector3>(); public float flash, fade; }
        readonly Dictionary<Player, Link> links = new Dictionary<Player, Link>();
        readonly Dictionary<Player, TObj> heads = new Dictionary<Player, TObj>();
        readonly Dictionary<Player, double> ghostTick = new Dictionary<Player, double>();
        sealed class Shard { public TMesh m; public float life, max = 1, size = 0.2f; public Vector3 v, spin; }
        readonly List<Shard> shards = new List<Shard>(); int si2;
        sealed class CraterMark { public TMesh b, g; public float life, age, heat = 1; }
        readonly List<CraterMark> craters = new List<CraterMark>();
        sealed class Spk { public float life, max, d; public double x, y, vx, vy; public Color c; }
        readonly Spk[] spk = new Spk[160]; int si;
        readonly DynMesh spkMesh;

        static Texture2D HexTex(bool cracks) => Fx.CanvasTex(256, (g, s) =>
        {
            var grad = g.createLinearGradient(0, 0, s, 0);
            grad.addColorStop(0, "rgba(255,255,255,0.95)"); grad.addColorStop(0.18f, "rgba(255,255,255,0.28)"); grad.addColorStop(0.82f, "rgba(255,255,255,0.28)"); grad.addColorStop(1, "rgba(255,255,255,0.95)");
            g.fillStyle = grad; g.fillRect(0, 0, s, s);
            g.strokeStyle = "rgba(255,255,255,0.75)"; g.lineWidth = 2;
            float r = s / 10f;
            for (int row = -1; row < 12; row++)
                for (int col = -1; col < 8; col++)
                {
                    float cx = col * r * 1.75f + ((row % 2) != 0 ? r * 0.87f : 0), cy = row * r * 1.5f;
                    g.beginPath(); for (int i = 0; i <= 6; i++) { float a = Mathf.PI / 3 * i + Mathf.PI / 6; g.lineTo(cx + Mathf.Cos(a) * r * 0.95f, cy + Mathf.Sin(a) * r * 0.95f); } g.stroke();
                }
            if (cracks)
            {
                g.strokeStyle = "rgba(255,255,255,1)"; g.lineWidth = 3;
                for (int k = 0; k < 7; k++)
                {
                    float x = s * (0.2f + Random.value * 0.6f), y = s * Random.value; g.beginPath(); g.moveTo(x, y);
                    for (int i = 0; i < 6; i++) { x += (Random.value - 0.5f) * s * 0.25f; y += (Random.value - 0.5f) * s * 0.3f; g.lineTo(x, y); }
                    g.stroke();
                }
            }
        }, repeat: true);

        // A crater: a scorched hollow with a broken rim and cracks running out of it, and the same cracks again as
        // a glow map (still hot with hard light for a moment after the impact)
        static Texture2D CraterTex(long seed, bool glow) => Fx.CanvasTex(256, (g, s) =>
        {
            long r = seed; float Rnd() { r = r * 16807 % 2147483647; return r / 2147483647f; }
            float c = s / 2f, R = s * 0.3f;
            var cracks = new List<List<Vector2>>();
            for (int k = 0; k < 9; k++)
            {
                float a = (k / 9f) * Mathf.PI * 2 + Rnd() * 0.5f, d = R * (0.5f + Rnd() * 0.3f); var pts = new List<Vector2> { new Vector2(c + Mathf.Cos(a) * d, c + Mathf.Sin(a) * d) };
                float len = R * (0.7f + Rnd() * 0.75f);
                for (int i = 0; i < 5; i++) { d += len / 5; a += (Rnd() - 0.5f) * 0.45f; pts.Add(new Vector2(c + Mathf.Cos(a) * d, c + Mathf.Sin(a) * d)); }
                cracks.Add(pts);
            }
            void CrackDraw(float w, string col)
            {
                g.strokeStyle = col; g.lineCap = "round";
                foreach (var pts in cracks)
                {
                    // (canvas strokes a whole path at the width set last; each segment here is drawn at the width
                    // the prototype's path ends with, as it does)
                    float lw = w; for (int i = 1; i < pts.Count; i++) lw *= 0.8f;
                    g.lineWidth = lw; g.beginPath(); g.moveTo(pts[0].x, pts[0].y); for (int i = 1; i < pts.Count; i++) g.lineTo(pts[i].x, pts[i].y); g.stroke();
                }
            }
            if (glow)
            {
                CrackDraw(6, "rgba(255,255,255,0.95)");
                var gg = g.createRadialGradient(c, c, 0, c, c, R); gg.addColorStop(0, "rgba(255,255,255,0.8)"); gg.addColorStop(1, "rgba(255,255,255,0)"); g.fillStyle = gg; g.fillRect(0, 0, s, s);
                return;
            }
            g.beginPath();
            for (int i = 0; i <= 40; i++) { float a = (i / 40f) * Mathf.PI * 2, rr = R * (1.08f + (Rnd() - 0.5f) * 0.2f); g.lineTo(c + Mathf.Cos(a) * rr, c + Mathf.Sin(a) * rr); }
            g.fillStyle = "rgba(120,128,140,0.55)"; g.fill();
            var hg = g.createRadialGradient(c, c, 0, c, c, R);
            hg.addColorStop(0, "rgba(8,10,14,0.95)"); hg.addColorStop(0.6f, "rgba(22,26,32,0.85)"); hg.addColorStop(1, "rgba(40,45,54,0.6)");
            g.beginPath();
            for (int i = 0; i <= 40; i++) { float a = (i / 40f) * Mathf.PI * 2, rr = R * (0.92f + (Rnd() - 0.5f) * 0.14f); g.lineTo(c + Mathf.Cos(a) * rr, c + Mathf.Sin(a) * rr); }
            g.fillStyle = hg; g.fill();
            CrackDraw(5, "rgba(10,12,16,0.85)");
            g.fillStyle = "rgba(30,34,40,0.7)";
            for (int i = 0; i < 26; i++) { float a = Rnd() * Mathf.PI * 2, d = R * (1.1f + Rnd() * 0.6f), z = 1.5f + Rnd() * 3; g.fillRect(c + Mathf.Cos(a) * d, c + Mathf.Sin(a) * d, z, z); }
        });

        // A crack in hard light: main fractures out from the point of impact, branches off them, a pale bruise
        static Texture2D CrackStarTex(long seed) => Fx.CanvasTex(256, (g, s) =>
        {
            long r = seed; float Rnd() { r = r * 16807 % 2147483647; return r / 2147483647f; }
            float c = s / 2f; g.lineCap = "round"; g.lineJoin = "round";
            void Walk(float x, float y, float a, int n, float step, float w, float alpha, bool branch)
            {
                string Col() => string.Format(System.Globalization.CultureInfo.InvariantCulture, "rgba(255,255,255,{0})", alpha);
                g.strokeStyle = Col(); g.lineWidth = w; g.beginPath(); g.moveTo(x, y);
                for (int i = 0; i < n; i++)
                {
                    a += (Rnd() - 0.5f) * 0.7f; x += Mathf.Cos(a) * step; y += Mathf.Sin(a) * step; g.lineTo(x, y);
                    if (branch && Rnd() < 0.35f)
                    {
                        g.stroke(); float da = (Rnd() < 0.5f ? -1 : 1) * (0.5f + Rnd() * 0.6f); int nn = 3 + (int)(Rnd() * 3);
                        Walk(x, y, a + da, nn, step * 0.7f, w * 0.55f, alpha * 0.8f, false);
                        g.strokeStyle = Col(); g.lineWidth = w; g.beginPath(); g.moveTo(x, y);
                    }
                }
                g.stroke();
            }
            int nm = 6 + (int)(Rnd() * 3);
            for (int k = 0; k < nm; k++) { float a0 = (float)k / nm * Mathf.PI * 2 + Rnd() * 0.6f; int nn = 6 + (int)(Rnd() * 4); Walk(c, c, a0, nn, s * 0.055f, 4, 1, true); }
            for (int k = 0; k < 10; k++) { float a = Rnd() * Mathf.PI * 2, d = s * (0.08f + Rnd() * 0.12f); Walk(c + Mathf.Cos(a) * d, c + Mathf.Sin(a) * d, a + Mathf.PI / 2, 2, s * 0.035f, 1.5f, 0.7f, false); }
            var gg = g.createRadialGradient(c, c, 0, c, c, s * 0.12f); gg.addColorStop(0, "rgba(255,255,255,0.9)"); gg.addColorStop(1, "rgba(255,255,255,0)");
            g.fillStyle = gg; g.fillRect(0, 0, s, s);
        });

        public RamFX(Fx fx)
        {
            this.fx = fx; scene = fx.scene;
            hexT = HexTex(false); crackedT = HexTex(true);
            TMat Plane(Texture map, string color, float op) => new TMat(TMat.Kind.Basic) { map = map, colorCss = color, transparent = true, opacity = op, blending = Blending.Additive, depthWrite = false, side = Side.Double, fog = false };
            paneM = Plane(hexT, BLUE, 0.6f); crackM = Plane(crackedT, PALE, 0); wallM = Plane(hexT, BLUE, 0.55f); wallCrackM = Plane(crackedT, PALE, 0);
            wedgeM = new TMat(TMat.Kind.Basic) { colorLin = S.Lin(BLUE) * 1.3f, transparent = true, opacity = 0.5f, blending = Blending.Additive, depthWrite = false };
            edgeM = new TMat(TMat.Kind.Basic) { colorLin = S.Lin(PALE) * 1.6f, transparent = true, opacity = 0.9f, blending = Blending.Additive, depthWrite = false };
            crackTex = new[] { CrackStarTex(11), CrackStarTex(23), CrackStarTex(37) };
            for (int i = 0; i < 48; i++)
            {
                var b = new GeoBuilder();
                var vs = new Vector3[3];
                for (int k = 0; k < 3; k++) { float a = (k / 3f) * Mathf.PI * 2 + (Random.value - 0.5f) * 1.2f, rr = 0.5f + Random.value * 0.5f; vs[k] = new Vector3(Mathf.Cos(a) * rr, Mathf.Sin(a) * rr, 0); }
                b.T(b.V(vs[0], Vector3.forward, new Vector2(0, 0)), b.V(vs[1], Vector3.forward, new Vector2(1, 0)), b.V(vs[2], Vector3.forward, new Vector2(0.5f, 1)));
                var m = new TMesh(b.ToMesh("shard"), new TMat(TMat.Kind.Basic) { colorCss = Random.value < 0.3f ? "#e4f2ff" : "#8fc4ff", transparent = true, opacity = 0, depthWrite = false, side = Side.Double });
                m.visible = false; m.RenderOrder = 6; scene.add(m);
                shards.Add(new Shard { m = m });
            }
            craterTex = new (Texture2D, Texture2D)[3];
            for (int k = 1; k <= 3; k++) craterTex[k - 1] = (CraterTex(k * 7919, false), CraterTex(k * 7919, true));
            for (int i = 0; i < CR_POOL; i++)
            {
                var g = Geo.Plane(1, 1);
                var b = new TMesh(g, new TMat(TMat.Kind.Basic) { transparent = true, opacity = 0, depthWrite = false, side = Side.Double });
                var gl = new TMesh(g, new TMat(TMat.Kind.Basic) { colorCss = BLUE, transparent = true, opacity = 0, blending = Blending.Additive, depthWrite = false, side = Side.Double, fog = false });
                b.visible = gl.visible = false; b.RenderOrder = 1; gl.RenderOrder = 2; scene.add(b); scene.add(gl);
                craters.Add(new CraterMark { b = b, g = gl });
            }
            // Sparks: thin streaks stretched along their flight (one mesh, ordinary blending); they fall, bounce off
            // the floor and cool from white-hot to red
            for (int i = 0; i < spk.Length; i++) spk[i] = new Spk();
            var tris = new int[spk.Length * 6];
            for (int i = 0; i < spk.Length; i++) { int a = i * 4; tris[i * 6] = a; tris[i * 6 + 1] = a + 1; tris[i * 6 + 2] = a + 2; tris[i * 6 + 3] = a; tris[i * 6 + 4] = a + 2; tris[i * 6 + 5] = a + 3; }
            spkMesh = new DynMesh(spk.Length * 4, tris, new TMat(TMat.Kind.Basic) { vertexColors = true, transparent = true, depthWrite = false, side = Side.Double, fog = false }, true, false, "sparks");
            spkMesh.obj.RenderOrder = 6; scene.add(spkMesh.obj);
        }

        // A quad's orientation along a sim-plane direction: `len` along (tx, ty) once scaled, facing the camera side
        static void PlaceQ(TObj mesh, double x, double y, double tx, double ty, double depth = 0)
        {
            var T = S.Dir(x, tx, ty).normalized; var f = Level.Frame(x); var Z = new Vector3((float)f.nx, 0, (float)f.nz).normalized;
            var X = Vector3.Cross(T, Z).normalized;
            mesh.SetQuaternion(ThQ.FromBasis(X, T, Z));
            mesh.position.copy(S.W(x, y, depth));
        }
        Pane PaneOf(Player p)
        {
            if (panes.TryGetValue(p, out var P)) return P;
            var g = Geo.Plane(1, 1);
            P = new Pane { face = new TMesh(g, paneM), crack = new TMesh(g, crackM), rim = new TMesh(g, edgeM) };
            foreach (var m in new[] { P.face, P.crack, P.rim }) { m.visible = false; m.RenderOrder = 5; scene.add(m); }
            panes[p] = P; return P;
        }
        static (double, double) GuardDir(Player p) => (p.guardDir.x, p.guardDir.y);

        public void OnEvent(Ev ev)
        {
            var F = fx; var p = ev.p;
            switch (ev.type)
            {
                case "guardOn": { var c = PlayerSim.Chest(p); var (nx, ny) = GuardDir(p); F.Sprite(c.x + nx * 0.8, c.y + ny * 0.8, "ring", BLUE, 1.0f, 0.18f, 2.2f); break; }
                case "guardBlock":
                {
                    var P = PaneOf(p); P.flash = Mathf.Min(1, P.flash + 0.5f + (float)ev.dmg * 0.03f);
                    CrackAt(p, P, ev.x, ev.y, (float)ev.dmg, (float)ev.frac);
                    if ((P.frac > 2f / 3 && ev.frac <= 2.0 / 3) || (P.frac > 1f / 3 && ev.frac <= 1.0 / 3)) Shatter(p, ev.x, ev.y, 7, 0.5f);
                    P.frac = (float)ev.frac;
                    if (p.onGround) Sparks(p.x + p.facing * (p.w / 2 + 0.3), p.y + 0.06, -(float)p.facing, 3 + Mathf.Min(8, (int)(ev.dmg / 2)), 0.9f);
                    var (nx, ny) = GuardDir(p);
                    F.Burst(ev.x, ev.y, ev.heavy ? WHITE : PALE, ev.heavy ? 18 : 10, ev.heavy ? 9 : 6, 0.3f, 0.28f, dir: Mathf.Atan2((float)ny, (float)nx), spread: 1.6f, grav: 6);
                    F.Sprite(ev.x, ev.y, "star", WHITE, ev.heavy ? 1.3f : 0.8f, 0.12f, 1.4f);
                    break;
                }
                case "perfectGuard":
                {
                    var P = PaneOf(p); P.flash = 1.4f;
                    F.Sprite(ev.x, ev.y, "star", WHITE, 2.0f, 0.18f, 1.5f); F.Sprite(ev.x, ev.y, "ring", BLUE, 1.2f, 0.3f, 3.2f);
                    F.Burst(ev.x, ev.y, BLUE, 24, 10, 0.32f, 0.35f); F.PopText(ev.x, ev.y + 0.6, "PERFECT", PALE, 0.6f);
                    break;
                }
                case "rampartBreak":
                {
                    var c = PlayerSim.Chest(p); var (nx, ny) = GuardDir(p); var P = PaneOf(p);
                    Shatter(p, null, null, 30, 1); P.frac = 0;
                    foreach (var k in P.cracks) k.at = -1;
                    for (int i = 0; i < 28; i++)
                    {
                        double u = (S.Rnd() - 0.5f) * 2.6f, x = c.x + nx * 0.8 - ny * u, y = c.y + ny * 0.8 + nx * u;
                        F.Burst(x, y, S.Rnd() < 0.5f ? PALE : BLUE, 1, 7, 0.3f, 0.5f, dir: Mathf.Atan2((float)ny, (float)nx) + (S.Rnd() - 0.5f) * 2, spread: 0.5f, grav: 12);
                    }
                    F.Sprite(c.x + nx * 0.8, c.y + ny * 0.8, "ring", WHITE, 1.6f, 0.35f, 3); F.PopText(c.x, c.y + 1.2, "BROKEN", "#ff8aa8", 0.8f);
                    break;
                }
                case "rampartReady": { PaneOf(p).frac = 1; var c = PlayerSim.Chest(p); F.Sprite(c.x, c.y, "ring", BLUE, 1.2f, 0.3f, 2.4f); F.Burst(c.x, c.y, PALE, 12, 4, 0.25f, 0.3f); break; }
                case "kineticRelease":
                {
                    float k = (float)ev.k, a0 = Mathf.Atan2((float)ev.ny, (float)ev.nx); var at = S.W(ev.x, ev.y, 0.3); var dir = S.Dir(ev.x, ev.nx, ev.ny).normalized;
                    F.charge.ShockRing(at, dir, WHITE, 0.4f, 1.4f + 2.2f * k, 0.3f, (float)ev.r * 0.6f);
                    F.charge.ShockRing(at, dir, BLUE, 0.3f, 1.0f + 1.8f * k, 0.38f, (float)ev.r * 0.8f);
                    F.Sprite(ev.x, ev.y, "star", WHITE, 1.6f + 2 * k, 0.2f, 1.5f); F.Sprite(ev.x, ev.y, "glow", BLUE, 2 + 3 * k, 0.3f, 1.6f);
                    for (int i = 0; i < 30 + 50 * k; i++)
                    {
                        float a = a0 + (S.Rnd() - 0.5f) * (float)ev.cone * 2, d = S.Rnd() * (float)ev.r;
                        F.Burst(ev.x + Mathf.Cos(a) * d * 0.3f, ev.y + Mathf.Sin(a) * d * 0.3f, S.Rnd() < 0.4f ? WHITE : BLUE, 1, 10 + 12 * k, 0.32f, 0.32f, dir: a, spread: 0.15f);
                    }
                    F.Dust(ev.x, ev.y - 1.2, 0.4f + 0.6f * k, new[] { ev.nx >= 0 ? 0 : Mathf.PI }, reach: 2);
                    break;
                }
                case "rush":
                {
                    float L = (float)ev.level; double x = p.x - ev.dx * 0.6;
                    F.Dust(p.x, p.y, 0.4f + 0.2f * L, new[] { ev.dx > 0 ? Mathf.PI : 0 }, noRing: L < 2);
                    F.Smoke(x, p.y + 0.3, "#8e97a3", 4 + 3 * L, 3 + L, 0.5f, 0.5f, dir: ev.dx > 0 ? Mathf.PI : 0, spread: 0.8f, op: 0.45f);
                    if (L != 0) { F.Sprite(p.x, p.y + 1.2, "ring", BLUE, 0.8f + 0.3f * L, 0.2f, 2.4f); F.Burst(p.x, p.y + 1.2, BLUE, 12 + 8 * L, 8 + 3 * L, 0.3f, 0.3f, dir: ev.dx > 0 ? Mathf.PI : 0, spread: 1.1f); }
                    if (p.onGround) Sparks(p.x + ev.dx * (p.w / 2 + 0.2), p.y + 0.08, (float)ev.dx, 10 + 6 * L, 1.2f);
                    for (int i = 0; i < 6; i++) F.Smoke(p.x - p.facing * 0.55, p.y + 2.25, "#6f7883", 1, 2, 0.4f, 0.6f, dir: Mathf.PI / 2 + (ev.dx > 0 ? 0.6f : -0.6f), spread: 0.5f, op: 0.45f, grav: -1.2f);
                    break;
                }
                case "plowCatch": { var e = ev.e; F.Sprite(e.x, e.y + e.h * 0.55, "star", WHITE, 1.0f, 0.12f, 1.4f); F.Burst(e.x, e.y + e.h * 0.55, PALE, 12, 7, 0.28f, 0.25f); break; }
                case "ramSplat":
                {
                    float k = Mathf.Min(1, 0.4f + 0.2f * (float)ev.n + 0.1f * (float)ev.level);
                    F.Sprite(ev.x, ev.y, "star", WHITE, 2.4f + k, 0.2f, 1.5f); F.Sprite(ev.x, ev.y, "ring", BLUE, 1.6f + k, 0.35f, 3.2f); F.Fireball(ev.x, ev.y, "#9fd0ff", 1.2f + k, 0.25f);
                    F.Burst(ev.x, ev.y, "#5d6674", 20, 9, 0.32f, 0.6f, dir: Mathf.PI / 2, spread: 2.4f, grav: 18); F.Burst(ev.x, ev.y, BLUE, 26, 11, 0.32f, 0.35f);
                    F.Dust(ev.x, p.y, 0.9f, null, reach: 1.5f); F.Smoke(ev.x, ev.y, "#8e97a3", 8, 1.8f, 0.7f, 0.9f, op: 0.45f, grow: 2.4f, grav: -0.6f);
                    F.PopText(ev.x, ev.y + 1.1, "SLAM", PALE, 0.65f);
                    double sd = System.Math.Sign(ev.x - p.x); if (sd == 0) sd = p.facing;
                    WallCrater(ev.x, ev.y, sd, 0.75f + 0.12f * Mathf.Min(4, (float)ev.n) + 0.1f * (float)ev.level);
                    break;
                }
                case "ramBonk": F.Sprite(ev.x, ev.y, "star", WHITE, 1.6f, 0.14f, 1.5f); F.Sprite(ev.x, ev.y, "ring", PALE, 1.0f, 0.25f, 2.6f); F.Burst(ev.x, ev.y, PALE, 18, 8, 0.28f, 0.3f, grav: 8); break;
                case "wallUp":
                {
                    double x = ev.x, y = ev.y;
                    F.GroundRing(x, y, BLUE, 0.3f, 2.2f, 0.35f, 0.9f); F.Dust(x, y, 0.5f, null, noRing: true);
                    for (int i = 0; i < 20; i++) F.Burst(x, y + S.Rnd() * RAM.wall.half * 2, S.Rnd() < 0.5f ? PALE : BLUE, 1, 4, 0.26f, 0.4f, dir: Mathf.PI / 2, spread: 0.6f);
                    F.Sprite(x, y + 0.4, "star", WHITE, 1.4f, 0.16f, 1.4f);
                    break;
                }
                case "wallHit": { F.Burst(ev.x, ev.y, PALE, 8, 5, 0.24f, 0.22f); if (ev.barrier != null && walls.TryGetValue(ev.barrier, out var W)) W.flash = 1; break; }
                case "wallDown":
                    if (ev.broken) for (int i = 0; i < 26; i++) F.Burst(ev.x, ev.y + S.Rnd() * RAM.wall.half * 2, S.Rnd() < 0.5f ? PALE : BLUE, 1, 7, 0.3f, 0.5f, grav: 10);
                    else F.Burst(ev.x, ev.y + 1.5, BLUE, 14, 2, 0.3f, 0.6f, dir: Mathf.PI / 2, spread: 0.6f);
                    break;
                case "link": { var c = PlayerSim.Chest(ev.q); F.Sprite(c.x, c.y, "ring", BLUE, 1.3f, 0.35f, 2.6f); F.Burst(c.x, c.y, PALE, 18, 4, 0.26f, 0.4f); break; }
                case "linkHit": { if (links.TryGetValue(p, out var L)) L.flash = 1; var c = PlayerSim.Chest(p); F.Burst(c.x, c.y, PALE, 8, 4, 0.22f, 0.25f); break; }
                case "linkEnd": { if (links.TryGetValue(p, out var L)) L.fade = 0.25f; break; }
                case "leap": F.Dust(p.x, p.y, 0.6f); F.Burst(p.x, p.y + 0.3, BLUE, 16, 6, 0.3f, 0.3f, dir: -Mathf.PI / 2, spread: 1.6f); break;
                case "leapLand":
                    F.GroundRing(ev.x, ev.y, WHITE, 0.4f, 3.2f, 0.32f, 0.8f); F.GroundRing(ev.x, ev.y, BLUE, 0.3f, 2.4f, 0.3f, 0.9f);
                    F.Dust(ev.x, ev.y, 0.9f); F.Burst(ev.x, ev.y + 0.3, "#5d6674", 14, 8, 0.3f, 0.55f, dir: Mathf.PI / 2, spread: 2.2f, grav: 18);
                    F.Sprite(ev.x, ev.y + 0.5, "star", WHITE, 1.6f, 0.16f, 1.5f);
                    Crater(ev.x, ev.y, 1.0f);
                    break;
                case "provoke":
                    foreach (var (sz, life, col) in new[] { (1.4f, 0.35f, WHITE), (2.4f, 0.5f, BLUE), (3.4f, 0.65f, PALE) }) F.Sprite(ev.x, ev.y, "ring", col, sz, life, 3.6f);
                    F.Burst(ev.x, ev.y, BLUE, 34, 9, 0.34f, 0.45f); F.Dust(p.x, p.y, 0.6f);
                    F.PopText(ev.x, ev.y + 1.6, "PROVOKE", PALE, 0.7f);
                    break;
                case "quake": F.GroundRing(ev.x, ev.y, BLUE, 0.4f, 2.6f, 0.32f, 0.9f); F.Dust(p.x, p.y, 1.0f, null, reach: 1.2f); F.Sprite(ev.x, ev.y + 0.3, "star", WHITE, 1.8f, 0.16f, 1.5f); Crater(ev.x, ev.y, 1.15f); break;
                case "upliftBlast":
                    F.Sprite(ev.x, ev.y, "star", WHITE, 1.8f, 0.16f, 1.5f); F.Sprite(ev.x, ev.y, "ring", BLUE, (float)ev.r * 0.9f, 0.3f, 2.8f);
                    F.Burst(ev.x, ev.y, BLUE, 26, 9, 0.3f, 0.32f); F.Burst(ev.x, ev.y, PALE, 10, 6, 0.24f, 0.25f, dir: Mathf.PI / 2, spread: 1.4f);
                    break;
                case "fortify": foreach (var q in ev.members) { var c = PlayerSim.Chest(q); F.Sprite(c.x, c.y, "ring", BLUE, 1.6f, 0.4f, 2.2f); F.Burst(c.x, c.y, PALE, 16, 4, 0.26f, 0.4f); } break;
                case "ramSlam":
                {
                    double x = ev.x, y = ev.y; float r = (float)ev.r;
                    F.Sprite(x, y, "star", WHITE, r * 1.4f, 0.3f, 1.6f); F.Sprite(x, y, "glow", PALE, r * 1.6f, 0.4f, 1.5f); F.Fireball(x, y, "#9fd0ff", r * 0.8f, 0.45f);
                    foreach (var (sz, life, g) in new[] { (r * 0.45f, 0.4f, 4.4f), (r * 0.3f, 0.6f, 6f), (r * 0.18f, 0.75f, 8f) }) F.Sprite(x, y, "ring", WHITE, sz, life, g);
                    F.Burst(x, y, BLUE, 80, 18, 0.42f, 0.75f, grav: 5); F.Burst(x, y, "#5d6674", 30, 12, 0.34f, 0.8f, dir: Mathf.PI / 2, spread: 2.4f, grav: 18);
                    var fl = F.FloorUnder(x, y, 3);
                    if (fl != null) { F.GroundRing(x, fl.Value, WHITE, 0.5f, r * 1.3f, 0.55f, 0.95f); F.GroundRing(x, fl.Value, BLUE, 0.4f, r, 0.5f, 0.9f); F.Dust(x, fl.Value, 1.4f, null, reach: 3); }
                    F.Smoke(x, y, "#8e97a3", 14, 2.5f, 1.2f, 1.3f, op: 0.45f, grow: 2.6f, grav: -0.8f);
                    Crater(x, y, Mathf.Min(3, r * 0.5f), 3);
                    break;
                }
            }
        }

        public void Update(float dt, World world, View view)
        {
            t += dt;
            UpdateCraters(dt); UpdateSparks(dt); UpdateShards(dt);
            var F = fx; var cam = view.camPos; var seenP = new HashSet<Player>(); var seenW = new HashSet<Barrier>(); var seenL = new HashSet<Player>();
            foreach (var p in world.players)
            {
                if (p.@char != "ram") continue;
                var rig = fx.RigOf(p); bool vis = rig != null && rig.root.visible && p.state != "dead";
                var P = PaneOf(p); seenP.Add(p);
                bool up = vis && p.state == "guard";
                P.k += ((up ? 1 : 0) - P.k) * (1 - Mathf.Exp(-dt * (up ? 30 : 14)));
                P.flash = Mathf.Max(0, P.flash - dt * 5);
                if (P.k > 0.02f)
                {
                    var c = PlayerSim.Chest(p); var (nx, ny) = GuardDir(p); double cx = c.x + nx * RAM.guard.reach, cy = c.y + ny * RAM.guard.reach;
                    float frac = Mathf.Max(0, (float)(p.integrity / RAM.guard.integrity)), weak = frac < 0.35f ? (Mathf.Sin(t * 40) > 0 ? 1 : 0.55f) : 1;
                    float len = (float)RAM.guard.half * 2 * (0.6f + 0.4f * P.k);
                    float dz = rig != null ? rig.extra.shield.position.z * (float)CHARS["ram"].scale + 0.16f : 0.3f;
                    PlaceQ(P.face, cx, cy, -ny, nx, dz); P.face.scale.set(1.1f, len, 1);
                    PlaceQ(P.rim, cx + nx * 0.55, cy + ny * 0.55, -ny, nx, dz + 0.02f); P.rim.scale.set(0.05f, len, 1);
                    PlaceQ(P.crack, cx, cy, -ny, nx, dz + 0.01f); P.crack.scale.set(1.1f, len, 1);
                    paneM.opacity = (0.32f + 0.3f * frac + 0.6f * P.flash) * P.k * weak;
                    crackM.opacity = Mathf.Max(0, 0.75f - frac) * P.k * weak;
                    edgeM.opacity = (0.55f + 0.45f * P.flash) * P.k;
                    paneM.offset = new Vector2(0, t * 0.25f);
                    P.face.visible = P.rim.visible = P.crack.visible = true;
                    foreach (var k in P.cracks)
                    {
                        bool on = p.integrity < k.at;
                        k.o += ((on ? 1 : 0) - k.o) * (1 - Mathf.Exp(-dt * (on ? 30 : 3)));
                        k.m.visible = k.o > 0.02f;
                        if (!k.m.visible) continue;
                        float u = Mathf.Clamp(k.u, -len / 2, len / 2);
                        PlaceQ(k.m, cx - ny * u + nx * k.w, cy + nx * u + ny * k.w, -ny, nx, dz + 0.025f);
                        k.m.RotateZ(k.rot); k.m.scale.setScalar(k.size * (1 + 0.6f * (1 - frac)));
                        k.m.material.opacity = k.o * P.k * weak * (0.6f + 0.4f * (1 - frac)) * (1 + P.flash);
                    }
                    if (p.onGround && System.Math.Abs(p.vx) > 0.4 && S.Rnd() < Mathf.Min(1, Mathf.Abs((float)p.vx) / 2.2f))
                    {
                        int bot = ny * nx >= 0 ? -1 : 1; double ex = cx - ny * bot * len / 2;
                        Sparks(System.Math.Abs(ny) > 0.5 ? ex : p.x + p.facing * (p.w / 2 + 0.3), p.y + 0.05, System.Math.Sign(p.vx), 1 + (System.Math.Abs(p.vx) > 1.5 ? 1 : 0), 0.75f);
                    }
                    if (p.kinetic > 10 && S.Rnd() < (float)p.kinetic / 120)
                    {
                        float u = (S.Rnd() - 0.5f) * len; var w = S.W(cx - ny * u, cy + nx * u, dz + 0.04f);
                        var pt = F.Particle(w, S.Rnd() < 0.5f ? WHITE : BLUE, 0.16f, 0.4f); pt.v = new Vector3(0, 1.2f, 0); pt.drag = 1;
                    }
                }
                else { P.face.visible = P.rim.visible = P.crack.visible = false; foreach (var k in P.cracks) k.m.visible = false; }
                // ---- The Ram Charge: a hard-light wedge in front, streaks and dust; afterimages ----
                bool rushing = vis && p.state == "rush" && p.rush != null, ult = vis && p.state == "ult" && p.ultRun != null && p.ultRun.kind == "ram";
                if (!wedges.TryGetValue(p, out var Wd)) { Wd = new TMesh(Geo.Cylinder(0, 0.9f, 1.4f, 4, 1, true), wedgeM); Wd.visible = false; Wd.RenderOrder = 5; scene.add(Wd); wedges[p] = Wd; }
                if (rushing || (ult && p.ultRun.t > ULT.ram.brace && p.ultRun.slamT == 0))
                {
                    float L = rushing ? (float)p.rush.level : 3; double dir = rushing ? p.rush.dx : p.ultRun.dx; float s = (0.8f + 0.18f * L) * (ult ? 1.6f : 1);
                    PlaceQ(Wd, p.x + dir * (p.w / 2 + 0.55 * s), p.y + 1.2, dir, 0, 0.3);
                    Wd.scale.set(s * (1 + 0.08f * Mathf.Sin(t * 40)), s, s * 0.5f); Wd.visible = true;
                    wedgeM.opacity = 0.32f + 0.08f * L;
                    for (int i = 0; i < 2 + L; i++) F.Burst(p.x + dir * (p.w / 2 + 0.4), p.y + 0.3 + S.Rnd() * p.h, S.Rnd() < 0.5f ? WHITE : BLUE, 1, 14 + 4 * L, 0.2f, 0.14f, dir: dir > 0 ? Mathf.PI : 0, spread: 0.12f);
                    if (p.onGround && S.Rnd() < 0.7f) F.Dust(p.x - dir * 0.3, p.y, 0.3f + 0.08f * L, new[] { dir > 0 ? Mathf.PI : 0 }, noRing: true, op: 0.45f);
                    if (p.onGround) { Sparks(p.x + dir * (p.w / 2 + 0.25), p.y + 0.06, (float)dir, 3 + L, 1); if (S.Rnd() < 0.6f) Sparks(p.x - dir * 0.15, p.y + 0.04, (float)dir, 1, 0.7f); }
                    else if (S.Rnd() < 0.6f) F.Burst(p.x + dir * (p.w / 2 + 0.3), p.y + 0.4 + S.Rnd() * 1.6f, S.Rnd() < 0.5f ? WHITE : PALE, 2, 6, 0.14f, 0.18f, dir: dir > 0 ? Mathf.PI : 0, spread: 1.6f, grav: 6);
                    double last = ghostTick.TryGetValue(p, out var l) ? l : -99;
                    if (rig != null && world.tick - last >= (L >= 2 ? 3 : 4)) { ghostTick[p] = world.tick; F.ghosts.Spawn(rig, S.Lin(BLUE) * (1.2f + 0.25f * L), 0.22f + 0.06f * L, 0.18f); }
                }
                else Wd.visible = false;
                if (vis && p.state == "attack" && p.moveId == "ram_rise" && p.st >= p.move.su && p.st < p.move.su + p.move.ac)
                {
                    foreach (var dz in new[] { -0.2f, 0.2f })
                    {
                        var w = S.W(p.x + (S.Rnd() - 0.5f) * 0.3f, p.y - 0.05, dz); var pt = F.Particle(w, S.Rnd() < 0.5f ? WHITE : BLUE, 0.3f, 0.16f);
                        pt.v = new Vector3((S.Rnd() - 0.5f) * 0.8f, -6 - S.Rnd() * 3, 0); pt.drag = 0.86f;
                    }
                    if (S.Rnd() < 0.5f) F.Smoke(p.x, p.y - 0.1, "#8e97a3", 1, 1, 0.5f, 0.6f, dir: -Mathf.PI / 2, spread: 1.2f, op: 0.4f);
                }
                if (vis && p.braceT > 0 && Mathf.Floor(t * 3) != Mathf.Floor((t - dt) * 3)) F.GroundRing(p.x, p.y, BLUE, 0.6f, 1.6f, 0.3f, 0.6f);
                if (vis && p.state == "leap" && rig != null)
                {
                    double last = ghostTick.TryGetValue(p, out var l) ? l : -99;
                    if (world.tick - last >= 3) { ghostTick[p] = world.tick; F.ghosts.Spawn(rig, S.Lin(BLUE) * 1.3f, 0.25f, 0.2f); }
                }
                // ---- Guardian Link: a tether of light to the teammate he guards ----
                if (p.link != null && vis)
                {
                    seenL.Add(p);
                    if (!links.TryGetValue(p, out var L)) { L = new Link { glow = new Strip(scene, 4), core = new Strip(scene, 5) }; links[p] = L; }
                    L.fade = 0; L.flash = Mathf.Max(0, L.flash - dt * 4);
                    var a = PlayerSim.Chest(p); var q = p.link.q; var b = PlayerSim.Chest(q); const int n = 16; var pts = L.pts; pts.Clear();
                    for (int i = 0; i <= n; i++)
                    {
                        double u = (double)i / n, sag = System.Math.Sin(u * System.Math.PI) * 0.6;
                        pts.Add(S.W(a.x + (b.x - a.x) * u, a.y + (b.y - a.y) * u - sag + Mathf.Sin(t * 7 + (float)u * 9) * 0.05, 0.25));
                    }
                    float k = Mathf.Min(1, (float)p.link.t / 40), fl = L.flash;
                    L.glow.Build(pts, cam, i => 0.12f + 0.08f * fl, i => new Vector4(0.35f, 0.65f, 1.0f, (0.22f + 0.4f * fl) * k));
                    int tick14 = Mathf.FloorToInt(t * 14);
                    L.core.Build(pts, cam, i => 0.035f, i => { bool on = ((i + tick14) % 3) != 0; return new Vector4(0.8f, 0.92f, 1.0f, (on ? 0.9f : 0.25f) * k); });
                    if (S.Rnd() < 0.15f) F.Sprite(b.x, b.y + 0.9, "ring", BLUE, 0.55f, 0.3f, 1.6f);
                }
            }
            foreach (var kv in new List<KeyValuePair<Player, Link>>(links))
            {
                if (seenL.Contains(kv.Key)) continue;
                var L = kv.Value;
                if (L.fade > 0) { L.fade -= dt; continue; }
                L.glow.mesh.visible = false; L.core.mesh.visible = false;
                if (!world.players.Contains(kv.Key)) links.Remove(kv.Key);
            }
            foreach (var kv in new List<KeyValuePair<Player, Pane>>(panes)) if (!seenP.Contains(kv.Key)) { var P = kv.Value; P.face.visible = P.rim.visible = P.crack.visible = false; if (!world.players.Contains(kv.Key)) panes.Remove(kv.Key); }
            foreach (var kv in new List<KeyValuePair<Player, TMesh>>(wedges)) if (!world.players.Contains(kv.Key) || kv.Key.@char != "ram") { kv.Value.destroy(); wedges.Remove(kv.Key); }
            // ---- Bulwark Walls ----
            foreach (var b in world.barriers)
            {
                if (b.kind != "rampart") continue;
                seenW.Add(b);
                if (!walls.TryGetValue(b, out var W))
                {
                    var g = Geo.Plane(1, 1); TMat face = wallM.clone(), crack = wallCrackM.clone(), edge = edgeM.clone();
                    W = new Wall { face = new TMesh(g, face), crack = new TMesh(g, crack), rimA = new TMesh(g, edge), rimB = new TMesh(g, edge), top = new TMesh(g, edge), bas = new TMesh(g, edge), faceM = face, crackM2 = crack, edgeM2 = edge, born = t };
                    W.meshes = new[] { W.face, W.crack, W.rimA, W.rimB, W.top, W.bas };
                    foreach (var m in W.meshes) { m.RenderOrder = 5; scene.add(m); }
                    walls[b] = W;
                }
                W.flash = Mathf.Max(0, W.flash - dt * 5);
                float grow = Mathf.Min(1, (t - W.born) / 0.18f), H = (float)b.half * 2 * grow; double cy = b.y - b.half + H / 2;
                float frac = Mathf.Max(0, (float)(b.hp / b.maxHp)), endK = Mathf.Min(1, (float)b.ttl / 30);
                const float D = 0.36f; float blink = b.ttl < 60 && b.ttl % 10 < 5 ? 0.6f : 1;
                PlaceQ(W.face, b.x, cy, 0, 1, 0.1); W.face.scale.set(D * 2, H, 1);
                PlaceQ(W.crack, b.x, cy, 0, 1, 0.11); W.crack.scale.set(D * 2, H, 1);
                PlaceQ(W.rimA, b.x - D, cy, 0, 1, 0.12); W.rimA.scale.set(0.05f, H, 1);
                PlaceQ(W.rimB, b.x + D, cy, 0, 1, 0.12); W.rimB.scale.set(0.05f, H, 1);
                PlaceQ(W.top, b.x, b.y - b.half + H, 1, 0, 0.12); W.top.scale.set(0.05f, D * 2 + 0.05f, 1);
                PlaceQ(W.bas, b.x, b.y - b.half + 0.04, 1, 0, 0.12); W.bas.scale.set(0.1f, D * 2 + 0.6f, 1);
                W.faceM.opacity = (0.45f + 0.35f * frac + 0.5f * W.flash) * endK * blink;
                W.crackM2.opacity = Mathf.Max(0, 0.75f - frac) * endK;
                W.edgeM2.opacity = (0.75f + 0.25f * W.flash) * endK * blink;
                W.faceM.offset = new Vector2(0, t * 0.2f);
                if (S.Rnd() < 0.3f) F.Burst(b.x + (S.Rnd() - 0.5f) * D * 2, b.y - b.half + S.Rnd() * H, PALE, 1, 0.8f, 0.16f, 0.4f, dir: Mathf.PI / 2, spread: 0.3f);
            }
            foreach (var kv in new List<KeyValuePair<Barrier, Wall>>(walls)) if (!seenW.Contains(kv.Key)) { foreach (var m in kv.Value.meshes) m.destroy(); walls.Remove(kv.Key); }
            // ---- Siege Breaker's ram's head: horns of hard light over the wedge ----
            foreach (var p in world.players)
            {
                var R = p.state == "ult" && p.ultRun != null && p.ultRun.kind == "ram" ? p.ultRun : null;
                heads.TryGetValue(p, out var Hd);
                if (R == null) { if (Hd != null) Hd.visible = false; continue; }
                if (Hd == null)
                {
                    Hd = Group.Make();
                    foreach (var sx in new[] { 1f, -1f }) { var h = new TMesh(Geo.Torus(0.75f, 0.16f, 8, 24, Mathf.PI * 1.5f), wedgeM); h.position.set(-0.2f, 0.55f, sx * 0.25f); h.rotation.z = 1.0f; Hd.add(h); }
                    var brow = new TMesh(Geo.Box(0.5f, 1.6f, 0.9f), wedgeM); brow.position.set(0.3f, 0, 0); Hd.add(brow);
                    var eyes = new TMesh(Geo.Box(0.1f, 0.18f, 0.95f), edgeM); eyes.position.set(0.55f, 0.25f, 0); Hd.add(eyes);
                    scene.add(Hd); heads[p] = Hd;
                }
                float form = R.slamT != 0 ? Mathf.Max(0, 1 - (float)(R.t - R.slamT) / 10) : Mathf.Min(1, (float)(R.t / ULT.ram.brace));
                if (form <= 0.01f) { Hd.visible = false; continue; }
                Hd.position.copy(S.W(p.x + R.dx * 1.6, p.y + 1.5, 0.3));
                var rig = fx.RigOf(p);
                Hd.rotation.y = (rig != null ? rig.root.rotation.y : 0) + (R.dx > 0 ? 0 : Mathf.PI);
                Hd.scale.setScalar(1.6f * form * (1 + 0.05f * Mathf.Sin(t * 30))); Hd.visible = true;
            }
        }

        // A hit on the Rampart cracks it where it landed: a new crack, or the one already there spreads
        void CrackAt(Player p, Pane P, double x, double y, float dmg, float frac)
        {
            var c = PlayerSim.Chest(p); var (nx, ny) = GuardDir(p); double cx = c.x + nx * RAM.guard.reach, cy = c.y + ny * RAM.guard.reach;
            float u = Mathf.Clamp((float)((x - cx) * -ny + (y - cy) * nx), -(float)RAM.guard.half * 0.9f, (float)RAM.guard.half * 0.9f);
            float at = frac * (float)RAM.guard.integrity + 12;
            var k = P.cracks.Find(q => q.at > p.integrity && Mathf.Abs(q.u - u) < 0.35f);
            if (k != null) { k.size = Mathf.Min(1.3f, k.size + 0.08f + dmg * 0.01f); k.at = Mathf.Max(k.at, at); return; }
            k = P.cracks.Find(q => q.o < 0.02f && !(q.at > p.integrity));
            if (k == null && P.cracks.Count < 8)
            {
                var m = new TMesh(Geo.Plane(1, 1), new TMat(TMat.Kind.Basic) { colorLin = S.Lin(PALE) * 1.5f, transparent = true, opacity = 0, blending = Blending.Additive, depthWrite = false, side = Side.Double });
                m.visible = false; m.RenderOrder = 6; scene.add(m); k = new Crack { m = m }; P.cracks.Add(k);
            }
            if (k == null) { k = P.cracks[0]; foreach (var q in P.cracks) if (q.at < k.at) k = q; }
            k.m.material.map = crackTex[(int)(S.Rnd() * crackTex.Length) % crackTex.Length];
            k.u = u; k.w = (S.Rnd() - 0.5f) * 0.35f; k.size = 0.4f + Mathf.Min(0.5f, dmg * 0.025f); k.rot = S.Rnd() * Mathf.PI * 2; k.at = at;
        }
        // Shards of the pane fly off: n of them, from (x, y) on it (a chip), or from all over it (null: it shatters)
        void Shatter(Player p, double? x, double? y, int n, float power)
        {
            var c = PlayerSim.Chest(p); var (nx, ny) = GuardDir(p); double cx = c.x + nx * RAM.guard.reach, cy = c.y + ny * RAM.guard.reach;
            var P = PaneOf(p); var q = P.face.tr.localRotation;
            for (int i = 0; i < n; i++)
            {
                var Sh = shards[si2]; si2 = (si2 + 1) % shards.Count;
                double u = x == null ? (S.Rnd() - 0.5f) * RAM.guard.half * 2 : (x.Value - cx) * -ny + (y.Value - cy) * nx + (S.Rnd() - 0.5f) * 0.5f;
                double w = (S.Rnd() - 0.5f) * 0.5f, sx = cx - ny * u + nx * w, sy = cy + nx * u + ny * w;
                Sh.m.position.copy(S.W(sx, sy, 0.35)); Sh.m.SetQuaternion(q);
                float sp = (3 + S.Rnd() * 6) * power, a = Mathf.Atan2((float)ny, (float)nx) + (S.Rnd() - 0.5f) * 1.6f;
                Sh.v = S.Dir(sx, Mathf.Cos(a) * sp, Mathf.Sin(a) * sp + 2 + S.Rnd() * 3); Sh.v.z += (S.Rnd() - 0.2f) * 3;
                Sh.spin = new Vector3((S.Rnd() - 0.5f) * 18, (S.Rnd() - 0.5f) * 18, (S.Rnd() - 0.5f) * 18);
                Sh.size = (0.16f + S.Rnd() * 0.26f) * (x == null ? 1.25f : 0.9f); Sh.m.scale.setScalar(Sh.size);
                Sh.life = Sh.max = 0.55f + S.Rnd() * 0.5f; Sh.m.visible = true;
            }
            if (x == null) { fx.Sprite(cx, cy, "glow", BLUE, 2.6f, 0.25f, 1.6f); fx.Sprite(cx, cy, "star", WHITE, 2.2f, 0.16f, 1.5f); }
            else fx.Sprite(x.Value, y.Value, "star", WHITE, 1.0f, 0.12f, 1.4f);
        }
        void UpdateShards(float dt)
        {
            foreach (var Sh in shards)
            {
                if (Sh.life <= 0) { Sh.m.visible = false; continue; }
                Sh.life -= dt; Sh.v.y -= 14 * dt; Sh.v *= Mathf.Pow(0.985f, dt * 60);
                Sh.m.position.copy(Sh.m.position.v + Sh.v * dt);
                Sh.m.tr.localRotation = Sh.m.tr.localRotation * Th.Quat(Sh.spin.x * dt, Sh.spin.y * dt, Sh.spin.z * dt);
                float k = Mathf.Max(0, Sh.life / Sh.max);
                Sh.m.material.opacity = 0.95f * Mathf.Min(1, k * 1.8f); Sh.m.scale.setScalar(Sh.size * (0.6f + 0.4f * k));
            }
        }
        // Sparks thrown back from a point scraping along the floor, `dir` the way he is moving
        public void Sparks(double x, double y, float dir, float n, float k = 1)
        {
            float a0 = dir > 0 ? Mathf.PI - 0.35f : 0.35f;
            for (int i = 0; i < n; i++)
            {
                var Sp = spk[si]; si = (si + 1) % spk.Length;
                float a = a0 + (S.Rnd() - 0.5f) * 0.9f, sp = (6 + S.Rnd() * 9) * k;
                Sp.x = x; Sp.y = y; Sp.vx = Mathf.Cos(a) * sp; Sp.vy = Mathf.Sin(a) * sp; Sp.d = 0.1f + S.Rnd() * 0.55f;
                Sp.life = Sp.max = 0.22f + S.Rnd() * 0.25f; Sp.c = S.Lin(SPARKS[(int)(S.Rnd() * SPARKS.Length) % SPARKS.Length]);
                if (i % 2 == 0) fx.Burst(x, y, SPARKS[0], 1, sp * 0.8f, 0.2f, 0.25f, dir: a, spread: 0.3f, grav: 18, drag: 0.95f);
            }
            if (n >= 2) fx.Sprite(x, y + 0.05, "glow", "#ffb24a", 0.55f + 0.05f * n, 0.08f, 1.3f);
        }
        void UpdateSparks(float dt)
        {
            var cool = S.Lin("#8a1c00");
            for (int i = 0; i < spk.Length; i++)
            {
                var Sp = spk[i];
                if (Sp.life <= 0) { for (int q = 0; q < 4; q++) { spkMesh.pos[i * 4 + q] = Vector3.zero; spkMesh.col[i * 4 + q] = Color.clear; } continue; }
                Sp.life -= dt; Sp.vy -= 22 * dt; Sp.vx *= Mathf.Pow(0.97f, dt * 60);
                Sp.x += Sp.vx * dt; Sp.y += Sp.vy * dt;
                double g = Level.GroundBelow(Sp.x, Sp.y + 0.3);
                if (Sp.y < g && Sp.y > g - 0.3) { Sp.y = g; Sp.vy = System.Math.Abs(Sp.vy) * 0.35; Sp.vx *= 0.7; }
                float k = Mathf.Max(0, Sp.life / Sp.max), spd = (float)JMath.Hypot(Sp.vx, Sp.vy);
                var T = S.Dir(Sp.x, Sp.vx, Sp.vy).normalized; var f = Level.Frame(Sp.x); var Z = new Vector3((float)f.nx, 0, (float)f.nz).normalized; var X = Vector3.Cross(T, Z).normalized;
                var c = S.W(Sp.x, Sp.y, Sp.d); float hw = 0.06f * (0.5f + 0.5f * k) / 2, hl = (0.06f + spd * 0.028f) / 2;
                spkMesh.Set(i * 4, c - X * hw - T * hl); spkMesh.Set(i * 4 + 1, c + X * hw - T * hl); spkMesh.Set(i * 4 + 2, c + X * hw + T * hl); spkMesh.Set(i * 4 + 3, c - X * hw + T * hl);
                var col = Color.Lerp(cool, Sp.c, Mathf.Min(1, k * 1.6f)); col.a = 1;
                for (int q = 0; q < 4; q++) spkMesh.col[i * 4 + q] = col;
            }
            spkMesh.Upload();
        }
        // A crater in the floor under (x, y), `r` m across its hollow, sized down to fit the ledge it is on
        public void Crater(double x, double y, float r, float heat = 1)
        {
            var fl0 = fx.FloorUnder(x, y, 2.5); if (fl0 == null) return; double fl = fl0.Value;
            bool Same(double xx) => System.Math.Abs(Level.GroundBelow(xx, fl + 0.3) - fl) < 0.05;
            while (r > 0.4f && !(Same(x - r) && Same(x + r))) r -= 0.1f;
            var C = NextCrater(r * 3.3f);
            C.b.position.copy(S.W(x, fl + 0.02, 0)); C.g.position.copy(C.b.position.v);
            C.b.rotation.set(-Mathf.PI / 2, 0, S.Rnd() * Mathf.PI * 2); C.g.rotation.set(-Mathf.PI / 2, 0, C.b.rotation.z);
            C.heat = heat;
        }
        // A crater in the wall a pile was slammed into
        void WallCrater(double x, double y, double dir, float r)
        {
            double? wx = null;
            for (double d = 0; d <= 4; d += 0.05) if (Level.PointInSolid(x + dir * d, y)) { wx = x + dir * (d - 0.02); break; }
            if (wx == null) return;
            var C = NextCrater(r * 3.3f); var f = Level.Frame(wx.Value);
            var pos = S.W(wx.Value, y, 0);
            C.b.position.copy(pos); C.g.position.copy(pos);
            // (lookAt for a mesh turns its +z toward the target: the wall face looks back along the path)
            var dirTo = new Vector3((float)(-f.tx * dir), 0, (float)(-f.tz * dir)).normalized;
            var q = ThQ.FromBasis(Vector3.Cross(Vector3.up, dirTo).normalized, Vector3.up, dirTo) * Th.Quat(0, 0, S.Rnd() * Mathf.PI * 2);
            C.b.SetQuaternion(q); C.g.SetQuaternion(q);
            C.heat = 1.4f;
        }
        CraterMark NextCrater(float size)
        {
            var C = craters.Find(q => q.life <= 0);
            if (C == null) { C = craters[0]; foreach (var q in craters) if (q.age > C.age) C = q; }
            var T = craterTex[(int)(S.Rnd() * craterTex.Length) % craterTex.Length];
            C.b.material.map = T.b; C.g.material.map = T.g;
            C.b.scale.set(size, size, 1); C.g.scale.set(size, size, 1);
            C.life = CR_LIFE; C.age = 0; C.b.visible = C.g.visible = true;
            return C;
        }
        void UpdateCraters(float dt)
        {
            foreach (var C in craters)
            {
                if (C.life <= 0) { C.b.visible = C.g.visible = false; continue; }
                C.life -= dt; C.age += dt;
                C.b.material.opacity = Mathf.Min(1, C.age * 12) * Mathf.Min(1, Mathf.Max(0, C.life) / CR_FADE);
                C.g.material.opacity = Mathf.Max(0, 1 - C.age / CR_GLOW) * 0.9f * C.heat;
            }
        }
    }
}
