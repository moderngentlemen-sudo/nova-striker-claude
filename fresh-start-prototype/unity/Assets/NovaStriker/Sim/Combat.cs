// Combat resolution (port of combat.js): melee hitboxes, projectiles, barriers, shockwaves, damage and parries.
using System.Collections.Generic;
using static NovaStriker.Sim.Cfg;
using static NovaStriker.Sim.U;

namespace NovaStriker.Sim
{
    public struct Box2 { public double x0, x1, y0, y1; public Box2(double x0, double x1, double y0, double y1) { this.x0 = x0; this.x1 = x1; this.y0 = y0; this.y1 = y1; } }

    public static class Combat
    {
        public static Box2 Hurtbox(Body ent) => new Box2(ent.x - ent.w / 2, ent.x + ent.w / 2, ent.y, ent.y + ent.h);
        static bool Overlap(Hit a, Box2 b) => a.x0 < b.x1 && a.x1 > b.x0 && a.y0 < b.y1 && a.y1 > b.y0;
        static bool CircleBox(double cx, double cy, double r, Box2 b)
        {
            double nx = JMath.Max(b.x0, JMath.Min(cx, b.x1)), ny = JMath.Max(b.y0, JMath.Min(cy, b.y1));
            return JMath.Pow(cx - nx, 2) + JMath.Pow(cy - ny, 2) < r * r;
        }

        public static bool CrossesBarrier(Barrier b, double x0, double y0, double x1, double y1)
        {
            double s0 = (x0 - b.x) * b.nx + (y0 - b.y) * b.ny, s1 = (x1 - b.x) * b.nx + (y1 - b.y) * b.ny;
            if ((s0 > 0) == (s1 > 0)) return false;
            double t = s0 / (s0 - s1);
            double cx = x0 + (x1 - x0) * t, cy = y0 + (y1 - y0) * t;
            return JMath.Abs((cx - b.x) * -b.ny + (cy - b.y) * b.nx) <= b.half;
        }

        public static void ResolveHitboxes(World world)
        {
            foreach (var hb in world.hitboxes.Live())
            {
                world.StrikeBoxes(hb);   // breakable pieces in reach (either side's strikes)
                var set = world.HitSet(hb.instance.Value);
                if (hb.team == "p")
                {
                    for (int i = 0; i < world.enemies.Count; i++)
                    {
                        var e = world.enemies[i];
                        if (e.dead || set.Contains(e.id) || !Overlap(hb, Hurtbox(e))) continue;
                        set.Add(e.id);
                        // Radial hits push each enemy away from their centre. Bursts and pound shockwaves count as blasts.
                        Hit hit = hb;
                        if (hb.radial) { hit = hb.Clone(); hit.kb = new[] { or(sign(e.x - hb.cx), 1) * JMath.Abs(hb.kb[0]), hb.kb[1] }; }
                        string res = HitEnemy(world, e, hit, hb.aegisBurst || hb.scatter || hb.quake ? "blast" : "melee");
                        if (hb.scatter && hb.owner is Player op && (res == "hit" || res == "kill")) op.hitConfirm = true;
                    }
                    // Fix's wrench on one of her gadgets: an upgrade (once a swing)
                    if (hb.wrench && hb.owner != null)
                    {
                        foreach (var g in world.gadgets.Live())
                        {
                            if (g.owner != hb.owner || g.dead || g.kind == "pad" || set.Contains("g" + g.id)) continue;
                            if (!Overlap(hb, new Box2(g.x - 0.45, g.x + 0.45, g.y, g.y + g.h))) continue;
                            set.Add("g" + g.id); world.WrenchGadget(g, (Player)hb.owner);
                        }
                    }
                }
                else
                {
                    foreach (var p in world.players.Live())
                    {
                        if (p.state == "dead" || p.state == "downed" || set.Contains("p" + p.slot)) continue;
                        if (!Overlap(hb, Hurtbox(p))) continue;
                        set.Add("p" + p.slot);
                        // A Bulwark Wall between the striker and the player takes the blow instead
                        var wall = hb.owner != null ? world.WallBetween(hb.owner, p) : null;
                        if (wall != null) { if (!set.Contains("w")) { set.Add("w"); world.HurtWall(wall, hb.dmg, wall.x, p.y + 1); } continue; }
                        HitPlayer(world, p, hb);
                    }
                    foreach (var g in world.gadgets.Live())
                    {
                        if (g.dead || g.kind == "pad" || set.Contains("g" + g.id)) continue;
                        if (!Overlap(hb, new Box2(g.x - 0.4, g.x + 0.4, g.y, g.y + g.h))) continue;
                        set.Add("g" + g.id); world.HurtGadget(g, hb.dmg);
                    }
                }
            }
            world.hitboxes.Clear();
        }

        // ---- Enemies taking hits ----

        static readonly HashSet<string> STATIC_TYPES = new HashSet<string> { "post", "turret", "sniper", "mortar" };

        public static string HitEnemy(World world, Enemy e, Hit hit, string source)
        {
            if (e.dead) return "none";
            var owner = hit.owner; var op = owner as Player;
            double cx = e.x, cy = e.y + e.h * 0.55;
            // A boss arriving or roaring into its second phase shrugs everything off
            if (e.invuln > 0) { world.Emit("blocked", new Ev { x = cx, y = cy, e = e }); return "blocked"; }
            // Veil ambush: the first melee hit after striking from hiding breaks guard and armor and staggers
            bool ambush = source == "melee" && op != null && op.ambushT > 0;
            if (ambush) op.ambushT = 0;

            // Arc blasts come over the top of the shield, so only direct hits are checked against it (and a shield that
            // is staggered, or stunned by Echo's spin, is down)
            if (e.type == "shield" && e.state != "stagger" && !(e.state == "hitstun" && e.dizzy) && source != "blast")
            {
                bool fromFront = source == "proj" ? sign(-hit.vx) == e.shieldDir || JMath.Abs(hit.vx) < 1e-3
                    : sign(owner.x - e.x) == e.shieldDir;
                bool breaks = hit.armorBreak || hit.bulwark || hit.vbTier >= 2 || hit.rail || hit.amplified || hit.ram || ambush;
                if (fromFront && !breaks)
                {
                    e.poise += or(hit.poise, 10) * 0.35;
                    world.Emit("blocked", new Ev { x = cx + e.shieldDir * 0.5, y = cy, e = e });
                    if (source == "melee" && owner != null) { owner.vx = -owner.facing * 3; owner.hitstop = 3; }
                    if (e.poise >= e.poiseMax) Stagger(world, e, 90);
                    return "blocked";
                }
                if (fromFront && breaks) { world.Emit("guardBreak", new Ev { x = cx, y = cy, e = e }); Stagger(world, e, 90); }
            }

            bool fury = op != null && op.furyT > 0;
            if (fury && hit.kb != null && !hit.furied) { hit = hit.Clone(); hit.furied = true; hit.kb = new[] { hit.kb[0] * POWERUPS.fury.kb, hit.kb[1] }; }
            double dmg = hit.dmg * (ambush ? SCARF.ambushDmg : 1) * (fury ? POWERUPS.fury.dmg : 1), poise = hit.poise;
            bool armored = e.armor > 0;
            if (armored)
            {
                if (hit.armorBreak || ambush)
                {
                    e.armor--; armored = e.armor > 0; dmg *= 0.6;
                    world.Emit("armorBreak", new Ev { x = cx, y = cy, e = e, left = e.armor, owner = owner });
                }
                else { dmg *= 0.3; poise *= 0.4; world.Emit("armorHit", new Ev { x = cx, y = cy, e = e }); }
            }
            if (e.tagged > 0) poise *= 1.25;
            if (!double.IsPositiveInfinity(e.hp)) e.hp -= dmg;
            e.poise += poise;
            e.flash = 6;
            if (op != null)
            {
                if (source == "melee") op.hitConfirm = true;
                PlayerSim.OnDealtDamage(op, dmg, source == "melee");
                if (!hit.ult) PlayerSim.GainUlt(op, dmg * ULT.gain.dealt, world);
            }
            bool heavyHit = hit.vbTier >= 2 || hit.armorBreak || hit.rail || poise >= 40;
            if (source == "melee")
            {
                double stop = truthy(hit.vbTier) ? 3 + hit.vbTier * 2 : heavyHit ? 6 : 3;
                if (owner != null) owner.hitstop = JMath.Max(owner.hitstop, stop);
                e.hitstop = stop + 1;
            }
            else e.hitstop = JMath.Max(e.hitstop, 2);
            if (e.boss) e.hitstop = JMath.Min(e.hitstop, 2);   // a combo never freezes a boss in place

            world.Emit("hit", new Ev { x = cx, y = cy, e = e, owner = owner, heavy = heavyHit, tier = hit.vbTier, source = source, dmg = dmg });
            if (hit.vbTier == 3 || (hit.rail && (e.type == "brute" || e.boss))) world.Emit("impact", new Ev { x = cx, y = cy, big = true });

            if (ambush) { world.Emit("ambush", new Ev { x = cx, y = cy, e = e, owner = owner }); world.Bark(op, "ambush", 0.3); }
            if (e.hp <= 0) { Kill(world, e, owner, hit); return "kill"; }
            string T = e.type;
            bool canMove = !STATIC_TYPES.Contains(T);
            if (ambush && T != "post" && T != "turret")
            {
                if (e.state != "stagger") Stagger(world, e, T == "brute" ? 120 : 90);
            }
            else if (hit.scatter && e.light && canMove && !e.flier && !armored)
            {
                // A ground pound's scatter blast throws light enemies outward in an arc
                if (e.state == "windup" || e.state == "aim" || e.state == "lock") world.director.Release(e);
                e.state = "launched"; e.st = 0; e.vy = hit.kb[1]; e.vx = hit.kb[0]; e.poise = 0;
            }
            else if (e.poise >= e.poiseMax)
            {
                Stagger(world, e, T == "brute" ? 120 : T == "shield" ? 90 : 60);
            }
            else if (!armored && e.state != "stagger" && !(e.state == "snared" && !hit.launcher) && (T == "swarmer" || T == "shield" || T == "sniper" || T == "drone"))
            {
                if (e.state == "windup" || e.state == "aim" || e.state == "lock") world.director.Release(e);
                // Drones fly, so they flinch in place instead of being launched
                if (hit.launcher && e.light && canMove && !e.flier)
                {
                    e.state = "launched"; e.st = 0; e.vy = hit.kb[1]; e.vx = hit.kb[0] * 0.3; world.director.Release(e);
                }
                else if (!e.flier && (e.state == "launched" || (!e.onGround && canMove)))
                {
                    e.state = "launched"; e.st = 0; e.vy = JMath.Max(e.vy, 4); e.vx = hit.kb[0] * 0.3;
                }
                else if (e.state != "caught")
                {
                    // (an enemy stunned by Echo's spin keeps what is left of that stun through the follow-up hits)
                    double left = e.state == "hitstun" && e.dizzy ? e.stun - e.st : 0;
                    e.state = "hitstun"; e.st = 0; e.stun = JMath.Max(T == "swarmer" ? 16 : 12, left);
                }
            }
            // Chain lightning holds a light enemy it stuns for longer; everything it touches crackles for a moment
            if (hit.shock)
            {
                e.shockT = JMath.Max(e.shockT, or(hit.stun, 12));
                if (e.light && !armored && !e.boss && e.state == "hitstun") e.stun = JMath.Max(e.stun, hit.stun);
            }
            if (canMove && !armored && hit.kb != null && !e.boss && !hit.well)
            {
                if (e.state != "launched") { e.vx = hit.kb[0]; if (hit.kb[1] > 0) e.vy = JMath.Max(e.vy, hit.kb[1] * 0.6); }
            }
            // RAM's close-range hits throw enemies much further
            if (hit.ramKnock && canMove && !armored && hit.kb != null && !e.boss && !hit.launcher && e.state != "caught" && e.state != "snared" && e.state != "plowed")
            {
                double vx = hit.kb[0] * (e.light ? RAM.knock.light : RAM.knock.heavy);
                if (e.light && !e.flier && JMath.Abs(vx) >= RAM.knock.launchAt)
                {
                    world.director.Release(e);
                    e.state = "launched"; e.st = 0; e.vx = vx; e.vy = hit.kb[1] < 0 ? hit.kb[1] * RAM.knock.light : JMath.Max(e.vy, hit.kb[1] * RAM.knock.light, RAM.knock.lift);
                }
                else e.vx = vx;
            }
            return "hit";
        }

        public static void Stagger(World world, Enemy e, double ticks)
        {
            if (e.boss) { if (e.staggerCd > 0 || e.invuln > 0) return; ticks = 150; e.staggerCd = 420; e.atk = null; }
            world.director.Release(e);
            e.state = "stagger"; e.st = 0; e.stun = ticks; e.poise = 0;
            world.Emit("stagger", new Ev { x = e.x, y = e.y + e.h * 0.6, e = e });
        }

        static void Kill(World world, Enemy e, Actor owner, Hit hit)
        {
            e.dead = true; e.deathT = 0; e.hp = 0;
            world.director.Release(e);
            if (owner is Player op && !(hit != null && hit.ult)) PlayerSim.GainUlt(op, ULT.gain.kill, world);
            world.OnKill(e, owner);
            world.Emit("kill", new Ev { x = e.x, y = e.y + e.h / 2, e = e, owner = owner });
            if (e.boss) world.Emit("bossDown", new Ev { e = e, x = e.x, y = e.y + e.h / 2, owner = owner });
        }

        // ---- Players taking hits ----

        public static string HitPlayer(World world, Player p, Hit hit)
        {
            if (p.state == "dead" || p.state == "downed" || p.state == "ult") return "ignored";
            // Nova's Aegis blocks every attack (unblockables too) for him and anyone inside it
            var guard = world.ShieldFor(p);
            if (guard != null)
            {
                var o = hit.owner;
                V2 from = hit.proj != null ? new V2(hit.proj.x, hit.proj.y) : hit.at != null ? hit.at.Value : o != null ? new V2(o.x, o.y + or(o.h, 1) * 0.5) : new V2(p.x + p.facing, p.y + 1);
                world.AbsorbAegis(guard, hit.dmg * Diff.dmg, from.x, from.y, hit.instance != null ? "i" + hit.instance.Value : null);
                // Heavy melee rebounds off hard light: a charging Charger is dazed as if it hit a wall
                if (o is Enemy oe && hit.proj == null && hit.heavy)
                {
                    oe.poise += 30;
                    if (oe.state == "charge") { oe.state = "dazed"; oe.st = 0; oe.vx = -oe.facing * 4; world.Emit("chargeCrash", new Ev { e = oe }); }
                    if (oe.boss && oe.state == "dive") oe.parried = 2;   // a diving Stormcaller crashes off the hard light
                }
                return "shielded";
            }
            var attacker = hit.owner; var ae = attacker as Enemy;
            bool unblockable = hit.cat == "unblockable" || hit.unblockable;
            double posx = p.x, posy = p.y + p.h * 0.6;
            // RAM's Rampart: a strike, a blast or a shot from in front of it is blocked (shockwaves along the floor go under it)
            if (p.@char == "ram" && p.state == "guard" && !p.guardBroken && !hit.ground)
            {
                V2? src = hit.proj != null ? new V2(hit.proj.x, hit.proj.y) : hit.at != null ? hit.at : attacker != null ? new V2(attacker.x, attacker.y + or(attacker.h, 1) * 0.5) : (V2?)null;
                if (src != null && world.GuardFaces(p, src.Value.x, src.Value.y))
                {
                    bool perfect = p.guardT <= RAM.guard.perfect;
                    if (ae != null && hit.proj == null)
                    {
                        if (perfect)
                        {
                            if (ae.boss) ae.parried = 2;
                            ae.poise += 60; ae.hitstop = 8;
                            if (ae.poise >= ae.poiseMax) Stagger(world, ae, ae.type == "brute" ? 120 : 80);
                            else if (ae.state == "attack") { ae.state = "recover"; ae.st = 0; }
                        }
                        // The shield stops a charge dead
                        if (ae.state == "charge") { ae.state = "dazed"; ae.st = 0; ae.vx = -ae.facing * 4; world.Emit("chargeCrash", new Ev { e = ae }); }
                    }
                    world.GuardResult(p, perfect ? 0 : hit.dmg * Diff.dmg, perfect, src.Value.x, src.Value.y, hit.heavy || unblockable);
                    return "guarded";
                }
            }

            PlayerSim.ParryWindows(p, out double winW, out double winP);
            if (p.state == "parry" && p.parryResult == null && p.parryT <= winW)
            {
                if (unblockable)
                {
                    world.Emit("parryFail", new Ev { x = posx, y = posy });
                }
                else
                {
                    bool perfect = p.parryT <= winP;
                    p.parryResult = perfect ? "perfect" : "normal"; p.st = 0;
                    p.hitstop = perfect ? 6 : 4;
                    if (hit.heavy && !perfect)
                    {
                        p.hp -= hit.dmg * 0.3 * Diff.dmg;
                        p.vx = -p.facing * 5;
                        if (p.hp <= 0) { world.DownPlayer(p); return "hit"; }
                    }
                    if (ae != null && hit.proj == null)
                    {
                        if (ae.boss) ae.parried = perfect ? 2 : 1;   // bosses react on their next tick
                        ae.poise += perfect ? 60 : 25;
                        ae.hitstop = perfect ? 8 : 4;
                        if (ae.poise >= ae.poiseMax) Stagger(world, ae, ae.type == "brute" ? 120 : 80);
                        else if (perfect && ae.state == "attack") { ae.state = "recover"; ae.st = 0; }
                        else if (perfect && ae.state == "charge") { ae.state = "dazed"; ae.st = 0; ae.vx = -ae.facing * 3; }   // a parried Charger reels
                    }
                    if (perfect)
                    {
                        if (p.@char == "nova")
                        {
                            p.bulwarkCd = JMath.Max(0, p.bulwarkCd - 120);
                            foreach (var e in world.enemies.Live())
                            {
                                if (e.dead || e.type == "post" || e.type == "turret") continue;
                                if (JMath.Abs(e.x - p.x) < 2.4 && JMath.Abs(e.y - p.y) < 2) e.vx = sign(e.x - p.x) * 8;
                            }
                        }
                        else
                        {
                            p.riposteT = 20; PlayerSim.AddResolve(p, 20); p.cells = JMath.Min(ECHO.cellsMax, p.cells + 2);
                        }
                    }
                    else PlayerSim.AddResolve(p, 10);
                    if (perfect) PlayerSim.GainUlt(p, ULT.gain.perfect, world);
                    world.Emit("parry", new Ev { x = posx, y = posy, p = p, perfect = perfect, heavy = hit.heavy });
                    if (perfect) world.Bark(p, "perfect", 0.35);
                    return "parried";
                }
            }

            // Nova's dodge: an attack that reaches him in its opening ticks is a perfect dodge
            if (p.state == "dodge" && p.dodge != null && !p.dodge.perfect && p.dodge.t <= DODGE.perfect) world.PerfectDodge(p);
            if (p.mercy > 0 || p.iframe) return "ignored";
            double dmg = hit.dmg * Diff.dmg;
            bool armoredP = p.@char == "echo" && p.state == "attack" && p.moveId == "echo_charged" && p.resolve >= ECHO.resolveHalf;
            // RAM braced by Provoke, or mid-charge, takes less
            if (p.@char == "ram") { if (p.braceT > 0) dmg *= RAM.provoke.brace; if (p.state == "rush" || p.state == "leap") dmg *= RAM.rushTaken; }
            // A RAM's Guardian Link takes his share; then Plating soaks what it can
            var guardian = world.GuardianOf(p);
            if (guardian != null) { double share = dmg * RAM.link.share; dmg -= share; world.LinkHit(guardian, share, p); }
            if (p.plate > 0) { double a = JMath.Min(p.plate, dmg); p.plate -= a; dmg -= a; if (a > 0) world.Emit("plateHit", new Ev { p = p, x = posx, y = posy, left = p.plate }); }
            // Stalwart: ordinary hits don't knock RAM about (heavy ones and blasts do); mid-charge or mid-leap nothing does
            bool stalwart = p.@char == "ram" && (p.state == "rush" || p.state == "leap" || (!hit.heavy && !unblockable));
            p.hp -= dmg;
            PlayerSim.GainUlt(p, dmg * ULT.gain.taken, world);
            PlayerSim.BreakVeil(p, world, "hit");
            if (p.@char == "echo")
            {
                p.strain = JMath.Min(p.maxHp - JMath.Max(0, p.hp), p.strain + dmg * 0.5); p.strainT = 300;
                if (world.NearestEnemyDist(p.x, p.y + 1) < 4 && world.tick - p.lastResolveHitT > 30)
                {
                    PlayerSim.AddResolve(p, 8); p.lastResolveHitT = world.tick;
                }
            }
            else if (!stalwart) { p.chargeT = 0; if (p.focus > 0) PlayerSim.LoseFocus(p, world); }
            if (!stalwart) { p.rifleT = 0; p.dashChargeT = 0; p.burstT = 0; p.subArmed = false; p.dodge = null; p.rivetQ = 0; p.tossArmed = false; }
            p.mercy = MERCY_TICKS; p.hitstop = stalwart ? 2 : 4;
            world.Emit("playerHit", new Ev { x = posx, y = posy, p = p, dmg = dmg, heavy = hit.heavy, armored = armoredP || stalwart });
            if (p.hp <= 0) { p.hp = 0; world.DownPlayer(p); return "hit"; }
            if (!armoredP && !stalwart)
            {
                if (p.beam != null) world.EndBeam(p, "hit");
                p.state = "hitstun"; p.st = 0; p.stun = hit.heavy || unblockable ? 24 : 14;
                p.vx = hit.kb != null ? hit.kb[0] : 0; p.vy = hit.kb != null ? hit.kb[1] : 3;
                p.dash = null; p.lash = null; p.zip = null; p.meleeCharged = false;
            }
            return "hit";
        }

        // ---- Projectiles, barriers, shockwaves ----

        // Minimum distance between two points moving linearly over the same tick
        static double SweptDistance(Projectile a, Projectile b)
        {
            double r0x = a.px - b.px, r0y = a.py - b.py;
            double dx = (a.x - b.x) - r0x, dy = (a.y - b.y) - r0y;
            double dd = dx * dx + dy * dy;
            double t = dd > 1e-9 ? JMath.Max(0, JMath.Min(1, -(r0x * dx + r0y * dy) / dd)) : 0;
            return JMath.Hypot(r0x + dx * t, r0y + dy * t);
        }

        // {...pr, kb}: the hit a projectile lands
        static Projectile AsHit(Projectile pr, double kx, double ky)
        {
            var h = (Projectile)pr.Clone(); h.kb = new[] { kx, ky }; return h;
        }

        static void ProjectileHits(World world, Projectile pr)
        {
            if (pr.team == "p")
            {
                for (int i = 0; i < world.enemies.Count; i++)
                {
                    var e = world.enemies[i];
                    if (e.dead || pr.hitSet.Contains(e.id) || !CircleBox(pr.x, pr.y, pr.r, Hurtbox(e))) continue;
                    pr.hitSet.Add(e.id);
                    if (pr.snare) { world.ApplySnare(e, (Player)pr.owner); pr.dead = true; return; }   // snares wrap around shields
                    if (pr.stick != null)
                    {   // a Hot Rivet: it hits, then stays stuck in the enemy until its fuse runs out
                        HitEnemy(world, e, AsHit(pr, sign(pr.vx) * or(pr.kbs, 2), 1), "proj");
                        pr.stuck = new StuckInfo { e = e, ox = pr.x - e.x, oy = pr.y - e.y, t = pr.stick.fuse }; pr.vx = 0; pr.vy = 0;
                        world.Emit("rivetStick", new Ev { p = pr.owner as Player, x = pr.x, y = pr.y, e = e });
                        return;
                    }
                    if (pr.blast != null) { Detonate(world, pr, pr.x, pr.y, null, false); pr.dead = true; return; }
                    var hit = AsHit(pr, sign(pr.vx) * or(pr.kbs, 2), or(pr.kbY, 1));
                    if (pr.falloff != null)
                    {   // secondary blaster pellets lose damage over distance
                        double f = JMath.Max(0.25, 1 - JMath.Hypot(pr.x - pr.falloff.x, pr.y - pr.falloff.y) / pr.falloff.d);
                        hit.dmg *= f; hit.poise *= f; hit.kb[0] *= f;
                    }
                    string res = HitEnemy(world, e, hit, "proj");
                    if (pr.disc != null)
                    {   // the disc cuts on through; a shield or a boss's guard turns it for home
                        if (res == "blocked" && pr.disc.phase == "out") { DiscTurn(pr, "back"); world.Emit("ricochet", new Ev { x = pr.x, y = pr.y, pr = pr }); }
                        continue;
                    }
                    if (res == "hit" || res == "kill") AwardFocus(world, pr.owner, pr.family, pr.kind);
                    if (pr.tracer || pr.mark) { e.tagged = JMath.Max(e.tagged, 600); world.Emit("tag", new Ev { x = e.x, y = e.y + e.h, e = e }); }
                    if (pr.splash != null) Detonate(world, pr, pr.x, pr.y, e, false);   // splash reaches the enemies around this one
                    if (pr.prism != null)
                    {   // splits on impact; a blocked prism glances back off the shield
                        world.SplitPrism(pr, pr.x, pr.y, res == "blocked" ? -pr.vx : pr.vx, pr.vy, e);
                        pr.dead = true; return;
                    }
                    if (!pr.pierce || res == "blocked") { pr.dead = true; return; }
                    if (pr.pierceLeft != null) { double left = pr.pierceLeft.Value; pr.pierceLeft = left - 1; if (left <= 0) { pr.dead = true; return; } }   // a Breach Shot goes through so many
                }
            }
            else
            {
                foreach (var p in world.players.Live())
                {
                    if (p.state == "dead" || p.state == "downed") continue;
                    if (CanDeflect(p, pr)) { world.Deflect(p, pr); return; }
                    if (!CircleBox(pr.x, pr.y, pr.r, Hurtbox(p))) continue;
                    if (pr.blast != null) { Detonate(world, pr, pr.x, pr.y, null, false); pr.dead = true; return; }   // mortar shells burst on contact
                    string res = HitPlayer(world, p, new Hit
                    {
                        dmg = pr.dmg, heavy = pr.heavy, cat = pr.heavy ? "heavy" : "standard",
                        kb = new[] { sign(pr.vx) * 6, 3.0 }, owner = pr.owner, proj = pr,
                    });
                    if (res != "ignored") { pr.dead = true; return; }
                }
            }
        }

        // Echo (Hunter kit) knocks back an enemy shot that reaches his staff
        static bool CanDeflect(Player p, Projectile pr)
        {
            if (p.@char != "echo" || SETTINGS.echoKit != "hunter" || pr.blast != null || pr.team != "e") return false;
            double cx = p.x, cy = p.y + p.h * 0.6, d = JMath.Hypot(pr.x - cx, pr.y - cy);
            if (p.state == "parry" && p.parryT <= DEFLECT.window) return d < DEFLECT.reach + pr.r;
            var m = p.move;
            if (p.state == "attack" && m != null && m.deflect && p.st >= m.su - 1 && p.st <= m.su + m.ac + 1)
            {
                bool front = m.spin || (pr.x - cx) * p.facing > -0.3;
                return front && d < DEFLECT.reach + 0.7 + pr.r;
            }
            return false;
        }

        // Marksman kit: the first piece of a charged release to land earns Focus for the whole shot (2 on a Perfect
        // Release); every basic round that lands earns a little
        public static void AwardFocus(World world, Actor owner, Family family, string kind)
        {
            var p = owner as Player;
            if (p == null || p.@char != "nova") return;
            if (family != null)
            {
                if (!family.focused) { family.focused = true; PlayerSim.GainFocus(p, family.perfect ? 2 : 1, world); }
            }
            else if (kind == "shot") PlayerSim.GainFocus(p, MARKSMAN.focus.perRound, world);
        }

        // Where a projectile bursts. Arc shells and mortar shells explode; Nova's other shots splash.
        static void Detonate(World world, Projectile pr, double x, double y, Enemy skip, bool onTerrain)
        {
            ExplodeArgs Common(Blast spec, string kind, bool rocket = false) => new ExplodeArgs
            {
                owner = pr.owner, team = pr.team, x = x, y = y, level = pr.level, perfect = pr.perfect, family = pr.family, skip = skip, spec = spec, kind = kind, rocket = rocket,
            };
            // RAM's level 3 Breach Shot bursts where it ends; Fix's Hot Rivets burst when their fuse runs out
            if (pr.endBlast != null) world.Explode(Common(pr.endBlast, "breachBlast"));
            if (pr.stick != null) { world.Explode(Common(pr.stick.blast, "rivetBlast")); return; }
            if (pr.blast != null) world.Explode(Common(pr.blast, pr.kind == "grenade" || pr.kind == "bomblet" ? "frag" : "blast", true));
            else if (pr.splash != null) world.Explode(Common(pr.splash, "splash", onTerrain));
            if (pr.cluster != null) world.ClusterBurst(pr, x, y);   // a level 3 grenade scatters bomblets
        }

        // Walls: shards ricochet while they have bounces left, shells burst, other shots splash, prisms split off the
        // surface. Returns true when the projectile is gone.
        static bool HitWall(World world, Projectile pr, double ox, double oy)
        {
            // A breakable piece takes the shot's damage (enemy fire wears cover down too); blasting shells do it in Explode
            var bk = pr.blast == null ? Level.BreakableAt(pr.x, pr.y, 0.05) : null;
            if (bk != null) world.DamageBox(bk, JMath.Max(0.5, pr.dmg) * (pr.heavy ? 2 : 1), pr.x, pr.y, pr.owner as Player);
            bool fx = Level.PointInSolid(pr.x, oy), fy = Level.PointInSolid(ox, pr.y);
            bool flipX = fx || !fy, flipY = fy || !fx;
            if (pr.disc != null)
            {
                // The disc glances off terrain on its way out and turns for home (hovering first if it would)
                pr.x = ox; pr.y = oy;
                if (pr.disc.phase == "out") DiscTurn(pr, pr.disc.hover > 0 ? "hover" : "back");
                else if (pr.disc.phase == "hover") { pr.vx = 0; pr.vy = 0; }
                world.Emit("ricochet", new Ev { x = ox, y = oy, pr = pr });
                return false;
            }
            if (truthy(pr.bouncy)) { Bounce(world, pr, ox, oy, flipX, flipY); return false; }
            if (pr.stick != null)
            {   // a Hot Rivet sticks in the wall where it struck
                pr.x = ox; pr.y = oy; pr.vx = 0; pr.vy = 0; pr.stuck = new StuckInfo { e = null, ox = ox, oy = oy, t = pr.stick.fuse };
                world.Emit("rivetStick", new Ev { p = pr.owner as Player, x = ox, y = oy, e = null });
                return true;
            }
            if (pr.bounces > 0)
            {
                pr.x = ox; pr.y = oy; if (flipX) pr.vx = -pr.vx; if (flipY) pr.vy = -pr.vy; pr.bounces--;
                world.Emit("ricochet", new Ev { x = ox, y = oy, pr = pr });
                return false;
            }
            Detonate(world, pr, ox, oy, null, true);
            if (pr.prism != null) world.SplitPrism(pr, ox, oy, flipX ? -pr.vx : pr.vx, flipY ? -pr.vy : pr.vy, null);
            else if (pr.snare) world.SnareLanded(pr, ox, oy);
            pr.dead = true; world.Emit("projWall", new Ev { x = pr.x, y = pr.y, pr = pr });
            return true;
        }

        // End of a projectile's time: shells burst in the air, prisms split forward
        static void Expire(World world, Projectile pr)
        {
            if (pr.blast != null) Detonate(world, pr, pr.x, pr.y, null, false);
            else if (pr.prism != null) world.SplitPrism(pr, pr.x, pr.y, pr.vx, pr.vy, null);
            pr.dead = true;
        }

        // A grenade bounces off terrain, keeping `bouncy` of its speed; on a floor with little speed left it comes to
        // rest and rolls to a stop
        static void Bounce(World world, Projectile pr, double ox, double oy, bool flipX, bool flipY)
        {
            pr.x = ox; pr.y = oy;
            double sp = JMath.Hypot(pr.vx, pr.vy);
            if (flipX) pr.vx = -pr.vx * pr.bouncy;
            if (flipY)
            {
                bool floor = pr.vy < 0;
                pr.vy = -pr.vy * pr.bouncy; pr.vx *= SUB.grenade.roll;
                if (floor && pr.vy < 2.4) { pr.vy = 0; pr.rest = true; double g = Level.GroundBelow(pr.x, oy + 0.05); if (g > double.NegativeInfinity && oy - g < 0.5) pr.y = g + pr.r; }
            }
            if (sp > 3) world.Emit("bounce", new Ev { x = ox, y = oy, pr = pr, sp = sp });
        }
        // The top of a one-way platform crossed going down between two heights, if any (grenades land on them)
        static double? OneWayTop(double x, double y0, double y1)
        {
            foreach (var b in Level.BOXES) if (b.type == 'o' && x > b.x0 && x < b.x1 && y0 >= b.y1 - 0.02 && y1 < b.y1) return b.y1;
            return null;
        }

        public static void UpdateProjectiles(World world, bool frozen = false)
        {
            var list = world.projectiles;
            for (int i = 0; i < list.Count; i++)
            {
                var pr = list[i];
                if (pr.dead) continue;
                pr.px = pr.x; pr.py = pr.y;
                if (frozen && pr.team == "e") continue;   // an ultimate holds enemy fire in the air
                if (pr.stuck != null)
                {
                    // A stuck Hot Rivet rides along in its enemy (or sits in the wall) until the fuse is out
                    var S = pr.stuck;
                    if (S.e != null) { if (!S.e.dead) { pr.x = S.e.x + S.ox; pr.y = S.e.y + JMath.Min(S.oy, S.e.h); } }
                    else { pr.x = S.ox; pr.y = S.oy; }
                    if (--S.t <= 0) { Detonate(world, pr, pr.x, pr.y, null, false); pr.dead = true; }
                    continue;
                }
                // A perfect dodge slows enemy shots close by
                double k = 1;
                if (pr.slowT > 0) { pr.slowT--; k = 0.5; }
                if (pr.homing) SteerToTagged(world, pr);
                if (pr.seek != null) SteerDart(world, pr);
                if (pr.disc != null) { SteerDisc(world, pr); if (pr.dead) continue; }
                if (pr.rest)
                {
                    // A grenade at rest rolls to a stop, and falls again if the floor goes
                    pr.vx *= 0.8; pr.vy = 0;
                    if (!Level.PointInSolid(pr.x, pr.y - pr.r - 0.08) && OneWayTop(pr.x, pr.y, pr.y - pr.r - 0.08) == null) pr.rest = false;
                }
                else if (truthy(pr.gravity)) pr.vy -= pr.gravity * DT * k;
                pr.ttl--;
                if (pr.ttl <= 0) { Expire(world, pr); continue; }
                // Anything that leaves the level is gone
                if (pr.x < Level.LEVEL_X0 - 2 || pr.x > Level.LEVEL_X1 + 2 || pr.y < Level.KillYAt(pr.x) - 6 || pr.y > 90) { pr.dead = true; continue; }
                // Sub-step so fast shots cannot skip over thin targets or walls
                int steps = (int)JMath.Max(1, JMath.Ceil(JMath.Hypot(pr.vx, pr.vy) * DT * k / 0.3));
                for (int s = 0; s < steps && !pr.dead; s++)
                {
                    double ox = pr.x, oy = pr.y;
                    pr.x += pr.vx * DT * k / steps; pr.y += pr.vy * DT * k / steps;
                    if (truthy(pr.bouncy) && pr.vy < 0)
                    {
                        var top = OneWayTop(pr.x, oy, pr.y);
                        if (top != null) { Bounce(world, pr, pr.x, top.Value + 0.01, false, true); continue; }
                    }
                    if (!pr.ghost && Level.PointInSolid(pr.x, pr.y))
                    {
                        if (HitWall(world, pr, ox, oy)) break;
                        continue;
                    }
                    if (pr.team == "e")
                    {
                        var guard = world.AegisAt(pr.x, pr.y, pr.r);
                        if (guard != null)
                        {
                            // Stopped at the hard light: a shell bursts on it, everything else is absorbed
                            world.AbsorbAegis(guard, pr.blast != null ? pr.blast.dmg : pr.dmg, pr.x, pr.y, null);
                            if (pr.blast != null) world.Emit("enemyBlast", new Ev { x = pr.x, y = pr.y, r = pr.blast.r * 0.6 });
                            pr.dead = true; break;
                        }
                    }
                    if (pr.team == "e")
                    {
                        // RAM's Rampart stops it, covering everyone behind him (a Perfect Guard sends it back as his)
                        var ram = world.RampartCross(ox, oy, pr.x, pr.y, pr.r);
                        if (ram != null) { if (world.BlockShot(ram, pr) == "block") break; continue; }
                        // Fix's gadgets stand in the way of shots too
                        var g = world.GadgetAt(pr.x, pr.y, pr.r);
                        if (g != null) { world.HurtGadget(g, pr.blast != null ? pr.blast.dmg : pr.dmg); if (pr.blast != null) world.Emit("enemyBlast", new Ev { x = pr.x, y = pr.y, r = pr.blast.r * 0.6 }); pr.dead = true; break; }
                    }
                    foreach (var b in world.barriers.Live())
                    {
                        if (!CrossesBarrier(b, ox, oy, pr.x, pr.y)) continue;
                        if (pr.team == "e")
                        {
                            pr.dead = true; world.Emit("barrierBlock", new Ev { x = pr.x, y = pr.y, kind = b.kind });
                            if (b.kind == "rampart") world.HurtWall(b, pr.blast != null ? pr.blast.dmg : pr.dmg, pr.x, pr.y);
                            break;
                        }
                        if (!pr.amplified)
                        {
                            pr.amplified = true; pr.pierce = true; pr.dmg *= 1.5; pr.poise = or(pr.poise, 8) * 1.5; pr.r *= 1.3;
                            if (pr.blast != null) { pr.blast.dmg *= 1.5; pr.blast.poise *= 1.5; pr.blast.r *= 1.2; }
                            world.Emit("amplify", new Ev { x = pr.x, y = pr.y, pr = pr });
                        }
                    }
                    if (!pr.dead) ProjectileHits(world, pr);
                }
            }
            // Nova's shots intercept hostile projectiles; heavy ones need a charged shot (not darts, shards or pellets)
            for (int i = 0; i < list.Count; i++)
            {
                var a = list[i];
                if (a.dead || a.team != "p" || !a.intercept) continue;
                for (int j = 0; j < list.Count; j++)
                {
                    var b = list[j];
                    if (b.dead || b.team != "e") continue;
                    if (SweptDistance(a, b) > a.r + b.r + 0.25) continue;
                    if (b.heavy && !(a.interceptHeavy ?? truthy(a.level))) { a.dead = true; world.Emit("interceptFail", new Ev { x = a.x, y = a.y }); break; }
                    b.dead = true; if (!a.pierce) a.dead = true;
                    var saved = world.ProjectileTarget(b, a.owner as Player);
                    world.Emit("intercept", new Ev { x = b.x, y = b.y, owner = a.owner, heavy = b.heavy, saved = saved });
                    if (a.dead) break;
                }
            }
            world.projectiles.RemoveAll(pr => pr.dead);
        }

        // Nova's disc: out along the throw, a hover there from level 2, then home to his chest
        static void DiscTurn(Projectile pr, string phase)
        {
            pr.disc.phase = phase; pr.disc.t = 0; pr.hitSet.Clear();
            if (phase == "back") pr.ghost = true;
        }
        static void SteerDisc(World world, Projectile pr)
        {
            var s = pr.disc; var o = pr.owner as Player; s.t++;
            if (o == null || !world.players.Contains(o) || o.state == "dead" || o.state == "downed" || o.@char != "nova")
            {
                pr.dead = true; world.Emit("discFade", new Ev { x = pr.x, y = pr.y }); return;
            }
            if (s.phase == "out")
            {
                double f = 1 - 0.65 * JMath.Max(0, (s.t - s.@out * 0.55) / (s.@out * 0.45));
                pr.vx = s.dx * s.speed * f; pr.vy = s.dy * s.speed * f;
                if (s.t >= s.@out) DiscTurn(pr, s.hover > 0 ? "hover" : "back");
            }
            else if (s.phase == "hover")
            {
                pr.vx *= 0.6; pr.vy *= 0.6;
                if (s.t % SUB.disc.tick == 0) pr.hitSet.Clear();
                if (s.t >= s.hover) DiscTurn(pr, "back");
            }
            else
            {
                var c = PlayerSim.Chest(o); double dx = c.x - pr.x, dy = c.y - pr.y, d = or(JMath.Hypot(dx, dy), 1), sp = JMath.Min(SUB.disc.back + s.t * 0.6, 44);
                pr.vx = dx / d * sp; pr.vy = dy / d * sp;
                if (d < 0.8 + sp * DT) { pr.dead = true; world.Emit("discCatch", new Ev { p = o, x = c.x, y = c.y }); }
                else if (s.t > SUB.disc.maxBack) pr.dead = true;
            }
        }

        // Volley darts fly straight for a moment so the fan opens, then turn toward their target
        static void SteerDart(World world, Projectile pr)
        {
            var s = pr.seek;
            if (++s.age < s.delay || s.age > s.until) return;   // after `until` ticks a dart flies straight on
            var t = s.target;
            if (t == null || t.dead)
            {
                t = null; double bd = 10;
                double sp0 = or(JMath.Hypot(pr.vx, pr.vy), 1);
                foreach (var e in world.enemies.Live())
                {
                    if (e.dead || pr.hitSet.Contains(e.id)) continue;
                    double dx = e.x - pr.x, dy = e.y + e.h / 2 - pr.y, d = JMath.Hypot(dx, dy);
                    if (d < bd && (dx * pr.vx + dy * pr.vy) / (d * sp0) > 0) { bd = d; t = e; }
                }
                s.target = t;
                if (t == null) return;
            }
            double sp = JMath.Hypot(pr.vx, pr.vy), a = JMath.Atan2(pr.vy, pr.vx);
            double da = JMath.Atan2(t.y + t.h / 2 - pr.y, t.x - pr.x) - a;
            while (da > JMath.PI) da -= 2 * JMath.PI; while (da < -JMath.PI) da += 2 * JMath.PI;
            double na = a + JMath.Max(-s.turn, JMath.Min(s.turn, da));
            pr.vx = JMath.Cos(na) * sp; pr.vy = JMath.Sin(na) * sp;
        }

        // Homing shots curve toward tagged enemies, and toward the shooter's lock-on target
        static void SteerToTagged(World world, Projectile pr)
        {
            Enemy best = null; double bd = 14;
            double sp = JMath.Hypot(pr.vx, pr.vy), dx0 = pr.vx / sp, dy0 = pr.vy / sp; var lockT = (pr.owner as Player)?.lockT;
            foreach (var e in world.enemies.Live())
            {
                if (e.dead || (e.tagged <= 0 && e != lockT)) continue;
                double dx = e.x - pr.x, dy = e.y + e.h / 2 - pr.y, d = JMath.Hypot(dx, dy);
                if (d < bd && (dx * dx0 + dy * dy0) / d > 0.5) { bd = d; best = e; }
            }
            if (best == null) return;
            double ta = JMath.Atan2(best.y + best.h / 2 - pr.y, best.x - pr.x), a = JMath.Atan2(pr.vy, pr.vx);
            double da = ta - a; while (da > JMath.PI) da -= 2 * JMath.PI; while (da < -JMath.PI) da += 2 * JMath.PI;
            double na = a + JMath.Max(-0.06, JMath.Min(0.06, da));
            pr.vx = JMath.Cos(na) * sp; pr.vy = JMath.Sin(na) * sp;
        }

        public static void UpdateShockwaves(World world)
        {
            foreach (var s in world.shockwaves.Live())
            {
                s.x += s.dir * s.speed * DT; s.ttl--;
                if (Level.PointInSolid(s.x + s.dir * 0.5, s.y + 0.3))
                {
                    var bk = Level.BreakableAt(s.x + s.dir * 0.5, s.y + 0.3);
                    if (bk != null) world.DamageBox(bk, or(s.dmg, 4) * 2, s.x + s.dir * 0.5, s.y + 0.4, s.owner as Player);
                    s.ttl = 0;
                }
                if (s.team == "p") world.SpawnHitbox(new Hit
                {
                    owner = s.owner, team = "p", x0 = s.x - 0.5, x1 = s.x + 0.5, y0 = s.y, y1 = s.y + s.h,
                    dmg = s.dmg, poise = s.poise, kb = new[] { s.dir * 6, 9.0 }, launcher = true, instance = s.instance, quake = true,
                });
                else world.SpawnHitbox(new Hit
                {
                    owner = s.owner, team = "e", x0 = s.x - 0.45, x1 = s.x + 0.45, y0 = s.y, y1 = s.y + s.h,
                    dmg = s.dmg, kb = new[] { s.dir * 8, 7.0 }, unblockable = true, cat = "unblockable", instance = s.instance, ground = true,
                });
            }
            world.shockwaves.RemoveAll(s => s.ttl <= 0);
        }
    }
}
