// Tuning data (port of config.js). Units: metres, seconds. Frame data is in simulation ticks (60 per second).
// Field names follow the prototype's, so the two can be read side by side. Per-level tables are arrays indexed
// by level (index 0 is the tap, or unused).
using System.Collections.Generic;

namespace NovaStriker.Sim
{
    public sealed class DashDef { public double ticks, speed, exitKeep, cooldown; }
    public sealed class SlideDef { public double ticks, speed, decay; }
    public sealed class WallJumpDef { public double slide, jumpVx, jumpVy, @lock; }
    public sealed class CharDef
    {
        public string name, role;
        public double hp, run, backpedal, accelG, decelG, accelA, crouchSpeed, jumpV, dblV;
        public DashDef dash; public SlideDef slide; public WallJumpDef wall;
        public double width, height, crouchH, scale;   // scale: 0 where the prototype leaves it out (drawn at 1)
        public string energy, trim, @base, under;
    }

    // A burst, splash or blast: radius, damage, poise and the extras some of them carry
    public sealed class Blast
    {
        public double r, dmg, poise, kb, up, stun;
        public bool armorBreak, rocket;
        public Blast Clone() => (Blast)MemberwiseClone();
    }
    public sealed class HitBoxDef { public double fx, y, w, h; }
    public sealed class WaveDef { public double speed, ttl, dmg, poise, r; }
    public sealed class QuakeDef { public double dmg, poise, speed, ttl, h; }
    public sealed class MoveDef
    {
        public string id;
        public double su, ac, rc;
        public HitBoxDef box;
        public double dmg, poise;
        public double[] kb;
        public string next;
        public bool shove, armorBreak, air, fist, offhand, heavy, launcher, blade, staff, hover, deflect, cross, glaive, spin, wall, shield, sweep, wrench, jack;
        public Blast blastFist, riseBlast, spark;
        public double rise, airRise, multi, hoverAll;
        public WaveDef wave;
        public QuakeDef quake;
        public string blastKind;
    }

    [System.Serializable]
    public sealed class Settings
    {
        public string novaKit = "marksman";   // 'marksman' (projectile proposal) or 'sentinel' (Pass 1 kit)
        public string echoKit = "hunter";     // 'hunter' (close-range proposal) or 'pursuit' (Pass 1 kit)
        public string echoHead = "bare";      // presentation only: 'helmet', 'mask' or 'bare' (Unity: bare, as in his concept art)
        public string novaHead = "helmet";    // presentation only (Unity only): 'helmet' (knocked off at critical health) or 'bare' (his face)
        public string echoRanged = "B";       // Pursuit kit only. A: Tracer only · B: Bolts + Tracer · C: no ranged
        public bool dashIframes = false;
        public string vbStop = "hard";        // 'hard' stop or 'keep30' momentum
        public bool vbRefund = true;
        public bool impactFrames = true;
        public string impactStyle = "scifi";  // scifi, comic, eclipse, shatter, thunder, sumi, warp
        public string impactColor = "style";  // 'style', 'player' or 'character'
        public double echoSpinStun = 0.85;    // seconds (0.25 to 3; heavy enemies about half)
        public double impactDuration = 0.4;   // seconds (0.3 to 5)
        public string camera = "persp";
        public double fov = 34;
        public bool aimAssist = true;
        public string difficulty = "normal";
        public bool shake = true;
        public string quality = "high";
        public bool reflections = true;       // presentation only (Unity only): the deck's mirror sheen (High and Ultra)
        public string charModels = "models";  // 'builtin' rigs, or 'models' where one exists (Unity: Nova's comes with the project)
        public bool hitboxes = false;
        public bool barks = true;
        public double volume = 0.6;
        public double music = 0.6;
        public string p1Aim = "mouse";
        public bool lockOn = true;
        public string lockMode = "auto";      // 'auto' or 'manual'
        public bool dashCharge = true;
        public bool haptics = true;
        public double hapticStrength = 0.8;
        public string echoBelt = "fire";      // Echo's utility belt: on a tap of fire, or on LB ('lb')
        public double aiTeammates = 0;        // computer-controlled players filling the team's empty slots (0-3)
        public string aiSkill = "veteran";    // rookie, veteran or elite
        // Unity build options
        public string novaDefense = "dodge";  // Nova's Marksman kit on the parry button: 'dodge', or 'shield' (absorbs hits into damage)
        public bool novaParryStun = false;    // Nova's perfect parry (or perfect shield block) stuns the attacker
        public bool hdr = false;              // HDR output on displays that support it
        public double settingsVersion = 11;

        public Settings Clone() => (Settings)MemberwiseClone();
    }

    public static partial class Cfg
    {
        public const double TICK_HZ = 60;
        public const double DT = 1.0 / TICK_HZ;

        public const double GRAVITY = 49;          // gives a ~3.2 m jump that peaks in ~0.36 s
        public const double FALL_MULT = 1.35;      // heavier on the way down
        public const double RISE_CUT_MULT = 2.4;   // releasing jump while rising cuts the arc
        public const double MAX_FALL = 22;
        public const double FAST_FALL = 30;
        public const double HIGH_VEL = 16;         // speed above which melee becomes a Velocity Break

        public const double COYOTE = 7;
        public const double JUMP_BUFFER = 8;
        public const double ACTION_BUFFER = 8;
        public const double PARRY_BUFFER = 3;

        public static class PARRY { public const double window = 12, perfect = 4, whiff = 16; }
        public const double MERCY_TICKS = 60;

        public static readonly Dictionary<string, CharDef> CHARS = new Dictionary<string, CharDef>
        {
            ["nova"] = new CharDef
            {
                name = "Nova", role = "Sentinel", hp = 100,
                run = 7.4, backpedal = 0.8, accelG = 95, decelG = 120, accelA = 60, crouchSpeed = 0.4,
                jumpV = 17.7, dblV = 15.2,
                dash = new DashDef { ticks = 11, speed = 22, exitKeep = 0.3, cooldown = 18 },
                slide = new SlideDef { ticks = 16, speed = 12.5, decay = 0.965 },
                wall = new WallJumpDef { slide = 3.6, jumpVx = 9.5, jumpVy = 16, @lock = 7 },
                width = 0.72, height = 1.72, crouchH = 0.95,
                energy = "#ffb547", trim = "#2f5f9e", @base = "#eef2f7", under = "#1c2c48",
            },
            ["echo"] = new CharDef
            {
                name = "Echo", role = "Pursuit", hp = 100,
                run = 8.8, backpedal = 0.65, accelG = 70, decelG = 50, accelA = 52, crouchSpeed = 0.45,
                jumpV = 17.7, dblV = 15.2,
                dash = new DashDef { ticks = 13, speed = 24, exitKeep = 0.6, cooldown = 16 },
                slide = new SlideDef { ticks = 18, speed = 13.5, decay = 0.972 },
                wall = new WallJumpDef { slide = 4.2, jumpVx = 10.5, jumpVy = 16, @lock = 6 },
                width = 0.68, height = 1.74, crouchH = 0.95,
                energy = "#ff9a1f", trim = "#15151b", @base = "#f4f4f2", under = "#1b1b22",
            },
            // RAM: the tank. Much bigger and heavier than the others: slower on his feet, lower jumps, more health
            ["ram"] = new CharDef
            {
                name = "RAM", role = "Vanguard", hp = 350,
                run = 6.3, backpedal = 0.75, accelG = 62, decelG = 85, accelA = 42, crouchSpeed = 0.35,
                jumpV = 16.4, dblV = 13.8,
                dash = new DashDef { ticks = 14, speed = 15, exitKeep = 0.35, cooldown = 26 },
                slide = new SlideDef { ticks = 16, speed = 11.5, decay = 0.965 },
                wall = new WallJumpDef { slide = 3.0, jumpVx = 9, jumpVy = 15.8, @lock = 8 },
                width = 1.04, height = 2.3, crouchH = 0.95, scale = 1.34,
                energy = "#58a6ff", trim = "#2d3540", @base = "#aeb8c4", under = "#1b2129",
            },
            // Fix: the support. A little smaller and lighter than Nova and Echo.
            ["fix"] = new CharDef
            {
                name = "Fix", role = "Mechanic", hp = 95,
                run = 7.8, backpedal = 0.8, accelG = 88, decelG = 95, accelA = 58, crouchSpeed = 0.45,
                jumpV = 17.9, dblV = 15.4,
                dash = new DashDef { ticks = 12, speed = 23, exitKeep = 0.4, cooldown = 16 },
                slide = new SlideDef { ticks = 18, speed = 13, decay = 0.97 },
                wall = new WallJumpDef { slide = 3.8, jumpVx = 10, jumpVy = 16, @lock = 6 },
                width = 0.64, height = 1.6, crouchH = 0.9, scale = 0.94,
                energy = "#3cf0b0", trim = "#2e7d6f", @base = "#f3f0ea", under = "#2a2e36",
            },
        };
        // Character order for joining (each new player takes the next one not in use) and for swapping
        public static readonly string[] ROSTER = { "nova", "echo", "ram", "fix" };
        public static string NextChar(string c, int dir = 1) =>
            ROSTER[(System.Array.IndexOf(ROSTER, c) + dir + ROSTER.Length) % ROSTER.Length];

        // Wall play (every character)
        public static class WALL
        {
            public const double grip = 8, gripSpeed = 1.1, ease = 14, brake = 90, fast = 2.2, stick = 7, coyote = 6, leapVy = 0.92;
            public static class climb { public const double vx = 4.2, @lock = 5; }
        }

        // Charged dash
        public static class DASH_CHARGE
        {
            public const double tap = 6, jumpCarry = 25;
            public static readonly double[] charge = { 16, 34, 54 }, speed = { 1.15, 1.3, 1.45 }, ticks = { 1.25, 1.5, 1.75 },
                exitKeep = { 0.45, 0.55, 0.65 }, iframes = { 0, 10, 99 };
            public static readonly Blast strike = new Blast { dmg = 2.5, poise = 40, kb = 9 };
        }

        // Lock-on
        public static class LOCK { public const double range = 18, keep = 24, hold = 20, lost = 90, magnet = 2.8, lunge = 18, auto = 14; }

        static HitBoxDef B(double fx, double y, double w, double h) => new HitBoxDef { fx = fx, y = y, w = w, h = h };
        static double[] KB(double x, double y) => new[] { x, y };

        // Melee frame data. box = hitbox relative to feet: fx forward offset, y centre, w, h.
        public static readonly Dictionary<string, MoveDef> MOVES = BuildMoves();
        static Dictionary<string, MoveDef> BuildMoves()
        {
            var M = new Dictionary<string, MoveDef>
            {
                ["nova_jab1"] = new MoveDef { su = 5, ac = 3, rc = 12, box = B(0.8, 1.15, 1.0, 0.7), dmg = 1.2, poise = 12, kb = KB(2, 0), next = "nova_jab2" },
                ["nova_jab2"] = new MoveDef { su = 5, ac = 3, rc = 12, box = B(0.8, 1.15, 1.0, 0.7), dmg = 1.2, poise = 12, kb = KB(2.5, 0), next = "nova_shove" },
                ["nova_shove"] = new MoveDef { su = 8, ac = 4, rc = 16, box = B(0.85, 1.1, 1.2, 1.1), dmg = 1.6, poise = 30, kb = KB(11, 3), shove = true },
                ["nova_brace"] = new MoveDef { su = 13, ac = 4, rc = 18, box = B(0.9, 1.1, 1.4, 1.3), dmg = 2.5, poise = 55, kb = KB(15, 4), shove = true, armorBreak = true },
                ["nova_air"] = new MoveDef { su = 5, ac = 4, rc = 10, box = B(0.75, 0.9, 1.1, 1.0), dmg = 1.2, poise = 12, kb = KB(4, 2), air = true },
                // Marksman kit, close range
                ["nova_k1"] = new MoveDef { su = 4, ac = 3, rc = 11, box = B(0.85, 1.15, 1.15, 0.85), dmg = 1.8, poise = 16, kb = KB(2.5, 0), next = "nova_k2", fist = true },
                ["nova_k2"] = new MoveDef { su = 4, ac = 3, rc = 11, box = B(0.85, 1.0, 1.15, 0.95), dmg = 1.8, poise = 16, kb = KB(2.5, 0), next = "nova_k3", fist = true, offhand = true },
                ["nova_k3"] = new MoveDef { su = 7, ac = 4, rc = 16, box = B(1.0, 1.1, 1.45, 1.15), dmg = 3.5, poise = 45, kb = KB(12, 4), shove = true, fist = true, heavy = true,
                    blastFist = new Blast { r = 1.3, dmg = 1.5, poise = 15 } },
                ["nova_kair"] = new MoveDef { su = 5, ac = 4, rc = 10, box = B(0.8, 0.65, 1.25, 1.1), dmg = 2.2, poise = 20, kb = KB(4, -2), air = true, fist = true },
                // Solar Uppercut
                ["nova_rise"] = new MoveDef { su = 4, ac = 14, rc = 14, box = B(0.45, 1.55, 1.35, 2.1), dmg = 1.4, poise = 16, kb = KB(1, 16), launcher = true, fist = true,
                    rise = 16, airRise = 0.8, multi = 5, riseBlast = new Blast { r = 1.7, dmg = 3, poise = 40 } },

                ["echo_g1"] = new MoveDef { su = 4, ac = 3, rc = 10, box = B(0.75, 1.15, 1.0, 0.7), dmg = 1.3, poise = 12, kb = KB(1.5, 0), next = "echo_g2", blade = true },
                ["echo_g2"] = new MoveDef { su = 5, ac = 3, rc = 11, box = B(0.8, 1.15, 1.1, 0.8), dmg = 1.3, poise = 12, kb = KB(2, 0), next = "echo_g3", blade = true },
                ["echo_g3"] = new MoveDef { su = 7, ac = 4, rc = 16, box = B(1.0, 1.0, 1.9, 1.2), dmg = 2.2, poise = 28, kb = KB(7, 2), staff = true },
                ["echo_launch"] = new MoveDef { su = 8, ac = 4, rc = 14, box = B(0.8, 1.4, 1.2, 1.6), dmg = 1.5, poise = 20, kb = KB(1.5, 15), launcher = true, staff = true },
                ["echo_air1"] = new MoveDef { su = 4, ac = 3, rc = 9, box = B(0.75, 0.95, 1.1, 1.0), dmg = 1.1, poise = 10, kb = KB(1.5, 5), air = true, hover = true, next = "echo_air2", blade = true },
                ["echo_air2"] = new MoveDef { su = 4, ac = 3, rc = 9, box = B(0.75, 0.95, 1.1, 1.0), dmg = 1.1, poise = 10, kb = KB(1.5, 5), air = true, hover = true, next = "echo_air3", blade = true },
                ["echo_air3"] = new MoveDef { su = 6, ac = 4, rc = 12, box = B(0.9, 0.9, 1.6, 1.2), dmg = 1.8, poise = 22, kb = KB(7, 1), air = true, staff = true },
                // Charged staff swing; the Hunter kit also looses a crescent wave that flies on
                ["echo_charged"] = new MoveDef { su = 14, ac = 4, rc = 18, box = B(1.1, 1.0, 2.2, 1.4), dmg = 7, poise = 60, kb = KB(10, 3), armorBreak = true, staff = true, heavy = true, deflect = true,
                    wave = new WaveDef { speed = 22, ttl = 38, dmg = 5, poise = 40, r = 0.75 } },
                ["echo_riposte"] = new MoveDef { su = 2, ac = 4, rc = 12, box = B(1.0, 1.1, 1.8, 1.4), dmg = 7, poise = 80, kb = KB(8, 3), staff = true, deflect = true },

                // Hunter kit
                ["echo_b1"] = new MoveDef { su = 3, ac = 2, rc = 8, box = B(0.7, 1.15, 1.05, 0.85), dmg = 1.7, poise = 12, kb = KB(1, 0), next = "echo_b2", blade = true },
                ["echo_b2"] = new MoveDef { su = 3, ac = 2, rc = 8, box = B(0.75, 1.15, 1.1, 0.85), dmg = 1.7, poise = 12, kb = KB(1, 0), next = "echo_b3", blade = true, offhand = true },
                ["echo_b3"] = new MoveDef { su = 4, ac = 3, rc = 9, box = B(0.85, 1.1, 1.3, 1.0), dmg = 2.1, poise = 16, kb = KB(1.5, 0), next = "echo_b4", blade = true, cross = true },
                ["echo_b4"] = new MoveDef { su = 5, ac = 5, rc = 13, box = B(1.1, 1.0, 2.3, 1.4), dmg = 4.2, poise = 40, kb = KB(8, 2), staff = true, glaive = true, deflect = true },
                // Rising Glaive
                ["echo_rise"] = new MoveDef { su = 3, ac = 15, rc = 12, box = B(0.55, 1.35, 1.6, 2.1), dmg = 1.5, poise = 18, kb = KB(1.5, 16), launcher = true, staff = true, glaive = true,
                    rise = 15, multi = 5, deflect = true },
                ["echo_ab1"] = new MoveDef { su = 3, ac = 3, rc = 8, box = B(0.75, 0.95, 1.15, 1.05), dmg = 1.6, poise = 11, kb = KB(1.5, 5), air = true, hover = true, next = "echo_ab2", blade = true },
                ["echo_ab2"] = new MoveDef { su = 3, ac = 3, rc = 8, box = B(0.75, 0.95, 1.15, 1.05), dmg = 1.6, poise = 11, kb = KB(1.5, 5), air = true, hover = true, next = "echo_ab3", blade = true, offhand = true },
                ["echo_ab3"] = new MoveDef { su = 5, ac = 4, rc = 11, box = B(0.95, 0.9, 1.8, 1.3), dmg = 3.2, poise = 28, kb = KB(7, 1), air = true, staff = true, glaive = true, deflect = true },
                // Spin Slash
                ["echo_spin"] = new MoveDef { su = 2, ac = 16, rc = 10, box = B(0, 0.9, 2.6, 2.4), dmg = 1.3, poise = 12, kb = KB(4, 4), air = true, staff = true, glaive = true,
                    spin = true, multi = 4, hoverAll = 0.35, deflect = true },
                // Wall Slash
                ["echo_wall"] = new MoveDef { su = 3, ac = 4, rc = 10, box = B(1.1, 1.0, 2.1, 2.0), dmg = 2.6, poise = 26, kb = KB(7, 3), air = true, blade = true, wall = true, deflect = true },

                // RAM
                ["ram_b1"] = new MoveDef { su = 7, ac = 4, rc = 13, box = B(1.05, 1.3, 1.6, 1.9), dmg = 2.6, poise = 24, kb = KB(5, 1), next = "ram_b2", shield = true },
                ["ram_b2"] = new MoveDef { su = 6, ac = 4, rc = 13, box = B(1.05, 1.3, 1.7, 1.9), dmg = 2.6, poise = 24, kb = KB(5, 1), next = "ram_b3", shield = true, offhand = true },
                ["ram_b3"] = new MoveDef { su = 10, ac = 5, rc = 18, box = B(1.2, 1.2, 2.0, 2.0), dmg = 5.5, poise = 65, kb = KB(14, 4), shove = true, heavy = true, armorBreak = true, fist = true },
                ["ram_air"] = new MoveDef { su = 6, ac = 5, rc = 12, box = B(0.95, 0.9, 1.7, 1.7), dmg = 3, poise = 28, kb = KB(6, -3), air = true, shield = true },
                ["ram_bash"] = new MoveDef { su = 3, ac = 4, rc = 12, box = B(1.05, 1.2, 1.5, 2.2), dmg = 2, poise = 40, kb = KB(11, 3), shove = true, shield = true },
                // Hydraulic Uplift
                ["ram_rise"] = new MoveDef { su = 6, ac = 12, rc = 16, box = B(0.95, 1.6, 2.0, 2.7), dmg = 2.4, poise = 12, kb = KB(2, 17), launcher = true, shield = true,
                    rise = 12.5, airRise = 0.75, multi = 6, sweep = true, riseBlast = new Blast { r = 2.2, dmg = 3.5, poise = 45 }, blastKind = "upliftBlast" },
                // Seismic Slam
                ["ram_slam"] = new MoveDef { su = 16, ac = 4, rc = 20, box = B(1.0, 0.8, 2.6, 1.6), dmg = 6, poise = 70, kb = KB(8, 9), heavy = true, armorBreak = true, shield = true,
                    quake = new QuakeDef { dmg = 4, poise = 45, speed = 12, ttl = 34, h = 1.1 } },

                // Fix
                ["fix_w1"] = new MoveDef { su = 4, ac = 3, rc = 10, box = B(0.8, 1.0, 1.15, 0.9), dmg = 1.5, poise = 12, kb = KB(2, 0), next = "fix_w2", wrench = true },
                ["fix_w2"] = new MoveDef { su = 4, ac = 3, rc = 10, box = B(0.8, 1.0, 1.15, 0.9), dmg = 1.5, poise = 12, kb = KB(2, 0), next = "fix_w3", wrench = true, offhand = true },
                ["fix_w3"] = new MoveDef { su = 7, ac = 4, rc = 15, box = B(0.95, 0.95, 1.5, 1.3), dmg = 3.4, poise = 40, kb = KB(9, 4), shove = true, heavy = true, wrench = true },
                ["fix_air"] = new MoveDef { su = 4, ac = 4, rc = 10, box = B(0.75, 0.85, 1.3, 1.1), dmg = 1.8, poise = 16, kb = KB(4, 2), air = true, wrench = true },
                // Torque Slam
                ["fix_slam"] = new MoveDef { su = 13, ac = 4, rc = 18, box = B(0.95, 0.9, 1.8, 1.4), dmg = 5, poise = 55, kb = KB(9, 6), heavy = true, armorBreak = true, wrench = true,
                    spark = new Blast { r = 2.8, dmg = 2, poise = 30, stun = 50 } },
                // Jack-Up
                ["fix_rise"] = new MoveDef { su = 5, ac = 11, rc = 13, box = B(0.55, 1.3, 1.35, 2.0), dmg = 1.5, poise = 18, kb = KB(1, 15), launcher = true, wrench = true,
                    rise = 16, airRise = 0.8, multi = 5, jack = true },
            };
            foreach (var kv in M) kv.Value.id = kv.Key;
            return M;
        }

        // Echo's Dash Slash
        public static class DASH_SLASH
        {
            public const double ticks = 12, keep = 0.93, recover = 9, hitRecover = 4;
            public static readonly double[] speed = { 19, 22, 26 }, dmg = { 2.8, 4.5, 7 }, poise = { 30, 50, 90 };
            public static readonly bool[] armorBreak = { false, true, true };
            public static class box { public const double fx = 0.9, w = 2.5, h = 1.6; }
        }

        // Ground pound
        public static class POUND
        {
            public const double windup = 5, hang = 1.2, maxHold = 110, speed = 34, maxDrop = 100, aimDown = -0.9, bounce = 13,
                recover = 12, hitRecover = 5, fallBonus = 0.6;
            public static readonly double[] charge = { 20, 44, 72 };
            public static class box { public const double w = 1.3, h = 1.6; }
            public static class drop { public const double dmg = 2.5, poise = 24; }
            public static readonly Blast[] land = {
                new Blast { r = 2.4, dmg = 4, poise = 45, kb = 9, up = 6 },
                new Blast { r = 3.0, dmg = 6, poise = 65, kb = 11, up = 7 },
                new Blast { r = 3.7, dmg = 8.5, poise = 90, kb = 13, up = 8, armorBreak = true },
                new Blast { r = 4.6, dmg = 12, poise = 130, kb = 16, up = 9, armorBreak = true },
            };
        }

        // Echo's staff deflect
        public static class DEFLECT
        {
            public const double window = 22, reach = 1.15, speed = 1.35, perfect = 1.6, stunReach = 0.8, heavyMult = 0.52;
            public static class dmg { public const double standard = 3, heavy = 6; }
        }

        public static class HUNTER
        {
            public const double snareCharges = 2, snareRecharge = 300, throwSpeed = 15, throwLift = 5, snareGravity = 32,
                armTicks = 10, life = 600, maxPlanted = 2, rootLight = 96, rootHeavyPoise = 38, leashTicks = 110, leashLen = 2.3, yankPoise = 45;
            public static class rifle
            {
                public const double raise = 10, focus = 60, cd = 48, range = 60, minDmg = 5, maxDmg = 18, crit = 1.6, critZone = 0.62, kb = 6, slow = 0.35, tag = 600;
                public static readonly double[] poise = { 18, 70 };
            }
        }

        // Echo's scarf modes
        public static class SCARF
        {
            public static readonly string[] modes = { "tether", "veil", "flare" };
            public const double switchCd = 10, veilFade = 20, veilRearm = 150, ambushWindow = 24, ambushDmg = 1.5, flareRange = 12, flareParry = 4,
                flarePerfect = 3, flareResolve = 1.6, flareTrickle = 2, challengeRange = 9, tauntTicks = 180;
        }

        // Velocity Break v0: power tier comes from what produced the speed
        public sealed class VBTier { public double dmg, poise, kb; public bool armorBreak; }
        public static class VB
        {
            public const double stopTicks = 2, activeFrom = 2, activeTo = 6, whiffRecovery = 10, driftAfter = 6;
            public static readonly VBTier[] tiers = {
                null,
                new VBTier { dmg = 2, poise = 30, kb = 6, armorBreak = false },
                new VBTier { dmg = 3.5, poise = 50, kb = 9, armorBreak = true },
                new VBTier { dmg = 6, poise = 90, kb = 13, armorBreak = true },
            };
        }

        public static class NOVA
        {
            public const double shotCd = 8, shotSpeed = 30, shotDmg = 1, shotRange = 20, charge1 = 30, charge2 = 72,
                bulwarkCd = 480, bulwarkRange = 4, barrierTicks = 120, barrierHalf = 1.6;
            public static class lance { public const double dmg = 3, poise = 30, speed = 36; }
            public static class rail { public const double dmg = 6, poise = 60, speed = 44; }
        }

        // ---- Nova's Marksman kit ----
        public sealed class LanceLevel { public double speed, dmg, poise, r, kb; public bool armorBreak, rail; public Blast splash; }
        public sealed class PrismLevel { public double dmg, poise, shards, bounces; public Blast splash; }
        public sealed class ShardDef { public double speed, dmg, poise, r, fan; public Blast splash; }
        public sealed class BurstLevel { public double pellets, fan, speed, dmg, poise, kb; public bool armorBreak; public Blast blast; }
        public sealed class BeamDef
        {
            public double at, ticks, pulse, dmg, poise, width, range, turn, slow, hover, armorEvery, kb;
            public double volleyEvery, arcEvery, prismBounces;
            public Blast arcBlast;
        }
        public static class MARKSMAN
        {
            public static readonly string[] attachments = { "lance", "volley", "arc", "prism" };
            public const double switchCd = 10, perfectWindow = 10, perfectMult = 1.5, life = 900;
            public static readonly double[] charge = { 34, 72, 115 };
            public static class round { public static readonly Blast splash = new Blast { r = 0.9, dmg = 0.35, poise = 4 }; }
            public static readonly LanceLevel[] lance = {
                null,
                new LanceLevel { speed = 40, dmg = 4.2, poise = 36, r = 0.22, kb = 5, splash = new Blast { r = 1.0, dmg = 1.6, poise = 12, rocket = true } },
                new LanceLevel { speed = 46, dmg = 7, poise = 60, r = 0.26, kb = 8, armorBreak = true, splash = new Blast { r = 1.4, dmg = 2.8, poise = 20, rocket = true } },
                new LanceLevel { speed = 52, dmg = 11.5, poise = 95, r = 0.32, kb = 11, armorBreak = true, rail = true, splash = new Blast { r = 1.9, dmg = 4.5, poise = 30, rocket = true } },
            };
            public static class volley
            {
                // darts and fan by level (index 1-3), index 4 = a Perfect Release
                public static readonly double[] darts = { 0, 3, 5, 7, 9 }, fan = { 0, 0.9, 1.1, 1.35, 1.6 };
                public const double speed = 20, dmg = 1.55, poise = 11, r = 0.14, seekDelay = 6, seekFor = 150, turn = 0.1, seekRange = 16, seekCone = 1.2;
                public static readonly Blast splash = new Blast { r = 0.8, dmg = 0.55, poise = 5 };
                public const bool rocket = true;
            }
            public static class arc
            {
                public const double speed = 17, lift = 0.7, gravity = 32, perfectRadius = 1.25;
                public static readonly Blast[] levels = {
                    null,
                    new Blast { r = 1.8, dmg = 4.2, poise = 42, rocket = true },
                    new Blast { r = 2.3, dmg = 6.3, poise = 60, armorBreak = true, rocket = true },
                    new Blast { r = 2.9, dmg = 9, poise = 90, armorBreak = true, rocket = true },
                };
            }
            public static class prism
            {
                public const double speed = 30, r = 0.2, perfectBounces = 1;
                public static readonly PrismLevel[] levels = {
                    null,
                    new PrismLevel { dmg = 2.2, poise = 17, shards = 2, bounces = 1, splash = new Blast { r = 1.2, dmg = 0.85, poise = 7, rocket = true } },
                    new PrismLevel { dmg = 3.1, poise = 22, shards = 3, bounces = 1, splash = new Blast { r = 1.4, dmg = 1.25, poise = 10, rocket = true } },
                    new PrismLevel { dmg = 4.2, poise = 29, shards = 5, bounces = 2, splash = new Blast { r = 1.7, dmg = 1.7, poise = 12, rocket = true } },
                };
                public static readonly ShardDef shard = new ShardDef { speed = 24, dmg = 1.7, poise = 10, r = 0.12, fan = 0.36, splash = new Blast { r = 0.7, dmg = 0.5, poise = 5 } };
            }
            public static class burst
            {
                public const double cd = 24, perfectWindow = 10, falloff = 14;
                public static readonly double[] charge = { 30, 62, 98 };
                // index 0 = the tap
                public static readonly BurstLevel[] levels = {
                    new BurstLevel { pellets = 5, fan = 0.63, speed = 28, dmg = 0.5, poise = 7, kb = 8 },
                    new BurstLevel { pellets = 7, fan = 0.7, speed = 29, dmg = 0.6, poise = 10, kb = 10 },
                    new BurstLevel { pellets = 9, fan = 0.8, speed = 30, dmg = 0.7, poise = 12, kb = 12, armorBreak = true },
                    new BurstLevel { pellets = 12, fan = 0.9, speed = 32, dmg = 0.8, poise = 14, kb = 14, armorBreak = true, blast = new Blast { r = 1.7, dmg = 2.5, poise = 30 } },
                };
            }
            public static class rocket
            {
                public static readonly double[] h = { 3.6, 8.4 }, air = { 1, 0.6, 0.35, 0.2 }, freeze = { 2, 3, 4 };
                public const double perfect = 10.5, reach = 1.1, close = 0.9, slack = 0.35, falloff = 0.45, side = 0.6, sideMax = 18;
                public static readonly Dictionary<string, double> attach = new Dictionary<string, double> { ["lance"] = 0.95, ["volley"] = 0.8, ["arc"] = 1, ["prism"] = 0.9 };
            }
            public static readonly BeamDef beam = new BeamDef
            {
                at = 170, ticks = 96, pulse = 6, dmg = 2.4, poise = 16, width = 0.34, range = 42, turn = 0.04, slow = 0.2, hover = 0.8, armorEvery = 24,
                volleyEvery = 10, arcEvery = 14, arcBlast = new Blast { r = 1.7, dmg = 2.4, poise = 22 }, prismBounces = 1,
            };
            public static class melee { public const double reach = 1.9, up = 1.6; }
            public static class boost { public const double fuel = 60, rise = 3.5, thrust = 70, refill = 2.5, minStart = 6, air = 1.1; }
            public static class focus { public const double max = 5, dmgPer = 0.06, perRound = 0.34, decay = 360, decayStep = 120; }
            public static class skate { public const double top = 8.2, accel = 60, coast = 30, carve = 95, tuck = 12, tuckMin = 3, backpedal = 1; }
        }

        // Nova's hard-light Aegis
        public static class AEGIS
        {
            public const double hp = 70, ticks = 300, cd = 660, radius = 1.55;
            public static class over { public const double perDmg = 2.2, max = 100, hold = 360, drain = 0.25, charge = 1.6, dmg = 1.4, cost = 34; }
            public static readonly Blast shatter = new Blast { r = 3.2, dmg = 3, poise = 45, kb = 10 };
            public static readonly Blast detonate = new Blast { r = 3.0, dmg = 4, poise = 45, kb = 10 };
            public const double detonateOver = 20;
        }

        // Nova's secondary weapons. Arrays are per level (0 = tap).
        public static readonly string[] SUBS = { "scatter", "grenade", "chain", "disc", "well" };
        public static class SUB
        {
            public const double switchCd = 10;
            public static class grenade
            {
                public const double cd = 26, lift = 0.55, gravity = 30, bounce = 0.5, roll = 0.82, r = 0.2;
                public static readonly double[] speed = { 14, 15, 16.5, 18 }, fuse = { 58, 64, 70, 76 };
                public static readonly Blast[] blast = {
                    new Blast { r = 1.8, dmg = 3, poise = 30 }, new Blast { r = 2.2, dmg = 4.5, poise = 45 },
                    new Blast { r = 2.7, dmg = 6.5, poise = 65, armorBreak = true }, new Blast { r = 3.2, dmg = 9, poise = 90, armorBreak = true },
                };
                public static class bomblets
                {
                    public const double n = 4, speed = 8, lift = 7, fuse = 28;
                    public static readonly Blast blast = new Blast { r = 1.3, dmg = 2.2, poise = 22 };
                }
            }
            public static class chain
            {
                public const double cd = 28, hop = 5, cone = 0.9;
                public static readonly double[] range = { 7, 8, 9, 10 }, jumps = { 3, 4, 5, 7 }, dmg = { 1.3, 1.9, 2.5, 3.4 }, poise = { 12, 18, 26, 38 }, stun = { 14, 20, 28, 40 };
            }
            public static class disc
            {
                public const double cd = 16, back = 26, tick = 8, maxBack = 150;
                public static readonly double[] speed = { 22, 24, 26, 28 }, @out = { 16, 18, 20, 22 }, hover = { 0, 0, 40, 60 }, r = { 0.4, 0.45, 0.55, 0.65 },
                    dmg = { 1.6, 2.2, 3, 4 }, poise = { 12, 16, 22, 30 };
            }
            public static class well
            {
                public const double cd = 30, speed = 13, heavy = 0.3, tick = 12, lift = 1.1;
                public static readonly double[] travel = { 24, 26, 28, 30 }, life = { 70, 90, 110, 130 }, r = { 3.2, 3.6, 4.2, 5 }, pull = { 6, 7, 8, 9.5 },
                    tickDmg = { 0.35, 0.45, 0.6, 0.8 };
                public static readonly Blast[] implode = {
                    new Blast { r = 2.2, dmg = 3, poise = 40 }, new Blast { r = 2.6, dmg = 4.5, poise = 55 },
                    new Blast { r = 3.1, dmg = 6.5, poise = 75, armorBreak = true }, new Blast { r = 3.8, dmg = 9, poise = 100, armorBreak = true },
                };
            }
        }

        // Nova's absorbing shield (Settings: Nova's defence = shield; replaces the dodge on the parry button). It blocks what
        // comes from in front; each blocked hit is absorbed as energy that makes his attacks hit harder: +bonus per 100
        // (full), and it overfills up to `max` (+150% damage at 150). Blocks wear down its stability, which grows back while it is lowered; broken, he reels. A block
        // in the first `perfect` ticks costs nothing and absorbs a set amount. Energy holds for `hold` ticks after the
        // last absorb, then drains; an unguarded hit spills `spill` of it.
        public static class NOVA_SHIELD
        {
            public const double perfect = 6, walk = 0.45, reach = 0.9, half = 1.0, gainPerDmg = 2.2, perfectGain = 14, bonus = 1.0, max = 150, poiseBonus = 0.3,
                hold = 360, drain = 0.12, spill = 0.5, stability = 100, cost = 1.1, regen = 30, delay = 45, recover = 40, brokenStun = 40;
            // a perfect parry or perfect block with Settings: Nova's perfect parry stuns: how long the attacker is stunned
            public const double stunLight = 80, stunHeavy = 45;
        }
        // Nova's dodge
        public static class DODGE { public const double ticks = 16, speed = 13, airSpeed = 11, keep = 0.86, iframes = 11, perfect = 7, cd = 28, slowTicks = 100, slowRange = 7, over = 20; }

        // ---- RAM (Vanguard) ----
        public sealed class CannonLevel { public double speed, dmg, poise, kb, r, ttl, pierce; public bool armorBreak; public Blast blast; }
        public static class RAM
        {
            public static class guard
            {
                public const double integrity = 100, regen = 22, delay = 60, perfect = 8, walk = 0.35, half = 1.35, reach = 1.0, brokenStun = 44, recover = 40,
                    kinetic = 1, perfectKinetic = 15, reflect = 1.4, minNy = -0.35;
            }
            public static class release
            {
                public const double min = 15, cone = 0.95;
                public static readonly double[] r = { 2.6, 5.4 }, dmg = { 4, 18 }, poise = { 40, 120 }, kb = { 9, 15 };
            }
            public static class cannon
            {
                public const double cd = 24;
                public static readonly double[] charge = { 36, 76, 118 };
                // index 0 = the slug (a tap), 1-3 = Breach Shots
                public static readonly CannonLevel[] levels = {
                    new CannonLevel { speed = 30, dmg = 2.6, poise = 26, kb = 8, r = 0.2, ttl = 34 },
                    new CannonLevel { speed = 32, dmg = 5, poise = 50, kb = 10, r = 0.26, ttl = 40, pierce = 1 },
                    new CannonLevel { speed = 34, dmg = 8, poise = 80, kb = 12, r = 0.32, ttl = 44, pierce = 2, armorBreak = true },
                    new CannonLevel { speed = 36, dmg = 12, poise = 110, kb = 15, r = 0.4, ttl = 48, pierce = 3, armorBreak = true, blast = new Blast { r = 2.2, dmg = 6, poise = 60 } },
                };
            }
            public static readonly BeamDef beam = new BeamDef { at = 170, ticks = 110, pulse = 6, dmg = 3.2, poise = 30, width = 0.55, range = 40, turn = 0.035, slow = 0.15, hover = 0.6, armorEvery = 18, kb = 7 };
            public static class rush
            {
                public static readonly double[] ticks = { 14, 18, 24, 30 }, speed = { 15, 17, 19.5, 22 }, catchDmg = { 2, 3, 4.5, 6 }, poise = { 30, 45, 70, 100 };
                public const double keep = 0.35, reach = 0.95, heavyFrom = 2;
                public static class end { public static readonly double[] kb = { 14, 5 }; }
                public static class splat { public static readonly double[] dmg = { 4, 5, 7, 10 }, poise = { 60, 80, 110, 160 }; public const double stun = 70; }
                public static class bonk { public static readonly double[] dmg = { 3, 4, 6, 9 }, poise = { 45, 65, 95, 140 }; }
            }
            public static class knock { public const double light = 1.6, heavy = 1.25, launchAt = 9, lift = 6; }
            public const double rushTaken = 0.6;
            public const double pound = 1.3;
            public static class wall { public const double cd = 720, ticks = 480, hp = 160, dist = 1.8, half = 1.75; }
            public static class link
            {
                public const double cd = 720, ticks = 480, range = 18, leapAt = 4.5, leapTicks = 34, share = 0.6, plate = 25, breakAt = 22;
                public static readonly Blast land = new Blast { r = 2.4, dmg = 3, poise = 45 };
            }
            public static class provoke
            {
                public const double cd = 600, ticks = 240, range = 11, brace = 0.6;
                public static class shove { public const double r = 2.6, kb = 13; }
            }
        }

        // ---- Fix (Mechanic) ----
        public sealed class GadgetDef
        {
            public double cost, hp, plateMax, poise, speed;
            public double[] life, r, heal, revive, plate, range, every, dmg, rate;
            public double rocketEvery, rocketSpeed;
            public Blast rocketBlast;
        }
        public static class FIX
        {
            public static class scrap { public const double max = 100, start = 60, regen = 2.5, perDmg = 0.35, kill = 6, killRange = 12; }
            public static class rivet
            {
                public const double cd = 14, n = 3, every = 3, speed = 34, dmg = 0.8, poise = 6, r = 0.11, ttl = 40;
                public static readonly double[] charge = { 32, 66, 102 };
                public static class hot
                {
                    public const double speed = 30, r = 0.16, fuse = 34, ttl = 60;
                    public static readonly double[] dmg = { 2, 3, 4.5 }, poise = { 16, 24, 36 };
                    public static readonly Blast[] blast = { new Blast { r = 1.5, dmg = 3, poise = 25 }, new Blast { r = 1.9, dmg = 5, poise = 40 },
                        new Blast { r = 2.4, dmg = 7.5, poise = 65, armorBreak = true } };
                }
            }
            public static class beam { public const double range = 9, keep = 12, heal = 24, self = 8, plate = 25, plateRate = 10, tune = 1.5, revive = 3, slow = 0.6; }
            public const double revive = 3, reviveHp = 0.6;
            public static readonly string[] gadgets = { "pylon", "sentry", "coil" };
            public static readonly Dictionary<string, GadgetDef> gadget = new Dictionary<string, GadgetDef>
            {
                ["pylon"] = new GadgetDef { cost = 40, life = new double[] { 900, 1080, 1260 }, hp = 60, r = new double[] { 4, 5, 6 }, heal = new double[] { 6, 9, 12 },
                    revive = new double[] { 0.35, 0.45, 0.6 }, plate = new double[] { 0, 0, 3 }, plateMax = 15 },
                ["sentry"] = new GadgetDef { cost = 50, life = new double[] { 1200, 1320, 1440 }, hp = 50, range = new double[] { 12, 13, 14 }, every = new double[] { 22, 16, 12 },
                    dmg = new double[] { 1.1, 1.3, 1.5 }, poise = 8, speed = 30, rocketEvery = 80, rocketSpeed = 18, rocketBlast = new Blast { r = 1.6, dmg = 4, poise = 40 } },
                ["coil"] = new GadgetDef { cost = 45, life = new double[] { 900, 1080, 1260 }, hp = 50, r = new double[] { 4.5, 5.5, 6.5 }, rate = new double[] { 1.5, 1.75, 2 } },
            };
            public static class pad { public const double life = 210, bounce = 21, w = 1.1, enemyBounce = 13; }
            public static readonly string[] powers = { "overclock", "plating", "medkit" };
            public static class power
            {
                public const double cost = 25, range = 14, speed = 14, lift = 6, gravity = 30, life = 900, grab = 0.9, ownerDelay = 30;
                public static class overclock { public const double ticks = 600, rate = 1.6; }
                public static class plating { public const double plate = 35; }
                public static class medkit { public const double heal = 40; }
            }
            public static readonly double[] poundHeal = { 8, 11, 14, 18 };
            public const double maxRate = 2.6;
        }
        // Plating never stacks past this
        public const double PLATE_MAX = 60;

        // ---- Ultimates ----
        public static class ULT
        {
            public const double max = 100, chord = 6, cast = 54, join = 18, boss = 0.5, mercy = 60;
            public static class gain { public const double dealt = 0.6, taken = 0.35, kill = 2, perfect = 6, blocked = 0.4, heal = 0.3; }
            public static class nova
            {
                public const string name = "Supernova";
                public const double rise = 1.6, gather = 24, beam = 110, pulse = 5, dmg = 5, width = 1.25, range = 40, turn = 0.06, end = 150;
                public static readonly Blast novaBlast = new Blast { r = 6, dmg = 12, poise = 120 };
            }
            public static class echo
            {
                public const string name = "Thousand Cuts";
                public const double range = 16, targets = 8, strikes = 20, every = 3, dmg = 3, start = 10, finisher = 14, end = 40;
                public static readonly Blast flourish = new Blast { r = 5, dmg = 10 };
            }
            public static class ram
            {
                public const string name = "Siege Breaker";
                public const double brace = 16, charge = 84, speed = 15, catchDmg = 3, every = 10, dmg = 2, end = 34, fortify = 40;
                public static readonly Blast slam = new Blast { r = 6, dmg = 14, poise = 140 };
            }
            public static class fix
            {
                public const string name = "Overhaul";
                public const double drop = 22, heal = 0.35, dmg = 4, end = 100, overclock = 720, plate = 40;
                public static readonly Blast pod = new Blast { r = 3.2, dmg = 6, poise = 60 };
                public static readonly double[] pulses = { 42, 60, 78 };
            }
            public static class team { public const double power = 1.3, dmg = 12, t = 50; }
            public static string NameOf(string ch) => ch == "nova" ? nova.name : ch == "echo" ? echo.name : ch == "ram" ? ram.name : fix.name;
            public static readonly Dictionary<string, string> teamNames = new Dictionary<string, string>
            {
                ["echo+nova"] = "Eclipse Protocol", ["nova+nova"] = "Binary Star", ["echo+echo"] = "Twin Phantom",
                ["nova+ram"] = "Starbreaker", ["echo+ram"] = "Shatterpoint", ["fix+nova"] = "Solar Overdrive", ["echo+fix"] = "Razorwire", ["fix+ram"] = "Heavy Metal",
                ["ram+ram"] = "Stampede", ["fix+fix"] = "Assembly Line",
            };
            public const string teamAll = "Full Resonance";
        }

        public static class ECHO
        {
            public const double cellsMax = 4, boltCd = 12, boltDmg = 1.5, boltSpeed = 34, tracerCd = 72, tracerHold = 24, tracerDmg = 0.8,
                lashRange = 6.5, lashCharges = 2, lashRecharge = 240, zipSpeed = 24, resolveHalf = 50;
        }

        public static readonly string[] PLAYER_COLORS = { "#5ac8fa", "#7ed957", "#f5f5f5", "#4dd0b8" };
        public static readonly string[] PLAYER_MARKS = { "▲", "◆", "●", "■" };
        public const string HOSTILE = "#ff2e7e";

        // Presentation names and tints
        public static readonly Dictionary<string, (string name, string tint)> SUB_LOOK = new Dictionary<string, (string, string)>
        {
            ["scatter"] = ("Scatter", "#ffcf7a"), ["grenade"] = ("Grenade", "#ff9a3d"), ["chain"] = ("Chain", "#ffe066"),
            ["disc"] = ("Disc", "#ffd36b"), ["well"] = ("Gravity Well", "#ffb547"),
        };
        public static readonly Dictionary<string, (string name, string tint)> FIX_LOOK = new Dictionary<string, (string, string)>
        {
            ["pylon"] = ("Patch Pylon", "#5cf2a6"), ["sentry"] = ("Sentry", "#ffcf5a"), ["coil"] = ("Amp Coil", "#6fe3ff"),
            ["overclock"] = ("Overclock", "#6fe3ff"), ["plating"] = ("Plating", "#a9c8ff"), ["medkit"] = ("Medkit", "#5cf2a6"),
            ["ultcell"] = ("Ult Cell", "#ffd23f"), ["fury"] = ("Fury", "#ff5a4a"),
        };
        public static readonly Dictionary<string, (string name, string tint)> ATTACH_LOOK = new Dictionary<string, (string, string)>
        {
            ["lance"] = ("Lance", "#ffb547"), ["volley"] = ("Volley", "#ffd889"), ["arc"] = ("Arc", "#ff9f40"), ["prism"] = ("Prism", "#fff0c8"),
        };

        // The settings in force (the prototype's SETTINGS)
        public static Settings SETTINGS = new Settings();

        // Power-ups found along the routes and in broken crates
        public static class POWERUPS
        {
            public static class ultcell { public const double ult = 40; }
            public static class fury { public const double ticks = 720, dmg = 1.5, kb = 1.3; }
            public const double dropLife = 1500;
        }

        public static readonly string[] IMPACT_STYLES = { "scifi", "comic", "eclipse", "shatter", "thunder", "sumi", "warp" };
        public static readonly Dictionary<string, string> IMPACT_ACCENT = new Dictionary<string, string>
        {
            ["scifi"] = "#38c8ff", ["comic"] = "#14101a", ["eclipse"] = "#ffb347", ["shatter"] = "#bfe8ff", ["thunder"] = "#8f7bff", ["sumi"] = "#c8202c", ["warp"] = "#a070ff",
        };

        public sealed class DifficultyDef { public double tokens, dmg; }
        public static readonly Dictionary<string, DifficultyDef> DIFFICULTY = new Dictionary<string, DifficultyDef>
        {
            ["easy"] = new DifficultyDef { tokens = -1, dmg = 0.7 },
            ["normal"] = new DifficultyDef { tokens = 0, dmg = 1 },
            ["hard"] = new DifficultyDef { tokens = 1, dmg = 1.3 },
        };
        // DIFFICULTY[SETTINGS.difficulty] || DIFFICULTY.normal
        public static DifficultyDef Diff => SETTINGS.difficulty != null && DIFFICULTY.TryGetValue(SETTINGS.difficulty, out var d) ? d : DIFFICULTY["normal"];
    }
}
