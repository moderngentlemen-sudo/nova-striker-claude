// A sense of movement in the open sky (the Skyport route): banners and flags that ripple in the breeze, clouds
// that drift with the wind (high ones far off, low banks rolling past below the deck), and sunlight that comes and
// goes through them: soft cloud shadows sweeping the deck (a scrolling cookie on the sun), the sun's light and glow
// rising and falling with the cover, and faint sunbeams when it breaks through; flecks of light drifting in the
// sun, and flocks of birds wheeling far off that scatter at an explosion. Presentation only.
using System.Collections.Generic;
using NovaStriker.Game.Three;
using NovaStriker.Sim;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Sprite = NovaStriker.Game.Three.Sprite;
using static NovaStriker.Sim.Cfg;

namespace NovaStriker.Game
{
    public sealed class Ambience
    {
        static readonly Vector2 WIND = new Vector2(2.2f, 0.6f);   // m/s along the route (x) and back from the camera (the cookie's drift)
        const float COOKIE_SIZE = 70;                              // m across one tile of cloud shadow

        sealed class Cloth { public Mesh mesh; public Vector3[] rest, pos; public float w, h, phase, amp, gust; public Vector3 at; }
        readonly List<Cloth> cloths = new List<Cloth>();
        readonly List<(TMesh s, float speed)> clouds = new List<(TMesh, float)>();
        readonly List<(TMesh m, float dx)> beams = new List<(TMesh, float)>();
        readonly Light sun; readonly TMesh sunGlow; readonly View view;
        TMat beamMat;
        float t, sky = 1, cover = 1;
        public float Sky => sky; public float Cover => cover;   // (the open sky's weight, and 1 when the sun is out: PostFx's flare)
        Vector2 cookieAt;
        UniversalAdditionalLightData sunData;

        // light motes and birds
        const int MOTES = 150, FLOCKS = 3, BIRDS = 9;
        DynMesh motes, birds;
        readonly Vector3[] mote = new Vector3[MOTES]; readonly float[] moteK = new float[MOTES];
        sealed class Flock { public float a, speed, rx, rz, cy, cz, dx, startle; public Vector3[] off = new Vector3[BIRDS]; public float[] flap = new float[BIRDS]; }
        readonly Flock[] flocks = new Flock[FLOCKS];
        readonly Camera cam;

        CloudLayer cloudLayer; Birds gulls;

        public Ambience(View view, Light sun, TMesh sunGlow)
        {
            this.view = view; this.sun = sun; this.sunGlow = sunGlow; cam = view.camera;
            cloudLayer = new CloudLayer(view); gulls = new Birds(view);
            // flecks of light in the air between the deck and the terrace, catching the sun
            var quads = new int[MOTES * 6];
            for (int i = 0; i < MOTES; i++) { int v = i * 4, k = i * 6; quads[k] = v; quads[k + 1] = v + 2; quads[k + 2] = v + 1; quads[k + 3] = v + 1; quads[k + 4] = v + 2; quads[k + 5] = v + 3; }
            var mm = new TMat(TMat.Kind.Basic) { map = view.fx.tex.glow, vertexColors = true, transparent = true, depthWrite = false, blending = Blending.Additive, side = Side.Double };
            motes = new DynMesh(MOTES * 4, quads, mm, true, true, "motes"); view.scene.add(motes.obj);
            for (int i = 0; i < MOTES; i++)
            {
                mote[i] = new Vector3(-20 + S.Rnd() * 40, 0.4f + S.Rnd() * 8, -11 + S.Rnd() * 14); moteK[i] = S.Rnd() * 10;
                int v = i * 4; motes.uv[v] = new Vector2(0, 0); motes.uv[v + 1] = new Vector2(1, 0); motes.uv[v + 2] = new Vector2(0, 1); motes.uv[v + 3] = new Vector2(1, 1);
            }
            // birds: small dark silhouettes, two wings each, in loose flocks circling far out over the city
            var bt = new int[FLOCKS * BIRDS * 6];
            for (int i = 0; i < FLOCKS * BIRDS; i++) { int v = i * 4, k = i * 6; bt[k] = v; bt[k + 1] = v + 1; bt[k + 2] = v + 2; bt[k + 3] = v; bt[k + 4] = v + 3; bt[k + 5] = v + 1; }
            var bm = new TMat(TMat.Kind.Basic) { colorHex = 0x55657a, side = Side.Double };
            birds = new DynMesh(FLOCKS * BIRDS * 4, bt, bm, false, false, "birds"); view.scene.add(birds.obj);
            for (int f = 0; f < FLOCKS; f++)
            {
                var F = flocks[f] = new Flock { a = S.Rnd() * 6.3f, speed = 0.06f + S.Rnd() * 0.05f, rx = 30 + S.Rnd() * 25, rz = 12 + S.Rnd() * 10, cy = 18 + f * 7 + S.Rnd() * 6, cz = -55 - f * 22, dx = -30 + f * 30 };
                for (int k = 0; k < BIRDS; k++) { F.off[k] = new Vector3((S.Rnd() - 0.5f) * 8, (S.Rnd() - 0.5f) * 2.5f, (S.Rnd() - 0.5f) * 6); F.flap[k] = S.Rnd() * 6.3f; }
            }
            // cloud shadows on the deck: a soft tileable pattern the sun casts, scrolled by the wind
            sun.cookie = CloudCookie(256);
            sunData = sun.GetUniversalAdditionalLightData();
            sunData.lightCookieSize = new Vector2(COOKIE_SIZE, COOKIE_SIZE);
            // low cloud banks below the deck, rolling past faster than the far ones
            var lowMat = TMat.Sprite(view.fx.tex.glow, 0xf4f9fd); lowMat.opacity = 0.42f; lowMat.depthWrite = false;
            for (int i = 0; i < 18; i++)
            {
                var s = Sprite.Make(lowMat);
                s.position.set(-200 + S.Rnd() * 520, -24 + S.Rnd() * 12, -30 - S.Rnd() * 90);
                float k = 18 + S.Rnd() * 26; s.scale.set(k * 2.2f, k * 0.8f, 1); view.scene.add(s);
                clouds.Add((s, 3.5f + S.Rnd() * 2.5f));
            }
            // sunbeams: long soft shafts slanting down from the sun's side of the sky, kept beside the camera
            beamMat = new TMat(TMat.Kind.Basic) { colorHex = 0xfff4dc, map = BeamTexture(), transparent = true, opacity = 0, blending = Blending.Additive, depthWrite = false, side = Side.Double, fog = false };
            for (int i = 0; i < 6; i++)
            {
                var m = new TMesh(Geo.Plane(1, 1), beamMat) { cast = false, receive = false, noOutline = true };
                float w = 6 + S.Rnd() * 10;
                m.scale.set(w, 150, 1); m.rotation.z = -0.55f - S.Rnd() * 0.12f;
                m.position.set(0, 40, -90 - i * 14); view.scene.add(m);
                beams.Add((m, -60 + i * 22 + S.Rnd() * 8));
            }
        }

        // The far clouds the backdrop made: they drift too
        public void AddCloud(TMesh s) => clouds.Add((s, 1.2f + S.Rnd() * 1.8f));

        // A banner or flag: a cloth `w` wide and `h` tall hung by its left edge at (x, y, z) (three.js space), which
        // the breeze ripples from that edge outward
        public void Banner(TMat mat, float x, float y, float z, float w, float h, float amp = 1)
        {
            var mesh = Geo.Copy(Geo.Plane(w, h, 10, 8));
            var m = new TMesh(mesh, mat) { cast = true, receive = true };
            m.position.set(x + w / 2, y, z); view.scene.add(m);
            var c = new Cloth { mesh = mesh, rest = mesh.vertices, w = w, h = h, phase = S.Rnd() * 10, amp = amp, at = new Vector3(x + w / 2, y, z) };
            c.pos = (Vector3[])c.rest.Clone();
            cloths.Add(c);
        }

        // Someone dashed past a three.js point: the cloths within 3 m of it whip for a moment
        public void Gust(Vector3 at, float k) { foreach (var C in cloths) if ((C.at - at).sqrMagnitude < 9 + C.w * C.w * 0.25f) C.gust = Mathf.Max(C.gust, k); }

        // Something big went off: the nearest flocks scatter, climbing and beating their wings faster for a few seconds
        public void Startle(float strength = 1) { foreach (var F in flocks) F.startle = Mathf.Min(1.5f, F.startle + strength); gulls.Startle(strength); }

        public void Update(float dt, float camX)
        {
            t += dt;
            UpdateMotes(dt, camX); UpdateBirds(dt, camX);
            cloudLayer.Update(dt, camX, sky); gulls.Update(dt, camX, sky);
            // the open sky belongs to the Skyport route; elsewhere the effects fade away
            float want = Level.RouteAt(camX).id == "skyport" ? 1 : 0;
            sky += (want - sky) * (1 - Mathf.Exp(-dt * 1.5f));
            // the cloud cover over the sun: slow and uneven, now and then breaking right through
            float c = 0.5f + 0.5f * (0.55f * Mathf.Sin(t * 0.11f) + 0.3f * Mathf.Sin(t * 0.27f + 1.3f) + 0.15f * Mathf.Sin(t * 0.61f + 4.1f));
            cover = Mathf.SmoothStep(0, 1, c);                       // 1: the sun is out
            float lit = Mathf.Lerp(1, 0.78f + 0.22f * cover, sky);
            sun.intensity *= lit;
            if (sunGlow != null) sunGlow.material.opacity = Mathf.Lerp(1, 0.55f + 0.45f * cover, sky);
            // cloud shadows drift with the wind (and fade out off the open sky)
            cookieAt += WIND * dt;
            sunData.lightCookieOffset = cookieAt;
            var ck = sky > 0.02f ? cookieTex : null;
            if (sun.cookie != ck) sun.cookie = ck;
            // clouds drift, wrapping round (the classic sprites; the volumetric clouds are CloudLayer's). Either way none
            // may come nearer than 70 m behind the plane the action is in
            bool classic = SETTINGS.clouds == "classic";
            var fr = Level.Frame(camX); var fp = new Vector3((float)fr.px, 0, (float)fr.pz); var fn = new Vector3((float)fr.nx, 0, (float)fr.nz);
            foreach (var (s, speed) in clouds)
            {
                s.visible = classic;
                if (!classic) continue;
                s.position.x += speed * dt;
                if (s.position.x > 480) s.position.x -= 820;
                var pos = s.position.v; float ahead = Vector3.Dot(new Vector3(pos.x, 0, pos.z) - fp, fn);
                if (ahead > -70) s.position.copy(pos - fn * (ahead + 70));
            }
            // sunbeams: brightest as the sun breaks out, beside the camera
            float beam = sky * Mathf.Clamp01((cover - 0.6f) / 0.4f);
            beamMat.opacity = 0.16f * beam;
            foreach (var (m, dx) in beams) { m.visible = beam > 0.01f; m.position.x = camX + dx; }
            // cloth: a wave travelling out from the hanging edge, growing toward the free edge, with gusts
            float gust = 0.75f + 0.25f * Mathf.Sin(t * 0.7f) + 0.15f * Mathf.Sin(t * 1.9f);
            foreach (var C in cloths)
            {
                C.gust = Mathf.Max(0, C.gust - dt * 1.2f);
                float whip = 1 + 2.2f * C.gust;
                for (int i = 0; i < C.rest.Length; i++)
                {
                    var r = C.rest[i]; float s = Mathf.Clamp01((r.x + C.w / 2) / C.w), v = r.y / C.h;
                    float wave = Mathf.Sin(s * 5.5f - t * 6.5f + C.phase + v * 1.2f) * 0.6f + Mathf.Sin(s * 9f - t * 10f + C.phase * 1.7f) * 0.25f;
                    float d = C.amp * gust * whip * 0.16f * Mathf.Pow(s, 1.2f) * wave;
                    C.pos[i] = new Vector3(r.x - Mathf.Abs(d) * 0.25f, r.y + 0.04f * s * Mathf.Sin(t * 3 + C.phase), r.z + d);
                }
                C.mesh.vertices = C.pos; C.mesh.RecalculateNormals(); C.mesh.RecalculateBounds();
            }
        }

        void UpdateMotes(float dt, float camX)
        {
            // they show in sunlight only: brightest as the sun breaks out
            float glow = sky * (0.12f + 0.88f * cover);
            motes.visible = glow > 0.01f;
            if (!motes.visible) return;
            Vector3 right = cam.transform.right, up = cam.transform.up; right.z = -right.z; up.z = -up.z;   // (into three.js space)
            for (int i = 0; i < MOTES; i++)
            {
                var p = mote[i];
                p.x += (WIND.x * 0.35f + Mathf.Sin(t * 0.7f + moteK[i]) * 0.15f) * dt; p.y += Mathf.Sin(t * 0.9f + moteK[i] * 1.3f) * 0.12f * dt;
                if (p.x > camX + 22) p.x -= 44; if (p.x < camX - 22) p.x += 44;
                mote[i] = p;
                float tw = 0.5f + 0.5f * Mathf.Sin(t * (1.5f + moteK[i] % 1.7f) + moteK[i]), s = 0.035f + 0.03f * (moteK[i] % 1);
                var c = new Color(1f, 0.93f, 0.78f) * (glow * tw * 0.9f);
                int v = i * 4;
                motes.Set(v, p - right * s - up * s); motes.Set(v + 1, p + right * s - up * s); motes.Set(v + 2, p - right * s + up * s); motes.Set(v + 3, p + right * s + up * s);
                for (int k = 0; k < 4; k++) motes.col[v + k] = c;
            }
            motes.Upload();
        }

        void UpdateBirds(float dt, float camX)
        {
            birds.visible = sky > 0.05f && SETTINGS.birds == "classic";
            if (!birds.visible) return;
            for (int f = 0; f < FLOCKS; f++)
            {
                var F = flocks[f]; F.startle = Mathf.Max(0, F.startle - dt * 0.4f);
                float st = F.startle;
                F.a += F.speed * (1 + st * 1.5f) * dt;
                var centre = new Vector3(camX + F.dx + Mathf.Cos(F.a) * F.rx, F.cy + Mathf.Sin(F.a * 2.3f) * 2 + st * 6, F.cz + Mathf.Sin(F.a) * F.rz);
                var heading = new Vector3(-Mathf.Sin(F.a) * F.rx, 0, Mathf.Cos(F.a) * F.rz).normalized;
                var wing = Vector3.Cross(Vector3.up, heading).normalized;
                for (int k = 0; k < BIRDS; k++)
                {
                    F.flap[k] += dt * (9 + st * 10 + k % 3);
                    var o = F.off[k] * (1 + st * 1.6f) + new Vector3(Mathf.Sin(t * 0.5f + k), Mathf.Sin(t * 0.7f + k * 1.7f) * 0.6f, Mathf.Cos(t * 0.4f + k)) * 0.8f;
                    var c = centre + o; float span = 0.8f, lift = Mathf.Sin(F.flap[k]) * 0.35f;
                    int v = (f * BIRDS + k) * 4;
                    birds.Set(v, c + heading * 0.18f); birds.Set(v + 1, c - heading * 0.25f);
                    birds.Set(v + 2, c + wing * span + Vector3.up * lift - heading * 0.12f); birds.Set(v + 3, c - wing * span + Vector3.up * lift - heading * 0.12f);
                }
            }
            birds.Upload();
        }

        Texture2D cookieTex;
        // Soft blotches of shade (about 0.6) in open light (1), tileable
        Texture2D CloudCookie(int n)
        {
            var px = new Color32[n * n];
            float Noise(float x, float y, int period)
            {
                int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y); float fx = x - x0, fy = y - y0;
                fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
                float H(int a, int b) { a = ((a % period) + period) % period; b = ((b % period) + period) % period; uint h = (uint)(a * 374761393 + b * 668265263); h = (h ^ (h >> 13)) * 1274126177; return (h & 0xffff) / 65535f; }
                return Mathf.Lerp(Mathf.Lerp(H(x0, y0), H(x0 + 1, y0), fx), Mathf.Lerp(H(x0, y0 + 1), H(x0 + 1, y0 + 1), fx), fy);
            }
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = x / (float)n, v = y / (float)n, f = 0, a = 0.55f;
                    for (int o = 0, p = 4; o < 4; o++, p *= 2, a *= 0.5f) f += a * Noise(u * p, v * p, p);
                    float shade = Mathf.SmoothStep(0, 1, Mathf.Clamp01((f - 0.42f) / 0.22f));
                    byte b = (byte)Mathf.RoundToInt(255 * (1 - 0.4f * shade));
                    px[y * n + x] = new Color32(b, b, b, 255);
                }
            cookieTex = new Texture2D(n, n, TextureFormat.RGBA32, true, true) { wrapMode = TextureWrapMode.Repeat, name = "cloud-shadows" };
            cookieTex.SetPixels32(px); cookieTex.Apply(true, true);
            return cookieTex;
        }

        // A shaft of light: soft across, fading toward both ends
        static Texture2D BeamTexture()
        {
            const int w = 32, h = 128; var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w * 2 - 1, v = (y + 0.5f) / h;
                    float a = Mathf.Exp(-u * u * 4) * Mathf.Sin(v * Mathf.PI) * (0.4f + 0.6f * v);
                    px[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255 * a));
                }
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false, false) { wrapMode = TextureWrapMode.Clamp, name = "sunbeam" };
            t.SetPixels32(px); t.Apply(false, true);
            return t;
        }
    }
}
