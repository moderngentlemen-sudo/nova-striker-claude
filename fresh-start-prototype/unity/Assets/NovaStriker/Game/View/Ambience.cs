// A sense of movement in the open sky (the Skyport route): banners and flags that ripple in the breeze, clouds
// that drift with the wind (high ones far off, low banks rolling past below the deck), and sunlight that comes and
// goes through them: soft cloud shadows sweeping the deck (a scrolling cookie on the sun), the sun's light and glow
// rising and falling with the cover, and faint sunbeams when it breaks through. Presentation only.
using System.Collections.Generic;
using NovaStriker.Game.Three;
using NovaStriker.Sim;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Sprite = NovaStriker.Game.Three.Sprite;

namespace NovaStriker.Game
{
    public sealed class Ambience
    {
        static readonly Vector2 WIND = new Vector2(2.2f, 0.6f);   // m/s along the route (x) and back from the camera (the cookie's drift)
        const float COOKIE_SIZE = 70;                              // m across one tile of cloud shadow

        sealed class Cloth { public Mesh mesh; public Vector3[] rest, pos; public float w, h, phase, amp; }
        readonly List<Cloth> cloths = new List<Cloth>();
        readonly List<(TMesh s, float speed)> clouds = new List<(TMesh, float)>();
        readonly List<(TMesh m, float dx)> beams = new List<(TMesh, float)>();
        readonly Light sun; readonly TMesh sunGlow; readonly View view;
        TMat beamMat;
        float t, sky = 1, cover = 1;
        Vector2 cookieAt;
        UniversalAdditionalLightData sunData;

        public Ambience(View view, Light sun, TMesh sunGlow)
        {
            this.view = view; this.sun = sun; this.sunGlow = sunGlow;
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
            var c = new Cloth { mesh = mesh, rest = mesh.vertices, w = w, h = h, phase = S.Rnd() * 10, amp = amp };
            c.pos = (Vector3[])c.rest.Clone();
            cloths.Add(c);
        }

        public void Update(float dt, float camX)
        {
            t += dt;
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
            // clouds drift, wrapping round
            foreach (var (s, speed) in clouds)
            {
                s.position.x += speed * dt;
                if (s.position.x > 480) s.position.x -= 820;
            }
            // sunbeams: brightest as the sun breaks out, beside the camera
            float beam = sky * Mathf.Clamp01((cover - 0.6f) / 0.4f);
            beamMat.opacity = 0.16f * beam;
            foreach (var (m, dx) in beams) { m.visible = beam > 0.01f; m.position.x = camX + dx; }
            // cloth: a wave travelling out from the hanging edge, growing toward the free edge, with gusts
            float gust = 0.75f + 0.25f * Mathf.Sin(t * 0.7f) + 0.15f * Mathf.Sin(t * 1.9f);
            foreach (var C in cloths)
            {
                for (int i = 0; i < C.rest.Length; i++)
                {
                    var r = C.rest[i]; float s = Mathf.Clamp01((r.x + C.w / 2) / C.w), v = r.y / C.h;
                    float wave = Mathf.Sin(s * 5.5f - t * 6.5f + C.phase + v * 1.2f) * 0.6f + Mathf.Sin(s * 9f - t * 10f + C.phase * 1.7f) * 0.25f;
                    float d = C.amp * gust * 0.16f * Mathf.Pow(s, 1.2f) * wave;
                    C.pos[i] = new Vector3(r.x - Mathf.Abs(d) * 0.25f, r.y + 0.04f * s * Mathf.Sin(t * 3 + C.phase), r.z + d);
                }
                C.mesh.vertices = C.pos; C.mesh.RecalculateNormals(); C.mesh.RecalculateBounds();
            }
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
