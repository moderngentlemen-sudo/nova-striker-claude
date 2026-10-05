// Level bosses (port of bosses.js). The Lockwarden holds the Concourse Lock (its last wave); the Stormcaller guards
// the relay beacon at the end of the Skyline Relay. Both have two phases: at half health they roar (invulnerable for
// a moment), re-arm and speed up, and gain a new attack.
using System;
using static NovaStriker.Sim.Cfg;
using static NovaStriker.Sim.U;

namespace NovaStriker.Sim
{
    public static class Bosses
    {
        public sealed class Swing { public double wind, active, rec, reach, dmg; public double[] kb; }
        public sealed class WardenDef
        {
            public string name = "Lockwarden", title = "The lock’s last guard";
            public double hp = 240, armor = 4, rearm = 2, poise = 420, poiseDecay = 0.6, stagger = 150, staggerCd = 420, daze = 110, walk = 2.4;
            public double[] cd = { 46, 28 };
            public Swing sweep = new Swing { wind = 22, active = 8, rec = 30, reach = 3.6, dmg = 14, kb = new double[] { 9, 4 } };
            public Swing hammer = new Swing { wind = 34, active = 6, rec = 44, reach = 3.9, dmg = 24, kb = new double[] { 12, 6 } };
            public double stompWind = 40, stompJump = 13, stompHop = 6, stompDmg = 22, stompRec = 40;
            public double missilesWind = 26, missilesDmg = 10, missilesGravity = 16, missilesRec = 34; public double[] missilesN = { 3, 5 };
            public double chargeWind = 36, chargeSpeed = 16, chargeMaxTicks = 120, chargeDmg = 26, chargeRec = 30;
            public double laserWind = 50, laserTicks = 42, laserDmg = 20, laserRange = 40, laserRec = 36;
            public double[] laserLow = { 0.3, 0.8 }, laserHigh = { 1.15, 1.55 };
        }
        public sealed class StormDef
        {
            public string name = "Stormcaller", title = "It keeps the relay beacon";
            public double hp = 260, armor = 0, rearm = 2, poise = 380, poiseDecay = 0.6, stagger = 140, staggerCd = 420, hover = 6.6, speed = 7;
            public double[] cd = { 40, 26 };
            public double[] padX = { 300.5, 313.5 }; public double floor = 18.6;
            public double volleyWind = 24, volleyEvery = 10, volleySpeed = 14, volleyDmg = 8, volleySpread = 0.12, volleyRec = 26; public double[] volleyBursts = { 3, 4 };
            public double rainWind = 40, rainGravity = 26, rainRec = 30; public double[] rainN = { 4, 6 }; public Blast rainBlast = new Blast { r = 1.7, dmg = 16 };
            public double sweepWind = 50, sweepTicks = 44, sweepHoverY = 2.4, sweepDmg = 20, sweepRec = 60;
            public double[] sweepLow = { 0.3, 0.8 }, sweepHigh = { 1.15, 1.55 };
            public double diveWind = 36, diveSpeed = 22, diveMaxTicks = 60, diveDmg = 24, diveCrash = 70, diveParried = 115, diveRise = 40;
            public double drones = 2;
        }
        public static readonly WardenDef WARDEN = new WardenDef();
        public static readonly StormDef STORM = new StormDef();
        public static string NameOf(string type) => type == "warden" ? WARDEN.name : STORM.name;
        public static string TitleOf(string type) => type == "warden" ? WARDEN.title : STORM.title;

        static void SetState(Enemy e, string s) { e.state = s; e.st = 0; }
        static string Pick(Enemy e, (string k, double w)[] opts, int count)
        {
            double sum = 0; int n = 0;
            var pool = new (string k, double w)[count];
            for (int i = 0; i < count; i++) if (opts[i].k != e.lastAtk) { pool[n++] = opts[i]; sum += opts[i].w; }
            double r = JRandom.Next() * sum;
            for (int i = 0; i < n; i++) { r -= pool[i].w; if (r <= 0) return pool[i].k; }
            return pool[0].k;
        }

        // Shared: poise decays, stagger immunity, and the half-health phase change (a roar that re-arms it)
        static bool BossTick(Enemy e, World world, double poiseDecay, double rearm)
        {
            e.poise = e.staggerCd > 0 ? 0 : JMath.Max(0, e.poise - poiseDecay);
            if (e.staggerCd > 0) e.staggerCd--;
            if (e.invuln > 0) e.invuln--;
            if (e.phase == 1 && e.hp <= e.maxHp / 2 && e.state != "intro")
            {
                e.phase = 2; e.armor = e.armorMax = rearm; e.invuln = 80; e.atk = null;
                SetState(e, "roar");
                world.Emit("bossPhase", new Ev { e = e, x = e.x, y = e.y + e.h * 0.6 });
                return true;
            }
            return false;
        }

        // Starts an attack: telegraphs it and remembers what it is
        static void Begin(Enemy e, World world, string kind, string cat, double wind, Action<EnemyAtk> extra = null)
        {
            e.atk = new EnemyAtk { kind = kind, wind = wind, inst = world.NewInstance() }; extra?.Invoke(e.atk); e.lastAtk = kind;
            SetState(e, "windup"); world.Telegraph(e, cat, wind);
        }

        // Where a laser runs: from the boss along `dir` at height band [y0, y1] above the floor, to the first wall
        static LaserSpan Span(Enemy e, double dir, double fy, double[] band, double range)
        {
            double y = fy + (band[0] + band[1]) / 2, x0 = e.x + dir * (e.w / 2 + 0.1);
            var h = Level.RayCast(x0, y, dir, 0, range);
            return new LaserSpan { x0 = x0, x1 = x0 + dir * h.t, y0 = fy + band[0], y1 = fy + band[1], y = y };
        }

        // ---- Lockwarden ----
        public static void Warden(Enemy e, World world)
        {
            var B = WARDEN; double fast = e.phase == 2 ? 0.8 : 1;
            if (BossTick(e, world, B.poiseDecay, B.rearm)) { Enemies.Physics(e); return; }
            var p = Enemies.NearestPlayer(e, world, 60); e.target = p;
            string s = e.state; var A = e.atk;
            if (s == "intro")
            {
                // Dropped into the lock from above: the landing throws shockwaves both ways
                Enemies.Physics(e);
                if (e.onGround && !e.landed) { e.landed = true; world.SpawnShockwave(e, 1, 12); world.SpawnShockwave(e, -1, 12); world.Emit("bossSlam", new Ev { e = e, x = e.x, y = e.y, big = true }); }
                if (e.landed && e.st >= 110) { e.invuln = 0; SetState(e, "idle"); e.cd = 30; }
                return;
            }
            if (s == "roar") { e.vx *= 0.8; Enemies.Physics(e); if (e.st >= 80) { SetState(e, "idle"); e.cd = 16; } return; }
            if (s == "dazed") { e.vx *= 0.85; Enemies.Physics(e); if (e.st >= B.daze) { SetState(e, "idle"); e.cd = 20; } return; }
            if (s == "idle" || s == "approach")
            {
                if (p == null) { e.vx *= 0.8; Enemies.Physics(e); return; }
                double dx = p.x - e.x, dist = JMath.Abs(dx);
                e.facing = dx >= 0 ? 1 : -1;
                if (dist > 3.2) e.vx = approach(e.vx, e.facing * B.walk * (e.phase == 2 ? 1.3 : 1), 0.35); else e.vx *= 0.7;
                if (e.cd == 0 && e.onGround)
                {
                    var opts = new (string, double)[4]; int n;
                    if (dist < 4.4) { opts[0] = ("sweep", 4); opts[1] = ("hammer", 3); opts[2] = ("stomp", 2); n = 3; }
                    else if (dist < 11) { opts[0] = ("charge", 3); opts[1] = ("missiles", 3); opts[2] = ("stomp", 2); n = 3; }
                    else { opts[0] = ("missiles", 4); opts[1] = ("charge", 3); n = 2; }
                    if (e.phase == 2) opts[n++] = ("laser", 3);
                    StartWarden(e, world, Pick(e, opts, n));
                }
                Enemies.Physics(e); return;
            }
            if (A == null) { SetState(e, "idle"); Enemies.Physics(e); return; }
            if (s == "windup")
            {
                e.vx *= 0.6;
                if (p != null && A.kind != "laser" && A.kind != "charge") e.facing = p.x >= e.x ? 1 : -1;
                if (A.kind == "laser") A.span = Span(e, e.facing, e.y, A.high ? B.laserHigh : B.laserLow, B.laserRange);
                if (e.st >= A.wind)
                {
                    if (A.kind == "stomp") { e.vy = B.stompJump; e.vx = p != null ? clamp((p.x - e.x) * 0.9, -B.stompHop, B.stompHop) : 0; e.onGround = false; SetState(e, "jump"); }
                    else if (A.kind == "missiles") { FireMissiles(e, world); SetState(e, "recover"); A.rec = B.missilesRec; }
                    else if (A.kind == "charge") { SetState(e, "charge"); world.Emit("chargeStart", new Ev { e = e }); }
                    else if (A.kind == "laser") { SetState(e, "laser"); world.Emit("bossLaser", new Ev { e = e, ticks = B.laserTicks, high = A.high }); }
                    else SetState(e, "attack");
                }
                Enemies.Physics(e); return;
            }
            if (s == "attack")
            {
                var S = A.kind == "hammer" ? B.hammer : B.sweep; double x0 = e.facing > 0 ? e.x + 0.3 : e.x - 0.3 - S.reach;
                world.SpawnHitbox(new Hit
                {
                    owner = e, team = "e", x0 = x0, x1 = x0 + S.reach, y0 = e.y + (A.kind == "hammer" ? 0 : 0.2), y1 = e.y + (A.kind == "hammer" ? 3.0 : 2.6),
                    dmg = S.dmg, kb = new[] { e.facing * S.kb[0], S.kb[1] }, heavy = A.kind == "hammer", instance = A.inst, cat = A.kind == "hammer" ? "heavy" : "standard",
                });
                if (A.kind == "hammer" && e.st == 2) world.Emit("bossSlam", new Ev { e = e, x = e.x + e.facing * 2.6, y = e.y });
                if (e.st >= S.active) { SetState(e, "recover"); A.rec = S.rec; }
                e.vx *= 0.5; Enemies.Physics(e); return;
            }
            if (s == "jump")
            {
                Enemies.Physics(e);
                if (e.onGround && e.st > 3)
                {
                    world.SpawnShockwave(e, 1, B.stompDmg, 1.3); world.SpawnShockwave(e, -1, B.stompDmg, 1.3);
                    world.Emit("bossSlam", new Ev { e = e, x = e.x, y = e.y, big = true });
                    if (--A.hops > 0) { e.vy = B.stompJump; e.vx = p != null ? clamp((p.x - e.x) * 0.9, -B.stompHop, B.stompHop) : 0; e.onGround = false; e.st = 0; }
                    else { SetState(e, "recover"); A.rec = B.stompRec; e.vx = 0; }
                }
                return;
            }
            if (s == "charge")
            {
                e.vx = e.facing * B.chargeSpeed;
                double x0 = e.facing > 0 ? e.x + 0.4 : e.x - 1.9;
                world.SpawnHitbox(new Hit { owner = e, team = "e", x0 = x0, x1 = x0 + 1.5, y0 = e.y + 0.1, y1 = e.y + 2.8, dmg = B.chargeDmg, heavy = true, kb = new[] { e.facing * 13, 6.0 }, instance = A.inst, cat = "heavy" });
                Enemies.Physics(e);
                if (truthy(e.hitWall)) { SetState(e, "dazed"); e.vx = -e.facing * 3; world.Emit("chargeCrash", new Ev { e = e }); world.Emit("bossSlam", new Ev { e = e, x = e.x + e.facing * 1.1, y = e.y }); }
                else if (e.st >= B.chargeMaxTicks) { SetState(e, "recover"); A.rec = B.chargeRec; }
                return;
            }
            if (s == "laser")
            {
                A.span = Span(e, e.facing, e.y, A.high ? B.laserHigh : B.laserLow, B.laserRange);
                var L = A.span;
                world.SpawnHitbox(new Hit { owner = e, team = "e", x0 = JMath.Min(L.x0, L.x1), x1 = JMath.Max(L.x0, L.x1), y0 = L.y0, y1 = L.y1, dmg = B.laserDmg, kb = new[] { e.facing * 6, 5.0 }, unblockable = true, cat = "unblockable", instance = A.inst });
                e.vx = 0; Enemies.Physics(e);
                if (e.st >= B.laserTicks) { SetState(e, "recover"); A.rec = B.laserRec; }
                return;
            }
            if (s == "recover")
            {
                e.vx *= 0.8; Enemies.Physics(e);
                // A perfect parry of the hammer (or any heavy blow) leaves it dazed
                if (e.parried == 2) { e.parried = 0; SetState(e, "dazed"); world.Emit("bossDazed", new Ev { e = e, x = e.x, y = e.y + e.h * 0.7 }); return; }
                if (e.st >= or(A.rec, 30) * fast) { e.atk = null; e.parried = 0; SetState(e, "idle"); e.cd = B.cd[(int)e.phase - 1]; }
                return;
            }
            SetState(e, "idle"); Enemies.Physics(e);
        }

        static void StartWarden(Enemy e, World world, string k)
        {
            var B = WARDEN; double fast = e.phase == 2 ? 0.8 : 1;
            e.vx = 0;
            if (k == "sweep") Begin(e, world, k, "standard", JMath.Round(B.sweep.wind * fast));
            else if (k == "hammer") Begin(e, world, k, "heavy", JMath.Round(B.hammer.wind * fast));
            else if (k == "stomp") Begin(e, world, k, "unblockable", JMath.Round(B.stompWind * fast), a => a.hops = e.phase == 2 ? 2 : 1);
            else if (k == "missiles") Begin(e, world, k, "standard", JMath.Round(B.missilesWind * fast));
            else if (k == "charge") Begin(e, world, k, "heavy", JMath.Round(B.chargeWind * fast));
            else { e.laserHigh = !e.laserHigh; bool hi = e.laserHigh; Begin(e, world, k, "unblockable", JMath.Round(B.laserWind * fast), a => a.high = hi); }
        }

        // Missiles lob up off its back and come down on the players (with some spread)
        static void FireMissiles(Enemy e, World world)
        {
            var B = WARDEN; int n = (int)B.missilesN[(int)e.phase - 1];
            var targets = world.players.FindAll(Enemies.CanTarget);
            for (int i = 0; i < n; i++)
            {
                var t = targets.Count > 0 ? targets[i % targets.Count] : null;
                double sx = e.x - e.facing * 0.5 + (i - (n - 1) / 2.0) * 0.25, sy = e.y + e.h + 0.1;
                double tx = (t != null ? t.x : e.x + e.facing * 6) + (i - (n - 1) / 2.0) * 1.3, ty = JMath.Max(Level.GroundBelow(tx, (t != null ? t.y : e.y) + 1), (t != null ? t.y : e.y) - 6) + 0.6;
                double T = clamp(1.0 + JMath.Abs(tx - sx) / 24 + i * 0.07, 1.0, 1.7), g = B.missilesGravity;
                world.SpawnProjectile(new Projectile { team = "e", owner = e, x = sx, y = sy, vx = (tx - sx) / T, vy = (ty - sy + 0.5 * g * T * T) / T, gravity = g, r = 0.26, dmg = B.missilesDmg, kind = "missile", ttl = 240 });
            }
            world.Emit("bossMissiles", new Ev { e = e, n = n });
        }

        // ---- Stormcaller ----
        static void Home(Enemy e, double tx, double ty, double k, double max)
        {
            e.vx = approach(e.vx, clamp((tx - e.x) * k, -max, max), 0.45); e.vy = approach(e.vy, clamp((ty - e.y) * k, -max, max), 0.45);
        }
        public static void Stormcaller(Enemy e, World world)
        {
            var B = STORM; double fast = e.phase == 2 ? 0.8 : 1, floor = B.floor;
            if (BossTick(e, world, B.poiseDecay, B.rearm)) { e.vx *= 0.9; e.vy *= 0.9; Enemies.Physics(e); return; }
            var p = Enemies.NearestPlayer(e, world, 60); e.target = p;
            string s = e.state; var A = e.atk; double bob = JMath.Sin(world.tick * 0.04) * 0.4;
            if (s == "intro")
            {
                Home(e, e.homeX, floor + B.hover, 1.2, 5);
                if (e.st >= 110) { e.invuln = 0; SetState(e, "idle"); e.cd = 30; }
                Enemies.Physics(e); return;
            }
            if (s == "roar")
            {
                e.vx *= 0.9; e.vy *= 0.9;
                if (e.st == 30)
                {
                    // Phase two: it calls in drones
                    for (int i = 0; i < B.drones; i++)
                    {
                        int ii = i;
                        var d = Enemies.CreateEnemy("drone", e.x + (i != 0 ? 4 : -4), e.y + 1.5, q => { q.zone = e.zone; q.enc = e.enc; q.cd = 60 + ii * 30; q.add = true; });
                        world.enemies.Add(d);
                    }
                    world.Emit("bossCall", new Ev { e = e });
                }
                if (e.st >= 80) { SetState(e, "idle"); e.cd = 16; }
                Enemies.Physics(e); return;
            }
            if (s == "crashed")
            {
                // Down on the pad: open to everything until it lifts off
                e.vx *= 0.85; e.vy = JMath.Max(e.vy - GRAVITY * DT, -12);
                Enemies.Physics(e);
                if (e.st >= e.crashFor) SetState(e, "rise");
                return;
            }
            if (s == "rise") { Home(e, e.x, floor + B.hover, 1.4, 6); Enemies.Physics(e); if (e.st >= B.diveRise) { SetState(e, "idle"); e.cd = 16; } return; }
            if (s == "idle")
            {
                if (p == null) { Home(e, e.homeX, floor + B.hover + bob, 1.6, B.speed); Enemies.Physics(e); return; }
                e.facing = p.x >= e.x ? 1 : -1;
                // Hover above the pad off to one side of the target
                double side = e.x >= p.x ? 1 : -1, tx = clamp(p.x + side * 4.5, B.padX[0], B.padX[1]);
                Home(e, tx, floor + B.hover + bob, 1.6, B.speed);
                if (e.cd == 0) StartStorm(e, world, Pick(e, new[] { ("volley", 4.0), ("rain", 3.0), ("sweep", 3.0), ("dive", 3.0) }, 4));
                Enemies.Physics(e); return;
            }
            if (A == null) { SetState(e, "idle"); Enemies.Physics(e); return; }
            if (s == "reposition")
            {
                double tx = B.padX[(int)A.edge], ty = floor + B.sweepHoverY;
                Home(e, tx, ty, 2.2, 9);
                e.facing = A.edge == 0 ? 1 : -1;
                if ((JMath.Abs(e.x - tx) < 0.4 && JMath.Abs(e.y - ty) < 0.4) || e.st > 90) { SetState(e, "windup"); world.Telegraph(e, "unblockable", A.wind); }
                Enemies.Physics(e); return;
            }
            if (s == "windup")
            {
                if (A.kind == "sweep") { Home(e, B.padX[(int)A.edge], floor + B.sweepHoverY, 2, 4); A.span = Span(e, e.facing, floor, A.high ? B.sweepHigh : B.sweepLow, 30); }
                else { e.vx *= 0.9; e.vy *= 0.9; if (p != null) e.facing = p.x >= e.x ? 1 : -1; }
                if (A.kind == "dive" && p != null) { A.tx = p.x; A.ty = p.y + 0.6; }
                if (e.st >= A.wind)
                {
                    if (A.kind == "volley") { SetState(e, "volley"); A.n = 0; }
                    else if (A.kind == "rain") { FireRain(e, world); SetState(e, "recover"); A.rec = B.rainRec; }
                    else if (A.kind == "sweep") { SetState(e, "laser"); world.Emit("bossLaser", new Ev { e = e, ticks = B.sweepTicks, high = A.high }); }
                    else
                    {
                        double dx = A.tx - e.x, dy = A.ty - (e.y + e.h / 2), m = or(JMath.Hypot(dx, dy), 1);
                        A.dx = dx / m; A.dy = dy / m; SetState(e, "dive"); world.Emit("bossDive", new Ev { e = e });
                    }
                }
                Enemies.Physics(e); return;
            }
            if (s == "volley")
            {
                e.vx *= 0.9; e.vy *= 0.9;
                if (e.st % B.volleyEvery == 1 && p != null && Enemies.CanTarget(p))
                {
                    double sx = e.x + e.facing * 1.2, sy = e.y + 0.3, a = JMath.Atan2(p.y + 1 - sy, p.x - sx);
                    for (int d = -1; d <= 1; d += 2)
                    {
                        double aa = a + d * B.volleySpread;
                        world.SpawnProjectile(new Projectile { team = "e", owner = e, x = sx, y = sy + d * 0.25, vx = JMath.Cos(aa) * B.volleySpeed, vy = JMath.Sin(aa) * B.volleySpeed, r = 0.2, dmg = B.volleyDmg, kind = "std", ttl = 150 });
                    }
                    world.Emit("enemyShot", new Ev { e = e, heavy = false });
                    if (++A.n >= A.shots) { SetState(e, "recover"); A.rec = B.volleyRec; }
                }
                Enemies.Physics(e); return;
            }
            if (s == "laser")
            {
                Home(e, B.padX[(int)A.edge], floor + B.sweepHoverY, 2, 3);
                A.span = Span(e, e.facing, floor, A.high ? B.sweepHigh : B.sweepLow, 30);
                var L = A.span;
                world.SpawnHitbox(new Hit { owner = e, team = "e", x0 = JMath.Min(L.x0, L.x1), x1 = JMath.Max(L.x0, L.x1), y0 = L.y0, y1 = L.y1, dmg = B.sweepDmg, kb = new[] { e.facing * 6, 5.0 }, unblockable = true, cat = "unblockable", instance = A.inst });
                Enemies.Physics(e);
                if (e.st >= B.sweepTicks) { SetState(e, "recover"); A.rec = B.sweepRec; A.low = true; }
                return;
            }
            if (s == "dive")
            {
                e.vx = A.dx * B.diveSpeed; e.vy = A.dy * B.diveSpeed;
                world.SpawnHitbox(new Hit
                {
                    owner = e, team = "e", x0 = e.x - e.w / 2 - 0.2, x1 = e.x + e.w / 2 + 0.2, y0 = e.y - 0.2, y1 = e.y + e.h, dmg = B.diveDmg, heavy = true,
                    kb = new[] { JMath.Sign(or(A.dx, e.facing)) * 12, 6.0 }, instance = A.inst, cat = "heavy",
                });
                Enemies.Physics(e);
                bool hitFloor = e.onGround || truthy(e.hitWall) || e.y <= floor + 0.05;   // the pad, a perch or a wall
                if (e.parried == 2 || hitFloor || e.st >= B.diveMaxTicks)
                {
                    bool parried = e.parried == 2; e.parried = 0;
                    if (parried || hitFloor) { e.crashFor = parried ? B.diveParried : B.diveCrash; SetState(e, "crashed"); world.Emit("bossCrash", new Ev { e = e, x = e.x, y = e.y, parried = parried }); }
                    else SetState(e, "rise");
                }
                return;
            }
            if (s == "recover")
            {
                if (A.low) { e.vx *= 0.85; e.vy *= 0.85; } else Home(e, e.x, floor + B.hover + bob, 1, 3);
                Enemies.Physics(e);
                if (e.parried == 2) { e.parried = 0; e.crashFor = B.diveParried; SetState(e, "crashed"); world.Emit("bossCrash", new Ev { e = e, x = e.x, y = e.y, parried = true }); return; }
                if (e.st >= or(A.rec, 30) * fast) { e.atk = null; e.parried = 0; SetState(e, A.low ? "rise" : "idle"); e.cd = B.cd[(int)e.phase - 1]; }
                return;
            }
            SetState(e, "idle"); Enemies.Physics(e);
        }

        static void StartStorm(Enemy e, World world, string k)
        {
            var B = STORM; double fast = e.phase == 2 ? 0.8 : 1;
            if (k == "volley") { double shots = B.volleyBursts[(int)e.phase - 1]; Begin(e, world, k, "standard", JMath.Round(B.volleyWind * fast), a => a.shots = shots); }
            else if (k == "rain") Begin(e, world, k, "unblockable", JMath.Round(B.rainWind * fast));
            else if (k == "dive") Begin(e, world, k, "heavy", JMath.Round(B.diveWind * fast));
            else
            {
                // The sweep: it drops low at one edge of the pad, then fires a laser across it
                double edge = JMath.Abs(e.x - B.padX[0]) < JMath.Abs(e.x - B.padX[1]) ? 0 : 1;
                e.laserHigh = e.phase == 2 ? !e.laserHigh : false;
                e.atk = new EnemyAtk { kind = "sweep", inst = world.NewInstance(), edge = edge, high = e.laserHigh, wind = JMath.Round(B.sweepWind * fast) }; e.lastAtk = "sweep";
                SetState(e, "reposition");
            }
        }
        // Tests and tools: start a named attack now, the same way the boss would choose it
        public static void ForceBossAttack(World world, Enemy e, string kind) { e.cd = 0; if (e.type == "warden") StartWarden(e, world, kind); else StartStorm(e, world, kind); }

        // Shells rained across the pad: one on each player, the rest spread
        static void FireRain(Enemy e, World world)
        {
            var B = STORM; int n = (int)B.rainN[(int)e.phase - 1];
            var targets = world.players.FindAll(Enemies.CanTarget);
            for (int i = 0; i < n; i++)
            {
                var t = i < targets.Count ? targets[i] : null;
                double tx = t != null ? t.x : B.padX[0] + (B.padX[1] - B.padX[0]) * ((i + 0.5) / n) + (JRandom.Next() - 0.5);
                double ty = JMath.Max(Level.GroundBelow(tx, B.floor + 2), B.floor - 6), sx = e.x + (i - n / 2.0) * 0.3, sy = e.y + e.h * 0.2;
                double T = 0.95 + i * 0.12, g = B.rainGravity;
                world.SpawnProjectile(new Projectile { team = "e", owner = e, x = sx, y = sy, vx = (tx - sx) / T, vy = (ty + 0.2 - sy + 0.5 * g * T * T) / T, gravity = g, r = 0.3, dmg = 0, heavy = true, kind = "mortar", ttl = 300, blast = B.rainBlast.Clone() });
                world.Emit("mortarShot", new Ev { e = e, x = tx, y = ty, r = B.rainBlast.r, ticks = JMath.Round(T * 60) });
            }
        }

        // A boss for an encounter: scaled for the number of players, arriving on its intro
        public static Enemy SpawnBoss(World world, string type, double x, double y, Action<Enemy> extra = null)
        {
            double hp = type == "warden" ? WARDEN.hp : STORM.hp; int n = Math.Max(1, world.players.Count);
            var e = Enemies.CreateEnemy(type, x, y, q =>
            {
                extra?.Invoke(q);
                q.boss = true; q.phase = 1; q.invuln = 999; q.staggerCd = 0; q.parried = 0; q.cd = 60; q.facing = -1; q.homeX = x; q.homeY = y;
            });
            e.hp = e.maxHp = JMath.Round(hp * (1 + 0.6 * (n - 1)));
            e.state = "intro"; e.st = 0;
            world.enemies.Add(e);
            world.Emit("bossIntro", new Ev { e = e, name = NameOf(type), title = TitleOf(type) });
            return e;
        }
    }
}
