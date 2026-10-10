// Weather and atmosphere per zone (Settings › Graphics › Weather), as particle volumes that follow the camera so the
// cost stays the same everywhere: rain with splashes, lightning, storm clouds and mist on the Storm Spire; wind
// streaks and distant airships and pods with blinking lights over the Skyline Relay; steam, rising embers and heat
// haze in the Helix Foundry; drips, low haze, flickering neon and a damp sheen in the Undercity; sweeping
// floodlights and holo dust in the Concourse Lock. The Movement Gym keeps its ambience. Also the world reacting:
// deck lights that stutter when something heavy lands, and flags that whip when someone dashes past.
// Presentation only.
using System.Collections.Generic;
using NovaStriker.Game.Three;
using NovaStriker.Sim;
using UnityEngine;
using Sprite = NovaStriker.Game.Three.Sprite;
using static NovaStriker.Sim.Cfg;

namespace NovaStriker.Game
{
    public sealed class Weather
    {
        readonly View view; readonly Vfx V; readonly Transform root;
        readonly FxLayer rain, drips;
        string zone = "";
        float t;
        readonly Dictionary<string, float> acc = new Dictionary<string, float>();

        // lightning: a sky-wide flash (a second sun) and a jagged bolt far behind the play area
        readonly Light bolt; readonly LineRenderer boltLine;
        float boltT = -1, nextBolt = 4;
        // sky traffic over the Skyline Relay
        sealed class Craft { public TObj o; public TMesh[] blink; public float u, d, h, speed, phase; }
        readonly List<Craft> craft = new List<Craft>();
        // the Concourse's floodlights: a spot light and a soft visible cone each
        sealed class Flood { public Light l; public Transform cone; public float phase, du; }
        readonly List<Flood> floods = new List<Flood>();
        // deck lights stutter after a heavy landing; neon flickers in the Undercity
        float lampT; bool lampsDirty, neonDirty, wet;

        public Weather(View view)
        {
            this.view = view; V = view.vfx;
            root = new GameObject("Weather").transform; root.SetParent(view.scene.go.transform, false);
            rain = Drops("rain", new Color(0.78f, 0.85f, 0.95f, 0.38f), 2400, 0.25f, 0.04f);
            drips = Drops("drips", new Color(0.7f, 0.86f, 1f, 0.6f), 300, 1f, 0.03f);

            var bgo = new GameObject("lightning"); bgo.transform.SetParent(root, false);
            bolt = bgo.AddComponent<Light>(); bolt.type = LightType.Directional; bolt.color = new Color(0.78f, 0.86f, 1f);
            bolt.intensity = 0; bolt.shadows = LightShadows.None; bolt.enabled = false;
            bgo.transform.rotation = Quaternion.Euler(62, -150, 0);
            var lgo = new GameObject("lightning bolt"); lgo.transform.SetParent(root, false);
            boltLine = lgo.AddComponent<LineRenderer>();
            boltLine.sharedMaterial = FxPool.Mat(Templates.ParticleAdd, FxTex.Get("glow"), new Color(0.85f, 0.9f, 1f));
            boltLine.positionCount = 0; boltLine.widthMultiplier = 0.9f; boltLine.numCornerVertices = 0;
            boltLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; boltLine.receiveShadows = false; boltLine.enabled = false;

            BuildCraft(); BuildFloods();
        }

        // A falling-drop layer: stretched streaks that die where they hit the level and throw up a few droplets there
        FxLayer Drops(string name, Color tint, int max, float gravity, float width)
        {
            var white = Color.white;
            var l = FxPool.Layer(new FxPool.Spec
            {
                name = name, mat = FxPool.Mat(Templates.ParticleAdd, FxTex.Get("streak"), white), max = max, gravity = gravity,
                stretch = true, stretchK = 0.045f, collide = true, bounce = 0, fade = FxPool.Fade(white, white, 0, 1, 1, 0.08f),
                extra = ps => { var c = ps.collision; c.lifetimeLoss = 1; c.dampen = 1; c.radiusScale = 0.2f; c.sendCollisionMessages = true; }
            }, root);
            var sgo = new GameObject(name + " splash"); sgo.transform.SetParent(l.ps.transform, false);
            var sp = sgo.AddComponent<ParticleSystem>();
            sp.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = sp.main; m.playOnAwake = false; m.loop = false; m.duration = 0.1f; m.maxParticles = max;
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.gravityModifier = 1;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.32f); m.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.2f);
            m.startSize = new ParticleSystem.MinMaxCurve(width * 0.8f, width * 1.4f); m.startColor = tint;
            var em = sp.emission; em.rateOverTime = 0; em.burstCount = 0; // every birth goes through WeatherSplash
            var sh = sp.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 38; sh.radius = 0.04f; sh.rotation = new Vector3(-90, 0, 0);
            var co = sp.colorOverLifetime; co.enabled = true; co.color = new ParticleSystem.MinMaxGradient(FxPool.Fade(white, white, 1, 0.8f, 0, 0.4f));
            var r = sgo.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = FxPool.Mat(Templates.ParticleAdd, FxTex.Get("spark"), white);
            r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.04f; r.lengthScale = 1.5f;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            ParticleBudget.Register(sp);
            var callback = l.ps.gameObject.AddComponent<WeatherSplash>(); callback.source = l.ps; callback.splash = sp;
            sp.Play();
            l.ps.Play();
            dropTint[name] = tint;
            return l;
        }
        readonly Dictionary<string, Color> dropTint = new Dictionary<string, Color>();

        // Airships (a long hull, a gondola, fins) and small pods, far behind the play area, each with blinking lights
        void BuildCraft()
        {
            var hull = TMat.Std(0xdfe4ea, 0.45f, 0.2f); var dark = TMat.Std(0x3d4f66, 0.5f, 0.3f);
            var red = TMat.Sprite(FxTex.Get("glow"), 0xff4a3a); red.blending = Blending.Additive; red.depthWrite = false; red.transparent = true;
            var cyan = TMat.Sprite(FxTex.Get("glow"), 0x7fe3ff); cyan.blending = Blending.Additive; cyan.depthWrite = false; cyan.transparent = true;
            var rnd = new System.Random(31);
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            for (int i = 0; i < 9; i++)
            {
                bool ship = i < 3;
                var g = Group.Make(0, 0, 0, ship ? "airship" : "pod"); view.scene.add(g);
                var blinks = new List<TMesh>();
                TMesh Part(Mesh m, TMat mat, float x, float y, float z, float rz = 0) { var p = new TMesh(m, mat); p.position.set(x, y, z); p.rotation.z = rz; g.add(p); return p; }
                TMesh Blink(TMat mat, float x, float y, float z, float s) { var b = Sprite.Make(mat); b.position.set(x, y, z); b.scale.set(s, s, 1); g.add(b); blinks.Add(b); return b; }
                if (ship)
                {
                    Part(Geo.Capsule(3.2f, 16, 4, 12), hull, 0, 0, 0, Mathf.PI / 2);
                    Part(Geo.Box(6, 1.6f, 2), dark, 0, -3.6f, 0);
                    Part(Geo.Box(2.6f, 3.2f, 0.25f), dark, -10.5f, 1.8f, 0); Part(Geo.Box(2.6f, 0.25f, 3.2f), dark, -10.5f, 0, 0);
                    Blink(red, 11.5f, 0, 0, 2.2f); Blink(red, -11.5f, 3.2f, 0, 1.8f); Blink(cyan, 0, -4.6f, 0, 1.6f);
                }
                else
                {
                    Part(Geo.Capsule(0.9f, 2.6f, 3, 8), hull, 0, 0, 0, Mathf.PI / 2);
                    Part(Geo.Box(1.4f, 0.5f, 1.2f), dark, -0.4f, -0.9f, 0);
                    Blink(i % 2 == 0 ? red : cyan, 2.4f, 0, 0, 1.1f);
                }
                craft.Add(new Craft
                {
                    o = g, blink = blinks.ToArray(), u = R(-260, 260), phase = R(0, 6),
                    d = ship ? R(160, 300) : R(90, 200), h = ship ? R(25, 60) : R(5, 40), speed = (ship ? R(3, 6) : R(10, 22)) * (rnd.NextDouble() < 0.5 ? -1 : 1),
                });
                g.visible = false;
            }
        }

        // Floodlights high above the Concourse, sweeping the deck: a spot light with a soft cookie, and a visible cone
        void BuildFloods()
        {
            var cookie = new Texture2D(128, 128, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "flood cookie" };
            var px = new Color32[128 * 128];
            for (int y = 0; y < 128; y++) for (int x = 0; x < 128; x++)
            {
                float dx = (x - 63.5f) / 63.5f, dy = (y - 63.5f) / 63.5f, r = Mathf.Sqrt(dx * dx + dy * dy);
                float v = Mathf.Clamp01(1 - Mathf.SmoothStep(0.55f, 1, r)) * (0.82f + 0.18f * Mathf.Cos(r * 22));   // a soft disc with faint lens rings
                byte b = (byte)(v * 255); px[y * 128 + x] = new Color32(b, b, b, b);
            }
            cookie.SetPixels32(px); cookie.Apply(false, true);
            // the visible cone: brightest at the lamp, fading to nothing at its far end
            var cone = Geo.Copy(Geo.Cone(1, 1, 24, 1, true));
            var verts = cone.vertices; var cols = new Color[verts.Length];
            for (int i = 0; i < verts.Length; i++) { float a = Mathf.Clamp01(verts[i].y + 0.5f); cols[i] = new Color(1, 1, 1, a * a); }
            cone.colors = cols;
            var coneMat = FxPool.Mat(Templates.ParticleAdd, null, new Color(0.75f, 0.9f, 1f, 0.07f));
            for (int i = 0; i < 3; i++)
            {
                var go = new GameObject("floodlight " + i); go.transform.SetParent(root, false);
                var l = go.AddComponent<Light>(); l.type = LightType.Spot; l.spotAngle = 26; l.innerSpotAngle = 14; l.range = 45;
                l.color = new Color(0.85f, 0.93f, 1f); l.intensity = 0; l.shadows = LightShadows.None; l.cookie = cookie; l.enabled = false;
                var cgo = new GameObject("floodlight cone"); cgo.transform.SetParent(go.transform, false);
                cgo.AddComponent<MeshFilter>().sharedMesh = cone;
                var mr = cgo.AddComponent<MeshRenderer>(); mr.sharedMaterial = coneMat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
                floods.Add(new Flood { l = l, cone = cgo.transform, phase = i * 2.1f, du = -12 + i * 12 });
            }
        }

        // How many to emit this frame at `rate` per second (scaled by the effect density), keeping the fractions
        int Count(string key, float rate, float dt)
        {
            acc.TryGetValue(key, out var a);
            a += rate * FxCfg.Amount * dt;
            int n = (int)a; acc[key] = a - n;
            return n;
        }
        static float Rn(float a, float b) => a + Random.value * (b - a);

        public void OnEvent(Ev ev)
        {
            string T = ev.type;
            if (T == "bossSlam" || T == "ramSlam" || (T == "poundLand" && ev.level >= 2) || (T == "boxBreak" && ev.box != null && ev.box.tag == "pillar"))
                lampT = Mathf.Max(lampT, 0.7f);
        }

        public void Update(float dt, World world, float camX, float camY)
        {
            ParticleBudget.Ambient = true;
            try {
            t += dt;
            bool on = SETTINGS.weather;
            string z = on ? Level.ZoneAt(camX).id : "";
            if (z != zone) Enter(z);
            Lamps(dt);
            if (world != null) Gusts(world);
            switch (zone)
            {
                case "tower": Storm(dt, camX, camY); break;
                case "skyline": Skyline(dt, camX, camY); break;
                case "foundry": Foundry(dt, camX, camY); break;
                case "undercity": Undercity(dt, camX, camY); break;
                case "arena": Concourse(dt, camX, camY); break;
            }
            UpdateBolt(dt);
            UpdateCraft(dt, camX);
            int weatherLights = bolt.enabled ? 1 : 0;
            foreach (var f in floods) if (f.l.enabled) weatherLights++;
            PerfOverlay.WeatherLights = weatherLights;
            } finally { ParticleBudget.Ambient = false; }
        }

        void Enter(string z)
        {
            zone = z; rain.ps.Clear(); drips.ps.Clear(); acc.Clear();
            foreach (var c in craft) c.o.visible = z == "skyline";
            foreach (var f in floods) f.l.enabled = z == "arena";
            if (z != "tower") { bolt.enabled = false; boltLine.enabled = false; boltT = -1; }
            bool w = z == "undercity";
            if (w != wet && view.floor != null) { view.floor.roughness = w ? 0.26f : 0.5f; wet = w; }   // the damp sheen
            if (z != "undercity" && neonDirty) { foreach (var (m, k) in view.neon) m.emissiveIntensity = k; neonDirty = false; }
        }

        // A random point in the volume the camera sees around the play plane: x along the route, height, depth
        static Vector3 At(float camX, float dx0, float dx1, float y0, float y1, float d0, float d1) => S.W(camX + Rn(dx0, dx1), Rn(y0, y1), Rn(d0, d1));

        void Storm(float dt, float camX, float camY)
        {
            var tint = dropTint["rain"];
            int n = Count("rain", 1100, dt);
            for (int i = 0; i < n; i++)
            {
                var at = At(camX, -20, 18, camY + 9, camY + 13, -9, 6);
                rain.Emit(at, S.Dir(camX, 4.5f, -24 - Random.value * 4), Rn(0.022f, 0.032f), 1.3f, tint);
            }
            // mist drifting low across the deck
            n = Count("mist", 1.4f, dt);
            for (int i = 0; i < n; i++)
                V.smoke.Emit(At(camX, -22, 22, camY - 3.5f, camY - 1, -7, 3), S.Dir(camX, 0.8f, 0.05f), Rn(6, 9), Rn(7, 10), new Color(0.78f, 0.83f, 0.9f, 0.16f));
            // dark clouds rolling past high behind the tower
            n = Count("storm", 2.2f, dt);
            for (int i = 0; i < n; i++)
                V.smokeDark.Emit(At(camX, -50, 70, camY + 16, camY + 32, -85, -40), S.Dir(camX, 2.5f, 0), Rn(28, 44), Rn(12, 17), new Color(0.32f, 0.34f, 0.4f, 0.65f));
            // lightning, every few seconds
            nextBolt -= dt;
            if (nextBolt <= 0) { nextBolt = Rn(5, 13); Strike(camX, camY); }
        }

        void Strike(float camX, float camY)
        {
            boltT = 0; bolt.enabled = true;
            float x = camX + Rn(-35, 35), d = -Rn(60, 120), top = camY + 38, bot = camY - 12;
            const int N = 14;
            boltLine.positionCount = N;
            for (int i = 0; i < N; i++)
            {
                float u = i / (float)(N - 1), j = i == 0 || i == N - 1 ? 0 : Rn(-2.5f, 2.5f);
                boltLine.SetPosition(i, Th.P(S.W(x + j + u * Rn(-1, 1) * 4, Mathf.Lerp(top, bot, u), d)));
            }
            boltLine.widthMultiplier = Rn(0.6f, 1.1f);
            V.lights.Flash(S.W(x, camY + 26, d * 0.5f), new Color(0.75f, 0.85f, 1f), 30, 140, 0.45f);
            lampT = Mathf.Max(lampT, 0.35f);
            view.Startle(0.6f);
        }

        void UpdateBolt(float dt)
        {
            if (boltT < 0) return;
            boltT += dt;
            // two or three quick pulses, then gone
            float k = boltT < 0.08f ? 1 : boltT < 0.16f ? 0.25f : boltT < 0.24f ? 0.85f : boltT < 0.5f ? Mathf.Lerp(0.5f, 0, (boltT - 0.24f) / 0.26f) : 0;
            bolt.intensity = 2.2f * k;
            bool flash = FxCfg.Lights && !Cfg.SETTINGS.reducedScreenEffects;
            bolt.enabled = flash && k > 0;
            boltLine.enabled = !Cfg.SETTINGS.reducedScreenEffects && k > 0.2f;
            var c = new Color(0.85f, 0.9f, 1f, k); boltLine.startColor = c; boltLine.endColor = c;
            if (boltT >= 0.5f) { boltT = -1; bolt.enabled = false; boltLine.enabled = false; }
        }

        void Skyline(float dt, float camX, float camY)
        {
            // wind streaks whipping across the view
            int n = Count("wind", 22, dt);
            for (int i = 0; i < n; i++)
                V.streak.Emit(At(camX, -26, 8, camY - 3, camY + 9, -8, 5), S.Dir(camX, 28 + Random.value * 10, Rn(-0.6f, 0.6f)), Rn(0.025f, 0.04f), Rn(0.45f, 0.75f), new Color(1, 1, 1, 0.2f));
        }

        void UpdateCraft(float dt, float camX)
        {
            if (zone != "skyline") return;
            var f = Level.Frame(camX);
            var P = new Vector3((float)f.px, 0, (float)f.pz); var T = new Vector3((float)f.tx, 0, (float)f.tz).normalized; var Nn = new Vector3((float)f.nx, 0, (float)f.nz).normalized;
            float yaw = S.YawAt(camX);
            foreach (var c in craft)
            {
                c.u += c.speed * dt;
                if (c.u > 300) c.u -= 600; else if (c.u < -300) c.u += 600;
                var p = P + T * c.u + Vector3.up * (c.h + Mathf.Sin(t * 0.3f + c.phase) * 1.5f) - Nn * c.d;
                c.o.position.set(p.x, p.y, p.z);
                c.o.rotation.y = yaw + (c.speed < 0 ? Mathf.PI : 0);
                for (int i = 0; i < c.blink.Length; i++) c.blink[i].visible = Mathf.Repeat(t * 0.9f + c.phase + i * 0.37f, 1) < 0.18f;
            }
        }

        void Foundry(float dt, float camX, float camY)
        {
            // steam: vents behind the deck huff now and then
            int n = Count("vent", 0.6f, dt);
            for (int i = 0; i < n; i++)
            {
                var at = At(camX, -18, 18, camY - 3.5f, camY - 2.5f, -8, -3);
                for (int k = 0; k < 6; k++)
                    V.smoke.Emit(at + Random.insideUnitSphere * 0.4f, new Vector3(Rn(-0.3f, 0.3f), Rn(1.6f, 2.8f), Rn(-0.3f, 0.3f)), Rn(1.6f, 2.6f), Rn(2.5f, 3.6f), new Color(0.93f, 0.93f, 0.96f, 0.4f));
            }
            // embers rising from the furnaces below
            n = Count("ember", 18, dt);
            for (int i = 0; i < n; i++)
                V.ember.Emit(At(camX, -22, 22, camY - 6, camY - 3, -10, 2), new Vector3(Rn(-0.4f, 0.4f), Rn(1.2f, 3f), Rn(-0.3f, 0.3f)), Rn(0.05f, 0.12f), Rn(3, 5), new Color(1, Rn(0.45f, 0.7f), 0.2f));
            // heat shimmer over them
            if (FxCfg.Distortion)
            {
                n = Count("heat", 3, dt);
                for (int i = 0; i < n; i++)
                    V.haze.Emit(At(camX, -16, 16, camY - 3.5f, camY - 2, -6, -2), new Vector3(0, Rn(0.8f, 1.5f), 0), Rn(3, 4.5f), Rn(1.6f, 2.4f), new Color(1, 1, 1, 0.45f));
            }
        }

        void Undercity(float dt, float camX, float camY)
        {
            // drips from the ledges overhead
            var tint = dropTint["drips"];
            int n = Count("drip", 28, dt);
            for (int i = 0; i < n; i++)
                drips.Emit(At(camX, -18, 18, camY + 6, camY + 9, -7, 4), new Vector3(0, -1.5f, 0), Rn(0.025f, 0.035f), 2.5f, tint);
            // low haze hanging over the street
            n = Count("haze", 1.1f, dt);
            for (int i = 0; i < n; i++)
                V.smoke.Emit(At(camX, -22, 22, camY - 3.8f, camY - 2, -8, 3), S.Dir(camX, 0.4f, 0.02f), Rn(7, 10), Rn(8, 11), new Color(0.55f, 0.62f, 0.78f, 0.14f));
            // the neon stutters: now and then a sign drops out for a moment
            float flick = Mathf.PerlinNoise(t * 7, 0.7f) > 0.8f ? 0.25f : 1;
            float hum = 0.92f + 0.08f * Mathf.Sin(t * 31);
            foreach (var (m, k) in view.neon) m.emissiveIntensity = k * flick * hum;
            neonDirty = true;
        }

        void Concourse(float dt, float camX, float camY)
        {
            // floodlights high in front of the deck, sweeping slowly over it
            foreach (var f in floods)
            {
                f.l.enabled = FxCfg.Lights;
                f.cone.gameObject.SetActive(FxCfg.Lights);
                float sweep = Mathf.Sin(t * 0.45f + f.phase);
                var pos = Th.P(S.W(camX + f.du, camY + 16, 9));
                var aim = Th.P(S.W(camX + f.du + sweep * 10, camY - 3, -1 + Mathf.Cos(t * 0.3f + f.phase) * 2));
                var dir = (aim - pos).normalized; float len = (aim - pos).magnitude + 4;
                f.l.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(dir));
                f.l.intensity = 40;
                // (the cone mesh's apex is at +y: point it back at the lamp, base toward the deck)
                f.cone.localPosition = new Vector3(0, 0, len * 0.5f);
                f.cone.localRotation = Quaternion.FromToRotation(Vector3.up, Vector3.back);
                float rad = len * Mathf.Tan(f.l.spotAngle * 0.5f * Mathf.Deg2Rad);
                f.cone.localScale = new Vector3(rad * 2, len, rad * 2);
            }
            // holo dust floating in their light
            int n = Count("dust", 14, dt);
            for (int i = 0; i < n; i++)
                V.mote.Emit(At(camX, -18, 18, camY - 3, camY + 7, -6, 4), new Vector3(Rn(-0.2f, 0.2f), Rn(-0.1f, 0.2f), Rn(-0.2f, 0.2f)), Rn(0.04f, 0.08f), Rn(3, 5), new Color(0.5f, 0.9f, 1f, 0.8f));
        }

        // Deck lights stutter, then settle, after something heavy lands (or lightning strikes)
        void Lamps(float dt)
        {
            if (lampT > 0)
            {
                lampT -= dt;
                float k = lampT <= 0 ? 1 : Mathf.PerlinNoise(t * 24, 3.3f) > 0.5f ? 1.35f : 0.12f;
                foreach (var (m, b) in view.lamps) m.emissiveIntensity = b * k;
                lampsDirty = lampT > 0;
            }
            else if (lampsDirty) { foreach (var (m, b) in view.lamps) m.emissiveIntensity = b; lampsDirty = false; }
        }

        // Flags and banners whip as a player dashes past
        void Gusts(World world)
        {
            foreach (var p in world.players)
            {
                if (p.state != "dash" && System.Math.Abs(p.vx) < 11) continue;
                view.ambience.Gust(S.W(p.x, p.y + 1, LevelFeatures.Depth(p)), 1);
            }
        }
    }
}
