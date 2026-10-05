// AI teammates (port of bot.js). Each one produces the same command a gamepad would (stick, held/pressed/released
// buttons, free aim) once a tick, so the simulation treats it exactly like a person.
using System;
using System.Collections.Generic;
using static NovaStriker.Sim.Cfg;
using static NovaStriker.Sim.U;

namespace NovaStriker.Sim
{
    public sealed class BotSkill { public double react, delay, retarget; public bool smart; }

    public sealed class Order { public string type; public Player by; public Enemy target; public double x, y, t; }

    public sealed class Bots
    {
        public static class BOT
        {
            public const double sight = 11, leash = 9, follow = 1.6, reviveRange = 22;
            public static double Range(string ch) => ch == "nova" ? 6.5 : ch == "echo" ? 1.3 : ch == "ram" ? 1.5 : 5.5;
            public static readonly Dictionary<string, BotSkill> skill = new Dictionary<string, BotSkill>
            {
                ["rookie"] = new BotSkill { react = 0.3, delay = 14, retarget = 20, smart = false },
                ["veteran"] = new BotSkill { react = 0.6, delay = 8, retarget = 12, smart = true },
                ["elite"] = new BotSkill { react = 0.9, delay = 4, retarget = 6, smart = true },
            };
        }
        public static bool IsBot(Player p) => p.device != null && p.device.StartsWith("cpu", StringComparison.Ordinal);
        static bool Alive(Enemy e) => !e.dead && e.hp > 0;
        static bool Up(Player q) => q.state != "downed" && q.state != "dead";
        static readonly HashSet<string> RANGED_FOES = new HashSet<string> { "sniper", "mortar", "turret", "drone" };
        public static class ORDERS
        {
            public static readonly Dictionary<string, string> names = new Dictionary<string, string>
            { ["attack"] = "Attack my target", ["cover"] = "Cover me", ["regroup"] = "Regroup on me", ["hold"] = "Hold here" };
            public const double regroup = 360, holdRange = 7, coverRange = 6;
            public static readonly Dictionary<string, Dictionary<string, string>> lines = new Dictionary<string, Dictionary<string, string>>
            {
                ["nova"] = new Dictionary<string, string> { ["attack"] = "Marking it. Going in.", ["cover"] = "I have your back.", ["regroup"] = "Coming to you.", ["hold"] = "Holding this line.", ["done"] = "Target down." },
                ["echo"] = new Dictionary<string, string> { ["attack"] = "It is mine.", ["cover"] = "Nothing gets near you.", ["regroup"] = "On my way.", ["hold"] = "I will hold.", ["done"] = "Done." },
                ["ram"] = new Dictionary<string, string> { ["attack"] = "Plowing through!", ["cover"] = "Behind me. Stay there.", ["regroup"] = "Falling back to you.", ["hold"] = "They will not pass.", ["done"] = "Down it goes." },
                ["fix"] = new Dictionary<string, string> { ["attack"] = "Rivets out!", ["cover"] = "Beam on you, stay close.", ["regroup"] = "Right behind you!", ["hold"] = "Setting up here.", ["done"] = "Scrap it!" },
            };
        }

        sealed class React { public double at; public bool yes; }
        sealed class Mem
        {
            public Buttons prev = new Buttons();
            public double jumpT, fireT, hold, guardT, t, retarget, gadgetT = 240, snareT, modeT;
            public bool jumpNew, beam;
            public Dictionary<object, React> react = new Dictionary<object, React>();
            public long seed;
            public Enemy target;
            public double stuck;
            public double? lastX, lastY;
        }

        readonly Dictionary<Player, Mem> mem = new Dictionary<Player, Mem>();
        public Order order;
        public double? done;

        // A team command from player `by`: returns each bot's answer (empty if there are no bots)
        public List<(Player bot, string line)> Issue(World world, Player by, string type)
        {
            var bots = world.players.FindAll(IsBot);
            var answers = new List<(Player, string)>();
            if (bots.Count == 0 || !ORDERS.names.ContainsKey(type)) return answers;
            if (order != null && order.type == type && order.by == by && type != "attack") { order = null; foreach (var b in bots.Live()) answers.Add((b, "Back to following.")); return answers; }
            Enemy target = null;
            if (type == "attack")
            {
                target = by.lockT != null && Alive(by.lockT) ? by.lockT : null;
                if (target == null) { double bd = BOT.sight + 6; foreach (var e in world.enemies.Live()) { if (!Alive(e)) continue; double d = JMath.Hypot(e.x - by.x, e.y - by.y); if (d < bd) { bd = d; target = e; } } }
                if (target == null) { foreach (var b in bots.Live()) answers.Add((b, "No target in sight.")); return answers; }
            }
            order = new Order { type = type, by = by, target = target, x = by.x, y = by.y, t = world.tick };
            foreach (var b in bots.Live()) if (mem.TryGetValue(b, out var M)) { M.retarget = 0; M.hold = 0; }
            foreach (var b in bots.Live()) answers.Add((b, ORDERS.lines[b.@char][type]));
            return answers;
        }
        // The command in force (it lapses when its commander leaves, its target falls, or a regroup has run its time)
        public Order ActiveOrder(World world)
        {
            var O = order; if (O == null) return null;
            if (!world.players.Contains(O.by)) { order = null; return null; }
            if (O.type == "attack" && !(O.target != null && Alive(O.target) && world.enemies.Contains(O.target))) { order = null; done = world.tick; return null; }
            if (O.type == "regroup" && world.tick - O.t > ORDERS.regroup) { order = null; return null; }
            return O;
        }

        // Keep the number of AI players at `want`, never more than the slots the human players leave free
        public void Sync(World world, int want)
        {
            var humans = world.players.FindAll(p => !IsBot(p)); var bots = world.players.FindAll(IsBot);
            int allowed = humans.Count > 0 ? Math.Max(0, Math.Min(want, 4 - humans.Count)) : 0;
            for (int i = bots.Count - 1; i >= allowed; i--) { world.RemovePlayer(bots[i].slot); mem.Remove(bots[i]); }
            for (int n = Math.Min(bots.Count, allowed); n < allowed; n++)
            {
                var used = new HashSet<string>(); foreach (var p in world.players.Live()) used.Add(p.@char);
                string ch = Array.Find(ROSTER, c => !used.Contains(c)) ?? ROSTER[n % ROSTER.Length];
                int id = 1; var devs = new HashSet<string>(); foreach (var p in world.players.Live()) devs.Add(p.device); while (devs.Contains("cpu" + id)) id++;
                world.AddPlayer("cpu" + id, ch);
            }
        }
        // Free a slot for a person joining a full team: the last AI player leaves
        public bool MakeRoom(World world)
        {
            var bots = world.players.FindAll(IsBot);
            if (world.players.Count < 4 || bots.Count == 0) return false;
            var b = bots[bots.Count - 1]; world.RemovePlayer(b.slot); mem.Remove(b); return true;
        }

        // The commands for every AI player this tick, added to `cmds` (by slot)
        public Dictionary<int, Cmd> Commands(World world, Dictionary<int, Cmd> cmds)
        {
            foreach (var p in world.players.Live()) if (IsBot(p)) cmds[p.slot] = Command(world, p);
            var gone = new List<Player>(); foreach (var p in mem.Keys) if (!world.players.Contains(p)) gone.Add(p);
            foreach (var p in gone.Live()) mem.Remove(p);
            return cmds;
        }

        Cmd Command(World world, Player p)
        {
            if (!mem.TryGetValue(p, out var M)) { M = new Mem { seed = (p.slot + 1) * 7919 }; mem[p] = M; }
            M.t++;
            var S = SETTINGS.aiSkill != null && BOT.skill.TryGetValue(SETTINGS.aiSkill, out var sk) ? sk : BOT.skill["veteran"];
            double Rnd() { M.seed = (M.seed * 16807) % 2147483647; return M.seed / 2147483647.0; }
            var held = new Buttons(); double mx = 0, my = 0, ax = 0, ay = 0; bool aimFree = false;
            Cmd Out()
            {
                var c = new Cmd { mx = mx, my = my, aimFree = aimFree, ax = ax, ay = ay, held = held };
                for (int i = 0; i < 10; i++) { c.pressed[i] = held[i] && !M.prev[i]; c.released[i] = !held[i] && M.prev[i]; }
                M.prev = held.Clone();
                return c;
            }
            if (p.state == "downed" || p.state == "dead" || p.state == "ult") { M.hold = 0; return Out(); }

            var team = world.players.FindAll(q => q != p);
            var O = ActiveOrder(world);
            var leader = (O != null && O.by != p && Up(O.by) ? O.by : null) ?? team.Find(q => !IsBot(q) && Up(q)) ?? team.Find(Up);
            var bots = world.players.FindAll(IsBot); int order_ = bots.IndexOf(p);
            double cx = p.x, cy = p.y + p.h / 2;
            double Dist(Enemy e) => JMath.Hypot(e.x - cx, e.y + e.h / 2 - cy);
            var foes = world.enemies.FindAll(e => Alive(e) && e.state != "plowed" && Dist(e) < BOT.sight + 4);
            // Commands narrow what it fights
            if (O != null && O.type == "regroup") foes = foes.FindAll(e => Dist(e) < 3.5);
            if (O != null && O.type == "hold") foes = foes.FindAll(e => JMath.Abs(e.x - O.x) < ORDERS.holdRange || ((p.@char == "nova" || p.@char == "fix") && Dist(e) < BOT.sight && JMath.Abs(e.x - O.x) < BOT.sight));

            // ---- Join a teammate's team ultimate ----
            if (world.ultCast != null && !world.ultCast.members.Contains(p) && p.ult >= ULT.max) { held.ult = (M.t % 4) < 2; return Out(); }

            // ---- 2. Who to fight ----
            if (O != null && O.type == "attack") { M.target = O.target; M.retarget = S.retarget; }
            else if (--M.retarget <= 0 || M.target == null || !Alive(M.target) || !foes.Contains(M.target))
            {
                M.retarget = S.retarget;
                Enemy best = null; double bs = double.PositiveInfinity;
                foreach (var e in foes.Live())
                {
                    double s = Score(world, p, e, Dist(e), leader, team, M, S);
                    if (O != null && O.type == "cover") s += (e.target == O.by ? -4 : 0) + JMath.Abs(e.x - O.by.x) * 0.5;
                    if (s < bs) { bs = s; best = e; }
                }
                M.target = best;
            }
            bool attackOrder = O != null && O.type == "attack";
            var tgt = M.target != null && Alive(M.target) && (attackOrder || Dist(M.target) < BOT.sight + 2) ? M.target : null;
            bool farFromLeader = !attackOrder && leader != null && JMath.Abs(leader.x - p.x) > BOT.leash;
            if (farFromLeader && tgt != null && JMath.Abs(tgt.x - leader.x) > BOT.leash) tgt = null;   // catch up first

            // ---- 3. Where to stand ----
            Player downed = null;
            foreach (var q in team.Live()) if (q.state == "downed" && JMath.Abs(q.x - p.x) < BOT.reviveRange && (downed == null || JMath.Abs(q.x - p.x) < JMath.Abs(downed.x - p.x))) downed = q;
            double goal = p.x, stopAt = 0.5, goalY = p.y;
            if (downed != null && !(p.@char == "fix" && JMath.Abs(downed.x - p.x) < FIX.beam.range - 1)) { goal = downed.x; goalY = downed.y; stopAt = 0.6; }
            else if (tgt != null && !farFromLeader)
            {
                (goal, stopAt) = Post(world, p, tgt, foes, leader, team, S, Dist);
                goalY = tgt.y;
            }
            else if (leader != null)
            {
                goal = leader.x - or(leader.facing, 1) * BOT.follow * (1 + order_ * 0.7); goalY = leader.y; stopAt = 0.8;
                // Climbing after them: aim for the platform they are on, not the gap behind it
                if (leader.y > p.y + 1.5 && leader.onGround) { goal = leader.x - or(leader.facing, 1) * 0.4 * order_; stopAt = 0.3; }
            }
            // Commands move where it stands
            if (O != null && downed == null)
            {
                Enemy near = null; foreach (var e in foes.Live()) if (near == null || Dist(e) < Dist(near)) near = e;
                if (O.type == "regroup" && leader != null) { goal = leader.x - or(leader.facing, 1) * (0.9 + order_ * 0.8); goalY = leader.y; stopAt = 0.5; }
                else if (O.type == "hold")
                {
                    double spot = O.x + (order_ - (bots.Count - 1) / 2.0) * 1.3;
                    bool melee = p.@char == "ram" || p.@char == "echo";
                    if (!(tgt != null && melee && JMath.Abs(tgt.x - O.x) < ORDERS.holdRange)) { goal = spot; goalY = O.y; stopAt = 0.5; }
                }
                else if (O.type == "cover" && leader != null)
                {
                    double threatX = near != null ? near.x : leader.x + or(leader.facing, 1) * 3, side = or(or(sign(threatX - leader.x), leader.facing), 1);
                    if (p.@char == "ram") { goal = leader.x + side * 1.4; stopAt = 0.4; }
                    else if (p.@char == "echo" && tgt != null && JMath.Abs(tgt.x - leader.x) < 4) { /* free to cut it down */ }
                    else { goal = leader.x - side * (1.2 + order_ * 0.7); stopAt = 0.6; }
                    goalY = leader.y;
                }
            }
            // Patched up on the way: a Medkit or Plating within reach when it is hurt
            if (tgt == null && downed == null && !(O != null && O.type == "hold") && p.hp < p.maxHp * 0.7)
            {
                var pk = world.pickups.Find(k => (k.kind == "medkit" || k.kind == "plating") && k.target == null && JMath.Abs(k.x - p.x) < 7 && JMath.Abs(k.y - p.y) < 2.5);
                if (pk != null) { goal = pk.x; goalY = pk.y; stopAt = 0.2; }
            }
            // spread out: don't stand where another bot already is
            foreach (var q in bots.Live()) if (q != p && Up(q) && bots.IndexOf(q) < order_ && JMath.Abs(q.x - goal) < 0.8 && JMath.Abs(q.y - p.y) < 1.5) goal += (goal >= q.x ? 1 : -1) * 0.9;
            // ---- 1. Hazards: leave a mortar's landing zone ----
            double? shell = S.smart ? MortarZone(world, p) : null;
            if (shell != null) { goal = p.x + (p.x >= shell.Value ? 1 : -1) * 3; stopAt = 0.2; }
            double dx = goal - p.x;
            if (JMath.Abs(dx) > stopAt) mx = sign(dx);

            // ---- Platforming ----
            double dir = or(mx, p.facing);
            bool wall = truthy(mx) && !Level.HasHeadroom(p.x + dir * 0.45, p.y + 0.3, p.w, JMath.Max(0.6, p.h - 0.4));
            // A wall far taller than where the goal is, with the goal just past it: wait on this side
            if (wall && JMath.Abs(dx) < 6)
            {
                double top = p.y; while (top < p.y + 30 && !Level.HasHeadroom(p.x + dir * 0.6, top, p.w * 0.5, 1)) top += 1;
                if (top > goalY + 3.5) { mx = 0; wall = false; }
            }
            double floorAhead = Level.GroundBelow(p.x + dir * (p.w / 2 + 0.6), p.y + 0.2);
            bool gap = truthy(mx) && p.onGround && floorAhead < p.y - 1.2;
            bool goalPastGap = JMath.Abs(dx) > 2.2;
            if (gap && !goalPastGap && goalY >= p.y - 0.5) mx = 0;           // don't step off for nothing
            bool wantUp = goalY > p.y + 1.1 && JMath.Abs(dx) < 5;
            // an enemy shockwave running along the floor at us: hop it
            bool wave = p.onGround && world.shockwaves.Exists(w => w.team != "p" && JMath.Abs(w.y - p.y) < 0.6 && sign(p.x - w.x) == sign(w.dir) && JMath.Abs(p.x - w.x) < 2.4)
                && Answer(world, M, "wave" + JMath.Floor(world.tick / 30), S, Rnd, true);
            // a wall in the way while airborne: climb kicks take it up a tall wall
            bool climb = !p.onGround && p.wallDir != 0 && p.wallDir == dir && (wall || goalY > p.y + 0.5);
            // (a jump only starts on a fresh press, so each one begins by letting go of the button if it is held)
            if (M.jumpT <= 0 && p.onGround && (wave || wall || (gap && goalPastGap && goalY >= p.y - 0.5) || wantUp)) { M.jumpT = 14; M.jumpNew = true; }
            else if (M.jumpT <= 0 && !p.onGround && (climb ? p.vy < 3 : p.vy < 1 && p.jumpsUsed < 1 && (wall || (goalY > p.y + 0.8 && JMath.Abs(dx) < 4) || (floorAhead < p.y - 3 && goalPastGap)))) { M.jumpT = 10; M.jumpNew = true; }
            if (M.jumpT > 0)
            {
                if (M.jumpNew && M.prev.jump) held.jump = false;
                else { held.jump = true; M.jumpNew = false; M.jumpT--; }
            }
            // Stuck (no headway toward a goal over 3 m away for about 3 s, out of a fight): catch up with the team
            if (M.t % 60 == 0)
            {
                bool below = goalY > p.y + 3, noHeadway = JMath.Abs(p.x - (M.lastX ?? p.x + 9)) < 0.6 && p.y < (M.lastY ?? -1e9) + 1;
                M.stuck = tgt == null && (JMath.Abs(dx) > 3 || below) && noHeadway ? M.stuck + 1 : 0; M.lastX = p.x; M.lastY = p.y;
                if (M.stuck >= 3) { M.stuck = 0; world.Recall(p, false); return Out(); }
            }

            if (tgt == null) { M.fireT = 0; M.hold = 0; Support(world, p, M, held, team, S, Rnd); return Out(); }

            // ---- 4. Fighting ----
            double d = Dist(tgt), tx = tgt.x - p.x, ty = (tgt.y + tgt.h / 2) - cy, len = or(JMath.Hypot(tx, ty), 1);
            aimFree = true; ax = tx / len; ay = ty / len;
            if (!truthy(mx) && JMath.Abs(tx) > 0.2 && sign(tx) != p.facing) mx = sign(tx) * 0.3;   // turn to face it
            if (!p.onGround) ay = JMath.Max(ay, -0.3);                                       // (aiming hard down in the air is a ground pound)
            bool seen = !Level.SegmentBlocked(cx, cy, tgt.x, tgt.y + tgt.h / 2);
            bool threat = Threat(world, p, M, S, Rnd);
            bool close = JMath.Abs(tx) < 2.0 + tgt.w / 2 && JMath.Abs(ty) < 1.6;
            int crowd = foes.FindAll(e => Dist(e) < 6).Count;
            int inLine = InLine(foes, cx, cy, ax, ay);
            double nearest = double.PositiveInfinity; foreach (var e in foes.Live()) nearest = JMath.Min(nearest, Dist(e));
            bool armoured = tgt.armor > 0 || tgt.boss;

            switch (p.@char)
            {
                case "nova":
                    {
                        // attachment for the job (Marksman kit)
                        if (S.smart && p.attachment != null && Array.IndexOf(MARKSMAN.attachments, p.attachment) >= 0 && p.modeCd == 0 && --M.modeT <= 0)
                        {
                            string want = armoured ? "lance" : crowd >= 3 ? "arc" : tgt.flier ? "volley" : p.attachment;
                            if (want != p.attachment) { held.mode = true; M.modeT = 20; }
                        }
                        if (threat) { held.parry = true; mx = or(-sign(tx), -p.facing); M.hold = 0; }          // dodge away
                        else if (close && !(M.beam && M.hold > 0 && nearest > 1.6)) { aimFree = false; held.melee = (M.t % 9) < 3; M.hold = 0; }
                        else if (seen)
                        {
                            // the Level 4 beam down a line of enemies (or a boss), when nothing is on top of her
                            bool beam = BeamPlan(M, S, inLine, tgt, nearest, 3) && p.attachment != null;
                            held.fire = Charge(M, p, beam ? MARKSMAN.beam.at + 2 : armoured ? MARKSMAN.charge[2] + 2 : crowd >= 2 ? MARKSMAN.charge[1] + 2 : MARKSMAN.charge[0] + 2);
                            if (beam) mx = 0;   // braced while it charges
                        }
                        else M.hold = 0;
                        break;
                    }
                case "echo":
                    {
                        if (threat) { held.parry = (M.t % 3) == 0; M.hold = 0; }                         // parry / deflect
                        else if (close) { aimFree = false; held.melee = (M.t % 8) < 3; M.hold = 0; }
                        else if (d > 3 && d < 6.5 && p.onGround && JMath.Abs(ty) < 1 && (M.t % 50) == 0) { held.dash = true; mx = sign(tx); }
                        else if (d >= 6.5 && seen) held.fire = Charge(M, p, HUNTER.rifle.raise + (armoured ? HUNTER.rifle.focus : 20));
                        // snares on a charger or heavy coming for the team
                        if (S.smart && SETTINGS.echoKit == "hunter" && p.snares > 0 && --M.snareT <= 0)
                        {
                            var rusher = foes.Find(e => (e.type == "charger" || e.type == "brute" || e.type == "shield") && Dist(e) < 7 && Dist(e) > 1.5 && sign(e.x - p.x) == p.facing);
                            if (rusher != null)
                            {
                                M.snareT = 150; M.hold = 0;
                                if (SETTINGS.echoBelt == "lb") held.sub = true; else held.fire = !M.prev.fire;   // a tap
                            }
                        }
                        break;
                    }
                case "ram":
                    {
                        bool guarding = p.state == "guard";
                        bool cover = (S.smart || (O != null && O.type == "cover")) && CoverNeeded(world, p, team);
                        if (threat || cover || (guarding && M.guardT > 0))
                        {
                            held.parry = true; aimFree = true; ax = or(sign(tx), p.facing); ay = 0; M.hold = 0;
                            if (threat || cover) M.guardT = 24; M.guardT--;
                            if (p.kinetic >= 45 && d < 5) held.fire = (M.t % 6) < 2;                         // let the Kinetic go
                        }
                        else if (close && !(M.beam && M.hold > 0 && nearest > 1.6)) { aimFree = false; held.melee = (M.t % 12) < 3; M.hold = 0; }
                        else if (M.beam && M.hold > 0) { bool b = BeamPlan(M, S, inLine, tgt, nearest, 1.6); held.fire = Charge(M, p, b ? RAM.beam.at + 2 : 1); mx = 0; }
                        else if (d > 3 && d < 8 && p.onGround && JMath.Abs(ty) < 0.8 && (M.t % 70) == 0 && p.rush == null && crowd >= 1 && !armoured) { held.dash = true; mx = sign(tx); M.hold = 0; }
                        else if (seen && d < 12)
                        {
                            bool beam = BeamPlan(M, S, inLine, tgt, nearest, 2.5);
                            held.fire = Charge(M, p, beam ? RAM.beam.at + 2 : armoured ? RAM.cannon.charge[1] + 2 : RAM.cannon.charge[0] + 2);
                            if (beam) mx = 0;   // braced while it charges
                        }
                        else M.hold = 0;
                        var hurt = team.Find(q => Up(q) && q.hp < q.maxHp * 0.45 && JMath.Abs(q.x - p.x) < RAM.link.range);
                        int barrage = world.projectiles.FindAll(pr => pr.team == "e" && !pr.dead && JMath.Abs(pr.x - p.x) < 10).Count + foes.FindAll(e => RANGED_FOES.Contains(e.type)).Count;
                        if (crowd >= 3 && p.provokeCd == 0 && (M.t % 30) == 0) held.sub = true;
                        else if (hurt != null && p.linkCd == 0 && p.link == null && (M.t % 30) == 15) held.mode = true;
                        else if (S.smart && barrage >= 4 && p.wallCd == 0 && p.onGround && (M.t % 30) == 7) held.sig = true;
                        break;
                    }
                case "fix":
                    {
                        if (Patch(world, p, team)) { held.parry = true; aimFree = false; M.hold = 0; }
                        else if (close) { aimFree = false; held.melee = (M.t % 9) < 3; M.hold = 0; }
                        else if (seen) held.fire = Charge(M, p, armoured ? FIX.rivet.charge[1] + 2 : 1);
                        else M.hold = 0;
                        Gadget(world, p, M, team, crowd, held);
                        break;
                    }
            }
            // Ultimate: when it is full and there is something worth hitting close by
            if (p.ult >= ULT.max && world.ultCast == null && d < 7 && (crowd >= 2 || tgt.boss || armoured) && (M.t % 20) == 0) held.ult = true;
            return Out();
        }

        // How much a bot wants to fight `e` (lower is better)
        double Score(World world, Player p, Enemy e, double d, Player leader, List<Player> team, Mem M, BotSkill S)
        {
            double s = d;
            bool ranged = p.@char == "nova" || p.@char == "fix"; double vy = JMath.Abs((e.y + e.h / 2) - (p.y + p.h / 2));
            if (e == M.target) s -= 1;
            if (leader != null && leader.lockT == e) s -= 3.5;
            if (S.smart && e.target != null && e.target != p && team.Contains(e.target) && (e.state == "windup" || e.state == "aim" || e.state == "lock")) s -= 3;
            if (e.hp <= 3) s -= 1.5;
            if (ranged && (e.flier || RANGED_FOES.Contains(e.type))) s -= 2;
            if (!ranged && (e.flier || vy > 2.6)) s += 4;
            if (e.armor > 0 && (p.@char == "echo" || p.@char == "fix")) s += 2;
            if (ranged && Level.SegmentBlocked(p.x, p.y + p.h / 2, e.x, e.y + e.h / 2)) s += 3;
            return s;
        }

        // Where to stand against `tgt`, by role: (x, how close counts as there)
        (double, double) Post(World world, Player p, Enemy tgt, List<Enemy> foes, Player leader, List<Player> team, BotSkill S, Func<Enemy, double> dist)
        {
            double R = BOT.Range(p.@char), side = or(or(sign(p.x - tgt.x), -tgt.facing), 1); Actor rf = (Actor)leader ?? p;
            switch (p.@char)
            {
                case "ram":
                    {
                        // between the team and the nearest enemy to it, shield toward it
                        Enemy front = null; foreach (var e in foes.Live()) if (front == null || JMath.Abs(e.x - rf.x) < JMath.Abs(front.x - rf.x)) front = e;
                        front = front ?? tgt;
                        double s = or(sign(rf.x - front.x), side);
                        return (front.x + s * (R + front.w / 2), 0.35);
                    }
                case "echo":
                    {
                        // a flank: the far side of the target from the team, if there is floor there
                        if (S.smart && leader != null)
                        {
                            double s = or(sign(tgt.x - leader.x), side), fx = tgt.x + s * (R + tgt.w / 2);
                            if (JMath.Abs(Level.GroundBelow(fx, tgt.y + 0.5) - tgt.y) < 0.6) return (fx, 0.35);
                        }
                        return (tgt.x + side * (R + tgt.w / 2), 0.35);
                    }
                case "nova":
                    {
                        // at range on the team's side; anything that gets within 3 m is backed away from (kiting)
                        var close = foes.Find(e => dist(e) < 3);
                        if (S.smart && close != null) return (p.x + or(or(sign(p.x - close.x), -close.facing), 1) * 3, 0.3);
                        double s = leader != null ? or(sign(leader.x - tgt.x), side) : side;
                        return (tgt.x + s * (R + tgt.w / 2), 1.2);
                    }
                default:
                    {   // fix: behind the team, near whoever is hurt most, well away from the fighting
                        var all = new List<Player> { p }; all.AddRange(team);
                        var ups = all.FindAll(Up);
                        StableSort.Sort(ups, (a, b) => a.hp / a.maxHp - b.hp / b.maxHp);
                        var hurt = ups.Count > 0 ? ups[0] : null;
                        Actor anchor = hurt != null && hurt != p && hurt.hp < hurt.maxHp * 0.85 ? hurt : rf;
                        double s = or(sign(anchor.x - tgt.x), side);
                        double x = anchor.x + s * 2.5;
                        if (JMath.Abs(x - tgt.x) < 4) x = tgt.x + s * 4.5;
                        return (x, 1.0);
                    }
            }
        }

        // Out of a fight: Fix keeps the team patched up (and builds a Pylon if they are hurt); RAM guards shots
        void Support(World world, Player p, Mem M, Buttons held, List<Player> team, BotSkill S, Func<double> rnd)
        {
            if (p.@char == "fix") { if (Patch(world, p, team)) held.parry = true; Gadget(world, p, M, team, 0, held); }
            if (p.@char == "ram" && Threat(world, p, M, S, rnd)) held.parry = true;
        }

        // Fix: someone in Patch Beam range is down or hurt (or she is, with no one else to see to)
        bool Patch(World world, Player p, List<Player> team)
        {
            double R = FIX.beam.range; var O = order;
            if (O != null && O.type == "cover" && Up(O.by) && O.by != p && O.by.hp < O.by.maxHp * 0.97 && JMath.Hypot(O.by.x - p.x, O.by.y - p.y) < R) return true;
            bool need = team.Exists(q => (q.state == "downed" || (Up(q) && q.hp < q.maxHp * 0.75)) && JMath.Hypot(q.x - p.x, q.y - p.y) < R);
            return need || p.patch != null || (p.hp < p.maxHp * 0.5);
        }
        // Fix: pick the gadget the fight needs, then build it when there is Scrap to spare (one at a time)
        void Gadget(World world, Player p, Mem M, List<Player> team, int crowd, Buttons held)
        {
            int hurt = team.FindAll(q => Up(q) && q.hp < q.maxHp * 0.6).Count;
            string want = hurt >= 1 ? "pylon" : crowd >= 1 ? "sentry" : null;
            if (want == null) return;
            if (p.gadgetSel != want) { if (p.modeCd == 0 && (M.t % 12) == 0) held.mode = true; return; }
            double cost = FIX.gadget[want].cost;
            if (p.scrap >= JMath.Max(55, cost + 10) && --M.gadgetT <= 0 && !world.gadgets.Exists(g => g.owner == p && !g.dead && g.kind != "pad")) { held.sig = true; M.gadgetT = 480; }
        }

        // Holding fire to a charge level: keep holding until the charge reaches `to` ticks, then let go
        bool Charge(Mem M, Player p, double to)
        {
            if (M.hold < 0) { M.hold++; return false; }
            if (M.hold == 0 && p.chargeT == 0 && p.rifleT == 0) { M.hold = 1; return true; }
            M.hold++;
            double now = JMath.Max(p.chargeT, p.rifleT);
            if (now >= to || M.hold > to + 40) { M.hold = -6; return false; }
            return true;
        }
        // A Level 4 beam: begun when enemies line up (or for a boss) with none too close, then kept to until it fires
        bool BeamPlan(Mem M, BotSkill S, int inLine, Enemy tgt, double nearest, double room)
        {
            if (M.beam && M.hold > 0) { if (nearest < 1.6) M.beam = false; return M.beam; }
            M.beam = S.smart && (inLine >= 3 || tgt.boss) && nearest > room;
            return M.beam;
        }
        // How many enemies sit along the aim line (within a beam's width of it, ahead)
        int InLine(List<Enemy> foes, double cx, double cy, double ax, double ay)
        {
            int n = 0;
            foreach (var e in foes.Live())
            {
                double rx = e.x - cx, ry = e.y + e.h / 2 - cy, along = rx * ax + ry * ay;
                if (along > 0 && along < 30 && JMath.Abs(rx * ay - ry * ax) < 0.9 + e.h / 3) n++;
            }
            return n;
        }
        // RAM: enemy shots heading at a teammate standing behind him
        bool CoverNeeded(World world, Player p, List<Player> team)
        {
            foreach (var pr in world.projectiles.Live())
            {
                if (pr.team != "e" || pr.dead || pr.kind == "mortar") continue;
                foreach (var q in team.Live())
                {
                    if (!Up(q) || JMath.Abs(q.x - p.x) > 4) continue;
                    bool behind = sign(q.x - p.x) == -sign(pr.vx) || JMath.Abs(q.x - p.x) < 0.6;
                    double rx = q.x - pr.x, ry = q.y + q.h / 2 - pr.y, dd = JMath.Hypot(rx, ry);
                    if (behind && dd < 7 && rx * pr.vx + ry * pr.vy > 0 && sign(pr.x - p.x) == sign(pr.x - q.x)) return true;
                }
            }
            return false;
        }
        // A mortar shell coming down within reach of us: where it lands (x), or null
        double? MortarZone(World world, Player p)
        {
            foreach (var pr in world.projectiles.Live())
            {
                if (pr.team != "e" || pr.dead || pr.kind != "mortar") continue;
                double x = pr.x, y = pr.y, vx = pr.vx, vy = pr.vy, g = pr.gravity, dt = 1.0 / 60;
                for (int i = 0; i < 120; i++)
                {
                    vy -= g * dt; x += vx * dt; y += vy * dt;
                    if (vy < 0 && y <= Level.GroundBelow(x, y + 0.5) + 0.1) break;
                }
                double r = pr.blast != null && truthy(pr.blast.r) ? pr.blast.r : 2;
                if (JMath.Abs(x - p.x) < r + 0.6 && JMath.Abs(y - p.y) < 2) return x;
            }
            return null;
        }

        // Answer a given threat or not: decided once per threat by chance (skill), and only after the skill's delay
        bool Answer(World world, Mem M, object key, BotSkill S, Func<double> rnd, bool now = false)
        {
            if (!M.react.TryGetValue(key, out var r)) { r = new React { at = world.tick, yes = rnd() < S.react }; M.react[key] = r; }
            return r.yes && (now || world.tick - r.at >= S.delay);
        }
        // An enemy winding up an attack on us, or an enemy shot that will reach us within a few ticks
        bool Threat(World world, Player p, Mem M, BotSkill S, Func<double> rnd)
        {
            double cx = p.x, cy = p.y + p.h / 2;
            object seen = null;
            foreach (var e in world.enemies.Live())
            {
                if (!Alive(e) || e.state != "windup" || (e.target != null && e.target != p)) continue;
                if (JMath.Abs(e.x - cx) < 3.2 && JMath.Abs(e.y - p.y) < 2) { seen = e; break; }
            }
            if (seen == null) foreach (var pr in world.projectiles.Live())
                {
                    if (pr.team != "e" || pr.dead) continue;
                    double rx = cx - pr.x, ry = cy - pr.y, d = JMath.Hypot(rx, ry);
                    if (d > 5 || (rx * pr.vx + ry * pr.vy) <= 0) continue;
                    double tHit = d / or(JMath.Hypot(pr.vx, pr.vy), 1) * 60;
                    if (tHit < 22) { seen = pr; break; }
                }
            var stale = new List<object>();
            foreach (var kv in M.react) if (world.tick - kv.Value.at > 90) stale.Add(kv.Key);
            foreach (var k in stale.Live()) M.react.Remove(k);
            if (seen == null) return false;
            return Answer(world, M, seen, S, rnd);
        }
    }
}
