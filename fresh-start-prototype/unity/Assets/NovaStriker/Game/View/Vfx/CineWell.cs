// Nova's Gravity Well as a small black hole (any Projectiles setting but Classic). SubFX still draws its inky core,
// accretion disc and photon ring (scaled up here by level); this adds, each frame: motes spiralling in from the
// edge of its pull (faster as they near the core, swallowed by its dark rim) and a ring of them whirling round the
// tilted disc, both simulated here and drawn with one particle system; an inky shadow around the core; heat-haze
// lensing that closes in on it; rings of light closing in from the edge of its pull (light bending); the floor
// under it rippling and warping inward; from level 2, rubble torn off the floor and drawn in; and a light. Everything
// grows with the charge level, level 3 being a full black hole. Near its end it implodes (Fx.WellCollapse bursts).
using System.Collections.Generic;
using NovaStriker.Game.Three;
using NovaStriker.Sim;
using UnityEngine;
using static NovaStriker.Sim.Cfg;

namespace NovaStriker.Game
{
    public sealed class CineWell
    {
        const int MAXO = 160;
        readonly Fx fx; readonly View view;
        readonly ParticleSystem orbit; readonly ParticleSystem.Particle[] buf = new ParticleSystem.Particle[MAXO * 4];
        sealed class WS
        {
            public Well w; public bool seen;
            public readonly float[] r = new float[MAXO], a = new float[MAXO], dz = new float[MAXO], wa = new float[MAXO], vr = new float[MAXO]; public readonly bool[] disk = new bool[MAXO];
            public int n; public float pulseT, warpT, lightT, rubbleAcc, hazeAcc, t;
            public Vector3 cx; public float coreR, R; public int L; public double x; public bool implode;
        }
        readonly List<WS> live = new List<WS>(), spare = new List<WS>();
        public readonly Color tint;

        public CineWell(Fx fx, View view)
        {
            this.fx = fx; this.view = view;
            tint = S.Lin(SUB_LOOK["well"].tint);
            var go = new GameObject("well motes"); go.transform.SetParent(view.vfx.root, false);
            orbit = go.AddComponent<ParticleSystem>();
            orbit.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = orbit.main; main.playOnAwake = false; main.loop = true; main.maxParticles = buf.Length; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.simulationSpeed = 0;   // (they are placed here each frame; the system only draws them)
            var em = orbit.emission; em.enabled = false;
            var sh = orbit.shape; sh.enabled = false;
            var rr = go.GetComponent<ParticleSystemRenderer>();
            rr.sharedMaterial = FxPool.Mat(Templates.ParticleAdd, FxTex.Get("ember"), Hdr(Color.Lerp(tint, Color.white, 0.3f), 3.2f));
            rr.renderMode = ParticleSystemRenderMode.Stretch; rr.velocityScale = 0.035f; rr.lengthScale = 1.2f;
            rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; rr.receiveShadows = false; rr.sortMode = ParticleSystemSortMode.None;
            orbit.Play();
        }

        static Color Hdr(Color c, float k, float a = 1) { var o = c * k; o.a = a; return o; }

        public void Begin() { foreach (var s in live) s.seen = false; }
        public void End()
        {
            for (int i = live.Count - 1; i >= 0; i--) if (!live[i].seen) { spare.Add(live[i]); live.RemoveAt(i); }
            // the motes of every well, drawn in one go
            int k = 0;
            foreach (var s in live) k = Fill(s, k);
            orbit.SetParticles(buf, k);
        }
        public void Hide() { Begin(); End(); }

        WS Get(Well w)
        {
            foreach (var s in live) if (s.w == w) return s;
            WS n = spare.Count > 0 ? spare[spare.Count - 1] : new WS(); if (spare.Count > 0) spare.RemoveAt(spare.Count - 1);
            n.w = w; n.n = 0; n.pulseT = n.warpT = n.lightT = n.rubbleAcc = n.hazeAcc = n.t = 0; live.Add(n);
            return n;
        }

        // Each frame for each open well: its sim point (x, y), its core's radius, how far it has opened (0-1) and
        // whether it is imploding (its last moments)
        public void Draw(Well w, double x, double y, float coreR, float open, bool implode, float dt)
        {
            var s = Get(w); s.seen = true; s.t += dt;
            var V = view.vfx; int L = Mathf.Clamp((int)w.level, 0, 3); float R = (float)w.r * open;
            var c = S.W(x, y, 0.2f);
            // the motes: spawn up to the level's count, then move them
            int want = Mathf.Min(MAXO, Mathf.RoundToInt((40 + 22 * L + 20 + 18 * L) * Mathf.Max(0.4f, FxCfg.Amount)));
            while (s.n < want) { Respawn(s, s.n, coreR, R, L, s.n % 5 < 2); s.n++; }
            float pull = implode ? 4 : 1;
            for (int i = 0; i < s.n; i++)
            {
                float r = s.r[i];
                if (s.disk[i])
                {
                    float r0 = Mathf.Max(0.2f, coreR * 1.3f);
                    s.wa[i] = 5.5f * (1 + 0.2f * L) * Mathf.Pow(r0 / Mathf.Max(r0 * 0.5f, r), 1.5f); s.vr[i] = (0.25f + 0.1f * L) * pull * (implode ? 6 : 1);
                    s.a[i] += dt * s.wa[i]; s.r[i] = r - dt * s.vr[i];
                    if (s.r[i] < coreR * 1.05f) Respawn(s, i, coreR, R, L, true);
                }
                else
                {
                    float k = Mathf.Max(0.3f, R) / Mathf.Max(coreR, r);
                    s.wa[i] = (0.7f + 0.3f * L) * Mathf.Min(14, Mathf.Pow(k, 1.25f)); s.vr[i] = (1.2f + 0.6f * L) * Mathf.Min(5, Mathf.Pow(k, 0.7f)) * pull;
                    s.a[i] += dt * s.wa[i]; s.r[i] = r - dt * s.vr[i];
                    if (s.r[i] < coreR * 0.95f) Respawn(s, i, coreR, R, L, false);
                }
            }
            s.cx = c; s.coreR = coreR; s.R = R; s.L = L; s.x = x; s.implode = implode;

            // the inky shadow round the core, and the haze bending the picture toward it
            V.dark.Emit(c, Vector3.zero, coreR * (4.2f + 0.4f * L), 0.06f, new Color(0, 0, 0, 0.55f + 0.08f * L));
            if (FxCfg.Distortion)
            {
                s.hazeAcc += (8 + 7 * L) * dt;
                while (s.hazeAcc >= 1)
                {
                    s.hazeAcc -= 1;
                    float a = Random.value * Mathf.PI * 2, rr = coreR * (1.2f + Random.value * (0.8f + 0.3f * L));
                    V.hazeIn.Emit(c + S.Dir(x, Mathf.Cos(a) * rr, Mathf.Sin(a) * rr), Vector3.zero, coreR * (3.2f + 0.8f * L), 0.45f, new Color(1, 1, 1, 0.55f + 0.12f * L));
                }
            }
            // light bending: rings closing in from the edge of its pull, and the floor rippling in under it
            s.pulseT -= dt;
            double? floor = fx.FloorUnder(x, y, 2.5 + L);
            if (s.pulseT <= 0)
            {
                s.pulseT = (implode ? 0.12f : 0.55f - 0.1f * L);
                V.ringIn.Emit(c, Vector3.zero, R * 2.5f, 0.5f, Hdr(Color.Lerp(tint, Color.white, 0.5f), 1.4f + 0.3f * L, 0.5f + 0.1f * L));
                if (L >= 2) V.ringIn.Emit(c, Vector3.zero, coreR * 7, 0.3f, Hdr(Color.white, 2, 0.6f));
                if (floor != null) fx.GroundRing(x, floor.Value, Color.Lerp(tint, Color.white, 0.3f), R * 0.95f, coreR * 0.4f, 0.55f, 0.3f + 0.1f * L);
            }
            if (floor != null && FxCfg.Distortion)
            {
                s.warpT -= dt;
                if (s.warpT <= 0)
                {
                    s.warpT = 0.14f - 0.025f * L;
                    float sz = R * (1.1f + 0.25f * L);
                    V.warp.Emit3D(S.W(x, floor.Value + 0.04, 0), Vector3.zero, new Vector3(sz, sz, 1), 0.6f, new Color(1, 1, 1, 0.5f + 0.15f * L), new Vector3(Mathf.PI / 2, 0, Random.value * 6.3f), new Vector3(0, 0, 0.8f));
                }
            }
            // rubble torn up off the floor and drawn into it (level 2 a little, level 3 a lot)
            if (L >= 2 && FxCfg.Debris != "off")
            {
                s.rubbleAcc += (L == 2 ? 5 : 16) * FxCfg.Amount * dt;
                while (s.rubbleAcc >= 1)
                {
                    s.rubbleAcc -= 1;
                    double px = x + (Random.value * 2 - 1) * R * 0.85;
                    var g = fx.FloorUnder(px, y, 2.5 + L); if (g == null) continue;
                    var p = S.W(px, g.Value + 0.05, (Random.value - 0.5f) * 1.6f); float life = 0.7f + Random.value * 0.5f, sz = 0.06f + Random.value * 0.1f;
                    V.rubble.Emit3D(p, (c - p) / life + Vector3.up * 0.8f, new Vector3(sz, sz * 0.7f, sz), life, Color.white, Random.insideUnitSphere * 3, Random.insideUnitSphere * 9);
                    if (Random.value < 0.35f && FxCfg.Smoke != "off") V.smoke.Emit(p, (c - p) * 0.6f / life, 0.35f, 0.7f, new Color(0.7f, 0.68f, 0.65f, 0.35f), Random.value * 6.3f);
                }
            }
            // the accretion disc lights what is near it; at level 3 the ground rumbles
            s.lightT -= dt;
            if (s.lightT <= 0) { s.lightT = 0.12f; V.lights.Flash(c, Color.Lerp(tint, Color.white, 0.3f), (2.5f + 1.3f * L) * (implode ? 2 : 1), 3 + 1.2f * L, 0.2f); }
            if (L >= 2) view.ShakeAtLeast(L == 3 ? 0.1f : 0.05f);
        }

        void Respawn(WS s, int i, float coreR, float R, int L, bool disk)
        {
            s.disk[i] = disk; s.a[i] = Random.value * Mathf.PI * 2; s.wa[i] = s.vr[i] = 0; s.dz[i] = (Random.value - 0.5f) * (disk ? 0.08f : 0.6f);
            s.r[i] = disk ? coreR * (1.4f + Random.value * (1.6f + 0.3f * L)) : Mathf.Max(coreR * 2, R * (0.7f + Random.value * 0.35f));
        }

        // Writes one well's motes into the shared buffer (Unity space): the infalling ones in the play plane, the disc's
        // on the disc's tilted plane; each fades as the dark rim swallows it
        int Fill(WS s, int k)
        {
            var f = Level.Frame(s.x);
            Vector3 T = new Vector3((float)f.tx, 0, (float)f.tz), N = new Vector3((float)f.nx, 0, (float)f.nz), U = Vector3.up, D = (N * 0.92f + U * 0.38f).normalized;
            for (int i = 0; i < s.n && k < buf.Length; i++)
            {
                float r = s.r[i], a = s.a[i], ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                Vector3 B = s.disk[i] ? D : U;
                var p = s.cx + T * (r * ca) + B * (r * sa) + N * s.dz[i];
                // speed along the orbit and inward, for the stretch
                var v = (-T * sa + B * ca) * (r * s.wa[i]) - (T * ca + B * sa) * s.vr[i];
                float fade = Mathf.Clamp01((r - s.coreR) / Mathf.Max(0.05f, s.coreR * 0.8f)) * Mathf.Clamp01((s.R * 1.05f - r) / Mathf.Max(0.1f, s.R * 0.2f) + (s.disk[i] ? 1 : 0));
                var col = s.disk[i] || i % 3 == 0 ? new Color32(255, 250, 235, (byte)(255 * fade)) : new Color32(255, 210, 140, (byte)(210 * fade));
                buf[k].position = Th.P(p); buf[k].velocity = Th.P(v); buf[k].startSize = s.disk[i] ? 0.07f + 0.012f * s.L : 0.055f + 0.015f * s.L;
                buf[k].startColor = col; buf[k].remainingLifetime = 1; buf[k].startLifetime = 1; buf[k].rotation = 0;
                k++;
            }
            return k;
        }
    }
}
