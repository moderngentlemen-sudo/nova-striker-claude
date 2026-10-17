// Nova's Disc as a hard-light saw (any Projectiles setting but Classic): a ring of hooked energy teeth spinning
// fast, two fading copies behind it for motion blur, a hot hub, and a trail. Wherever its teeth bite an enemy or
// scrape a wall or floor, sparks and hard-light chips are thrown off along the spin (tangentially) with a flash
// and a light. Each charge level adds teeth, makes them longer and brighter, and lengthens the trail and the
// spark showers; from level 2 it hovers at the end of its throw and grinds there in a constant shower. Overlaps
// are found each frame from the sim's positions (the sim's hit events carry no contact point).
using System.Collections.Generic;
using NovaStriker.Game.Three;
using NovaStriker.Sim;
using UnityEngine;
using static NovaStriker.Sim.Cfg;

namespace NovaStriker.Game
{
    public sealed class CineDisc
    {
        const int SLOTS = 4;
        static readonly int[] TEETH = { 8, 10, 13, 16 };
        readonly View view; readonly Transform root;
        readonly Material mBlade, mTrail;
        readonly Mesh[] blades = new Mesh[4];
        sealed class Slot
        {
            public Projectile pr; public bool seen;
            public Transform t; public MeshRenderer blade, ghost1, ghost2; public MeshFilter f0, f1, f2; public TrailRenderer trail;
            public float spin, sparkAcc, chipAcc, lightT, showerAcc;
        }
        readonly Slot[] slots = new Slot[SLOTS];
        readonly MaterialPropertyBlock mpb = new MaterialPropertyBlock();
        static readonly int BASE_COLOR = Shader.PropertyToID("_BaseColor");
        readonly Color tint, hot;

        public CineDisc(View view)
        {
            this.view = view;
            root = new GameObject("discs").transform;
            tint = S.Lin(SUB_LOOK["disc"].tint); hot = Color.Lerp(tint, Color.white, 0.7f);
            mBlade = FxPool.Mat(Templates.ParticleAdd, null, Color.white);
            mTrail = FxPool.Mat(Templates.ParticleAdd, CineLightning.SoftLine, Color.white);
            mTrail.SetColor(BASE_COLOR, Hdr(tint, 2.2f));
            for (int L = 0; L < 4; L++) blades[L] = Blade(L);
            var fadeOut = new Gradient();
            fadeOut.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0.9f, 0), new GradientAlphaKey(0, 1) });
            for (int i = 0; i < SLOTS; i++)
            {
                var go = new GameObject("disc " + i); go.transform.SetParent(root, false);
                MeshRenderer R(string n, out MeshFilter f)
                {
                    var g = new GameObject(n); g.transform.SetParent(go.transform, false);
                    f = g.AddComponent<MeshFilter>(); var r = g.AddComponent<MeshRenderer>();
                    r.sharedMaterial = mBlade; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
                    return r;
                }
                var s = new Slot { t = go.transform };
                s.blade = R("blade", out s.f0); s.ghost1 = R("blur 1", out s.f1); s.ghost2 = R("blur 2", out s.f2);
                var tr = go.AddComponent<TrailRenderer>();
                tr.sharedMaterial = mTrail; tr.alignment = LineAlignment.View; tr.minVertexDistance = 0.08f; tr.numCapVertices = 2;
                tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; tr.receiveShadows = false; tr.emitting = false;
                tr.widthCurve = AnimationCurve.EaseInOut(0, 1, 1, 0); tr.colorGradient = fadeOut;
                s.trail = tr;
                go.SetActive(false);
                slots[i] = s;
            }
        }

        // The saw: hooked teeth around a thin band of hard light, in the XY plane, radius 1. Teeth bases are the
        // disc's colour, their tips near white (vertex colours; the material carries the brightness).
        Mesh Blade(int L)
        {
            int n = TEETH[L]; float ri = 0.66f - 0.05f * L, band = ri - 0.1f, span = Mathf.PI * 2 / n;
            var v = new List<Vector3>(); var c = new List<Color>(); var uv = new List<Vector2>(); var tri = new List<int>();
            Color baseC = tint, tipC = hot; baseC.a = 0.9f; tipC.a = 1;
            for (int i = 0; i < n; i++)
            {
                float a0 = i * span, a1 = a0 + span * 0.82f, at = a0 + span * 1.05f;   // (the tip leans forward: hooked teeth)
                int k = v.Count;
                v.Add(new Vector3(Mathf.Cos(a0) * ri, Mathf.Sin(a0) * ri)); v.Add(new Vector3(Mathf.Cos(a1) * ri, Mathf.Sin(a1) * ri)); v.Add(new Vector3(Mathf.Cos(at), Mathf.Sin(at)));
                v.Add(new Vector3(Mathf.Cos(a0 + span * 0.6f) * (ri + (1 - ri) * 0.45f), Mathf.Sin(a0 + span * 0.6f) * (ri + (1 - ri) * 0.45f)));   // (a notch inside the hook)
                c.Add(baseC); c.Add(baseC); c.Add(tipC); c.Add(Color.Lerp(baseC, tipC, 0.5f));
                for (int j = 0; j < 4; j++) uv.Add(new Vector2(0.5f, 0.5f));
                tri.Add(k); tri.Add(k + 3); tri.Add(k + 1); tri.Add(k + 3); tri.Add(k + 2); tri.Add(k + 1);
            }
            // the band the teeth stand on
            const int seg = 40; int b0 = v.Count;
            for (int i = 0; i <= seg; i++)
            {
                float a = i * Mathf.PI * 2 / seg;
                v.Add(new Vector3(Mathf.Cos(a) * band, Mathf.Sin(a) * band)); v.Add(new Vector3(Mathf.Cos(a) * ri, Mathf.Sin(a) * ri));
                var cb = baseC; cb.a = 0.55f; c.Add(cb); c.Add(baseC); uv.Add(new Vector2(0.5f, 0.5f)); uv.Add(new Vector2(0.5f, 0.5f));
                if (i < seg) { int k = b0 + i * 2; tri.Add(k); tri.Add(k + 2); tri.Add(k + 1); tri.Add(k + 1); tri.Add(k + 2); tri.Add(k + 3); }
            }
            var m = new Mesh { name = "saw " + L };
            m.SetVertices(v); m.SetColors(c); m.SetUVs(0, uv); m.SetTriangles(tri, 0); m.RecalculateBounds();
            return m;
        }

        static Color Hdr(Color c, float k, float a = 1) { var o = c * k; o.a = a; return o; }

        public void Begin() { foreach (var s in slots) s.seen = false; }
        public void End()
        {
            foreach (var s in slots)
                if (!s.seen && s.pr != null) { s.pr = null; s.trail.Clear(); s.trail.emitting = false; s.t.gameObject.SetActive(false); }
        }

        // Each frame for each disc in flight: `at` its world point (three.js space), (x, y) its sim point
        public void Draw(Projectile pr, World world, Vector3 at, double x, double y, float dt)
        {
            Slot s = null;
            foreach (var q in slots) if (q.pr == pr) { s = q; break; }
            if (s == null) foreach (var q in slots) if (q.pr == null) { s = q; break; }
            if (s == null) return;
            int L = Mathf.Clamp((int)pr.level, 0, 3); bool fresh = s.pr != pr;
            if (fresh)
            {
                s.pr = pr; s.spin = Random.value * 6; s.t.gameObject.SetActive(true);
                s.f0.sharedMesh = s.f1.sharedMesh = s.f2.sharedMesh = blades[L];
                s.trail.time = (FxCfg.Trails == "long" ? 1 : 0.6f) * (0.07f + 0.07f * L); s.trail.widthMultiplier = (float)pr.r * 1.7f;
                s.trail.Clear();
            }
            s.seen = true;
            var V = view.vfx;
            bool hover = pr.disc != null && pr.disc.phase == "hover";
            float rv = (float)pr.r * 1.12f, k = 1 + 0.3f * L + (pr.perfect ? 0.3f : 0);
            float dir = pr.vx < 0 ? 1 : -1;   // (it spins so its top edge leads)
            s.spin += dt * (26 + 4 * L) * dir;
            // facing the camera across the path, tipped back a little so it reads as a disc
            var f = Level.Frame(x);
            var Nu = Th.P(new Vector3((float)f.nx, 0, (float)f.nz));
            var baseRot = Quaternion.LookRotation(Nu, Vector3.up) * Quaternion.AngleAxis(-20, Vector3.right);
            s.t.position = Th.P(at);
            s.blade.transform.localRotation = baseRot * Quaternion.Euler(0, 0, s.spin * Mathf.Rad2Deg);
            s.ghost1.transform.localRotation = baseRot * Quaternion.Euler(0, 0, (s.spin - dir * 0.16f) * Mathf.Rad2Deg);
            s.ghost2.transform.localRotation = baseRot * Quaternion.Euler(0, 0, (s.spin - dir * 0.32f) * Mathf.Rad2Deg);
            s.blade.transform.localScale = s.ghost1.transform.localScale = s.ghost2.transform.localScale = Vector3.one * rv;
            float flick = 0.9f + 0.2f * Random.value;
            Tint(s.blade, Hdr(Color.white, (2.4f + 0.7f * L) * flick));
            Tint(s.ghost1, Hdr(Color.white, (1.2f + 0.3f * L), 0.5f));
            Tint(s.ghost2, Hdr(Color.white, (0.7f + 0.2f * L), 0.25f));
            s.trail.emitting = true;
            // the hub burns and a soft glow surrounds it
            V.glow.Emit(at, Vector3.zero, rv * (2.4f + 0.3f * L), 0.05f, Hdr(tint, (1 + 0.35f * L) * flick));
            V.flash.Emit(at, Vector3.zero, rv * 0.7f, 0.04f, Hdr(hot, 3));

            // where the teeth bite: enemies it overlaps, and walls or floors it scrapes
            bool bite = false; double cx = 0, cy = 0;
            foreach (var e in world.enemies)
            {
                if (e.dead) continue;
                double bx = System.Math.Max(e.x - e.w / 2, System.Math.Min(e.x + e.w / 2, x)), by = System.Math.Max(e.y, System.Math.Min(e.y + e.h, y));
                double dx = bx - x, dy = by - y;
                if (dx * dx + dy * dy > rv * rv) continue;
                double d = System.Math.Sqrt(dx * dx + dy * dy);
                if (d < 1e-3) { dx = e.x - x; dy = e.y + e.h * 0.5 - y; d = System.Math.Max(1e-3, System.Math.Sqrt(dx * dx + dy * dy)); }
                cx = dx / d; cy = dy / d; bite = true; break;
            }
            bool scrape = false;
            if (!bite)
                for (int i = 0; i < 8; i++)
                {
                    double a = i * Mathf.PI / 4, ux = System.Math.Cos(a), uy = System.Math.Sin(a);
                    if (Level.PointInSolid(x + ux * rv * 1.05, y + uy * rv * 1.05)) { cx = ux; cy = uy; scrape = true; break; }
                }
            if (bite || scrape || hover)
            {
                if (hover && !bite && !scrape) { cx = 0; cy = -1; }   // (hovering in the open: it grinds the air, throwing its shower down)
                double px = x + cx * rv, py = y + cy * rv;
                var cp = S.W(px, py, 0) + (at - S.W(x, y, 0));   // (the contact point, at the disc's depth)
                // the rim there moves along the spin: perpendicular to the contact direction
                double tx = -cy * -dir, ty = cx * -dir;
                float rate = (bite ? 70 : scrape ? 50 : 0) + (hover ? 60 : 0);
                rate *= (1 + 0.45f * L) * FxCfg.Amount;
                s.sparkAcc += rate * dt;
                int n = Mathf.Min(24, (int)s.sparkAcc); s.sparkAcc -= n;
                for (int i = 0; i < n; i++)
                {
                    float sp = 7 + Random.value * (8 + 2 * L);
                    var v = S.Dir(px, tx * sp + cx * Random.value * 3, ty * sp + cy * Random.value * 3 + Random.value * 2) + Random.insideUnitSphere * 1.5f;
                    V.spark.Emit(cp, v, 0.03f + 0.01f * L, 0.18f + Random.value * 0.3f, Hdr(Random.value < 0.5f ? hot : new Color(1, 0.7f, 0.35f), 5));
                }
                s.chipAcc += (bite ? 18 : 8) * (1 + 0.5f * L) * FxCfg.Amount * dt;
                n = Mathf.Min(8, (int)s.chipAcc); s.chipAcc -= n;
                for (int i = 0; i < n; i++)
                {
                    float sp = 5 + Random.value * 6;
                    V.chip.Emit(cp, S.Dir(px, tx * sp, ty * sp + 1.5f) + Random.insideUnitSphere * 2, 0.05f + 0.02f * L, 0.25f + Random.value * 0.2f, Hdr(tint, 4), Random.value * 6.3f);
                }
                if (Random.value < 0.5f) V.flash.Emit(cp, Vector3.zero, 0.35f + 0.1f * L, 0.04f, Hdr(Color.white, 5));
                s.lightT -= dt;
                if (s.lightT <= 0) { s.lightT = 0.09f; V.lights.Flash(cp, Color.Lerp(tint, Color.white, 0.4f), 2.5f * k, 3 + L, 0.12f); }
                if (scrape && Random.value < 0.15f) V.decals.Stamp(px, py, 0.3f, "scuff", 3, 0.5f, 0, Random.value * 6.3f);
            }
            if (hover)
            {
                // the grinding shower: sparks thrown all round off the spinning teeth, a heat shimmer and a pulse of light
                s.showerAcc += (40 + 25 * L) * FxCfg.Amount * dt;
                int n = Mathf.Min(16, (int)s.showerAcc); s.showerAcc -= n;
                for (int i = 0; i < n; i++)
                {
                    float a = Random.value * Mathf.PI * 2; double ux = Mathf.Cos(a), uy = Mathf.Sin(a);
                    double ttx = -uy * -dir, tty = ux * -dir; float sp = 8 + Random.value * 8;
                    V.spark.Emit(at + S.Dir(x, ux * rv, uy * rv), S.Dir(x, ttx * sp, tty * sp + 1), 0.03f, 0.15f + Random.value * 0.25f, Hdr(Random.value < 0.4f ? Color.white : tint, 5));
                }
                if (FxCfg.Distortion && Random.value < 0.25f) V.haze.Emit(at, Vector3.zero, rv * 4, 0.2f, new Color(1, 1, 1, 0.5f));
            }
        }

        void Tint(Renderer r, Color c) { mpb.SetColor(BASE_COLOR, c); r.SetPropertyBlock(mpb); }

        public void Hide() { Begin(); End(); }
    }
}
