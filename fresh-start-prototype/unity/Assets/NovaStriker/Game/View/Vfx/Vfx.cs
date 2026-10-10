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
        readonly List<FxLayer> all = new List<FxLayer>();

        public Vfx(View view)
        {
            this.view = view;
            root = new GameObject("Vfx").transform; root.SetParent(view.scene.go.transform, false);
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
            smoke = Add(new FxPool.Spec { name = "smoke", mat = FxPool.Mat(lit, FxTex.Get("smoke"), white), max = 700, flipbook = 8, gravity = -0.025f, drag = 1.1f, noise = 0.35f,
                size = FxPool.Curve(0, 0.45f, 1, 1.6f), fade = FxPool.Fade(white, white, 0, 0.85f, 0, 0.12f) });
            smokeDark = Add(new FxPool.Spec { name = "smoke dark", mat = FxPool.Mat(alpha, FxTex.Get("smoke"), new Color(0.22f, 0.21f, 0.2f)), max = 400, flipbook = 8, gravity = -0.03f, drag = 1.2f, noise = 0.4f,
                size = FxPool.Curve(0, 0.5f, 1, 1.7f), fade = FxPool.Fade(white, white, 0, 0.8f, 0, 0.15f) });
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
        }

        public void ClearParticles() { foreach (var layer in all) layer.ps.Clear(); }

        public void Update(float dt)
        {
            ParticleBudget.Update(dt);
            if (FxCfg.Smoke == "off") { smoke.ps.Clear(); smokeDark.ps.Clear(); }
            if (FxCfg.Debris == "off") { debris.ps.Clear(); shard.ps.Clear(); }
            if (!FxCfg.Distortion) haze.ps.Clear();
            lights.Update(dt); decals.Update(dt); colliders.Update();
            int n = ParticleBudget.Live;
            PerfOverlay.Particles = n; PerfOverlay.Lights = lights.Active + Sparks.ActiveLights; PerfOverlay.Decals = decals.Active;
        }
    }
}
