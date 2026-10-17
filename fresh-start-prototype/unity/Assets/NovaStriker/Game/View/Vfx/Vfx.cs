// The cinematic effects' shared machinery, made once by the view: the particle layers (FxPool), effect lights
// (FxLights), scorch marks (FxDecals) and the level colliders particles bounce off (FxColliders). Effect modules
// reach it through view.vfx; every amount they ask for is scaled by FxCfg.Amount.
using System.Collections.Generic;
using NovaStriker.Game.Three;
using UnityEngine;

namespace NovaStriker.Game
{
    public sealed class Vfx
    {
        public readonly View view;
        public readonly FxLights lights;
        public readonly FxDecals decals;
        public readonly FxColliders colliders;
        public readonly Transform root;
        // the layers
        public FxLayer spark, ember, glow, fire, smoke, smokeDark, debris, shard, streak, mote, flash, toon, haze;
        // (the weapons' energy layers: rings that spread or close, a closing haze, floor warp, rubble a well pulls up, hard-light chips)
        public FxLayer ring, ringIn, hazeIn, warp, rubble, chip;
        readonly List<FxLayer> all = new List<FxLayer>();

        public Vfx(View view)
        {
            this.view = view;
            root = new GameObject("Vfx").transform;
            lights = new FxLights(root);
            decals = new FxDecals(view.scene);
            colliders = new FxColliders(root);
            BuildLayers();
        }

        FxLayer Add(FxPool.Spec s) { var l = FxPool.Layer(s, root); all.Add(l); return l; }

        void BuildLayers()
        {
            Material add = Templates.ParticleAdd, alpha = Templates.ParticleAlpha, lit = Templates.ParticleLit;
            var white = Color.white;
            // hot streaks that fall, bounce off the level and cool
            spark = Add(new FxPool.Spec { name = "sparks", mat = FxPool.Mat(add, FxTex.Get("spark"), white), max = 1200, gravity = 0.45f, drag = 0.6f, stretch = true, stretchK = 0.05f, collide = true, bounce = 0.4f,
                fade = FxPool.Fade(white, new Color(1, 0.45f, 0.15f), 1, 1, 0) });
            ember = Add(new FxPool.Spec { name = "embers", mat = FxPool.Mat(add, FxTex.Get("ember"), white), max = 800, gravity = -0.04f, drag = 0.5f, noise = 0.8f, collide = true,
                size = FxPool.Curve(0, 1, 1, 0.3f), fade = FxPool.Fade(white, new Color(1, 0.35f, 0.1f), 1, 0.9f, 0) });
            glow = Add(new FxPool.Spec { name = "glow", mat = FxPool.Mat(add, FxTex.Get("glow"), white), max = 300,
                size = FxPool.Curve(0, 0.6f, 0.2f, 1, 1, 1.15f), fade = FxPool.Fade(white, white, 1, 0.6f, 0, 0.15f) });
            flash = Add(new FxPool.Spec { name = "flash", mat = FxPool.Mat(add, FxTex.Get("spark"), white), max = 60,
                size = FxPool.Curve(0, 0.4f, 0.25f, 1, 1, 1.4f), fade = FxPool.Fade(white, white, 1, 0.8f, 0, 0.2f) });
            fire = Add(new FxPool.Spec { name = "fire", mat = FxPool.Mat(add, FxTex.Get("fireball"), white), max = 500, flipbook = 8, gravity = -0.06f, drag = 1.6f, noise = 0.5f,
                size = FxPool.Curve(0, 0.5f, 0.4f, 1, 1, 1.3f), fade = FxPool.Fade(white, white, 1, 1, 0, 0.6f) });
            // thick, slow smoke that billows out, holds for most of its life and only thins at the end
            smoke = Add(new FxPool.Spec { name = "smoke", mat = FxPool.Mat(lit, FxTex.Get("smoke"), white), max = 1600, flipbook = 8, gravity = -0.012f, drag = 0.9f, noise = 0.3f,
                size = FxPool.Curve(0, 0.4f, 0.2f, 1.1f, 1, 2.4f), fade = FxPool.Linger(0.95f, 0.6f) });
            smokeDark = Add(new FxPool.Spec { name = "smoke dark", mat = FxPool.Mat(alpha, FxTex.Get("smoke"), new Color(0.2f, 0.19f, 0.18f)), max = 900, flipbook = 8, gravity = -0.016f, drag = 1f, noise = 0.35f,
                size = FxPool.Curve(0, 0.45f, 0.2f, 1.1f, 1, 2.5f), fade = FxPool.Linger(0.92f, 0.55f) });
            // toon puffs for the Stylised explosions: hard-edged, two-tone
            toon = Add(new FxPool.Spec { name = "toon", mat = FxPool.Mat(alpha, FxTex.Get("glow"), white), max = 300, drag = 2.2f, gravity = -0.02f,
                size = FxPool.Curve(0, 0.3f, 0.25f, 1, 1, 0.1f), fade = FxPool.Fade(white, new Color(0.35f, 0.35f, 0.4f), 1, 1, 1, 0.5f) });
            streak = Add(new FxPool.Spec { name = "streaks", mat = FxPool.Mat(add, FxTex.Get("streak"), white), max = 400, stretch = true, stretchK = 0.09f,
                fade = FxPool.Fade(white, white, 1, 1, 0) });
            mote = Add(new FxPool.Spec { name = "motes", mat = FxPool.Mat(add, FxTex.Get("ember"), white), max = 800, drag = 0.8f, noise = 1.2f,
                fade = FxPool.Fade(white, white, 0, 1, 0, 0.2f) });
            // heat haze and shock rings: they bend the scene behind them (Distort.shader)
            var hazeMat = new Material(Templates.Distort); hazeMat.SetTexture("_MainTex", FxTex.Get("ripple"));
            haze = Add(new FxPool.Spec { name = "haze", mat = hazeMat, max = 120, size = FxPool.Curve(0, 0.3f, 1, 1.4f), fade = FxPool.Fade(white, white, 0, 1, 0, 0.25f) });
            // chunky debris and ceramic shards: lit mesh particles that bounce off the level and cast shadows
            var chunkMat = FxPool.Mat(lit, null, new Color(0.55f, 0.58f, 0.62f));
            debris = Add(new FxPool.Spec { name = "debris", mat = chunkMat, max = 220, gravity = 1, collide = true, bounce = 0.3f, mesh3D = true,
                meshes = new[] { Geo.Box(1, 0.6f, 0.8f), Geo.Tetrahedron(0.7f), Geo.Octahedron(0.6f) }, fade = FxPool.Fade(white, white, 1, 1, 0, 0.85f) });
            var shardMat = FxPool.Mat(lit, null, new Color(0.92f, 0.93f, 0.96f));
            shard = Add(new FxPool.Spec { name = "shards", mat = shardMat, max = 220, gravity = 1, collide = true, bounce = 0.35f, mesh3D = true,
                meshes = new[] { Geo.Box(1, 0.15f, 0.7f), Geo.Tetrahedron(0.6f) }, fade = FxPool.Fade(white, white, 1, 1, 0, 0.85f) });
            BuildWeaponLayers(add, lit, white);
        }

        // The weapons' energy layers (Fx.Weapons, CineLightning, CineDisc, CineWell)
        void BuildWeaponLayers(Material add, Material lit, Color white)
        {
            // a crisp ring that spreads (impact and muzzle rings), and one that closes in (light bending into a well)
            ring = Add(new FxPool.Spec { name = "rings", mat = FxPool.Mat(add, FxTex.Get("ring"), white), max = 200,
                size = FxPool.Curve(0, 0.2f, 0.35f, 0.8f, 1, 1), fade = FxPool.Fade(white, white, 1, 0.85f, 0, 0.3f) });
            ringIn = Add(new FxPool.Spec { name = "rings in", mat = FxPool.Mat(add, FxTex.Get("ring"), white), max = 120,
                size = FxPool.Curve(0, 1, 0.6f, 0.45f, 1, 0.05f), fade = FxPool.Fade(white, white, 0, 1, 0.3f, 0.35f) });
            // a haze that closes in (lensing around a black hole), and flat warps that lie on the floor under one
            var hazeInMat = new Material(Templates.Distort); hazeInMat.SetTexture("_MainTex", FxTex.Get("ripple"));
            hazeIn = Add(new FxPool.Spec { name = "haze in", mat = hazeInMat, max = 80, size = FxPool.Curve(0, 1.2f, 1, 0.25f), fade = FxPool.Fade(white, white, 0, 1, 0, 0.5f) });
            var warpMat = new Material(Templates.Distort); warpMat.SetTexture("_MainTex", FxTex.Get("ripple"));
            warp = Add(new FxPool.Spec { name = "floor warp", mat = warpMat, max = 60, mesh3D = true, meshes = new[] { Geo.Plane(1, 1) },
                size = FxPool.Curve(0, 1.15f, 1, 0.35f), fade = FxPool.Fade(white, white, 0, 1, 0, 0.5f),
                extra = ps => { var r = ps.GetComponent<ParticleSystemRenderer>(); r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; } });
            // floor rubble a well tears up and draws in (no gravity: the well holds it), shrinking as it is swallowed
            rubble = Add(new FxPool.Spec { name = "rubble", mat = FxPool.Mat(lit, null, new Color(0.42f, 0.44f, 0.48f)), max = 160, mesh3D = true,
                meshes = new[] { Geo.Box(1, 0.5f, 0.8f), Geo.Tetrahedron(0.7f) }, size = FxPool.Curve(0, 1, 0.75f, 0.9f, 1, 0.05f), fade = FxPool.Fade(white, white, 1, 1, 1, 0.5f) });
            // hard-light chips: tiny bright shards of energy thrown off saw teeth and prisms (additive, unlit)
            chip = Add(new FxPool.Spec { name = "chips", mat = FxPool.Mat(add, FxTex.Get("spark"), white), max = 400, gravity = 0.25f, drag = 1.2f, stretch = true, stretchK = 0.03f,
                size = FxPool.Curve(0, 1, 1, 0.2f), fade = FxPool.Fade(white, white, 1, 1, 0, 0.5f) });
        }

        // Draws every effect once, unseen (a speck that dies at once), while the start screen is up: their shaders are
        // compiled then rather than in the middle of the first fight
        public void WarmUp(Vector3 at)
        {
            var c = new Color(1, 1, 1, 0.002f);
            foreach (var l in all)
            {
                if (l.ps.GetComponent<ParticleSystemRenderer>().renderMode == ParticleSystemRenderMode.Mesh) l.Emit3D(at, Vector3.zero, Vector3.one * 0.001f, 0.05f, c, Vector3.zero, Vector3.zero);
                else l.Emit(at, Vector3.zero, 0.001f, 0.05f, c, 0, 0, true);
            }
            lights.Flash(at, Color.black, 0.001f, 0.1f, 0.05f);
        }

        int smokeMax, smokeDarkMax; float smokeK = -1;
        public void Update(float dt)
        {
            // Smoke duration: longer-lived smoke needs room for more puffs at once
            float k = FxCfg.SmokeLife;
            if (k != smokeK)
            {
                if (smokeK < 0) { smokeMax = smoke.ps.main.maxParticles; smokeDarkMax = smokeDark.ps.main.maxParticles; }
                smokeK = k; smoke.lifeK = smokeDark.lifeK = k;
                var m1 = smoke.ps.main; m1.maxParticles = (int)(smokeMax * Mathf.Max(1, k));
                var m2 = smokeDark.ps.main; m2.maxParticles = (int)(smokeDarkMax * Mathf.Max(1, k));
            }
            lights.Update(dt); decals.Update(dt); colliders.Update();
            int n = 0; foreach (var l in all) n += l.Count;
            PerfOverlay.Particles = n; PerfOverlay.Lights = lights.Active; PerfOverlay.Decals = decals.Active;
        }
    }
}
