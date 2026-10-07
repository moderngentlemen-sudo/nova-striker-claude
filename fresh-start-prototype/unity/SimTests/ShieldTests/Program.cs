// Headless tests of the Unity-only options: Nova's absorbing shield (blocks, energy, overfill to 150%, firing
// behind it, perfect blocks) and the perfect-parry stun. `dotnet run` here; it prints PASS/FAIL per check.
using System; using System.Collections.Generic; using System.Linq; using NovaStriker.Sim; using static NovaStriker.Sim.Cfg;
static class P {
  static Cmd C(bool parry, double ax = 1) { var c = new Cmd { ax = ax, ay = 0, aimFree = true }; c.held.parry = parry; return c; }
  static int fails;
  static void Check(bool ok, string m) { Console.WriteLine((ok ? "PASS " : "FAIL ") + m); if (!ok) fails++; }
  static (World w, Player p, Enemy e) Setup(string def, bool stun, string enemy = "swarmer") {
    prev = new Cmd();
    SETTINGS = new Settings { novaKit = "marksman", novaDefense = def, novaParryStun = stun };
    var w = new World(); w.Teleport("gym"); w.enemies.Clear();
    var p = w.AddPlayer("pad0", "nova"); p.x = 10; p.y = 0; p.facing = 1;
    var e = Enemies.CreateEnemy(enemy, 12.2, 0, q => q.zone = "gym"); w.enemies.Add(e);
    return (w, p, e);
  }
  static List<Ev> Run(World w, Player p, int ticks, Func<int, Cmd> cmd) {
    var evs = new List<Ev>();
    for (int i = 0; i < ticks; i++) {
      var c = cmd(i);
      for (int b = 0; b < 10; b++) { c.pressed[b] = c.held[b] && !prev.held[b]; c.released[b] = !c.held[b] && prev.held[b]; }
      w.Step(new Dictionary<int, Cmd> { [p.slot] = c }); evs.AddRange(w.events); w.events.Clear(); prev = c;
    }
    return evs;
  }
  static Cmd prev = new Cmd();
  static void Dummy() {
  }
  static void Main(string[] argv) {
    // 1. Dodge (default): LT dodges, no shield state ever
    { var (w, p, e) = Setup("dodge", false); var ev = Run(w, p, 30, i => C(i < 3));
      Check(ev.Any(x => x.type == "dodge") && !ev.Any(x => x.type == "nshieldOn"), "default: LT dodges"); }
    // 2. Shield: held LT raises it; the swarmer's attacks are blocked and absorbed; no damage taken
    { var (w, p, e) = Setup("shield", false); double hp0 = p.hp;
      var ev = Run(w, p, 600, i => C(true, w.enemies.Count > 0 && w.enemies[0].x < p.x ? -1 : 1));
      int blocks = ev.Count(x => x.type == "nshieldBlock");
      Check(ev.Any(x => x.type == "nshieldOn"), "shield: LT raises the shield");
      Check(blocks > 0, $"shield: blocked {blocks} hits");
      Check(p.absorb > 0, $"shield: absorbed energy {p.absorb:0.0}");
      Check(p.hp >= hp0 - 1e-9 || ev.Any(x=>x.type=="nshieldBreak"), $"shield: no damage taken while it holds (hp {p.hp:0.0}/{hp0})");
      Check(PlayerSim.AbsorbMult(p) > 1, $"shield: damage multiplier {PlayerSim.AbsorbMult(p):0.00}");
      // the energy makes his hits harder: compare damage on a fresh enemy with and without energy
      double a = p.absorb;
      var t = Enemies.CreateEnemy("brute", 0, 0); double h0 = t.hp; Combat.HitEnemy(w, t, new Hit { dmg = 5, poise = 0, owner = p, kb = new[]{0.0,0.0} }, "melee");
      double withE = h0 - t.hp; p.absorb = 0;
      var t2 = Enemies.CreateEnemy("brute", 0, 0); double h2 = t2.hp; Combat.HitEnemy(w, t2, new Hit { dmg = 5, poise = 0, owner = p, kb = new[]{0.0,0.0} }, "melee");
      double without = h2 - t2.hp;
      Check(withE > without * 1.01, $"shield: energy {a:0} raises damage {without:0.00} -> {withE:0.00}");
      // releasing LT lowers it; the energy holds, then drains
      p.absorb = 50; p.absorbIdle = 0; w.enemies.Clear();
      Run(w, p, 120, i => C(false)); double held = p.absorb;
      Run(w, p, 600, i => C(false));
      Check(held == 50 && p.absorb < 50, $"energy holds ({held:0}) then drains ({p.absorb:0.0})");
      Check(p.state != "nshield", "shield lowered when LT released");
    }
    // 3. Perfect block with the stun option: sweep the raise time after the first windup starts (the sim is deterministic)
    { bool stun=false, perf=false; string info="";
      for (int off = 0; off < 60 && !stun; off++) {
        var (w, p, e) = Setup("shield", true); var ev = new List<Ev>(); int ws = -1;
        for (int t = 0; t < 400; t++) {
          if (ws < 0 && e.state == "windup") ws = t;
          bool hold = ws >= 0 && t >= ws + off && t < ws + off + 30;
          ev.AddRange(Run(w, p, 1, i => C(hold, e.x < p.x ? -1 : 1)));
          if (ws >= 0 && t > ws + off + 30) break;
        }
        if (ev.Any(x => x.type == "nshieldBlock" && x.perfect)) perf = true;
        if (ev.Any(x => x.type == "parryStun")) { stun = true; info = $" (offset {off}, state {e.state}, dizzy {e.dizzy})"; }
      }
      Check(perf, "a perfect shield block was registered");
      Check(stun, "stun option: a perfect shield block stuns the attacker" + info);
    }
    // 3b. Firing from behind the shield: taps and a charged shot, and the shield stays up
    { var (w, p, e) = Setup("shield", false); w.enemies.Clear();
      Cmd F(bool fire) { var c = C(true); c.held.fire = fire; return c; }
      var ev = Run(w, p, 10, i => C(true));
      bool up0 = p.state == "nshield";
      ev = Run(w, p, 40, i => F(i % 10 < 2));
      int taps = ev.Count(x => x.type == "shot"); bool stillUp = p.state == "nshield";
      ev = Run(w, p, (int)MARKSMAN.charge[1] + 5, i => F(true)); ev.AddRange(Run(w, p, 3, i => F(false)));
      bool charged = ev.Any(x => x.type != "chargeLevel" && x.level >= 1) || ev.Any(x => x.type.StartsWith("shot") && x.level >= 1);
      Check(up0 && stillUp && taps >= 3, $"fire with the shield up: {taps} shots, shield still up ({p.state})");
      Check(p.state == "nshield" && ev.Any(x => x.type == "chargeLevel"), "charged shot from behind the shield: " + string.Join(",", ev.Select(x => x.type).Distinct()));
      var melee = C(true); melee.held.melee = true; Run(w, p, 3, i => melee);
      Check(p.state != "nshield", "melee still lowers it");
    }
    // 3c. Overfill: the charge goes past 100 to 150; +100% damage at 100, +150% at 150
    { var (w, p, e) = Setup("shield", false); var ev = new List<Ev>();
      for (int k = 0; k < 20; k++) { ev.Add(null); PlayerSim.NovaShieldBlock(p, w, 4, false, p.x + 1, p.y + 1, false); p.nshieldStab = 100; }
      ev = w.events.ToList(); w.events.Clear();
      Check(System.Math.Abs(p.absorb - 150) < 1e-9, $"charge overfills and caps at 150 ({p.absorb:0.0})");
      Check(ev.Count(x => x.type == "nshieldBlock" && x.full) == 1 && ev.Count(x => x.type == "nshieldBlock" && x.max) == 1, "one 'full' and one 'max' event");
      Check(System.Math.Abs(PlayerSim.AbsorbMult(p) - 2.5) < 1e-9, $"x{PlayerSim.AbsorbMult(p):0.00} at 150");
      p.absorb = 100; Check(System.Math.Abs(PlayerSim.AbsorbMult(p) - 2.0) < 1e-9, $"x{PlayerSim.AbsorbMult(p):0.00} at 100");
    }
    // 4. Without the stun option, no stun
    { var (w, p, e) = Setup("shield", false); var ev = Run(w, p, 600, i => C(i % 90 < 40));
      Check(!ev.Any(x => x.type == "parryStun"), "no stun without the option"); }
    // 5. Sentinel kit perfect parry with the stun option (same sweep)
    { bool perf=false, stun=false;
      for (int off = 0; off < 60 && !stun; off++) {
        SETTINGS = new Settings { novaKit = "sentinel", novaParryStun = true };
        var w = new World(); w.Teleport("gym"); w.enemies.Clear();
        var p = w.AddPlayer("pad0", "nova"); p.x = 10; p.y = 0; var e = Enemies.CreateEnemy("swarmer", 12.2, 0, q => q.zone = "gym"); w.enemies.Add(e);
        var ev = new List<Ev>(); prev = new Cmd(); int ws = -1;
        for (int t = 0; t < 400; t++) {
          if (ws < 0 && e.state == "windup") ws = t;
          bool press = ws >= 0 && t >= ws + off && t < ws + off + 3;
          ev.AddRange(Run(w, p, 1, i => C(press, e.x < p.x ? -1 : 1)));
          if (ws >= 0 && t > ws + off + 30) break;
        }
        if (ev.Any(x => x.type == "parry" && x.perfect)) perf = true;
        if (ev.Any(x => x.type == "parryStun")) stun = true;
      }
      Check(perf, "sentinel: perfect parry happened");
      Check(stun, "sentinel + stun option: perfect parry stuns");
    }
    Console.WriteLine(fails == 0 ? "ALL PASS" : $"{fails} FAILED");
  }
}
