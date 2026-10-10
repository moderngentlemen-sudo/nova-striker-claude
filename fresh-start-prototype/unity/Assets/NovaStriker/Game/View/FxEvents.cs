// Event reactions (fx.js onEvent and the effects it starts): hits, guards, deaths, movement, the characters'
// kits, the bosses. Each module (charge, Aegis, beam, secondary weapons, ultimates, RAM, Fix) takes its own events.
using System.Collections.Generic;
using NovaStriker.Game.Three;
using NovaStriker.Sim;
using UnityEngine;
using static NovaStriker.Sim.Cfg;

namespace NovaStriker.Game
{
    public sealed partial class Fx
    {
        static string NOVA_GOLD => CHARS["nova"].energy;
        static string ECHO_ORANGE => CHARS["echo"].energy;
        const string VEIL_PALE = "#dcecff";
        const float PI = Mathf.PI;
        static float F(double v) => (float)v;
        static float Or(double v, double d) => (float)(v != 0 && !double.IsNaN(v) ? v : d);

        public void OnEvent(Ev ev, World world)
        {
            double previousDepth = effectDepth; effectDepth = ev.depth ?? 0;
            try {
            VfxEventCatalog.Observe(ev.type);
            string pc = ev.p != null ? CHARS[ev.p.@char].energy : "#ffffff";
            if (CineEvent(ev, world)) return;   // (the cinematic effects: Fx.Cine.cs)
            switch (ev.type)
            {
                case "hit":
                {
                    var op = ev.owner as Player;
                    string col = op != null ? CHARS[op.@char].energy : "#ffffff"; float d = Or(ev.dmg, 1);
                    if (FxCfg.Projectiles == "classic") {
                    Burst(ev.x, ev.y, col, (ev.heavy ? 18 : 9) + Mathf.Min(14, d * 2), (ev.heavy ? 9 : 6) + Mathf.Min(5, d * 0.5f), ev.heavy ? 0.5f : 0.35f, 0.3f);
                    if (ev.heavy || d >= 4) Sprite(ev.x, ev.y, "ring", col, 0.6f + Mathf.Min(1, d * 0.1f), 0.22f, 3);
                    if (ev.tier != 0) Sprite(ev.x, ev.y, "ring", "#ffffff", 1 + F(ev.tier) * 0.5f, 0.25f, 3.5f);
                    }
                    if (op != null && ev.source == "melee")
                    {
                        if (op.@char == "echo")
                        {
                            var ds = op.state == "dashslash" ? op.slash : null;
                            float rot = ds != null ? Mathf.Atan2(F(ds.dy), F(ds.dx)) : (S.Rnd() - 0.5f) * 1.2f + (S.Rnd() < 0.5f ? 0 : 0.5f);
                            SlashMark(ev.x, ev.y, "#fff1d6", 1.2f + Mathf.Min(1.4f, d * 0.18f), rot, ds != null ? 0.2f : 0.14f);
                            if (d >= 4) SlashMark(ev.x, ev.y, col, 1.6f + d * 0.12f, rot + 1.2f, 0.16f);
                        }
                        else Sprite(ev.x, ev.y, "star", "#fff4d6", 0.7f + Mathf.Min(1, d * 0.15f), 0.12f, 1.5f);
                    }
                    break;
                }
                case "blocked": Burst(ev.x, ev.y, "#cfe8ff", 8, 6, 0.3f, 0.25f); Sprite(ev.x, ev.y, "star", "#dff2ff", 0.6f, 0.15f, 1.2f); break;
                case "guardBreak": Burst(ev.x, ev.y, "#ffffff", 22, 10, 0.5f, 0.45f); Sprite(ev.x, ev.y, "ring", "#ffffff", 1.2f, 0.3f, 3); break;
                case "armorBreak": Burst(ev.x, ev.y + 0.4, "#e6e9f0", 26, 11, 0.55f, 0.6f, grav: 14); Sprite(ev.x, ev.y, "ring", HOSTILE, 1.5f, 0.35f, 3); break;
                case "armorHit": Burst(ev.x, ev.y, "#b9c3d6", 5, 4, 0.25f, 0.2f); break;
                case "enemyShot": if (ev.e != null) Sprite(ev.e.x+ev.e.facing*ev.e.w*0.55,ev.e.y+ev.e.h*0.65,"star",HOSTILE,ev.heavy?1.2f:0.65f,0.08f,1.2f); break;
                case "tracer": Sprite(ev.x,ev.y,"star",ECHO_ORANGE,0.6f,0.07f,1.2f); break;
                case "spinStun": Sprite(ev.x, ev.y + 0.5, "star", ECHO_ORANGE, 0.8f, 0.45f, 1.3f); Burst(ev.x, ev.y, ECHO_ORANGE, 10, 4, 0.3f, 0.5f); break;
                case "stagger": Sprite(ev.x, ev.y + 0.6, "star", "#fff4c2", 0.9f, 0.5f, 1.4f); Burst(ev.x, ev.y + 0.5, "#fff4c2", 12, 4, 0.3f, 0.6f); break;
                case "kill":
                    Burst(ev.x, ev.y, HOSTILE, 24, 9, 0.45f, 0.55f, grav: 6); Burst(ev.x, ev.y, "#ffffff", 10, 5, 0.3f, 0.3f);
                    if (ev.e != null && !ev.e.flier) Dust(ev.e.x, ev.e.y, ev.e.type == "brute" || ev.e.boss ? 0.9f : 0.3f);
                    break;
                case "parry":
                {
                    string col = ev.perfect ? "#fff6d8" : "#cfe8ff";
                    Sprite(ev.x, ev.y, "ring", col, ev.perfect ? 1.4f : 0.9f, ev.perfect ? 0.3f : 0.2f, ev.perfect ? 3.2f : 2.2f);
                    Sprite(ev.x, ev.y, "star", col, ev.perfect ? 1.6f : 0.9f, 0.25f, 1.3f);
                    Burst(ev.x, ev.y, ev.perfect ? pc : "#cfe8ff", ev.perfect ? 22 : 10, ev.perfect ? 10 : 6, 0.4f, 0.35f);
                    break;
                }
                case "parryFail": Burst(ev.x, ev.y, HOSTILE, 10, 5, 0.3f, 0.3f); break;
                case "playerHit": Burst(ev.x, ev.y, "#ffffff", 10, 6, 0.35f, 0.25f); break;
                case "intercept": Burst(ev.x, ev.y, "#ffd28a", 16, 8, 0.4f, 0.3f); Sprite(ev.x, ev.y, "star", "#ffe7b5", 0.9f, 0.18f, 1.5f); break;
                case "interceptFail": Burst(ev.x, ev.y, "#ffd28a", 5, 4, 0.2f, 0.15f); break;
                case "barrierBlock": Burst(ev.x, ev.y, "#ffd28a", 12, 6, 0.35f, 0.3f); break;
                case "erase": Burst(ev.x, ev.y, "#ffe2a8", 8, 5, 0.3f, 0.25f); break;
                case "amplify": Sprite(ev.x, ev.y, "ring", "#ffe2a8", 0.5f, 0.15f, 2); break;
                case "bulwark":
                    Sprite(ev.x + ev.ax * 1.2, ev.y + ev.ay * 1.2, "ring", NOVA_GOLD, 1.4f, 0.3f, 3);
                    Burst(ev.x + ev.ax * 1.5, ev.y + ev.ay * 1.5, NOVA_GOLD, 26, 10, 0.4f, 0.35f, dir: Mathf.Atan2(F(ev.ay), F(ev.ax)), spread: 1.6f);
                    break;
                case "boost": Sprite(ev.x, ev.y, "ring", "#ffe2a8", 1.2f, 0.25f, 2.5f); Burst(ev.x, ev.y, "#ffe2a8", 14, 8, 0.35f, 0.3f); break;
                case "vbStart": Burst(ev.p.x, ev.p.y + 0.8, pc, 6 + F(ev.tier) * 6, 4 + F(ev.tier) * 3, 0.35f, 0.25f); break;
                case "lashPull": case "lashZip": Burst(ev.e.x, ev.e.y + ev.e.h / 2, CHARS["echo"].energy, 14, 6, 0.35f, 0.3f); break;
                case "tag": Sprite(ev.x, ev.y + 0.3, "star", "#ffb347", 0.7f, 0.4f, 1.2f); break;
                case "land":
                    if (ev.p == null) break;
                    if (ev.p.state == "pound") break;
                    if (ev.vy < -16)
                    {
                        float k = Mathf.Min(1, F(-ev.vy - 16) / 12);
                        GroundRing(ev.p.x, ev.p.y, "#e6ecf2", 0.4f, 1.4f + k, 0.3f, 0.6f);
                        Dust(ev.p.x, ev.p.y, 0.45f + 0.45f * k, new[] { PI, 0 }, noRing: true);
                    }
                    else Dust(ev.p.x, ev.p.y, Mathf.Min(0.35f, 0.08f + F(-ev.vy) * 0.018f), new[] { PI, 0 }, noRing: true, op: 0.4f);
                    // Marksman Nova's skate blades strike sparks off the deck on a hard landing, both ways
                    if (ev.p.@char == "nova" && SETTINGS.novaKit == "marksman" && ev.vy < -12)
                    {
                        float k = Mathf.Min(1, F(-ev.vy - 12) / 14);
                        foreach (var a in new[] { 0.3f, PI - 0.3f }) view.sparks.Emit(ev.p.slot, ev.p.x, ev.p.y + 0.05, 4 + 8 * k, a, 3 + 4 * k, 0.6f, light: 0.8f + k, depth: (float)(ev.depth ?? 0), lane: ev.p.lane);
                    }
                    break;
                case "jump": case "djump": if (ev.p != null) Burst(ev.p.x, ev.p.y + 0.1, "#e8eef5", 5, 2.5f, 0.3f, 0.25f, dir: -PI / 2, spread: 2); break;
                case "walljump":
                {
                    double wx = ev.p.x - ev.dir * ev.p.w / 2;
                    Smoke(wx, ev.p.y + 0.3, "#a3abb5", ev.climb ? 4 : 6, 2.5f, 0.35f, 0.4f, dir: ev.dir > 0 ? 0 : PI, spread: 1.6f, op: 0.5f);
                    if (ev.p.@char == "nova") view.sparks.Emit(ev.p.slot, wx, ev.p.y + 0.1, 8, ev.dir > 0 ? 0.3f : PI - 0.3f, 5, 1.1f, depth: (float)(ev.depth ?? 0), lane: ev.p.lane);
                    Sprite(wx, ev.p.y + 0.5, "ring", "#ffffff", 0.35f, 0.14f, 2.4f);
                    break;
                }
                case "wallSlide": if (ev.on) Burst(ev.p.x + ev.dir * ev.p.w / 2, ev.p.y + ev.p.h * 0.8, "#e8eef5", 6, 2, 0.3f, 0.3f); break;
                case "dash":
                {
                    if (ev.p == null) break;
                    var p = ev.p; float L = F(ev.level), a = Mathf.Atan2(F(ev.dy), F(ev.dx != 0 ? ev.dx : p.facing));
                    if (p.onGround || p.y - p.lastSafeY < 0.2) Dust(p.x, p.y, 0.3f + 0.2f * L, new[] { a + PI }, noRing: L < 2);
                    if (L == 0) { Burst(p.x, p.y + 0.9, pc, 10, 4, 0.3f, 0.25f); break; }
                    var col = Color.Lerp(S.Lin(pc), Color.white, L >= 3 ? 0.55f : L * 0.15f);
                    Sprite(p.x, p.y + 0.9, "ring", col, 0.6f + L * 0.2f, 0.18f + L * 0.03f, 2.2f);
                    Smoke(p.x, p.y + 0.15, "#9aa3ae", 4 + L * 3, 3 + L, 0.4f, 0.45f, dir: a + PI, spread: 0.8f, op: 0.45f);
                    Burst(p.x, p.y + 0.9, col, 12 + L * 8, 8 + L * 3, 0.32f, 0.3f, dir: a + PI, spread: 1.1f);
                    if (p.onGround || p.y - p.lastSafeY < 0.2) GroundRing(p.x, p.y, col, 0.3f, 1.2f + L * 0.5f, 0.3f, 0.8f);
                    if (L >= 3) Sprite(p.x, p.y + 0.9, "star", "#ffffff", 1.6f, 0.16f, 1.5f);
                    break;
                }
                case "dashChargeStart": break;
                case "dashLevel": charge.LevelUp(ev.p, RigOf(ev.p), F(ev.level), "dash"); break;
                case "rifleRaise": charge.LevelUp(ev.p, RigOf(ev.p), 1, "rifle"); break;
                case "rifleFocus": charge.LevelUp(ev.p, RigOf(ev.p), 3, "rifle"); break;
                case "snipe": Snipe(ev); break;
                case "crit": PopText(ev.x, ev.y + 0.5, "CRIT", "#ffd27a", 0.75f); Sprite(ev.x, ev.y, "star", "#ffffff", 1.5f, 0.16f, 1.5f); break;
                case "deflect":
                {
                    string c = ev.perfect ? "#fff6d8" : ECHO_ORANGE;
                    Sprite(ev.x, ev.y, "star", "#ffffff", ev.perfect ? 1.8f : 1.1f, 0.14f, 1.4f);
                    Sprite(ev.x, ev.y, "ring", c, ev.perfect ? 1.1f : 0.7f, 0.22f, 2.8f);
                    Burst(ev.x, ev.y, c, ev.perfect ? 24 : 14, ev.perfect ? 11 : 8, 0.26f, 0.28f, grav: 6);
                    SlashMark(ev.x, ev.y, "#ffffff", ev.perfect ? 1.8f : 1.2f, (S.Rnd() - 0.5f) * 1.4f, 0.12f);
                    break;
                }
                case "dashSlash":
                {
                    var p = ev.p; float a = Mathf.Atan2(F(ev.dy), F(ev.dx)), T = F(ev.tier); string col = ev.tier >= 3 ? "#fff1d6" : ECHO_ORANGE;
                    slashes[p] = new SlashRec { x0 = p.x, y0 = p.y + 0.95, depth = effectDepth, tier = T };
                    Sprite(p.x, p.y + 0.9, "ring", col, 0.6f + T * 0.15f, 0.18f, 2.4f);
                    Burst(p.x, p.y + 0.9, col, 10 + T * 6, 8 + T * 2, 0.3f, 0.25f, dir: a + PI, spread: 1);
                    if (p.onGround) Smoke(p.x, p.y + 0.15, "#9aa3ae", 3 + T * 2, 3 + T, 0.4f, 0.4f, dir: a + PI, spread: 0.7f, op: 0.45f);
                    break;
                }
                case "crescent":
                {
                    var p = ev.p;
                    Sprite(ev.x, ev.y, "star", "#ffffff", 1.5f, 0.14f, 1.4f); Sprite(ev.x, ev.y, "ring", ECHO_ORANGE, 0.9f, 0.2f, 2.6f);
                    Burst(ev.x, ev.y, ECHO_ORANGE, 18, 9, 0.3f, 0.3f, dir: p.facing > 0 ? 0 : PI, spread: 1.2f);
                    break;
                }
                case "pogo":
                {
                    var p = ev.p;
                    Sprite(p.x, p.y, "ring", "#fff1d6", 0.9f, 0.2f, 2.6f); Sprite(p.x, p.y + 0.9, "ring", ECHO_ORANGE, 0.7f, 0.25f, 2.4f);
                    Burst(p.x, p.y - 0.1, ECHO_ORANGE, 16, 8, 0.28f, 0.25f, dir: -PI / 2, spread: 1.6f);
                    break;
                }
                case "swing":
                    if (ev.id == "echo_spin" || ev.id == "echo_rise") { Sprite(ev.p.x, ev.p.y + 1, "ring", "#fff1d6", 1.3f, 0.3f, 1.8f); Burst(ev.p.x, ev.p.y + 1, "#e8eef5", 12, 6, 0.3f, 0.35f); }
                    if (ev.id == "echo_rise") Dust(ev.p.x, ev.p.y, 0.55f);
                    if (ev.id == "nova_k3" || ev.id == "echo_charged" || ev.id == "echo_b4") Dust(ev.p.x, ev.p.y, 0.25f, new[] { ev.p.facing > 0 ? PI : 0 }, noRing: true);
                    break;
                case "poundStart":
                {
                    var p = ev.p; string c = CHARS[p.@char].energy;
                    Sprite(p.x, p.y + 1, "ring", "#ffffff", 1.1f, 0.25f, 2.2f); Sprite(p.x, p.y + 1, "ring", c, 0.7f, 0.35f, 2.8f);
                    Burst(p.x, p.y + 1, "#e8eef5", 14, 5, 0.3f, 0.3f);
                    poundT[p] = 0;
                    break;
                }
                case "poundLevel": charge.LevelUp(ev.p, RigOf(ev.p), F(ev.level), "pound"); break;
                case "poundDrop":
                {
                    var p = ev.p; string c = CHARS[p.@char].energy; float L = F(ev.level);
                    Sprite(p.x, p.y + 1, "star", "#ffffff", 1 + 0.3f * L, 0.12f, 1.4f);
                    Burst(p.x, p.y + 1.4, c, 12 + 6 * L, 6, 0.28f, 0.25f, dir: PI / 2, spread: 1.2f);
                    break;
                }
                case "poundLand": PoundLand(ev); break;
                // Bosses
                case "bossSlam":
                {
                    float k = ev.big ? 1 : 0.6f;
                    Dust(ev.x, ev.y, 0.7f + 0.5f * k, BOTH, noRing: true, spread: 0.3f);
                    GroundRing(ev.x, ev.y, "#ffffff", 0.4f, 2.5f + 2.5f * k, 0.35f, 0.8f); GroundRing(ev.x, ev.y, HOSTILE, 0.3f, 1.8f + 2 * k, 0.3f, 0.9f);
                    Burst(ev.x, ev.y + 0.2, "#5d6674", 10 + 10 * k, 8, 0.32f, 0.6f, dir: PI / 2, spread: 2, grav: 18);
                    Sprite(ev.x, ev.y + 0.3, "star", "#ffd2e4", 1.4f + k, 0.16f, 1.4f);
                    break;
                }
                case "bossPhase":
                {
                    foreach (var (sz, life, col) in new[] { (1.6f, 0.35f, "#ffffff"), (2.6f, 0.5f, HOSTILE), (3.6f, 0.65f, "#ffd2e4") }) Sprite(ev.x, ev.y, "ring", col, sz, life, 3.2f);
                    Sprite(ev.x, ev.y, "star", "#ffffff", 3.2f, 0.2f, 1.5f); Fireball(ev.x, ev.y, "#ff5aa0", 2, 0.3f);
                    Burst(ev.x, ev.y, HOSTILE, 40, 12, 0.4f, 0.5f); Burst(ev.x, ev.y, "#ffffff", 16, 8, 0.3f, 0.3f);
                    if (!ev.e.flier) Dust(ev.e.x, ev.e.y, 1.2f);
                    break;
                }
                case "bossDazed": case "bossCrash":
                {
                    var e = ev.e;
                    Sprite(e.x, e.y + e.h + 0.3, "star", "#fff4c2", 1.2f, 0.5f, 1.4f); Burst(e.x, e.y + e.h * 0.6, "#fff4c2", 16, 5, 0.3f, 0.5f);
                    if (ev.type == "bossCrash") { Dust(e.x, e.y, ev.parried ? 1.2f : 0.9f); Burst(e.x, e.y + 0.3, "#5d6674", 14, 8, 0.3f, 0.6f, dir: PI / 2, spread: 2, grav: 18); Sprite(e.x, e.y + 0.4, "star", "#ffffff", 2.2f, 0.16f, 1.5f); }
                    break;
                }
                case "bossMissiles": { var e = ev.e; Burst(e.x - e.facing * 0.6, e.y + e.h + 0.2, HOSTILE, 14, 5, 0.3f, 0.3f, dir: PI / 2, spread: 1); Smoke(e.x - e.facing * 0.6, e.y + e.h + 0.2, "#8e97a3", 5, 1.5f, 0.5f, 0.7f, dir: PI / 2, spread: 1.2f, op: 0.45f); break; }
                case "bossLaser": { var e = ev.e; Sprite(e.x + e.facing * e.w * 0.5, e.y + (e.flier ? 0.4 : e.h * 0.9), "star", "#ffffff", 1.6f, 0.16f, 1.4f); break; }
                case "bossDive": { var e = ev.e; Sprite(e.x, e.y + 0.7, "ring", HOSTILE, 2, 0.25f, 2.4f); break; }
                case "bossCall": { var e = ev.e; foreach (var d in new[] { -4, 4 }) { Sprite(e.x + d, e.y + 1.5, "ring", HOSTILE, 1.2f, 0.35f, 2.4f); Burst(e.x + d, e.y + 1.5, HOSTILE, 14, 5, 0.3f, 0.35f); } break; }
                case "bossDown": BossExplosion(ev); break;
                case "shot":
                    if (ev.level > 0 && ev.p != null) charge.Release(new ChargeFX.Rel { level = F(ev.level), perfect = ev.perfect, attach = ev.attach, ax = ev.ax, ay = ev.ay, cannon = ev.cannon, rivet = ev.rivet }, ev.p, RigOf(ev.p));
                    else if (ev.cannon)
                    {
                        Sprite(ev.x, ev.y, "star", "#ffffff", 0.9f, 0.08f, 1.4f); Burst(ev.x, ev.y, CHARS["ram"].energy, 10, 7, 0.26f, 0.16f, dir: Mathf.Atan2(F(ev.ay), F(ev.ax)), spread: 0.6f);
                        Smoke(ev.x - ev.ax * 0.4, ev.y - ev.ay * 0.4, "#8e97a3", 3, 1.2f, 0.4f, 0.5f, op: 0.4f);
                    }
                    else if (ev.rivet) Burst(ev.x, ev.y, "#ffe2a8", 3, 4, 0.14f, 0.1f, dir: Mathf.Atan2(F(ev.ay), F(ev.ax)), spread: 0.5f);
                    break;
                case "downed": Burst(ev.p.x, ev.p.y + 0.4, "#ffffff", 16, 5, 0.35f, 0.5f); break;
                case "revived": Sprite(ev.p.x, ev.p.y + 1, "ring", "#9cf5c8", 1.2f, 0.4f, 2.5f); Burst(ev.p.x, ev.p.y + 1, "#9cf5c8", 18, 5, 0.35f, 0.5f); break;
                case "recall": case "respawn": case "join": if (ev.p != null) { Sprite(ev.p.x, ev.p.y + 1, "ring", "#bfe9ff", 1.1f, 0.35f, 2.4f); Burst(ev.p.x, ev.p.y + 1, "#bfe9ff", 16, 4, 0.3f, 0.5f); } break;
                case "slam": Burst(ev.e.x, ev.e.y + 0.2, HOSTILE, 24, 9, 0.5f, 0.45f, dir: PI / 2, spread: 3); Dust(ev.e.x, ev.e.y, 0.9f); break;
                case "telegraph": telegraphs.Add(new Tele { e = ev.e, cat = ev.cat, ticks = F(ev.ticks) }); break;
                case "lock": Sprite(ev.e.x + ev.e.facing * 1.3, ev.e.y + 1.35, "star", "#ffffff", 0.9f, 0.2f, 1.3f); break;
                case "snareThrow": Burst(ev.x, ev.y, ECHO_ORANGE, 6, 3, 0.25f, 0.2f); break;
                case "snarePlant": Sprite(ev.x, ev.y + 0.1, "ring", ECHO_ORANGE, 0.9f, 0.3f, 2); break;
                case "snareTrigger": Sprite(ev.x, ev.y + 0.3, "ring", ECHO_ORANGE, 1.4f, 0.3f, 2.6f); Burst(ev.x, ev.y + 0.3, ECHO_ORANGE, 20, 7, 0.35f, 0.35f); break;
                case "snared": Burst(ev.x, ev.y, ECHO_ORANGE, ev.weak ? 6 : 14, 5, 0.3f, 0.35f); break;
                case "leash": Sprite(ev.e.x, ev.e.y + ev.e.h / 2, "ring", ECHO_ORANGE, 0.8f, 0.25f, 2); break;
                case "yank": Burst(ev.e.x, ev.e.y + ev.e.h / 2, ECHO_ORANGE, 18, 8, 0.4f, 0.35f); Sprite(ev.e.x, ev.e.y + ev.e.h / 2, "ring", "#ffffff", 1.3f, 0.25f, 2.8f); break;
                case "scarfMode": Burst(ev.p.x, ev.p.y + 1.4, ev.mode == "veil" ? VEIL_PALE : ECHO_ORANGE, ev.mode == "flare" ? 18 : 12, 3, 0.3f, 0.3f); break;
                case "veilOn": Burst(ev.p.x, ev.p.y + 1, VEIL_PALE, 14, 2.5f, 0.35f, 0.45f); break;
                case "veilBreak": if (ev.wasHidden) Burst(ev.p.x, ev.p.y + 1, VEIL_PALE, 12, 5, 0.3f, 0.3f); break;
                case "vanish": Sprite(ev.p.x, ev.p.y + 1, "ring", VEIL_PALE, 1.2f, 0.3f, 2.4f); Burst(ev.p.x, ev.p.y + 1, VEIL_PALE, 20, 6, 0.35f, 0.4f); break;
                case "ambush": Sprite(ev.x, ev.y, "star", "#ffffff", 2.0f, 0.28f, 1.6f); Burst(ev.x, ev.y, ECHO_ORANGE, 26, 10, 0.45f, 0.45f); break;
                case "challenge": Sprite(ev.x, ev.y, "ring", ECHO_ORANGE, 2.2f, 0.45f, 4.2f); Burst(ev.x, ev.y, ECHO_ORANGE, 30, 9, 0.4f, 0.45f); break;
                case "lostTrack": Glyph(ev.e, "?", "#ffffff", 1.0f); break;
                case "taunted": Glyph(ev.e, "!", ev.by != null && ev.by.@char == "ram" ? CHARS["ram"].energy : ECHO_ORANGE, 0.9f); break;
                case "attach":
                {
                    string c = ATTACH_LOOK[ev.attach].tint; double x = ev.p.x + ev.p.facing * 0.35, y = ev.p.y + 1.05;
                    Sprite(x, y, "ring", c, 0.5f, 0.2f, 2); Burst(x, y, c, 6, 2.5f, 0.25f, 0.25f);
                    break;
                }
                case "chargeLevel": case "burstLevel":
                    if (ev.p.@char != "echo") charge.LevelUp(ev.p, RigOf(ev.p), F(ev.level), ev.type == "burstLevel" ? "burst" : "shot");
                    break;
                case "splash":
                    if (ev.r > 1.2) Dust(ev.x, ev.y, 0.15f + 0.1f * F(ev.level), BOTH, reach: F(ev.r) * 0.6f, noRing: true);
                    Sprite(ev.x, ev.y, "ring", NOVA_GOLD, 0.35f + F(ev.r) * 0.7f, 0.2f, 2.2f);
                    Burst(ev.x, ev.y, NOVA_GOLD, 5 + Mathf.Round(F(ev.r) * 6), 3 + F(ev.r) * 3, 0.28f, 0.25f, grav: 5);
                    break;
                case "rocketJump": RocketBlast(ev); break;
                case "mortarShot": AddLandingMark(ev.x, ev.y, F(ev.r), F(ev.ticks) / 60); break;
                case "enemyBlast":
                    Dust(ev.x, ev.y, 0.8f, BOTH, reach: 1.5f);
                    Sprite(ev.x, ev.y + 0.3, "ring", HOSTILE, F(ev.r) * 1.1f, 0.3f, 2.4f); Sprite(ev.x, ev.y + 0.3, "star", "#ffd2e4", F(ev.r), 0.2f, 1.3f);
                    Burst(ev.x, ev.y + 0.3, HOSTILE, 26, 9, 0.45f, 0.45f, grav: 7); Burst(ev.x, ev.y + 0.3, "#ffffff", 8, 5, 0.3f, 0.25f);
                    break;
                case "chargeStart": Burst(ev.e.x - ev.e.facing * 0.7, ev.e.y + 0.2, "#c9d3de", 12, 4, 0.4f, 0.4f, dir: PI / 2, spread: 1.6f); break;
                case "chargeCrash":
                    Dust(ev.e.x + ev.e.facing * 0.8, ev.e.y, 0.6f, new[] { ev.e.facing > 0 ? PI : 0 });
                    Sprite(ev.e.x + ev.e.facing * 0.8, ev.e.y + 0.9, "star", "#ffffff", 1.6f, 0.25f, 1.5f);
                    Burst(ev.e.x + ev.e.facing * 0.8, ev.e.y + 0.9, "#e6e9f0", 20, 8, 0.4f, 0.45f, grav: 10);
                    break;
                case "perfectRelease":
                    Sprite(ev.x, ev.y, "star", "#ffffff", 1.8f, 0.22f, 1.5f); Sprite(ev.x, ev.y, "ring", NOVA_GOLD, 1.0f, 0.3f, 3);
                    Burst(ev.x, ev.y, "#fff1c9", 14, 7, 0.35f, 0.3f);
                    break;
                case "blast":
                {
                    Dust(ev.x, ev.y, 0.35f + 0.15f * Or(ev.level, 1), BOTH, reach: 1 + Or(ev.r, 1) * 0.6f);
                    string c = ATTACH_LOOK["arc"].tint; float r = F(ev.r), L = F(ev.level);
                    Sprite(ev.x, ev.y, "ring", c, r * 0.9f, 0.3f, 2.4f); Sprite(ev.x, ev.y, "star", "#fff1d0", r * 0.8f, 0.18f, 1.3f);
                    Burst(ev.x, ev.y, c, 22 + L * 6, 7 + r * 2, 0.45f, 0.4f, grav: 6); Burst(ev.x, ev.y, "#ffffff", 8, 4, 0.3f, 0.25f);
                    break;
                }
                case "split": Sprite(ev.x, ev.y, "star", ATTACH_LOOK["prism"].tint, 1.1f, 0.18f, 1.4f); Burst(ev.x, ev.y, ATTACH_LOOK["prism"].tint, 10, 6, 0.3f, 0.25f); break;
                case "ricochet": Burst(ev.x, ev.y, ATTACH_LOOK["prism"].tint, 4, 4, 0.22f, 0.18f); break;
                case "burst":
                    Burst(ev.x, ev.y, "#ffcf7a", ev.charged ? 22 : 14, ev.charged ? 14 : 11, 0.32f, 0.16f, dir: Mathf.Atan2(F(ev.ay), F(ev.ax)), spread: ev.charged ? 0.9f : 0.7f);
                    Sprite(ev.x, ev.y, "ring", NOVA_GOLD, ev.charged ? 0.8f : 0.5f, 0.14f, 2.2f);
                    break;
                case "carve":
                    Dust(ev.p.x, ev.p.y, 0.25f, new[] { ev.p.vx > 0 ? 0 : PI }, noRing: true);
                    Burst(ev.p.x + System.Math.Sign(ev.p.vx) * 0.2, ev.p.y + 0.05, "#ffe2a8", 8, 4, 0.22f, 0.25f, dir: ev.p.vx > 0 ? 0.5f : PI - 0.5f, spread: 0.9f, grav: 8);
                    break;
                case "focusUp": Sprite(ev.p.x, ev.p.y + ev.p.h + 0.35, "star", NOVA_GOLD, 0.45f + F(ev.level) * 0.08f, 0.3f, 1.3f); break;
                case "focusLost": Burst(ev.p.x, ev.p.y + 1.2, "#9aa6b8", 8, 3, 0.25f, 0.3f); break;
                case "aegisOn": case "aegisHit": case "aegisOff": aegis.OnEvent(ev); break;
                case "nshieldOn": case "nshieldBlock": case "nshieldBreak": case "nshieldReady": case "absorbSpill": case "parryStun": nshield.OnEvent(ev); break;
                case "beamStart":
                {
                    beam.OnEvent(ev);
                    var p = ev.p; var rig = RigOf(p); bool ramC = p.@char == "ram";
                    charge.Release(new ChargeFX.Rel { level = 4, attach = ev.attach, perfect = true, ax = p.aimX, ay = p.aimY, beam = true, cannon = ramC }, p, rig);
                    Fireball(p.x + p.aimX * 0.8, p.y + p.h * 0.62 + p.aimY * 0.8, ramC ? "#9fd0ff" : "#ffd27a", ramC ? 2.0f : 1.4f, 0.2f);
                    if (ramC) { ram.Sparks(p.slot, p.x - p.facing * 0.3, p.y + 0.06, -F(p.facing), 14, 1.1f); Dust(p.x, p.y, 0.8f, new[] { p.facing > 0 ? PI : 0 }, reach: 1); }
                    break;
                }
                case "subSwitch": case "grenadeThrow": case "bounce": case "frag": case "cluster": case "chain": case "discThrow": case "discRecall": case "discCatch":
                case "discFade": case "wellLaunch": case "wellOpen": case "wellCollapse": case "dodge": case "perfectDodge": case "riseBlast":
                    sub.OnEvent(ev); break;
                case "ultCast": case "ultJoin": case "ultRun": case "ultBegin": case "ultNova": case "ultCut": case "ultFinisher": case "ultEnd": case "teamFinisher":
                    ult.OnEvent(ev); break;
                case "ultReady": Sprite(ev.p.x, ev.p.y + 1, "ring", "#7fe3ff", 1, 0.4f, 3); Burst(ev.p.x, ev.p.y + 1, "#7fe3ff", 20, 5, 0.28f, 0.45f); break;
                case "guardOn": case "guardBlock": case "perfectGuard": case "rampartBreak": case "rampartReady": case "kineticRelease": case "rush": case "plowCatch": case "ramSplat":
                case "ramBonk": case "wallUp": case "wallHit": case "wallDown": case "link": case "linkHit": case "linkEnd": case "leap": case "leapLand": case "provoke": case "quake":
                case "upliftBlast": case "fortify": case "ramSlam":
                    ram.OnEvent(ev); break;
                case "gadgetSelect": case "powerSelect": case "gadgetDeploy": case "gadgetLand": case "gadgetUp": case "gadgetWrench": case "gadgetHit": case "gadgetEnd": case "sentryShot":
                case "sentryRocket": case "padPlace": case "padBounce": case "powerToss": case "powerUp": case "powerFade": case "noScrap": case "scrap": case "rivetStick": case "rivetBlast":
                case "sparkRing": case "repairPulse": case "patchOn": case "patchTarget": case "podCall": case "podLand": case "overhaulPulse": case "overhaulDone":
                    fixfx.OnEvent(ev); break;
                case "breachBlast":
                {
                    string c = CHARS["ram"].energy; float r = F(ev.r);
                    Dust(ev.x, ev.y, 0.5f, BOTH, reach: 1.6f);
                    Sprite(ev.x, ev.y, "ring", c, r * 0.9f, 0.3f, 2.6f); Sprite(ev.x, ev.y, "star", "#ffffff", r * 0.8f, 0.16f, 1.4f); Fireball(ev.x, ev.y, "#9fd0ff", r * 0.6f, 0.25f);
                    Burst(ev.x, ev.y, c, 26, 8 + r * 2, 0.4f, 0.4f, grav: 6);
                    if (FloorUnder(ev.x, ev.y, 0.8) != null) ram.Crater(ev.x, ev.y, 0.8f);
                    break;
                }
                case "plateHit": Burst(ev.x, ev.y, "#a9c8ff", 10, 5, 0.24f, 0.25f); Sprite(ev.x, ev.y, "ring", "#a9c8ff", 0.8f, 0.16f, 2); break;
                case "notReady": Sprite(ev.p.x, ev.p.y + ev.p.h + 0.3, "ring", "#8a94a3", 0.45f, 0.2f, 1.6f); break;
                case "linkNone": PopText(ev.p.x, ev.p.y + ev.p.h + 0.6, "NO ONE TO LINK", "#cfe6ff", 0.6f); break;
                case "beamEnd":
                {
                    beam.OnEvent(ev);
                    var p = ev.p; double cx = p.x + p.facing * 0.5, cy = p.y + p.h * 0.62;
                    Smoke(cx, cy, "#8e97a3", 6, 1.6f, 0.5f, 0.7f, dir: PI / 2, spread: 1.4f, op: 0.4f);
                    Burst(cx, cy, "#fff1c9", 10, 4, 0.2f, 0.3f);
                    break;
                }
            }
            } finally { effectDepth = previousDepth; }
        }
        public Rig RigOf(Player p) => p != null && rigs.TryGetValue(p, out var r) ? r : null;

        // Echo's sniper shot: a muzzle blast, a tracer to where it stopped with a vapour trail along it, and at full
        // focus a white rail with shock rings down its length
        void Snipe(Ev ev)
        {
            var p = ev.p; var rig = RigOf(p); float f = F(ev.f); bool full = ev.full;
            var at = charge.Muzzle(p, rig); var end = W(ev.x1, ev.y1, 0.25);
            charge.Release(new ChargeFX.Rel { level = 1 + Mathf.Round(f * 2), rifle = true, mark = full, ax = ev.ax, ay = ev.ay }, p, rig);
            var line = charge.Line(full ? "#ffffff" : "#ffc070", 0.025f + 0.035f * f + (full ? 0.02f : 0));
            charge.Span(line, at, end); charge.AddFlash(line, full ? 0.3f : 0.16f, 1);
            if (full) { var glow = charge.Line("#ff9a1f", 0.12f); charge.Span(glow, at, end); charge.AddFlash(glow, 0.22f, 0.7f); }
            double len = JMath.Hypot(ev.x1 - ev.x0, ev.y1 - ev.y0);
            for (double d = 0.6; d < len; d += 0.55) Smoke(ev.x0 + ev.ax * d, ev.y0 + ev.ay * d, "#c4ccd6", 1, 0.3f, 0.26f + 0.12f * f, 0.5f + 0.3f * f, op: 0.3f + 0.15f * f, grow: 1.9f, grav: -0.3f);
            Smoke(ev.x0 - ev.ax * 0.6, ev.y0 - ev.ay * 0.6, "#9aa3ae", 3, 2, 0.4f, 0.5f, dir: Mathf.Atan2(F(-ev.ay), F(-ev.ax)), spread: 0.8f, op: 0.45f);
            if (ev.wall)
            {
                Burst(ev.x1, ev.y1, "#ffe0b0", 14 + f * 10, 7, 0.22f, 0.3f, dir: Mathf.Atan2(F(-ev.ay), F(-ev.ax)), spread: 2.2f, grav: 10);
                Smoke(ev.x1, ev.y1, "#8e97a3", 4, 1.5f, 0.45f, 0.6f, op: 0.45f);
                Sprite(ev.x1, ev.y1, "star", "#fff1d6", 0.8f + f * 0.6f, 0.12f, 1.4f);
            }
            if (full)
            {
                var dir = S.Dir(ev.x0, ev.ax, ev.ay).normalized;
                foreach (var u in new[] { 0.18, 0.45, 0.72 })
                    if (u * len > 1) charge.ShockRing(W(ev.x0 + ev.ax * len * u, ev.y0 + ev.ay * len * u, 0.25), dir, "#fff1d6", 0.3f, 0.9f, 0.28f, 1.5f);
            }
        }

        // The pound lands: a flash, rings the size of the real blast, dust and debris thrown out both ways, sparks
        void PoundLand(Ev ev)
        {
            var p = ev.p; double x = ev.x, y = ev.y; float L = F(ev.level), r = F(ev.r); string c = CHARS[p.@char].energy;
            Sprite(x, y + 0.4, "star", "#ffffff", 1.8f + 0.6f * L, 0.16f + 0.02f * L, 1.5f);
            string fb = p.@char == "nova" ? "#ffd27a" : p.@char == "echo" ? "#ff9a1f" : p.@char == "ram" ? "#9fd0ff" : "#9ff5d0";
            Fireball(x, y + 0.3, fb, (0.9f + 0.35f * L) * (p.@char == "ram" ? 1.3f : 1), 0.2f + 0.04f * L);
            GroundRing(x, y, "#fff6e0", 0.3f, r * 1.1f, 0.32f + 0.05f * L, 0.95f); GroundRing(x, y, c, 0.2f, r * 0.8f, 0.28f, 0.9f);
            if (L >= 2) GroundRing(x, y, "#ffffff", 0.5f, r * 1.35f, 0.45f, 0.7f);
            Dust(x, y, 0.7f + 0.25f * L, BOTH, noRing: true, spread: 0.3f);
            Smoke(x, y + 0.4, "#8e97a3", 4 + 3 * L, 1.2f, 0.8f + 0.2f * L, 0.9f + 0.2f * L, dir: PI / 2, spread: 1.4f, grav: -1, grow: 2.4f, op: 0.45f);
            foreach (var dir in new[] { 0.1f, PI - 0.1f }) Burst(x, y + 0.15, c, 10 + 6 * L, 9 + 3 * L, 0.3f, 0.32f, dir: dir, spread: 0.35f, grav: 5);
            Burst(x, y + 0.2, "#5d6674", 8 + 5 * L, 6 + 2 * L, 0.3f, 0.6f, dir: PI / 2, spread: 1.9f, grav: 18);
            Burst(x, y + 0.3, "#ffffff", 10 + 4 * L, 6 + 2 * L, 0.22f, 0.25f);
            if (p.@char == "echo") { SlashMark(x, y + 0.5, "#fff1d6", 1.6f + 0.4f * L, 0.6f, 0.18f); SlashMark(x, y + 0.5, c, 1.6f + 0.4f * L, -0.6f, 0.18f); }
            else Sprite(x, y + 0.5, "ring", "#fff1c9", 0.8f + 0.3f * L, 0.3f, 3);
            if (p.@char == "ram") ram.Crater(x, y, Mathf.Min(2.2f, 0.8f + 0.3f * L + 0.12f * r), 1 + 0.3f * L);
            poundT.Remove(p);
        }
        readonly Dictionary<Player, float> poundT = new Dictionary<Player, float>();
        readonly Dictionary<Player, double> prevVx = new Dictionary<Player, double>();
        readonly Dictionary<Player, float> stepT = new Dictionary<Player, float>();
        readonly Dictionary<Player, double> ghostTick = new Dictionary<Player, double>();

        // While a pound hangs: energy drawn into the fist or glaive, slow ripples and motes; while it drops:
        // streaks rushing past, and dust lifted just before it lands
        void PoundFx(World world, View view, float dt)
        {
            foreach (var p in world.players)
            {
                if (p.state != "pound" || p.pound == null) continue;
                var rig = RigOf(p); if (rig == null || !rig.root.visible) continue;
                using var origin = AtDepth(LevelFeatures.Depth(p));
                var Sp = p.pound; string c = CHARS[p.@char].energy;
                if (Sp.phase == "hold")
                {
                    var at = charge.Muzzle(p, rig); double n = 1 + Sp.level;
                    for (int i = 0; i < n; i++)
                    {
                        float a = S.Rnd() * PI * 2, r = 0.9f + S.Rnd() * 0.6f; var tv = S.Dir(p.x, Mathf.Cos(a) * r, Mathf.Sin(a) * r);
                        charge.Inward(at, tv.x, tv.y, tv.z, S.Rnd() < 0.3f ? "#ffffff" : c, 0.15f, 0.24f);
                    }
                    float t = (poundT.TryGetValue(p, out var t0) ? t0 : 0) + dt; poundT[p] = t;
                    if (t > 0.22f - 0.03f * F(Sp.level)) { poundT[p] = 0; Sprite(p.x, p.y + 1, "ring", Sp.level >= 3 ? "#ffffff" : c, 0.8f, 0.55f, 3.2f); }
                    if (S.Rnd() < 0.5f)
                    {
                        var w = W(p.x + (S.Rnd() - 0.5f) * 3, p.y - 0.5 + S.Rnd() * 3, (S.Rnd() - 0.5f) * 1.5f);
                        var P = Particle(w, S.Rnd() < 0.5f ? "#ffffff" : c, 0.1f, 0.6f); P.v = new Vector3(0, 0.6f, 0); P.drag = 1;
                    }
                    DustSwirl(p.x, p.y, 0.3f + 0.2f * F(Sp.level));
                }
                else if (Sp.phase == "drop")
                {
                    for (int i = 0; i < 3; i++) Burst(p.x + (S.Rnd() - 0.5f) * 1.2f, p.y + S.Rnd() * 2, "#ffffff", 1, 26, 0.12f, 0.08f, dir: PI / 2, spread: 0.05f);
                    if (FloorUnder(p.x, p.y, 1.6) != null && S.Rnd() < 0.7f) Dust(p.x, p.y, 0.25f, BOTH, reach: 1.6f, noRing: true, op: 0.35f);
                }
            }
        }

        // Dust and grit from the characters in general
        void GroundFx(World world, View view, float dt)
        {
            foreach (var p in world.players)
            {
                if (p.state == "dead" || p.state == "downed") continue;
                var rig = RigOf(p); if (rig == null || !rig.root.visible) continue;
                using var origin = AtDepth(LevelFeatures.Depth(p));
                if (p.onGround)
                {
                    float dl = p.state == "dashCharge" ? (p.dashChargeT >= DASH_CHARGE.charge[2] ? 3 : p.dashChargeT >= DASH_CHARGE.charge[1] ? 2 : p.dashChargeT >= DASH_CHARGE.charge[0] ? 1 : 0.3f) : 0;
                    float cl = p.@char == "nova" && SETTINGS.novaKit == "marksman" && p.chargeT > MARKSMAN.charge[1] ? (p.chargeT >= MARKSMAN.beam.at ? 3 : p.chargeT >= MARKSMAN.charge[2] ? 2 : 1) : 0;
                    float k = Mathf.Max(dl, cl);
                    if (k != 0 && S.Rnd() < 0.25f + 0.2f * k) DustSwirl(p.x, p.y, 0.25f * k);
                    double pv = prevVx.TryGetValue(p, out var q) ? q : p.vx; prevVx[p] = p.vx;
                    if (p.state == "normal" && System.Math.Abs(pv) > 6 && System.Math.Sign(p.vx) != System.Math.Sign(pv) && System.Math.Abs(p.vx) > 0.5) Dust(p.x, p.y, 0.35f, new[] { pv > 0 ? 0 : PI }, noRing: true);
                    if (p.@char == "echo" && p.state == "normal" && System.Math.Abs(p.vx) > 7.5)
                    {
                        float t = (stepT.TryGetValue(p, out var s0) ? s0 : 0) + dt; stepT[p] = t;
                        if (t > 0.19f) { stepT[p] = 0; Smoke(p.x - System.Math.Sign(p.vx) * 0.2, p.y + 0.08, DUST, 1, 1.4f, 0.3f, 0.4f, dir: p.vx > 0 ? PI - 0.4f : 0.4f, spread: 0.5f, grav: -0.4f, op: 0.4f); }
                    }
                }
                else if (p.thrusting && S.Rnd() < 0.6f) Dust(p.x, p.y, 0.25f, BOTH, reach: 3.2f, noRing: true, op: 0.35f);
                if (p.state == "beam" && p.beam != null && p.beam.segs != null)
                {
                    foreach (var g in p.beam.segs)
                    {
                        double len = JMath.Hypot(g.x1 - g.x0, g.y1 - g.y0);
                        for (double d = 0.5; d < len; d += 1.6)
                        {
                            if (S.Rnd() > 0.22f) continue;
                            double x = g.x0 + (g.x1 - g.x0) * d / len, y = g.y0 + (g.y1 - g.y0) * d / len; var fl = FloorUnder(x, y, 1.6);
                            if (fl == null) continue;
                            float dir = g.x1 >= g.x0 ? 0.35f : PI - 0.35f;
                            Smoke(x, fl.Value + 0.08, DUST, 1, 2.5f + 2 * F(1.6 - (y - fl.Value)), 0.28f, 0.45f, dir: dir, spread: 0.5f, grav: -0.5f, grow: 2, op: 0.3f);
                        }
                    }
                    if (p.onGround && S.Rnd() < 0.5f) Dust(p.x, p.y, 0.2f, new[] { p.beam.dx > 0 ? PI : 0 }, noRing: true, op: 0.35f);
                }
            }
            foreach (var e in world.enemies)
                if (!e.dead && e.type == "charger" && e.state == "charge" && e.onGround && S.Rnd() < 0.6f)
                    using (AtDepth(LevelFeatures.Depth(e))) Dust(e.x - e.facing * 0.6, e.y, 0.25f, new[] { e.facing > 0 ? PI : 0 }, noRing: true);
        }

        // ---- Bosses ----
        sealed class Boom { public float t; public Enemy e; public double ox, oy, x, y, depth; public double? ground; public bool big, done; }
        readonly List<Boom> booms = new List<Boom>();
        void BossExplosion(Ev ev)
        {
            var e = ev.e;
            for (int i = 0; i < 9; i++) booms.Add(new Boom { t = i * 0.11f + S.Rnd() * 0.05f, e = e, depth = ev.depth ?? LevelFeatures.Depth(e), ox = (S.Rnd() - 0.5f) * e.w, oy = S.Rnd() * e.h });
            booms.Add(new Boom { t = 1.15f, e = e, depth = ev.depth ?? LevelFeatures.Depth(e), ox = 0, oy = e.h * 0.5, big = true });
        }
        void UpdateBooms(float dt)
        {
            if (booms.Count == 0) return;
            foreach (var b in booms)
            {
                b.t -= dt; if (b.t > 0) continue;
                using var origin = AtDepth(b.depth);
                b.done = true; b.x = b.e.x + b.ox; b.y = b.e.y + b.oy; b.ground = FloorUnder(b.e.x, b.e.y, 1) != null ? b.e.y : (double?)null;
                if (!b.big) { Fireball(b.x, b.y, "#ff5aa0", 0.9f, 0.22f); Burst(b.x, b.y, HOSTILE, 14, 7, 0.3f, 0.35f, grav: 6); Smoke(b.x, b.y, "#6d7480", 3, 1.4f, 0.6f, 0.8f, op: 0.5f); continue; }
                Fireball(b.x, b.y, "#ffd2e4", 3, 0.4f); Sprite(b.x, b.y, "star", "#ffffff", 5, 0.25f, 1.5f);
                foreach (var (sz, life) in new[] { (2.5f, 0.4f), (4f, 0.6f) }) Sprite(b.x, b.y, "ring", "#ffffff", sz, life, 3);
                Burst(b.x, b.y, HOSTILE, 60, 14, 0.45f, 0.7f, grav: 8); Burst(b.x, b.y, "#ffffff", 30, 10, 0.3f, 0.4f);
                Burst(b.x, b.y, "#5d6674", 24, 10, 0.34f, 0.9f, grav: 20);
                Smoke(b.x, b.y, "#6d7480", 16, 2.5f, 1.1f, 1.4f, op: 0.55f, grow: 2.6f, grav: -0.8f);
                if (b.ground != null) Dust(b.x, b.ground.Value, 1.4f);
            }
            booms.RemoveAll(b => b.done);
        }

        // Boss lasers: a thin flickering line during the windup, then a thick beam with a white-hot core
        sealed class BossBeam { public TMesh core, glow, feed; }
        readonly Dictionary<Enemy, BossBeam> bossBeams = new Dictionary<Enemy, BossBeam>();
        void SyncBossLasers(World world)
        {
            var seen = new HashSet<Enemy>();
            foreach (var e in world.enemies)
            {
                var A = e.atk;
                if (!e.boss || e.dead || A == null || !(e.state == "laser" || (e.state == "windup" && (A.kind == "laser" || A.kind == "sweep")))) continue;
                if (A.span.x0 == 0 && A.span.x1 == 0 && A.span.y == 0) continue;
                seen.Add(e);
                using var origin = AtDepth(LevelFeatures.Depth(e));
                if (!bossBeams.TryGetValue(e, out var B))
                {
                    TMesh Mk(float r, Color col, float op)
                    {
                        var m = new TMesh(Geo.Cylinder(r, r, 1, 10, 1, true), new TMat(TMat.Kind.Basic) { colorLin = col, transparent = true, opacity = op, depthWrite = false, fog = false });
                        m.RenderOrder = 4; scene.add(m); return m;
                    }
                    B = new BossBeam { core = Mk(0.07f, Color.white * 2.5f, 1), glow = Mk(0.26f, S.Lin(HOSTILE) * 2, 0.55f), feed = Mk(0.1f, S.Lin(HOSTILE) * 2.2f, 0.7f) };
                    bossBeams[e] = B;
                }
                var L = A.span; bool live = e.state == "laser";
                Vector3 a = W(L.x0, L.y, 0.2), b = W(L.x1, L.y, 0.2);
                charge.Span(B.core, a, b); charge.Span(B.glow, a, b);
                if (e.flier) { var c = W(e.x + e.facing * 1.3, e.y + 0.35, 0.2); charge.Span(B.feed, c, a); B.feed.material.opacity = live ? 0.75f : 0.25f; B.feed.scale.x = B.feed.scale.z = live ? 1 : 0.35f; }
                else B.feed.visible = false;
                if (live)
                {
                    float f = 0.85f + S.Rnd() * 0.3f;
                    B.core.scale.x = B.core.scale.z = f; B.glow.scale.x = B.glow.scale.z = f * (1 + 0.1f * Mathf.Sin(Time.time * 1000 * 0.05f));
                    B.core.material.opacity = 1; B.glow.material.opacity = 0.6f;
                    Burst(L.x1, L.y, S.Rnd() < 0.5f ? "#ffffff" : HOSTILE, 3, 8, 0.2f, 0.2f, dir: L.x1 > L.x0 ? PI : 0, spread: 2.2f, grav: 8);
                    var fl0 = FloorUnder(L.x0, L.y0, 3);
                    if (L.y0 - (fl0 ?? -99) < 1.2)
                        for (int i = 0; i < 2; i++)
                        {
                            double x = L.x0 + (L.x1 - L.x0) * S.Rnd(); var g = FloorUnder(x, L.y0, 1.3);
                            if (g != null) Smoke(x, g.Value + 0.1, DUST, 1, 2.5f, 0.35f, 0.4f, dir: L.x1 > L.x0 ? 0.4f : PI - 0.4f, spread: 0.6f, op: 0.4f);
                        }
                }
                else
                {
                    bool on = S.Rnd() < 0.75f;
                    B.core.scale.x = B.core.scale.z = 0.25f; B.glow.scale.x = B.glow.scale.z = 0.12f;
                    B.core.material.opacity = on ? 0.55f : 0.1f; B.glow.material.opacity = on ? 0.5f : 0.1f;
                }
            }
            foreach (var kv in new List<KeyValuePair<Enemy, BossBeam>>(bossBeams))
            {
                if (seen.Contains(kv.Key)) continue;
                var B = kv.Value; B.core.visible = B.glow.visible = B.feed.visible = false;
                if (kv.Key.dead || !world.enemies.Contains(kv.Key)) { B.core.DestroyOwnedMaterials(); B.glow.DestroyOwnedMaterials(); B.feed.DestroyOwnedMaterials(); bossBeams.Remove(kv.Key); }
            }
        }

        // A badly damaged boss smokes and throws sparks
        void BossDamage(World world)
        {
            foreach (var e in world.enemies)
            {
                if (!e.boss || e.dead || e.hp > e.maxHp * 0.6) continue;
                using var origin = AtDepth(LevelFeatures.Depth(e));
                float k = 1 - F(e.hp / (e.maxHp * 0.6));
                if (S.Rnd() < 0.15f + 0.4f * k) Smoke(e.x + (S.Rnd() - 0.5f) * e.w * 0.8, e.y + e.h * (0.4f + S.Rnd() * 0.5f), "#5d6674", 1, 0.8f, 0.5f + 0.3f * k, 0.9f, dir: PI / 2, spread: 0.8f, grav: -1, op: 0.45f);
                if (S.Rnd() < 0.06f + 0.2f * k) Burst(e.x + (S.Rnd() - 0.5f) * e.w, e.y + e.h * S.Rnd(), S.Rnd() < 0.5f ? "#ffffff" : "#ffd2e4", 4, 5, 0.14f, 0.25f, grav: 12);
            }
        }

        // Echo's Dash Slash: as the lunge ends, a bright cut line along his whole path flashes and fades
        sealed class SlashRec { public double x0, y0, depth; public float tier; public bool drawn; }
        readonly Dictionary<Player, SlashRec> slashes = new Dictionary<Player, SlashRec>();
        void UpdateSlashes(World world)
        {
            foreach (var kv in new List<KeyValuePair<Player, SlashRec>>(slashes))
            {
                var p = kv.Key; var Sl = kv.Value;
                bool done = p.state != "dashslash" || p.st >= DASH_SLASH.ticks;
                if (!done || Sl.drawn) { if (p.state != "dashslash" && Sl.drawn) slashes.Remove(p); continue; }
                Sl.drawn = true;
                double x1 = p.x, y1 = p.y + 0.95, len = JMath.Hypot(x1 - Sl.x0, y1 - Sl.y0);
                if (len < 1) continue;
                Vector3 a = S.W(Sl.x0, Sl.y0, Sl.depth + .3), b = S.W(x1, y1, LevelFeatures.Depth(p) + .3);
                var core = charge.Line("#ffffff", 0.03f + Sl.tier * 0.012f); var glow = charge.Line(ECHO_ORANGE, 0.09f + Sl.tier * 0.03f);
                charge.Span(core, a, b); charge.Span(glow, a, b);
                charge.AddFlash(core, 0.26f, 1); charge.AddFlash(glow, 0.3f, 0.75f);
                for (double d = 0; d < len; d += 0.4)
                {
                    double u = d / len; using var origin = AtDepth(Sl.depth + (LevelFeatures.Depth(p) - Sl.depth) * u); Burst(Sl.x0 + (x1 - Sl.x0) * u, Sl.y0 + (y1 - Sl.y0) * u, S.Rnd() < 0.4f ? "#ffffff" : ECHO_ORANGE, 1, 2, 0.2f, 0.3f);
                }
            }
        }

        // Slides: Echo's hand drags sparks off the floor and his boots kick up dust; Nova's skates spray sparks
        void SlideFx(World world, View view)
        {
            foreach (var p in world.players)
            {
                if (p.state != "slide" || !p.onGround) continue;
                var rig = RigOf(p); if (rig == null || !rig.root.visible) continue;
                using var origin = AtDepth(LevelFeatures.Depth(p));
                float back = p.vx > 0 ? PI - 0.35f : 0.35f, sp = Mathf.Min(1, Mathf.Abs(F(p.vx)) / 10);
                if (p.@char == "echo")
                {
                    var hand = rig.armF.end.worldPos;
                    if (S.Rnd() < 0.7f) { var P = Particle(hand, S.Rnd() < 0.5f ? "#ffe2a8" : ECHO_ORANGE, 0.14f, 0.2f); P.v = S.Dir(p.x, Mathf.Cos(back) * 5 * sp, Mathf.Sin(back) * 5 * sp + 1.5f); P.grav = 10; P.drag = 0.9f; }
                }
                else if (p.@char == "nova") { if (S.Rnd() < 0.8f) view.sparks.Emit(p.slot, p.x + System.Math.Sign(p.vx) * 0.3, p.y + 0.05, 2.5f, back + (p.vx > 0 ? -0.25f : 0.25f), 4 * sp + 2, 0.6f, depth: (float)LevelFeatures.Depth(p), lane: p.lane); }   // (they bounce and light the deck: Sparks)
                else if (S.Rnd() < 0.8f) Burst(p.x + System.Math.Sign(p.vx) * 0.3, p.y + 0.04, "#ffe2a8", 2, 4 * sp + 1, 0.15f, 0.22f, dir: back, spread: 0.5f, grav: 8);
                if (S.Rnd() < 0.5f) Smoke(p.x - System.Math.Sign(p.vx) * 0.2, p.y + 0.1, "#a3abb5", 1, 1.2f, 0.35f, 0.45f, dir: back, spread: 0.6f, op: 0.4f);
            }
        }

        // Rocket jump launch: everything scales with the launch power, so a Perfect Release reads as the biggest
        void RocketBlast(Ev ev)
        {
            float k = Or(ev.power, 0.5); double x = ev.x, y = ev.y; string gold = NOVA_GOLD;
            float edy = ev.dy != 0 || ev.dx != 0 ? F(ev.dy) : 1, edx = F(ev.dx);
            bool vertical = edy > 0.6f;
            Sprite(x, y + 0.1, "star", "#ffffff", 1.4f + k * 2.2f, 0.16f, 1.6f);
            Sprite(x, y + 0.1, "glow", ev.perfect ? "#ffffff" : "#ffe2a8", 2 + k * 3, 0.22f, 1.8f);
            Fireball(x, y + 0.25, ev.perfect ? "#ffd27a" : "#ff9a2e", 0.9f + k * 1.4f, 0.3f + k * 0.1f);
            Sprite(x, y + 0.2, "ring", "#fff1c9", 0.9f + k * 1.2f, 0.28f, 3.4f);
            if (vertical)
            {
                GroundRing(x, y, "#fff1c9", 0.3f, 2.4f + k * 3.2f, 0.42f + k * 0.1f, 0.95f);
                GroundRing(x, y, gold, 0.2f, 1.4f + k * 2, 0.3f, 0.8f);
                foreach (var dir in new[] { 0.12f, PI - 0.12f })
                {
                    Burst(x, y + 0.1, "#dfe6ee", 8 + k * 10, 7 + k * 8, 0.45f, 0.45f, dir: dir, spread: 0.45f, drag: 0.9f, grav: 1.5f);
                    Smoke(x, y + 0.2, "#9aa3ae", 7 + k * 8, 6 + k * 7, 0.7f + k * 0.4f, 0.55f + k * 0.3f, dir: dir, spread: 0.35f, drag: 0.88f, grav: -0.3f, op: 0.55f);
                }
            }
            float away = Mathf.Atan2(-edy, -edx);
            Burst(x, y + 0.2, gold, 18 + k * 22, 9 + k * 9, 0.28f, 0.45f, dir: away, spread: 2.6f, grav: 16);
            Burst(x, y + 0.2, "#ffffff", 8 + k * 10, 6 + k * 6, 0.22f, 0.25f, dir: away, spread: 3);
            Burst(x, y + 0.2, "#5d6674", 6 + k * 8, 6 + k * 5, 0.3f, 0.7f, dir: away, spread: 2.2f, grav: 20);
            Smoke(x, y + 0.4, "#7d8692", 10 + k * 12, 1.4f + k, 0.9f + k * 0.6f, 1.1f + k * 0.5f, dir: PI / 2, spread: 1.3f, drag: 0.94f, grav: -1.2f, grow: 2.4f, op: 0.5f);
            if (ev.perfect) Sprite(x, y + 0.4, "ring", "#ffffff", 1.6f, 0.35f, 4.2f);
        }

        // Afterimages: charged dashes leave more, brighter, longer-lived ghosts with each level; strong rocket
        // launches leave a trail of gold ones on the way up
        static readonly HashSet<string> GHOST_MOVES = new HashSet<string> { "echo_spin", "echo_rise", "echo_b4", "echo_charged", "nova_rise", "ram_rise", "ram_slam", "fix_rise", "fix_slam" };
        void UpdateGhosts(World world, View view)
        {
            foreach (var p in world.players)
            {
                var rig = RigOf(p); if (rig == null || !rig.root.visible) continue;
                double last = ghostTick.TryGetValue(p, out var l) ? l : -99, dt = world.tick - last;
                var col = S.Lin(CHARS[p.@char].energy);
                if (p.state == "dash" && p.dash != null)
                {
                    int L = (int)p.dash.level; float every = new[] { 5f, 4, 3, 2 }[L];
                    if (dt >= every)
                    {
                        ghostTick[p] = world.tick;
                        var c = Color.Lerp(col, S.Lin("#fff6e0"), new[] { 0, 0.05f, 0.2f, 0.35f }[L]) * new[] { 1, 1, 1.3f, 2.2f }[L];
                        ghosts.Spawn(rig, c, new[] { 0.16f, 0.3f, 0.45f, 0.62f }[L], new[] { 0.12f, 0.2f, 0.28f, 0.4f }[L]);
                    }
                }
                else if (p.state == "dashslash" && dt >= 2)
                {
                    ghostTick[p] = world.tick;
                    float T = p.slash != null ? F(p.slash.tier) : 1;
                    ghosts.Spawn(rig, Color.Lerp(col, S.Lin("#fff6e0"), 0.12f * T) * (1 + 0.45f * T), 0.26f + 0.08f * T, 0.2f + 0.05f * T);
                }
                else if (p.state == "pound" && p.pound != null && p.pound.phase == "drop" && dt >= 2)
                {
                    ghostTick[p] = world.tick; float L = F(p.pound.level);
                    ghosts.Spawn(rig, Color.Lerp(col, S.Lin("#fff6e0"), 0.1f * L) * (1.2f + 0.3f * L), 0.26f + 0.06f * L, 0.16f + 0.03f * L);
                }
                else if (p.state == "attack" && p.move != null && p.moveId != null && GHOST_MOVES.Contains(p.moveId) && p.st >= p.move.su && p.st < p.move.su + p.move.ac && dt >= 3)
                {
                    ghostTick[p] = world.tick;
                    ghosts.Spawn(rig, col, 0.2f, 0.14f);
                }
                else if (p.rocketT > 0 && p.vy > 8 && p.rocketPow > 0.55 && dt >= 3)
                {
                    ghostTick[p] = world.tick;
                    ghosts.Spawn(rig, S.Lin("#ffb547") * (1 + F(p.rocketPow)), 0.2f + 0.25f * F(p.rocketPow), 0.3f);
                }
            }
        }

        // Rocket climb: a jet of sparks and smoke from his boots while he is still going up fast
        void RocketTrails(World world)
        {
            foreach (var p in world.players)
            {
                if (!(p.rocketT > 0 && p.vy > 6 && !p.onGround)) continue;
                using var origin = AtDepth(LevelFeatures.Depth(p));
                float k = Mathf.Min(1, F(p.vy) / 30) * Or(p.rocketPow, 0.5);
                for (int i = 0; i < 1 + k * 3; i++)
                    Burst(p.x + (S.Rnd() - 0.5f) * 0.25f, p.y - 0.05, S.Rnd() < 0.5f ? "#fff1c9" : NOVA_GOLD, 1, 2, 0.3f + k * 0.2f, 0.22f, dir: -PI / 2, spread: 0.6f);
                if (S.Rnd() < 0.7f) Smoke(p.x, p.y - 0.2, "#8e97a3", 1, 0.8f, 0.45f + k * 0.35f, 0.7f, dir: -PI / 2, spread: 1, drag: 0.93f, grav: -0.4f, op: 0.4f);
            }
        }

        // Wall slides: grit off the wall at the hand and boot; Nova's skate blades grind sparks
        void WallGrit(World world)
        {
            foreach (var p in world.players)
            {
                if (!p.wallSliding) continue;
                using var origin = AtDepth(LevelFeatures.Depth(p));
                double wx = p.x + p.wallDir * p.w / 2; float speed = Mathf.Min(1, F(-p.vy) / 6);
                if (S.Rnd() < 0.2f + speed * 0.4f) Smoke(wx, p.y + p.h * 0.85, "#aab2bc", 1, 1.0f, 0.22f, 0.35f, dir: PI / 2, spread: 1, op: 0.45f);
                if (S.Rnd() < 0.3f + speed * 0.5f) Smoke(wx, p.y + 0.08, "#a3abb5", 1, 1.4f, 0.28f, 0.4f, dir: PI / 2 + F(p.wallDir) * 0.6f, spread: 0.8f, op: 0.5f);
                if (p.@char == "nova" && SETTINGS.novaKit == "marksman" && S.Rnd() < 0.35f + speed * 0.5f)
                    view.sparks.Emit(p.slot, wx, p.y + 0.05, 1.2f, p.wallDir > 0 ? PI - 0.5f : 0.5f, 3 + speed * 2, 0.8f, light: 0.6f, depth: (float)LevelFeatures.Depth(p), lane: p.lane);
            }
        }

        // Marksman Nova's light boosters: two small jets under the boots while they fire
        void ThrusterJets(World world, View view)
        {
            foreach (var p in world.players)
            {
                if (!p.thrusting) continue;
                using var origin = AtDepth(LevelFeatures.Depth(p));
                foreach (var dz in new[] { -0.13f, 0.13f })
                {
                    var v = W(p.x + (S.Rnd() - 0.5f) * 0.1f, p.y - 0.05, dz);
                    var P = Particle(v, S.Rnd() < 0.5f ? "#fff1c9" : NOVA_GOLD, 0.26f, 0.16f + S.Rnd() * 0.08f);
                    P.v = new Vector3((S.Rnd() - 0.5f) * 0.6f, -5 - S.Rnd() * 3, (S.Rnd() - 0.5f) * 0.6f); P.drag = 0.86f;
                }
            }
        }
        // Marksman Nova: a few sparks trail from the skate blades at speed
        void SkateSparks(World world)
        {
            if (SETTINGS.novaKit != "marksman") return;
            foreach (var p in world.players)
            {
                if (p.@char != "nova" || !p.onGround || System.Math.Abs(p.vx) < 6 || S.Rnd() > 0.35f) continue;
                view.sparks.Emit(p.slot, p.x - System.Math.Sign(p.vx) * 0.15, p.y + 0.05, 1, p.vx > 0 ? PI - 0.15f : 0.15f, 2.5f, 0.5f, light: 0.4f, depth: (float)LevelFeatures.Depth(p), lane: p.lane);
            }
        }
    }
}
