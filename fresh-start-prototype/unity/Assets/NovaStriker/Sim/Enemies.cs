// Enemy archetypes and behaviour (port of enemies.js). Every attack announces its category through the world's
// telegraph events: 'standard' (parryable), 'heavy' (perfect parry to fully negate), 'unblockable'.
using System;
using System.Collections.Generic;
using static NovaStriker.Sim.Cfg;
using static NovaStriker.Sim.U;

namespace NovaStriker.Sim
{
    public sealed class EnemyType
    {
        public double w, h, hp, poise, speed, armor;
        public bool flinch, light, stationary, noGravity, flier, boss;
    }

    public static partial class Enemies
    {
        public static readonly Dictionary<string, EnemyType> ENEMY_TYPES = new Dictionary<string, EnemyType>
        {
            ["swarmer"] = new EnemyType { w = 0.7, h = 0.8, hp = 3, poise = 18, speed = 5.2, flinch = true, light = true },
            ["shield"] = new EnemyType { w = 0.95, h = 1.85, hp = 8, poise = 55, speed = 2.6, flinch = true, light = true },
            ["sniper"] = new EnemyType { w = 0.8, h = 1.7, hp = 4, poise = 25, speed = 0, flinch = true, light = true, stationary = true },
            ["brute"] = new EnemyType { w = 1.5, h = 2.5, hp = 45, poise = 100, speed = 2.1, flinch = false, light = false, armor = 3 },
            ["post"] = new EnemyType { w = 0.9, h = 2.1, hp = double.PositiveInfinity, poise = 60, speed = 0, light = false, stationary = true },
            ["turret"] = new EnemyType { w = 0.8, h = 0.8, hp = double.PositiveInfinity, poise = 999, speed = 0, light = false, stationary = true, noGravity = true },
            ["drone"] = new EnemyType { w = 0.8, h = 0.6, hp = 3, poise = 16, speed = 5, flinch = true, light = true, flier = true },
            ["mortar"] = new EnemyType { w = 1.1, h = 1.2, hp = 8, poise = 40, speed = 0, light = false, stationary = true },
            ["charger"] = new EnemyType { w = 1.2, h = 1.5, hp = 12, poise = 60, speed = 3.2, light = false, armor = 1 },
            // the bosses (bosses.js registers them here)
            ["warden"] = new EnemyType { w = 2.2, h = 3.4, hp = Bosses.WARDEN.hp, poise = Bosses.WARDEN.poise, speed = Bosses.WARDEN.walk, light = false, armor = Bosses.WARDEN.armor, boss = true },
            ["stormcaller"] = new EnemyType { w = 3.0, h = 1.5, hp = Bosses.STORM.hp, poise = Bosses.STORM.poise, speed = Bosses.STORM.speed, light = false, flier = true, boss = true },
        };
        public static class MORTAR { public const double range = 24, minRange = 3, wind = 36, gravity = 26, cd = 170; public static readonly Blast blast = new Blast { r = 1.9, dmg = 14 }; }
        public static class CHARGER { public const double wind = 34, speed = 15, maxTicks = 48, dmg = 16, daze = 80, cd = 100, minRange = 3, maxRange = 13; }

        public static int nextId = 1;
        public static Enemy CreateEnemy(string type, double x, double y, Action<Enemy> extra = null)
        {
            var T = ENEMY_TYPES[type];
            var e = new Enemy
            {
                kind = "enemy", id = nextId++, type = type, x = x, y = y, vx = 0, vy = 0, w = T.w, h = T.h, prevX = x, prevY = y,
                facing = -1, shieldDir = -1, onGround = false, hp = T.hp, maxHp = T.hp, poise = 0, poiseMax = T.poise,
                armor = T.armor, armorMax = T.armor,
                state = "idle", st = 0, atk = null, token = null, target = null, cd = 30 + JMath.Floor(JRandom.Next() * 40),
                hitstop = 0, flash = 0, dead = false, deathT = 0, tagged = 0, stun = 0, slamCd = 120, cycle = 0,
                aimX = 0, aimY = 0, label = "", light = T.light, flier = T.flier, boss = T.boss, homeX = x, homeY = y,
            };
            extra?.Invoke(e);
            return e;
        }

        // Enemies cannot see a player hidden by Veil
        public static bool CanTarget(Player p) => p.state != "downed" && p.state != "dead" && !p.veiled;

        public static Player NearestPlayer(Enemy e, World world, double maxD = 40)
        {
            if (e.tauntT > 0 && e.taunter != null && CanTarget(e.taunter)) return e.taunter;
            Player best = null, flare = null; double bd = maxD, fd = JMath.Min(maxD, SCARF.flareRange);
            foreach (var p in world.players.Live())
            {
                if (!CanTarget(p)) continue;
                double d = JMath.Hypot(p.x - e.x, (p.y + 0.9) - (e.y + e.h / 2));
                if (d < bd) { bd = d; best = p; }
                if (p.@char == "echo" && p.scarfMode == "flare" && d < fd) { fd = d; flare = p; }
            }
            return flare ?? best;
        }

        static void SetState(Enemy e, string s) { e.state = s; e.st = 0; }
        static void ReleaseToken(Enemy e, World world) { if (e.token != null) world.director.Release(e); }

        public static void Physics(Enemy e)
        {
            var T = ENEMY_TYPES[e.type];
            if (T.flier) { e.vx *= 0.93; e.vy *= 0.93; Level.MoveBody(e, DT); return; }   // drones drift to a stop
            if (T.noGravity) return;
            e.vy -= GRAVITY * DT;
            if (e.vy < -MAX_FALL) e.vy = -MAX_FALL;
            Level.MoveBody(e, DT);
        }

        public static void UpdateEnemy(Enemy e, World world)
        {
            e.prevX = e.x; e.prevY = e.y;
            if (e.flash > 0) e.flash--;
            if (e.dropT > 0) e.dropT--;
            if (e.tagged > 0) e.tagged--;
            if (e.tauntT > 0) e.tauntT--;
            if (e.dead)
            {
                e.deathT++;
                if (e.boss && !e.onGround) { e.vy = JMath.Max(e.vy - GRAVITY * DT, -14); e.vx *= 0.95; Level.MoveBody(e, DT); }   // a downed gunship falls onto the pad
                return;
            }
            if (e.y < Level.KillYAt(e.x))
            {   // fell out of the level
                e.dead = true; e.deathT = 0; e.hp = 0; world.director.Release(e);
                world.Emit("kill", new Ev { x = e.x, y = e.y, e = e, owner = null });
                return;
            }
            if (e.shockT > 0) e.shockT--;
            if (e.wellT > 0) e.wellT--;
            // Slowed by Nova's perfect dodge: it only acts every other tick
            if (e.slowT > 0) { e.slowT--; if (e.slowT % 2 != 0) return; }
            if (e.hitstop > 0) { e.hitstop--; return; }
            e.st++;
            if (e.cd > 0) e.cd--;
            if (e.slamCd > 0) e.slamCd--;

            // Shared interrupt states
            if (e.state == "stagger" || e.state == "hitstun")
            {
                if (e.onGround) e.vx *= 0.85;
                Physics(e);
                if (e.st >= e.stun) { SetState(e, "idle"); e.dizzy = false; }
                return;
            }
            e.dizzy = false;
            if (e.state == "launched")
            {
                Physics(e);
                if ((e.onGround || e.flier) && e.st > 6) { e.stun = 20; SetState(e, "hitstun"); }
                return;
            }
            if (e.state == "caught")
            {
                var p = e.catcher;
                bool leashed = p != null && p.leash != null && p.leash.e == e;
                if (e.st <= 8 && p != null)
                {
                    double tx = p.x + or(e.catchSide, p.facing) * (p.w / 2 + e.w / 2 + 0.3), ty = p.y;
                    e.vx = (tx - e.x) * 14; e.vy = (ty - e.y) * 14;
                    Level.MoveBody(e, DT);
                }
                else if (leashed)
                {
                    // Reeled in on the tether: dragged along, never more than the leash length away
                    double dx = e.x - p.x, d = JMath.Abs(dx), L = HUNTER.leashLen;
                    if (d > L) e.vx = p.vx + (p.x + JMath.Sign(dx) * L - e.x) * 20; else e.vx *= 0.8;
                    Physics(e);
                    e.st = JMath.Min(e.st, 12);
                }
                else { e.vx *= 0.8; Physics(e); }
                if (e.st >= 28) SetState(e, "idle");
                return;
            }
            if (e.state == "snared")
            {
                e.vx = 0; Physics(e);
                if (e.st >= e.stun) SetState(e, "idle");
                return;
            }
            if (e.state == "plowed")
            {
                // Scooped up by RAM's charge: he carries it; if he lets go without throwing it, it drops
                var p = e.plowBy;
                if (p == null || (p.state != "rush" && p.state != "ult") || e.st > 150) { e.plowBy = null; SetState(e, "hitstun"); e.stun = 16; Physics(e); }
                return;
            }

            var before = e.target;
            Behave(e, world);
            if (before != null && before.veiled && e.target != before) world.Emit("lostTrack", new Ev { e = e, p = before });
        }

        static void Behave(Enemy e, World world)
        {
            switch (e.type)
            {
                case "swarmer": Swarmer(e, world); break;
                case "shield": Shield(e, world); break;
                case "sniper": Sniper(e, world); break;
                case "brute": Brute(e, world); break;
                case "post": Post(e, world); break;
                case "drone": Drone(e, world); break;
                case "mortar": Mortar(e, world); break;
                case "charger": Charger(e, world); break;
                case "turret": Turret(e, world); break;
                case "warden": Bosses.Warden(e, world); break;
                case "stormcaller": Bosses.Stormcaller(e, world); break;
            }
        }

        static void WalkToward(Enemy e, Actor target, double speed, double stopDist)
        {
            double dx = target.x - e.x;
            e.facing = dx >= 0 ? 1 : -1;
            if (JMath.Abs(dx) > stopDist) e.vx = e.facing * speed;
            else e.vx *= 0.7;
        }

        static void Swarmer(Enemy e, World world)
        {
            var p = NearestPlayer(e, world, 18);
            e.target = p;
            if (e.state == "idle" || e.state == "approach")
            {
                if (p == null) { e.vx *= 0.8; Physics(e); return; }
                WalkToward(e, p, ENEMY_TYPES["swarmer"].speed, 1.7);
                bool close = JMath.Abs(p.x - e.x) < 2.1 && JMath.Abs(p.y - e.y) < 1.4;
                if (close && e.cd == 0 && world.director.Request(e, "melee")) { SetState(e, "windup"); e.vx = 0; world.Telegraph(e, "standard", 20); }
                else e.state = "approach";
            }
            else if (e.state == "windup")
            {
                e.vx *= 0.7;
                if (p != null) e.facing = p.x >= e.x ? 1 : -1;
                if (e.st >= 20) { SetState(e, "attack"); e.atk = new EnemyAtk { inst = world.NewInstance() }; }
            }
            else if (e.state == "attack")
            {
                e.vx = e.facing * 10;
                double x0 = e.facing > 0 ? e.x : e.x - 1.1;
                world.SpawnHitbox(new Hit { owner = e, team = "e", x0 = x0, x1 = x0 + 1.1, y0 = e.y, y1 = e.y + 0.9, dmg = 8, kb = new[] { e.facing * 5, 3.0 }, instance = e.atk.inst, cat = "standard" });
                if (e.st >= 10) SetState(e, "recover");
            }
            else if (e.state == "recover")
            {
                e.vx *= 0.8;
                if (e.st >= 26) { ReleaseToken(e, world); e.cd = 40 + JMath.Floor(JRandom.Next() * 40); SetState(e, "idle"); }
            }
            Physics(e);
        }

        static void Shield(Enemy e, World world)
        {
            var p = NearestPlayer(e, world, 22);
            e.target = p;
            // The shield turns slowly, so flanking and attacking from above work
            if (p != null && e.st % 36 == 0 && e.state != "attack") e.shieldDir = p.x >= e.x ? 1 : -1;
            if (e.state == "idle" || e.state == "approach")
            {
                if (p == null) { e.vx *= 0.8; Physics(e); return; }
                double dx = p.x - e.x;
                e.facing = e.shieldDir;
                if (JMath.Abs(dx) > 2.2 && JMath.Sign(dx) == e.shieldDir) e.vx = e.shieldDir * ENEMY_TYPES["shield"].speed;
                else e.vx *= 0.7;
                bool close = JMath.Abs(dx) < 2.5 && JMath.Abs(p.y - e.y) < 1.6 && JMath.Sign(dx) == e.shieldDir;
                if (close && e.cd == 0 && world.director.Request(e, "melee")) { SetState(e, "windup"); world.Telegraph(e, "standard", 22); }
            }
            else if (e.state == "windup")
            {
                e.vx *= 0.6;
                if (e.st >= 22) { SetState(e, "attack"); e.atk = new EnemyAtk { inst = world.NewInstance() }; }
            }
            else if (e.state == "attack")
            {
                e.vx = e.shieldDir * 8;
                double x0 = e.shieldDir > 0 ? e.x : e.x - 1.3;
                world.SpawnHitbox(new Hit { owner = e, team = "e", x0 = x0, x1 = x0 + 1.3, y0 = e.y + 0.2, y1 = e.y + 1.8, dmg = 12, kb = new[] { e.shieldDir * 7, 3.0 }, instance = e.atk.inst, cat = "standard" });
                if (e.st >= 8) SetState(e, "recover");
            }
            else if (e.state == "recover")
            {
                e.vx *= 0.75;
                if (e.st >= 34) { ReleaseToken(e, world); e.cd = 60 + JMath.Floor(JRandom.Next() * 40); SetState(e, "idle"); }
            }
            Physics(e);
        }

        static void Sniper(Enemy e, World world)
        {
            var p = NearestPlayer(e, world, 24);
            if (e.state == "idle")
            {
                e.target = p;
                if (p != null && e.cd == 0)
                {
                    double cx = e.x, cy = e.y + 1.4;
                    if (!Level.SegmentBlocked(cx, cy, p.x, p.y + 1) && world.director.Request(e, "ranged")) { SetState(e, "aim"); world.Telegraph(e, "heavy", 64); }
                }
            }
            else if (e.state == "aim")
            {
                var t = e.target;
                if (t == null || !CanTarget(t)) { ReleaseToken(e, world); e.target = null; SetState(e, "idle"); Physics(e); return; }
                e.aimX = t.x; e.aimY = t.y + 1.0;
                e.facing = t.x >= e.x ? 1 : -1;
                if (e.st >= 48) { SetState(e, "lock"); world.Emit("lock", new Ev { e = e }); }
            }
            else if (e.state == "lock")
            {
                if (e.st >= 16)
                {
                    double sx = e.x + e.facing * 0.5, sy = e.y + 1.4;
                    double dx = e.aimX - sx, dy = e.aimY - sy, d = or(JMath.Hypot(dx, dy), 1);
                    world.SpawnProjectile(new Projectile { team = "e", owner = e, x = sx, y = sy, vx = dx / d * 24, vy = dy / d * 24, r = 0.28, dmg = 18, heavy = true, kind = "heavy", ttl = 90 });
                    world.Emit("enemyShot", new Ev { e = e, heavy = true });
                    SetState(e, "recover");
                }
            }
            else if (e.state == "recover")
            {
                if (e.st >= 30) { ReleaseToken(e, world); e.cd = 140; SetState(e, "idle"); }
            }
            Physics(e);
        }

        static void Brute(Enemy e, World world)
        {
            var p = NearestPlayer(e, world, 30);
            e.target = p;
            if (e.state == "idle" || e.state == "approach")
            {
                if (p == null) { e.vx *= 0.8; Physics(e); return; }
                double dist = JMath.Abs(p.x - e.x);
                WalkToward(e, p, ENEMY_TYPES["brute"].speed, 2.0);
                if (e.cd == 0)
                {
                    if (dist > 3.2 && dist < 8.5 && e.slamCd == 0 && world.director.Request(e, "melee")) { SetState(e, "slamWindup"); e.vx = 0; world.Telegraph(e, "unblockable", 44); }
                    else if (dist < 2.8 && JMath.Abs(p.y - e.y) < 2 && world.director.Request(e, "melee")) { SetState(e, "windup"); e.vx = 0; world.Telegraph(e, "heavy", 26); }
                }
            }
            else if (e.state == "windup")
            {
                e.vx *= 0.6;
                if (e.st >= 26) { SetState(e, "attack"); e.atk = new EnemyAtk { inst = world.NewInstance() }; }
            }
            else if (e.state == "attack")
            {
                double x0 = e.facing > 0 ? e.x + 0.2 : e.x - 2.8;
                world.SpawnHitbox(new Hit { owner = e, team = "e", x0 = x0, x1 = x0 + 2.6, y0 = e.y + 0.3, y1 = e.y + 2.2, dmg = 20, kb = new[] { e.facing * 10, 5.0 }, heavy = true, instance = e.atk.inst, cat = "heavy" });
                if (e.st >= 6) SetState(e, "recover");
            }
            else if (e.state == "slamWindup")
            {
                if (e.st >= 44)
                {
                    world.SpawnShockwave(e, 1, 22); world.SpawnShockwave(e, -1, 22);
                    world.Emit("slam", new Ev { e = e }); SetState(e, "slamRecover");
                }
            }
            else if (e.state == "recover" || e.state == "slamRecover")
            {
                e.vx *= 0.8;
                double len = e.state == "recover" ? 38 : 50;
                if (e.st >= len)
                {
                    ReleaseToken(e, world);
                    if (e.state == "slamRecover") e.slamCd = 300;
                    e.cd = 30 + JMath.Floor(JRandom.Next() * 30); SetState(e, "idle");
                }
            }
            Physics(e);
        }

        static readonly string[] PATTERN = { "standard", "heavy", "unblockable" };
        static void Post(Enemy e, World world)
        {
            var p = NearestPlayer(e, world, 3.6);
            if (e.state == "idle")
            {
                e.label = "";
                if (p != null && e.cd == 0)
                {
                    e.facing = p.x >= e.x ? 1 : -1;
                    string cat = PATTERN[(int)(e.cycle % 3)]; e.cycle++;
                    double wind = cat == "standard" ? 24 : cat == "heavy" ? 30 : 44;
                    e.atk = new EnemyAtk { cat = cat, wind = wind, inst = world.NewInstance() };
                    e.label = cat == "standard" ? "Standard: parry it" : cat == "heavy" ? "Heavy: perfect-parry it" : "Unblockable: jump it";
                    SetState(e, "windup"); world.Telegraph(e, cat, wind);
                }
            }
            else if (e.state == "windup")
            {
                if (e.st >= e.atk.wind)
                {
                    if (e.atk.cat == "unblockable") { world.SpawnShockwave(e, 1, 20, 0.6); world.SpawnShockwave(e, -1, 20, 0.6); world.Emit("slam", new Ev { e = e }); }
                    SetState(e, "attack");
                }
            }
            else if (e.state == "attack")
            {
                if (e.atk.cat != "unblockable")
                {
                    double x0 = e.facing > 0 ? e.x : e.x - 2.0;
                    world.SpawnHitbox(new Hit
                    {
                        owner = e, team = "e", x0 = x0, x1 = x0 + 2.0, y0 = e.y + 0.4, y1 = e.y + 1.9, dmg = e.atk.cat == "heavy" ? 16 : 8,
                        kb = new[] { e.facing * 6, 3.0 }, heavy = e.atk.cat == "heavy", instance = e.atk.inst, cat = e.atk.cat,
                    });
                }
                if (e.st >= 5) SetState(e, "recover");
            }
            else if (e.state == "recover")
            {
                if (e.st >= 48) { e.cd = 20; SetState(e, "idle"); }
            }
            Physics(e);
        }

        // Drone: hovers above and to one side of its target, bobbing, and fires parryable shots down at it
        static void Drone(Enemy e, World world)
        {
            var p = NearestPlayer(e, world, 26);
            e.target = p;
            double sp = ENEMY_TYPES["drone"].speed, bob = JMath.Sin((world.tick + e.id * 37) * 0.05) * 0.5;
            double tx = e.homeX, ty = e.homeY + bob;
            if (p != null) { double side = e.x >= p.x ? 1 : -1; tx = p.x + side * 5.5; ty = p.y + 3.4 + bob; e.facing = p.x >= e.x ? 1 : -1; }
            if (e.state == "idle")
            {
                e.vx = approach(e.vx, clamp((tx - e.x) * 1.6, -sp, sp), 0.35);
                e.vy = approach(e.vy, clamp((ty - e.y) * 1.8, -sp, sp), 0.35);
                if (p != null && e.cd == 0 && JMath.Abs(p.x - e.x) < 11 && !Level.SegmentBlocked(e.x, e.y + 0.3, p.x, p.y + 1) && world.director.Request(e, "ranged"))
                { SetState(e, "windup"); world.Telegraph(e, "standard", 26); }
            }
            else if (e.state == "windup")
            {
                e.vx *= 0.85; e.vy *= 0.85;
                if (e.st >= 26)
                {
                    if (p != null && CanTarget(p))
                    {
                        double sx = e.x + e.facing * 0.4, sy = e.y + 0.25, dx = p.x - sx, dy = p.y + 1 - sy, d = or(JMath.Hypot(dx, dy), 1);
                        world.SpawnProjectile(new Projectile { team = "e", owner = e, x = sx, y = sy, vx = dx / d * 13, vy = dy / d * 13, r = 0.2, dmg = 6, kind = "std", ttl = 110 });
                        world.Emit("enemyShot", new Ev { e = e, heavy = false });
                    }
                    SetState(e, "recover");
                }
            }
            else if (e.state == "recover")
            {
                e.vx *= 0.9; e.vy *= 0.9;
                if (e.st >= 24) { ReleaseToken(e, world); e.cd = 70 + (e.id * 13) % 50; SetState(e, "idle"); }
            }
            else SetState(e, "idle");
            Physics(e);
        }

        // Mortar: stays put and lobs a heavy shell that bursts where a marker shows on the ground
        static void Mortar(Enemy e, World world)
        {
            var p = NearestPlayer(e, world, MORTAR.range);
            if (e.state == "idle")
            {
                e.target = p;
                if (p != null && e.cd == 0 && JMath.Abs(p.x - e.x) > MORTAR.minRange && world.director.Request(e, "ranged"))
                {
                    e.facing = p.x >= e.x ? 1 : -1;
                    SetState(e, "windup"); world.Telegraph(e, "unblockable", MORTAR.wind);
                }
            }
            else if (e.state == "windup")
            {
                var t = e.target;
                if (t != null) e.facing = t.x >= e.x ? 1 : -1;
                if (e.st >= MORTAR.wind)
                {
                    if (t != null && CanTarget(t))
                    {
                        // Aim where the target stands now; flight time grows with distance
                        double sx = e.x + e.facing * 0.5, sy = e.y + 1.35, tx = t.x, ty = JMath.Max(Level.GroundBelow(t.x, t.y + 0.3), t.y - 6);
                        double T = clamp(0.9 + JMath.Abs(tx - sx) / 25, 0.9, 1.5), g = MORTAR.gravity;
                        double vx = (tx - sx) / T, vy = (ty + 0.2 - sy + 0.5 * g * T * T) / T;
                        world.SpawnProjectile(new Projectile { team = "e", owner = e, x = sx, y = sy, vx = vx, vy = vy, gravity = g, r = 0.3, dmg = 0, heavy = true, kind = "mortar", ttl = 300, blast = MORTAR.blast.Clone() });
                        world.Emit("mortarShot", new Ev { e = e, x = tx, y = ty, r = MORTAR.blast.r, ticks = JMath.Round(T * 60) });
                    }
                    SetState(e, "recover");
                }
            }
            else if (e.state == "recover")
            {
                if (e.st >= 40) { ReleaseToken(e, world); e.cd = MORTAR.cd; SetState(e, "idle"); }
            }
            else SetState(e, "idle");
            Physics(e);
        }

        // Charger: an armored rusher. It paws the ground, then charges in a straight line.
        static void Charger(Enemy e, World world)
        {
            var p = NearestPlayer(e, world, 26);
            e.target = p;
            if (e.state == "idle" || e.state == "approach")
            {
                if (p == null) { e.vx *= 0.8; Physics(e); return; }
                double dx = p.x - e.x, dist = JMath.Abs(dx); bool level = JMath.Abs(p.y - e.y) < 1.8;
                e.facing = dx >= 0 ? 1 : -1;
                if (dist < CHARGER.minRange + 1) e.vx = -e.facing * ENEMY_TYPES["charger"].speed;   // backs off to get a run-up
                else if (dist > CHARGER.maxRange - 2) e.vx = e.facing * ENEMY_TYPES["charger"].speed;
                else e.vx *= 0.7;
                e.state = "approach";
                if (e.cd == 0 && level && dist >= CHARGER.minRange && dist <= CHARGER.maxRange && world.director.Request(e, "melee"))
                { SetState(e, "windup"); e.vx = 0; world.Telegraph(e, "heavy", CHARGER.wind); }
            }
            else if (e.state == "windup")
            {
                e.vx *= 0.5;
                if (p != null) e.facing = p.x >= e.x ? 1 : -1;
                if (e.st >= CHARGER.wind) { SetState(e, "charge"); e.atk = new EnemyAtk { inst = world.NewInstance(), x0 = e.x }; world.Emit("chargeStart", new Ev { e = e }); }
            }
            else if (e.state == "charge")
            {
                e.vx = e.facing * CHARGER.speed;
                double x0 = e.facing > 0 ? e.x + 0.1 : e.x - 1.4;
                world.SpawnHitbox(new Hit { owner = e, team = "e", x0 = x0, x1 = x0 + 1.3, y0 = e.y + 0.1, y1 = e.y + 1.4, dmg = CHARGER.dmg, heavy = true, kb = new[] { e.facing * 12, 5.0 }, instance = e.atk.inst, cat = "heavy" });
                if (truthy(e.hitWall)) { SetState(e, "dazed"); e.vx = -e.facing * 3; world.Emit("chargeCrash", new Ev { e = e }); }
                else if (e.st >= CHARGER.maxTicks) SetState(e, "recover");
            }
            else if (e.state == "dazed")
            {
                e.vx *= 0.85;
                if (e.st >= CHARGER.daze) { ReleaseToken(e, world); e.cd = CHARGER.cd; SetState(e, "idle"); }
            }
            else if (e.state == "recover")
            {
                e.vx *= 0.85;
                if (e.st >= 30) { ReleaseToken(e, world); e.cd = CHARGER.cd; SetState(e, "idle"); }
            }
            Physics(e);
        }

        static void Turret(Enemy e, World world)
        {
            Player target = null; double bd = 13;
            foreach (var p in world.players.Live())
            {
                if (!CanTarget(p) || p.x > e.x + 0.5 || p.x < 44) continue;
                double d = JMath.Hypot(p.x - e.x, p.y + 1 - e.y);
                if (d < bd) { bd = d; target = p; }
            }
            if (e.state == "idle")
            {
                if (target != null && e.cd == 0)
                {
                    bool heavy = e.cycle % 3 == 2; e.cycle++;
                    e.atk = new EnemyAtk { heavy = heavy }; e.target = target;
                    SetState(e, "windup"); world.Telegraph(e, heavy ? "heavy" : "standard", 24);
                }
            }
            else if (e.state == "windup")
            {
                if (e.st >= 24)
                {
                    var t = e.target;
                    if (t != null && CanTarget(t))
                    {
                        double dx = t.x - e.x, dy = t.y + 1.0 - e.y, d = or(JMath.Hypot(dx, dy), 1);
                        double sp = e.atk.heavy ? 14 : 12;
                        world.SpawnProjectile(new Projectile
                        {
                            team = "e", owner = e, x = e.x - 0.5, y = e.y + 0.4, vx = dx / d * sp, vy = dy / d * sp, r = e.atk.heavy ? 0.28 : 0.2,
                            dmg = e.atk.heavy ? 14 : 6, heavy = e.atk.heavy, kind = e.atk.heavy ? "heavy" : "std", ttl = 120,
                        });
                        world.Emit("enemyShot", new Ev { e = e, heavy = e.atk.heavy });
                    }
                    e.cd = 70; SetState(e, "idle");
                }
            }
        }
    }
}
