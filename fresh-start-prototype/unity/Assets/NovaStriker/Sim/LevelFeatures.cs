// The Unity level features (Settings › Level features; each applies when a zone loads, a checkpoint resets or a
// game starts, through Level.RestoreBoxes):
//   Tiers: upper catwalks and ledges (one-way platforms) in every zone, within double-jump reach (a jump is about
//     3.2 m, a double jump about 5.5 m), with extra enemies placed on them.
//   Hazards: timed machinery that hurts players and enemies alike (so enemies can be lured in): steam vents that
//     launch whoever stands on them, shock floors, crushers, laser gates, wind, lightning, molten slag, crumbling
//     platforms and falling debris. Each runs idle -> warn -> on -> idle on its own tick pattern (no random numbers).
//   Depth lanes: in marked stretches the play area widens into three lanes (back, middle, front). B and M on a
//     keyboard (or L3 with the stick up or down) hop between them; melee, shots and beams only reach their own lane, area attacks reach
//     all three. Leaving a stretch brings everyone back to the middle. Bosses stay in the middle.
// With all three off the level and the rules are exactly as before.
using System.Collections.Generic;
using static NovaStriker.Sim.Cfg;

namespace NovaStriker.Sim
{
    public sealed class Hazard
    {
        public int id;
        public int laneMask = 7;
        public string kind, zone;
        public double x0, x1, y0, y1;
        public int period, warn, on, phase;
        public double dmg, kbx, kby, dir;
        public string state = "idle";   // idle, warn, on, crumble, gone (collapse)
        public int t;
        public LevelBox box;             // (collapse: the platform it controls)
        public double boxY0, boxY1;
        public readonly HashSet<string> hit = new HashSet<string>();
    }

    public static partial class Level
    {
        public const int EXTRA_ID = 10000;
        public static readonly List<Hazard> HAZARDS = new List<Hazard>();
        public static readonly List<(double x0, double x1)> LANES = new List<(double, double)>();
        public static readonly Dictionary<string, SpawnDef[]> TIER_SPAWNS = new Dictionary<string, SpawnDef[]>();
        static bool fTiers, fHazards, fLanes, fApplied;

        // ---- Tiers: one-way catwalks (x0, x1, y0, y1) ----
        static readonly double[][] TIERS = {
            // Movement Gym
            new[] { 6.5, 11.5, 3.6, 4.0 }, new[] { 20, 25, 3.8, 4.2 }, new[] { 50.5, 55.5, 4.0, 4.4 },
            // Concourse Lock: mezzanines either side of the dais, a high walk above it
            new[] { 69.5, 74.5, 6.6, 7.0 }, new[] { 83.5, 88.5, 6.6, 7.0 }, new[] { 76.5, 81.5, 9.8, 10.2 },
            // Storm Spire Climb: side ledges as shortcuts
            new[] { 104, 108, 3.6, 4.0 }, new[] { 108.5, 112, 7.0, 7.4 }, new[] { 113.5, 117.5, 6.2, 6.6 },
            // Skyline Relay: antenna platforms over the bridge and roof, gantries over the yard
            new[] { 170, 175, 19.2, 19.6 }, new[] { 178, 183, 22.4, 22.8 }, new[] { 194, 198, 19.4, 19.8 }, new[] { 207, 211, 19.6, 20.0 },
            new[] { 232, 236, 16.6, 17.0 }, new[] { 240, 243, 17.6, 18.0 },
            // Helix Foundry: crane catwalks over the floor, a high perch in the Crucible
            new[] { 415, 421, 4.0, 4.4 }, new[] { 436, 442, 4.2, 4.6 }, new[] { 452, 458, 4.4, 4.8 }, new[] { 520, 527, 4.0, 4.4 },
            new[] { 533, 539, 7.2, 7.6 }, new[] { 545, 551, 4.0, 4.4 }, new[] { 562, 568, 4.2, 4.6 }, new[] { 725, 731, 31.0, 31.4 },
            // Undercity Descent: balconies and fire escapes
            new[] { 836, 841, 23.8, 24.2 }, new[] { 846, 851, 27.0, 27.4 }, new[] { 896, 900, 23.6, 24.0 },
            new[] { 1050, 1055, 4.0, 4.4 }, new[] { 1058, 1063, 7.2, 7.6 }, new[] { 1090, 1096, 4.0, 4.4 }, new[] { 1104, 1109, 4.2, 4.6 },
        };
        // Crumbling platforms (with hazards on): one-way, they give way 48 ticks after someone stands on them
        static readonly double[][] COLLAPSE = {
            new[] { 222, 227, 16.4, 16.8 }, new[] { 186.4, 189.6, 14.0, 14.4 },          // Skyline yard; under the bridge's gap
            new[] { 1066, 1071, 5.4, 5.8 }, new[] { 1080, 1084, 3.6, 4.0 },               // Undercity transit line
        };
        // Extra enemies on the tiers, joining the first wave of these encounters
        static void TierSpawns()
        {
            TIER_SPAWNS.Clear();
            TIER_SPAWNS["patrol"] = new[] { new SpawnDef("sniper", 180.5, 22.8) };
            TIER_SPAWNS["yard"] = new[] { new SpawnDef("sniper", 234, 17.0) };
            TIER_SPAWNS["f-approach"] = new[] { new SpawnDef("sniper", 439, 4.6), new SpawnDef("drone", 455, 8) };
            TIER_SPAWNS["f-trench"] = new[] { new SpawnDef("sniper", 536, 7.6) };
            TIER_SPAWNS["f-crucible"] = new[] { new SpawnDef("sniper", 728, 31.4) };
            TIER_SPAWNS["u-roofs"] = new[] { new SpawnDef("sniper", 848.5, 27.4) };
            TIER_SPAWNS["u-transit"] = new[] { new SpawnDef("sniper", 1060.5, 7.6) };
            TIER_SPAWNS["u-plaza"] = new[] { new SpawnDef("drone", 1148, 9) };
        }

        // ---- Hazards: kind, x0, x1, y0 (floor), y1 (top of the danger), period, warn, on, phase, damage, kb x, kb y ----
        static readonly object[][] HAZARD_ROWS = {
            // Movement Gym: two steam vents as launch pads (harmless here)
            new object[] { "vent", 9.0, 10.4, 0.0, 6.0, 180, 50, 40, 0, 0.0, 0.0, 22.0 },
            new object[] { "vent", 24.0, 25.4, 0.0, 6.0, 180, 50, 40, 90, 0.0, 0.0, 22.0 },
            // Concourse Lock: shock floor panels, a low laser gate to jump
            new object[] { "shock", 70.0, 73.0, 0.0, 0.4, 300, 60, 120, 0, 6.0, 0.0, 6.0 },
            new object[] { "shock", 85.0, 88.0, 0.0, 0.4, 300, 60, 120, 150, 6.0, 0.0, 6.0 },
            new object[] { "laser", 78.6, 79.4, 0.3, 1.2, 240, 45, 90, 60, 10.0, 6.0, 5.0 },
            // Storm Spire: a headwind up the climb, lightning on three marked spots in turn
            new object[] { "wind", 103.0, 118.0, 0.0, 9.0, 420, 60, 180, 0, 0.0, -3.5, 0.0 },
            new object[] { "lightning", 124.0, 125.6, 0.0, 16.0, 210, 70, 10, 0, 18.0, 4.0, 10.0 },
            new object[] { "lightning", 131.4, 133.0, 0.0, 16.0, 210, 70, 10, 70, 18.0, 4.0, 10.0 },
            new object[] { "lightning", 139.0, 140.6, 0.0, 16.0, 210, 70, 10, 140, 18.0, 4.0, 10.0 },
            // Skyline Relay: a crosswind on the bridge, a laser fence on the roof
            new object[] { "wind", 168.0, 185.0, 15.6, 24.0, 360, 60, 150, 100, 0.0, 3.0, 0.0 },
            new object[] { "laser", 213.0, 213.8, 16.0, 17.0, 260, 45, 100, 0, 10.0, 6.0, 5.0 },
            // Helix Foundry: crushers, a slag channel, a vent
            new object[] { "crusher", 443.0, 445.4, 0.0, 6.0, 200, 60, 20, 0, 24.0, 8.0, 9.0 },
            new object[] { "crusher", 540.0, 542.4, 0.0, 6.0, 200, 60, 20, 100, 24.0, 8.0, 9.0 },
            new object[] { "slag", 523.0, 527.0, 0.0, 0.6, 1, 0, 1, 0, 4.0, 0.0, 8.0 },
            new object[] { "vent", 576.0, 577.4, 0.0, 7.0, 200, 50, 40, 0, 3.0, 0.0, 22.0 },
            // Undercity Descent: shock grates, falling debris
            new object[] { "shock", 870.0, 873.0, 20.0, 20.4, 280, 60, 110, 0, 6.0, 0.0, 6.0 },
            new object[] { "debris", 1043.0, 1044.6, 0.0, 9.0, 240, 70, 48, 0, 16.0, 3.0, 4.0 },
            new object[] { "shock", 1093.0, 1096.0, 0.0, 0.4, 300, 60, 120, 60, 6.0, 0.0, 6.0 },
            new object[] { "debris", 1113.0, 1114.6, 0.0, 9.0, 240, 70, 48, 120, 16.0, 3.0, 4.0 },
        };

        // ---- Depth lanes: the stretches where three lanes open (x0, x1) ----
        static readonly double[][] LANE_ROWS = {
            new[] { 41.0, 56.0 }, new[] { 64.0, 95.0 }, new[] { 191.0, 213.0 }, new[] { 259.0, 296.0 },
            new[] { 404.0, 445.0 }, new[] { 465.0, 515.0 }, new[] { 703.0, 754.0 }, new[] { 1011.0, 1084.0 }, new[] { 1126.0, 1173.0 },
        };

        static Zone ZoneOf(double x) => ZoneAt(x);

        // Add or remove the features' boxes, hazards and lanes. With everything off, BOXES is exactly as built.
        public static void ApplyFeatures(bool tiers, bool hazards, bool lanes)
        {
            if (fApplied && tiers == fTiers && hazards == fHazards && lanes == fLanes)
            {
                foreach (var h in HAZARDS) ResetHazard(h);
                return;
            }
            fApplied = true; fTiers = tiers; fHazards = hazards; fLanes = lanes;
            BOXES.RemoveAll(b => b.id >= EXTRA_ID);
            HAZARDS.Clear(); LANES.Clear(); TIER_SPAWNS.Clear();
            int id = EXTRA_ID;
            if (tiers)
            {
                foreach (var r in TIERS) BOXES.Add(new LevelBox { id = id++, x0 = r[0], x1 = r[1], y0 = r[2], y1 = r[3], type = 'o', tag = "tier" });
                TierSpawns();
            }
            if (hazards)
            {
                int hid = 0;
                foreach (var r in HAZARD_ROWS)
                {
                    var h = new Hazard { id = hid++, kind = (string)r[0], x0 = (double)r[1], x1 = (double)r[2], y0 = (double)r[3], y1 = (double)r[4],
                        period = (int)r[5], warn = (int)r[6], on = (int)r[7], phase = (int)r[8], dmg = (double)r[9], kbx = (double)r[10], kby = (double)r[11] };
                    h.zone = ZoneOf((h.x0 + h.x1) / 2).id; h.dir = h.kbx;
                    HAZARDS.Add(h);
                }
                foreach (var r in COLLAPSE)
                {
                    var b = new LevelBox { id = id++, x0 = r[0], x1 = r[1], y0 = r[2], y1 = r[3], type = 'o', tag = "collapse" };
                    BOXES.Add(b);
                    HAZARDS.Add(new Hazard { id = hid++, kind = "collapse", x0 = r[0], x1 = r[1], y0 = r[2], y1 = r[3], box = b, boxY0 = r[2], boxY1 = r[3], zone = ZoneOf(r[0]).id });
                }
            }
            if (lanes) foreach (var r in LANE_ROWS) LANES.Add((r[0], r[1]));
            Version++;
        }

        static void ResetHazard(Hazard h)
        {
            h.state = "idle"; h.t = 0; h.hit.Clear();
            if (h.box != null) { h.box.y0 = h.boxY0; h.box.y1 = h.boxY1; }
        }

        public static bool InLaneStretch(double x)
        {
            foreach (var (x0, x1) in LANES) if (x >= x0 && x < x1) return true;
            return false;
        }
    }

    public static class LevelFeatures
    {
        public const double LANE_W = 1.4;    // m between lanes
        public const int HOP = 10;           // ticks a lane hop takes (the lane changes halfway)

        // Can a and b reach each other? (always, without lanes; area attacks reach every lane)
        public static bool Same(Actor a, Actor b, bool area = false) =>
            area || Level.LANES.Count == 0 || a == null || b == null || a.lane == b.lane;
        public static bool Same(int lane, Actor b, bool area = false) =>
            area || Level.LANES.Count == 0 || b == null || lane == b.lane;

        // Where a body is drawn in depth (m toward the camera), easing through a hop
        public static double Depth(Body b)
        {
            if (b == null) return 0;
            if (b.laneT <= 0) return b.laneTo * LANE_W;
            double k = 1 - b.laneT / (double)HOP; k = k * k * (3 - 2 * k);
            return (b.laneFrom + (b.laneTo - b.laneFrom) * k) * LANE_W;
        }

        public static bool Danger(double x, double y, int lane, double margin = 0.6)
        {
            foreach (var h in Level.HAZARDS)
                if (h.dmg > 0 && Level.InLane(h.laneMask, lane) && (h.state == "warn" || h.state == "on") && x > h.x0 - margin && x < h.x1 + margin && y < h.y1 && y + 1.5 > h.y0) return true;
            return false;
        }

        public static bool CanHop(Actor a, int to)
        {
            if (a == null || to < -1 || to > 1 || a.laneT > 0) return false;
            if (!Level.HasHeadroom(a.x, a.y, a.w, a.h, to)) return false;
            if (a is Enemy e && e.flier) return true;
            double floor = Level.GroundBelow(a.x, a.y + 0.1, to);
            return !double.IsNegativeInfinity(floor) && floor >= Level.KillYAt(a.x) && (!a.onGround || a.y - floor < 0.15);
        }

        static void StartHop(World w, Actor a, int to)
        {
            if (to == a.laneTo || a.laneT > 0) return;
            if (!CanHop(a, to)) { w.Emit("laneBlocked", new Ev { p = a as Player, owner = a, x = a.x, y = a.y, n = to }); return; }
            a.laneFrom = a.laneTo; a.laneTo = to; a.laneT = HOP;
            w.Emit("laneHop", new Ev { x = a.x, y = a.y, n = to, owner = a, p = a as Player, e = a as Enemy });
        }

        static void TickLane(Actor a)
        {
            if (a.laneT <= 0) return;
            a.laneT--;
            if (a.laneT == HOP / 2) a.lane = a.laneTo;
            if (a.laneT == 0) a.lane = a.laneTo;
        }

        public static void Step(World w, Dictionary<int, Cmd> cmds, bool frozen)
        {
            if (Level.LANES.Count > 0) StepLanes(w, cmds);
            if (Level.HAZARDS.Count > 0 && !frozen) StepHazards(w);
        }

        // ---- Lanes ----
        static void StepLanes(World w, Dictionary<int, Cmd> cmds)
        {
            foreach (var p in w.players.Live())
            {
                TickLane(p);
                if (p.state == "dead") continue;
                bool open = Level.InLaneStretch(p.x);
                if (!open) { if (p.laneTo != 0) StartHop(w, p, 0); continue; }
                int want = 0;
                if (cmds != null && cmds.TryGetValue(p.slot, out var c) && c.lane != 0) want = c.lane;
                else if (Bots.IsBot(p))
                {
                    // AI teammates keep to the lane of the nearest human after a moment
                    Player near = null; double best = 1e9;
                    foreach (var q in w.players.Live()) if (!Bots.IsBot(q) && q.state != "dead") { double d = JMath.Abs(q.x - p.x); if (d < best) { best = d; near = q; } }
                    if (near != null && near.laneTo != p.laneTo) { p.laneWait++; if (p.laneWait > 30) { want = near.laneTo > p.laneTo ? 1 : -1; p.laneWait = 0; } }
                    else p.laneWait = 0;
                }
                if (want != 0 && p.state != "downed" && p.state != "ult")
                {
                    int to = p.laneTo + (want > 0 ? 1 : -1);
                    if (to >= -1 && to <= 1) StartHop(w, p, to);
                }
            }
            foreach (var e in w.enemies.Live())
            {
                TickLane(e);
                if (e.dead || e.boss) continue;
                if (!Level.InLaneStretch(e.x)) { if (e.laneTo != 0) StartHop(w, e, 0); continue; }
                // hop toward the nearest player's lane after a while apart
                Player near = null; double best = 1e9;
                foreach (var q in w.players.Live()) if (q.state != "dead" && q.state != "downed") { double d = JMath.Abs(q.x - e.x); if (d < best) { best = d; near = q; } }
                if (near != null && near.laneTo != e.laneTo && best < 14) { e.laneWait++; if (e.laneWait > 72) { StartHop(w, e, e.laneTo + (near.laneTo > e.laneTo ? 1 : -1)); e.laneWait = 0; } }
                else e.laneWait = 0;
            }
        }

        // ---- Hazards ----
        static void StepHazards(World w)
        {
            foreach (var h in Level.HAZARDS)
            {
                if (h.kind == "collapse") { StepCollapse(w, h); continue; }
                if (h.kind == "slag") { h.state = "on"; Harm(w, h, 20); continue; }
                long c = ((long)w.tick + h.phase) % h.period;
                string st = c < h.period - h.warn - h.on ? "idle" : c < h.period - h.on ? "warn" : "on";
                if (st != h.state)
                {
                    h.state = st; h.hit.Clear();
                    w.Emit(st == "warn" ? "hazardWarn" : st == "on" ? "hazardOn" : "hazardOff", Ev(h));
                }
                if (st != "on") continue;
                // Machinery descends through empty air before its impact damage window.
                if ((h.kind == "crusher" || h.kind == "debris") && c - (h.period - h.on) < h.on * 0.65) continue;
                if (h.kind == "wind") { Push(w, h); continue; }
                if (h.kind == "vent") { Launch(w, h); continue; }
                Harm(w, h, h.kind == "shock" ? 20 : 0);
            }
        }

        static Ev Ev(Hazard h) => new Ev { id = h.id.ToString(), kind = h.kind, x = (h.x0 + h.x1) / 2, y = h.y0, x0 = h.x0, x1 = h.x1, y0 = h.y0, y1 = h.y1, dir = h.dir };

        public static bool Inside(Hazard h, Body b) => Level.InLane(h.laneMask, b.lane) && b.x + b.w / 2 > h.x0 && b.x - b.w / 2 < h.x1 && b.y < h.y1 && b.y + b.h > h.y0;
        static bool ImpactInside(Hazard h, Body b) => Inside(h,b) && ((h.kind != "crusher" && h.kind != "debris") || b.y < h.y0 + 1.2);

        // Damage whoever is inside: once per `on` window, or every `every` ticks for lasting hazards
        static void Harm(World w, Hazard h, int every)
        {
            if (every > 0 && (long)w.tick % every == 0) h.hit.Clear();
            foreach (var p in w.players.Live())
            {
                if (p.state == "dead" || p.state == "downed" || !ImpactInside(h, p) || h.hit.Contains("p" + p.slot)) continue;
                if (h.kind == "shock" && !p.onGround) continue;   // (jump to clear a live floor)
                h.hit.Add("p" + p.slot);
                if (h.dmg <= 0) continue;
                Combat.HitPlayer(w, p, Hit(h, p.x, p.y));
                w.Emit("hazardHit", new Ev { id = h.id.ToString(), kind = h.kind, x = p.x, y = p.y + p.h * 0.5, p = p });
            }
            foreach (var e in w.enemies.Live())
            {
                if (e.dead || e.boss || !ImpactInside(h, e) || h.hit.Contains("e" + e.id)) continue;
                if (h.kind == "shock" && !e.onGround) continue;
                h.hit.Add("e" + e.id);
                if (h.dmg <= 0) continue;
                Combat.HitEnemy(w, e, Hit(h, e.x, e.y), "blast");
                w.Emit("hazardHit", new Ev { id = h.id.ToString(), kind = h.kind, x = e.x, y = e.y + e.h * 0.5, e = e });
            }
        }

        static Hit Hit(Hazard h, double x, double y)
        {
            double side = x < (h.x0 + h.x1) / 2 ? -1 : 1;
            return new Hit { team = "h", dmg = h.dmg, poise = h.dmg * 2, kb = new[] { side * h.kbx, h.kby }, heavy = h.kind == "crusher" || h.kind == "lightning",
                cat = "unblockable", unblockable = true, at = new V2((h.x0 + h.x1) / 2, (h.y0 + h.y1) / 2), ground = true };
        }

        static void Push(World w, Hazard h)
        {
            foreach (var p in w.players.Live()) if (p.state != "dead" && Inside(h, p)) p.x += h.dir * DT;
            foreach (var e in w.enemies.Live()) if (!e.dead && !e.boss && Inside(h, e)) e.x += h.dir * 0.6 * DT;
        }

        static void Launch(World w, Hazard h)
        {
            foreach (var p in w.players.Live())
            {
                if (p.state == "dead" || p.state == "downed" || !Inside(h, p) || h.hit.Contains("p" + p.slot)) continue;
                h.hit.Add("p" + p.slot);
                // thrown up as a lift pad throws (World.LiftTick): no jump cut, the air moves kept
                p.vy = h.kby; p.onGround = false; p.coyote = 0; p.jumpsUsed = 0; p.airDashes = 1; p.airRise = true; p.fastFall = false; p.dashCarry = true; p.padCd = 30;
                if (h.dmg > 0) Combat.HitPlayer(w, p, Hit(h, p.x, p.y));
                w.Emit("hazardHit", new Ev { id = h.id.ToString(), kind = h.kind, x = p.x, y = p.y, p = p });
            }
            foreach (var e in w.enemies.Live())
            {
                if (e.dead || e.boss || e.flier || !Inside(h, e) || h.hit.Contains("e" + e.id)) continue;
                h.hit.Add("e" + e.id); e.vy = h.kby * 0.8; e.onGround = false;
                if (h.dmg > 0) Combat.HitEnemy(w, e, Hit(h, e.x, e.y), "blast");
            }
        }

        // A crumbling platform: someone stands on it, it shakes for 48 ticks and drops away, back after 360
        static void StepCollapse(World w, Hazard h)
        {
            var b = h.box;
            if (h.state == "idle")
            {
                bool stood = false;
                foreach (var p in w.players.Live()) if (p.onGround && Level.InLane(b.laneMask, p.lane) && JMath.Abs(p.y - b.y1) < 0.05 && p.x > b.x0 && p.x < b.x1) stood = true;
                foreach (var e in w.enemies.Live()) if (!e.dead && e.onGround && Level.InLane(b.laneMask, e.lane) && JMath.Abs(e.y - b.y1) < 0.05 && e.x > b.x0 && e.x < b.x1) stood = true;
                if (stood) { h.state = "warn"; h.t = 48; w.Emit("hazardWarn", Ev(h)); }
            }
            else if (h.state == "warn") { if (--h.t <= 0) { h.state = "on"; h.t = 360; b.y0 = -1000; b.y1 = -999.6; w.Emit("hazardOn", Ev(h)); } }
            else if (h.state == "on") { if (--h.t <= 0) { h.state = "idle"; b.y0 = h.boxY0; b.y1 = h.boxY1; w.Emit("hazardOff", Ev(h)); } }
        }
    }
}
