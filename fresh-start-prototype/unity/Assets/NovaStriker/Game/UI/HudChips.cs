// The chips under each player's health bar (ui.js): what each character is doing and has ready.
using System.Collections.Generic;
using System.Linq;
using NovaStriker.Sim;
using UnityEngine;
using static NovaStriker.Sim.Cfg;

namespace NovaStriker.Game.UI
{
    public static class HudChips
    {
        static string Rep(string s, double n) => n > 0 ? string.Concat(Enumerable.Repeat(s, (int)n)) : "";
        static int Ceil(double v) => (int)System.Math.Ceiling(v);

        // Echo's scarf mode chip: every player can read which mode his scarf is in
        static Chip ScarfChip(Player p)
        {
            if (p.scarfMode == "veil")
            {
                string txt = p.veiled ? "Veil · hidden" : p.veilBreakT > 0 ? $"Veil · {Ceil(p.veilBreakT / 60)}s" : "Veil · fading";
                return Chip.Of(txt, p.veiled ? "veilon" : "veil");
            }
            if (p.scarfMode == "flare") return Chip.Of("Flare" + (p.targetedBy != 0 ? $" · {p.targetedBy} on you" : ""), "flare");
            return Chip.Of("Tether", "tether");
        }
        static Chip ScarfCharges(Player p) => Chip.Of("Scarf " + Rep("◆", p.lashCharges) + Rep("◇", ECHO.lashCharges - p.lashCharges));

        // Nova, Marksman kit: loaded attachment and secondary weapon, charge stages (the Perfect Release window reads
        // "Release!"), and Focus
        static readonly Dictionary<string, string> STAGE = new Dictionary<string, string>
        { ["charging"] = "charging", ["L1"] = "Level 1", ["L2"] = "Level 2", ["perfect"] = "Release!", ["L3"] = "Level 3", ["L4"] = "Level 4 · Beam" };
        static string Stage(string s) => s != null && STAGE.TryGetValue(s, out var v) ? v : null;
        static Color Tint(string css) => Pal.C(css);

        static void Marksman(List<Chip> o, Player p, World world)
        {
            var A = ATTACH_LOOK[p.attachment]; var S = SUB_LOOK.TryGetValue(p.sub, out var sl) ? sl : SUB_LOOK["scatter"];
            string stage = PlayerSim.ChargeStage(p), bstage = PlayerSim.BurstStage(p); int f = (int)System.Math.Floor(p.focus);
            float fuel = (float)(p.fuel / MARKSMAN.boost.fuel);
            string @out = (p.sub == "disc" || p.sub == "well") && world.SubOut(p, p.sub) ? (p.sub == "disc" ? " · out" : " · open") : "";
            o.Add(Chip.Of(A.name, "attach", Tint(A.tint)));
            o.Add(Chip.Of(S.name + @out, "attach", Tint(S.tint)));
            if (p.state == "beam" && p.beam != null) o.Add(Chip.Of($"Beam {(p.beam.t / 60):0.0}s", "perfect"));
            if (Stage(stage) != null) o.Add(Chip.Of(Stage(stage), stage == "perfect" || stage == "L4" ? "perfect" : "ready"));
            if (Stage(bstage) != null) o.Add(Chip.Of($"{S.name} {Stage(bstage)}", bstage == "perfect" ? "perfect" : "ready"));
            o.Add(Chip.Of("Focus " + Rep("◆", f) + Rep("◇", MARKSMAN.focus.max - f), f > 0 ? "focuson" : ""));
            o.Add(Chip.Bar(44, fuel, Pal.C("#ffd88a")));
            Aegis(o, p);
        }
        // The hard-light Aegis: its strength while up, else its cooldown; Overcharge from the damage it soaked
        static void Aegis(List<Chip> o, Player p)
        {
            var a = p.aegis;
            if (a != null) { o.Add(Chip.Of("Aegis", "perfect")); o.Add(Chip.Bar(48, (float)System.Math.Max(0, a.hp / a.max), Pal.C("#fff0c8"))); }
            else o.Add(Chip.Of("Aegis " + (p.aegisCd == 0 ? "ready" : Ceil(p.aegisCd / 60) + "s"), p.aegisCd == 0 ? "ready" : ""));
            if (p.overcharge > 0) { o.Add(Chip.Of("Overcharged", "over")); o.Add(Chip.Bar(40, (float)(p.overcharge / AEGIS.over.max), Color.white)); }
        }

        // RAM: the Rampart's Integrity, stored Kinetic, the cannon's charge, and his three abilities
        static Chip Cd(string name, double t) => Chip.Of(name + " " + (t <= 0 ? "ready" : Ceil(t / 60) + "s"), t <= 0 ? "ready" : "");
        static void Ram(List<Chip> o, Player p)
        {
            double frac = System.Math.Max(0, p.integrity / RAM.guard.integrity); int kin = (int)System.Math.Round(p.kinetic); string stage = PlayerSim.ChargeStage(p);
            o.Add(Chip.Bar(56, (float)frac, p.guardBroken ? Pal.C("#ff5a6e") : Pal.C("#8fc4ff")));
            if (p.guardBroken) o.Add(Chip.Of("Broken", "red")); else if (p.state == "guard") o.Add(Chip.Of("Guard", "ram"));
            o.Add(Chip.Of($"Kinetic {kin}%", kin >= RAM.release.min ? "kinon" : "kin"));
            if (Stage(stage) != null) o.Add(Chip.Of("Breach " + Stage(stage), stage == "L3" ? "perfect" : "ready"));
            o.Add(Cd("Wall", p.wallCd)); o.Add(Cd("Link", p.linkCd)); o.Add(Cd("Provoke", p.provokeCd));
            if (p.link != null) o.Add(Chip.Of($"Guarding P{p.link.q.slot + 1}", "ram"));
        }
        // Fix: her Scrap, the gadget and power-up she has picked (and how her gadgets stand), who her beam holds,
        // and the rivet gun's charge
        static void Fix(List<Chip> o, Player p, World world)
        {
            var G = FIX_LOOK[p.gadgetSel]; var Wp = FIX_LOOK[p.powerSel]; double cost = FIX.gadget[p.gadgetSel].cost; string stage = PlayerSim.ChargeStage(p);
            var q = p.state == "patch" && p.patch != null ? p.patch.target : null;
            o.Add(Chip.Bar(50, (float)(p.scrap / FIX.scrap.max), Pal.C("#ffd95a")));
            o.Add(Chip.Of($"Scrap {(int)System.Math.Floor(p.scrap)}"));
            o.Add(Chip.Of($"{G.name} · {cost}", "attach", Tint(G.tint)));
            o.Add(Chip.Of($"{Wp.name} · {FIX.power.cost}", "attach", Tint(Wp.tint)));
            foreach (var g in world.gadgets.Where(g => g.owner == p && g.kind != "pad"))
                o.Add(Chip.Of($"{FIX_LOOK[g.kind].name.Replace("Patch ", "").Replace("Amp ", "")} L{g.level} {Ceil((g.life - g.t) / 60)}s", "fix"));
            if (p.state == "patch") o.Add(Chip.Of(q != null ? (q.state == "downed" ? $"Reviving P{q.slot + 1}" : $"Patching P{q.slot + 1}") : "Welding", "fixon"));
            if (Stage(stage) != null) o.Add(Chip.Of("Hot Rivet " + Stage(stage), stage == "L3" ? "perfect" : "ready"));
        }
        // Boosts anyone can carry: Plating, Overclock, Fury, Tune-Up, an Amp Coil's field, Provoke's brace
        static void Boosts(List<Chip> o, Player p)
        {
            if (p.plate > 0.5) o.Add(Chip.Of($"Plating {Ceil(p.plate)}", "plate"));
            if (p.overclockT > 0) o.Add(Chip.Of($"Overclock {Ceil(p.overclockT / 60)}s", "over2"));
            if (p.furyT > 0) o.Add(Chip.Of($"Fury {Ceil(p.furyT / 60)}s", "fury"));
            if (p.tuneT > 0) o.Add(Chip.Of("Tuned up", "fixon"));
            if (p.ampK > 1) o.Add(Chip.Of($"Amp x{p.ampK}", "over2"));
            if (p.braceT > 0) o.Add(Chip.Of("Braced", "ram"));
        }

        // Chips every character can show: a charging dash, a charging pound, and the lock-on target
        public static readonly Dictionary<string, string> ENEMY_NAMES = new Dictionary<string, string>
        { ["swarmer"] = "Swarmer", ["shield"] = "Shieldbearer", ["sniper"] = "Sniper", ["brute"] = "Brute", ["post"] = "Sparring post", ["turret"] = "Turret",
          ["drone"] = "Drone", ["mortar"] = "Mortar", ["charger"] = "Charger" };
        static void Common(List<Chip> o, Player p, Color pc)
        {
            var C = DASH_CHARGE.charge; double t = p.state == "dashCharge" ? p.dashChargeT : 0;
            int L = t >= C[2] ? 3 : t >= C[1] ? 2 : t >= C[0] ? 1 : 0;
            double pl = p.state == "pound" && p.pound != null && p.pound.phase == "hold" ? p.pound.level : 0;
            if (L > 0) o.Add(Chip.Of($"Dash {L}", L == 3 ? "perfect" : "ready"));
            if (pl > 0) o.Add(Chip.Of($"Pound {pl}", pl == 3 ? "perfect" : "ready"));
            if (p.lockT != null) o.Add(Chip.Of("◎ " + (ENEMY_NAMES.TryGetValue(p.lockT.type, out var n) ? n : p.lockT.boss ? "Boss" : "Target"), "lock", pc));
            else if (p.lockSuspend) o.Add(Chip.Of("Lock paused"));
        }
        // Echo's sniper rifle (Hunter kit): its focus while scoped, the bolt cycling after a shot, and Riposte
        static void Rifle(List<Chip> o, Player p)
        {
            if (p.rifleT >= HUNTER.rifle.raise && p.rifleCd == 0)
            {
                double f = PlayerSim.RifleFocus(p.rifleT);
                o.Add(f >= 1 ? Chip.Of("Full focus", "red") : Chip.Of($"Scope {System.Math.Round(f * 100)}%", "ready"));
            }
            else if (p.rifleCd > 0) o.Add(Chip.Of($"Bolt {(p.rifleCd / 60):0.0}s"));
            if (p.riposteT > 0) o.Add(Chip.Of("Riposte!", "perfect"));
        }
        static void Vb(List<Chip> o, Player p) { double v = PlayerSim.VbTier(p); if (v != 0) o.Add(Chip.Of($"VB {v}", "vb")); }

        // The whole row for a player, or the status line when they are down or dead (returned as `status`)
        public static List<Chip> For(Player p, World world, Color pc, out string status)
        {
            var o = new List<Chip>(); status = null;
            if (p.state == "downed") { status = p.autoRevive > 0 ? "Second Wind…" : $"Down · revive {(int)System.Math.Floor(p.revive / 1.2)}% · {Ceil(p.downedT / 60)}s"; return o; }
            if (p.state == "dead") { status = $"Respawning in {Ceil(p.respawnT / 60)}s"; return o; }
            if (p.@char == "ram") { Ram(o, p); Vb(o, p); Common(o, p, pc); }
            else if (p.@char == "fix") { Fix(o, p, world); Vb(o, p); Common(o, p, pc); }
            else if (p.@char == "nova")
            {
                if (SETTINGS.novaKit == "marksman") { Marksman(o, p, world); Vb(o, p); Common(o, p, pc); }
                else
                {
                    o.Add(Chip.Of("Bulwark " + (p.bulwarkCd == 0 ? "ready" : Ceil(p.bulwarkCd / 60) + "s"), p.bulwarkCd == 0 ? "ready" : ""));
                    string ch = p.chargeT >= NOVA.charge2 ? "RAIL" : p.chargeT >= NOVA.charge1 ? "LANCE" : p.chargeT > 0 ? "charging" : "";
                    if (ch != "") o.Add(Chip.Of(ch, "ready"));
                    Vb(o, p); Common(o, p, pc);
                }
            }
            else if (SETTINGS.echoKit == "hunter")
            {
                o.Add(Chip.Bar(64, (float)(p.resolve / 100), Pal.C("#ffd08a"))); o.Add(ScarfChip(p)); o.Add(ScarfCharges(p));
                o.Add(Chip.Of("Snares " + Rep("◆", p.snares) + Rep("◇", HUNTER.snareCharges - p.snares), p.snares > 0 ? "ready" : ""));
                Rifle(o, p); if (p.leash != null) o.Add(Chip.Of("Reeling", "vb")); Vb(o, p); Common(o, p, pc);
            }
            else
            {
                string mode = SETTINGS.echoRanged;
                o.Add(Chip.Bar(64, (float)(p.resolve / 100), Pal.C("#ffd08a"))); o.Add(ScarfChip(p)); o.Add(ScarfCharges(p));
                if (mode == "B") o.Add(Chip.Of("Bolts " + Rep("●", p.cells) + Rep("○", ECHO.cellsMax - p.cells)));
                else if (mode == "A") o.Add(Chip.Of("Tracer " + (p.tracerCd == 0 ? "ready" : ""), p.tracerCd == 0 ? "ready" : ""));
                Vb(o, p); Common(o, p, pc);
            }
            Boosts(o, p);
            return o;
        }
    }
}
