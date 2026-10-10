// Gulls (Settings › Effects › Birds: Realistic): white birds with grey wings and dark tips (Art/Blender/build_bird.py,
// Resources/NovaStriker/Models/bird.json), drawn instanced in one call with their wings beating in the shader
// (Bird.shader). Three flocks wheel far behind the play area (60-200 m back from it, placed from the camera's place on
// the path, as the clouds are). Each bird glides most of the time and flaps in bursts, banks into its turns, keeps
// loosely with its flock, and they all scatter, climbing and beating hard, when something big goes off.
using NovaStriker.Game.Three;
using NovaStriker.Sim;
using UnityEngine;
using UnityEngine.Rendering;
using static NovaStriker.Sim.Cfg;

namespace NovaStriker.Game
{
    public sealed class Birds
    {
        [System.Serializable] sealed class BirdJson { public float[] p, n, c, uv2; public int[] i; }
        sealed class Bird { public Vector3 off, prev, dir = Vector3.right; public float phase, amp, timer, roll, scale; public bool flapping, initialized; }
        sealed class Flock { public float a, speed, ru, rd, cu, ch, cd, startle; public Bird[] birds; }
        readonly Flock[] flocks = new Flock[3];
        readonly Mesh mesh; readonly Material mat;
        readonly Matrix4x4[] mats = new Matrix4x4[40]; readonly Vector4[] flaps = new Vector4[40];
        readonly MaterialPropertyBlock mpb = new MaterialPropertyBlock();
        readonly View view;
        float t;

        public Birds(View view)
        {
            this.view = view;
            mesh = LoadMesh();
            mat = new Material(Templates.Bird) { enableInstancing = true };
            var rnd = new System.Random(31);
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            for (int f = 0; f < flocks.Length; f++)
            {
                var F = flocks[f] = new Flock { a = R(0, 6.3f), speed = R(0.05f, 0.09f), ru = R(28, 50), rd = R(10, 22), cu = -40 + f * 40 + R(-10, 10), ch = R(16, 34), cd = 80 + f * 35 };
                F.birds = new Bird[7 + f * 2];
                for (int k = 0; k < F.birds.Length; k++)
                    F.birds[k] = new Bird { off = new Vector3(R(-7, 7), R(-2, 2), R(-5, 5)), phase = R(0, 6.3f), timer = R(0, 3), flapping = rnd.NextDouble() < 0.4, scale = R(0.9f, 1.05f) };
            }
        }

        static Mesh LoadMesh()
        {
            var src = Resources.Load<TextAsset>("NovaStriker/Models/bird");
            if (src == null) return null;
            BirdJson b;
            try { b = JsonUtility.FromJson<BirdJson>(src.text); } catch { return null; }
            if (b?.p == null || b.n == null || b.c == null || b.uv2 == null || b.i == null || b.p.Length % 3 != 0 || b.n.Length != b.p.Length || b.c.Length != b.p.Length || b.uv2.Length != b.p.Length / 3 * 2 || b.i.Length % 3 != 0) return null;
            foreach (var index in b.i) if (index < 0 || index >= b.p.Length / 3) return null;
            int nv = b.p.Length / 3;
            var pos = new Vector3[nv]; var nrm = new Vector3[nv]; var col = new Color[nv]; var uv2 = new Vector2[nv];
            for (int k = 0; k < nv; k++)
            {
                pos[k] = new Vector3(b.p[3 * k], b.p[3 * k + 1], -b.p[3 * k + 2]);   // (three.js to Unity: z mirrored)
                nrm[k] = new Vector3(b.n[3 * k], b.n[3 * k + 1], -b.n[3 * k + 2]);
                col[k] = new Color(b.c[3 * k], b.c[3 * k + 1], b.c[3 * k + 2]).linear; uv2[k] = new Vector2(b.uv2[2 * k], b.uv2[2 * k + 1]);
            }
            var tri = new int[b.i.Length];
            for (int k = 0; k < tri.Length; k += 3) { tri[k] = b.i[k]; tri[k + 1] = b.i[k + 2]; tri[k + 2] = b.i[k + 1]; }   // (and the winding)
            var m = new Mesh { name = "gull" };
            m.vertices = pos; m.normals = nrm; m.colors = col; m.SetUVs(1, uv2); m.triangles = tri; m.RecalculateBounds(); var bnd = m.bounds; bnd.Expand(new Vector3(0.2f, 1.5f, 0.2f)); m.bounds = bnd;
            return m;
        }

        public void Startle(float k) { foreach (var F in flocks) F.startle = Mathf.Min(1.5f, F.startle + k); }

        public void Update(float dt, double camX, float sky)
        {
            if (mesh == null || SETTINGS.birds != "realistic" || sky < 0.05f) return;
            t += dt;
            var f = Level.Frame(camX);
            var P = new Vector3((float)f.px, 0, (float)f.pz); var T = new Vector3((float)f.tx, 0, (float)f.tz).normalized; var Nn = new Vector3((float)f.nx, 0, (float)f.nz).normalized;
            float envelope = 0;
            for (double x = camX - 80; x <= camX + 80; x += 2)
                envelope = Mathf.Max(envelope, Vector3.Dot(P - S.W(x, 0, -2.8), Nn));
            int n = 0;
            foreach (var F in flocks)
            {
                F.startle = Mathf.Max(0, F.startle - dt * 0.35f);
                float st = F.startle;
                F.a += F.speed * (1 + st * 1.6f) * dt;
                float u = F.cu + Mathf.Cos(F.a) * F.ru, d = Mathf.Max(60, F.cd + Mathf.Sin(F.a) * F.rd), h = F.ch + Mathf.Sin(F.a * 2.3f) * 2 + st * 8;
                foreach (var B in F.birds)
                {
                    var o = B.off * (1 + st * 1.4f) + new Vector3(Mathf.Sin(t * 0.5f + B.phase), Mathf.Sin(t * 0.7f + B.phase * 1.7f) * 0.6f, Mathf.Cos(t * 0.4f + B.phase)) * 0.9f;
                    var pos = P + T * (u + o.x) + Vector3.up * (h + o.y) - Nn * Mathf.Max(72 + envelope + mesh.bounds.extents.magnitude * B.scale, d + o.z);
                    // heading and bank from its motion
                    // Heading uses flock-local motion, so camera relocation cannot spin the gull.
                    var local = new Vector3(u + o.x, h + o.y, -d - o.z);
                    var delta = B.initialized ? local - B.prev : Vector3.zero;
                    B.prev = local; B.initialized = true;
                    var v = dt > 0 ? (T * delta.x + Vector3.up * delta.y + Nn * delta.z) / dt : Vector3.zero;
                    if (v.sqrMagnitude > 0.01f && v.sqrMagnitude < 1e4f)
                    {
                        var nd = Vector3.Slerp(B.dir, v.normalized, 1 - Mathf.Exp(-dt * 4));
                        float turn = Vector3.SignedAngle(new Vector3(B.dir.x, 0, B.dir.z), new Vector3(nd.x, 0, nd.z), Vector3.up) / Mathf.Max(1e-3f, dt);
                        B.roll += (Mathf.Clamp(-turn * 0.012f, -0.7f, 0.7f) - B.roll) * (1 - Mathf.Exp(-dt * 3));
                        B.dir = nd;
                    }
                    // glide, then a burst of flapping (always flapping hard when startled)
                    B.timer -= dt;
                    if (B.timer <= 0) { B.flapping = !B.flapping; B.timer = B.flapping ? 0.9f + Random.value * 1.6f : 1.8f + Random.value * 3; }
                    bool flap = B.flapping || st > 0.2f;
                    B.amp += ((flap ? 0.55f + st * 0.2f : 0.06f) - B.amp) * (1 - Mathf.Exp(-dt * 5));
                    B.phase += dt * (flap ? 8 + st * 7 : 2);
                    float angle = B.amp * Mathf.Sin(B.phase) + (flap ? 0 : 0.12f);   // (wings held a touch up in a glide)
                    var dirU = new Vector3(B.dir.x, B.dir.y, -B.dir.z);
                    if (dirU.sqrMagnitude < 1e-4f) dirU = Vector3.right;
                    var rot = Quaternion.LookRotation(dirU, Vector3.up) * Quaternion.Euler(0, 0, B.roll * Mathf.Rad2Deg);
                    mats[n] = Matrix4x4.TRS(Th.P(pos), rot, Vector3.one * B.scale);
                    flaps[n] = new Vector4(angle, 0, 0, 0);
                    n++;
                }
            }
            mpb.SetVectorArray("_Flap", flaps);
            var rp = new RenderParams(mat) { matProps = mpb, shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false, worldBounds = new Bounds(view.camera.transform.position, Vector3.one * 1200) };
            rp.layer = 8;
            if (SystemInfo.supportsInstancing) Graphics.RenderMeshInstanced(rp, mesh, 0, mats, n);
            else for (int i = 0; i < n; i++) { mpb.SetVector("_Flap", flaps[i]); Graphics.DrawMesh(mesh, mats[i], mat, 8, null, 0, mpb); }
        }
    }
}
