// Sparks with weight and light (Nova's skate blades): each spark is a bright streak that falls, bounces along the
// floor two or three times losing speed, and dies away, and every shower lights the floor round it with a
// flickering warm point light while it lasts. They live in route coordinates (x along the route, y up, dz toward
// the camera), so they follow the path's curves like the rest of the game, and bounce on Level's real floors.
using NovaStriker.Game.Three;
using NovaStriker.Sim;
using UnityEngine;

namespace NovaStriker.Game
{
    public sealed class Sparks
    {
        const int N = 320, LIGHTS = 4;
        const float GRAVITY = 20, BOUNCE = 0.42f, FRICTION = 0.72f;
        sealed class P { public double x, y; public float dz, vx, vy, vdz, life, max, size; public double floor; public int bounces; public Color c; }
        readonly P[] ps = new P[N]; int next;
        readonly DynMesh dm;
        readonly Light[] lights = new Light[LIGHTS]; readonly float[] lightK = new float[LIGHTS]; readonly int[] lightOwner = new int[LIGHTS];
        readonly Camera cam;
        float t;

        public Sparks(TObj scene, Camera cam)
        {
            this.cam = cam;
            ParticleBudget.Register(() => { int n = 0; foreach (var p in ps) if (p != null && p.life > 0) n++; return n; }, excess => { int dropped = 0; foreach (var p in ps) if (p != null && p.life > 0 && dropped < excess) { p.life = 0; dropped++; } return dropped; });
            for (int i = 0; i < N; i++) ps[i] = new P();
            var tris = new int[N * 6];
            for (int i = 0; i < N; i++) { int v = i * 4, k = i * 6; tris[k] = v; tris[k + 1] = v + 2; tris[k + 2] = v + 1; tris[k + 3] = v + 1; tris[k + 4] = v + 2; tris[k + 5] = v + 3; }
            var mat = new TMat(TMat.Kind.Basic) { vertexColors = true, transparent = true, depthWrite = false, blending = Blending.Additive, side = Side.Double };
            dm = new DynMesh(N * 4, tris, mat, true, false, "sparks");
            scene.add(dm.obj);
            for (int i = 0; i < LIGHTS; i++)
            {
                var go = new GameObject("spark-light"); var l = go.AddComponent<Light>();
                l.type = LightType.Point; l.color = new Color(1f, 0.78f, 0.45f); l.range = 3.6f; l.intensity = 0; l.shadows = LightShadows.None; l.enabled = false;
                lights[i] = l; lightOwner[i] = -1;
            }
        }

        // A shower of `n` sparks from a sim point, thrown toward `dir` (radians; 0 = forward along +x, PI/2 = up)
        // at about `speed` m/s. `owner` keeps one light per source (a sliding player keeps relighting the same one).
        public void Emit(int owner, double x, double y, float n, float dir, float speed, float spread = 0.7f, string color = "#ffe2a8", float light = 1)
        {
            double floor = Level.GroundBelow(x, y + 0.3);
            if (double.IsNegativeInfinity(floor)) floor = y - 50;
            var c = S.Lin(color);
            int count = Mathf.Max(1, Mathf.RoundToInt(n * (0.6f + Random.value * 0.8f)));
            for (int i = 0; i < count; i++)
            {
                if (!ParticleBudget.Admit()) break;
                var p = ps[next]; next = (next + 1) % N;
                float a = dir + (Random.value - 0.5f) * spread, sp = speed * (0.5f + Random.value * 0.8f);
                p.x = x; p.y = y; p.dz = (Random.value - 0.5f) * 0.3f; p.floor = floor; p.bounces = 0;
                p.vx = Mathf.Cos(a) * sp; p.vy = Mathf.Sin(a) * sp; p.vdz = (Random.value - 0.5f) * sp * 0.5f;
                p.life = p.max = 0.45f + Random.value * 0.6f; p.size = 0.02f + Random.value * 0.02f;
                p.c = c * (2.5f + Random.value * 2f);   // (bright enough to bloom)
            }
            if (FxCfg.Lights) Light(owner, x, y, light * Mathf.Min(1.5f, 0.4f + count * 0.12f));
        }

        void Light(int owner, double x, double y, float k)
        {
            int slot = -1;
            for (int i = 0; i < LIGHTS; i++) if (lightOwner[i] == owner) { slot = i; break; }
            if (slot < 0) { slot = 0; for (int i = 1; i < LIGHTS; i++) if (lightK[i] < lightK[slot]) slot = i; lightOwner[slot] = owner; }
            lightK[slot] = Mathf.Max(lightK[slot], k);
            lights[slot].transform.position = Th.P(S.W(x, y + 0.15, 0.2));
        }

        public static int ActiveLights;
        public void Update(float dt)
        {
            ActiveLights = 0; foreach (var light in lights) if (light.enabled) ActiveLights++;
            t += dt;
            var camPos = cam.transform.position;
            for (int i = 0; i < N; i++)
            {
                var p = ps[i]; int v = i * 4;
                if (p.life <= 0) { for (int k = 0; k < 4; k++) dm.col[v + k] = Color.clear; continue; }
                p.life -= dt;
                p.vy -= GRAVITY * dt;
                p.x += p.vx * dt; p.y += p.vy * dt; p.dz += p.vdz * dt;
                if (p.y < p.floor && p.vy < 0)
                {
                    if (p.bounces < 3 && p.vy < -1.2f) { p.y = p.floor; p.vy = -p.vy * BOUNCE; p.vx *= FRICTION; p.vdz *= FRICTION; p.bounces++; }
                    else { p.y = p.floor; p.vy = 0; p.vx *= Mathf.Pow(0.05f, dt); p.vdz *= Mathf.Pow(0.05f, dt); }
                }
                // a streak along its motion, facing the camera
                var a = S.W(p.x, p.y, p.dz);
                var vel = S.Dir(p.x, p.vx, p.vy) + new Vector3(0, 0, p.vdz);
                float speed = vel.magnitude;
                var d = speed > 0.01f ? vel / speed : Vector3.up;
                var b = a - d * Mathf.Min(0.35f, 0.03f + speed * 0.022f);
                var toCam = (Th.P(camPos) - a).normalized;
                var side = Vector3.Cross(d, toCam).normalized * p.size;
                dm.Set(v, a + side); dm.Set(v + 1, a - side); dm.Set(v + 2, b + side); dm.Set(v + 3, b - side);
                float k2 = Mathf.Clamp01(p.life / p.max), fade = k2 * k2;
                var head = p.c * fade; head.a = fade; var tail = p.c * fade * 0.3f; tail.a = 0;
                dm.col[v] = head; dm.col[v + 1] = head; dm.col[v + 2] = tail; dm.col[v + 3] = tail;
            }
            dm.Upload();
            for (int i = 0; i < LIGHTS; i++)
            {
                lightK[i] *= Mathf.Exp(-dt * 7);
                var l = lights[i]; bool on = FxCfg.Lights && lightK[i] > 0.02f;
                if (l.enabled != on) l.enabled = on;
                if (on) l.intensity = lightK[i] * 2.4f * (0.75f + 0.25f * Mathf.Sin(t * 47 + i * 2.1f) * Mathf.Sin(t * 31 + i));
                else lightOwner[i] = -1;
            }
        }
    }
}
