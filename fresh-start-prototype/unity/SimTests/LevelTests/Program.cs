// Headless tests of the level features (Sim/LevelFeatures.cs): multi-tier catwalks, hazards and depth lanes, and
// that with all three off the level is exactly as built. `dotnet run` here; it prints PASS/FAIL per check.
using System; using System.Collections.Generic; using System.Linq; using NovaStriker.Sim; using static NovaStriker.Sim.Cfg;
static class P {
  static int fails;
  static void Check(bool ok, string m) { Console.WriteLine((ok ? "PASS " : "FAIL ") + m); if (!ok) fails++; }
  static World Make(bool tiers, bool hazards, bool lanes, string zone) {
    SETTINGS = new Settings { levelTiers = tiers, levelHazards = hazards, depthLanes = lanes };
    var w = new World(); w.Teleport(zone); Level.RestoreBoxes(); w.enemies.Clear(); return w;
  }
  static List<Ev> Run(World w, Player p, int ticks, Func<int, Cmd> cmd = null, bool clearEnemies = true) {
    var evs = new List<Ev>();
    for (int i = 0; i < ticks; i++) {
      if (clearEnemies) w.enemies.Clear();
      var c = cmd != null ? cmd(i) : new Cmd();
      w.Step(new Dictionary<int, Cmd> { [p.slot] = c }); evs.AddRange(w.events); w.events.Clear();
    }
    return evs;
  }
  static void Main() {
    // 1. All off: the level is exactly as built
    var w0 = Make(false, false, false, "gym"); int baseCount = Level.BOXES.Count; int baseVersion = Level.Version;
    Check(!Level.BOXES.Any(b => b.id >= Level.EXTRA_ID) && Level.HAZARDS.Count == 0 && Level.LANES.Count == 0, $"off: no extra boxes, hazards or lanes ({baseCount} boxes)");
    var first = Level.BOXES[0]; var last = Level.BOXES[Level.BOXES.Count - 1];

    // 2. Tiers on: catwalks inside their zones; a player lands on one; off again restores the level
    { var w = Make(true, false, false, "gym");
      var extra = Level.BOXES.Where(b => b.id >= Level.EXTRA_ID).ToList();
      Check(extra.Count > 20 && extra.All(b => b.type == 'o'), $"tiers: {extra.Count} one-way catwalks");
      Check(extra.All(b => Level.ZONES.Any(z => b.x0 >= z.x0 - 1 && b.x1 <= z.x1 + 1)), "tiers: every catwalk lies inside a zone");
      Check(extra.All(b => !Level.CHECKPOINTS.Any(c => c.x > b.x0 - 1 && c.x < b.x1 + 1 && System.Math.Abs(c.y - b.y1) < 3)), "tiers: none sits on a checkpoint");
      var p = w.AddPlayer("pad0", "nova"); p.x = 9; p.y = 5.5; p.vy = 0;
      Run(w, p, 90);
      Check(System.Math.Abs(p.y - 4.0) < 0.05 && p.onGround, $"tiers: a player lands on the gym catwalk (y {p.y:0.00})");
      Make(false, false, false, "gym");
      Check(Level.BOXES.Count == baseCount && Level.BOXES[0] == first && Level.BOXES[Level.BOXES.Count - 1] == last, "tiers off again: the level is as built");
    }

    // 3. Hazards
    { var w = Make(false, true, false, "gym");
      var vent = Level.HAZARDS.First(h => h.kind == "vent");
      var p = w.AddPlayer("pad0", "nova"); p.x = (vent.x0 + vent.x1) / 2; p.y = 0;
      double top = 0; var ev = Run(w, p, vent.period + 5, i => { top = System.Math.Max(top, p.y); return new Cmd(); });
      Check(ev.Any(e => e.type == "hazardWarn") && ev.Any(e => e.type == "hazardOn"), "hazards: the vent warns, then fires");
      Check(top > 4, $"hazards: the vent launches a player standing on it (up to {top:0.0} m)");
    }
    { var w = Make(false, true, false, "arena");
      var shock = Level.HAZARDS.First(h => h.kind == "shock" && h.zone == "arena");
      var p = w.AddPlayer("pad0", "nova"); p.x = (shock.x0 + shock.x1) / 2; p.y = 0;
      bool hurtWhileOff = false, hurtWhileOn = false; double hp = p.hp;
      for (int i = 0; i < shock.period + 10; i++) {
        Run(w, p, 1); p.x = (shock.x0 + shock.x1) / 2;   // (held on the panel)
        if (p.hp < hp - 1e-9) { if (shock.state == "on") hurtWhileOn = true; else hurtWhileOff = true; }
        hp = p.hp;
      }
      Check(hurtWhileOn && !hurtWhileOff, "hazards: a shock panel hurts only while it is live");
    }
    { var w = Make(false, true, false, "skyline");
      var col = Level.HAZARDS.First(h => h.kind == "collapse" && h.x0 >= 200);
      var p = w.AddPlayer("pad0", "nova"); p.x = (col.x0 + col.x1) / 2; p.y = col.y1; p.onGround = true;
      Run(w, p, 5);
      bool standing = System.Math.Abs(p.y - col.y1) < 0.05;
      Run(w, p, 60);
      Check(standing && col.state == "on" && p.y < col.y1 - 1, $"hazards: a crumbling platform gives way under a player (y {p.y:0.0})");
      Run(w, p, 370);
      Check(col.state == "idle" && System.Math.Abs(col.box.y1 - col.y1) < 1e-9, "hazards: the platform comes back");
    }
    { var w = Make(false, false, false, "gym"); var p = w.AddPlayer("pad0", "nova"); p.x = 5; p.y = 0;
      var ev = Run(w, p, 400);
      Check(!ev.Any(e => e.type.StartsWith("hazard")), "hazards off: no hazard events");
    }

    // 3b. AI teammates and hazards: a bot following the player hops the slag and waits out a crusher, and steps off a
    // shock panel before it goes live, without being hurt
    { (bool hurt, double x) Follow(string zone, double botX, double leadX, int ticks, Func<World, Hazard> pick) {
        var w = Make(false, true, false, zone); var bots = new Bots();
        var lead = w.AddPlayer("pad0", "nova"); lead.x = leadX; lead.y = 0;
        bots.Sync(w, 1); var bot = w.players.First(Bots.IsBot); bot.x = botX; bot.y = 0; bot.vx = 0;
        var h = pick(w); bool hurt = false;
        for (int i = 0; i < ticks; i++) {
          w.enemies.Clear(); lead.x = leadX; lead.vx = 0;
          var cmds = new Dictionary<int, Cmd> { [lead.slot] = new Cmd() }; bots.Commands(w, cmds); w.Step(cmds);
          if (w.events.Any(e => e.type == "hazardHit" && e.p == bot)) hurt = true; w.events.Clear();
        }
        return (hurt, bot.x);
      }
      var slag = Follow("foundry", 517, 533, 600, w => Level.HAZARDS.First(h => h.kind == "slag"));
      Check(!slag.hurt && slag.x > 527.5, $"bots: a teammate crosses the slag channel unhurt (x {slag.x:0.0})");
      // a crusher about to come down just ahead: walking straight on would be under it when it slams
      { var w = Make(false, true, false, "foundry"); var bots = new Bots();
        var cr = Level.HAZARDS.First(h => h.kind == "crusher" && h.x0 < 450);
        var lead = w.AddPlayer("pad0", "nova"); lead.x = 452; lead.y = 0; bots.Sync(w, 1); var bot = w.players.First(Bots.IsBot);
        void Tick() { w.enemies.Clear(); lead.x = 452; lead.vx = 0; var c = new Dictionary<int, Cmd> { [lead.slot] = new Cmd() }; bots.Commands(w, c); w.Step(c); }
        for (int i = 0; i < 2000; i++) { long c = ((long)w.tick + cr.phase) % cr.period; if (c == cr.period - cr.on - 12) break; Tick(); w.events.Clear(); bot.x = 436; bot.vx = 0; }
        bot.x = cr.x0 - 1.0; bot.y = 0; bot.vx = 0; bool hurt = false;
        for (int i = 0; i < 400; i++) { Tick(); if (w.events.Any(e => e.type == "hazardHit" && e.p == bot)) hurt = true; w.events.Clear(); }
        Check(!hurt && bot.x > cr.x1, $"bots: a teammate waits out a crusher about to slam, then passes unhurt (x {bot.x:0.0})");
      }
      // a bot dropped on a shock panel just as it starts to warn gets off before it is live
      { var w = Make(false, true, false, "arena"); var bots = new Bots();
        var shock = Level.HAZARDS.First(h => h.kind == "shock" && h.zone == "arena");
        var lead = w.AddPlayer("pad0", "nova"); lead.x = 72; lead.y = 0; bots.Sync(w, 1); var bot = w.players.First(Bots.IsBot);   // (the player stands on the panel: a bot that just follows would too)
        for (int i = 0; i < 2000 && shock.state != "warn"; i++) { w.enemies.Clear(); var c0 = new Dictionary<int, Cmd> { [lead.slot] = new Cmd() }; bots.Commands(w, c0); w.Step(c0); w.events.Clear(); bot.x = 62; lead.x = 72; lead.hp = lead.maxHp; }
        bot.x = shock.x0 + 0.6; bot.y = 0; bot.vx = 0; bool hurt = false;
        for (int i = 0; i < shock.warn + shock.on; i++) {
          w.enemies.Clear(); lead.x = 72; lead.hp = lead.maxHp; var cmds = new Dictionary<int, Cmd> { [lead.slot] = new Cmd() }; bots.Commands(w, cmds); w.Step(cmds);
          if (w.events.Any(e => e.type == "hazardHit" && e.p == bot)) hurt = true; w.events.Clear();
        }
        Check(!hurt, $"bots: a teammate on a shock panel leaves it before it is live (x {bot.x:0.0}, panel {shock.x0}-{shock.x1})");
      }
    }

    // 3c. The right stick picking targets (Settings: Gamepad right stick): a flick locks the enemy that way, another
    // flick switches; holding the aim-down button aims at the feet, so a charged shot launches (a rocket jump)
    { var w = Make(false, false, false, "gym"); SETTINGS.padAim = "target";
      var p = w.AddPlayer("pad0", "nova"); p.x = 20; p.y = 0;
      Run(w, p, 5);
      var right = Enemies.CreateEnemy("swarmer", 26, 0); var left = Enemies.CreateEnemy("swarmer", 14, 0);
      w.enemies.Add(right); w.enemies.Add(left);
      Run(w, p, 1, i => new Cmd { pick = true, px = 1, py = 0 }, false);
      bool gotRight = p.lockT == right;
      Run(w, p, 1, i => new Cmd { pick = true, px = -1, py = 0 }, false);
      Check(gotRight && p.lockT == left, "stick targeting: a flick locks the enemy that way, another flick switches");
      Run(w, p, 1, i => new Cmd { aimDown = true }, false);
      Check(p.aimX == 0 && p.aimY == -1, "stick targeting: the aim-down button aims straight at the feet");
      w.enemies.Clear(); double top = 0; int charge = (int)MARKSMAN.charge[2] + 2;
      Run(w, p, charge + 60, i => { top = System.Math.Max(top, p.y); var c = new Cmd { aimDown = true }; c.held.fire = i < charge; c.pressed.fire = i == 0; c.released.fire = i == charge; return c; });
      Check(top > 2.5, $"stick targeting: aim down, charge and let go rocket-jumps (up to {top:0.0} m)");
      SETTINGS.padAim = "free";
    }

    // 4. Depth lanes
    { var w = Make(false, false, true, "gym");
      var p = w.AddPlayer("pad0", "nova"); p.x = 52; p.y = 0;
      Run(w, p, 2);
      var ev = Run(w, p, 1, i => new Cmd { lane = -1 });
      int midLane = 99; Run(w, p, 4); midLane = p.lane; Run(w, p, 1);
      bool changedAtHalf = midLane == 0 && p.lane == -1;
      Run(w, p, 10);
      Check(ev.Any(e => e.type == "laneHop") && p.lane == -1 && p.laneT == 0, $"lanes: B hops to the back lane (lane {p.lane})");
      Check(changedAtHalf, "lanes: the lane changes halfway through the hop");
      var e1 = Enemies.CreateEnemy("swarmer", 54, 0); e1.lane = 0; e1.laneTo = 0;
      Check(!LevelFeatures.Same(p, e1) && LevelFeatures.Same(p, e1, true), "lanes: shots miss another lane; blasts reach it");
      p.x = 30; Run(w, p, 14);
      Check(p.lane == 0, "lanes: leaving the stretch brings a player back to the middle");
    }
    { var w = Make(false, false, false, "gym"); var p = w.AddPlayer("pad0", "nova"); p.x = 52; p.y = 0;
      Run(w, p, 12, i => new Cmd { lane = i == 1 ? -1 : 0 });
      Check(p.lane == 0 && p.laneT == 0 && LevelFeatures.Same(p, Enemies.CreateEnemy("swarmer", 54, 0)), "lanes off: no hops, everything reaches everything");
    }
    Console.WriteLine(fails == 0 ? "ALL PASS" : fails + " FAILED");
  }
}
