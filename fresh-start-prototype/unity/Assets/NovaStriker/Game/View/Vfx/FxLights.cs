// Light from shots and blasts: a small pool of point lights that flash and die away with a flicker. Never more than
// FxCfg.MaxLights at once (none when off or on Low quality); a new flash takes the oldest. Forward+ rendering
// (NovaSetup) lifts the per-object light limit, so they light everything near them.
using UnityEngine;

namespace NovaStriker.Game
{
    public sealed class FxLights
    {
        const int N = 8;
        sealed class L { public Light l; public float t, life, k; public uint seed; }
        readonly L[] ls = new L[N];
        int next;
        public static int ActiveLights { get; private set; }

        public FxLights(Transform parent)
        {
            ActiveLights = 0;
            for (int i = 0; i < N; i++)
            {
                var go = new GameObject("fx light " + i); go.transform.SetParent(parent, false);
                var l = go.AddComponent<Light>(); l.type = LightType.Point; l.shadows = LightShadows.None; l.enabled = false;
                l.renderMode = LightRenderMode.ForcePixel;
                ls[i] = new L { l = l };
            }
        }

        public int Active { get { int n = 0; foreach (var x in ls) if (x.l.enabled) n++; return n; } }

        // A flash at a three.js point: colour, peak intensity, range (m), life (s)
        public void Flash(Vector3 at, Color c, float intensity, float range, float life)
        {
            int max = Mathf.Max(0, FxCfg.MaxLights - Sparks.ActiveLights);
            if (max <= 0) return;
            if (Active >= max) { int oldest = 0; float best = -1; for (int i = 0; i < N; i++) if (ls[i].l.enabled && ls[i].t / ls[i].life > best) { best = ls[i].t / ls[i].life; oldest = i; } next = oldest; }
            else for (int i = 0; i < N; i++) { int j = (next + i) % N; if (!ls[j].l.enabled) { next = j; break; } }
            var x = ls[next]; next = (next + 1) % N;
            x.l.transform.position = new Vector3(at.x, at.y, -at.z);
            x.l.color = c; x.l.range = range; x.k = intensity; x.t = 0; x.life = Mathf.Max(0.05f, life); x.seed = (uint)Random.Range(1, 1 << 30);
            x.l.intensity = intensity; x.l.enabled = true;
            ActiveLights = Active;
        }

        public void Update(float dt)
        {
            int allowed = Mathf.Max(0, FxCfg.MaxLights - Sparks.ActiveLights);
            foreach (var x in ls)
            {
                if (x.l.enabled && allowed-- <= 0) x.l.enabled = false;
                if (!x.l.enabled) continue;
                x.t += dt;
                float u = x.t / x.life;
                if (u >= 1) { x.l.enabled = false; continue; }
                float flicker = 0.85f + 0.15f * Mathf.PerlinNoise(x.seed % 1000, x.t * 30);
                x.l.intensity = x.k * (1 - u) * (1 - u) * flicker;
            }
            ActiveLights = Active;
        }
    }
}
