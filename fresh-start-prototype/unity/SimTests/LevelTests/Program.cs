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
    // 5. Collision masks, blocked hops, immutable event/projectile origins, four kits.
    foreach (var character in new[] { "nova", "echo", "ram", "fix" }) {
      var w=Make(true,false,true,"gym");var p=w.AddPlayer("pad0",character);p.x=52;p.y=0;Run(w,p,2);
      var block=new LevelBox { id=22000,x0=51,x1=53,y0=0,y1=3,type='s',laneMask=4 };Level.BOXES.Add(block);
      var ev=Run(w,p,12,i=>new Cmd {lane=i==0?1:0});
      Check(p.lane==0 && ev.Any(e=>e.type=="laneBlocked"), character+": solid destination blocks hop");
      Check(!Level.PointInSolid(52,1,0) && Level.PointInSolid(52,1,1), character+": solid query respects lane mask");
      Level.BOXES.Remove(block);Run(w,p,12,i=>new Cmd {lane=i==0?1:0});
      Check(p.lane==1,character+": safe outer lane is reachable");
      w.Emit("shot",new Ev {p=p,x=p.x,y=p.y});var shot=w.events.Last();
      var projectile=new Projectile {owner=p,team="p",x=52,y=2,vx=10,ttl=60};w.SpawnProjectile(projectile);
      p.lane=p.laneTo=-1;Run(w,p,1);
      Check(projectile.lane==1 && shot.depth==LevelFeatures.LANE_W,character+": shot/event depth stays at emission lane");
      var platform=Level.BOXES.First(b=>b.tag=="tier" && b.x0>50);platform.laneMask=2;
      p.x=53;p.y=platform.y1;p.onGround=true;p.lane=p.laneTo=0;p.laneT=0;
      Check(!LevelFeatures.CanHop(p,1),character+": hop rejects unsupported tier destination");
      w.Teleport("gym");Check(w.players.All(q=>q.lane==0&&q.laneT==0),character+": checkpoint/zone respawn resets lane");
    }
    // Every feature combination rebuilds the exact expected lists.
    for(int bits=0;bits<8;bits++) {
      var w=Make((bits&1)!=0,(bits&2)!=0,(bits&4)!=0,"gym");
      Check((Level.TIER_SPAWNS.Count>0)==((bits&1)!=0)&&(Level.HAZARDS.Count>0)==((bits&2)!=0)&&(Level.LANES.Count>0)==((bits&4)!=0),"feature combination "+bits+": consistent rebuild");
    }
    // Hazard timing is independent of graphics settings, all nine families reset.
    {
      var w=Make(true,true,true,"foundry");var p=w.AddPlayer("pad0","ram");
      foreach(var kind in new[]{"vent","shock","laser","lightning","crusher","slag","wind","debris","collapse"}) {
        var h=Level.HAZARDS.First(x=>x.kind==kind);h.state="on";h.t=9;h.hit.Add("probe");
      }
      Level.RestoreBoxes();
      Check(Level.HAZARDS.All(h=>h.state=="idle"&&h.t==0&&h.hit.Count==0&&(h.box==null||h.box.y1==h.boxY1)),"all hazard families reset and collapse support restores");
      var crusher=Level.HAZARDS.First(h=>h.kind=="crusher");p.x=(crusher.x0+crusher.x1)/2;p.y=0;p.hp=p.maxHp;p.mercy=0;
      w.tick=crusher.period-crusher.on-crusher.phase-1;double hp=p.hp;
      Run(w,p,1);Check(p.hp==hp,"crusher: no damage at descent start");
      p.x=(crusher.x0+crusher.x1)/2;p.y=0;p.mercy=0;w.tick=crusher.period-2-crusher.phase;Run(w,p,1);
      Check(p.hp<hp,"crusher: impact window damages occupant");
      var shock=Level.HAZARDS.First(h=>h.kind=="shock");shock.laneMask=2;p.x=(shock.x0+shock.x1)/2;p.y=shock.y0;p.lane=p.laneTo=1;
      Check(!LevelFeatures.Inside(shock,p),"hazard mask excludes another lane");
      var slag=Level.HAZARDS.First(h=>h.kind=="slag");w.tick=0;Run(w,p,1);Check(slag.state=="on","slag: permanent boundary reports active");
    }
    // Actual jump capabilities constrain a useful tier waypoint, with no teleport.
    foreach(var character in new[]{"nova","echo","ram","fix"}) {
      var w=Make(true,false,false,"gym");var p=w.AddPlayer("pad0",character);p.x=7;p.y=0;p.onGround=true;
      var next=LevelNavigation.Waypoint(p,22,4.2);
      Check(next.x<22 && next.x>=7 && next.y>=0 && next.y<=4.2,character+": upper route yields reachable intermediate waypoint");
    }
    // All noncollapse hazard families: warnings never damage; every damaging window can reach its occupant.
    foreach(var kind in new[]{"vent","shock","laser","lightning","crusher","slag","wind","debris"}) {
      var w=Make(false,true,false,"gym");var p=w.AddPlayer("pad0","ram");var h=Level.HAZARDS.First(x=>x.kind==kind);
      bool offDamage=false,onDamage=false;double highestVy=0;
      for(int i=0;i<Math.Max(1,h.period);i++) {
        p.x=(h.x0+h.x1)/2;p.y=h.y0;p.onGround=true;p.mercy=0;p.state="normal";
        double hp=p.hp;w.tick=i;LevelFeatures.Step(w,null,false);highestVy=Math.Max(highestVy,p.vy);
        if(p.hp<hp){if(h.state=="on")onDamage=true;else offDamage=true;}
      }
      Check(!offDamage&&(h.dmg<=0||onDamage),kind+": damage follows active window");
      if(kind=="vent")Check(highestVy>20,"vent: launch retained");
    }
    foreach(var kind in new[]{"crusher","debris"}) {
        SETTINGS=new Settings{levelHazards=true};var w=new World();w.AddPlayer("keyboard","nova");w.Teleport(kind=="crusher"?"foundry":"undercity");
        var h=Level.HAZARDS.Find(z=>z.kind==kind);var p=w.players[0];p.x=(h.x0+h.x1)/2;p.y=h.y0+3;p.onGround=false;p.mercy=0;double hp=p.hp;
        w.tick=h.period-h.on-h.phase+(int)System.Math.Ceiling(h.on*.65);LevelFeatures.Step(w,new Dictionary<int,Cmd>(),false);
        Check(p.hp==hp,kind+": floor impact does not damage empty air above the head");
    }
    // Observe real bot commands and movement over a tier route, rather than checking the waypoint formula alone.
    foreach(var hero in new[]{"nova","echo"}) {
      var w=Make(true,false,true,"gym");w.AddPlayer("keyboard",hero);var ai=new Bots();ai.Sync(w,3);w.Teleport("gym");
      var reached=new HashSet<string>();
      foreach(var p in w.players){p.x=52;p.y=p.device=="keyboard"?4.4:0;p.onGround=true;}
      for(int tick=0;tick<600;tick++) {
        w.enemies.Clear();var commands=new Dictionary<int,Cmd>{[0]=new Cmd()};ai.Commands(w,commands);w.Step(commands);w.events.Clear();
        foreach(var p in w.players)if(Bots.IsBot(p)&&p.onGround&&p.y>=4.3)reached.Add(p.@char);
      }
      Check(w.players.Where(Bots.IsBot).All(p=>reached.Contains(p.@char)),hero+": all three teammates physically reach the upper route");
    }
    string Replay(string preset,string quality) {
      var w=Make(true,true,true,"gym");SETTINGS.fxPreset=preset;SETTINGS.quality=quality;var p=w.AddPlayer("pad0","nova");p.x=52;p.y=0;
      Run(w,p,180,i=>new Cmd {mx=i<70?.5:0,lane=i==20?1:0,held=new Buttons {jump=i>35&&i<55},pressed=new Buttons{jump=i==36}});
      return $"{w.tick}:{p.x:R}:{p.y:R}:{p.hp:R}:{p.lane}:{Level.HAZARDS.Sum(h=>h.hit.Count)}";
    }
    Check(Replay("cinematic","high")==Replay("classic","low")&&Replay("balanced","high")==Replay("custom","ultra"),"deterministic replay: presentation presets and quality do not change simulation");
    Console.WriteLine(fails == 0 ? "ALL PASS" : fails + " FAILED");
    Environment.ExitCode = fails == 0 ? 0 : 1;
  }
}
