// A beam weapon drawn with Unity's effects (any Projectiles setting but Classic, which keeps the original strips):
// three line renderers (a wide soft halo, the surging sheath in its colour, a white-hot core) carrying a scrolling
// energy texture; energy streaks and motes racing down it; heat haze rippling along it and at both ends; a flaring
// muzzle and a burning impact (sparks, embers, flame, smoke that hangs, scorch marks) with real lights at both ends.
// When it fires: a screen flash, a shockwave through the picture, a burst at the muzzle and a kick of the camera;
// when it dies, it collapses in a flash and leaves smoke along its path. Used by the Level 4 beams, the Supernova
// and the bosses' lasers. Points come in three.js space as the rest of the effects.
using System.Collections.Generic;
using NovaStriker.Game.Three;
using UnityEngine;

namespace NovaStriker.Game
{
    public sealed class CineBeam
    {
        readonly View view; readonly Transform root;
        readonly LineRenderer halo, sheath, core; readonly Material mHalo, mSheath, mCore;
        readonly Light lMuzzle, lHit;
        readonly Dictionary<string, float> acc = new Dictionary<string, float>();
        Vector3[] buf = new Vector3[64];
        readonly List<Vector3> path = new List<Vector3>(); readonly List<float> along = new List<float>();
        float t, len, scorchT; bool wasLive;
        Vector3 lastStart, lastEnd, lastDir;
        public Color tint = Color.white;
        public float width = 0.5f, power = 1;   // (the sheath's width in m; power scales everything else)
        static Texture2D energy;

        public CineBeam(View view, string name)
        {
            this.view = view;
            root = new GameObject("beam " + name).transform;
            LineRenderer Line(string n, out Material m)
            {
                var go = new GameObject(n); go.transform.SetParent(root, false);
                var l = go.AddComponent<LineRenderer>();
                m = FxPool.Mat(Templates.ParticleAdd, Energy, Color.white);
                l.sharedMaterial = m; l.useWorldSpace = true; l.textureMode = LineTextureMode.Tile;
                l.numCapVertices = 4; l.numCornerVertices = 2; l.alignment = LineAlignment.View;
                l.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; l.receiveShadows = false;
                l.widthCurve = new AnimationCurve(new Keyframe(0, 0.55f), new Keyframe(0.06f, 1), new Keyframe(0.94f, 1), new Keyframe(1, 0.75f));
                l.positionCount = 0; l.enabled = false;
                return l;
            }
            halo = Line("halo", out mHalo); sheath = Line("sheath", out mSheath); core = Line("core", out mCore);
            mHalo.SetTextureScale("_BaseMap", new Vector2(0.08f, 1)); mSheath.SetTextureScale("_BaseMap", new Vector2(0.18f, 1)); mCore.SetTextureScale("_BaseMap", new Vector2(0.35f, 1));
            Light L(string n)
            {
                var go = new GameObject(n); go.transform.SetParent(root, false);
                var l = go.AddComponent<Light>(); l.type = LightType.Point; l.shadows = LightShadows.None; l.renderMode = LightRenderMode.ForcePixel; l.enabled = false;
                return l;
            }
            lMuzzle = L("muzzle light"); lHit = L("impact light");
        }

        // A tileable band of energy: soft across the beam, streaky and uneven along it
        static Texture2D Energy
        {
            get
            {
                if (energy != null) return energy;
                const int W = 256, H = 32;
                energy = new Texture2D(W, H, TextureFormat.RGBA32, true, true) { wrapMode = TextureWrapMode.Repeat, name = "beam energy" };
                energy.wrapModeV = TextureWrapMode.Clamp;
                var px = new Color32[W * H];
                for (int y = 0; y < H; y++)
                {
                    float v = (y + 0.5f) / H * 2 - 1, across = Mathf.Exp(-v * v * 3.2f);
                    for (int x = 0; x < W; x++)
                    {
                        float u = x / (float)W * Mathf.PI * 2;
                        // bands that run along the beam, each wandering a little across it
                        float band = 0.62f + 0.18f * Mathf.Sin(u * 3 + v * 2.1f) + 0.12f * Mathf.Sin(u * 7 - v * 3.3f + 1.3f) + 0.08f * Mathf.Sin(u * 13 + 2.4f);
                        float a = Mathf.Clamp01(across * band * 1.15f);
                        byte b = (byte)(a * 255); px[y * W + x] = new Color32(255, 255, 255, b);
                    }
                }
                energy.SetPixels32(px); energy.Apply(true, true);
                return energy;
            }
        }

        static Color Hdr(Color c, float k, float a = 1) { var o = c * k; o.a = a; return o; }
        static Vector3 Rnd3() => Random.insideUnitSphere;
        int Count(string key, float perSec, float dt)
        {
            acc.TryGetValue(key, out var a);
            a += perSec * FxCfg.Amount * dt;
            int n = (int)a; acc[key] = a - n;
            return Mathf.Min(n, 60);
        }
        // A point at distance d along the path, and the path's direction there
        Vector3 At(float d, out Vector3 dir)
        {
            int i = 1;
            while (i < along.Count - 1 && along[i] < d) i++;
            float a = along[i - 1], b = along[i], f = b > a ? Mathf.Clamp01((d - a) / (b - a)) : 0;
            dir = (path[i] - path[i - 1]).normalized;
            return Vector3.Lerp(path[i - 1], path[i], f);
        }

        // Each frame. `k` is how open it is (0 hidden, 1 full); `live` while the weapon fires. Sim points: the muzzle
        // (mx, my), the end (ex, ey) and the surface normal there (nx, ny) when `hit` it struck something.
        public void Update(float dt, List<Vector3> pts, float k, bool live, double mx, double my, double ex, double ey, double nx, double ny, bool hit)
        {
            t += dt;
            var V = view.vfx;
            if (pts == null || pts.Count < 2 || k <= 0.001f)
            {
                if (wasLive && path.Count >= 2) Collapse(V, mx, my);
                wasLive = false; Hide();
                return;
            }
            // the path, and the distance along it at each point
            path.Clear(); along.Clear(); len = 0;
            for (int i = 0; i < pts.Count; i++) { if (i > 0) len += Vector3.Distance(pts[i], pts[i - 1]); path.Add(pts[i]); along.Add(len); }
            if (len < 0.05f) { Hide(); return; }
            Vector3 start = path[0], end = path[path.Count - 1], dir0 = (path[1] - path[0]).normalized;
            lastStart = start; lastEnd = end; lastDir = dir0;
            if (live && !wasLive) Ignite(V, start, dir0, mx, my);
            if (!live && wasLive) Collapse(V, mx, my);
            wasLive = live;

            // the body: three lines over the same path, pulsing and flickering
            if (buf.Length < path.Count) buf = new Vector3[path.Count * 2];
            for (int i = 0; i < path.Count; i++) buf[i] = Th.P(path[i]);
            float flick = 0.88f + Random.value * 0.24f, surge = 1 + 0.12f * Mathf.Sin(t * 38) + 0.05f * Mathf.Sin(t * 91);
            void Set(LineRenderer l, Material m, float w, Color c, float scroll)
            {
                l.enabled = true; l.positionCount = path.Count; l.SetPositions(buf);
                l.widthMultiplier = w; m.SetColor("_BaseColor", c);
                m.SetTextureOffset("_BaseMap", new Vector2(-t * scroll, 0));
            }
            var hot = Color.Lerp(tint, Color.white, 0.75f);
            Set(halo, mHalo, width * 3.4f * k * surge, Hdr(tint, 0.9f * power, 0.32f * k), 6);
            Set(sheath, mSheath, width * 1.15f * k * surge, Hdr(tint, 2.6f * power, 0.9f * k), 14);
            Set(core, mCore, width * 0.42f * k * flick, Hdr(hot, 5f * power, 1), 22);

            // light at both ends (when effect lights are on)
            bool lights = FxCfg.Lights;
            lMuzzle.enabled = lights; lHit.enabled = lights && hit;
            if (lights)
            {
                lMuzzle.transform.position = Th.P(start + dir0 * 0.3f); lMuzzle.color = Color.Lerp(tint, Color.white, 0.3f);
                lMuzzle.intensity = 7 * power * k * flick; lMuzzle.range = 6 + 3 * power;
                if (hit) { lHit.transform.position = Th.P(end - (end - path[path.Count - 2]).normalized * 0.4f); lHit.color = Color.Lerp(tint, new Color(1, 0.75f, 0.45f), 0.35f); lHit.intensity = 9 * power * k * (0.75f + Random.value * 0.5f); lHit.range = 6 + 3 * power; }
            }
            if (!live) return;

            // energy racing down it
            int n = Count("streak", 70 * power * Mathf.Min(4, len / 8 + 0.5f), dt);
            for (int i = 0; i < n; i++)
            {
                var p = At(Random.value * len, out var d);
                V.streak.Emit(p + Rnd3() * width * 0.45f, d * (45 + Random.value * 25), width * (0.12f + Random.value * 0.1f), 0.12f + Random.value * 0.12f, Hdr(Random.value < 0.5f ? hot : tint, 4));
            }
            n = Count("mote", 40 * power * Mathf.Min(4, len / 8 + 0.5f), dt);
            for (int i = 0; i < n; i++)
            {
                var p = At(Random.value * len, out var d);
                var off = Vector3.Cross(d, Random.onUnitSphere).normalized * width * (0.6f + Random.value * 0.8f);
                V.mote.Emit(p + off, off * 3 + d * 6, 0.06f + Random.value * 0.06f, 0.3f + Random.value * 0.3f, Hdr(tint, 5));
            }
            // heat haze rippling along the beam and pouring off the muzzle
            if (FxCfg.Distortion)
            {
                n = Count("haze", 26 * Mathf.Min(5, len / 4 + 1), dt);
                for (int i = 0; i < n; i++) { var p = At(Random.value * len, out var d); V.haze.Emit(p + Rnd3() * width * 0.3f, Rnd3() * 0.6f + Vector3.up * 0.5f, width * (3.2f + Random.value * 1.6f), 0.28f + Random.value * 0.15f, new Color(1, 1, 1, 0.7f)); }
                if (Count("hazeM", 18, dt) > 0) V.haze.Emit(start + dir0 * 0.4f, dir0 * 2, width * 5.5f * power, 0.22f, new Color(1, 1, 1, 0.8f));
            }
            // the muzzle burns white
            V.glow.Emit(start, Vector3.zero, width * 6 * power * (0.9f + Random.value * 0.3f), 0.06f, Hdr(tint, 2.2f));
            V.flash.Emit(start, Vector3.zero, width * 3.2f * power * flick, 0.05f, Hdr(hot, 5));
            // the impact: sparks and embers spraying off the surface, flame licking it, smoke that hangs, scorch marks
            if (hit)
            {
                var nrm = new Vector3((float)nx, (float)ny, 0); var sd = S.Dir(ex, nx, ny); if (sd.sqrMagnitude > 1e-4f) nrm = sd.normalized;
                V.glow.Emit(end, Vector3.zero, width * 7 * power * (0.85f + Random.value * 0.3f), 0.07f, Hdr(Color.Lerp(tint, new Color(1, 0.8f, 0.5f), 0.4f), 2.4f));
                V.flash.Emit(end, Vector3.zero, width * 3 * power * flick, 0.05f, Hdr(hot, 4));
                n = Count("spark", 150 * power, dt);
                for (int i = 0; i < n; i++) V.spark.Emit(end, (nrm * (0.6f + Random.value) + Rnd3() * 0.9f).normalized * (8 + Random.value * 12), 0.04f + Random.value * 0.04f, 0.3f + Random.value * 0.45f, Hdr(Random.value < 0.4f ? Color.white : Color.Lerp(tint, new Color(1, 0.8f, 0.5f), 0.5f), 4));
                n = Count("ember", 40 * power, dt);
                for (int i = 0; i < n; i++) V.ember.Emit(end + Rnd3() * 0.3f, (nrm + Rnd3() * 0.7f + Vector3.up * 0.4f) * (2 + Random.value * 3), 0.05f + Random.value * 0.06f, 0.8f + Random.value, Hdr(new Color(1, 0.6f, 0.3f), 3));
                n = Count("fire", 14 * power, dt);
                for (int i = 0; i < n; i++) V.fire.Emit(end + nrm * 0.2f + Rnd3() * 0.25f, nrm * (1 + Random.value * 1.5f) + Vector3.up * 0.8f, width * (1.2f + Random.value), 0.35f + Random.value * 0.2f, Hdr(Color.Lerp(new Color(1, 0.75f, 0.5f), tint, 0.3f), 1.8f), Random.value * 6.3f);
                if (FxCfg.Smoke != "off")
                {
                    n = Count("smoke", (FxCfg.Smoke == "rich" ? 7 : 3) * power, dt);
                    for (int i = 0; i < n; i++)
                    {
                        var c = Color.Lerp(new Color(0.32f, 0.31f, 0.31f), new Color(0.55f, 0.54f, 0.54f), Random.value); c.a = 0.85f;
                        V.smoke.Emit(end + nrm * 0.4f + Rnd3() * 0.3f, nrm * 1.2f + Vector3.up * 0.7f + Rnd3() * 0.4f, 0.6f + Random.value * 0.6f, 3.5f + Random.value * 2.5f, c, Random.value * 6.3f, (Random.value - 0.5f) * 0.5f);
                    }
                }
                if (FxCfg.Distortion && Count("hazeH", 16, dt) > 0) V.haze.Emit(end + nrm * 0.3f, nrm + Vector3.up, width * 5 * power, 0.3f, new Color(1, 1, 1, 0.8f));
                // the scorch it burns into a floor (stamped every so often while it holds there)
                scorchT -= dt;
                if (scorchT <= 0 && ny > 0.6) { scorchT = 0.3f; V.decals.Stamp(ex, ey, width * 2.6f * power, "scorch", 9, 0.85f, 0, Random.value * 6.3f); }
            }
            // the frame strains while it fires: bloom, aberration and a shake that never quite settles
            view.Kick(Mathf.Min(1, 0.22f * power));
            view.ShakeAtLeast(Mathf.Min(0.5f, 0.12f + 0.08f * power));
        }

        // It fires: the screen flashes its colour, a shockwave bends the picture and the muzzle bursts
        void Ignite(Vfx V, Vector3 at, Vector3 dir, double mx, double my)
        {
            view.Flash(Color.Lerp(tint, Color.white, 0.5f), Mathf.Min(1.4f, 0.55f * power));
            view.Shockwave(mx, my, Mathf.Min(1.3f, 0.6f * power), 0.6f);
            view.Kick(Mathf.Min(1.3f, 0.8f * power));
            view.ShakeAtLeast(Mathf.Min(0.9f, 0.45f * power));
            V.lights.Flash(at, Color.Lerp(tint, Color.white, 0.4f), 14 * power, 10 * power, 0.25f);
            V.flash.Emit(at, Vector3.zero, width * 9 * power, 0.14f, Hdr(Color.white, 6));
            V.glow.Emit(at, Vector3.zero, width * 14 * power, 0.3f, Hdr(tint, 2.5f));
            if (FxCfg.Distortion) for (int i = 0; i < 3; i++) V.haze.Emit(at, Vector3.zero, width * (8 + i * 5) * power, 0.35f + i * 0.1f, new Color(1, 1, 1, 0.9f));
            int n = (int)(50 * power * FxCfg.Amount);
            for (int i = 0; i < n; i++)
            {
                var d = (dir * 0.7f + Random.onUnitSphere).normalized;
                V.spark.Emit(at, d * (10 + Random.value * 16), 0.05f, 0.25f + Random.value * 0.35f, Hdr(Color.Lerp(tint, Color.white, Random.value), 4));
                if (i % 2 == 0) V.streak.Emit(at, (dir * 1.4f + Random.onUnitSphere * 0.5f).normalized * (30 + Random.value * 20), width * 0.2f, 0.15f + Random.value * 0.1f, Hdr(tint, 4));
            }
        }

        // It dies: a closing flash at the muzzle, haze, and smoke left hanging along where it was
        void Collapse(Vfx V, double mx, double my)
        {
            view.Flash(tint, Mathf.Min(0.6f, 0.2f * power));
            V.flash.Emit(lastStart, Vector3.zero, width * 6 * power, 0.12f, Hdr(Color.white, 5));
            V.glow.Emit(lastStart, Vector3.zero, width * 9 * power, 0.25f, Hdr(tint, 2));
            if (path.Count < 2) return;
            int n = (int)(Mathf.Min(24, len / 1.4f) * FxCfg.Amount);
            for (int i = 0; i < n; i++)
            {
                var p = At(Random.value * len, out var d);
                if (FxCfg.Smoke != "off") { var c = new Color(0.62f, 0.62f, 0.64f, 0.5f); V.smoke.Emit(p, Rnd3() * 0.4f + Vector3.up * 0.3f, width * (1.4f + Random.value), 2.2f + Random.value * 1.5f, c, Random.value * 6.3f, (Random.value - 0.5f) * 0.3f); }
                if (FxCfg.Distortion && i % 2 == 0) V.haze.Emit(p, Vector3.up * 0.6f, width * 4, 0.45f, new Color(1, 1, 1, 0.6f));
                V.mote.Emit(p, Rnd3() * 3, 0.07f, 0.4f + Random.value * 0.3f, Hdr(tint, 4));
            }
        }

        public void Hide()
        {
            halo.enabled = sheath.enabled = core.enabled = false;
            lMuzzle.enabled = lHit.enabled = false;
        }
        public void Destroy() { Object.Destroy(root.gameObject); Object.Destroy(mHalo); Object.Destroy(mSheath); Object.Destroy(mCore); }
    }
}
