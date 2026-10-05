// World, part three (port of world.js): the ultimates. A full bar and both triggers: the call. The world freezes for
// ULT.cast ticks while the caster powers up; teammates with a full bar can pull both triggers to join.
using System.Collections.Generic;
using System.Linq;
using static NovaStriker.Sim.Cfg;
using static NovaStriker.Sim.U;
using static NovaStriker.Sim.PlayerSim;
using static NovaStriker.Sim.Combat;

namespace NovaStriker.Sim
{
    public sealed partial class World
    {
        public void StartUlt(Player p)
        {
            p.ult = 0; p.chordP = p.chordF = 99;
            EnterUlt(p);
            ultCast = new UltCast { phase = "cast", t = 0, len = ULT.cast, name = ULT.NameOf(p.@char), team = false, power = 1 };
            ultCast.members.Add(p);
            // Nothing is left mid-motion to smear while everything holds still
            foreach (var q in players.Live()) { q.prevX = q.x; q.prevY = q.y; }
            foreach (var q in enemies.Live()) { q.prevX = q.x; q.prevY = q.y; }
            foreach (var pr in projectiles.Live()) { pr.px = pr.x; pr.py = pr.y; }
            foreach (var w in wells.Live()) { w.px = w.x; w.py = w.y; }
            Emit("ultCast", new Ev { p = p, name = ultCast.name, x = p.x, y = p.y + p.h * 0.6 });
        }
        void EnterUlt(Player p)
        {
            if (p.beam != null) EndBeam(p, "ult");
            if (p.rush != null) EndRush(p, "cancel");
            p.leap = null; p.patch = null; p.tossArmed = false;
            if (p.thrusting) { p.thrusting = false; Emit("thrustOff", new Ev { p = p }); }
            if (p.leash != null) ReleaseLeash(p);
            p.state = "ult"; p.st = 0; p.ultRun = null; p.dash = null; p.dodge = null; p.pound = null; p.lash = null; p.zip = null; p.slash = null; p.chargeT = 0; p.burstT = 0;
            p.subArmed = false; p.rifleT = 0; p.dashChargeT = 0; p.meleeCharged = false; p.crouch = false; p.hitstop = 0; p.wallSliding = false;
        }
        void UltCastTick(Dictionary<int, Cmd> cmds)
        {
            var U = ultCast; U.t++;
            foreach (var q in players.Live())
            {
                if (U.members.Contains(q) || q.state == "dead" || q.state == "downed") continue;
                var cmd = cmds != null && cmds.TryGetValue(q.slot, out var c) ? c : Cmd.EMPTY;
                TrackChord(q, cmd);
                if (q.ult >= ULT.max && ChordReady(q, cmd))
                {
                    q.ult = 0; q.chordP = q.chordF = 99; q.prevX = q.x; q.prevY = q.y;
                    EnterUlt(q); U.members.Add(q); U.len = JMath.Max(U.len, U.t + ULT.join);
                    Emit("ultJoin", new Ev { p = q, n = U.members.Count });
                }
            }
            if (U.t >= U.len) RunUlt();
        }
        void RunUlt()
        {
            var U = ultCast; U.phase = "run"; U.t = 0;
            U.team = U.members.Count > 1; U.power = U.team ? ULT.team.power : 1;
            if (U.team)
            {
                if (U.members.Count > 2) U.name = ULT.teamAll;
                else
                {
                    var key = string.Join("+", U.members.Select(m => m.@char).OrderBy(s => s, System.StringComparer.Ordinal));
                    U.name = ULT.teamNames.TryGetValue(key, out var nm) ? nm : ULT.teamAll;
                }
            }
            foreach (var m in U.members.ToArray()) BeginUlt(m, U.power);
            Emit("ultRun", new Ev { members = new List<Player>(U.members), team = U.team, name = U.name });
        }
        void BeginUlt(Player p, double power)
        {
            var c = Chest(p);
            if (p.@char == "nova")
            {
                // Supernova: it opens toward the lock-on target if he has one
                double dx = p.aimX, dy = p.aimY;
                if (p.lockT != null && !p.lockT.dead) { double ex = p.lockT.x - c.x, ey = p.lockT.y + p.lockT.h * 0.55 - c.y, m = or(JMath.Hypot(ex, ey), 1); dx = ex / m; dy = ey / m; }
                p.ultRun = new UltRun { kind = "nova", t = 0, power = power, dx = dx, dy = dy, pulse = 0, segs = null };
            }
            else if (p.@char == "ram")
            {
                // Siege Breaker: the charge runs the way he aims (toward the lock-on target if he has one)
                double dx = JMath.Abs(p.aimX) > 0.2 ? sign(p.aimX) : p.facing;
                if (p.lockT != null && !p.lockT.dead) dx = or(sign(p.lockT.x - p.x), dx);
                p.facing = dx;
                p.ultRun = new UltRun { kind = "ram", t = 0, power = power, dx = dx, carried = new List<Enemy>(), hit = new HashSet<Enemy>(), slamT = 0, blocked = false };
            }
            else if (p.@char == "fix")
            {
                p.ultRun = new UltRun { kind = "fix", t = 0, power = power, podX = p.x, podY = p.y };
            }
            else
            {
                // Thousand Cuts: the targets are picked now and the cuts shared out among them, nearest first
                double D(Enemy e) => JMath.Hypot(e.x - c.x, e.y + e.h / 2 - c.y);
                var targets = enemies.FindAll(e => !e.dead && D(e) <= ULT.echo.range);
                StableSort.Sort(targets, (a, b) => D(a) - D(b));
                if (targets.Count > ULT.echo.targets) targets = targets.GetRange(0, (int)ULT.echo.targets);
                var cuts = new List<Cut>();
                if (targets.Count > 0) for (int i = 0; i < ULT.echo.strikes; i++) cuts.Add(new Cut { e = targets[i % targets.Count], at = ULT.echo.start + i * ULT.echo.every, i = i });
                double fin = cuts.Count > 0 ? cuts[cuts.Count - 1].at + 14 : ULT.echo.start;
                p.ultRun = new UltRun { kind = "echo", t = 0, power = power, targets = targets, cuts = cuts, fin = fin, end = fin + ULT.echo.end, x0 = p.x, y0 = p.y };
            }
            Emit("ultBegin", new Ev { p = p, kind = p.ultRun.kind });
        }
        // Ultimate damage: breaks armor, reduced on bosses, and never charges anyone's ultimate
        string UltHit(Player p, Enemy e, double dmg, double poise, double kx = 0) =>
            HitEnemy(this, e, new Hit { owner = p, dmg = dmg * (e.boss ? ULT.boss : 1), poise = poise, kb = new[] { kx, 3.0 }, armorBreak = true, ult = true }, "blast");
        // Each member's ultimate, a tick at a time (called from their 'ult' state)
        public void UltStep(Player p, Cmd cmd)
        {
            var R = p.ultRun;
            if (R == null) { p.vx = 0; p.vy = p.onGround ? -0.5 : 0; return; }
            R.t++;
            if (R.kind == "nova") UltNova(p, R);
            else if (R.kind == "ram") UltRam(p, R);
            else if (R.kind == "fix") UltFix(p, R);
            else UltEcho(p, R);
        }
        // Siege Breaker
        void UltRam(Player p, UltRun R)
        {
            double t = R.t;
            void Fall() { p.vy = p.onGround ? -0.5 : JMath.Max(p.vy - GRAVITY * DT, -MAX_FALL); }
            p.facing = R.dx;
            if (t == 1)
            {
                foreach (var q in players.Live()) if (q.state != "dead" && q.state != "downed") AddPlate(q, ULT.ram.fortify);
                Emit("fortify", new Ev { p = p, members = players.FindAll(q => q.state != "dead" && q.state != "downed") });
            }
            if (truthy(R.slamT)) { p.vx *= 0.7; Fall(); if (t >= R.slamT + ULT.ram.end) FinishUlt(p); return; }
            if (t <= ULT.ram.brace) { p.vx = 0; Fall(); return; }
            p.vx = R.dx * ULT.ram.speed; Fall();
            if (t >= ULT.ram.brace + ULT.ram.charge) UltRamSlam(p, R);
        }
        void UltRamCarry(Player p)
        {
            var R = p.ultRun;
            if (R == null || R.kind != "ram" || truthy(R.slamT) || R.t <= ULT.ram.brace) return;
            double dir = R.dx, front = p.x + dir * p.w / 2;
            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                if (e.dead || R.hit.Contains(e)) continue;
                double ax0 = dir > 0 ? front - 0.3 : front - 1.3, ax1 = dir > 0 ? front + 1.3 : front + 0.3;
                if (e.x + e.w / 2 < ax0 || e.x - e.w / 2 > ax1 || e.y > p.y + p.h + 0.6 || e.y + e.h < p.y) continue;
                R.hit.Add(e);
                if (e.boss || e.type == "post" || e.type == "turret") { UltHit(p, e, ULT.ram.catchDmg * 2 * R.power, 80, dir * 3); R.blocked = true; continue; }
                UltHit(p, e, ULT.ram.catchDmg * R.power, 40, 0);
                if (!e.dead) { R.carried.Add(e); e.state = "plowed"; e.st = 0; e.plowBy = p; Emit("plowCatch", new Ev { p = p, e = e, level = 3 }); }
            }
            CarryPile(p, R.carried, dir);
            if ((R.t - ULT.ram.brace) % ULT.ram.every == 0) foreach (var e in R.carried.ToArray()) if (!e.dead) UltHit(p, e, ULT.ram.dmg * R.power, 10, 0);
            bool jam = R.carried.Exists(e => !e.dead && !Level.HasHeadroom(e.x, e.y + 0.05, e.w, e.h - 0.1));
            if (truthy(p.hitWall) || R.blocked || jam) UltRamSlam(p, R);
        }
        void UltRamSlam(Player p, UltRun R)
        {
            var S = ULT.ram.slam; R.slamT = R.t; p.vx = 0;
            double x = p.x + R.dx * 1.4, y = p.y + 1.0, r = S.r * (R.power > 1 ? 1.15 : 1);
            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                if (e.dead) continue;
                double nx = JMath.Max(e.x - e.w / 2, JMath.Min(x, e.x + e.w / 2)), ny = JMath.Max(e.y, JMath.Min(y, e.y + e.h));
                if (JMath.Hypot(nx - x, ny - y) <= r) UltHit(p, e, S.dmg * R.power, S.poise, or(sign(e.x - p.x), R.dx) * 12);
            }
            foreach (var e in R.carried.Live()) { if (e.dead) continue; e.plowBy = null; e.state = "launched"; e.st = 0; e.vx = R.dx * 11; e.vy = 9; }
            R.carried = new List<Enemy>();
            Emit("ramSlam", new Ev { p = p, x = x, y = y, r = r });
        }
        // Overhaul
        void UltFix(Player p, UltRun R)
        {
            double t = R.t;
            p.vx *= 0.7; p.vy = p.onGround ? -0.5 : JMath.Max(p.vy - GRAVITY * DT, -MAX_FALL);
            if (t == 1)
            {
                double x = p.x + p.facing * 2.4;
                for (int i = 0; i < 8 && Level.PointInSolid(x, p.y + 0.5); i++) x -= p.facing * 0.3;
                double g = Level.GroundBelow(x, p.y + 1); R.podX = x; R.podY = g > double.NegativeInfinity && p.y - g < 6 ? g : p.y;
                Emit("podCall", new Ev { p = p, x = R.podX, y = R.podY, ticks = ULT.fix.drop });
            }
            if (t == ULT.fix.drop)
            {
                for (int i = 0; i < enemies.Count; i++)
                {
                    var e = enemies[i];
                    if (e.dead || JMath.Abs(e.x - R.podX) > ULT.fix.pod.r + e.w / 2 || e.y > R.podY + 3 || e.y + e.h < R.podY - 0.5) continue;
                    UltHit(p, e, ULT.fix.pod.dmg * R.power, ULT.fix.pod.poise, or(sign(e.x - R.podX), 1) * 9);
                }
                Emit("podLand", new Ev { p = p, x = R.podX, y = R.podY });
            }
            int pi = System.Array.IndexOf(ULT.fix.pulses, t);
            if (pi >= 0)
            {
                foreach (var q in players.ToArray())
                {
                    if (q.state == "dead") continue;
                    if (q.state == "downed") { if (pi == 0) RevivePlayer(q, p, FIX.reviveHp); continue; }
                    Heal(q, q.maxHp * ULT.fix.heal * (R.power > 1 ? 1.2 : 1));
                }
                var C = cam;
                for (int i = 0; i < enemies.Count; i++)
                {
                    var e = enemies[i];
                    if (e.dead || JMath.Abs(e.x - C.x) > C.halfW + 2 || JMath.Abs(e.y + e.h / 2 - C.y) > C.halfH + 2) continue;
                    UltHit(p, e, ULT.fix.dmg * R.power, 30, or(sign(e.x - R.podX), 1) * 4);
                }
                Emit("overhaulPulse", new Ev { p = p, x = R.podX, y = R.podY + 1.2, i = pi });
            }
            if (t == ULT.fix.end - 8)
            {
                foreach (var q in players.Live()) if (q.state != "dead" && q.state != "downed") { q.overclockT = JMath.Max(q.overclockT, ULT.fix.overclock); AddPlate(q, ULT.fix.plate); }
                foreach (var g in gadgets.Live()) if (g.owner == p && g.kind != "pad") { g.level = 3; g.pts = 0; g.t = 0; g.life = FIX.gadget[g.kind].life[2]; g.hp = g.maxHp; }
                Emit("overhaulDone", new Ev { p = p, x = R.podX, y = R.podY });
            }
            if (t >= ULT.fix.end) FinishUlt(p);
        }
        // Supernova
        void UltNova(Player p, UltRun R)
        {
            double t = R.t; var c = Chest(p);
            // He rises into a hover while the light gathers, then holds there
            p.vx *= 0.7; p.vy = t <= 12 ? ULT.nova.rise * 10 * (1 - t / 12) : 0;
            if (t > ULT.nova.gather && t <= ULT.nova.gather + ULT.nova.beam)
            {
                double a0 = JMath.Atan2(R.dy, R.dx); double da = JMath.Atan2(p.aimY, p.aimX) - a0;
                while (da > JMath.PI) da -= 2 * JMath.PI; while (da < -JMath.PI) da += 2 * JMath.PI;
                double a = a0 + JMath.Max(-ULT.nova.turn, JMath.Min(ULT.nova.turn, da)); R.dx = JMath.Cos(a); R.dy = JMath.Sin(a);
                if (JMath.Abs(R.dx) > 0.2) p.facing = sign(R.dx);
                var g = new BeamSeg { x0 = c.x + R.dx * 0.6, y0 = c.y + R.dy * 0.6, x1 = c.x + R.dx * ULT.nova.range, y1 = c.y + R.dy * ULT.nova.range };
                R.segs = new List<BeamSeg> { g };
                foreach (var pr in projectiles.Live()) if (pr.team == "e" && !pr.dead && DistToSeg(pr.x, pr.y, g) < ULT.nova.width + pr.r) { pr.dead = true; Emit("erase", new Ev { x = pr.x, y = pr.y }); }
                if ((t - ULT.nova.gather) % ULT.nova.pulse == 1)
                {
                    R.pulse++;
                    for (int i = 0; i < enemies.Count; i++) { var e = enemies[i]; if (!e.dead && SegHitsBox(g, Hurtbox(e), ULT.nova.width)) UltHit(p, e, ULT.nova.dmg * R.power, 30, sign(R.dx) * 2); }
                }
            }
            else R.segs = null;
            if (t == ULT.nova.gather + ULT.nova.beam + 6)
            {
                // The nova: a burst of light from where he hangs
                var B = ULT.nova.novaBlast; double r = B.r * (R.power > 1 ? 1.15 : 1);
                for (int i = 0; i < enemies.Count; i++)
                {
                    var e = enemies[i];
                    if (e.dead) continue;
                    double nx = JMath.Max(e.x - e.w / 2, JMath.Min(c.x, e.x + e.w / 2)), ny = JMath.Max(e.y, JMath.Min(c.y, e.y + e.h));
                    if (JMath.Hypot(nx - c.x, ny - c.y) <= r) UltHit(p, e, B.dmg * R.power, B.poise, or(sign(e.x - c.x), 1) * 9);
                }
                Emit("ultNova", new Ev { p = p, x = c.x, y = c.y, r = r });
            }
            if (t >= ULT.nova.end) FinishUlt(p);
        }
        // Thousand Cuts
        void UltEcho(Player p, UltRun R)
        {
            double t = R.t;
            p.vx = 0; p.vy = p.onGround ? -0.5 : 0;   // he is gone: only his cuts are seen
            foreach (var cut in R.cuts.Live())
            {
                if (cut.at != t) continue;
                var e = cut.e;
                if (e.dead) e = R.targets.Find(q => !q.dead);   // its target fell: the cut goes to one still standing
                if (e == null) continue;
                double dir = cut.i % 2 != 0 ? 1 : -1;
                UltHit(p, e, ULT.echo.dmg * R.power, 16, dir * 2);
                Emit("ultCut", new Ev { p = p, e = e, x = e.x, y = e.y + e.h * 0.55, i = cut.i, dir = dir, ang = ((cut.i * 2.39996) % JMath.PI) - JMath.PI / 2 });
            }
            if (t == R.fin)
            {
                // Every cut lands again at once
                if (R.cuts.Count > 0) { foreach (var e in R.targets.Live()) if (!e.dead) UltHit(p, e, ULT.echo.finisher * R.power, 90, or(sign(e.x - p.x), 1) * 8); }
                else
                {
                    // No one in reach: a flourish around him
                    var c = Chest(p);
                    for (int i = 0; i < enemies.Count; i++) { var e = enemies[i]; if (!e.dead && JMath.Hypot(e.x - c.x, e.y + e.h / 2 - c.y) <= ULT.echo.flourish.r) UltHit(p, e, ULT.echo.flourish.dmg * R.power, 60); }
                }
                Emit("ultFinisher", new Ev { p = p, x = p.x, y = p.y + p.h * 0.6, targets = R.targets.FindAll(e => !e.dead || e.deathT < 3), flourish = R.cuts.Count == 0 });
            }
            if (t >= R.end) FinishUlt(p);
        }
        void FinishUlt(Player p)
        {
            var R = p.ultRun;
            if (R != null && R.carried != null) foreach (var e in R.carried.Live()) if (!e.dead && e.state == "plowed") { e.plowBy = null; e.state = "launched"; e.st = 0; e.vy = 6; }
            p.ultRun = null; p.state = "normal"; p.st = 0; p.mercy = JMath.Max(p.mercy, ULT.mercy); p.vy = JMath.Min(p.vy, 0);
            Emit("ultEnd", new Ev { p = p });
        }
        // The run (and a team ultimate's finisher) after every member has finished
        void UltTick()
        {
            var U = ultCast;
            U.members.RemoveAll(m => !players.Contains(m));
            U.t++;
            if (U.phase == "run")
            {
                if (U.members.Exists(m => m.ultRun != null)) return;
                if (U.team && U.members.Count > 0) { U.phase = "finish"; U.t = 0; }
                else ultCast = null;
            }
            else if (U.phase == "finish")
            {
                if (U.t == 8)
                {
                    // The team finisher: every enemy on screen
                    var C = cam; int n = U.members.Count; var lead = U.members[0];
                    for (int i = 0; i < enemies.Count; i++)
                    {
                        var e = enemies[i];
                        if (e.dead || JMath.Abs(e.x - C.x) > C.halfW + 2 || JMath.Abs(e.y + e.h / 2 - C.y) > C.halfH + 2) continue;
                        UltHit(lead, e, ULT.team.dmg * n, 120, or(sign(e.x - C.x), 1) * 10);
                    }
                    Emit("teamFinisher", new Ev { name = U.name, members = new List<Player>(U.members), x = C.x, y = C.y, chars = U.members.Select(m => m.@char).ToArray() });
                }
                if (U.t >= ULT.team.t) ultCast = null;
            }
        }
    }
}
