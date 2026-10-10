// World (port of world.js): owns every entity, runs the fixed-tick simulation, and emits events for rendering,
// audio and the interface. Nothing in here touches Unity.
using System;
using System.Collections.Generic;
using static NovaStriker.Sim.Cfg;
using static NovaStriker.Sim.U;
using static NovaStriker.Sim.PlayerSim;
using static NovaStriker.Sim.Combat;

namespace NovaStriker.Sim
{
    public sealed class ExplodeArgs
    {
        public Actor owner;
        public string team = "p";
        public double x, y;
        public Blast spec;
        public double level;
        public bool perfect;
        public Family family;
        public Enemy skip;
        public bool rocket;
        public string kind = "splash";
    }

    // Who may attack at once: a few melee and ranged tokens, shared out among the enemies
    public sealed class Director
    {
        readonly World world;
        readonly HashSet<Enemy> melee = new HashSet<Enemy>(), ranged = new HashSet<Enemy>();
        public Director(World w) { world = w; }
        HashSet<Enemy> Pool(string pool) => pool == "melee" ? melee : ranged;
        public double Cap(string pool)
        {
            int n = Math.Max(1, world.players.FindAll(p => p.state != "dead").Count);
            double d = Diff.tokens;
            // Flare's cost: one more enemy may commit to a melee attack at a time
            double flare = world.players.Exists(p => p.@char == "echo" && p.scarfMode == "flare" && p.state != "dead" && p.state != "downed") ? 1 : 0;
            return pool == "melee" ? JMath.Max(1, 2 + (n - 1) + d + flare) : n >= 3 ? 2 : 1;
        }
        public bool Request(Enemy e, string pool)
        {
            if (e.token != null) return true;
            if (Pool(pool).Count < Cap(pool)) { Pool(pool).Add(e); e.token = pool; return true; }
            return false;
        }
        public void Release(Enemy e) { if (e.token != null) { Pool(e.token).Remove(e); e.token = null; } }
        public void Reset() { melee.Clear(); ranged.Clear(); }
        public int MeleeUsed => melee.Count;
        public int RangedUsed => ranged.Count;
    }

    public sealed partial class World
    {
        // How tall each of Fix's gadgets stands (what enemy shots and strikes can hit)
        static double GadgetH(string kind) => kind == "pylon" ? 1.3 : kind == "sentry" ? 0.95 : kind == "coil" ? 1.6 : 0.3;
        // Height gained by a body launched upward at v with no rise cut, as the fixed-tick integration plays it out
        public static double ApexGain(double v) => JMath.Max(0, v * v / (2 * GRAVITY) - v * DT / 2);

        // Placeholder lines. Not canon; written only to test the feel.
        static readonly Dictionary<string, Dictionary<string, string[]>> BARKS = new Dictionary<string, Dictionary<string, string[]>>
        {
            ["nova"] = new Dictionary<string, string[]>
            {
                ["intercept_save"] = new[] { "Got that one.", "Covered." }, ["saved_reply"] = new[] { "Thanks. Eyes up." },
                ["perfect"] = new[] { "Denied.", "Not today." }, ["revive"] = new[] { "Up. I have you.", "On your feet." },
                ["revived"] = new[] { "Thanks.", "Noted." }, ["lash_reply"] = new[] { "Show-off.", "I had him." },
                ["lash_save"] = new[] { "Over here." }, ["lock_broken"] = new[] { "Lock's open. Move." },
                ["challenge_reply"] = new[] { "Don't make a habit of that.", "I see them. Covering you." },
                ["perfect_shot"] = new[] { "Textbook.", "Right on the mark.", "Clean." },
            },
            ["echo"] = new Dictionary<string, string[]>
            {
                ["intercept_save"] = new[] { "Got it!" }, ["saved_reply"] = new[] { "I had it.", "Didn't need that." },
                ["perfect"] = new[] { "Too slow.", "Again!" }, ["revive"] = new[] { "Come on, get up.", "Not like this. Up." },
                ["revived"] = new[] { "I was fine.", "Thanks." }, ["lash_reply"] = new[] { "Nice pull." },
                ["lash_save"] = new[] { "Mine now!", "Over here!" }, ["lock_broken"] = new[] { "That's how it's done." },
                ["challenge"] = new[] { "Eyes on me!", "Come on, all of you!" }, ["ambush"] = new[] { "Missed me?", "Right behind you." },
            },
            ["ram"] = new Dictionary<string, string[]>
            {
                ["intercept_save"] = new[] { "Behind me." }, ["saved_reply"] = new[] { "Appreciated.", "Good eye." },
                ["perfect"] = new[] { "Not getting through." }, ["revive"] = new[] { "Up. Stay behind me.", "On your feet. I hold the line." },
                ["revived"] = new[] { "Still standing.", "Back to the front." }, ["lash_reply"] = new[] { "Good pull." }, ["lash_save"] = new[] { "Hold on." }, ["lock_broken"] = new[] { "Line holds." },
                ["challenge_reply"] = new[] { "Bring them to me.", "I will take the rest." }, ["provoke"] = new[] { "Over here!", "Come and try me!" },
                ["guardian"] = new[] { "I have your back.", "Stay close to me." }, ["perfect_guard"] = new[] { "Denied.", "Back at you." },
            },
            ["fix"] = new Dictionary<string, string[]>
            {
                ["intercept_save"] = new[] { "Got it!" }, ["saved_reply"] = new[] { "Thanks! Owe you one." }, ["perfect"] = new[] { "Ha! Clean." },
                ["revive"] = new[] { "Jump-start! Up you get.", "Patched. Back in it." }, ["revived"] = new[] { "Back in business.", "Who fixed me? Thanks." },
                ["lash_reply"] = new[] { "Neat trick." }, ["lash_save"] = new[] { "Gotcha!" }, ["lock_broken"] = new[] { "Tools down. Nice work." },
                ["challenge_reply"] = new[] { "Keep them busy, I will patch you after." }, ["gadget"] = new[] { "Deploying!", "Built it." }, ["upgrade"] = new[] { "Tuned up.", "Better than new." },
            },
        };

        static readonly HashSet<string> SPIN_SPARED = new HashSet<string> { "stagger", "snared", "dazed", "attack", "charge", "dive" };
        // Settings: Nova's perfect parry stuns the attacker: dizzy, out of its attack, for a while (a boss only reels)
        public void ParryStun(Player p, Enemy e)
        {
            if (e == null || e.dead || e.type == "post" || e.type == "turret") return;
            if (e.boss) { e.parried = 2; return; }
            double ticks = e.light ? NOVA_SHIELD.stunLight : NOVA_SHIELD.stunHeavy;
            director.Release(e);
            e.stun = e.state == "hitstun" ? JMath.Max(e.stun, e.st + ticks) : ticks;
            if (e.state != "hitstun") { e.state = "hitstun"; e.st = 0; }
            e.atk = null; e.dizzy = true; e.flash = JMath.Max(e.flash, 4);
            Emit("parryStun", new Ev { p = p, e = e, x = e.x, y = e.y + e.h * 0.6 });
        }
        // Ticks Echo's deflect spin stuns an enemy for: the setting (seconds) for a light one, DEFLECT.heavyMult of it for a heavy one
        public static double SpinStunTicks(Enemy e)
        {
            double v = or(SETTINGS.echoSpinStun, 0.85);
            double light = JMath.Round(JMath.Min(3, JMath.Max(0.25, v)) * 60);
            return e.light ? light : JMath.Max(1, JMath.Round(light * DEFLECT.heavyMult));
        }

        sealed class Scheduled { public double t; public Action fn; }
        public sealed class ArenaState { public string state = "idle"; }

        public List<Player> players = new List<Player>();
        public List<Enemy> enemies = new List<Enemy>();
        public List<Projectile> projectiles = new List<Projectile>();
        public List<Hit> hitboxes = new List<Hit>();
        public List<Barrier> barriers = new List<Barrier>();
        public List<Shockwave> shockwaves = new List<Shockwave>();
        public List<Ev> events = new List<Ev>();
        List<Scheduled> scheduled = new List<Scheduled>();
        public List<Snare> snares = new List<Snare>();
        public List<Well> wells = new List<Well>();
        public UltCast ultCast;
        public List<Gadget> gadgets = new List<Gadget>();
        public List<Pickup> pickups = new List<Pickup>();
        public Dictionary<int, HashSet<object>> hitSets = new Dictionary<int, HashSet<object>>();
        public double tick;
        public int instanceSeq = 1;
        public int checkpoint;
        public double wipeT, globalBarkCd;
        public ArenaState arena = new ArenaState();
        public bool towerSpawned;
        public List<EncounterState> encounters = new List<EncounterState>();
        public bool routeDone;
        public HashSet<string> routesDone = new HashSet<string>();
        public double? bossClearedT;
        public double aspect = 16.0 / 9;
        public Cam cam = new Cam { x = 0, y = 3, dist = 16, halfW = 10, halfH = 5 };
        public Director director;

        public World()
        {
            foreach (var def in Level.ENCOUNTERS) encounters.Add(new EncounterState { def = def, state = "idle", wave = 0 });
            Level.RestoreBoxes(); SpawnLevelPickups();
            director = new Director(this);
            SpawnGym();
        }

        public void Emit(string type, Ev data) { data.type = type; events.Add(data); }
        public int NewInstance() => instanceSeq++;
        public void Schedule(double ticks, Action fn) { scheduled.Add(new Scheduled { t = tick + ticks, fn = fn }); }
        public List<Player> ActivePlayers() => players.FindAll(p => p.state != "dead" && p.state != "downed");
        public HashSet<object> HitSet(int inst)
        {
            if (!hitSets.TryGetValue(inst, out var set)) { set = new HashSet<object>(); hitSets[inst] = set; }
            return set;
        }

        // ---- Spawning helpers used by players, enemies and combat ----
        public void SpawnHitbox(Hit hb) { hitboxes.Add(hb); }
        public void SpawnProjectile(Projectile pr) { pr.px = pr.x; pr.py = pr.y; projectiles.Add(pr); }
        public void SpawnShockwave(Enemy e, double dir, double dmg, double scale = 1)
        {
            shockwaves.Add(new Shockwave { owner = e, x = e.x + dir * (e.w / 2), y = e.y, dir = dir, speed = 11, ttl = JMath.Round(60 * scale), dmg = dmg, h = 0.9, instance = NewInstance() });
        }
        public void Telegraph(Enemy e, string cat, double ticks) { Emit("telegraph", new Ev { e = e, cat = cat, ticks = ticks }); }

        public void FireShot(Player p, int level)
        {
            var c = Chest(p); double ax = p.aimX, ay = p.aimY; bool far = marksman(p);
            var pr = new Projectile { team = "p", owner = p, x = c.x + ax * 0.7, y = c.y + ay * 0.7, intercept = true, homing = level > 0, level = level };
            if (level == 0) { pr.speed = NOVA.shotSpeed; pr.dmg = NOVA.shotDmg; pr.poise = 6; pr.r = 0.16; pr.kbs = 1.5; pr.kind = "shot"; }
            else if (level == 1) { pr.speed = NOVA.lance.speed; pr.dmg = NOVA.lance.dmg; pr.poise = NOVA.lance.poise; pr.r = 0.24; pr.pierce = true; pr.kbs = 5; pr.kind = "lance"; }
            else { pr.speed = NOVA.rail.speed; pr.dmg = NOVA.rail.dmg; pr.poise = NOVA.rail.poise; pr.r = 0.3; pr.pierce = true; pr.rail = true; pr.armorBreak = true; pr.kbs = 9; pr.kind = "rail"; }
            pr.dmg *= FocusMult(p);
            pr.ttl = JMath.Round(NOVA.shotRange / pr.speed * 60);
            // Marksman kit: rounds fly the whole level and splash where they land
            if (far) { pr.splash = MARKSMAN.round.splash; pr.ttl = MARKSMAN.life; }
            pr.vx = ax * pr.speed; pr.vy = ay * pr.speed;
            SpawnProjectile(pr);
            if (level == 2 && !p.onGround) p.vy = JMath.Max(p.vy, 1.5);   // a mid-air shot briefly holds him up (no push-back)
            else if (level == 1 && !p.onGround) p.vy = JMath.Max(p.vy, 0.5);
            Emit("shot", new Ev { p = p, level = level, x = c.x + ax * 0.7, y = c.y + ay * 0.7 });
        }

        static Blast Scaled(Blast S, double mult, double rk)
        {
            var b = S.Clone(); b.dmg = S.dmg * mult; b.poise = S.poise * mult; b.r = S.r * rk; return b;
        }

        // Marksman kit: a charged release fires the loaded attachment. Every projectile from one release shares a family.
        public void FireAttachment(Player p, string kind, int level, bool perfect, double chargeT)
        {
            var c = Chest(p); double ax = p.aimX, ay = p.aimY;
            double over = SpendOvercharge(p);   // Aegis Overcharge: a stronger release
            double x = c.x + ax * 0.7, y = c.y + ay * 0.7, fm = FocusMult(p) * over;
            double mult = fm * (perfect ? MARKSMAN.perfectMult : 1);
            var family = new Family { focused = false, rocketed = false, perfect = perfect, chargeT = chargeT, attach = kind, level = level };
            Projectile Base() => new Projectile { team = "p", owner = p, x = x, y = y, level = level, perfect = perfect, family = family, intercept = true, ttl = MARKSMAN.life };
            if (kind == "lance")
            {
                var L = MARKSMAN.lance[level];
                var pr = Base();
                pr.vx = ax * L.speed; pr.vy = ay * L.speed; pr.r = L.r * (perfect ? 1.2 : 1); pr.dmg = L.dmg * mult; pr.poise = L.poise * mult; pr.kbs = L.kb;
                pr.pierce = true; pr.homing = true; pr.armorBreak = L.armorBreak || perfect; pr.rail = L.rail || perfect; pr.interceptHeavy = true;
                pr.kind = level == 3 ? "rail" : "lance"; pr.splash = Scaled(L.splash, mult, perfect ? 1.2 : 1);
                SpawnProjectile(pr);
                if (!p.onGround) p.vy = JMath.Max(p.vy, level == 3 ? 1.5 : 0.5);
            }
            else if (kind == "volley")
            {
                int key = perfect ? 4 : level; int n = (int)MARKSMAN.volley.darts[key]; double fan = MARKSMAN.volley.fan[key], a0 = JMath.Atan2(ay, ax);
                // Darts are spread over the enemies in front: the lower darts take the lower targets
                List<Enemy> targets = p.lockT != null && !p.lockT.dead && LockChosen(p) ? new List<Enemy> { p.lockT } : EnemiesInCone(c.x, c.y, ax, ay, MARKSMAN.volley.seekRange, MARKSMAN.volley.seekCone);
                var S = MARKSMAN.volley.splash.Clone(); S.dmg = MARKSMAN.volley.splash.dmg * fm; S.poise = MARKSMAN.volley.splash.poise * fm; S.rocket = MARKSMAN.volley.rocket;
                for (int i = 0; i < n; i++)
                {
                    double a = a0 + fan * ((double)i / (n - 1) - 0.5), sp = MARKSMAN.volley.speed * (1 + (i % 2) * 0.08);
                    var target = targets.Count > 0 ? targets[(int)JMath.Min(targets.Count - 1, JMath.Floor((double)i * targets.Count / n))] : null;
                    var pr = Base();
                    pr.vx = JMath.Cos(a) * sp; pr.vy = JMath.Sin(a) * sp; pr.r = MARKSMAN.volley.r;
                    pr.dmg = MARKSMAN.volley.dmg * fm; pr.poise = MARKSMAN.volley.poise * fm; pr.kbs = 2; pr.interceptHeavy = false; pr.kind = "dart"; pr.splash = S;
                    pr.seek = new SeekState { target = target, delay = MARKSMAN.volley.seekDelay, until = MARKSMAN.volley.seekFor, turn = MARKSMAN.volley.turn, age = 0 };
                    SpawnProjectile(pr);
                }
            }
            else if (kind == "arc")
            {
                var S = MARKSMAN.arc.levels[level]; double dx = ax, dy = ay + MARKSMAN.arc.lift, m = or(JMath.Hypot(dx, dy), 1);
                var pr = Base();
                pr.intercept = false; pr.vx = dx / m * MARKSMAN.arc.speed; pr.vy = dy / m * MARKSMAN.arc.speed; pr.gravity = MARKSMAN.arc.gravity;
                pr.r = 0.2; pr.dmg = 0; pr.poise = 0; pr.kind = "shell";
                pr.blast = new Blast { r = S.r * (perfect ? MARKSMAN.arc.perfectRadius : 1), dmg = S.dmg * mult, poise = S.poise * mult, armorBreak = S.armorBreak || perfect, rocket = S.rocket };
                SpawnProjectile(pr);
            }
            else
            {
                var S = MARKSMAN.prism.levels[level];
                var pr = Base();
                pr.vx = ax * MARKSMAN.prism.speed; pr.vy = ay * MARKSMAN.prism.speed; pr.r = MARKSMAN.prism.r * (perfect ? 1.2 : 1);
                pr.dmg = S.dmg * mult; pr.poise = S.poise * mult; pr.kbs = 3; pr.homing = true; pr.interceptHeavy = true; pr.kind = "prism"; pr.splash = Scaled(S.splash, mult, perfect ? 1.2 : 1);
                pr.prism = new PrismInfo { shards = S.shards, bounces = S.bounces + (perfect ? MARKSMAN.prism.perfectBounces : 0), mult = mult };
                SpawnProjectile(pr);
            }
            Emit("shot", new Ev { p = p, level = level, attach = kind, perfect = perfect, x = x, y = y, ax = ax, ay = ay, over = over > 1 });
            if (perfect) { Emit("perfectRelease", new Ev { p = p, x = x, y = y, attach = kind }); Bark(p, "perfect_shot", 0.25); }
        }

        // Explosions: splash from Nova's shots, Arc shells, the level 3 burst, and enemy mortar shells
        public void Explode(ExplodeArgs a)
        {
            var spec = a.spec; double x = a.x, y = a.y;
            BlastBoxes(x, y, spec.r, or(spec.dmg, 2) * 1.5, a.owner as Player);
            bool Reach(Body ent, double r)
            {
                double nx = JMath.Max(ent.x - ent.w / 2, JMath.Min(x, ent.x + ent.w / 2)), ny = JMath.Max(ent.y, JMath.Min(y, ent.y + ent.h));
                return JMath.Hypot(x - nx, y - ny) <= r;
            }
            if (a.team == "e")
            {
                int id = NewInstance();
                foreach (var q in players.ToArray())
                {
                    if (q.state == "dead" || q.state == "downed" || !Reach(q, spec.r)) continue;
                    HitPlayer(this, q, new Hit { owner = a.owner, dmg = spec.dmg, unblockable = true, cat = "unblockable", heavy = true, kb = new[] { or(sign(q.x - x), 1) * 7, 6.0 }, instance = id, at = new V2(x, y) });
                }
                Emit("enemyBlast", new Ev { x = x, y = y, r = spec.r });
                return;
            }
            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                if (e.dead || e == a.skip || !Reach(e, spec.r)) continue;
                string res = HitEnemy(this, e, new Hit { owner = a.owner, dmg = spec.dmg, poise = spec.poise, armorBreak = spec.armorBreak, kb = new[] { or(sign(e.x - x), 1) * 7, 5.0 }, blastHit = true }, "blast");
                if (a.family != null && (res == "hit" || res == "kill")) AwardFocus(this, a.owner, a.family, null);
            }
            if (a.rocket && spec.rocket && a.owner is Player op && marksman(op)) RocketPush(op, x, y, spec, a.family, a.perfect);
            Emit(a.kind, new Ev { p = a.owner as Player, owner = a.owner, x = x, y = y, r = spec.r, level = a.level, perfect = a.perfect });
        }

        // Rocket jump: a charged shot bursting on terrain close to Nova launches him away from the burst
        void RocketPush(Player p, double x, double y, Blast spec, Family family, bool perfect)
        {
            if (family != null && family.rocketed) return;
            double reach = spec.r + MARKSMAN.rocket.reach;
            if (JMath.Hypot(p.x - x, p.y + p.h * 0.5 - y) > reach || p.state == "downed" || p.state == "dead") return;
            // A burst anywhere under his boots counts as right under him
            double ox = p.x - x, sx = JMath.Sign(ox) * JMath.Max(0, JMath.Abs(ox) - MARKSMAN.rocket.slack);
            double dx = sx, dy = p.y + p.h * 0.5 - y;
            double dEff = JMath.Hypot(dx, dy);
            if (dEff < 0.05) { dx = 0; dy = 1; } else { dx /= dEff; dy /= dEff; }
            if (family != null) family.rocketed = true;
            double H = RocketHeight(family != null ? family.chargeT : MARKSMAN.charge[2], family != null ? family.attach : "arc", perfect);
            double near = JMath.Max(0, dEff - MARKSMAN.rocket.close) / JMath.Max(0.01, reach - MARKSMAN.rocket.close);   // 0 for a burst at his feet
            double air = p.onGround ? 1 : MARKSMAN.rocket.air[(int)JMath.Min(p.rockets, MARKSMAN.rocket.air.Length - 1)];
            double k = JMath.Sqrt(2 * GRAVITY * H) * (1 - MARKSMAN.rocket.falloff * JMath.Min(1, near)) * air;
            if (!p.onGround) p.rockets++;
            double vx = p.vx + dx * k * MARKSMAN.rocket.side;
            if (JMath.Abs(vx) > MARKSMAN.rocket.sideMax && JMath.Abs(vx) > JMath.Abs(p.vx)) vx = sign(vx) * JMath.Max(MARKSMAN.rocket.sideMax, JMath.Abs(p.vx));
            p.vx = vx;
            if (dy > 0) p.vy = JMath.Max(p.vy, dy * k); else p.vy += dy * k * 0.5;
            if (dy > 0.2) { p.onGround = false; p.coyote = 0; }   // launched: no late ground jump to cut the climb
            p.dashCarry = true; p.fastFall = false;
            double power = JMath.Min(1, k / JMath.Sqrt(2 * GRAVITY * MARKSMAN.rocket.perfect));
            double h = dy > 0 ? ApexGain(dy * k) : 0;
            p.rocketT = 50; p.rocketPow = power;
            p.hitstop = JMath.Max(p.hitstop, MARKSMAN.rocket.freeze[power < 0.45 ? 0 : power < 0.8 ? 1 : 2]);
            Emit("rocketJump", new Ev { p = p, x = x, y = y, k = k, h = h, power = power, perfect = perfect, level = family != null ? family.level : 3, dx = dx, dy = dy });
        }

        public sealed class RocketPreviewInfo { public double x, y, apex, level, h; public bool perfect; }
        // What a rocket jump would do right now (presentation only: the apex marker)
        public RocketPreviewInfo RocketPreview(Player p)
        {
            if (!marksman(p) || p.chargeT < MARKSMAN.charge[0] || p.chargeT >= MARKSMAN.beam.at || p.aimY > -0.6 || (p.state != "normal" && p.state != "slide")) return null;
            double gy = Level.GroundBelow(p.x, p.y + 0.1);
            if (double.IsNegativeInfinity(gy)) return null;
            string stage = ChargeStage(p); bool perfect = stage == "perfect"; int level = p.chargeT >= MARKSMAN.charge[2] ? 3 : p.chargeT >= MARKSMAN.charge[1] ? 2 : 1;
            string A = p.attachment;
            double r = A == "lance" ? MARKSMAN.lance[level].splash.r * (perfect ? 1.2 : 1) : A == "volley" ? MARKSMAN.volley.splash.r
                : A == "arc" ? MARKSMAN.arc.levels[level].r * (perfect ? MARKSMAN.arc.perfectRadius : 1) : MARKSMAN.prism.levels[level].splash.r * (perfect ? 1.2 : 1);
            double d = p.y + p.h * 0.5 - (gy + 0.15), reach = r + MARKSMAN.rocket.reach;
            if (d > reach) return null;
            double H = RocketHeight(p.chargeT, A, perfect), near = JMath.Max(0, d - MARKSMAN.rocket.close) / JMath.Max(0.01, reach - MARKSMAN.rocket.close);
            double air = p.onGround ? 1 : MARKSMAN.rocket.air[(int)JMath.Min(p.rockets, MARKSMAN.rocket.air.Length - 1)];
            double k = JMath.Sqrt(2 * GRAVITY * H) * (1 - MARKSMAN.rocket.falloff * JMath.Min(1, near)) * air;
            double up = JMath.Max(p.vy, k);
            return new RocketPreviewInfo { x = p.x, y = p.y, apex = p.y + ApexGain(up), level = level, perfect = perfect, h = H };
        }

        // ---- Nova: the Level 4 beam ----
        public void BeamTick(Player p)
        {
            var B = BeamSpec(p); var b = p.beam; var c = Chest(p);
            var segs = new List<BeamSeg>(); double sx = c.x + b.dx * 0.6, sy = c.y + b.dy * 0.6, dx = b.dx, dy = b.dy;
            double bounces = b.attach == "prism" ? B.prismBounces : 0;
            for (int i = 0; i <= bounces; i++)
            {
                var h = Level.RayCast(sx, sy, dx, dy, B.range);
                segs.Add(new BeamSeg { x0 = sx, y0 = sy, x1 = h.x, y1 = h.y, wall = h.wall, nx = h.nx, ny = h.ny });
                if (!h.wall || i == bounces) break;
                double dot = dx * h.nx + dy * h.ny; dx -= 2 * dot * h.nx; dy -= 2 * dot * h.ny;
                sx = h.x + h.nx * 0.05; sy = h.y + h.ny * 0.05;
            }
            b.segs = segs; b.pulse++;
            var last = segs[segs.Count - 1];
            var endBox = Level.RayCast(last.x0, last.y0, dx, dy, B.range).box;
            if (endBox != null && endBox.type == 'd' && b.pulse % B.pulse == 1) DamageBox(endBox, B.dmg * b.mult * 2, last.x1, last.y1, p);
            bool Near(double x, double y, double r) { foreach (var g in segs) if (DistToSeg(x, y, g) < r) return true; return false; }
            foreach (var pr in projectiles.Live()) if (pr.team == "e" && !pr.dead && Near(pr.x, pr.y, B.width + pr.r)) { pr.dead = true; Emit("erase", new Ev { x = pr.x, y = pr.y }); }
            if (b.pulse % B.pulse == 1)
            {
                for (int i = 0; i < enemies.Count; i++)
                {
                    var e = enemies[i];
                    if (e.dead) continue;
                    if (!LevelFeatures.Same(p, e)) continue;   // (depth lanes: the beam stays in its lane)
                    var hb = Hurtbox(e); bool any = false;
                    foreach (var g in segs) if (SegHitsBox(g, hb, B.width)) { any = true; break; }
                    if (!any) continue;
                    bool ab = !b.armor.TryGetValue(e.id, out var lastT) || b.pulse - lastT >= B.armorEvery;
                    if (ab) b.armor[e.id] = b.pulse;
                    string res = HitEnemy(this, e, new Hit { owner = p, dmg = B.dmg * b.mult, poise = B.poise * b.mult, kb = new[] { sign(b.dx) * or(B.kb, 3), 1.0 }, vx = b.dx, armorBreak = ab, rail = true, beam = true }, "proj");
                    if (p.@char == "nova" && (res == "hit" || res == "kill")) AwardFocus(this, p, b.family, null);
                }
            }
            var end = segs[segs.Count - 1];
            if (b.attach == "arc" && b.pulse % B.arcEvery == 0)
            {
                var spec = B.arcBlast.Clone(); spec.dmg = B.arcBlast.dmg * b.mult; spec.poise = B.arcBlast.poise * b.mult; spec.armorBreak = true;
                Explode(new ExplodeArgs { owner = p, x = end.x1, y = end.y1, spec = spec, level = 2, kind = "blast" });
            }
            if (b.attach == "volley" && b.pulse % B.volleyEvery == 0)
            {
                double a = JMath.Atan2(b.dy, b.dx) + (JRandom.Next() - 0.5) * 0.9, sx0 = c.x + b.dx * 0.7, sy0 = c.y + b.dy * 0.7;
                var target = NearestEnemyInCone(sx0, sy0, b.dx, b.dy, MARKSMAN.volley.seekRange, MARKSMAN.volley.seekCone);
                SpawnProjectile(new Projectile
                {
                    team = "p", owner = p, x = sx0, y = sy0, vx = JMath.Cos(a) * MARKSMAN.volley.speed, vy = JMath.Sin(a) * MARKSMAN.volley.speed, ttl = MARKSMAN.life, r = MARKSMAN.volley.r,
                    dmg = MARKSMAN.volley.dmg * b.mult, poise = MARKSMAN.volley.poise * b.mult, kbs = 2, kind = "dart", level = 4, family = b.family, intercept = true, interceptHeavy = false,
                    splash = MARKSMAN.volley.splash.Clone(), seek = new SeekState { target = target, delay = 4, until = MARKSMAN.volley.seekFor, turn = MARKSMAN.volley.turn, age = 0 },
                });
            }
        }
        public void EndBeam(Player p, string why)
        {
            if (p.beam == null) return;
            Emit("beamEnd", new Ev { p = p, why = why });
            p.beam = null;
        }

        // ---- Nova: the hard-light Aegis ----
        public void RaiseAegis(Player p)
        {
            p.aegis = new AegisState { hp = AEGIS.hp, max = AEGIS.hp, t = AEGIS.ticks };
            Emit("aegisOn", new Ev { p = p });
        }
        // Ends it: 'break' (damage) shatters it outward, 'detonate' (pressed again) blasts it outward on purpose
        public void EndAegis(Player p, string why)
        {
            var S = p.aegis; if (S == null) return;
            var c = Chest(p); double frac = JMath.Max(0, S.hp / S.max);
            p.aegis = null; p.aegisCd = AEGIS.cd;
            if (why == "break" || why == "detonate")
            {
                var B = why == "break" ? AEGIS.shatter : AEGIS.detonate; double k = why == "detonate" ? 0.5 + 0.5 * frac : 1;
                SpawnHitbox(new Hit
                {
                    owner = p, team = "p", x0 = c.x - B.r, x1 = c.x + B.r, y0 = c.y - B.r, y1 = c.y + B.r, dmg = B.dmg * k, poise = B.poise * k,
                    kb = new[] { B.kb, 5.0 }, radial = true, cx = c.x, armorBreak = true, instance = NewInstance(), aegisBurst = true,
                });
                if (why == "detonate") { p.overcharge = JMath.Min(AEGIS.over.max, p.overcharge + AEGIS.detonateOver * frac); p.overT = AEGIS.over.hold; }
            }
            Emit("aegisOff", new Ev { p = p, why = why, x = c.x, y = c.y, frac = frac });
        }
        public void DetonateAegis(Player p) { EndAegis(p, "detonate"); }
        // The Nova whose Aegis shelters this player (their own, or one they stand inside), if any
        public Player ShieldFor(Player q)
        {
            foreach (var n in players.Live())
            {
                if (n.aegis == null || n.state == "dead" || n.state == "downed") continue;
                if (n == q) return n;
                var a = Chest(n); var b = Chest(q);
                if (JMath.Hypot(a.x - b.x, a.y - b.y) < AEGIS.radius) return n;
            }
            return null;
        }
        // The Aegis a point (a shot of radius r) has reached, if any
        public Player AegisAt(double x, double y, double r)
        {
            foreach (var n in players.Live())
            {
                if (n.aegis == null || n.state == "dead" || n.state == "downed") continue;
                var c = Chest(n);
                if (JMath.Hypot(x - c.x, y - c.y) < AEGIS.radius + r) return n;
            }
            return null;
        }
        // The Aegis takes a hit coming from (fx, fy): damage to the hard light becomes Overcharge. `key` makes one
        // attack count once even when it reaches several players inside.
        public void AbsorbAegis(Player n, double dmg, double fx, double fy, string key)
        {
            var S = n.aegis; if (S == null) return;
            if (key != null) { if (S.seen.Contains(key)) return; S.seen.Add(key); }
            var c = Chest(n);
            double dx = fx - c.x, dy = fy - c.y, m = or(JMath.Hypot(dx, dy), 1); dx /= m; dy /= m;
            S.hp -= dmg;
            n.overcharge = JMath.Min(AEGIS.over.max, n.overcharge + dmg * AEGIS.over.perDmg); n.overT = AEGIS.over.hold;
            Emit("aegisHit", new Ev { p = n, x = c.x + dx * AEGIS.radius, y = c.y + dy * AEGIS.radius, dx = dx, dy = dy, dmg = dmg, frac = JMath.Max(0, S.hp / S.max) });
            if (S.hp <= 0) EndAegis(n, "break");
        }

        // ---- Echo: sniper rifle and staff deflect ----
        public void FireSniper(Player p, double f)
        {
            var c = Chest(p); double ax = p.aimX, ay = p.aimY; bool full = f >= 1;
            double x0 = c.x + ax * 0.9, y0 = c.y + ay * 0.9; var wall = Level.RayCast(x0, y0, ax, ay, HUNTER.rifle.range);
            if (wall.box != null && wall.box.type == 'd') DamageBox(wall.box, HUNTER.rifle.minDmg + (HUNTER.rifle.maxDmg - HUNTER.rifle.minDmg) * f, wall.x, wall.y, p);
            var line = new List<(Enemy e, double t)>();
            foreach (var e in enemies.Live())
            {
                if (e.dead || !LevelFeatures.Same(p, e)) continue;
                var hb = Hurtbox(e); var h = Level.RayBoxT(x0, y0, ax, ay, hb.x0 - 0.06, hb.y0 - 0.06, hb.x1 + 0.06, hb.y1 + 0.06);
                if (h != null && h.Value.t <= wall.t) line.Add((e, h.Value.t));
            }
            StableSort.Sort(line, (a, b) => a.t - b.t);
            double dmg = HUNTER.rifle.minDmg + (HUNTER.rifle.maxDmg - HUNTER.rifle.minDmg) * f * f, poise = HUNTER.rifle.poise[0] + (HUNTER.rifle.poise[1] - HUNTER.rifle.poise[0]) * f;
            double endT = wall.t, crits = 0, n = 0;
            foreach (var (e, t) in line)
            {
                double hy = y0 + ay * (t + 0.15); bool crit = hy > e.y + e.h * HUNTER.rifle.critZone;
                string res = HitEnemy(this, e, new Hit { owner = p, dmg = dmg * (crit ? HUNTER.rifle.crit : 1), poise = poise * (crit ? 1.3 : 1), kb = new[] { sign(ax) * HUNTER.rifle.kb * (0.5 + f), 2.0 }, vx = ax, armorBreak = full, rail = full, snipe = true }, "proj");
                n++;
                if (crit && res != "blocked") { crits++; Emit("crit", new Ev { p = p, e = e, x = x0 + ax * t, y = hy }); }
                if (full && !e.dead) { e.tagged = JMath.Max(e.tagged, HUNTER.rifle.tag); Emit("tag", new Ev { x = e.x, y = e.y + e.h, e = e }); }
                if (!full || res == "blocked") { endT = t; break; }
            }
            Emit("snipe", new Ev { p = p, x0 = x0, y0 = y0, x1 = x0 + ax * endT, y1 = y0 + ay * endT, ax = ax, ay = ay, f = f, full = full, crits = crits, n = n, wall = endT == wall.t && wall.wall });
        }
        // Echo's deflect spin stuns every enemy the twirling staff touches, once per spin
        public void SpinStun(Player p)
        {
            if (SETTINGS.echoKit != "hunter" || p.parryT < 1 || p.parryT > DEFLECT.window) return;
            if (p.parryT == 1 || p.spinHit == null) p.spinHit = new HashSet<Enemy>();
            foreach (var e in enemies.Live())
            {
                if (e.dead || e.boss || e.type == "post" || p.spinHit.Contains(e) || SPIN_SPARED.Contains(e.state)) continue;
                double gap = JMath.Abs(e.x - p.x) - e.w / 2 - p.w / 2;
                if (gap > DEFLECT.stunReach || e.y > p.y + p.h + 0.3 || e.y + e.h < p.y - 0.3) continue;
                p.spinHit.Add(e);
                double ticks = SpinStunTicks(e);
                director.Release(e);
                e.stun = e.state == "hitstun" ? JMath.Max(e.stun, e.st + ticks) : ticks;
                if (e.state != "hitstun") { e.state = "hitstun"; e.st = 0; }
                e.atk = null; e.dizzy = true; e.vx = or(JMath.Sign(e.x - p.x), p.facing) * 2.5; e.flash = JMath.Max(e.flash, 4);
                Emit("spinStun", new Ev { p = p, e = e, x = e.x, y = e.y + e.h * 0.6 });
            }
        }

        // Echo's staff knocks an enemy shot back toward whoever fired it, as his own, faster
        public void Deflect(Player p, Projectile pr)
        {
            ParryWindows(p, out _, out double winP);
            bool perfect = p.state == "parry" && p.parryT <= winP;
            var src = pr.owner is Enemy oe && !oe.dead ? oe : null; double sp = JMath.Hypot(pr.vx, pr.vy) * DEFLECT.speed;
            double vx = -pr.vx * DEFLECT.speed, vy = -pr.vy * DEFLECT.speed;
            if (src != null) { double dx = src.x - pr.x, dy = src.y + src.h * 0.6 - pr.y, m = or(JMath.Hypot(dx, dy), 1); vx = dx / m * sp; vy = dy / m * sp; }
            bool heavy = pr.heavy;
            pr.team = "p"; pr.owner = p; pr.vx = vx; pr.vy = vy; pr.dmg = (heavy ? DEFLECT.dmg.heavy : DEFLECT.dmg.standard) * (perfect ? DEFLECT.perfect : 1); pr.poise = heavy ? 40 : 20; pr.kbs = 5;
            pr.hitSet = new HashSet<object>(); pr.deflected = true; pr.intercept = false; pr.heavy = false; pr.homing = false; pr.ttl = JMath.Max(pr.ttl, 120); pr.gravity = 0;
            if (p.state == "parry" && p.parryResult == null) { p.parryResult = perfect ? "perfect" : "normal"; p.st = 0; p.hitstop = perfect ? 5 : 3; }
            if (perfect) { p.riposteT = 20; AddResolve(p, 12); GainUlt(p, ULT.gain.perfect, this); } else AddResolve(p, 6);
            Emit("deflect", new Ev { p = p, x = pr.x, y = pr.y, perfect = perfect, heavy = heavy });
        }

        // Prism rounds split into shards that fan out along (dx, dy); shards skip the enemy that split them
        public void SplitPrism(Projectile pr, double x, double y, double dx, double dy, Enemy skip)
        {
            var S = MARKSMAN.prism.shard; int n = (int)pr.prism.shards; double a0 = JMath.Atan2(dy, dx);
            var splash = S.splash.Clone(); splash.dmg = S.splash.dmg * pr.prism.mult; splash.poise = S.splash.poise * pr.prism.mult;
            for (int i = 0; i < n; i++)
            {
                double a = a0 + S.fan * (i - (n - 1) / 2.0);
                var sh = new Projectile
                {
                    team = "p", owner = pr.owner, x = x, y = y, vx = JMath.Cos(a) * S.speed, vy = JMath.Sin(a) * S.speed, ttl = MARKSMAN.life, r = S.r,
                    dmg = S.dmg * pr.prism.mult, poise = S.poise * pr.prism.mult, kbs = 2, level = pr.level, perfect = pr.perfect, family = pr.family,
                    intercept = true, interceptHeavy = false, bounces = pr.prism.bounces, kind = "shard", splash = splash,
                };
                SpawnProjectile(sh);
                if (skip != null) sh.hitSet.Add(skip.id);
            }
            Emit("split", new Ev { p = pr.owner as Player, x = x, y = y, n = n });
        }

        // ---- Nova: secondary weapons (SUB) ----
        public void FireSub(Player p, int level, bool perfect = false)
        {
            string k = p.sub;
            if (k == "grenade") ThrowGrenade(p, level, perfect);
            else if (k == "chain") FireChain(p, level, perfect);
            else if (k == "disc") ThrowDisc(p, level, perfect);
            else if (k == "well") LaunchWell(p, level, perfect);
            else FireBurst(p, level, perfect);
            p.shootT = 10;
        }

        // Scatter: point-blank pellets; level 3 adds a blast at the muzzle. No recoil.
        void FireBurst(Player p, int level, bool perfect)
        {
            var S = MARKSMAN.burst.levels[level]; var c = Chest(p); double ax = p.aimX, ay = p.aimY, a0 = JMath.Atan2(ay, ax);
            double x = c.x + ax * 0.5, y = c.y + ay * 0.5, mult = perfect ? MARKSMAN.perfectMult : 1;
            int pellets = (int)S.pellets;
            for (int i = 0; i < pellets; i++)
            {
                double a = a0 + S.fan * ((double)i / (pellets - 1) - 0.5);
                SpawnProjectile(new Projectile
                {
                    team = "p", owner = p, x = x, y = y, vx = JMath.Cos(a) * S.speed, vy = JMath.Sin(a) * S.speed, ttl = MARKSMAN.life, r = 0.16,
                    dmg = S.dmg * mult, poise = S.poise * mult, kbs = S.kb, kbY = 2, intercept = true, interceptHeavy = false, kind = "pellet",
                    falloff = new FalloffInfo { x = x, y = y, d = MARKSMAN.burst.falloff }, armorBreak = S.armorBreak && i == (pellets >> 1),
                });
            }
            if (S.blast != null)
            {
                Explode(new ExplodeArgs
                {
                    owner = p, x = x + ax * 0.7, y = y + ay * 0.7, level = level, perfect = perfect, kind = "blast",
                    spec = new Blast { r = S.blast.r * (perfect ? 1.25 : 1), dmg = S.blast.dmg * mult, poise = S.blast.poise * mult, armorBreak = true },
                });
            }
            p.shootT = 10; p.burstCd = MARKSMAN.burst.cd;
            Emit("burst", new Ev { p = p, x = x, y = y, ax = ax, ay = ay, level = level, charged = level > 0, perfect = perfect });
        }

        // Grenade: a bouncing frag on a fuse; it bursts early on an enemy, and level 3 scatters bomblets
        void ThrowGrenade(Player p, int level, bool perfect)
        {
            var c = Chest(p); double ax = p.aimX, ay = p.aimY, dy = ay + SUB.grenade.lift, m = or(JMath.Hypot(ax, dy), 1), sp = SUB.grenade.speed[level];
            double mult = perfect ? MARKSMAN.perfectMult : 1; var B = SUB.grenade.blast[level];
            double x = c.x + ax * 0.6, y = c.y + ay * 0.6;
            SpawnProjectile(new Projectile
            {
                team = "p", owner = p, x = x, y = y, vx = ax / m * sp, vy = dy / m * sp, gravity = SUB.grenade.gravity, bouncy = SUB.grenade.bounce, r = SUB.grenade.r, ttl = SUB.grenade.fuse[level],
                dmg = 0, poise = 0, kind = "grenade", level = level, perfect = perfect, intercept = false,
                blast = new Blast { r = B.r * (perfect ? 1.2 : 1), dmg = B.dmg * mult, poise = B.poise * mult, armorBreak = B.armorBreak || perfect },
                cluster = level == 3 ? new ClusterDef { n = SUB.grenade.bomblets.n, speed = SUB.grenade.bomblets.speed, lift = SUB.grenade.bomblets.lift, fuse = SUB.grenade.bomblets.fuse, blast = SUB.grenade.bomblets.blast } : null,
            });
            p.burstCd = SUB.grenade.cd;
            Emit("grenadeThrow", new Ev { p = p, x = x, y = y, level = level, perfect = perfect });
        }
        public void ClusterBurst(Projectile pr, double x, double y)
        {
            var K = pr.cluster;
            for (int i = 0; i < K.n; i++)
            {
                double a = JMath.PI / 2 + (i / (K.n - 1) - 0.5) * 2.2;
                SpawnProjectile(new Projectile
                {
                    team = "p", owner = pr.owner, x = x, y = y + 0.2, vx = JMath.Cos(a) * K.speed, vy = JMath.Sin(a) * K.speed + K.lift * 0.3, gravity = SUB.grenade.gravity,
                    bouncy = 0.35, r = 0.14, ttl = K.fuse + i * 3, dmg = 0, poise = 0, kind = "bomblet", level = 1, intercept = false, blast = K.blast.Clone(),
                });
            }
            Emit("cluster", new Ev { p = pr.owner as Player, x = x, y = y, n = K.n });
        }

        // Chain: instant lightning from the bracer to the nearest enemy in front, then on to the nearest within `hop`
        void FireChain(Player p, int level, bool perfect)
        {
            var c = Chest(p); double ax = p.aimX, ay = p.aimY, mult = perfect ? MARKSMAN.perfectMult : 1;
            double x0 = c.x + ax * 0.6, y0 = c.y + ay * 0.6, R = SUB.chain.range[level], cos = JMath.Cos(SUB.chain.cone);
            V2 Mid(Enemy e) => new V2(e.x, e.y + e.h * 0.55);
            bool Clear(V2 a, V2 b) => !Level.SegmentBlocked(a.x, a.y, b.x, b.y);
            Enemy first = null;
            var t = p.lockT;
            if (t != null && !t.dead && JMath.Hypot(t.x - x0, Mid(t).y - y0) <= R && Clear(new V2(x0, y0), Mid(t))) first = t;
            else
            {
                double bd = R;
                foreach (var e in enemies.Live())
                {
                    if (e.dead) continue;
                    var m = Mid(e); double dx = m.x - x0, dy = m.y - y0, d = JMath.Hypot(dx, dy);
                    if (d < bd && d > 1e-3 && (dx * ax + dy * ay) / d > cos && Clear(new V2(x0, y0), m)) { bd = d; first = e; }
                }
            }
            var pts = new List<ChainPt> { new ChainPt { x = x0, y = y0 } }; var hit = new HashSet<Enemy>();
            Enemy cur = first; var from = new V2(x0, y0);
            while (cur != null && hit.Count < SUB.chain.jumps[level])
            {
                hit.Add(cur);
                var m = Mid(cur); pts.Add(new ChainPt { x = m.x, y = m.y });
                HitEnemy(this, cur, new Hit
                {
                    owner = p, dmg = SUB.chain.dmg[level] * mult, poise = SUB.chain.poise[level] * mult, kb = new[] { or(sign(m.x - from.x), p.facing) * 2, 1.0 }, stun = SUB.chain.stun[level], shock = true,
                    armorBreak = level == 3 && hit.Count == 1,
                }, "blast");
                from = m; cur = null; double bd = SUB.chain.hop;
                foreach (var e in enemies.Live())
                {
                    if (e.dead || hit.Contains(e)) continue;
                    var n = Mid(e); double d = JMath.Hypot(n.x - m.x, n.y - m.y);
                    if (d < bd && Clear(m, n)) { bd = d; cur = e; }
                }
            }
            // Nothing in reach: the arc lashes out and earths itself on the nearest surface in front
            if (first == null) { var h = Level.RayCast(x0, y0, ax, ay, R * 0.7); pts.Add(new ChainPt { x = h.x, y = h.y, fizzle = true }); }
            p.burstCd = SUB.chain.cd;
            Emit("chain", new Ev { p = p, pts = pts, level = level, perfect = perfect, n = hit.Count });
        }

        // Disc: out along the aim, (from level 2) a hover at the far end, then back to him
        void ThrowDisc(Player p, int level, bool perfect)
        {
            var c = Chest(p); double ax = p.aimX, ay = p.aimY, sp = SUB.disc.speed[level], mult = perfect ? MARKSMAN.perfectMult : 1;
            double x = c.x + ax * 0.6, y = c.y + ay * 0.6;
            SpawnProjectile(new Projectile
            {
                team = "p", owner = p, x = x, y = y, vx = ax * sp, vy = ay * sp, ttl = 100000, r = SUB.disc.r[level] * (perfect ? 1.2 : 1),
                dmg = SUB.disc.dmg[level] * mult, poise = SUB.disc.poise[level] * mult, kbs = 3, pierce = true, intercept = true, interceptHeavy = level >= 2, kind = "disc", level = level, perfect = perfect,
                disc = new DiscState { phase = "out", t = 0, @out = SUB.disc.@out[level], hover = SUB.disc.hover[level] + (perfect ? 20 : 0), dx = ax, dy = ay, speed = sp },
            });
            p.burstCd = SUB.disc.cd;
            Emit("discThrow", new Ev { p = p, x = x, y = y, level = level, perfect = perfect });
        }
        // Gravity Well: an orb that opens where it stops
        void LaunchWell(Player p, int level, bool perfect)
        {
            var c = Chest(p); double x = c.x + p.aimX * 0.7, y = c.y + p.aimY * 0.7;
            wells.Add(new Well
            {
                owner = p, x = x, y = y, px = x, py = y, vx = p.aimX * SUB.well.speed, vy = p.aimY * SUB.well.speed, phase = "orb", t = 0, level = level, perfect = perfect,
                r = SUB.well.r[level] * (perfect ? 1.2 : 1), life = SUB.well.life[level] + (perfect ? 30 : 0), mult = perfect ? MARKSMAN.perfectMult : 1,
            });
            p.burstCd = SUB.well.cd;
            Emit("wellLaunch", new Ev { p = p, x = x, y = y, level = level, perfect = perfect });
        }
        // Is a disc or a well of his still out?
        public bool SubOut(Player p, string kind)
        {
            if (kind == "disc") return projectiles.Exists(pr => pr.owner == p && pr.kind == "disc" && !pr.dead);
            if (kind == "well") return wells.Exists(w => w.owner == p);
            return false;
        }
        // Pressed again while it is out: the disc turns for home; the well opens where the orb is, or collapses
        public void RecallSub(Player p, string kind)
        {
            if (kind == "disc")
            {
                var pr = projectiles.Find(q => q.owner == p && q.kind == "disc" && !q.dead);
                if (pr != null && pr.disc.phase != "back") { pr.disc.phase = "back"; pr.disc.t = 0; pr.ghost = true; pr.hitSet.Clear(); Emit("discRecall", new Ev { p = p, x = pr.x, y = pr.y }); }
            }
            else if (kind == "well")
            {
                var w = wells.Find(q => q.owner == p);
                if (w != null) { if (w.phase == "orb") OpenWell(w); else w.collapse = true; }
            }
        }
        void OpenWell(Well w)
        {
            w.phase = "open"; w.t = 0;
            // A well that opens at floor level lifts a little, so what it catches floats up into it
            double gy = Level.GroundBelow(w.x, w.y + 0.05);
            if (gy > double.NegativeInfinity && w.y - gy < SUB.well.lift && !Level.PointInSolid(w.x, gy + SUB.well.lift)) w.y = gy + SUB.well.lift;
            Emit("wellOpen", new Ev { p = w.owner, x = w.x, y = w.y, r = w.r, level = w.level });
        }
        void UpdateWells(bool frozen)
        {
            foreach (var w in wells.ToArray())
            {
                w.px = w.x; w.py = w.y; w.t++;
                bool gone = !players.Contains(w.owner);
                if (w.phase == "orb")
                {
                    double nx = w.x + w.vx * DT, ny = w.y + w.vy * DT;
                    bool open = w.t >= SUB.well.travel[(int)w.level] || gone;
                    if (Level.PointInSolid(nx, ny)) open = true; else { w.x = nx; w.y = ny; }
                    if (!open) foreach (var e in enemies.Live()) { if (!e.dead && JMath.Abs(e.x - w.x) < e.w / 2 + 0.35 && w.y > e.y - 0.35 && w.y < e.y + e.h + 0.35) { open = true; break; } }
                    if (open) OpenWell(w);
                    continue;
                }
                // Open: pull light enemies in and hold them, drag heavy ones, swallow enemy shots, hurt everything
                int L = (int)w.level; bool tickHit = w.t % SUB.well.tick == 0;
                for (int i = 0; i < enemies.Count; i++)
                {
                    var e = enemies[i];
                    if (e.dead) continue;
                    double cy = e.y + e.h * 0.5, dx = w.x - e.x, dy = w.y - cy, d = JMath.Hypot(dx, dy);
                    if (d > w.r) continue;
                    if (tickHit) HitEnemy(this, e, new Hit { owner = w.owner, dmg = SUB.well.tickDmg[L] * w.mult, poise = 4, kb = new[] { 0.0, 0.0 }, well = true }, "blast");
                    if (e.dead || e.boss || frozen) continue;
                    if (Enemies.ENEMY_TYPES[e.type].stationary) continue;
                    double ux = d > 1e-3 ? dx / d : 0, uy = d > 1e-3 ? dy / d : 0, sp = JMath.Min(SUB.well.pull[L], d * 6);
                    if (e.light && e.armor <= 0)
                    {
                        if (e.state != "stagger" && e.state != "caught" && e.state != "snared")
                        {
                            if (e.state == "windup" || e.state == "aim" || e.state == "lock") director.Release(e);
                            e.state = "launched"; e.st = 1;
                        }
                        e.vx = ux * sp; e.vy = uy * sp + (e.flier ? 0 : GRAVITY * DT); e.wellT = 2;
                    }
                    else
                    {
                        // Heavy: dragged along the ground toward it
                        double step = ux * SUB.well.pull[L] * SUB.well.heavy * DT, nx = e.x + step;
                        if (!Level.PointInSolid(nx + sign(step) * e.w / 2, e.y + 0.3)) e.x = nx;
                        e.wellT = 2;
                    }
                }
                foreach (var pr in projectiles.Live())
                {
                    if (pr.team != "e" || pr.dead) continue;
                    double dx = w.x - pr.x, dy = w.y - pr.y, d = JMath.Hypot(dx, dy);
                    if (d > w.r) continue;
                    if (d < 0.6) { pr.dead = true; Emit("erase", new Ev { x = pr.x, y = pr.y }); continue; }
                    double sp = JMath.Hypot(pr.vx, pr.vy); pr.vx += dx / d * 40 * DT; pr.vy += dy / d * 40 * DT;
                    double s2 = or(JMath.Hypot(pr.vx, pr.vy), 1); pr.vx *= sp / s2; pr.vy *= sp / s2;
                }
                if (w.t >= w.life || w.collapse || gone)
                {
                    var S = SUB.well.implode[L];
                    Explode(new ExplodeArgs
                    {
                        owner = gone ? null : w.owner, x = w.x, y = w.y, level = L + 1, perfect = w.perfect, kind = "wellCollapse",
                        spec = new Blast { r = S.r * (w.perfect ? 1.2 : 1), dmg = S.dmg * w.mult, poise = S.poise * w.mult, armorBreak = S.armorBreak },
                    });
                    w.dead = true;
                }
            }
            wells.RemoveAll(w => w.dead);
        }

        public void FireBolt(Player p)
        {
            var c = Chest(p);
            SpawnProjectile(new Projectile { team = "p", owner = p, x = c.x + p.aimX * 0.7, y = c.y + p.aimY * 0.7, vx = p.aimX * ECHO.boltSpeed, vy = p.aimY * ECHO.boltSpeed, ttl = 36, r = 0.15, dmg = ECHO.boltDmg, poise = 8, kbs = 2, kind = "bolt" });
            Emit("shot", new Ev { p = p, level = 0, bolt = true, x = c.x, y = c.y });
        }
        public void FireTracer(Player p)
        {
            var c = Chest(p);
            SpawnProjectile(new Projectile { team = "p", owner = p, x = c.x + p.aimX * 0.7, y = c.y + p.aimY * 0.7, vx = p.aimX * 40, vy = p.aimY * 40, ttl = 42, r = 0.16, dmg = ECHO.tracerDmg, poise = 5, kbs = 1, tracer = true, kind = "tracer" });
            Emit("tracer", new Ev { p = p, x = c.x, y = c.y });
        }

        public void BulwarkPulse(Player p)
        {
            var c = Chest(p); double ax = p.aimX, ay = p.aimY, cos = JMath.Cos(JMath.PI * 50 / 180);
            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                if (e.dead || e.type == "turret") continue;
                double dx = e.x - c.x, dy = e.y + e.h / 2 - c.y, d = or(JMath.Hypot(dx, dy), 0.01);
                if ((d < 4.4 && (dx * ax + dy * ay) / d > cos) || d < 1.4)
                    HitEnemy(this, e, new Hit { owner = p, dmg = 1, poise = 40, kb = new[] { ax * 12, 4 + ay * 6 }, bulwark = true }, "pulse");
            }
            foreach (var pr in projectiles.Live())
            {
                if (pr.team != "e" || pr.dead) continue;
                double dx = pr.x - c.x, dy = pr.y - c.y, d = or(JMath.Hypot(dx, dy), 0.01);
                if ((d < 4.4 && (dx * ax + dy * ay) / d > cos) || d < 1.6) { pr.dead = true; Emit("erase", new Ev { x = pr.x, y = pr.y }); }
            }
            barriers.Add(new Barrier { x = c.x + ax * 2.4, y = c.y + ay * 2.4, nx = ax, ny = ay, half = NOVA.barrierHalf, ttl = NOVA.barrierTicks, max = NOVA.barrierTicks, owner = p });
            Emit("bulwark", new Ev { p = p, x = c.x, y = c.y, ax = ax, ay = ay });
        }

        public Actor FindLashTarget(Player p, double cx, double cy, double ax, double ay, double range)
        {
            Actor best = null; double bt = range + 0.01;
            void Consider(Actor ent)
            {
                double dx = ent.x - cx, dy = ent.y + ent.h * 0.5 - cy, t = dx * ax + dy * ay;
                if (t <= 0.3 || t > range) return;
                if (JMath.Abs(dx * -ay + dy * ax) > ent.h * 0.5 + 0.9) return;
                if (t < bt) { bt = t; best = ent; }
            }
            foreach (var e in enemies.Live()) if (!e.dead) Consider(e);
            foreach (var q in players.Live()) if (q != p && q.state == "downed") Consider(q);
            return best;
        }

        public void LashConnect(Player p, Actor tAct, bool held = false)
        {
            if (tAct is Player tp)
            {
                tp.x = p.x + p.facing * 0.9; tp.y = p.y + 0.1;
                Emit("lashAlly", new Ev { p = p, q = tp });
                return;
            }
            var t = (Enemy)tAct;
            if (t.light && t.armor <= 0)
            {
                var ally = players.Find(q => q != p && q.state != "dead" && q.state != "downed" && JMath.Hypot(q.x - t.x, q.y - t.y) < 3);
                director.Release(t);
                t.state = "caught"; t.st = 0; t.catcher = p; t.catchSide = or(sign(t.x - p.x), p.facing); t.shieldDir = t.catchSide; t.dropT = 16;
                AddResolve(p, 4);
                Emit("lashPull", new Ev { p = p, e = t });
                if (held) { p.leash = new LeashState { e = t, t = 0 }; Emit("leash", new Ev { p = p, e = t }); }
                if (ally != null) { Bark(p, "lash_save", 0.8); Schedule(50, () => Bark(ally, "lash_reply", 1, true)); }
            }
            else if (held)
            {
                // Hunter kit: yank a heavy target off balance instead of zipping to it
                HitEnemy(this, t, new Hit { owner = p, dmg = 0.5, poise = HUNTER.yankPoise, kb = new[] { sign(p.x - t.x) * 3, 0.0 } }, "pulse");
                Emit("yank", new Ev { p = p, e = t });
            }
            else
            {
                p.zip = new ZipState { target = t }; p.state = "zip"; p.st = 0;
                Emit("lashZip", new Ev { p = p, e = t });
            }
        }

        public void ReleaseLeash(Player p)
        {
            var L = p.leash; p.leash = null;
            if (L != null && L.e != null && L.e.state == "caught") L.e.st = JMath.Max(L.e.st, 20);
            Emit("leashEnd", new Ev { p = p });
        }

        // ---- Scarf modes ----
        // Flare Signature (Challenge): every enemy close by turns on Echo, and attacks sooner
        public void Challenge(Player p)
        {
            var c = Chest(p); double n = 0;
            foreach (var e in enemies.Live())
            {
                if (e.dead || e.type == "post" || e.type == "turret") continue;
                if (JMath.Hypot(e.x - c.x, e.y + e.h / 2 - c.y) > SCARF.challengeRange) continue;
                e.taunter = p; e.tauntT = SCARF.tauntTicks; e.target = p; n++;
                if (e.type == "sniper" && (e.state == "aim" || e.state == "lock")) { e.aimX = p.x; e.aimY = p.y + 1.0; }
                if (e.cd > 20) e.cd = 20;
                Emit("taunted", new Ev { e = e });
            }
            AddResolve(p, 4);
            Emit("challenge", new Ev { p = p, x = c.x, y = c.y, n = n });
            var ally = players.Find(q => q != p && q.state != "dead" && q.state != "downed");
            if (n > 0) { Bark(p, "challenge", 0.6); if (ally != null) Schedule(60, () => Bark(ally, "challenge_reply", 0.6, true)); }
        }
        // Veil Signature (Vanish): everything tracking Echo loses him, including a sniper mid-aim
        public void ShakeOffTrackers(Player p)
        {
            foreach (var e in enemies.Live())
            {
                if (e.dead) continue;
                if (e.taunter == p) { e.tauntT = 0; e.taunter = null; }
                if (e.target != p) continue;
                e.target = null;
                if (e.type == "sniper" && e.state == "aim") { director.Release(e); e.state = "idle"; e.st = 0; }
                Emit("lostTrack", new Ev { e = e, p = p });
            }
        }

        // ---- Hunter kit snares ----
        public void ThrowSnare(Player p)
        {
            var c = Chest(p);
            // Bola throw: flies straight at an enemy inside the throw cone; otherwise arcs out and plants as a trap
            var t = NearestEnemyInCone(c.x, c.y, p.aimX, p.aimY, 10, JMath.PI / 6);
            double vx, vy, gravity;
            if (t != null)
            {
                double dx = t.x - c.x, dy = t.y + t.h * 0.5 - c.y, d = or(JMath.Hypot(dx, dy), 1);
                vx = dx / d * 20; vy = dy / d * 20; gravity = 3;
            }
            else { vx = p.aimX * HUNTER.throwSpeed; vy = p.aimY * HUNTER.throwSpeed + HUNTER.throwLift; gravity = HUNTER.snareGravity; }
            SpawnProjectile(new Projectile { team = "p", owner = p, x = c.x + p.aimX * 0.6, y = c.y + p.aimY * 0.6, vx = vx, vy = vy, gravity = gravity, r = 0.3, ttl = 110, dmg = 0, snare = true, kind = "snare" });
            Emit("snareThrow", new Ev { p = p, x = c.x, y = c.y });
        }
        public void PlantSnare(Player p) { AddSnare(p, p.x + p.facing * 0.6, p.y); }
        public void SnareLanded(Projectile pr, double x, double y)
        {
            double gy = Level.GroundBelow(x, y + 0.2);
            if (gy > double.NegativeInfinity && y - gy < 6) AddSnare((Player)pr.owner, x, gy);
        }
        void AddSnare(Player owner, double x, double y)
        {
            var mine = snares.FindAll(s => s.owner == owner);
            if (mine.Count >= HUNTER.maxPlanted) mine[0].dead = true;
            snares.RemoveAll(s => s.dead);
            snares.Add(new Snare { owner = owner, x = x, y = y, armT = HUNTER.armTicks, ttl = HUNTER.life, dead = false });
            Emit("snarePlant", new Ev { x = x, y = y, p = owner });
        }
        public void ApplySnare(Enemy e, Player owner)
        {
            if (e.dead) return;
            if (e.type == "post" || e.type == "turret") { Emit("snared", new Ev { e = e, x = e.x, y = e.y + 0.4, owner = owner, weak = true }); return; }
            e.tagged = JMath.Max(e.tagged, 600);
            if (e.light && e.armor <= 0)
            {
                director.Release(e);
                if (e.catcher != null && e.catcher.leash != null && e.catcher.leash.e == e) ReleaseLeash(e.catcher);
                e.state = "snared"; e.st = 0; e.stun = HUNTER.rootLight; e.vx = 0;
            }
            else HitEnemy(this, e, new Hit { owner = owner, dmg = 0.5, poise = HUNTER.rootHeavyPoise, kb = new[] { 0.0, 0.0 } }, "pulse");
            Emit("snared", new Ev { e = e, x = e.x, y = e.y + e.h * 0.4, owner = owner });
        }
        void UpdateSnares()
        {
            foreach (var s in snares.Live())
            {
                s.ttl--; if (s.armT > 0) { s.armT--; continue; }
                for (int i = 0; i < enemies.Count; i++)
                {
                    var e = enemies[i];
                    if (e.dead || e.state == "snared" || e.type == "turret") continue;
                    if (JMath.Abs(e.x - s.x) < e.w / 2 + 0.45 && e.y < s.y + 0.6 && e.y + e.h > s.y - 0.1)
                    {
                        ApplySnare(e, s.owner); s.dead = true; Emit("snareTrigger", new Ev { x = s.x, y = s.y, e = e });
                        break;
                    }
                }
            }
            snares.RemoveAll(s => s.dead || s.ttl <= 0);
        }

        // Echo's dash chases tagged enemies roughly in the dash direction
        Enemy PursuitTarget(Player p, double dx, double dy)
        {
            if (p.@char != "echo") return null;
            Enemy best = null; double bd = 12;
            var c = Chest(p);
            foreach (var e in enemies.Live())
            {
                if (e.dead || e.tagged <= 0) continue;
                double ex = e.x - c.x, ey = e.y + e.h / 2 - c.y, d = JMath.Hypot(ex, ey);
                if (d < bd && d > 1 && (ex * dx + ey * dy) / d > 0.5) { bd = d; best = e; }
            }
            return best;
        }

        // ---- Lock-on ----
        // Candidates in range, best first: near, in front, in sight (training targets last)
        List<Enemy> LockCandidates(Player p)
        {
            var c = Chest(p); var outL = new List<(Enemy e, double score)>();
            foreach (var e in enemies.Live())
            {
                if (e.dead) continue;
                double ty = e.y + e.h * 0.55, dx = e.x - c.x, d = JMath.Hypot(dx, ty - c.y);
                if (d > LOCK.range) continue;
                bool behind = dx * p.facing < -0.5, blocked = Level.SegmentBlocked(c.x, c.y, e.x, ty);
                outL.Add((e, d + (behind ? 7 : 0) + (blocked ? 10 : 0) + (e.type == "post" || e.type == "turret" ? 4 : 0)));
            }
            StableSort.Sort(outL, (a, b) => a.score - b.score);
            return outL.ConvertAll(o => o.e);
        }
        public Enemy BestLockTarget(Player p) => LockCandidates(p).Find(e => e != p.lockT);
        // Automatic lock-on: the nearest enemy in sight within LOCK.auto
        public Enemy AutoLockTarget(Player p)
        {
            var c = Chest(p);
            return LockCandidates(p).Find(e => JMath.Hypot(e.x - c.x, e.y + e.h * 0.55 - c.y) <= LOCK.auto && !Level.SegmentBlocked(c.x, c.y, e.x, e.y + e.h * 0.55));
        }
        public Enemy NextLockTarget(Player p)
        {
            var list = LockCandidates(p);
            if (list.Count == 0) return p.lockT;
            return list[(list.IndexOf(p.lockT) + 1) % list.Count];
        }
        public void SetLock(Player p, Enemy e, string why)
        {
            var prev = p.lockT;
            p.lockT = e; p.lockLost = 0;
            p.lockPicked = e != null && (why == "on" || why == "cycle");   // chosen by the player, not by automatic lock-on
            if (e != null && e != prev) Emit(prev != null ? "lockSwitch" : "lockOn", new Ev { p = p, e = e, why = why });
            else if (e == null && prev != null) Emit("lockOff", new Ev { p = p, why = why });
            else if (e == null && why == "on") Emit("lockNone", new Ev { p = p });
        }
        // The target died (the lock moves to the next one), left, or is out of range or sight too long
        public void ValidateLock(Player p)
        {
            var t = p.lockT; if (t == null) return;
            if (t.dead || !enemies.Contains(t)) { SetLock(p, BestLockTarget(p), "switch"); return; }
            var c = Chest(p); double ty = t.y + t.h * 0.55;
            if (JMath.Hypot(t.x - c.x, ty - c.y) > LOCK.keep) { SetLock(p, null, "range"); return; }
            p.lockLost = Level.SegmentBlocked(c.x, c.y, t.x, ty) ? p.lockLost + 1 : 0;
            if (p.lockLost > LOCK.lost) SetLock(p, null, "sight");
        }

        public Enemy NearestEnemyInCone(double x, double y, double dx, double dy, double range, double half)
        {
            Enemy best = null; double bd = range, cos = JMath.Cos(half);
            foreach (var e in enemies.Live())
            {
                if (e.dead || e.type == "post" || e.type == "turret") continue;
                double ex = e.x - x, ey = e.y + e.h / 2 - y, d = JMath.Hypot(ex, ey);
                if (d < bd && (ex * dx + ey * dy) / d > cos) { bd = d; best = e; }
            }
            return best;
        }
        // Enemies within range and half-angle of a direction, ordered by signed angle from it
        public List<Enemy> EnemiesInCone(double x, double y, double dx, double dy, double range, double half)
        {
            double cos = JMath.Cos(half); var outL = new List<(Enemy e, double a)>();
            foreach (var e in enemies.Live())
            {
                if (e.dead) continue;
                double ex = e.x - x, ey = e.y + e.h / 2 - y, d = JMath.Hypot(ex, ey);
                if (d > range || d < 1e-3 || (ex * dx + ey * dy) / d < cos) continue;
                outL.Add((e, JMath.Atan2(dx * ey - dy * ex, dx * ex + dy * ey)));
            }
            StableSort.Sort(outL, (a, b) => a.a - b.a);
            return outL.ConvertAll(o => o.e);
        }
        public double NearestEnemyDist(double x, double y)
        {
            double bd = double.PositiveInfinity;
            foreach (var e in enemies.Live()) if (!e.dead) bd = JMath.Min(bd, JMath.Hypot(e.x - x, e.y + e.h / 2 - y));
            return bd;
        }
        public Enemy EnemyBelow(Player p, double dist) =>
            enemies.Find(e => !e.dead && JMath.Abs(e.x - p.x) < (e.w + p.w) / 2 && p.y - (e.y + e.h) < dist && p.y > e.y);
        public bool OnOneWay(Player p)
        {
            foreach (var b in Level.BOXES) if (b.type == 'o' && JMath.Abs(p.y - b.y1) < 0.03 && p.x > b.x0 && p.x < b.x1) return true;
            return false;
        }
        public Player ProjectileTarget(Projectile b, Player savior)
        {
            double sp = or(JMath.Hypot(b.vx, b.vy), 1);
            foreach (var q in players.Live())
            {
                if (q == savior || q.state == "dead" || q.state == "downed") continue;
                double dx = q.x - b.x, dy = q.y + 1 - b.y, along = (dx * b.vx + dy * b.vy) / sp;
                if (along > 0 && along < 5 && JMath.Abs(dx * b.vy - dy * b.vx) / sp < 1.4) return q;
            }
            return null;
        }

        // ---- Barks ----
        public void Bark(Player p, string key, double chance = 1, bool force = false)
        {
            if (!SETTINGS.barks || p == null || JRandom.Next() > chance) return;
            if (!force && (p.barkCd > 0 || globalBarkCd > 0)) return;
            if (!BARKS[p.@char].TryGetValue(key, out var lines)) return;
            p.barkCd = 300; globalBarkCd = 60;
            Emit("bark", new Ev { p = p, text = lines[(int)JMath.Floor(JRandom.Next() * lines.Length)] });
        }

        // Distance from a point to a beam segment, and whether a thick segment touches a box
        public static double DistToSeg(double x, double y, BeamSeg g)
        {
            double vx = g.x1 - g.x0, vy = g.y1 - g.y0, L = vx * vx + vy * vy;
            double t = L > 1e-9 ? JMath.Max(0, JMath.Min(1, ((x - g.x0) * vx + (y - g.y0) * vy) / L)) : 0;
            return JMath.Hypot(x - (g.x0 + vx * t), y - (g.y0 + vy * t));
        }
        static bool SegHitsBox(BeamSeg g, Box2 b, double w)
        {
            double vx = g.x1 - g.x0, vy = g.y1 - g.y0, L = JMath.Hypot(vx, vy);
            if (L < 1e-6) return false;
            var h = Level.RayBoxT(g.x0, g.y0, vx / L, vy / L, b.x0 - w, b.y0 - w, b.x1 + w, b.y1 + w);
            return h != null && h.Value.t <= L;
        }
    }

    // JavaScript's Array.prototype.sort is stable; List<T>.Sort is not
    public static class StableSort
    {
        public static void Sort<T>(List<T> list, Func<T, T, double> cmp)
        {
            // insertion sort: stable, and the lists here are short
            for (int i = 1; i < list.Count; i++)
            {
                var v = list[i]; int j = i - 1;
                while (j >= 0 && cmp(list[j], v) > 0) { list[j + 1] = list[j]; j--; }
                list[j + 1] = v;
            }
        }
    }
}
