// World, part two (port of world.js): players joining and leaving, RAM's and Fix's kits, power-ups, breakable
// pieces, ultimates, the tick, the camera and the encounters.
using System;
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
        // ---- Players ----
        public Player AddPlayer(string device, string charId)
        {
            var used = new HashSet<int>(players.Select(q => q.slot));
            int slot = 0; while (used.Contains(slot)) slot++;
            if (slot > 3) return null;
            var act = ActivePlayers(); var anchor = act.Count > 0 ? act[0] : null;
            var cp = Level.CHECKPOINTS[checkpoint];
            double x = anchor != null ? anchor.x - 0.8 : cp.x + slot * 0.8, y = anchor != null ? anchor.lastSafeY : cp.y;
            var p = CreatePlayer(slot, device, charId, x, y);
            p.mercy = 120;
            players.Add(p); StableSort.Sort(players, (a, b) => a.slot - b.slot);
            Emit("join", new Ev { p = p });
            return p;
        }
        public void RemovePlayer(int slot)
        {
            var gone = players.Find(q => q.slot == slot);
            if (gone != null) LeaveRole(gone);
            players.RemoveAll(q => q.slot == slot);
            wells.RemoveAll(w => !players.Contains(w.owner));
            foreach (var p in players.ToArray()) if (p.link != null && p.link.q == gone) EndLink(p, "gone");
            foreach (var b in barriers.Live()) if (b.owner == gone) b.ttl = 0;
            if (ultCast != null) { ultCast.members.RemoveAll(m => !players.Contains(m)); if (ultCast.members.Count == 0) ultCast = null; }
            Emit("leave", new Ev { slot = slot });
        }
        public void SwapCharacter(Player p, string charId)
        {
            if (ultCast != null || p.state == "ult") return;   // not in the middle of an ultimate
            if (p.thrusting) Emit("thrustOff", new Ev { p = p });
            if (p.aegis != null) EndAegis(p, "swap");
            if (p.beam != null) EndBeam(p, "swap");
            LeaveRole(p);
            SetCharacter(p, charId);
            Emit("swap", new Ev { p = p });
        }

        // What a RAM or a Fix leaves behind when they swap out or leave
        void LeaveRole(Player p)
        {
            if (p.rush != null) EndRush(p, "cancel");
            if (p.link != null) EndLink(p, "swap");
            p.leap = null;
            foreach (var b in barriers.Live()) if (b.kind == "rampart" && b.owner == p) b.ttl = 0;
            foreach (var g in gadgets.Live()) if (g.owner == p && !g.dead) { g.dead = true; Emit("gadgetEnd", new Ev { g = g, why = "gone" }); }
        }
        public void DownPlayer(Player p)
        {
            if (p.lockT != null) SetLock(p, null, "downed");
            if (p.aegis != null) EndAegis(p, "down");
            if (p.beam != null) EndBeam(p, "down");
            if (p.rush != null) EndRush(p, "cancel");
            if (p.link != null) EndLink(p, "down");
            p.leap = null; p.patch = null; p.tossArmed = false; p.fixRevive = false; p.reviveBy = null; p.reviveGain = 0;
            p.hp = 0; p.chargeT = 0; p.meleeCharged = false; p.dash = null; p.lash = null; p.zip = null; p.rifleT = 0; p.dashChargeT = 0;
            p.dodge = null; p.pound = null; p.burstT = 0; p.subArmed = false;
            p.veiled = false; p.veilCharge = 0; p.ambushT = 0;
            p.state = "downed"; p.st = 0; p.revive = 0; p.autoRevive = 0;
            if (players.Count == 1)
            {
                p.downedT = 9999;
                if (p.secondWind) { p.secondWind = false; p.autoRevive = 70; Emit("downed", new Ev { p = p, secondWind = true }); }
                else { Emit("downed", new Ev { p = p }); StartWipe(); }
                return;
            }
            p.downedT = 600;
            Emit("downed", new Ev { p = p });
            if (ActivePlayers().Count == 0) StartWipe();
        }
        public void BleedOut(Player p)
        {
            p.state = "dead"; p.respawnT = 360;
            Emit("bleedOut", new Ev { p = p });
            if (ActivePlayers().Count == 0) StartWipe();
        }
        public void RevivePlayer(Player p, Player by, double frac)
        {
            p.state = "normal"; p.st = 0; p.hp = JMath.Round(p.maxHp * frac); p.mercy = 90; p.revive = 0;
            p.fixRevive = false; p.reviveBy = null; p.reviveGain = 0;
            p.h = CHARS[p.@char].height; p.crouch = !Level.HasHeadroom(p.x, p.y, p.w, p.h);
            Emit("revived", new Ev { p = p, by = by });
            if (by != null) { Bark(by, "revive", 1, true); Schedule(40, () => Bark(p, "revived", 1, true)); }
        }
        void StartWipe() { if (wipeT <= 0) { wipeT = 100; Emit("wipe", new Ev()); } }

        public void ResetToCheckpoint()
        {
            var cp = Level.CHECKPOINTS[checkpoint];
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                p.x = cp.x + i * 0.8; p.y = cp.y; p.prevX = p.x; p.prevY = p.y; p.vx = 0; p.vy = 0;
                p.hp = p.maxHp; p.strain = 0; p.state = "normal"; p.st = 0; p.secondWind = true; p.mercy = 60;
                p.h = CHARS[p.@char].height; p.lastSafeX = p.x; p.lastSafeY = p.y; p.resolve = 0; p.chargeT = 0;
                p.veiled = false; p.veilCharge = 0; p.veilBreakT = 0; p.ambushT = 0; p.focus = 0;
                p.aegis = null; p.aegisCd = 0; p.overcharge = 0; p.beam = null;
                p.dodge = null; p.pound = null; p.burstT = 0; p.subArmed = false; p.ultRun = null; p.lockSuspend = false;
                p.rush = null; p.link = null; p.leap = null; p.patch = null; p.tossArmed = false; p.integrity = RAM.guard.integrity; p.guardBroken = false; p.kinetic = 0;
                p.lane = p.laneTo = p.laneFrom = p.laneT = 0;
                p.plate = 0; p.overclockT = 0; p.tuneT = 0; p.braceT = 0; p.scrap = JMath.Max(p.scrap, FIX.scrap.start); p.fixRevive = false; p.reviveGain = 0;
            }
            projectiles = new List<Projectile>(); shockwaves = new List<Shockwave>(); barriers = new List<Barrier>(); snares = new List<Snare>(); wells = new List<Well>(); ultCast = null;
            gadgets = new List<Gadget>(); pickups = new List<Pickup>();
            Level.RestoreBoxes(); SpawnLevelPickups();
            foreach (var e in enemies.Live()) if (e.state == "plowed") { e.state = "idle"; e.plowBy = null; }
            foreach (var p in players.Live()) p.leash = null;
            if (arena.state != "cleared") ResetArena();
            if (towerSpawned && enemies.Exists(e => e.zone == "tower" && !e.dead))
            {
                enemies.RemoveAll(e => e.zone == "tower"); towerSpawned = false;
            }
            // Encounters that were not finished start over (and their gates open)
            foreach (var S in encounters.Live())
            {
                if (S.state == "cleared") continue;
                enemies.RemoveAll(e => e.enc == S.def.id);
                S.state = "idle"; S.wave = 0;
                if (S.def.gates != null) foreach (var g in S.def.gates) Level.GATES[g] = false;
            }
            director.Reset();
            Emit("respawnAll", new Ev());
        }

        public void Teleport(string zoneId)
        {
            var z = Array.Find(Level.ZONES, q => q.id == zoneId); if (z == null) return;
            checkpoint = Array.FindIndex(Level.CHECKPOINTS, c => c.x == z.spawnX && c.y == z.spawnY);
            if (checkpoint < 0) checkpoint = 0;
            if (zoneId == "arena") arena.state = "idle";
            wipeT = 0;
            ResetToCheckpoint();
            Emit("banner", new Ev { text = z.name, sub = "Zone loaded" });
        }

        void ResetArena()
        {
            bool boss = arena.state == "boss" || arena.state == "bossReady";
            enemies.RemoveAll(e => e.zone == "arena");
            Level.GATES["L"] = false; Level.GATES["R"] = false;
            arena = new ArenaState { state = boss ? "bossReady" : "idle" };   // a wipe in the boss fight comes back to the boss
        }

        // The Concourse Lock's last wave: the Lockwarden drops in
        void StartWarden()
        {
            arena.state = "boss";
            Bosses.SpawnBoss(this, "warden", 87, 12, q => q.zone = "arena");   // drops in beside the dais, not onto it
            Emit("banner", new Ev { text = Bosses.WARDEN.name, sub = Bosses.WARDEN.title });
        }

        // Straight to a boss fight (pause menu): the arena's boss, or the beacon's with the relay already won
        public void BossRush(string id)
        {
            if (id == "warden")
            {
                Teleport("arena"); arena.state = "bossReady";
                foreach (var p in players.Live()) { p.x = 64.5 + p.slot * 0.8; p.prevX = p.x; }
                return;
            }
            foreach (var S in encounters.Live()) { S.state = S.def.boss != null ? "idle" : "cleared"; if (S.def.gates != null) foreach (var g in S.def.gates) Level.GATES[g] = false; }
            enemies.RemoveAll(e => e.zone == "skyline");
            checkpoint = Array.FindIndex(Level.CHECKPOINTS, c => c.x == 302); wipeT = 0;
            ResetToCheckpoint();
            Emit("banner", new Ev { text = "Skyline Relay", sub = "The beacon pad" });
        }

        // ---- Nova: the perfect dodge ----
        public void PerfectDodge(Player p)
        {
            var c = Chest(p);
            p.dodge.perfect = true;
            foreach (var e in enemies.Live()) if (!e.dead && JMath.Hypot(e.x - c.x, e.y + e.h / 2 - c.y) < DODGE.slowRange + e.w / 2) e.slowT = e.boss ? (int)DODGE.slowTicks >> 1 : DODGE.slowTicks;
            foreach (var pr in projectiles.Live()) if (pr.team == "e" && !pr.dead && JMath.Hypot(pr.x - c.x, pr.y - c.y) < DODGE.slowRange) pr.slowT = DODGE.slowTicks;
            p.overcharge = JMath.Min(AEGIS.over.max, p.overcharge + DODGE.over); p.overT = AEGIS.over.hold;
            GainUlt(p, ULT.gain.perfect, this);
            Emit("perfectDodge", new Ev { p = p, x = c.x, y = c.y });
            Bark(p, "perfect", 0.3);
        }

        // ---- RAM ----
        // The cannon on his shoulder: a heavy slug on a tap (level 0), or a Breach Shot (levels 1-3)
        public void FireSlug(Player p, int level)
        {
            var S = RAM.cannon.levels[level]; var c = Chest(p); double ax = p.aimX, ay = p.aimY;
            double x = c.x + ax * 0.95, y = c.y + 0.3 + ay * 0.95;
            SpawnProjectile(new Projectile
            {
                team = "p", owner = p, x = x, y = y, vx = ax * S.speed, vy = ay * S.speed, ttl = S.ttl, r = S.r, dmg = S.dmg, poise = S.poise, kbs = S.kb, kbY = 2,
                pierce = truthy(S.pierce), pierceLeft = S.pierce, armorBreak = S.armorBreak, intercept = true, interceptHeavy = level >= 1, level = level,
                kind = level != 0 ? "breach" : "slug", endBlast = S.blast,
            });
            p.shootT = 12;
            Emit("shot", new Ev { p = p, level = level, x = x, y = y, ax = ax, ay = ay, cannon = true });
        }

        // Which way the Rampart covers: a point is in front of it
        public bool GuardFaces(Player p, double x, double y) { var c = Chest(p); return (x - c.x) * p.guardDir.x + (y - c.y) * p.guardDir.y > -0.25; }
        // The guarding RAM whose shield a shot crossed between two points, coming from in front of it, if any
        public Player RampartCross(double ox, double oy, double x, double y, double r)
        {
            foreach (var p in players.Live())
            {
                if (p.@char != "ram" || p.state != "guard" || p.guardBroken) continue;
                var c = Chest(p); double nx = p.guardDir.x, ny = p.guardDir.y, bx = c.x + nx * RAM.guard.reach, by = c.y + ny * RAM.guard.reach;
                double s0 = (ox - bx) * nx + (oy - by) * ny, s1 = (x - bx) * nx + (y - by) * ny;
                if (s0 < -r || s1 > r) continue;
                double t = s0 - s1 > 1e-6 ? JMath.Max(0, JMath.Min(1, s0 / (s0 - s1))) : 0;
                double cx = ox + (x - ox) * t, cy = oy + (y - oy) * t;
                if (JMath.Abs((cx - bx) * -ny + (cy - by) * nx) <= RAM.guard.half + r) return p;
            }
            return null;
        }
        // A shot stopped by the Rampart: absorbed, or on a Perfect Guard sent straight back as his
        public string BlockShot(Player p, Projectile pr)
        {
            bool perfect = p.guardT <= RAM.guard.perfect;
            if (perfect && pr.blast == null)
            {
                var src = pr.owner is Enemy oe && !oe.dead ? oe : null; double sp = JMath.Hypot(pr.vx, pr.vy) * RAM.guard.reflect;
                double vx = -pr.vx * RAM.guard.reflect, vy = -pr.vy * RAM.guard.reflect;
                if (src != null) { double dx = src.x - pr.x, dy = src.y + src.h * 0.6 - pr.y, m = or(JMath.Hypot(dx, dy), 1); vx = dx / m * sp; vy = dy / m * sp; }
                pr.team = "p"; pr.owner = p; pr.vx = vx; pr.vy = vy; pr.dmg = JMath.Max(3, pr.dmg * 0.6) * (pr.heavy ? 1.6 : 1); pr.poise = pr.heavy ? 45 : 22; pr.kbs = 6;
                pr.hitSet = new HashSet<object>(); pr.deflected = true; pr.reflected = true; pr.intercept = false; pr.heavy = false; pr.homing = false; pr.ttl = JMath.Max(pr.ttl, 120); pr.gravity = 0;
                GuardResult(p, 0, true, pr.x, pr.y, false);
                return "reflect";
            }
            pr.dead = true;
            if (pr.blast != null) Emit("enemyBlast", new Ev { x = pr.x, y = pr.y, r = pr.blast.r * 0.6 });
            GuardResult(p, (pr.blast != null ? pr.blast.dmg : pr.dmg) * Diff.dmg, perfect, pr.x, pr.y, pr.heavy || pr.blast != null);
            return "block";
        }
        // A blocked hit: the Integrity it costs, the Kinetic it stores, ultimate charge; a broken shield
        public void GuardResult(Player p, double dmg, bool perfect, double x, double y, bool heavy)
        {
            double rate = BoostRate(p);
            p.blockT = 0;
            if (perfect)
            {
                p.kinetic = JMath.Min(100, p.kinetic + RAM.guard.perfectKinetic * rate);
                GainUlt(p, ULT.gain.perfect, this);
                Emit("perfectGuard", new Ev { p = p, x = x, y = y, heavy = heavy });
                Bark(p, "perfect_guard", 0.3);
                return;
            }
            p.integrity -= dmg;
            p.kinetic = JMath.Min(100, p.kinetic + dmg * RAM.guard.kinetic * rate);
            GainUlt(p, dmg * ULT.gain.blocked, this);
            Emit("guardBlock", new Ev { p = p, x = x, y = y, dmg = dmg, heavy = heavy, frac = JMath.Max(0, p.integrity / RAM.guard.integrity) });
            if (heavy && p.onGround) p.vx = -p.facing * 3.5;   // a heavy blow shoves him back a step
            if (p.integrity <= 0)
            {
                p.integrity = 0; p.guardBroken = true; p.guardOffT = 0;
                p.state = "hitstun"; p.st = 0; p.stun = RAM.guard.brokenStun; p.vx = -p.facing * 4; p.vy = 2;
                Emit("rampartBreak", new Ev { p = p, x = x, y = y });
            }
        }

        // Kinetic Release: the Rampart dumps its stored Kinetic as a cone of force along the guard
        public void KineticRelease(Player p)
        {
            double k = JMath.Min(1, p.kinetic / 100); var c = Chest(p); double nx = p.guardDir.x, ny = p.guardDir.y;
            double At(double[] a) => a[0] + (a[1] - a[0]) * k;
            double r = At(RAM.release.r), dmg = At(RAM.release.dmg), poise = At(RAM.release.poise), kb = At(RAM.release.kb), cos = JMath.Cos(RAM.release.cone);
            double ox = c.x + nx * 0.6, oy = c.y + ny * 0.6;
            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                if (e.dead) continue;
                var hb = Hurtbox(e); double qx = JMath.Max(hb.x0, JMath.Min(ox, hb.x1)), qy = JMath.Max(hb.y0, JMath.Min(oy, hb.y1));
                double dx = qx - ox, dy = qy - oy, d = JMath.Hypot(dx, dy);
                if (d > r || (d > 0.9 && (dx * nx + dy * ny) / d < cos)) continue;
                HitEnemy(this, e, new Hit { owner = p, dmg = dmg, poise = poise, kb = new[] { or(sign(e.x - p.x), p.facing) * kb, 4 + 4 * k }, armorBreak = k >= 0.5, heavy = true, ram = true, ramKnock = true }, "pulse");
            }
            foreach (var pr in projectiles.Live())
            {
                if (pr.team != "e" || pr.dead) continue;
                double dx = pr.x - ox, dy = pr.y - oy, d = or(JMath.Hypot(dx, dy), 1e-3);
                if (d < r && (dx * nx + dy * ny) / d > cos) { pr.dead = true; Emit("erase", new Ev { x = pr.x, y = pr.y }); }
            }
            p.kinetic = 0;
            Emit("kineticRelease", new Ev { p = p, x = ox, y = oy, nx = nx, ny = ny, r = r, k = k, cone = RAM.release.cone });
        }

        static readonly HashSet<string> ROOTED = new HashSet<string> { "post", "turret", "sniper", "mortar" };
        // The Ram Charge, after he has moved this tick: what is in front of him is scooped up and carried along
        void RamPlow(Player p)
        {
            var r = p.rush; if (r == null) return;
            int L = (int)r.level; double dir = r.dx, front = p.x + dir * p.w / 2;
            // A breakable piece in the way is smashed; he carries on through what breaks
            foreach (var hy in new[] { 0.4, p.h * 0.5, p.h - 0.4 })
            {
                var bk = Level.BreakableAt(front + dir * 0.2, p.y + hy, 0.05);
                if (bk != null && DamageBox(bk, 25 + 30 * L, front + dir * 0.2, p.y + hy, p)) p.hitWall = 0;
            }
            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                if (e.dead || r.hit.Contains(e) || e.state == "plowed") continue;
                double ax0 = dir > 0 ? front - 0.3 : front - RAM.rush.reach, ax1 = dir > 0 ? front + RAM.rush.reach : front + 0.3;
                if (e.x + e.w / 2 < ax0 || e.x - e.w / 2 > ax1 || e.y > p.y + p.h - 0.1 || e.y + e.h < p.y + 0.15) continue;
                r.hit.Add(e);
                bool rooted = e.boss || ROOTED.Contains(e.type), heavy = !e.light;
                if (rooted || (heavy && L < RAM.rush.heavyFrom))
                {
                    HitEnemy(this, e, new Hit { owner = p, dmg = RAM.rush.bonk.dmg[L], poise = RAM.rush.bonk.poise[L], kb = new[] { dir * 6, 2.0 }, armorBreak = L >= 2, heavy = true, ram = true, ramKnock = true }, "pulse");
                    EndRush(p, "bonk", e); p.state = "normal"; p.st = 0;
                    return;
                }
                HitEnemy(this, e, new Hit { owner = p, dmg = RAM.rush.catchDmg[L], poise = RAM.rush.poise[L], kb = new[] { dir * 3, 0.0 }, armorBreak = L >= 2, ram = true }, "pulse");
                if (e.dead) continue;
                director.Release(e);
                e.state = "plowed"; e.st = 0; e.plowBy = p; r.carried.Add(e);
                if (r.carried.Count == 1) p.hitstop = JMath.Max(p.hitstop, 2);
                Emit("plowCatch", new Ev { p = p, e = e, level = L });
            }
            CarryPile(p, r.carried, dir);
            bool jam = r.carried.Exists(e => !e.dead && !Level.HasHeadroom(e.x, e.y + 0.05, e.w, e.h - 0.1));
            if (truthy(p.hitWall) || jam) { EndRush(p, "wall"); p.state = "normal"; p.st = 0; p.vx = 0; }
        }
        // Carried enemies are stacked in front of him, at his feet
        void CarryPile(Player p, List<Enemy> pile, double dir)
        {
            double off = p.x + dir * p.w / 2;
            foreach (var e in pile.Live())
            {
                if (e.dead) continue;
                e.prevX = e.x; e.prevY = e.y;
                e.x = off + dir * (e.w / 2 + 0.06); off += dir * (e.w + 0.06);
                e.y = p.y; e.vx = p.vx; e.vy = 0;
            }
        }
        // The charge ends: at a wall the pile is slammed into it; otherwise it is thrown on ahead of him
        public void EndRush(Player p, string why, Enemy bonked = null)
        {
            var r = p.rush; if (r == null) return;
            int L = (int)r.level; double dir = r.dx;
            var pile = r.carried.FindAll(e => !e.dead && e.state == "plowed");
            if (why == "wall" && pile.Count > 0)
            {
                // the pile can't stay inside the wall: back him off until it fits
                for (int i = 0; i < 24 && pile.Exists(e => !Level.HasHeadroom(e.x, e.y + 0.05, e.w, e.h - 0.1)); i++) { p.x -= dir * 0.1; CarryPile(p, pile, dir); }
            }
            foreach (var e in pile.Live())
            {
                e.plowBy = null;
                if (why == "wall")
                {
                    HitEnemy(this, e, new Hit { owner = p, dmg = RAM.rush.splat.dmg[L], poise = RAM.rush.splat.poise[L], kb = new[] { -dir * 2, 2.0 }, armorBreak = true, heavy = true, ram = true }, "pulse");
                    if (e.dead) continue;
                    director.Release(e); e.state = "stagger"; e.st = 0; e.stun = RAM.rush.splat.stun; e.poise = 0; e.vx = -dir * 2;
                    Emit("stagger", new Ev { x = e.x, y = e.y + e.h * 0.6, e = e });
                }
                else if (why == "cancel") { e.state = "hitstun"; e.st = 0; e.stun = 16; }
                else { e.state = "launched"; e.st = 0; e.vx = dir * RAM.rush.end.kb[0] * (1 + 0.15 * L); e.vy = RAM.rush.end.kb[1]; }
            }
            if (why == "wall" && pile.Count > 0) Emit("ramSplat", new Ev { p = p, x = p.x + dir * (p.w / 2 + 0.6), y = p.y + 1, n = pile.Count, level = L });
            if (why == "bonk") { p.vx = -dir * 4; p.hitstop = JMath.Max(p.hitstop, 4); Emit("ramBonk", new Ev { p = p, e = bonked, x = p.x + dir * p.w / 2, y = p.y + 1.2, level = L }); }
            p.rush = null;
            Emit("rushEnd", new Ev { p = p, why = why, n = pile.Count });
        }

        // Bulwark Wall: a hard-light wall planted in front of him (one at a time)
        public void RaiseWall(Player p)
        {
            foreach (var b in barriers.Live()) if (b.kind == "rampart" && b.owner == p) b.ttl = 0;
            double x = p.x + p.facing * RAM.wall.dist;
            for (int i = 0; i < 6 && Level.PointInSolid(x, p.y + 0.5); i++) x -= p.facing * 0.3;   // against a wall it goes up close
            double gy = Level.GroundBelow(x, p.y + 0.5), bse = gy > double.NegativeInfinity && p.y - gy < 4 ? gy : p.y;
            barriers.Add(new Barrier { kind = "rampart", owner = p, x = x, y = bse + RAM.wall.half, nx = p.facing, ny = 0, half = RAM.wall.half, ttl = RAM.wall.ticks, max = RAM.wall.ticks, hp = RAM.wall.hp, maxHp = RAM.wall.hp, hitT = 0 });
            p.wallCd = RAM.wall.cd;
            Emit("wallUp", new Ev { p = p, x = x, y = bse });
        }
        // An enemy that would pass through a Bulwark Wall is held on its own side (a boss smashes it)
        void WallBlock(Enemy e)
        {
            foreach (var b in barriers.Live())
            {
                if (b.kind != "rampart" || b.ttl <= 0) continue;
                if (e.y > b.y + b.half || e.y + e.h < b.y - b.half) continue;
                double hw = e.w / 2, s1 = e.x - b.x;
                if (e.boss) { if (JMath.Abs(s1) < hw) { b.hp = 0; b.ttl = 0; } continue; }
                double side = or(or(sign(e.prevX - b.x), sign(s1)), 1);
                if (side * s1 < hw) { e.x = b.x + side * (hw + 0.02); if (e.vx * side < 0) e.vx = 0; }
            }
        }
        // The Bulwark Wall standing between an attacker and a player, if any
        public Barrier WallBetween(Actor a, Player q)
        {
            foreach (var b in barriers.Live())
            {
                if (b.kind != "rampart" || b.ttl <= 0 || (a.x - b.x) * (q.x - b.x) >= 0) continue;
                if (q.y > b.y + b.half || q.y + q.h < b.y - b.half) continue;
                return b;
            }
            return null;
        }
        public void HurtWall(Barrier b, double dmg, double x, double y)
        {
            b.hp -= dmg; b.hitT = 8;
            Emit("wallHit", new Ev { barrier = b, x = x, y = y, frac = JMath.Max(0, b.hp / b.maxHp) });
            if (b.hp <= 0) b.ttl = 0;
        }

        // Guardian Link: to the teammate who needs it most, leaping to their side first when they are far
        public void StartLink(Player p)
        {
            Player best = null; double bs = double.PositiveInfinity;
            foreach (var q in players.Live())
            {
                if (q == p || q.state == "dead" || q.state == "downed") continue;
                double d = JMath.Hypot(q.x - p.x, q.y - p.y);
                if (d > RAM.link.range) continue;
                double s = d - 12 * (1 - q.hp / q.maxHp) + (GuardianOf(q) != null ? 6 : 0);
                if (s < bs) { bs = s; best = q; }
            }
            if (best == null) { Emit("linkNone", new Ev { p = p }); return; }
            p.linkCd = RAM.link.cd;
            if (JMath.Hypot(best.x - p.x, best.y - p.y) > RAM.link.leapAt && p.state != "ult")
            {
                if (p.rush != null) EndRush(p, "cancel");
                double T = RAM.link.leapTicks * DT, dy = best.y - p.y;
                p.leap = new LeapState { q = best, t = 0, side = or(sign(best.x - p.x), 1), tx = best.x, ty = best.y };
                p.state = "leap"; p.st = 0; p.vy = JMath.Min(30, JMath.Max(11, (dy + 0.5 * GRAVITY * T * T) / T)); p.onGround = false; p.crouch = false;
                p.dash = null; p.dodge = null; p.pound = null; p.chargeT = 0; p.dashChargeT = 0; p.meleeCharged = false;
                Emit("leap", new Ev { p = p, q = best });
            }
            else MakeLink(p, best);
        }
        void MakeLink(Player p, Player q)
        {
            if (p.link != null && p.link.q != q) EndLink(p, "replaced");
            p.link = new LinkState { q = q, t = RAM.link.ticks };
            AddPlate(q, RAM.link.plate);
            Emit("link", new Ev { p = p, q = q });
            Bark(p, "guardian", 0.5);
        }
        // The leap lands: enemies close by are shoved away, and the link is made
        public void LandLeap(Player p)
        {
            var leap = p.leap; p.leap = null;
            p.state = "normal"; p.st = 0; p.vx *= 0.2;
            SpawnHitbox(new Hit
            {
                owner = p, team = "p", x0 = p.x - RAM.link.land.r, x1 = p.x + RAM.link.land.r, y0 = p.y - 0.3, y1 = p.y + 1.9, dmg = RAM.link.land.dmg, poise = RAM.link.land.poise,
                kb = new[] { 9.0, 5.0 }, radial = true, cx = p.x, instance = NewInstance(), scatter = true, ramKnock = true,
            });
            Emit("leapLand", new Ev { p = p, x = p.x, y = p.y });
            var q = leap?.q;
            if (q != null && players.Contains(q) && q.state != "dead" && q.state != "downed") MakeLink(p, q);
        }
        public void TickLink(Player p)
        {
            var k = p.link; var q = k.q;
            if (--k.t <= 0 || !players.Contains(q) || q.state == "dead" || q.state == "downed" || p.state == "downed" || p.state == "dead" ||
                JMath.Hypot(q.x - p.x, q.y - p.y) > RAM.link.breakAt) EndLink(p, k.t <= 0 ? "expire" : "break");
        }
        void EndLink(Player p, string why) { if (p.link == null) return; var q = p.link.q; p.link = null; Emit("linkEnd", new Ev { p = p, q = q, why = why }); }
        // The RAM linked to this player, if any
        public Player GuardianOf(Player q) { foreach (var g in players.Live()) if (g.link != null && g.link.q == q && g.state != "downed" && g.state != "dead") return g; return null; }
        // His share of a hit on the teammate he guards (his Plating first; it can put him down)
        public void LinkHit(Player g, double amount, Player from)
        {
            double dmg = amount;
            if (g.plate > 0) { double a = JMath.Min(g.plate, dmg); g.plate -= a; dmg -= a; }
            g.hp -= dmg; GainUlt(g, amount * ULT.gain.taken, this);
            Emit("linkHit", new Ev { p = g, q = from, dmg = amount });
            if (g.hp <= 0) { g.hp = 0; DownPlayer(g); }
        }

        // Provoke: every enemy close by turns on him and attacks sooner; he braces, and the roar shoves light enemies
        public void Provoke(Player p)
        {
            var c = Chest(p); double n = 0;
            foreach (var e in enemies.Live())
            {
                if (e.dead || e.type == "post" || e.type == "turret") continue;
                double d = JMath.Hypot(e.x - c.x, e.y + e.h / 2 - c.y);
                if (d > RAM.provoke.range) continue;
                e.taunter = p; e.tauntT = RAM.provoke.ticks; e.target = p; n++;
                if (e.type == "sniper" && (e.state == "aim" || e.state == "lock")) { e.aimX = p.x; e.aimY = p.y + 1.2; }
                if (e.cd > 20) e.cd = 20;
                Emit("taunted", new Ev { e = e, by = p });
                if (d < RAM.provoke.shove.r + e.w / 2 && e.light && !e.boss && !e.flier && e.state != "plowed")
                {
                    director.Release(e); e.state = "launched"; e.st = 0; e.vx = or(sign(e.x - p.x), p.facing) * RAM.provoke.shove.kb; e.vy = 5;
                }
            }
            p.braceT = RAM.provoke.ticks; p.provokeCd = RAM.provoke.cd;
            Emit("provoke", new Ev { p = p, x = c.x, y = c.y, n = n, r = RAM.provoke.range });
            Bark(p, "provoke", 0.6);
        }

        // Seismic Slam: shockwaves run out both ways along the floor from where the shield struck
        public void SpawnQuake(Player p, QuakeDef Q)
        {
            foreach (double dir in new[] { 1.0, -1.0 })
                shockwaves.Add(new Shockwave { owner = p, team = "p", x = p.x + dir * (p.w / 2 + 0.5), y = p.y, dir = dir, speed = Q.speed, ttl = Q.ttl, dmg = Q.dmg, poise = Q.poise, h = Q.h, instance = NewInstance() });
            Emit("quake", new Ev { p = p, x = p.x + p.facing * 1.0, y = p.y });
        }
        // Hydraulic Uplift: enemy shots in the sweep of the rising shield go
        public void SweepShots(Player p)
        {
            double x0 = p.facing > 0 ? p.x - 0.6 : p.x - 2.4, x1 = p.facing > 0 ? p.x + 2.4 : p.x + 0.6, y0 = p.y + 0.4, y1 = p.y + p.h + 1.8;
            foreach (var pr in projectiles.Live())
            {
                if (pr.team != "e" || pr.dead || pr.x < x0 || pr.x > x1 || pr.y < y0 || pr.y > y1) continue;
                pr.dead = true; Emit("erase", new Ev { x = pr.x, y = pr.y });
            }
        }

        // ---- Fix ----
        public void FireRivet(Player p, double i)
        {
            var c = Chest(p); double a = JMath.Atan2(p.aimY, p.aimX) + (i - 1) * 0.035;
            double x = c.x + p.aimX * 0.7, y = c.y + p.aimY * 0.7;
            SpawnProjectile(new Projectile { team = "p", owner = p, x = x, y = y, vx = JMath.Cos(a) * FIX.rivet.speed, vy = JMath.Sin(a) * FIX.rivet.speed, ttl = FIX.rivet.ttl, r = FIX.rivet.r, dmg = FIX.rivet.dmg, poise = FIX.rivet.poise, kbs = 1.5, intercept = true, interceptHeavy = false, kind = "rivet", level = 0 });
            p.shootT = 10;
            Emit("shot", new Ev { p = p, level = 0, x = x, y = y, ax = p.aimX, ay = p.aimY, rivet = true });
        }
        // A Hot Rivet: it sticks in what it hits and bursts after its fuse
        public void FireHotRivet(Player p, int level)
        {
            var c = Chest(p); double x = c.x + p.aimX * 0.7, y = c.y + p.aimY * 0.7; int L = level - 1;
            SpawnProjectile(new Projectile
            {
                team = "p", owner = p, x = x, y = y, vx = p.aimX * FIX.rivet.hot.speed, vy = p.aimY * FIX.rivet.hot.speed, ttl = FIX.rivet.hot.ttl, r = FIX.rivet.hot.r,
                dmg = FIX.rivet.hot.dmg[L], poise = FIX.rivet.hot.poise[L], kbs = 2, intercept = true, interceptHeavy = level >= 2, kind = "hotRivet", level = level,
                stick = new StickInfo { fuse = FIX.rivet.hot.fuse, blast = FIX.rivet.hot.blast[L] },
            });
            p.shootT = 12;
            Emit("shot", new Ev { p = p, level = level, x = x, y = y, ax = p.aimX, ay = p.aimY, rivet = true });
        }

        public double Heal(Player q, double amount, Player from = null)
        {
            if (!(amount > 0) || q.state == "dead" || q.state == "downed") return 0;
            double before = q.hp; q.hp = JMath.Min(q.maxHp, q.hp + amount);
            if (truthy(q.strain)) q.strain = JMath.Max(0, JMath.Min(q.strain, q.maxHp - q.hp));
            double healed = q.hp - before;
            if (from != null && from != q && healed > 0) GainUlt(from, healed * ULT.gain.heal, this);
            return healed;
        }
        // Fix's ground pound: a repair pulse from the landing
        public void RepairPulse(Player p, double x, double y, double r, double amount)
        {
            foreach (var q in players.Live()) if (q.state != "dead" && q.state != "downed" && JMath.Hypot(q.x - x, q.y - y) <= r) Heal(q, amount, p);
            Emit("repairPulse", new Ev { p = p, x = x, y = y, r = r });
        }
        // Torque Slam: a ring of sparks that stuns light enemies and drones
        public void SparkRing(Player p, Blast S)
        {
            double x = p.x + p.facing * 0.9, y = p.y + 0.5;
            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                if (e.dead) continue;
                var hb = Hurtbox(e); double qx = JMath.Max(hb.x0, JMath.Min(x, hb.x1)), qy = JMath.Max(hb.y0, JMath.Min(y, hb.y1));
                if (JMath.Hypot(qx - x, qy - y) > S.r) continue;
                string res = HitEnemy(this, e, new Hit { owner = p, dmg = S.dmg, poise = S.poise, kb = new[] { or(sign(e.x - x), p.facing) * 3, 2.0 }, shock = true, stun = S.stun }, "blast");
                if (res != "none" && !e.dead && e.light && !e.boss && e.armor <= 0 && e.state != "plowed")
                {
                    director.Release(e); e.state = "hitstun"; e.st = 0; e.stun = S.stun; e.shockT = S.stun;
                }
            }
            Emit("sparkRing", new Ev { p = p, x = x, y = y, r = S.r });
        }

        // The Patch Beam's target: the teammate who needs it most within range
        public Player PatchTarget(Player p, Player keep)
        {
            Player best = null; double bs = double.PositiveInfinity;
            foreach (var q in players.Live())
            {
                if (q == p || q.state == "dead") continue;
                double d = JMath.Hypot(q.x - p.x, q.y + q.h * 0.5 - (p.y + p.h * 0.5));
                if (d > (q == keep ? FIX.beam.keep : FIX.beam.range)) continue;
                double s = d - (q.state == "downed" ? 30 : 12 * (1 - q.hp / q.maxHp)) - (q == keep ? 4 : 0);
                if (p.aimFree && JMath.Abs(p.aimX) > 0.3 && (q.x - p.x) * p.aimX < -1.5) s += 8;
                if (s < bs) { bs = s; best = q; }
            }
            return best;
        }
        public void PatchTick(Player p)
        {
            var P = p.patch; P.t++;
            var q = P.target;
            bool valid = q != null && players.Contains(q) && q.state != "dead" && JMath.Hypot(q.x - p.x, q.y - p.y) <= FIX.beam.keep;
            // Every half second it looks again in case someone needs it more
            if (!valid || P.t % 30 == 0)
            {
                var n = PatchTarget(p, valid ? q : null);
                if (n != q) { P.target = q = n; Emit("patchTarget", new Ev { p = p, q = q }); }
            }
            if (q == null) { P.self = true; Heal(p, FIX.beam.self / 60); return; }
            P.self = false;
            if (q.state == "downed") { q.reviveGain = q.reviveGain + FIX.beam.revive; q.fixRevive = true; q.reviveBy = p; return; }
            if (Heal(q, FIX.beam.heal / 60, p) <= 0) AddPlate(q, FIX.beam.plateRate / 60, FIX.beam.plate);
            q.tuneT = JMath.Max(q.tuneT, 3);
        }

        // Gadgets
        public bool DeployGadget(Player p)
        {
            string kind = p.gadgetSel; var G = FIX.gadget[kind];
            if (p.scrap < G.cost) { Emit("noScrap", new Ev { p = p, kind = kind, cost = G.cost }); return false; }
            double x = p.x + p.facing * 0.95;
            for (int i = 0; i < 4 && Level.PointInSolid(x, p.y + 0.4, p.lane); i++) x -= p.facing * 0.3;
            double gy = Level.GroundBelow(x, p.y + 0.3, p.lane);
            if (double.IsNegativeInfinity(gy)) { Emit("noScrap", new Ev { p = p, kind = kind, cost = G.cost, spot = true }); return false; }   // nothing to stand it on
            var old = gadgets.Find(g => g.owner == p && g.kind == kind && !g.dead);
            if (old != null) { old.dead = true; Emit("gadgetEnd", new Ev { g = old, why = "moved" }); }
            p.scrap -= G.cost;
            var gd = new Gadget
            {
                id = NewInstance(), lane = p.lane, kind = kind, owner = p, x = x, y = p.y, py = p.y, gy = gy, vy = 0, landed = p.y - gy < 0.05, level = 1, pts = 0, t = 0, life = G.life[0],
                hp = G.hp, maxHp = G.hp, cd = 24, rocketCd = 50, aim = p.facing, aimY = 0, dead = false, h = GadgetH(kind),
            };
            if (gd.landed) gd.y = gy;
            gadgets.Add(gd);
            Emit("gadgetDeploy", new Ev { p = p, g = gd });
            Bark(p, "gadget", 0.25);
            return true;
        }
        // Jack-Up's jack, left behind as a spring pad (one at a time)
        public void PlacePad(Player p)
        {
            foreach (var g in gadgets.Live()) if (g.owner == p && g.kind == "pad") g.dead = true;
            double gy = Level.GroundBelow(p.x, p.y + 0.3, p.lane);
            if (double.IsNegativeInfinity(gy) || p.y - gy > 0.3) return;
            gadgets.Add(new Gadget { id = NewInstance(), lane = p.lane, kind = "pad", owner = p, x = p.x, y = gy, py = gy, gy = gy, vy = 0, landed = true, level = 1, pts = 0, t = 0, life = FIX.pad.life, hp = 999, maxHp = 999, dead = false, h = GadgetH("pad") });
            Emit("padPlace", new Ev { p = p, x = p.x, y = gy });
        }
        // One of her gadgets (not a pad) close in front of her: melee is then the wrench
        public bool GadgetNear(Player p) =>
            gadgets.Exists(g => g.owner == p && g.kind != "pad" && !g.dead && LevelFeatures.Same(g.lane, p) && (g.x - p.x) * p.facing > -0.6 && JMath.Abs(g.x - p.x) < 1.9 && JMath.Abs(g.y - p.y) < 1.6);
        // A gadget an enemy shot has reached
        public Gadget GadgetAt(double x, double y, double r, int lane = 0)
        {
            foreach (var g in gadgets.Live())
            {
                if (g.dead || g.kind == "pad" || !LevelFeatures.Same(lane, g.lane)) continue;
                double nx = JMath.Max(g.x - 0.4, JMath.Min(x, g.x + 0.4)), ny = JMath.Max(g.y, JMath.Min(y, g.y + g.h));
                if (JMath.Hypot(x - nx, y - ny) < r) return g;
            }
            return null;
        }
        public void HurtGadget(Gadget g, double dmg)
        {
            if (g.dead || g.kind == "pad") return;
            g.hp -= dmg; g.hitT = 8;
            Emit("gadgetHit", new Ev { g = g, dmg = dmg });
            if (g.hp <= 0) { g.dead = true; Emit("gadgetEnd", new Ev { g = g, why = "broken" }); }
        }
        // A wrench hit: two raise a gadget a level (up to 3), refreshing it; at level 3 they repair it and buy it time
        public void WrenchGadget(Gadget g, Player p)
        {
            if (g.dead || g.kind == "pad" || !LevelFeatures.Same(g.lane, p)) return;
            var G = FIX.gadget[g.kind];
            if (g.level < 3)
            {
                if (++g.pts >= 2) { g.pts = 0; g.level++; g.t = 0; g.life = G.life[(int)g.level - 1]; g.hp = g.maxHp; Emit("gadgetUp", new Ev { p = p, g = g }); Bark(p, "upgrade", 0.3); }
                else Emit("gadgetWrench", new Ev { p = p, g = g });
            }
            else { g.hp = g.maxHp; g.t = JMath.Max(0, g.t - 120); Emit("gadgetWrench", new Ev { p = p, g = g, max = true }); }
        }
        // An Amp Coil's field, worked out before anyone acts
        void AmpField()
        {
            foreach (var p in players.Live()) p.ampK = 1;
            foreach (var g in gadgets.Live())
            {
                if (g.kind != "coil" || g.dead || !g.landed) continue;
                var C = FIX.gadget["coil"]; int L = (int)g.level - 1;
                foreach (var p in players.Live()) if (p.state != "dead" && JMath.Hypot(p.x - g.x, p.y + p.h * 0.5 - (g.y + 0.8)) <= C.r[L]) p.ampK = JMath.Max(p.ampK, C.rate[L]);
            }
        }
        void UpdateGadgets()
        {
            foreach (var g in gadgets.ToArray())
            {
                if (g.dead) continue;
                var o = g.owner;
                if (!players.Contains(o) || o.@char != "fix") { g.dead = true; Emit("gadgetEnd", new Ev { g = g, why = "gone" }); continue; }
                g.py = g.y;
                if (g.landed && Level.GroundBelow(g.x, g.y + .08, g.lane) < g.y - .08) { g.landed = false; g.vy = 0; }
                if (!g.landed)
                {
                    g.gy = Level.GroundBelow(g.x, g.y + .08, g.lane);
                    g.vy -= GRAVITY * DT; g.y += g.vy * DT;
                    if (g.y <= g.gy) { g.y = g.gy; g.vy = 0; g.landed = true; Emit("gadgetLand", new Ev { g = g }); }
                    if (g.y < Level.KillYAt(g.x)) { g.dead = true; Emit("gadgetEnd", new Ev { g = g, why = "fell" }); continue; }
                }
                if (g.hitT > 0) g.hitT--;
                if (++g.t >= g.life) { g.dead = true; Emit("gadgetEnd", new Ev { g = g, why = "expire" }); continue; }
                if (!g.landed) continue;
                int L = (int)g.level - 1;
                if (g.kind == "pylon") PylonTick(g, L);
                else if (g.kind == "sentry") SentryTick(g, L);
                else if (g.kind == "pad") PadTick(g);
            }
            gadgets.RemoveAll(g => g.dead);
        }
        // Patch Pylon: heals everyone in its field; a downed teammate inside it gets back up on their own, slowly
        void PylonTick(Gadget g, int L)
        {
            var P = FIX.gadget["pylon"];
            foreach (var q in players.Live())
            {
                if (q.state == "dead" || JMath.Hypot(q.x - g.x, q.y + q.h * 0.5 - (g.y + 0.7)) > P.r[L]) continue;
                if (q.state == "downed") { q.reviveGain = q.reviveGain + P.revive[L]; q.fixRevive = true; q.reviveBy = q.reviveBy ?? g.owner; continue; }
                if (Heal(q, P.heal[L] / 60, g.owner) <= 0 && truthy(P.plate[L])) AddPlate(q, P.plate[L] / 60, P.plateMax);
            }
        }
        // Sentry: the nearest enemy in sight and in range; a bolt every few ticks, and at level 3 a homing rocket too
        void SentryTick(Gadget g, int L)
        {
            var S = FIX.gadget["sentry"]; double ox = g.x, oy = g.y + 0.78;
            if (g.cd > 0) g.cd--; if (g.rocketCd > 0) g.rocketCd--;
            Enemy best = null; double bd = S.range[L];
            foreach (var e in enemies.Live())
            {
                if (e.dead || e.type == "post" || !LevelFeatures.Same(g.lane, e)) continue;
                double ex = e.x, ey = e.y + e.h * 0.55, d = JMath.Hypot(ex - ox, ey - oy);
                if (d < bd && !Level.SegmentBlocked(ox, oy, ex, ey, g.lane)) { bd = d; best = e; }
            }
            g.target = best;
            if (best == null) return;
            double dx = best.x - ox, dy = best.y + best.h * 0.55 - oy, m = or(JMath.Hypot(dx, dy), 1);
            g.aim = dx / m; g.aimY = dy / m;
            if (g.cd <= 0)
            {
                SpawnProjectile(new Projectile { team = "p", owner = g.owner, lane = g.lane, laneSet = true, x = ox + g.aim * 0.45, y = oy + g.aimY * 0.45, vx = g.aim * S.speed, vy = g.aimY * S.speed, ttl = 60, r = 0.1, dmg = S.dmg[L], poise = S.poise, kbs = 1.5, kind = "sentryBolt", intercept = true, interceptHeavy = false, gadget = true });
                g.cd = S.every[L];
                Emit("sentryShot", new Ev { g = g, x = ox + g.aim * 0.45, y = oy + g.aimY * 0.45 });
            }
            if (L >= 2 && g.rocketCd <= 0)
            {
                SpawnProjectile(new Projectile
                {
                    team = "p", owner = g.owner, lane = g.lane, laneSet = true, x = ox, y = oy + 0.2, vx = g.aim * S.rocketSpeed * 0.5, vy = S.rocketSpeed * 0.6, ttl = 150, r = 0.14, dmg = 0, poise = 0,
                    kind = "sentryRocket", intercept = false, blast = S.rocketBlast.Clone(), seek = new SeekState { target = best, delay = 8, until = 150, turn = 0.12, age = 0 }, gadget = true,
                });
                g.rocketCd = S.rocketEvery;
                Emit("sentryRocket", new Ev { g = g, x = ox, y = oy + 0.2 });
            }
        }
        static bool Bounceable(string s) => s == "normal" || s == "guard" || s == "patch" || s == "attack";
        // Spring pad: whoever comes down onto it is bounced high; light enemies standing on it are thrown up
        void PadTick(Gadget g)
        {
            foreach (var q in players.Live())
            {
                if (!LevelFeatures.Same(g.lane, q)) continue;
                if (q.padCd > 0 || !Bounceable(q.state) || q.vy > 0.5) continue;
                if (JMath.Abs(q.x - g.x) > (FIX.pad.w + q.w) / 2 || q.y < g.y - 0.05 || q.y > g.y + 0.45) continue;
                q.vy = FIX.pad.bounce; q.onGround = false; q.coyote = 0; q.jumpsUsed = 0; q.airDashes = 1; q.airRise = true; q.airDodge = true;
                q.dashCarry = true; q.fastFall = false; q.padCd = 20;
                Emit("padBounce", new Ev { p = q, g = g, x = g.x, y = g.y });
            }
            foreach (var e in enemies.Live())
            {
                if (e.dead || !e.light || e.flier || e.boss || e.state == "launched" || e.state == "plowed") continue;
                if (!LevelFeatures.Same(g.lane, e)) continue;
                if (JMath.Abs(e.x - g.x) > (FIX.pad.w + e.w) / 2 || JMath.Abs(e.y - g.y) > 0.3) continue;
                director.Release(e); e.state = "launched"; e.st = 0; e.vy = FIX.pad.enemyBounce; e.vx = 0;
                Emit("padBounce", new Ev { e = e, g = g, x = g.x, y = g.y });
            }
        }

        // Power-ups: tossed to the nearest teammate in front (it homes in on them), or dropped at her feet
        public bool TossPower(Player p)
        {
            string kind = p.powerSel; double cost = FIX.power.cost;
            if (p.scrap < cost) { Emit("noScrap", new Ev { p = p, kind = kind, cost = cost }); return false; }
            p.scrap -= cost;
            Player target = null; double bd = FIX.power.range;
            foreach (var q in players.Live())
            {
                if (q == p || q.state == "dead" || q.state == "downed") continue;
                double dx = q.x - p.x, d = JMath.Hypot(dx, q.y - p.y);
                if (d < bd && (dx * p.facing > -1 || d < 2.5)) { bd = d; target = q; }
            }
            var c = Chest(p); double x = c.x + p.facing * 0.4, y = c.y + 0.25;
            pickups.Add(new Pickup
            {
                id = NewInstance(), lane = p.lane, kind = kind, owner = p, x = x, y = y, px = x, py = y, vx = target != null ? or(sign(target.x - p.x), p.facing) * FIX.power.speed * 0.6 : p.facing * 2.5,
                vy = target != null ? FIX.power.lift : 4, target = target, t = 0, life = FIX.power.life, rest = false, dead = false,
            });
            Emit("powerToss", new Ev { p = p, kind = kind, q = target, x = x, y = y });
            return true;
        }
        void UpdatePickups()
        {
            foreach (var k in pickups.ToArray())
            {
                k.px = k.x; k.py = k.y; k.t++;
                if (k.target != null && (k.target.state == "dead" || k.target.state == "downed" || !players.Contains(k.target))) k.target = null;
                if (k.target != null)
                {
                    // a pass that speeds up into their hands
                    var q = k.target; double dx = q.x - k.x, dy = q.y + q.h * 0.55 - k.y, d = or(JMath.Hypot(dx, dy), 1), sp = JMath.Min(26, FIX.power.speed + k.t * 0.5);
                    k.vx += (dx / d * sp - k.vx) * 0.25; k.vy += (dy / d * sp - k.vy) * 0.25;
                    k.x += k.vx * DT; k.y += k.vy * DT;
                }
                else if (!k.rest)
                {
                    k.vy -= FIX.power.gravity * DT; double nx = k.x + k.vx * DT, ny = k.y + k.vy * DT;
                    if (Level.PointInSolid(nx, k.y, k.lane)) k.vx *= -0.3; else k.x = nx;
                    double g = Level.GroundBelow(k.x, k.y + 0.05, k.lane);
                    if (k.vy <= 0 && g > double.NegativeInfinity && ny - 0.18 <= g) { k.y = g + 0.18; k.vy = 0; k.vx = 0; k.rest = true; } else k.y = ny;
                    if (k.y < Level.KillYAt(k.x)) k.dead = true;
                }
                foreach (var q in players.ToArray())
                {
                    if (k.dead || q.state == "dead" || q.state == "downed" || (q == k.owner && k.t < FIX.power.ownerDelay)) continue;
                    if (JMath.Abs(q.x - k.x) < q.w / 2 + FIX.power.grab * 0.5 && k.y > q.y - 0.4 && k.y < q.y + q.h + 0.4) { ApplyPower(q, k.kind, k.owner); k.dead = true; }
                }
                if (!k.dead && --k.life <= 0) { k.dead = true; Emit("powerFade", new Ev { x = k.x, y = k.y, kind = k.kind, depth = k.lane * LevelFeatures.LANE_W }); }
            }
            pickups.RemoveAll(k => k.dead);
        }
        void ApplyPower(Player q, string kind, Player from)
        {
            if (kind == "overclock") q.overclockT = JMath.Max(q.overclockT, FIX.power.overclock.ticks);
            else if (kind == "plating") AddPlate(q, FIX.power.plating.plate);
            else if (kind == "ultcell") GainUlt(q, POWERUPS.ultcell.ult, this);
            else if (kind == "fury") q.furyT = JMath.Max(q.furyT, POWERUPS.fury.ticks);
            else Heal(q, FIX.power.medkit.heal, from);
            Emit("powerUp", new Ev { p = q, kind = kind, by = from });
        }
        // Lift pads: a player coming down onto one is thrown up to the platform over it
        void LiftTick()
        {
            foreach (var L in Level.LIFTS) foreach (var q in players.Live())
                {
                    if (q.padCd > 0 || !Bounceable(q.state) || q.vy > 0.5) continue;
                    if (JMath.Abs(q.x - L.x) > 0.8 + q.w / 2 || q.y < L.y - 0.05 || q.y > L.y + 0.45) continue;
                    q.vy = JMath.Sqrt(2 * GRAVITY * (L.top - L.y + 1.6)); q.onGround = false; q.coyote = 0; q.jumpsUsed = 0; q.airDashes = 1; q.airRise = true;
                    q.fastFall = false; q.dashCarry = true; q.padCd = 30;
                    Emit("liftBounce", new Ev { p = q, x = L.x, y = L.y, top = L.top });
                }
        }

        // Power-ups along the routes: they wait where they are until someone takes them
        void SpawnLevelPickups()
        {
            pickups.RemoveAll(k => k.level);
            foreach (var lp in Level.LEVEL_PICKUPS) AddLevelPickup(lp.x, lp.y + 0.18, lp.kind, true);
        }
        Pickup AddLevelPickup(double x, double y, string kind, bool rest = false, double vy = 0)
        {
            var k = new Pickup { id = NewInstance(), kind = kind, owner = null, x = x, y = y, px = x, py = y, vx = 0, vy = vy, target = null, t = 0, life = rest ? 1e9 : POWERUPS.dropLife, rest = rest, dead = false, level = true };
            pickups.Add(k); return k;
        }

        // ---- Breakable pieces ----
        public bool DamageBox(LevelBox b, double dmg, double x, double y, Player by = null)
        {
            if (b == null || b.broken || b.type != 'd') return false;
            var D = Level.DESTRUCT[b.tag];
            if (dmg < D.min) { Emit("boxChip", new Ev { box = b, x = x, y = y, hard = true }); return false; }
            b.hp -= dmg;
            if (b.hp > 0) { Emit("boxChip", new Ev { box = b, x = x, y = y }); return false; }
            b.broken = true;
            Emit("boxBreak", new Ev { box = b, x = (b.x0 + b.x1) / 2, y = (b.y0 + b.y1) / 2, by = by });
            if (b.loot != null) AddLevelPickup((b.x0 + b.x1) / 2, (b.y0 + b.y1) / 2, b.loot, false, 5);
            return true;
        }
        // Every breakable piece touching a box (a strike), once per attack instance
        public void StrikeBoxes(Hit hb)
        {
            var set = HitSet(hb.instance.Value);
            foreach (var b in Level.BOXES)
            {
                if (b.type != 'd' || b.broken || set.Contains("b" + b.id)) continue;
                if (hb.x0 < b.x1 && hb.x1 > b.x0 && hb.y0 < b.y1 && hb.y1 > b.y0)
                {
                    set.Add("b" + b.id);
                    DamageBox(b, or(hb.dmg, 1) * (hb.heavy || hb.armorBreak ? 2 : 1) + (hb.ram ? 6 : 0), (b.x0 + b.x1) / 2, JMath.Min(b.y1, JMath.Max(b.y0, (hb.y0 + hb.y1) / 2)), hb.owner as Player);
                }
            }
        }
        // A blast: every breakable piece within r (more damage the closer)
        void BlastBoxes(double x, double y, double r, double dmg, Player by)
        {
            foreach (var b in Level.BOXES)
            {
                if (b.type != 'd' || b.broken) continue;
                double nx = JMath.Max(b.x0, JMath.Min(x, b.x1)), ny = JMath.Max(b.y0, JMath.Min(y, b.y1)), d = JMath.Hypot(x - nx, y - ny);
                if (d <= r) DamageBox(b, dmg * (1.5 - 0.5 * d / JMath.Max(0.1, r)), nx, ny, by);
            }
        }

        // An enemy fell: Fix picks up Scrap from it if she is close
        public void OnKill(Enemy e, Actor owner)
        {
            foreach (var p in players.Live())
            {
                if (p.@char != "fix" || p.state == "dead" || p.state == "downed" || JMath.Hypot(p.x - e.x, p.y - e.y) > FIX.scrap.killRange) continue;
                p.scrap = JMath.Min(FIX.scrap.max, p.scrap + FIX.scrap.kill);
                Emit("scrap", new Ev { p = p, x = e.x, y = e.y + e.h / 2 });
            }
        }

        // ---- The tick ----
        void SpawnGym()
        {
            enemies.Add(Enemies.CreateEnemy("post", 54.5, 0, q => q.zone = "gym"));
            enemies.Add(Enemies.CreateEnemy("turret", 58, 3.05, q => { q.zone = "gym"; q.facing = -1; }));
        }

        public void Step(Dictionary<int, Cmd> cmds)
        {
            tick++;
            // An ultimate being called: the world holds still, and only teammates joining in are listened to
            if (ultCast != null && ultCast.phase == "cast") { UltCastTick(cmds); return; }
            if (globalBarkCd > 0) globalBarkCd--;
            var due = scheduled.FindAll(s => s.t <= tick);
            scheduled = scheduled.FindAll(s => s.t > tick);
            foreach (var s in due.Live()) s.fn();
            // While an ultimate plays out, enemies, their shots and shockwaves stay frozen
            bool frozen = ultCast != null;
            AmpField();

            foreach (var p in players.ToArray())
            {
                if (p.barkCd > 0) p.barkCd--;
                if (p.state == "dead") { TickDead(p); continue; }
                var c0 = Chest(p);
                UpdatePlayer(p, cmds != null && cmds.TryGetValue(p.slot, out var cmd) ? cmd : Cmd.EMPTY, this);
                if (p.state == "parry" && p.@char == "echo") SpinStun(p);
                // RAM's charges carry what they scoop up, after he has moved
                if (p.state == "rush") RamPlow(p);
                else if (p.state == "ult" && p.ultRun != null && p.ultRun.kind == "ram") UltRamCarry(p);
                if (p.state == "dash")
                {
                    var c1 = Chest(p);
                    foreach (var b in barriers.Live())
                        if (b.kind != "rampart" && p.boostT <= 0 && CrossesBarrier(b, c0.x, c0.y, c1.x, c1.y)) { p.boostT = 40; Emit("boost", new Ev { p = p, x = c1.x, y = c1.y }); }
                    if (p.dash != null && p.dash.pursuit == null && p.st == 1)
                    {
                        var t = PursuitTarget(p, p.dash.dx, p.dash.dy);
                        if (t != null) { p.dash.pursuit = t; p.dash.t = JMath.Max(p.dash.t, 20); Emit("pursuit", new Ev { p = p, e = t }); }
                    }
                }
            }
            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                if (frozen && !e.dead) { e.prevX = e.x; e.prevY = e.y; if (e.flash > 0) e.flash--; continue; }
                Enemies.UpdateEnemy(e, this);
            }
            if (barriers.Exists(b => b.kind == "rampart")) foreach (var e in enemies.Live()) if (!e.dead) WallBlock(e);
            if (!frozen) UpdateShockwaves(this);
            UpdateProjectiles(this, frozen);
            UpdateWells(frozen);
            UpdateSnares();
            UpdateGadgets();
            UpdatePickups();
            LiftTick();
            LevelFeatures.Step(this, cmds, frozen);   // hazards and depth lanes
            ResolveHitboxes(this);
            if (ultCast != null) UltTick();
            foreach (var b in barriers.Live())
            {
                b.ttl--; if (b.hitT > 0) b.hitT--;
                if (b.ttl <= 0 && b.kind == "rampart") Emit("wallDown", new Ev { barrier = b, x = b.x, y = b.y - b.half, broken = b.hp <= 0 });
            }
            barriers.RemoveAll(b => b.ttl <= 0);

            TickRevives();
            UpdateCamera();
            UpdateEncounters();

            enemies.RemoveAll(e => e.dead && e.deathT > (e.boss ? 84 : 45));   // a boss stays for its explosions
            if (tick % 120 == 0)
            {
                int keep = instanceSeq - 400;
                foreach (var k in hitSets.Keys.ToList()) if (k < keep) hitSets.Remove(k);
            }
            if (wipeT > 0) { wipeT--; if (wipeT == 0) ResetToCheckpoint(); }
        }

        void TickDead(Player p)
        {
            p.prevX = p.x; p.prevY = p.y;
            if (wipeT > 0) return;
            p.respawnT--;
            if (p.respawnT <= 0)
            {
                var act = ActivePlayers(); var ally = act.Count > 0 ? act[0] : null;
                if (ally == null) return;
                p.x = ally.lastSafeX; p.y = ally.lastSafeY; p.prevX = p.x; p.prevY = p.y; p.vx = 0; p.vy = 0;
                p.state = "normal"; p.st = 0; p.hp = JMath.Round(p.maxHp * 0.3); p.mercy = 120; p.h = CHARS[p.@char].height;
                Emit("respawn", new Ev { p = p });
            }
        }

        // Reviving: everyone standing beside a downed teammate adds to it (Fix counts FIX.revive times over), and so do
        // Fix's Patch Beam and Patch Pylons from range (reviveGain)
        void TickRevives()
        {
            foreach (var p in players.ToArray())
            {
                if (p.state != "downed") { p.reviveGain = 0; continue; }
                if (p.autoRevive > 0) { p.autoRevive--; if (p.autoRevive == 0) RevivePlayer(p, null, 0.4); continue; }
                double gain = p.reviveGain; var by = p.reviveBy;
                foreach (var q in players.Live())
                {
                    if (q == p || q.state == "downed" || q.state == "dead" || q.state == "hitstun" || !LevelFeatures.Same(p, q) || JMath.Abs(q.x - p.x) >= 1.7 || JMath.Abs(q.y - p.y) >= 1.6) continue;
                    gain += q.@char == "fix" ? FIX.revive : 1; by = by ?? q;
                    if (q.@char == "fix") { p.fixRevive = true; by = q; }
                }
                p.reviveGain = 0;
                if (gain > 0)
                {
                    p.revive += gain;
                    if (p.revive >= 120) RevivePlayer(p, by, p.fixRevive ? FIX.reviveHp : 0.4);
                }
                else p.revive = JMath.Max(0, p.revive - 0.5);
            }
        }

        void UpdateCamera()
        {
            var act = players.FindAll(p => p.state != "dead");
            if (act.Count == 0) return;
            double x0 = double.PositiveInfinity, x1 = double.NegativeInfinity, y0 = double.PositiveInfinity, y1 = double.NegativeInfinity;
            foreach (var p in act.Live())
            {
                x0 = JMath.Min(x0, p.x); x1 = JMath.Max(x1, p.x); y0 = JMath.Min(y0, p.y); y1 = JMath.Max(y1, p.y + p.h);
                // Lining up a rocket jump, the camera eases back to show how high it will go
                var pv = p.chargeT > 0 ? RocketPreview(p) : null;
                if (pv != null) y1 = JMath.Max(y1, pv.apex + 0.6);
                if (p.rocketT > 0 && p.vy > 0) { y1 = JMath.Max(y1, p.y + ApexGain(p.vy) * 0.85 + p.h * 0.5); y0 = JMath.Min(y0, p.y - 2); }
            }
            // A boss in the fight stays in the frame
            double mid = (x0 + x1) / 2;
            foreach (var e in enemies.Live())
            {
                if (!e.boss || e.dead || JMath.Abs(e.x - mid) > 24) continue;
                x0 = JMath.Min(x0, e.x - e.w / 2); x1 = JMath.Max(x1, e.x + e.w / 2); y0 = JMath.Min(y0, e.y); y1 = JMath.Max(y1, JMath.Min(e.y + e.h, y0 + 16));
            }
            double tanH = JMath.Tan((SETTINGS.fov * JMath.PI / 180) / 2);
            double needW = (x1 - x0) + 9, needH = (y1 - y0) + 7;
            const double MIN_D = 13, MAX_D = 32;
            double dist = JMath.Max(needH / 2 / tanH, needW / 2 / (tanH * aspect));
            dist = JMath.Max(MIN_D, JMath.Min(MAX_D, dist));
            double halfH = dist * tanH, halfW = halfH * aspect;
            cam = new Cam { x = (x0 + x1) / 2 + 0.8, y = (y0 + y1) / 2 + 1.0, dist = dist, halfW = halfW, halfH = halfH };
            // Spread limit and recall (co-op). Being left behind is never a damage penalty.
            bool multi = act.Count > 1;
            foreach (var p in players.ToArray())
            {
                if (p.state == "dead") continue;
                if (p.y < Level.KillYAt(p.x)) { Recall(p, true); continue; }
                if (!multi) continue;
                double right = cam.x + halfW - 0.7;
                if (p.x > right) { p.x = right; if (p.vx > 0) p.vx = 0; }
                bool off = p.x < cam.x - halfW - 0.5 || p.y + p.h < cam.y - halfH - 1;
                p.offscreenT = off ? p.offscreenT + 1 : 0;
                if (p.offscreenT > 90 && p.state != "downed") Recall(p, false);
            }
        }

        public void Recall(Player p, bool pit)
        {
            var allies = ActivePlayers().FindAll(q => q != p);
            StableSort.Sort(allies, (m, n) => JMath.Abs(m.x - p.x) - JMath.Abs(n.x - p.x));
            var a = allies.Count > 0 ? allies[0] : null;
            double tx = a != null ? a.lastSafeX : p.lastSafeX, ty = a != null ? a.lastSafeY : p.lastSafeY;
            p.x = tx; p.y = ty; p.prevX = tx; p.prevY = ty; p.vx = 0; p.vy = 0; p.offscreenT = 0;
            p.mercy = 90;
            // An ultimate under way ends here; one still being called carries on from the new spot
            if (p.state == "ult") { if (p.ultRun != null) FinishUlt(p); }
            else if (p.state != "downed") { p.state = "normal"; p.st = 0; }
            Emit("recall", new Ev { p = p, pit = pit });
            if (pit && p.state != "downed")
            {
                p.hp -= 10;
                if (p.hp <= 0) DownPlayer(p);
            }
        }

        void UpdateEncounters()
        {
            // Checkpoints
            for (int i = checkpoint + 1; i < Level.CHECKPOINTS.Length; i++)
            {
                var cp = Level.CHECKPOINTS[i];
                if (players.Exists(p => p.state != "dead" && p.x >= cp.x - 0.5 && p.y >= cp.y - 0.5 && p.onGround))
                {
                    if (i == 1 || arena.state == "cleared" || i > 2) { checkpoint = i; Emit("checkpoint", new Ev { i = i }); }
                }
            }
            // Concourse Lock
            int n = Math.Max(1, players.Count);
            var A = arena;
            if (A.state == "idle" && players.Exists(p => p.state != "dead" && p.x > Level.ARENA_TRIGGER_X && p.x < 96))
            {
                Level.GATES["L"] = true; Level.GATES["R"] = true; A.state = "wave1";
                foreach (var p in players.Live()) if (p.x < 63) { p.x = 64 + p.slot * 0.8; p.y = 0; p.vx = 0; p.vy = 0; }
                var sp = new List<Enemy> { Enemies.CreateEnemy("shield", 88, 0), Enemies.CreateEnemy("shield", 92, 0), Enemies.CreateEnemy("sniper", 94.1, 5.4) };
                if (n >= 3) { sp.Add(Enemies.CreateEnemy("shield", 71, 0)); sp.Add(Enemies.CreateEnemy("sniper", 64.9, 5.4, q => q.facing = 1)); }
                foreach (var e in sp.Live()) { e.zone = "arena"; enemies.Add(e); }
                Emit("banner", new Ev { text = "Concourse Lock", sub = "Gate sealed. Break the lock." });
                Emit("gates", new Ev { closed = true });
            }
            else if (A.state == "wave1")
            {
                int alive = enemies.FindAll(e => e.zone == "arena" && !e.dead).Count;
                if (alive <= 1)
                {
                    A.state = "wave2";
                    int count = n == 1 ? 3 : n == 2 ? 4 : 6;
                    for (int i = 0; i < count; i++)
                    {
                        var e = Enemies.CreateEnemy("swarmer", i % 2 != 0 ? 66 : 94, 0); e.zone = "arena"; e.cd = 20 + i * 12; enemies.Add(e);
                    }
                    var b = Enemies.CreateEnemy("brute", 90, 0); b.zone = "arena"; enemies.Add(b);
                    Emit("banner", new Ev { text = "Wave 2", sub = "The Brute holds the lock." });
                }
            }
            else if (A.state == "wave2")
            {
                if (!enemies.Exists(e => e.zone == "arena" && !e.dead)) StartWarden();
            }
            else if (A.state == "bossReady")
            {
                // After a wipe in the boss fight, walking back in goes straight to the boss
                if (players.Exists(p => p.state != "dead" && p.x > Level.ARENA_TRIGGER_X && p.x < 96))
                {
                    Level.GATES["L"] = true; Level.GATES["R"] = true; Emit("gates", new Ev { closed = true });
                    foreach (var p in players.Live()) if (p.x < 63) { p.x = 64 + p.slot * 0.8; p.y = 0; p.vx = 0; p.vy = 0; }
                    StartWarden();
                }
            }
            else if (A.state == "boss")
            {
                if (!enemies.Exists(e => e.zone == "arena" && !e.dead))
                {
                    A.state = "cleared"; Level.GATES["L"] = false; Level.GATES["R"] = false;
                    Emit("banner", new Ev { text = "Lockwarden destroyed", sub = "Gates open. Storm Spire climb ahead." });
                    Emit("gates", new Ev { closed = false });
                    var act = ActivePlayers();
                    int idx = (int)JMath.Floor(JRandom.Next() * Math.Max(1, act.Count));
                    Bark(idx < act.Count ? act[idx] : null, "lock_broken", 1, true);
                }
            }
            UpdateSkyline(n);
            // Storm Spire climb enemies
            if (!towerSpawned && players.Exists(p => p.x > Level.TOWER_TRIGGER_X && p.x < 162))
            {
                towerSpawned = true;
                var spawns = new (string t, double x, double y)[] { ("swarmer", 122, 5.2), ("swarmer", 134, 11.2), ("drone", 129, 12), ("shield", 152, 15.6), ("drone", 147, 19.5), ("sniper", 158, 15.6) };
                foreach (var (t, x, y) in spawns) { var e = Enemies.CreateEnemy(t, x, y); e.zone = "tower"; enemies.Add(e); }
            }
        }

        // Data-driven encounters (Level.ENCOUNTERS)
        bool Here(double x0, string route) => players.Exists(p => p.state != "dead" && p.state != "downed" && p.x > x0 && Level.RouteAt(p.x).id == route);
        void UpdateSkyline(int n)
        {
            foreach (var S in encounters.Live())
            {
                var E = S.def;
                if (S.state == "idle")
                {
                    if (!Here(E.trigger, E.route)) continue;
                    S.state = "active"; S.wave = 0;
                    if (E.boss != null) S.boss = Bosses.SpawnBoss(this, E.boss, E.bossAtX, E.bossAtY, q => { q.zone = "skyline"; q.enc = E.id; });
                    else SpawnWave(S, n);
                    if (E.gates != null)
                    {
                        foreach (var g in E.gates) Level.GATES[g] = true;
                        // Anyone still outside the gate is brought in
                        foreach (var p in players.Live()) if (p.x < E.inside - 1) { p.x = E.inside + p.slot * 0.8; p.y = Level.GroundBelow(p.x, 40); p.vx = 0; p.vy = 0; p.prevX = p.x; p.prevY = p.y; }
                        Emit("gates", new Ev { closed = true });
                    }
                    Emit("banner", new Ev { text = E.banner[0], sub = E.banner[1] });
                }
                else if (S.state == "active")
                {
                    if (E.boss != null && S.boss != null && S.boss.dead && S.state == "active")
                    {
                        // The boss is down: its drones go with it
                        foreach (var e in enemies.Live()) if (e.enc == E.id && !e.dead && e.add) { e.hp = 0; e.dead = true; e.deathT = 0; Emit("kill", new Ev { x = e.x, y = e.y + e.h / 2, e = e, owner = null }); }
                    }
                    int alive = enemies.FindAll(e => e.enc == E.id && !e.dead).Count;
                    bool last = E.boss != null || S.wave >= E.waves.Length - 1;
                    if (!last && alive <= 1)
                    {
                        S.wave++; SpawnWave(S, n);
                        var b = E.waveBanners != null && S.wave < E.waveBanners.Length ? E.waveBanners[S.wave] : null;
                        if (b != null) Emit("banner", new Ev { text = b[0], sub = b[1] });
                    }
                    else if (last && alive == 0)
                    {
                        S.state = "cleared"; if (E.boss != null) bossClearedT = tick;
                        if (E.gates != null) { foreach (var g in E.gates) Level.GATES[g] = false; Emit("gates", new Ev { closed = false }); }
                        if (E.cleared != null)
                        {
                            Emit("banner", new Ev { text = E.cleared[0], sub = E.cleared[1] });
                            var act = ActivePlayers();
                            int idx = (int)JMath.Floor(JRandom.Next() * Math.Max(1, act.Count));
                            Bark(idx < act.Count ? act[idx] : null, "lock_broken", 1, true);
                        }
                    }
                }
            }
            // A route completes once every encounter on it is won and someone reaches its end
            foreach (var R in Level.ROUTES)
            {
                if (routesDone.Contains(R.id) || !Here(R.endX, R.id) || tick - (bossClearedT ?? -1e9) <= 150) continue;
                if (!encounters.TrueForAll(S => S.def.route != R.id || S.state == "cleared")) continue;
                routesDone.Add(R.id); if (R.id == "skyport") routeDone = true;
                Emit("banner", new Ev { text = "Route complete", sub = R.id == "skyport" ? "You reached the end of the Skyport route." : R.name + " cleared." });
            }
        }

        void SpawnWave(EncounterState S, int n)
        {
            var E = S.def; var list = new List<SpawnDef>(E.waves[S.wave]);
            if (S.wave == 0 && n >= 3 && E.extra != null) list.AddRange(E.extra);
            if (S.wave == 0 && Level.TIER_SPAWNS.TryGetValue(E.id, out var up)) list.AddRange(up);   // (on the upper tiers)
            for (int i = 0; i < list.Count; i++)
            {
                var sd = list[i]; int ii = i;
                enemies.Add(Enemies.CreateEnemy(sd.type, sd.x, sd.y, q => { q.zone = "skyline"; q.enc = E.id; q.cd = 40 + ii * 14; }));
            }
        }
    }
}
