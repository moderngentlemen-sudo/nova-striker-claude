// The weapons' energy looks (any Projectiles setting but Classic): the pieces of Fx the cinematic weapon effects
// share. Nova's Disc is drawn by CineDisc, his Chain by CineLightning (SubFX), his Gravity Well by CineWell (SubFX).
using NovaStriker.Sim;
using UnityEngine;
using static NovaStriker.Sim.Cfg;

namespace NovaStriker.Game
{
    public sealed partial class Fx
    {
        float frameDt;   // (this frame's time step, for the per-frame drawing in SyncProjectiles)
        CineDisc cineDisc;
        CineDisc CineDiscFx => FxCfg.CineBeams && view != null && view.vfx != null ? (cineDisc ??= new CineDisc(view)) : null;

        // The Disc's moments (on top of the classic reactions): thrown, called back, caught
        void DiscEvent(Ev ev)
        {
            if (!FxCfg.CineBeams) return;
            var c = S.Lin(SUB_LOOK["disc"].tint); var at = W(ev.x, ev.y, 0.3f); float L = (float)ev.level;
            switch (ev.type)
            {
                case "discThrow":
                    V.flash.Emit(at, Vector3.zero, 0.9f + 0.25f * L, 0.07f, Hdr(Color.white, 5));
                    V.ring.Emit(at, Vector3.zero, 1.1f + 0.35f * L, 0.18f, Hdr(c, 3));
                    for (int i = 0, n = Count(8 + 5 * L); i < n; i++) V.chip.Emit(at, Random.onUnitSphere * (5 + Random.value * 5), 0.06f, 0.3f, Hdr(c, 4), Random.value * 6.3f);
                    V.lights.Flash(at, Color.Lerp(c, Color.white, 0.3f), 2.5f + L, 3 + L, 0.12f);
                    break;
                case "discRecall": V.ring.Emit(at, Vector3.zero, 1.2f, 0.16f, Hdr(Color.white, 2.5f)); V.flash.Emit(at, Vector3.zero, 0.7f, 0.06f, Hdr(c, 4)); break;
                case "discCatch":
                    V.ringIn.Emit(at, Vector3.zero, 1.6f, 0.14f, Hdr(c, 3));
                    V.flash.Emit(at, Vector3.zero, 1.0f, 0.08f, Hdr(Color.white, 5));
                    for (int i = 0, n = Count(8); i < n; i++) V.chip.Emit(at, Random.onUnitSphere * 4, 0.05f, 0.22f, Hdr(c, 4), Random.value * 6.3f);
                    break;
                case "discFade": for (int i = 0, n = Count(12); i < n; i++) V.chip.Emit(at, Random.onUnitSphere * 5, 0.06f, 0.35f, Hdr(c, 3), Random.value * 6.3f); break;
            }
        }

        // ---- Gravity Well (the open well each frame: CineWell) ----
        // In flight: a small dark orb trailing light, motes already curling into it
        public void WellOrb(Well w, double x, double y)
        {
            if (V == null) return;
            var c = S.Lin(SUB_LOOK["well"].tint); var at = W(x, y, 0.2f); float L = (float)w.level;
            V.glow.Emit(at, Vector3.zero, 0.9f + 0.15f * L, 0.05f, Hdr(c, 2));
            V.dark.Emit(at, Vector3.zero, 0.6f + 0.1f * L, 0.05f, new Color(0, 0, 0, 0.6f));
            V.mote.Emit(at, -S.Dir(x, w.vx, w.vy) * 0.08f + Rnd3() * 0.3f, 0.07f, 0.25f, Hdr(c, 3));
            for (int i = 0; i < 1 + (int)L; i++) { var d = Random.onUnitSphere; V.mote.Emit(at + d * (0.6f + 0.15f * L), -d * 4 + Vector3.Cross(d, Vector3.forward) * 3, 0.05f, 0.14f, Hdr(Color.Lerp(c, Color.white, 0.5f), 3)); }
        }

        // Launched, opened, collapsed. The collapse implodes to a point, then bursts: a flash, rings, motes and sparks
        // flung out, a shockwave through the picture, a kick and a shake, all bigger at each level.
        public void WellEvent(Ev ev)
        {
            if (V == null) return;
            var c = S.Lin(SUB_LOOK["well"].tint); var hot = Color.Lerp(c, Color.white, 0.6f); var at = W(ev.x, ev.y, 0.2f);
            switch (ev.type)
            {
                case "wellLaunch":
                    V.flash.Emit(at, Vector3.zero, 0.8f, 0.06f, Hdr(hot, 4)); V.ringIn.Emit(at, Vector3.zero, 1.4f, 0.14f, Hdr(c, 2.5f));
                    break;
                case "wellOpen":
                {
                    float L = (float)ev.level, r = (float)ev.r;
                    V.flash.Emit(at, Vector3.zero, 1.6f + 0.3f * L, 0.08f, Hdr(Color.white, 6));
                    V.ringIn.Emit(at, Vector3.zero, r * 2.6f, 0.3f, Hdr(hot, 2.5f));
                    V.ringIn.Emit(at, Vector3.zero, r * 1.6f, 0.22f, Hdr(Color.white, 2));
                    if (FxCfg.Distortion) V.hazeIn.Emit(at, Vector3.zero, r * 2.2f, 0.35f, new Color(1, 1, 1, 0.9f));
                    for (int i = 0, n = Count(24 + 10 * L); i < n; i++) { var d = Random.onUnitSphere; d.z *= 0.3f; V.mote.Emit(at + d * r, -d * r * 3.2f, 0.09f, 0.3f, Hdr(Random.value < 0.4f ? Color.white : c, 3.5f)); }
                    V.lights.Flash(at, hot, 4 + 1.5f * L, 4 + L, 0.25f);
                    view.Kick(0.3f + 0.12f * L); view.ShakeAtLeast(0.12f + 0.06f * L);
                    break;
                }
                case "wellCollapse":
                {
                    float L = Mathf.Max(0, (float)ev.level - 1), r = (float)ev.r, k = 1 + 0.3f * L;
                    // the last instant of the implosion, then the burst
                    V.ringIn.Emit(at, Vector3.zero, r * 1.4f, 0.1f, Hdr(Color.white, 3));
                    V.flash.Emit(at, Vector3.zero, r * 2.2f, 0.1f, Hdr(Color.white, 8));
                    V.glow.Emit(at, Vector3.zero, r * 3.2f, 0.35f, Hdr(c, 2.4f));
                    V.ring.Emit(at, Vector3.zero, r * 2.6f, 0.32f, Hdr(Color.white, 3));
                    V.ring.Emit(at, Vector3.zero, r * 2.0f, 0.42f, Hdr(c, 3));
                    for (int i = 0, n = Count(30 + 14 * L); i < n; i++)
                    {
                        var d = Random.onUnitSphere;
                        V.mote.Emit(at, d * (6 + 4 * r) * (0.6f + 0.6f * Random.value), 0.1f + 0.05f * Random.value, 0.45f + Random.value * 0.4f, Hdr(Random.value < 0.35f ? Color.white : c, 3.5f));
                        if (i % 2 == 0) V.spark.Emit(at, d * (10 + 6 * r), 0.05f, 0.35f + Random.value * 0.3f, Hdr(hot, 4));
                    }
                    if (FxCfg.Distortion) for (int i = 0; i < 2 + (int)L; i++) V.haze.Emit(at, Vector3.zero, r * (2.5f + 1.5f * i), 0.35f + 0.1f * i, new Color(1, 1, 1, 0.9f));
                    V.lights.Flash(at, hot, 8 * k, r * 3.5f, 0.35f);
                    var fl = FloorUnder(ev.x, ev.y, 2.5);
                    if (fl != null)
                    {
                        GroundRing(ev.x, fl.Value, Color.white, 0.3f, r * 1.9f, 0.35f, 0.7f); GroundRing(ev.x, fl.Value, c, 0.2f, r * 1.5f, 0.3f, 0.85f);
                        DustWave(ev.x, fl.Value, 0.6f + 0.2f * L);
                        if (L >= 2) Debris(ev.x, fl.Value + 0.3, 6 + 4 * L, new Color(0.5f, 0.52f, 0.56f), 6 + 2 * L);
                    }
                    view.Shockwave(ev.x, ev.y, Mathf.Min(1.3f, 0.6f + 0.2f * L), 0.55f);
                    view.Kick(Mathf.Min(1.3f, 0.5f + 0.2f * L)); view.ShakeAtLeast(Mathf.Min(0.8f, 0.3f + 0.12f * L));
                    view.Flash(Color.Lerp(c, Color.white, 0.6f), 0.18f + 0.08f * L);
                    break;
                }
            }
        }
    }
}
