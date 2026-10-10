// Volume-like lit cloud impostors (legacy settings key: volumetric), drawn instanced
// in one call. Every cloud lives behind the play area: it is placed from the camera's place on the path, at least
// 70 m back from the plane the action is in, along the path's direction, so it slides past behind the action as the
// wind and the camera move and can never drift into it. Far banks sit 180-420 m back; low banks roll below the deck.
// Only over the open sky of the Skyport route (Ambience's sky weight).
using System.Collections.Generic;
using NovaStriker.Game.Three;
using NovaStriker.Sim;
using UnityEngine;
using UnityEngine.Rendering;
using static NovaStriker.Sim.Cfg;

namespace NovaStriker.Game
{
    public sealed class CloudLayer
    {
        const float SPAN = 340, MIN_BACK = 70;
        sealed class Puff { public Vector3 off; public float sx, sy; }
        sealed class Cloud { public double x; public float d, h, speed; public List<Puff> puffs = new List<Puff>(); }
        readonly List<Cloud> clouds = new List<Cloud>();
        readonly Material mat; readonly Mesh quad;
        Matrix4x4[] mats, sorted; float[] dist; int[] order;
        readonly View view;

        public CloudLayer(View view)
        {
            this.view = view;
            mat = new Material(Templates.Cloud) { enableInstancing = true };
            mat.SetTexture("_MainTex", FxTex.Get("cloudpuff"));
            quad = Geo.Plane(1, 1);
            var rnd = new System.Random(77);
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            for (int i = 0; i < 40; i++)   // far banks
            {
                var c = new Cloud { x = -SPAN + i * (2 * SPAN / 40) + R(0, 10), d = R(180, 420), h = R(-6, 55), speed = R(1.2f, 3) };
                int n = (int)R(6, 13);
                for (int k = 0; k < n; k++) { float s = R(24, 52); c.puffs.Add(new Puff { off = new Vector3(R(-30, 30), R(-5, 8) * (1 - k / (float)n), R(0, 18)), sx = s * R(1.1f, 1.5f), sy = s }); }
                clouds.Add(c);
            }
            for (int i = 0; i < 16; i++)   // low banks below the deck
            {
                var c = new Cloud { x = -SPAN + i * (2 * SPAN / 16) + R(0, 20), d = R(MIN_BACK, 200), h = R(-42, -26), speed = R(3, 5.5f) };
                int n = (int)R(5, 9);
                for (int k = 0; k < n; k++) { float s = R(16, 30); c.puffs.Add(new Puff { off = new Vector3(R(-26, 26), R(-2, 3), R(0, 14)), sx = s * R(1.8f, 2.4f), sy = s * 0.75f }); }
                clouds.Add(c);
            }
            int total = 0; foreach (var c in clouds) total += c.puffs.Count;
            mats = new Matrix4x4[total]; sorted = new Matrix4x4[total]; dist = new float[total]; order = new int[total];
        }

        public void Update(float dt, double camX, float sky)
        {
            Shader.SetGlobalVector("_NovaSkyTop", view.SkyTop); Shader.SetGlobalVector("_NovaSkyBot", view.SkyBot);
            if (SETTINGS.clouds != "volumetric" || sky < 0.02f) return;
            var f = Level.Frame(camX);
            var P = new Vector3((float)f.px, 0, (float)f.pz); var T = new Vector3((float)f.tx, 0, (float)f.tz).normalized; var Nn = new Vector3((float)f.nx, 0, (float)f.nz).normalized;
            var camU = view.camera.transform.position;
            // Include the visible curved route and the entire billboard radius in the exclusion envelope.
            float envelope = 0;
            for (double x = camX - 80; x <= camX + 80; x += 2)
                envelope = Mathf.Max(envelope, Vector3.Dot(P - S.W(x, 0, -2.8), Nn));
            int n = 0;
            foreach (var c in clouds)
            {
                c.x += c.speed * dt;
                c.x = camX + ((c.x - camX + SPAN) % (2 * SPAN) + 2 * SPAN) % (2 * SPAN) - SPAN;
                float u = (float)(c.x - camX);
                foreach (var p in c.puffs)
                {
                    float back = Mathf.Max(MIN_BACK + envelope + 0.5f * Mathf.Sqrt(p.sx * p.sx + p.sy * p.sy), c.d + p.off.z);   // (never closer than MIN_BACK behind the play plane)
                    var three = P + T * (u + p.off.x) + Vector3.up * (c.h + p.off.y) - Nn * back;
                    var w = Th.P(three);
                    mats[n] = Matrix4x4.TRS(w, Quaternion.identity, new Vector3(p.sx, p.sy, 1));
                    dist[n] = (w - camU).sqrMagnitude; order[n] = n; n++;
                }
            }
            // back to front, so the nearer puffs blend over the farther
            System.Array.Sort(dist, order, 0, n);
            for (int i = 0; i < n; i++) sorted[i] = mats[order[n - 1 - i]];
            mat.SetFloat("_Opacity", 0.92f * sky);
            var rp = new RenderParams(mat) { shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false, worldBounds = new Bounds(camU, Vector3.one * 2400) };
            if (!SystemInfo.supportsInstancing) { for (int i = 0; i < n; i++) Graphics.DrawMesh(quad, sorted[i], mat, 8); return; }
            for (int start = 0; start < n; start += 500)
            {
                int count = Mathf.Min(500, n - start);
                var bounds = new Bounds(sorted[start].GetColumn(3), Vector3.zero);
                for (int i = start; i < start + count; i++)
                {
                    float radius = Mathf.Sqrt(sorted[i].m00 * sorted[i].m00 + sorted[i].m11 * sorted[i].m11) * 0.5f;
                    bounds.Encapsulate(new Bounds(sorted[i].GetColumn(3), Vector3.one * (radius * 2)));
                }
                rp.worldBounds = bounds; rp.layer = 8;
                Graphics.RenderMeshInstanced(rp, quad, 0, sorted, count, start);
            }
        }
    }
}
