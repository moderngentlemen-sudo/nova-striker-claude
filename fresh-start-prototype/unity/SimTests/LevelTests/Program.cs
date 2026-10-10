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
    // Deployed origins and their direct interactions must outlive an owner's lane change.
    {
      var w=Make(false,false,true,"gym");SETTINGS.aiTeammates=0;var p=w.AddPlayer("pad0","fix");
      p.x=12;p.y=0;p.onGround=true;p.scrap=200;p.gadgetSel="sentry";p.lane=p.laneTo=p.laneFrom=1;
      Check(w.DeployGadget(p),"sentry: deploy on a supported outer lane");var g=w.gadgets.Single();
      p.lane=p.laneTo=p.laneFrom=-1;
      Check(g.lane==1&&!w.GadgetNear(p),"sentry: its placement lane persists after the owner hops; remote wrench is unavailable");
      Check(w.GadgetAt(g.x,g.y+.4,.2,-1)==null&&w.GadgetAt(g.x,g.y+.4,.2,1)==g,"gadget: direct shot collision resolves the device's lane");
      w.WrenchGadget(g,p);Check(g.pts==0,"gadget: a wrench in another lane cannot upgrade it");
      var same=Enemies.CreateEnemy("brute",18,0);same.lane=same.laneTo=same.laneFrom=1;same.hp=100;
      var other=Enemies.CreateEnemy("brute",16,0);other.lane=other.laneTo=other.laneFrom=-1;other.hp=100;
      w.enemies.Add(same);w.enemies.Add(other);g.cd=0;g.level=3;g.rocketCd=0;w.events.Clear();var emitted=Run(w,p,1,clearEnemies:false);
      Check(g.target==same,"sentry: selects a reachable same-lane target over a nearer other-lane enemy");
      var shots=w.projectiles.Where(q=>q.kind=="sentryBolt"||q.kind=="sentryRocket").ToArray();
      Check(shots.Length==2&&shots.All(q=>q.laneSet&&q.lane==1),"sentry: bolt and rocket snapshot the device lane, not its moving owner");
      var muzzles=emitted.Where(e=>e.type=="sentryShot"||e.type=="sentryRocket").ToArray();
      Check(muzzles.Length==2&&muzzles.All(e=>e.depth==LevelFeatures.LANE_W),"sentry: muzzle events use the deployed origin");
      p.lane=p.laneTo=p.laneFrom=1;Check(w.GadgetNear(p),"gadget: returning to its lane restores wrench access");
    }
    {
      var w=Make(false,false,true,"gym");SETTINGS.aiTeammates=0;var p=w.AddPlayer("pad0","fix");p.x=12;p.y=4;p.scrap=200;p.gadgetSel="pylon";p.lane=p.laneTo=p.laneFrom=1;
      var deck=new LevelBox{x0=10,x1=13,y0=3.6,y1=4,type='o',laneMask=2};Level.BOXES.Add(deck);
      w.DeployGadget(p);var g=w.gadgets.Single();Check(g.gy==0&&!g.landed,"gadget: a deck in another lane is not invisible supporting geometry");
      deck.laneMask=4;g.y=g.py=4;g.landed=true;g.gy=4;Run(w,p,1);Check(g.landed&&g.y==4,"gadget: a matching lane deck supplies support");
      Level.BOXES.Remove(deck);Run(w,p,1);Check(!g.landed&&g.y<4,"gadget: losing a collapsing or removed deck starts a real fall");
    }
    {
      var w=Make(false,false,true,"gym");SETTINGS.aiTeammates=0;var p=w.AddPlayer("pad0","fix");p.x=12;p.y=0;p.scrap=200;p.gadgetSel="pylon";p.lane=p.laneTo=p.laneFrom=1;
      w.DeployGadget(p);var q=w.AddPlayer("pad1","nova");q.x=12;q.y=0;q.lane=q.laneTo=q.laneFrom=-1;q.hp=10;
      Run(w,p,1);Check(q.hp>10,"pylon: healing remains an explicitly all-lanes area effect");
    }
    {
      var w=Make(false,false,true,"gym");SETTINGS.aiTeammates=0;var p=w.AddPlayer("pad0","echo");p.x=12;p.y=0;
      var pr=new Projectile{owner=p,lane=-1,laneSet=true};p.lane=p.laneTo=p.laneFrom=1;
      w.SnareLanded(pr,12.8,.1);var trap=w.snares.Single();
      Check(trap.lane==-1&&w.events.Last(e=>e.type=="snarePlant").depth==-LevelFeatures.LANE_W,"snare: landing retains the emitted shot lane after the thrower hops");
      var target=Enemies.CreateEnemy("swarmer",trap.x,trap.y);target.lane=target.laneTo=target.laneFrom=1;w.enemies.Add(target);trap.armT=0;
      var events=Run(w,p,1,clearEnemies:false);
      Check(events.Any(e=>e.type=="snareTrigger"&&e.depth==-LevelFeatures.LANE_W)&&target.tagged>0,"snare: retains all-lanes area triggering with a fixed trap origin");
    }
    {
      var w=Make(false,false,true,"gym");SETTINGS.aiTeammates=0;var p=w.AddPlayer("pad0","nova");p.x=12;p.y=0;p.aimX=1;p.aimY=0;p.sub="well";p.lane=p.laneTo=p.laneFrom=-1;
      w.FireSub(p,2,false);var well=w.wells.Single();well.phase="open";well.collapse=true;p.lane=p.laneTo=p.laneFrom=1;
      var events=Run(w,p,1);Check(events.Any(e=>e.type=="wellCollapse"&&e.depth==-LevelFeatures.LANE_W),"well: collapse origin survives the caster changing lanes");
    }
    {
      var w=Make(false,false,false,"gym");SETTINGS.aiTeammates=0;var p=w.AddPlayer("pad0","fix");p.x=12;p.y=0;p.scrap=200;p.gadgetSel="sentry";
      w.DeployGadget(p);var g=w.gadgets.Single();g.lane=1;p.lane=-1;
      Check(w.GadgetNear(p)&&w.GadgetAt(g.x,g.y+.4,.2,-1)==g,"lanes off: deployed queries preserve legacy all-lanes access");
    }
    // Backdrop bounds sample across staging gaps and outside zones; every frame must remain finite.
    {
      bool finite=true,orthogonal=true;
      for(double x=-500;x<=2200;x+=.5) {var f=Level.Frame(x);finite&=double.IsFinite(f.px)&&double.IsFinite(f.pz)&&double.IsFinite(f.tx)&&double.IsFinite(f.tz)&&double.IsFinite(f.nx)&&double.IsFinite(f.nz);orthogonal&=Math.Abs(f.tx*f.tx+f.tz*f.tz-1)<1e-9&&Math.Abs(f.nx*f.nx+f.nz*f.nz-1)<1e-9&&Math.Abs(f.tx*f.nx+f.tz*f.nz)<1e-9;}
      Check(finite&&orthogonal,"route frames: backdrop sampling across every zone and staging gap is finite and orthonormal");
      var gap=Level.Frame(399);Check(Math.Abs(gap.pz+2*Level.ARC_R)<1e-9&&gap.tx==-1,"route frames: skyport staging gap continues its analytic route");
    }
    // Chain geometry snapshots each impacted lane, including across lanes, before damage/removal.
    {
      var w = Make(false,false,true,"gym"); var p = w.AddPlayer("pad0","nova");
      p.x=12;p.y=0;p.aimX=1;p.aimY=0;p.sub="chain";p.lane=p.laneTo=p.laneFrom=-1;
      var a=Enemies.CreateEnemy("brute",16,0);a.lane=a.laneTo=a.laneFrom=1;a.hp=100;
      var b=Enemies.CreateEnemy("brute",18,0);b.lane=0;b.laneFrom=0;b.hp=100;
      w.enemies.Add(a);w.enemies.Add(b);w.FireSub(p,3,false);
      var ev=w.events.First(e=>e.type=="chain");
      Check(ev.pts.Count>=3 && ev.pts[0].depth==-LevelFeatures.LANE_W && ev.pts[1].depth==LevelFeatures.LANE_W && ev.pts[2].depth==0,"chain: origin and each cross-lane target have separate depth snapshots");
      a.lane=a.laneTo=-1;a.x+=10;w.enemies.Clear();
      Check(ev.pts[1].depth==LevelFeatures.LANE_W && ev.pts[1].x==16,"chain: target removal or later movement cannot drag an existing arc");
      w.events.Clear();w.FireSub(p,0,false);var miss=w.events.First(e=>e.type=="chain");
      Check(miss.pts.Count==2 && miss.pts[1].fizzle && miss.pts.All(q=>q.depth==-LevelFeatures.LANE_W),"chain: missed discharge keeps the emission lane at both endpoints");
    }
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
    foreach(int fps in new[]{30,60,120}) {
      var clock=new NovaStriker.Game.EmissionClock();int emissions=0;
      for(int frame=0;frame<fps*10;frame++)emissions+=clock.TakeSteps(1.0/fps);
      Check(emissions==600,fps+" fps: ten seconds produce 600 decorative steps");
    }
    {var clock=new NovaStriker.Game.EmissionClock();Check(clock.TakeSteps(1)==8&&clock.TakeSteps(0)==0,"effect clock: catchup bounded and paused draw consumes no time");}
    // A directly created boss must be usable by preview tools and every named attack, too.
    foreach(var type in new[]{"warden","stormcaller"}) {
      var initial=Enemies.CreateEnemy(type,87,0);
      Check(initial.phase==1,type+": direct factory starts at phase one");
      foreach(int phase in new[]{1,2}) foreach(var attack in type=="warden"?new[]{"sweep","hammer","stomp","missiles","charge","laser"}:new[]{"volley","rain","sweep","dive"}) {
        var w=Make(false,false,false,type=="warden"?"arena":"skyline");var p=w.AddPlayer("keyboard","ram");p.x=type=="warden"?87:305;p.y=type=="warden"?0:Bosses.STORM.floor;p.mercy=999;
        var e=Enemies.CreateEnemy(type,p.x-4,p.y+(type=="warden"?0:Bosses.STORM.hover));e.phase=phase;w.enemies.Add(e);
        Bosses.ForceBossAttack(w,e,attack);bool selected=e.atk!=null&&e.atk.kind==attack;
        for(int tick=0;tick<360;tick++){p.hp=p.maxHp;p.state="normal";p.mercy=999;Enemies.UpdateEnemy(e,w);w.tick++;}
        Check(selected&&!e.dead,type+" phase "+phase+": "+attack+" runs without invalid phase indices");
      }
    }
    foreach(var preset in new[]{"cinematic","balanced","classic"}) {
      SETTINGS=new Settings{quality="high",fxPreset=preset};
      string Effective() => string.Join("|",NovaStriker.Game.FxCfg.Explosions,NovaStriker.Game.FxCfg.Projectiles,NovaStriker.Game.FxCfg.Trails,NovaStriker.Game.FxCfg.Smoke,NovaStriker.Game.FxCfg.Screen,NovaStriker.Game.FxCfg.Density,NovaStriker.Game.FxCfg.Debris,NovaStriker.Game.FxCfg.Lights,NovaStriker.Game.FxCfg.Distortion,NovaStriker.Game.FxCfg.Decals);
      string before=Effective();NovaStriker.Game.FxCfg.CaptureCustom();
      Check(SETTINGS.fxPreset=="custom"&&before==Effective(),preset+": switching to Custom preserves every effective option");
    }
    SETTINGS=new Settings{quality="high",fxPreset="custom",fxExplosions="classic",fxProjectiles="energy"};
    Check(NovaStriker.Game.FxCfg.Classic&&NovaStriker.Game.FxCfg.Projectiles=="energy","Custom: Classic explosions preserve independent energy projectiles");
    SETTINGS.fxExplosions="volumetric";SETTINGS.fxProjectiles="classic";
    Check(!NovaStriker.Game.FxCfg.Classic&&NovaStriker.Game.FxCfg.Projectiles=="classic","Custom: rich explosions preserve independent Classic projectiles");
    SETTINGS.quality="low";SETTINGS.fxLights=true;SETTINGS.fxDistortion=true;SETTINGS.fxDecals=true;
    Check(NovaStriker.Game.FxCfg.MaxParticles==1200&&NovaStriker.Game.FxCfg.MaxLights==0&&NovaStriker.Game.FxCfg.MaxDecals==0&&!NovaStriker.Game.FxCfg.Distortion,"Low graphics: advertised particle/light/decal/distortion caps apply");
    SETTINGS.quality="high";SETTINGS.fxPreset="cinematic";SETTINGS.reducedScreenEffects=true;
    Check(NovaStriker.Game.FxCfg.Screen=="clean"&&!NovaStriker.Game.FxCfg.Distortion&&NovaStriker.Game.FxCfg.Explosions=="volumetric","reduced screen effects preserves combat style and disables screen distortion");
    Console.WriteLine(fails == 0 ? "ALL PASS" : fails + " FAILED");
    Environment.ExitCode = fails == 0 ? 0 : 1;
  }
}
