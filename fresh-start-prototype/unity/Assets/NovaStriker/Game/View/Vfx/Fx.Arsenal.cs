// Every weapon's own energy look (Projectiles: Energy): a muzzle flash, a body (core, glow, trail) and an impact
// shaped to the weapon, each with light, so no two read alike.
//   Nova:  plain shot, a gold plasma pellet; Lance, a spear of light wrapped in a spiral and shedding rings at
//          Level 3 (Rail); Volley, needles that twinkle; Arc, a pulsing lobbed sun spitting embers; Prism, a
//          glinting crystal shedding spectral chips (its shards glint in those colours). Charged releases flare
//          harder at each level; a Perfect Release bursts white with rings, a screen flash and a kick.
//          Scatter: a cone of sparks and a ring; Grenade: a blinking core spitting fuse sparks, its burst a fiery blast.
//   RAM:   the slug, a dense pale round pushing shock rings; the Breach Shot, a plasma slug shedding rings with a
//          heat wake and a light, fired with a shockwave and a kick.
//   Fix:   rivets, hot needles that clink in sparks; the Hot Rivet glows red, drips embers, hisses where it sticks.
//   Echo:  bolts, orange blades that cross-slash on impact; the tracer round trails rings; the rifle's shot races
//          with streaks and lights both ends; snares whirl; the glaive's crescent wave sheds light along its edge.
// Tracer keeps every shot a plain streak; Classic keeps the original looks.
using NovaStriker.Sim;
using UnityEngine;
using static NovaStriker.Sim.Cfg;

namespace NovaStriker.Game
{
    public sealed partial class Fx
    {
        static readonly Color[] SPECTRUM = { new Color(0.45f, 0.95f, 1f), new Color(1f, 0.5f, 0.95f), new Color(1f, 0.95f, 0.5f), new Color(0.6f, 1f, 0.65f) };
        static bool Energy => FxCfg.Projectiles == "energy";
        float Every(float secs) => Mathf.Clamp01(frameDt / secs);   // (a per-frame chance that fires about once every `secs`)
        static void Basis(Vector3 dir, out Vector3 u, out Vector3 n)
        {
            n = Vector3.Cross(Vector3.up, dir); if (n.sqrMagnitude < 1e-4f) n = Vector3.forward; n.Normalize();
            u = Vector3.Cross(dir, n).normalized;
        }

        // ---- Bodies (each frame, for each projectile in flight); true when drawn here ----
        bool WeaponBody(Projectile pr, string kind, Vector3 at, Vector3 dir, Vector3 vel, Color c, float trail)
        {
            if (!Energy || pr.deflected || pr.team == "e") return false;
            var hot = Color.Lerp(c, Color.white, 0.65f);
            float ph = Time.time * 28 + (pr.GetHashCode() & 15);
            switch (kind)
            {
                case "shot":
                    V.glow.Emit(at, Vector3.zero, 0.55f, 0.05f, Hdr(c, 1.8f));
                    V.flash.Emit(at, Vector3.zero, 0.3f, 0.03f, Hdr(hot, 4));
                    if (trail > 0) V.mote.Emit(at - dir * 0.12f, -vel * 0.05f + Rnd3() * 0.2f, 0.07f, trail * 0.8f, Hdr(c, 3));
                    return true;
                case "lance": case "rail":
                {
                    bool rail = kind == "rail"; float k = rail ? 1.5f : 1;
                    V.streak.Emit(at - dir * 0.2f, vel * 0.32f, 0.16f * k, 0.04f, Hdr(rail ? Color.white : hot, 4));
                    V.glow.Emit(at, Vector3.zero, 1.0f * k, 0.06f, Hdr(c, 2.2f));
                    // a double helix of light wound round the spear
                    Basis(dir, out var u, out var n); float rad = 0.2f * k;
                    for (int s = 0; s < 2; s++) { float a = ph + s * Mathf.PI; V.mote.Emit(at - dir * 0.15f + (u * Mathf.Cos(a) + n * Mathf.Sin(a)) * rad, -vel * 0.04f, 0.07f * k, 0.12f + trail * 0.6f, Hdr(s == 0 ? hot : c, 3.5f)); }
                    if (rail && Random.value < Every(0.05f)) V.ring.Emit(at - dir * 0.3f, -vel * 0.05f, 1.0f, 0.2f, Hdr(c, 2.5f));
                    if (Random.value < Every(rail ? 0.06f : 0.1f)) V.lights.Flash(at, Hdr(c, 1), rail ? 3 : 1.8f, rail ? 4 : 3, 0.12f);
                    if (FxCfg.Distortion && Random.value < (rail ? 0.6f : 0.3f)) V.haze.Emit(at - dir * 0.4f, Vector3.zero, 0.9f * k, 0.25f, new Color(1, 1, 1, 0.6f));
                    return true;
                }
                case "dart":
                    V.streak.Emit(at, vel * 0.22f, 0.05f, 0.035f, Hdr(hot, 4));
                    V.glow.Emit(at, Vector3.zero, 0.35f, 0.04f, Hdr(c, 1.6f));
                    if (Random.value < 0.5f) V.chip.Emit(at - dir * 0.1f, -vel * 0.04f + Rnd3() * 0.8f, 0.035f, 0.18f, Hdr(c, 3), Random.value * 6.3f);
                    return true;
                case "shell":
                {
                    float pulse = 1 + 0.25f * Mathf.Sin(ph * 1.3f);
                    V.glow.Emit(at, Vector3.zero, 1.0f * pulse, 0.05f, Hdr(c, 2.2f));
                    V.flash.Emit(at, Vector3.zero, 0.42f * pulse, 0.03f, Hdr(hot, 4));
                    V.ember.Emit(at - dir * 0.15f, -vel * 0.08f + Rnd3() * 1.4f, 0.06f, 0.4f + Random.value * 0.3f, Hdr(new Color(1, 0.6f, 0.25f), 3));
                    if (Random.value < Every(0.12f)) V.ring.Emit(at, Vector3.zero, 0.9f, 0.16f, Hdr(c, 2));
                    return true;
                }
                case "prism":
                    V.glow.Emit(at, Vector3.zero, 0.75f, 0.05f, Hdr(c, 1.8f));
                    V.flash.Emit(at, Vector3.zero, 0.45f + 0.35f * Random.value, 0.03f, Hdr(Color.white, 4));   // (it glints as it turns)
                    if (Random.value < 0.7f) V.chip.Emit(at, -vel * 0.05f + Rnd3() * 1.6f, 0.05f, 0.3f, Hdr(SPECTRUM[Random.Range(0, 4)], 3), Random.value * 6.3f);
                    return true;
                case "shard":
                {
                    var sc = SPECTRUM[pr.GetHashCode() & 3];
                    V.streak.Emit(at, vel * 0.18f, 0.05f, 0.035f, Hdr(Color.Lerp(sc, Color.white, 0.4f), 4));
                    if (Random.value < 0.5f) V.flash.Emit(at, Vector3.zero, 0.3f, 0.03f, Hdr(sc, 4));
                    return true;
                }
                case "grenade": case "bomblet":
                {
                    if (pr.rest && Random.value < 0.6f) return true;
                    bool g = kind == "grenade"; float fuse = g ? Mathf.Clamp01((float)pr.ttl / 60) : 1;
                    float blink = Mathf.Sin(Time.time * (g ? 10 + 30 * (1 - fuse) : 20)) > 0 ? 1 : 0.35f;   // (faster as the fuse runs down)
                    V.glow.Emit(at, Vector3.zero, (g ? 0.7f : 0.4f) * (0.8f + 0.4f * blink), 0.04f, Hdr(c, 2 * blink));
                    V.spark.Emit(at + Vector3.up * 0.12f, Rnd3() * 2 + Vector3.up * 2.5f, 0.025f, 0.15f + Random.value * 0.15f, Hdr(new Color(1, 0.8f, 0.5f), 4));
                    if (g && FxCfg.Smoke != "off" && Random.value < 0.3f) V.smoke.Emit(at, Rnd3() * 0.2f + Vector3.up * 0.3f, 0.25f, 0.8f, new Color(0.75f, 0.75f, 0.77f, 0.4f), Random.value * 6.3f);
                    return true;
                }
                case "slug":
                    V.streak.Emit(at - dir * 0.1f, vel * 0.18f, 0.22f, 0.04f, Hdr(hot, 3.5f));
                    V.glow.Emit(at, Vector3.zero, 0.6f, 0.04f, Hdr(c, 1.4f));
                    if (Random.value < Every(0.07f)) V.ring.Emit(at + dir * 0.2f, Vector3.zero, 0.55f, 0.12f, Hdr(Color.white * 0.7f, 1.6f));   // (the air it shoves aside)
                    if (FxCfg.Distortion && Random.value < 0.25f) V.haze.Emit(at, Vector3.zero, 0.7f, 0.2f, new Color(1, 1, 1, 0.5f));
                    return true;
                case "breach":
                {
                    float k = 1 + 0.25f * (float)pr.level;
                    V.glow.Emit(at, Vector3.zero, 1.6f * k, 0.06f, Hdr(c, 2.5f));
                    V.streak.Emit(at - dir * 0.2f, vel * 0.25f, 0.3f * k, 0.05f, Hdr(hot, 4));
                    Basis(dir, out var u, out var n);
                    for (int s = 0; s < 3; s++) { float a = ph * 0.8f + s * 2.1f; V.mote.Emit(at - dir * 0.3f + (u * Mathf.Cos(a) + n * Mathf.Sin(a)) * 0.35f * k, -vel * 0.06f, 0.09f, 0.18f + trail * 0.6f, Hdr(c, 3.5f)); }
                    if (Random.value < Every(0.045f)) V.ring.Emit(at - dir * 0.4f, -vel * 0.04f, 1.3f * k, 0.22f, Hdr(c, 2.5f));
                    if (Random.value < Every(0.06f)) V.lights.Flash(at, Hdr(c, 1), 3 * k, 4.5f, 0.12f);
                    if (FxCfg.Distortion) V.haze.Emit(at - dir * 0.5f, Vector3.zero, 1.3f * k, 0.3f, new Color(1, 1, 1, 0.65f));
                    return true;
                }
                case "rivet":
                    V.streak.Emit(at, vel * 0.2f, 0.045f, 0.035f, Hdr(hot, 4));
                    if (Random.value < 0.25f) V.spark.Emit(at, -vel * 0.05f + Rnd3() * 1.5f, 0.02f, 0.12f, Hdr(hot, 4));
                    return true;
                case "hotRivet":
                {
                    var red = new Color(1, 0.38f, 0.12f);
                    if (pr.stuck != null)
                    {   // stuck fast and hissing until it bursts
                        float k = 1 + 0.5f * Mathf.Sin(Time.time * (12 + 40 * Mathf.Clamp01(1 - (float)pr.stuck.t / 60)));
                        V.glow.Emit(at, Vector3.zero, 0.6f * k, 0.04f, Hdr(red, 2.5f));
                        if (Random.value < 0.6f) V.spark.Emit(at, Rnd3() * 4 + Vector3.up * 1.5f, 0.025f, 0.15f + Random.value * 0.2f, Hdr(new Color(1, 0.7f, 0.4f), 4));
                        if (FxCfg.Distortion && Random.value < 0.3f) V.haze.Emit(at, Vector3.up * 0.6f, 0.6f, 0.3f, new Color(1, 1, 1, 0.5f));
                        return true;
                    }
                    V.streak.Emit(at, vel * 0.22f, 0.09f, 0.04f, Hdr(Color.Lerp(red, Color.white, 0.4f), 5));
                    V.glow.Emit(at, Vector3.zero, 0.55f, 0.04f, Hdr(red, 2));
                    if (Random.value < 0.6f) V.ember.Emit(at, -vel * 0.04f + Vector3.down * 0.6f + Rnd3() * 0.4f, 0.05f, 0.4f, Hdr(red, 3));
                    if (FxCfg.Distortion && Random.value < 0.3f) V.haze.Emit(at, Vector3.zero, 0.5f, 0.2f, new Color(1, 1, 1, 0.5f));
                    return true;
                }
                case "bolt":
                {
                    Basis(dir, out var u, out _);
                    V.streak.Emit(at, vel * 0.22f, 0.09f, 0.04f, Hdr(hot, 4));
                    V.streak.Emit(at - dir * 0.12f + u * 0.1f, vel * 0.15f, 0.04f, 0.04f, Hdr(c, 3)); V.streak.Emit(at - dir * 0.12f - u * 0.1f, vel * 0.15f, 0.04f, 0.04f, Hdr(c, 3));
                    V.glow.Emit(at, Vector3.zero, 0.5f, 0.04f, Hdr(c, 1.6f));
                    return true;
                }
                case "tracer":
                    V.streak.Emit(at, vel * 0.2f, 0.06f, 0.04f, Hdr(hot, 4));
                    if (Random.value < Every(0.05f)) V.ring.Emit(at, -vel * 0.02f, 0.5f, 0.2f, Hdr(c, 2.5f));
                    V.glow.Emit(at, Vector3.zero, 0.45f, 0.04f, Hdr(c, 1.5f));
                    return true;
                case "snare":
                {
                    Basis(dir.sqrMagnitude > 0 ? dir : Vector3.right, out var u, out var n);
                    for (int s = 0; s < 3; s++) { float a = ph * 0.6f + s * 2.094f; V.flash.Emit(at + (u * Mathf.Cos(a) + n * Mathf.Sin(a) * 0.4f) * 0.25f, Vector3.zero, 0.22f, 0.04f, Hdr(s == 0 ? Color.white : c, 4)); }
                    V.glow.Emit(at, Vector3.zero, 0.6f, 0.04f, Hdr(c, 1.2f));
                    if (trail > 0 && Random.value < 0.6f) V.mote.Emit(at, -vel * 0.03f + Rnd3() * 0.3f, 0.05f, trail, Hdr(c, 2.5f));
                    return true;
                }
                case "wave":
                {
                    // light streaming off the crescent's edge (the mesh: an arc of radius 0.86 bulging along its flight)
                    var f = Level.Frame(pr.x); var N = new Vector3((float)f.nx, 0, (float)f.nz);
                    var side = Vector3.Cross(dir, N).normalized;
                    for (int i = 0, n = Count(5); i < n; i++)
                    {
                        float a = Mathf.PI / 2 + (Random.value * 2 - 1) * 1.1f;
                        var p = at + dir * (Mathf.Sin(a) * 0.86f - 0.72f) + side * (Mathf.Cos(a) * 0.86f);
                        V.mote.Emit(p, -vel * 0.12f + Rnd3() * 0.5f, 0.07f + 0.04f * Random.value, 0.18f + trail * 0.5f, Hdr(Random.value < 0.35f ? Color.white : c, 3.5f));
                    }
                    V.glow.Emit(at + dir * 0.1f, Vector3.zero, 1.6f, 0.05f, Hdr(c, 1.2f));
                    if (FxCfg.Distortion && Random.value < 0.4f) V.haze.Emit(at, Vector3.zero, 1.4f, 0.25f, new Color(1, 1, 1, 0.55f));
                    if (Random.value < Every(0.1f)) V.lights.Flash(at, Hdr(c, 1), 2, 3.5f, 0.12f);
                    return true;
                }
            }
            return false;
        }

        // ---- Muzzles ("shot" events): the flash shaped to the weapon and its charge ----
        bool WeaponMuzzle(Ev ev, string css)
        {
            if (!Energy || ev.p == null) return false;
            var p = ev.p; double ax = ev.ax != 0 || ev.ay != 0 ? ev.ax : p.aimX, ay = ev.ax != 0 || ev.ay != 0 ? ev.ay : p.aimY;
            var at = W(ev.x, ev.y, 0.3f); var dir = S.Dir(ev.x, ax, ay); if (dir.sqrMagnitude < 1e-4f) dir = S.Dir(ev.x, p.facing, 0); dir.Normalize();
            float L = (float)ev.level;
            var c = S.Lin(css); var hot = Color.Lerp(c, Color.white, 0.6f);
            void Cone(int n, float speed, float spread, float size, float life, Color col, FxLayer layer)
            {
                for (int i = 0, m = Count(n); i < m; i++) layer.Emit(at, (dir + Rnd3() * spread).normalized * speed * (0.6f + 0.6f * Random.value), size, life * (0.6f + 0.6f * Random.value), Hdr(col, 4));
            }
            if (ev.cannon)
            {
                var rc = S.Lin(CHARS["ram"].energy);
                if (L == 0)
                {   // the slug: a hard, pale blast with a puff of smoke
                    V.flash.Emit(at, Vector3.zero, 1.4f, 0.06f, Hdr(Color.white, 5)); V.ring.Emit(at, Vector3.zero, 1.2f, 0.12f, Hdr(new Color(0.8f, 0.9f, 1), 2.5f));
                    Cone(10, 14, 0.35f, 0.04f, 0.2f, new Color(0.85f, 0.92f, 1), V.spark);
                    V.lights.Flash(at, new Color(0.85f, 0.92f, 1), 4, 4, 0.1f); view.Kick(0.25f);
                }
                else
                {   // the Breach Shot: a plasma blast, a shockwave through the picture, smoke rolling off the barrel
                    float k = 1 + 0.35f * L;
                    V.flash.Emit(at, Vector3.zero, 2.2f * k, 0.08f, Hdr(Color.white, 6)); V.glow.Emit(at, Vector3.zero, 2.6f * k, 0.2f, Hdr(rc, 2.2f));
                    V.ring.Emit(at, Vector3.zero, 1.8f * k, 0.16f, Hdr(Color.white, 3)); V.ring.Emit(at + dir * 0.4f, dir * 3, 1.4f * k, 0.22f, Hdr(rc, 3));
                    Cone(8 + 4 * (int)L, 30, 0.12f, 0.12f, 0.12f, Color.Lerp(rc, Color.white, 0.5f), V.streak);
                    Cone(14 + 6 * (int)L, 16, 0.4f, 0.05f, 0.3f, rc, V.spark);
                    if (FxCfg.Distortion) V.haze.Emit(at, Vector3.zero, 2.4f * k, 0.3f, new Color(1, 1, 1, 0.8f));
                    if (FxCfg.Smoke != "off") for (int i = 0, n = Count(4 + 2 * L); i < n; i++) V.smokeDark.Emit(at, Rnd3() * 0.8f + Vector3.up * 0.6f + dir * 0.5f, 0.7f, 1.8f + Random.value, new Color(1, 1, 1, 0.55f));
                    V.lights.Flash(at, Color.Lerp(rc, Color.white, 0.3f), 6 * k, 6 * k, 0.18f);
                    view.Shockwave(ev.x, ev.y, Mathf.Min(1, 0.35f + 0.2f * L), 0.4f);
                    view.Kick(Mathf.Min(1.2f, 0.45f + 0.2f * L)); view.ShakeAtLeast(Mathf.Min(0.6f, 0.15f + 0.1f * L));
                }
                return true;
            }
            if (ev.rivet)
            {
                if (L == 0) { V.flash.Emit(at, Vector3.zero, 0.45f, 0.04f, Hdr(hot, 4)); Cone(3, 8, 0.4f, 0.02f, 0.12f, Color.white, V.spark); }
                else
                {
                    var red = new Color(1, 0.4f, 0.15f);
                    V.flash.Emit(at, Vector3.zero, 0.9f, 0.06f, Hdr(Color.Lerp(red, Color.white, 0.5f), 5)); V.ring.Emit(at, Vector3.zero, 0.8f, 0.12f, Hdr(red, 2.5f));
                    Cone(8, 9, 0.5f, 0.05f, 0.5f, red, V.ember);
                    if (FxCfg.Distortion) V.haze.Emit(at, Vector3.zero, 1.0f, 0.25f, new Color(1, 1, 1, 0.6f));
                    V.lights.Flash(at, red, 2.5f, 3, 0.1f);
                }
                return true;
            }
            if (ev.bolt)
            {
                V.flash.Emit(at, Vector3.zero, 0.7f, 0.05f, Hdr(hot, 5)); Cone(3, 22, 0.1f, 0.06f, 0.06f, c, V.streak);
                return true;
            }
            // Nova: a plain shot (level 0), a charged one (lance; rail), or a loaded attachment's release
            string a = ev.attach ?? (L >= 2 ? "rail" : L >= 1 ? "lance" : "shot");
            var ac = ev.attach != null && ATTACH_LOOK.TryGetValue(ev.attach, out var look) ? S.Lin(look.tint) : c; var ahot = Color.Lerp(ac, Color.white, 0.6f);
            float big = 1 + 0.35f * L;
            switch (a)
            {
                case "shot": V.flash.Emit(at, Vector3.zero, 0.6f, 0.04f, Hdr(ahot, 5)); V.ring.Emit(at, Vector3.zero, 0.45f, 0.08f, Hdr(ac, 2)); return true;
                case "lance": case "rail":
                    // a spike of light along the aim
                    V.flash.Emit(at, Vector3.zero, 1.0f * big, 0.06f, Hdr(ahot, 5));
                    Cone(6 + 3 * (int)L, 34, 0.06f, 0.1f * big, 0.08f, ahot, V.streak);
                    V.ring.Emit(at + dir * 0.3f, dir * 4, 0.9f * big, 0.14f, Hdr(ac, 3));
                    if (a == "rail" || L >= 3) { V.ring.Emit(at + dir * 0.9f, dir * 6, 1.2f * big, 0.18f, Hdr(Color.white, 2.5f)); if (FxCfg.Distortion) V.haze.Emit(at, Vector3.zero, 1.6f * big, 0.25f, new Color(1, 1, 1, 0.8f)); }
                    break;
                case "volley":
                {   // a fan of little flashes, one per dart
                    Basis(dir, out var u, out _);
                    for (int i = -2; i <= 2; i++) V.flash.Emit(at + (dir * 0.25f + u * (0.18f * i)), Vector3.zero, 0.45f, 0.05f, Hdr(ahot, 4));
                    Cone(10, 9, 0.6f, 0.035f, 0.25f, ac, V.chip);
                    break;
                }
                case "arc":
                    // a lob: a puff of sun-orange plasma and embers spilling upward
                    V.flash.Emit(at, Vector3.zero, 1.1f * big, 0.07f, Hdr(ahot, 5)); V.glow.Emit(at, Vector3.zero, 1.6f * big, 0.18f, Hdr(ac, 2));
                    V.ring.Emit(at, Vector3.zero, 1.0f * big, 0.16f, Hdr(ac, 2.5f));
                    for (int i = 0, n = Count(10 + 4 * L); i < n; i++) V.ember.Emit(at, (dir + Vector3.up * 0.8f + Rnd3() * 0.6f).normalized * (3 + Random.value * 4), 0.06f, 0.5f + Random.value * 0.4f, Hdr(new Color(1, 0.6f, 0.25f), 3));
                    break;
                case "prism":
                    // a crystal flash: a hard star and spectral chips
                    V.flash.Emit(at, Vector3.zero, 1.3f * big, 0.07f, Hdr(Color.white, 6));
                    V.ring.Emit(at, Vector3.zero, 0.9f * big, 0.14f, Hdr(ahot, 2.5f));
                    for (int i = 0, n = Count(10 + 4 * L); i < n; i++) V.chip.Emit(at, (dir + Rnd3() * 0.9f).normalized * (4 + Random.value * 5), 0.05f, 0.3f, Hdr(SPECTRUM[i & 3], 3.5f), Random.value * 6.3f);
                    break;
            }
            if (L > 0)
            {
                V.glow.Emit(at, Vector3.zero, 1.4f * big, 0.14f, Hdr(ac, 2));
                V.lights.Flash(at, Color.Lerp(ac, Color.white, 0.3f), 3 * big, 3.5f * big, 0.12f);
                view.Kick(Mathf.Min(1, 0.15f + 0.12f * L)); if (L >= 2) view.ShakeAtLeast(0.05f * L);
            }
            if (ev.over) V.ring.Emit(at, Vector3.zero, 1.8f * big, 0.2f, Hdr(Color.white, 2.5f));   // (an Aegis Overcharge release)
            return true;
        }

        // A Perfect Release: everything at once
        void PerfectRelease(Ev ev)
        {
            var c = S.Lin(ev.attach != null && ATTACH_LOOK.TryGetValue(ev.attach, out var l) ? l.tint : NOVA_GOLD); var at = W(ev.x, ev.y, 0.35f);
            V.flash.Emit(at, Vector3.zero, 2.6f, 0.1f, Hdr(Color.white, 7));
            V.ring.Emit(at, Vector3.zero, 2.4f, 0.22f, Hdr(Color.white, 3)); V.ring.Emit(at, Vector3.zero, 1.6f, 0.3f, Hdr(c, 3));
            for (int i = 0, n = Count(20); i < n; i++) V.chip.Emit(at, Random.onUnitSphere * (5 + Random.value * 6), 0.06f, 0.35f, Hdr(i % 3 == 0 ? Color.white : c, 4), Random.value * 6.3f);
            V.lights.Flash(at, Color.Lerp(c, Color.white, 0.4f), 8, 7, 0.25f);
            view.Flash(Color.Lerp(c, Color.white, 0.6f), 0.3f); view.Kick(0.8f); view.Shockwave(ev.x, ev.y, 0.5f, 0.4f);
        }

        // ---- Impacts ("hit" and "projWall" events that carry the shot); true when drawn here ----
        bool WeaponImpact(Projectile pr, double x, double y, bool wall)
        {
            if (!Energy || pr == null || pr.deflected || pr.team == "e") return false;
            string kind = pr.kind; var c = S.Lin(TrailColor(pr)); var hot = Color.Lerp(c, Color.white, 0.6f);
            var at = W(x, y, 0.4f); var dir = S.Dir(x, pr.vx, pr.vy); if (dir.sqrMagnitude < 1e-4f) dir = Vector3.up; dir.Normalize();
            void Spray(int n, Vector3 d, float spread, float speed, FxLayer layer, float size, float life, Color col)
            {
                for (int i = 0, m = Count(n); i < m; i++) layer.Emit(at, (d + Rnd3() * spread).normalized * speed * (0.5f + 0.7f * Random.value), size, life * (0.6f + 0.6f * Random.value), Hdr(col, 4));
            }
            var back = wall ? -dir : dir;   // (off a wall it sprays back; through an enemy it carries on)
            switch (kind)
            {
                case "shot": case "std":
                    V.flash.Emit(at, Vector3.zero, 0.8f, 0.06f, Hdr(hot, 5)); V.ring.Emit(at, Vector3.zero, 0.7f, 0.12f, Hdr(c, 2.5f));
                    Spray(7, back, 1, 8, V.spark, 0.04f, 0.3f, hot);
                    return true;
                case "lance": case "rail":
                {
                    bool rail = kind == "rail"; float k = rail ? 1.5f : 1;
                    V.flash.Emit(at, Vector3.zero, 1.3f * k, 0.07f, Hdr(Color.white, 6)); V.ring.Emit(at, Vector3.zero, 1.2f * k, 0.16f, Hdr(c, 3));
                    Spray(8, back, 0.25f, 22, V.streak, 0.07f * k, 0.08f, hot);   // (the spear's light punches through)
                    Spray(12, back, 0.8f, 10, V.spark, 0.04f, 0.35f, hot);
                    V.lights.Flash(at, Hdr(c, 1), 4 * k, 4 * k, 0.15f);
                    if (rail && FxCfg.Distortion) V.haze.Emit(at, Vector3.zero, 1.8f, 0.25f, new Color(1, 1, 1, 0.7f));
                    return true;
                }
                case "dart":
                    V.flash.Emit(at, Vector3.zero, 0.55f, 0.05f, Hdr(hot, 5)); V.ring.Emit(at, Vector3.zero, 0.5f, 0.1f, Hdr(c, 2.5f));
                    Spray(5, back, 1, 6, V.chip, 0.04f, 0.25f, c);
                    return true;
                case "prism": case "shard":
                    V.flash.Emit(at, Vector3.zero, kind == "prism" ? 1.2f : 0.7f, 0.06f, Hdr(Color.white, 6));
                    V.ring.Emit(at, Vector3.zero, kind == "prism" ? 1.0f : 0.6f, 0.12f, Hdr(hot, 2.5f));
                    for (int i = 0, n = Count(kind == "prism" ? 12 : 6); i < n; i++) V.chip.Emit(at, (back + Rnd3()).normalized * (4 + Random.value * 5), 0.05f, 0.3f, Hdr(SPECTRUM[i & 3], 3.5f), Random.value * 6.3f);
                    return true;
                case "pellet":
                    V.flash.Emit(at, Vector3.zero, 0.45f, 0.04f, Hdr(hot, 5)); Spray(4, back, 1, 7, V.spark, 0.035f, 0.25f, hot);
                    return true;
                case "slug":
                    V.flash.Emit(at, Vector3.zero, 1.3f, 0.07f, Hdr(Color.white, 5)); V.ring.Emit(at, Vector3.zero, 1.4f, 0.16f, Hdr(Color.white, 2));
                    Spray(14, -dir, 1.1f, 9, V.spark, 0.045f, 0.4f, new Color(0.9f, 0.95f, 1));
                    if (wall) Debris(x, y, 4, new Color(0.55f, 0.57f, 0.6f), 5);
                    V.lights.Flash(at, new Color(0.85f, 0.92f, 1), 3, 3.5f, 0.12f); view.Kick(0.25f);
                    return true;
                case "breach":
                {
                    float k = 1 + 0.25f * (float)pr.level;
                    V.flash.Emit(at, Vector3.zero, 2.0f * k, 0.08f, Hdr(Color.white, 6)); V.glow.Emit(at, Vector3.zero, 2.4f * k, 0.2f, Hdr(c, 2.2f));
                    V.ring.Emit(at, Vector3.zero, 2.0f * k, 0.2f, Hdr(c, 3)); V.ring.Emit(at, Vector3.zero, 1.4f * k, 0.14f, Hdr(Color.white, 2.5f));
                    Spray(10, back, 0.3f, 24, V.streak, 0.1f, 0.1f, hot); Spray(20, back, 1, 11, V.spark, 0.05f, 0.4f, hot);
                    if (FxCfg.Distortion) V.haze.Emit(at, Vector3.zero, 2.2f * k, 0.3f, new Color(1, 1, 1, 0.8f));
                    V.lights.Flash(at, Color.Lerp(c, Color.white, 0.3f), 6 * k, 5 * k, 0.2f);
                    view.Kick(0.45f); view.Shockwave(x, y, 0.4f, 0.35f);
                    return true;
                }
                case "rivet": case "sentryBolt":
                    V.flash.Emit(at, Vector3.zero, 0.4f, 0.04f, Hdr(Color.white, 5)); Spray(5, -dir, 1.2f, 7, V.spark, 0.025f, 0.35f, Color.white);   // (a metallic clink)
                    return true;
                case "bolt":
                {   // a cross-slash of light
                    Basis(dir, out var u, out var n);
                    var d1 = (u + n * 0.3f).normalized; var d2 = (u - n * 0.3f).normalized;
                    V.streak.Emit(at - d1 * 0.4f, d1 * 18, 0.08f, 0.06f, Hdr(hot, 5)); V.streak.Emit(at + d2 * 0.4f, -d2 * 18, 0.08f, 0.06f, Hdr(hot, 5));
                    V.flash.Emit(at, Vector3.zero, 0.8f, 0.06f, Hdr(hot, 5)); Spray(6, back, 1, 7, V.spark, 0.035f, 0.3f, c);
                    return true;
                }
                case "tracer":
                    V.ring.Emit(at, Vector3.zero, 1.0f, 0.2f, Hdr(c, 3)); V.flash.Emit(at, Vector3.zero, 0.7f, 0.06f, Hdr(hot, 5)); Spray(5, back, 1, 6, V.spark, 0.03f, 0.3f, hot);
                    return true;
                case "wave":
                    V.flash.Emit(at, Vector3.zero, 1.4f, 0.07f, Hdr(Color.white, 5)); V.ring.Emit(at, Vector3.zero, 1.3f, 0.16f, Hdr(c, 3));
                    Spray(8, dir, 0.35f, 20, V.streak, 0.08f, 0.08f, hot); Spray(10, dir, 1, 8, V.spark, 0.04f, 0.35f, c);
                    V.lights.Flash(at, Hdr(c, 1), 3, 3.5f, 0.12f);
                    return true;
            }
            return false;
        }

        // ---- Other weapon moments (on top of the classic reactions, or in place of them where Fx.Cine says) ----
        void ArsenalEvent(Ev ev)
        {
            if (!Energy) return;
            switch (ev.type)
            {
                case "perfectRelease": PerfectRelease(ev); break;
                case "split":
                {
                    var at = W(ev.x, ev.y, 0.4f);
                    V.flash.Emit(at, Vector3.zero, 1.3f, 0.07f, Hdr(Color.white, 6)); V.ring.Emit(at, Vector3.zero, 1.1f, 0.14f, Hdr(S.Lin(ATTACH_LOOK["prism"].tint), 3));
                    for (int i = 0, n = Count(14); i < n; i++) V.chip.Emit(at, Random.onUnitSphere * (4 + Random.value * 5), 0.05f, 0.3f, Hdr(SPECTRUM[i & 3], 3.5f), Random.value * 6.3f);
                    break;
                }
                case "burst":
                {   // Scatter: a cone of hot sparks and a ring at the muzzle, wider and brighter when charged
                    var c = S.Lin(SUB_LOOK["scatter"].tint); var at = W(ev.x, ev.y, 0.3f); var dir = S.Dir(ev.x, ev.ax, ev.ay).normalized; float L = (float)ev.level;
                    V.flash.Emit(at, Vector3.zero, 1.0f + 0.3f * L, 0.06f, Hdr(Color.white, 5)); V.ring.Emit(at + dir * 0.3f, dir * 3, 0.9f + 0.3f * L, 0.12f, Hdr(c, 3));
                    for (int i = 0, n = Count(16 + 8 * L); i < n; i++) V.spark.Emit(at, (dir + Rnd3() * (0.45f + 0.1f * L)).normalized * (12 + Random.value * 10), 0.04f, 0.15f + Random.value * 0.15f, Hdr(Color.Lerp(c, Color.white, Random.value * 0.6f), 4));
                    if (FxCfg.Smoke != "off") for (int i = 0, n = Count(2 + L); i < n; i++) V.smokeDark.Emit(at + dir * 0.4f, dir * 1.2f + Rnd3() * 0.6f + Vector3.up * 0.4f, 0.5f, 1.2f + Random.value, new Color(1, 1, 1, 0.45f));
                    V.lights.Flash(at, Color.Lerp(c, Color.white, 0.4f), 3 + L, 3.5f + L, 0.1f);
                    view.Kick(0.2f + 0.1f * L);
                    break;
                }
                case "rivetStick":
                {
                    var at = W(ev.x, ev.y, 0.4f); var red = new Color(1, 0.4f, 0.15f);
                    V.flash.Emit(at, Vector3.zero, 0.7f, 0.06f, Hdr(Color.Lerp(red, Color.white, 0.5f), 5));
                    for (int i = 0, n = Count(10); i < n; i++) V.spark.Emit(at, Random.onUnitSphere * (4 + Random.value * 4) + Vector3.up * 2, 0.03f, 0.3f, Hdr(new Color(1, 0.7f, 0.4f), 4));
                    if (FxCfg.Smoke != "off") V.smokeDark.Emit(at, Vector3.up * 0.6f, 0.4f, 1.2f, new Color(1, 1, 1, 0.4f));
                    break;
                }
                case "snipe":
                {   // Echo's rifle: streaks racing the length of the shot, lights at both ends, sparks where it ends
                    var c = S.Lin(ECHO_ORANGE); float f = (float)ev.f; bool full = ev.full;
                    var a = W(ev.x0, ev.y0, 0.25f); var b = W(ev.x1, ev.y1, 0.25f); var d = b - a; float len = d.magnitude; if (len < 0.1f) break; d /= len;
                    for (int i = 0, n = Count(Mathf.Min(40, len * (1.5f + 2 * f))); i < n; i++)
                        V.streak.Emit(a + d * (Random.value * len), d * (40 + Random.value * 30), 0.04f + 0.05f * f, 0.06f + Random.value * 0.06f, Hdr(Random.value < 0.5f ? Color.white : c, 4));
                    V.flash.Emit(a, Vector3.zero, 0.9f + f, 0.06f, Hdr(Color.white, 5)); V.lights.Flash(a, Color.Lerp(c, Color.white, 0.4f), 3 + 3 * f, 4, 0.1f);
                    V.flash.Emit(b, Vector3.zero, 0.8f + f, 0.07f, Hdr(Color.white, 5)); V.ring.Emit(b, Vector3.zero, 0.8f + 0.8f * f, 0.16f, Hdr(c, 3));
                    for (int i = 0, n = Count(8 + 10 * f); i < n; i++) V.spark.Emit(b, (-d + Rnd3()).normalized * (6 + Random.value * 8), 0.035f, 0.35f, Hdr(Color.Lerp(c, Color.white, 0.5f), 4));
                    V.lights.Flash(b, c, 3 + 3 * f, 4, 0.14f);
                    if (full && FxCfg.Distortion) for (int i = 0; i < 4; i++) V.haze.Emit(a + d * (len * (i + 0.5f) / 4), Vector3.up * 0.4f, 1.0f, 0.35f, new Color(1, 1, 1, 0.6f));
                    break;
                }
            }
        }
    }
}
