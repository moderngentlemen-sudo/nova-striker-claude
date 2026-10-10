// Long-lived particle layers (Unity's Particle System): one system per kind of particle (sparks, embers, fire, smoke,
// debris...), fed with Emit at many places, so a blast costs no new objects. Each layer is set up once: its
// material and texture, flipbook, gravity, drag, noise, fade and growth over its life, and whether it bounces off
// the level (FxColliders' layer). Positions come in three.js space (as the rest of the game) and are mirrored here.
using System;
using NovaStriker.Game.Three;
using UnityEngine;

namespace NovaStriker.Game
{
    public sealed class FxLayer
    {
        public readonly ParticleSystem ps;
        ParticleSystem.EmitParams ep;
        public FxLayer(ParticleSystem ps) { this.ps = ps; ep = new ParticleSystem.EmitParams(); }
        public int Count => ps.particleCount;

        // One particle at a three.js point, moving at v (three.js space, m/s)
        public void Emit(Vector3 at, Vector3 v, float size, float life, Color c, float rot = 0, float spin = 0)
        {
            ep.position = Th.P(at); ep.velocity = Th.P(v);
            ep.startSize = size; ep.startLifetime = life; ep.startColor = c;
            ep.rotation = rot * Mathf.Rad2Deg; ep.angularVelocity = spin * Mathf.Rad2Deg;
            ps.Emit(ep, 1);
        }
        public void Emit3D(Vector3 at, Vector3 v, Vector3 size, float life, Color c, Vector3 rot, Vector3 spin)
        {
            ep.position = Th.P(at); ep.velocity = Th.P(v);
            ep.startSize3D = size; ep.startLifetime = life; ep.startColor = c;
            ep.rotation3D = rot * Mathf.Rad2Deg; ep.angularVelocity3D = spin * Mathf.Rad2Deg;
            ps.Emit(ep, 1);
            ep.ResetStartSize(); ep.ResetRotation(); ep.ResetAngularVelocity();
        }
    }

    public static class FxPool
    {
        public const int FX_LAYER = 9;   // (the invisible level colliders particles bounce off: FxColliders)

        // A particle material from one of the particle templates (NovaSetup: ParticleAdd, ParticleAlpha, ParticleLit)
        public static Material Mat(Material template, Texture tex, Color tint)
        {
            var m = new Material(template);
            if (tex != null) m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", tint);
            return m;
        }

        public sealed class Spec
        {
            public string name; public Material mat; public int max = 600;
            public float gravity, drag;                 // gravity: times the scene's (negative rises); drag: per second
            public int flipbook;                        // tiles across (8 for the 8 x 8 sheets), 0 = none
            public bool stretch, collide, mesh3D;       // stretched along velocity; bounce off the level; 3D-sized mesh particles
            public float stretchK = 0.08f, bounce = 0.35f;
            public Mesh[] meshes;
            public AnimationCurve size;                 // size over life (multiplier)
            public Gradient fade;                       // colour and alpha over life (multiplies the emitted colour)
            public float noise;                         // turbulence strength
            public Action<ParticleSystem> extra;
        }

        public static FxLayer Layer(Spec s, Transform parent)
        {
            var go = new GameObject("fx " + s.name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false; main.loop = true; main.duration = 1;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = s.max; main.gravityModifier = s.gravity;
            main.startSize3D = s.mesh3D; main.startRotation3D = s.mesh3D;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            var em = ps.emission; em.rateOverTime = 0; em.enabled = true;
            var sh = ps.shape; sh.enabled = false;
            if (s.drag > 0) { var lv = ps.limitVelocityOverLifetime; lv.enabled = true; lv.drag = s.drag; lv.multiplyDragByParticleSize = false; lv.multiplyDragByParticleVelocity = true; }
            if (s.size != null) { var so = ps.sizeOverLifetime; so.enabled = true; so.size = new ParticleSystem.MinMaxCurve(1, s.size); }
            if (s.fade != null) { var co = ps.colorOverLifetime; co.enabled = true; co.color = new ParticleSystem.MinMaxGradient(s.fade); }
            if (s.flipbook > 0)
            {
                var tsa = ps.textureSheetAnimation; tsa.enabled = true; tsa.numTilesX = s.flipbook; tsa.numTilesY = s.flipbook;
                tsa.animation = ParticleSystemAnimationType.WholeSheet; tsa.frameOverTime = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 0, 1, 1));
            }
            if (s.noise > 0) { var n = ps.noise; n.enabled = true; n.strength = s.noise; n.frequency = 0.6f; n.scrollSpeed = 0.4f; n.quality = ParticleSystemNoiseQuality.Medium; }
            if (s.collide)
            {
                var c = ps.collision; c.enabled = true; c.type = ParticleSystemCollisionType.World; c.mode = ParticleSystemCollisionMode.Collision3D;
                c.collidesWith = 1 << FX_LAYER; c.bounce = s.bounce; c.dampen = 0.35f; c.lifetimeLoss = 0.08f; c.radiusScale = 0.5f;
                c.quality = ParticleSystemCollisionQuality.Medium;
            }
            if (s.mesh3D) { var r3 = ps.rotationOverLifetime; r3.enabled = false; }
            s.extra?.Invoke(ps);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = s.mat;
            r.sortMode = ParticleSystemSortMode.Distance;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            if (s.meshes != null)
            {
                r.renderMode = ParticleSystemRenderMode.Mesh; r.SetMeshes(s.meshes); r.enableGPUInstancing = true;
                r.alignment = ParticleSystemRenderSpace.World;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; r.receiveShadows = true;
            }
            else if (s.stretch) { r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = s.stretchK; r.lengthScale = 1; }
            else r.renderMode = ParticleSystemRenderMode.Billboard;
            ps.Play();
            return new FxLayer(ps);
        }

        // Handy curves and fades
        public static AnimationCurve Curve(params float[] kv)
        {
            var keys = new Keyframe[kv.Length / 2];
            for (int i = 0; i < keys.Length; i++) keys[i] = new Keyframe(kv[i * 2], kv[i * 2 + 1]);
            var c = new AnimationCurve(keys);
            for (int i = 0; i < keys.Length; i++) c.SmoothTangents(i, 0);
            return c;
        }
        public static Gradient Fade(Color c0, Color c1, float a0, float aMid, float a1, float mid = 0.3f)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(c0, 0), new GradientColorKey(c1, 1) },
                      new[] { new GradientAlphaKey(a0, 0), new GradientAlphaKey(aMid, mid), new GradientAlphaKey(a1, 1) });
            return g;
        }
    }
}
