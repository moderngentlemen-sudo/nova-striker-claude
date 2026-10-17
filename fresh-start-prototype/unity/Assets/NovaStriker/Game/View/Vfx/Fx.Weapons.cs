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
    }
}
