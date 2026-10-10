// Shared decorative admission. All native layers (including rain/splashes) and legacy pools register here.
// Projectile cores and hazard meshes are separate, persistent gameplay cues and are never culled by this budget.
using System;
using System.Collections.Generic;
using UnityEngine;
namespace NovaStriker.Game
{
    public static class ParticleBudget
    {
        static readonly List<ParticleSystem> systems = new List<ParticleSystem>();
        static readonly List<Func<int>> pools = new List<Func<int>>();
        static readonly ParticleSystem.Particle[] trim = new ParticleSystem.Particle[6000];
        static readonly List<Func<int, int>> trimmers = new List<Func<int, int>>();
        static int frame = -1, admitted, counted;
        public static bool Ambient;
        public static bool Advancing = true;
        public static void Register(ParticleSystem ps) { if (!systems.Contains(ps)) systems.Add(ps); }
        public static void Register(Func<int> count, Func<int, int> trim = null) { pools.Add(count); if (trim != null) trimmers.Add(trim); }
        public static int Live
        {
            get { int n = 0; foreach (var ps in systems) if (ps) n += ps.particleCount; foreach (var p in pools) n += p(); return n; }
        }
        public static bool Admit(bool ambient = false)
        {
            if (!Advancing) return false;
            if (frame != Time.frameCount) { frame = Time.frameCount; admitted = 0; counted = Live; }
            int cap = FxCfg.MaxParticles;
            if (ambient || Ambient) cap = cap * 3 / 5; // reserve combat feedback before weather and lingering smoke
            if (counted + admitted >= cap) return false;
            admitted++; return true;
        }
        public static void Update(float dt)
        {
            Advancing = dt > 0;
            int excess = Mathf.Max(0, Live - FxCfg.MaxParticles);
            // Shed native lingering particles immediately when the preset lowers its cap.
            for (int i = systems.Count - 1; i >= 0; i--)
            {
                var ps = systems[i]; if (!ps) { systems.RemoveAt(i); continue; }
                var main = ps.main;
                // Native particles use the same bounded presentation delta as legacy effects.
                // Keep Unity's automatic simulation/jobs; scale its clock instead of simulating twice.
                float nativeDelta = main.useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                main.simulationSpeed = Advancing && nativeDelta > 0 ? dt / nativeDelta : 0;
                if (excess > 0) { int n = ps.GetParticles(trim); int drop = Mathf.Min(n, excess); ps.SetParticles(trim, n - drop); excess -= drop; }
            }
            foreach (var trimPool in trimmers) if (excess > 0) excess -= trimPool(excess);
            frame = -1;
        }
    }
}
