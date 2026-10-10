// Level data, the curved gameplay path, and collision helpers (port of level.js).
// The simulation is purely 2D (x along the path, y up). Rendering maps x onto a path that runs straight, bends
// 180° around the Storm Spire tower, then runs straight; the Version 12 routes wind through 3D space as chains of
// line and arc pieces. The simulation never knows: it stays 2D.
using System.Collections.Generic;

namespace NovaStriker.Sim
{
    // Anything moved by MoveBody: x is the centre, y the feet
    public class Body
    {
        public double x, y, w, h, vx, vy;
        public double dropT;          // dropping through one-way platforms while > 0
        public bool onGround, hitCeil;
        public double hitWall, wallDir;
    }

    public sealed class LevelBox
    {
        public int id;
        public double x0, x1, y0, y1;
        public char type;             // 's' solid, 'o' one-way, 'g' gate (solid when closed), 'd' breakable
        public string tag;
        public string loot;           // the power-up a breakable piece drops (or null)
        public double hp;
        public bool broken;
    }

    public sealed class DestructDef { public double hp, min; public string color, debris; }

    public sealed class Piece { public string kind; public double len, r, s; }
    public sealed class Route
    {
        public string id, name;
        public double x0, x1, sx, endX, phi;
        public double startX, startZ;
        public Piece[] pieces;
    }
    public sealed class Seg
    {
        public string route, kind;
        public double x0, x1, Px, Pz, phi, r, s, Cx, Cz;
    }
    // World position, tangent and normal (toward the camera) at a sim x
    public struct PathFrame { public double px, pz, tx, tz, nx, nz; }

    public sealed class Zone { public string id, name; public double x0, x1, spawnX, spawnY; }
    public struct Pt { public double x, y; public Pt(double x, double y) { this.x = x; this.y = y; } }
    public sealed class SpawnDef { public string type; public double x, y; public SpawnDef(string t, double x, double y) { type = t; this.x = x; this.y = y; } }
    public sealed class EncounterDef
    {
        public string id;
        public double trigger;
        public string[] banner;
        public SpawnDef[][] waves;
        public SpawnDef[] extra;
        public string[] gates;
        public double inside;
        public string boss;
        public double bossAtX, bossAtY;
        public string[][] waveBanners;
        public string[] cleared;
        public string route;
    }
    public struct LevelPickup { public double x, y; public string kind; public LevelPickup(double x, double y, string k) { this.x = x; this.y = y; kind = k; } }
    public struct Lift { public double x, y, top; public Lift(double x, double y, double t) { this.x = x; this.y = y; top = t; } }
    public struct RayHit { public double t, nx, ny; }
    public struct CastHit { public double t, x, y, nx, ny; public bool wall; public LevelBox box; }

    public static class Level
    {
        public const double ARC_START = 104;
        public const double ARC_R = 14;
        public static readonly double ARC_END = ARC_START + JMath.PI * ARC_R;
        public const double PATH2_X0 = 400;

        static Piece P(string kind, double len, double r = 0, double s = 0) => new Piece { kind = kind, len = len, r = r, s = s };

        public static readonly Route[] ROUTES = {
            new Route { id = "skyport", name = "Skyport route", x0 = -20, x1 = 400, endX = 304 },
            new Route { id = "foundry", name = "Helix Foundry", x0 = 400, x1 = 790, sx = 400, endX = 758, startX = -60, startZ = 150, phi = 0,
                pieces = new[] { P("line", 64), P("arc", 52, 26, -1), P("arc", 46, 22, 1), P("line", 22), P("arc", 78, 15, 1), P("line", 40), P("line", 62) } },
            new Route { id = "undercity", name = "Undercity Descent", x0 = 790, x1 = 1200, sx = 800, endX = 1177, startX = -200, startZ = -100, phi = 5 * JMath.PI / 4,
                pieces = new[] { P("line", 50), P("arc", 44, 28, -1), P("line", 30), P("arc", 56, 18, 1), P("arc", 30, 30, -1), P("line", 75), P("arc", 34, 22, 1), P("line", 62) } },
        };

        public static Route RouteAt(double x)
        {
            foreach (var r in ROUTES) if (x >= r.x0 && x < r.x1) return r;
            return ROUTES[0];
        }

        public static readonly List<Seg> SEGS = new List<Seg>();
        public static readonly Dictionary<string, List<Seg>> SEGS_OF = new Dictionary<string, List<Seg>>();

        // ---- Boxes: [x0, x1, y0, y1, type, tag, loot] ----
        struct Raw { public double x0, x1, y0, y1; public char type; public string tag, loot; }
        static Raw R(double x0, double x1, double y0, double y1, char type, string tag, string loot = null) =>
            new Raw { x0 = x0, x1 = x1, y0 = y0, y1 = y1, type = type, tag = tag, loot = loot };

        static List<Raw> BuildRaw()
        {
            var L = new List<Raw> {
                // Movement gym
                R(-12, -10, -6, 30, 's', "bound"),
                R(-10, 14, -6, 0, 's', "ground"),
                R(18, 32, -6, 0, 's', "ground"),
                R(28.5, 29.3, 2.1, 8.6, 's', "panel"),      // floating wall for wall-jump practice
                R(32, 40, -6, 6, 's', "ledge"),
                R(40, 60, -6, 0, 's', "ground"),
                R(44, 50, 1.0, 6.5, 's', "tunnel"),         // slide or crouch under
                R(57.5, 58.5, 0, 3, 's', "pillar"),         // turret pillar
                // Concourse Lock arena
                R(60, 97, -6, 0, 's', "ground"),
                // Gates reach far above any jump, rocket jump or booster climb, and cannot be wall-slid (energy)
                R(62, 62.8, 0, 30, 'g', "L"),
                R(96.2, 97, 0, 30, 'g', "R"),
                R(74, 84, 2.0, 2.4, 'o', "dais"),
                R(63.2, 66.6, 5.0, 5.4, 'o', "perch"),
                R(92.2, 96.0, 5.0, 5.4, 'o', "perch"),
                R(67.5, 68.3, 2.6, 5.0, 's', "column"),      // floats above head height: walk under, wall-jump off
                R(89.9, 90.7, 2.6, 5.0, 's', "column"),
                // Storm Spire climb (curved path)
                R(97, 113, -6, 0, 's', "ground"),
                R(113, 118, -6, 2.5, 's', "step"),
                R(118, 143, -6, 0, 's', "ground"),
                R(120, 124, 4.6, 5.2, 'o', "plat"),
                R(126, 130, 7.6, 8.2, 'o', "plat"),
                R(132, 136.5, 10.6, 11.2, 'o', "plat"),
                R(138.5, 142, 13.2, 13.8, 'o', "plat"),
                R(143, 162, -6, 15.6, 's', "top"),
                // Skyline Relay: rooftops and sky bridges beyond the Storm Spire
                R(162, 186, 12.6, 15.6, 's', "bridge"),
                R(190, 214, 5, 15.6, 's', "roof"),
                R(200, 203.5, 15.6, 17.4, 's', "cover"),
                R(219, 244, 5, 12.6, 's', "yard"),
                R(229, 229.8, 12.6, 14.8, 's', "wall"),
                R(238, 238.8, 12.6, 14.8, 's', "wall"),
                R(244, 248, 5, 15.6, 's', "step"),
                R(248, 258, 5, 18.6, 's', "ledge"),
                R(258, 258.8, 18.6, 50, 'g', "L2"),          // Relay Gate
                R(258, 298, 5, 18.6, 's', "relay"),
                R(264, 270, 21.6, 22.0, 'o', "plat"),
                R(276, 281, 23.6, 24.0, 'o', "plat"),
                R(287, 293, 21.6, 22.0, 'o', "plat"),
                R(297.2, 298, 18.6, 50, 'g', "R2"),
                R(298, 316, 5, 18.6, 's', "pad"),            // beacon pad: the Stormcaller's arena
                R(301.5, 305, 21.8, 22.2, 'o', "plat"),
                R(309, 312.5, 21.8, 22.2, 'o', "plat"),
                R(316, 318, -6, 50, 's', "bound"),

                // ---- Helix Foundry ----
                R(398, 400, -6, 40, 's', "bound"),
                R(400, 430, -6, 0, 's', "ground"), R(433, 464, -6, 0, 's', "ground"),
                R(446, 452, 0, 1.4, 's', "block"),
                R(464, 516, -6, 0, 's', "ground"),
                R(474, 480, 3, 3.4, 'o', "walk"), R(486, 492, 4.5, 4.9, 'o', "walk"), R(498, 504, 3, 3.4, 'o', "walk"),
                R(516, 584, -6, 0, 's', "ground"),
                R(584, 662, -6, 0, 's', "floor"),
            };
            for (int i = 0; i < 10; i++) L.Add(R(586 + i * 7, 592 + i * 7, 2.2 * (i + 1) - 0.4, 2.2 * (i + 1), 'o', "ring"));   // 1 m gaps
            L.AddRange(new[] {
                R(655, 662, 20, 24.2, 's', "landing"),
                R(662, 702, 22.2, 24.2, 's', "bridge"),
                R(702, 762, 18, 24.2, 's', "crucible"),
                R(708, 708.8, 24.2, 60, 'g', "F1"), R(755.2, 756, 24.2, 60, 'g', "F2"),
                R(716, 721, 27.4, 27.8, 'o', "perch"), R(740, 745, 27.4, 27.8, 'o', "perch"),
                R(762, 764, -6, 60, 's', "bound"),

                // ---- Undercity Descent ----
                R(798, 800, 0, 60, 's', "bound"),
                R(800, 824, 14, 20, 's', "roof"), R(827, 894, 14, 20, 's', "roof"),
                R(862, 868, 23, 23.4, 'o', "vent"),
                R(894, 902, 14, 20, 's', "roof"), R(905, 911, 12, 18, 's', "roof"), R(914, 924, 10, 16, 's', "roof"),
            });
            for (int i = 0; i < 8; i++) L.Add(R(924 + i * 7, 931 + i * 7, 16 - 1.4 * (i + 1) - 6, 16 - 1.4 * (i + 1), 's', "stair"));
            L.AddRange(new[] {
                R(980, 1010, -2, 4, 's', "ground"),
                R(1010, 1085, -6, 0, 's', "ground"),
                R(1032, 1040, 3.6, 4.0, 'o', "gantry"),
                R(1085, 1119, -6, 0, 's', "ground"),
                R(1119, 1179, -6, 0, 's', "plaza"),
                R(1124, 1124.8, 0, 40, 'g', "U1"), R(1174.2, 1175, 0, 40, 'g', "U2"),
                R(1133, 1138, 3.4, 3.8, 'o', "perch"), R(1160, 1165, 3.4, 3.8, 'o', "perch"),
                R(1179, 1181, -6, 60, 's', "bound"),

                // ---- Breakable pieces: type 'd', solid until broken; the loot is a power-up it drops ----
                // Skyport route
                R(206, 207, 15.6, 16.6, 'd', "crate", "ultcell"), R(233.5, 234.5, 12.6, 13.6, 'd', "crate"), R(272, 273.2, 18.6, 20.2, 'd', "barricade"),
                // Helix Foundry
                R(412, 413, 0, 1, 'd', "crate", "medkit"), R(413.1, 414.1, 0, 1, 'd', "crate"), R(412.5, 413.5, 1, 2, 'd', "crate", "fury"),
                R(422, 423.2, 0, 1.6, 'd', "barricade"), R(440, 441, 0, 1, 'd', "crate"), R(457, 458.2, 0, 1.6, 'd', "barricade"),
                R(482, 482.4, 0, 3.2, 'd', "glass"), R(494, 495, 0, 1, 'd', "crate", "plating"), R(508, 509.2, 0, 1.6, 'd', "barricade"),
                R(528, 529.2, 0, 2.4, 'd', "pillar"), R(546, 547, 0, 1, 'd', "crate", "overclock"), R(570, 571, 0, 1, 'd', "crate"), R(571.1, 572.1, 0, 1, 'd', "crate", "ultcell"),
                R(608.5, 609.5, 8.8, 9.8, 'd', "crate", "medkit"), R(629.5, 630.5, 15.4, 16.4, 'd', "crate", "fury"),
                R(676, 676.4, 24.2, 27, 'd', "glass"), R(690, 691.2, 24.2, 25.8, 'd', "barricade"), R(697, 698, 24.2, 25.2, 'd', "crate", "plating"),
                R(728, 729, 24.2, 27.4, 'd', "pillar"), R(734, 735, 24.2, 27.4, 'd', "pillar"), R(748, 749, 24.2, 25.2, 'd', "crate", "medkit"),
                // Undercity Descent
                R(812, 812.4, 20, 23, 'd', "glass"), R(818, 819, 20, 21, 'd', "crate", "medkit"), R(840, 841.2, 20, 21.6, 'd', "barricade"),
                R(878, 879.2, 20, 21.6, 'd', "barricade"), R(888, 889, 20, 21, 'd', "crate", "fury"), R(917, 918, 16, 17, 'd', "crate", "ultcell"),
                R(990, 991, 4, 5, 'd', "crate"), R(991.1, 992.1, 4, 5, 'd', "crate", "plating"),
                R(1024, 1025.2, 0, 1.8, 'd', "barricade"), R(1028, 1029, 0, 1, 'd', "crate"), R(1029.1, 1030.1, 0, 1, 'd', "crate", "medkit"),
                R(1046, 1047.2, 0, 1.8, 'd', "barricade"), R(1056, 1056.4, 0, 3.5, 'd', "glass"), R(1066, 1067.2, 0, 1.8, 'd', "barricade"),
                R(1072, 1073, 0, 1, 'd', "crate", "overclock"), R(1100, 1101.2, 0, 3, 'd', "pillar"), R(1110, 1111, 0, 1, 'd', "crate", "ultcell"),
                R(1142, 1143, 0, 3.4, 'd', "pillar"), R(1152, 1153, 0, 3.4, 'd', "pillar"), R(1168, 1169, 0, 1, 'd', "crate", "medkit"),
            });
            return L;
        }

        // Breakable pieces: hit points, the debris colour, and what can break them (glass shatters at anything; a
        // pillar shrugs off small arms fire: only blows of at least `min` damage count)
        public static readonly Dictionary<string, DestructDef> DESTRUCT = new Dictionary<string, DestructDef> {
            ["crate"] = new DestructDef { hp = 12, min = 0, color = "#c98b4a", debris = "wood" },
            ["barricade"] = new DestructDef { hp = 40, min = 0, color = "#8a97a8", debris = "metal" },
            ["glass"] = new DestructDef { hp = 1, min = 0, color = "#bfe8ff", debris = "glass" },
            ["pillar"] = new DestructDef { hp = 70, min = 3, color = "#b9c2cc", debris = "stone" },
        };

        public static readonly List<LevelBox> BOXES = new List<LevelBox>();
        public static int Version;   // (bumped whenever BOXES changes, so the view can rebuild what it made from them)
        public static readonly double LEVEL_X0, LEVEL_X1;
        public static readonly Dictionary<string, bool> GATES = new Dictionary<string, bool> {
            ["L"] = false, ["R"] = false, ["L2"] = false, ["R2"] = false, ["F1"] = false, ["F2"] = false, ["U1"] = false, ["U2"] = false,
        };

        // Put every breakable piece back (a reset to a checkpoint, a zone load)
        public static void RestoreBoxes()
        {
            foreach (var b in BOXES) if (b.type == 'd') { b.broken = false; b.hp = DESTRUCT[b.tag].hp; }
        }

        public static readonly Zone[] ZONES = {
            new Zone { id = "gym", name = "Movement Gym", x0 = -10, x1 = 60, spawnX = 0, spawnY = 0 },
            new Zone { id = "arena", name = "Concourse Lock", x0 = 60, x1 = 97, spawnX = 58.5, spawnY = 0 },
            new Zone { id = "tower", name = "Storm Spire Climb", x0 = 97, x1 = 162, spawnX = 100, spawnY = 0 },
            new Zone { id = "skyline", name = "Skyline Relay", x0 = 162, x1 = 318, spawnX = 166, spawnY = 15.6 },
            new Zone { id = "foundry", name = "Helix Foundry", x0 = 398, x1 = 764, spawnX = 402, spawnY = 0 },
            new Zone { id = "undercity", name = "Undercity Descent", x0 = 798, x1 = 1181, spawnX = 802, spawnY = 20 },
        };
        public static Zone ZoneAt(double x)
        {
            foreach (var z in ZONES) if (x >= z.x0 && x < z.x1) return z;
            return ZONES[0];
        }

        public static readonly Pt[] CHECKPOINTS = {
            new Pt(0, 0), new Pt(58.5, 0), new Pt(100, 0), new Pt(152, 15.6),
            new Pt(166, 15.6), new Pt(193, 15.6), new Pt(222, 12.6), new Pt(252, 18.6), new Pt(302, 18.6),
            // Helix Foundry, Undercity Descent
            new Pt(402, 0), new Pt(466, 0), new Pt(586, 0), new Pt(664, 24.2), new Pt(704, 24.2),
            new Pt(802, 20), new Pt(852, 20), new Pt(926, 14.6), new Pt(1012, 0), new Pt(1121, 0),
        };

        static SpawnDef S(string t, double x, double y) => new SpawnDef(t, x, y);

        // Encounters. Each starts when a player passes `trigger`; the next wave comes when at most one enemy of the
        // current wave is left, and the last wave must be cleared. `extra` joins the first wave with three or more
        // players. Gated encounters seal their gates until cleared.
        public static readonly EncounterDef[] ENCOUNTERS = {
            new EncounterDef { id = "patrol", trigger = 191, banner = new[] { "Skyline Relay", "Drones inbound." },
                waves = new[] { new[] { S("drone", 204, 19.5), S("drone", 209, 20.5), S("swarmer", 207, 15.6), S("swarmer", 211, 15.6) } },
                extra = new[] { S("drone", 212, 19) } },
            new EncounterDef { id = "yard", trigger = 221, banner = new[] { "Mortar Yard", "Watch for the landing markers." },
                waves = new[] { new[] { S("mortar", 251, 18.6), S("mortar", 255.5, 18.6), S("charger", 240, 12.6), S("swarmer", 233, 12.6) } },
                extra = new[] { S("swarmer", 236, 12.6) } },
            new EncounterDef { id = "relay", trigger = 261, gates = new[] { "L2", "R2" }, inside = 260, banner = new[] { "Relay Gate", "Gates sealed. Take the relay." },
                waves = new[] {
                    new[] { S("shield", 286, 18.6), S("charger", 292, 18.6), S("drone", 272, 23), S("drone", 284, 25), S("sniper", 278.5, 24) },
                    new[] { S("brute", 290, 18.6), S("charger", 266, 18.6), S("drone", 270, 24), S("drone", 288, 24.5), S("mortar", 294, 18.6),
                        S("swarmer", 280, 18.6), S("swarmer", 284, 18.6) },
                },
                waveBanners = new[] { null, new[] { "Final wave", "The Brute holds the relay." } },
                extra = new[] { S("swarmer", 276, 18.6) },
                cleared = new[] { "Relay secured", "Gates open. The beacon is ahead." } },
            // The level boss: the gunship guarding the beacon. Its gate seals behind the team.
            new EncounterDef { id = "beacon", trigger = 300.5, gates = new[] { "R2" }, inside = 299.5, boss = "stormcaller", bossAtX = 308, bossAtY = 32,
                banner = new[] { "Stormcaller", "It keeps the relay beacon." },
                cleared = new[] { "Beacon secured", "The Stormcaller is down." } },

            // ---- Helix Foundry ----
            new EncounterDef { id = "f-approach", trigger = 406, banner = new[] { "Helix Foundry", "Break through the approach." },
                waves = new[] { new[] { S("swarmer", 420, 0), S("swarmer", 426, 0), S("shield", 450, 1.4), S("sniper", 460, 0) } }, extra = new[] { S("swarmer", 438, 0) } },
            new EncounterDef { id = "f-trench", trigger = 470, banner = new[] { "The Trench", "It sweeps round you. Watch the walkways." },
                waves = new[] { new[] { S("drone", 490, 8), S("drone", 501, 7), S("swarmer", 489, 4.9), S("charger", 512, 0) },
                    new[] { S("shield", 532, 0), S("mortar", 556, 0), S("swarmer", 522, 0), S("swarmer", 540, 0) } },
                extra = new[] { S("drone", 496, 9) } },
            new EncounterDef { id = "f-helix", trigger = 590, banner = new[] { "The Helix", "Climb the core." },
                waves = new[] { new[] { S("swarmer", 602, 6.6), S("sniper", 616, 11), S("drone", 624, 16), S("swarmer", 630, 15.4) },
                    new[] { S("shield", 658, 24.2), S("drone", 645, 23), S("drone", 652, 25) } },
                extra = new[] { S("sniper", 637, 17.6) } },
            new EncounterDef { id = "f-crucible", trigger = 710, gates = new[] { "F1", "F2" }, inside = 709.5, banner = new[] { "The Crucible", "Sealed in. Hold the floor." },
                waves = new[] { new[] { S("shield", 740, 24.2), S("charger", 748, 24.2), S("sniper", 718, 27.8), S("drone", 730, 30) },
                    new[] { S("brute", 745, 24.2), S("charger", 720, 24.2), S("swarmer", 735, 24.2), S("swarmer", 750, 24.2), S("mortar", 752, 24.2), S("drone", 742, 31) } },
                waveBanners = new[] { null, new[] { "Final wave", "The foundry Brute." } }, extra = new[] { S("swarmer", 726, 24.2) },
                cleared = new[] { "Foundry secured", "The Helix Foundry is yours." } },

            // ---- Undercity Descent ----
            new EncounterDef { id = "u-roofs", trigger = 806, banner = new[] { "Undercity Descent", "Down through the city." },
                waves = new[] { new[] { S("drone", 818, 25), S("swarmer", 835, 20), S("sniper", 846, 20), S("swarmer", 830, 20) } }, extra = new[] { S("drone", 840, 26) } },
            new EncounterDef { id = "u-vents", trigger = 856, banner = new[] { "The Vents", "The rooftops swing round you." },
                waves = new[] { new[] { S("charger", 885, 20), S("shield", 890, 20), S("drone", 870, 27) },
                    new[] { S("swarmer", 880, 20), S("swarmer", 884, 20), S("mortar", 919, 16), S("sniper", 908, 18) } } },
            new EncounterDef { id = "u-stair", trigger = 930, banner = new[] { "Cooling Tower", "Down the stair." },
                waves = new[] { new[] { S("drone", 950, 17), S("sniper", 968, 6.2), S("swarmer", 976, 4.8), S("shield", 962, 7.6) } }, extra = new[] { S("drone", 962, 14) } },
            new EncounterDef { id = "u-transit", trigger = 1016, banner = new[] { "Transit Line", "Through the barricades." },
                waves = new[] { new[] { S("shield", 1040, 0), S("swarmer", 1030, 0), S("swarmer", 1034, 0), S("charger", 1060, 0), S("drone", 1050, 7) },
                    new[] { S("brute", 1078, 0), S("sniper", 1036, 4.0), S("mortar", 1082, 0), S("swarmer", 1070, 0) } },
                extra = new[] { S("drone", 1044, 8) } },
            new EncounterDef { id = "u-plaza", trigger = 1126, gates = new[] { "U1", "U2" }, inside = 1125.5, banner = new[] { "The Plaza", "Last stand at the bottom." },
                waves = new[] { new[] { S("shield", 1150, 0), S("charger", 1160, 0), S("drone", 1140, 7), S("drone", 1156, 8), S("sniper", 1162, 3.8) },
                    new[] { S("brute", 1145, 0), S("brute", 1166, 0), S("swarmer", 1135, 0), S("swarmer", 1158, 0), S("mortar", 1171, 0) } },
                waveBanners = new[] { null, new[] { "Final wave", "Two Brutes." } }, extra = new[] { S("swarmer", 1150, 0) },
                cleared = new[] { "Undercity secured", "You made it to the bottom." } },
        };

        // Power-ups waiting along the routes (more drop from broken crates)
        public static readonly LevelPickup[] LEVEL_PICKUPS = {
            new LevelPickup(116, 2.5, "medkit"), new LevelPickup(150, 15.6, "overclock"), new LevelPickup(196, 15.6, "plating"), new LevelPickup(254, 18.6, "medkit"),
            new LevelPickup(403, 0, "overclock"), new LevelPickup(468, 0, "plating"), new LevelPickup(489, 4.9, "ultcell"), new LevelPickup(586, 0, "medkit"),
            new LevelPickup(617.5, 11, "overclock"), new LevelPickup(659, 24.2, "medkit"),
            new LevelPickup(700, 24.2, "fury"), new LevelPickup(705, 24.2, "plating"),
            new LevelPickup(804, 20, "overclock"), new LevelPickup(865, 23.4, "plating"), new LevelPickup(927, 14.6, "medkit"), new LevelPickup(1013, 0, "fury"),
            new LevelPickup(1036, 4.0, "ultcell"), new LevelPickup(1090, 0, "medkit"), new LevelPickup(1122, 0, "plating"),
        };

        // Lift pads on the floor (Helix Foundry): anyone who comes down on one is thrown straight up to `top`
        public static readonly Lift[] LIFTS = { new Lift(593, 0, 8.8), new Lift(607, 0, 13.2), new Lift(621, 0, 17.6), new Lift(635, 0, 22), new Lift(649, 0, 24.2) };

        // How low counts as falling out (a fall below this brings you back): per stretch of the routes
        static readonly double[][] KILL = { new double[] { 662, 764, 14 }, new double[] { 800, 924, 6 } };
        public static double KillYAt(double x)
        {
            foreach (var k in KILL) if (x >= k[0] && x < k[1]) return k[2];
            return KILL_Y;
        }
        public const double ROUTE_END_X = 304;
        public const double KILL_Y = -10;
        public const double ARENA_TRIGGER_X = 64;
        public const double TOWER_TRIGGER_X = 108;

        public static readonly double TOWER_CENTER_X = ARC_START, TOWER_CENTER_Z = -ARC_R;

        static Level()
        {
            // The winding Version 12 routes, as chains of pieces
            foreach (var R in ROUTES)
            {
                if (R.pieces == null) continue;
                double x = R.sx, Px = R.startX, Pz = R.startZ, phi = R.phi;
                var list = new List<Seg>();
                SEGS_OF[R.id] = list;
                foreach (var pc in R.pieces)
                {
                    var seg = new Seg { route = R.id, kind = pc.kind, x0 = x, x1 = x + pc.len, Px = Px, Pz = Pz, phi = phi, r = pc.r, s = pc.s };
                    if (pc.kind == "arc")
                    {
                        double n0x = JMath.Sin(phi), n0z = JMath.Cos(phi);
                        seg.Cx = Px - pc.s * n0x * pc.r; seg.Cz = Pz - pc.s * n0z * pc.r;
                    }
                    SEGS.Add(seg); list.Add(seg);
                    var f = FrameOn(seg, pc.len);
                    Px = f.px; Pz = f.pz;
                    phi = pc.kind == "arc" ? phi + pc.s * pc.len / pc.r : phi;
                    x += pc.len;
                }
            }
            var raw = BuildRaw();
            double lx0 = double.PositiveInfinity, lx1 = double.NegativeInfinity;
            for (int id = 0; id < raw.Count; id++)
            {
                var r = raw[id];
                BOXES.Add(new LevelBox { id = id, x0 = r.x0, x1 = r.x1, y0 = r.y0, y1 = r.y1, type = r.type, tag = r.tag, loot = r.loot,
                    hp = r.type == 'd' ? DESTRUCT[r.tag].hp : 0, broken = false });
                lx0 = JMath.Min(lx0, r.x0); lx1 = JMath.Max(lx1, r.x1);
            }
            LEVEL_X0 = lx0; LEVEL_X1 = lx1;
            // Which route an encounter belongs to (its trigger only counts for players on that route)
            foreach (var E in ENCOUNTERS) E.route = RouteAt(E.trigger).id;
        }

        static PathFrame FrameOn(Seg g, double u)
        {
            if (g.kind == "line")
            {
                double tx = JMath.Cos(g.phi), tz = -JMath.Sin(g.phi);
                return new PathFrame { px = g.Px + tx * u, pz = g.Pz + tz * u, tx = tx, tz = tz, nx = -tz, nz = tx };
            }
            double phi = g.phi + g.s * u / g.r, nx = JMath.Sin(phi), nz = JMath.Cos(phi);
            return new PathFrame { px = g.Cx + g.s * nx * g.r, pz = g.Cz + g.s * nz * g.r, tx = JMath.Cos(phi), tz = -JMath.Sin(phi), nx = nx, nz = nz };
        }

        // Does the stretch x0..x1 curve (its geometry is drawn in short pieces that follow the bend)?
        public static bool CurvedSpan(double x0, double x1)
        {
            if (x1 > ARC_START && x0 < ARC_END && x0 < PATH2_X0) return true;
            foreach (var g in SEGS) if (g.kind == "arc" && x1 > g.x0 && x0 < g.x1) return true;
            return false;
        }

        // World position/tangent/normal for sim x. The normal points toward the camera.
        public static PathFrame Frame(double x)
        {
            if (x >= PATH2_X0)
            {
                var segs = SEGS_OF[RouteAt(x).id];
                Seg g = null;
                foreach (var q in segs) if (x < q.x1) { g = q; break; }
                if (g == null) g = segs[segs.Count - 1];
                if (x < segs[0].x0) g = segs[0];
                return FrameOn(g, x - g.x0);   // (past either end it runs straight on along the end piece)
            }
            if (x <= ARC_START) return new PathFrame { px = x, pz = 0, tx = 1, tz = 0, nx = 0, nz = 1 };
            if (x < ARC_END)
            {
                double th = (x - ARC_START) / ARC_R, s = JMath.Sin(th), c = JMath.Cos(th);
                return new PathFrame { px = ARC_START + ARC_R * s, pz = -ARC_R + ARC_R * c, tx = c, tz = -s, nx = s, nz = c };
            }
            double d = x - ARC_END;
            return new PathFrame { px = ARC_START - d, pz = -2 * ARC_R, tx = -1, tz = 0, nx = 0, nz = -1 };
        }

        public static bool IsSolid(LevelBox b) =>
            b.type == 's' || (b.type == 'g' && GATES[b.tag]) || (b.type == 'd' && !b.broken);

        // The breakable piece at a point, if any
        public static LevelBox BreakableAt(double x, double y, double pad = 0)
        {
            foreach (var b in BOXES)
                if (b.type == 'd' && !b.broken && x > b.x0 - pad && x < b.x1 + pad && y > b.y0 - pad && y < b.y1 + pad) return b;
            return null;
        }

        static bool Overlaps(double x0, double x1, double y0, double y1, LevelBox b) =>
            x0 < b.x1 && x1 > b.x0 && y0 < b.y1 && y1 > b.y0;

        // Sets onGround, wallDir, hitWall, hitCeil
        public static void MoveBody(Body body, double dt)
        {
            double hw = body.w / 2;
            body.hitWall = 0; body.hitCeil = false;
            // Horizontal
            body.x += body.vx * dt;
            foreach (var b in BOXES)
            {
                if (!IsSolid(b)) continue;
                if (Overlaps(body.x - hw, body.x + hw, body.y, body.y + body.h, b))
                {
                    if (body.x < (b.x0 + b.x1) / 2) { body.x = b.x0 - hw - 1e-4; body.hitWall = 1; }
                    else { body.x = b.x1 + hw + 1e-4; body.hitWall = -1; }
                    body.vx = 0;
                }
            }
            // Vertical
            double prevY = body.y;
            body.y += body.vy * dt;
            body.onGround = false;
            foreach (var b in BOXES)
            {
                bool solid = IsSolid(b);
                if (!solid && b.type != 'o') continue;
                if (!Overlaps(body.x - hw, body.x + hw, body.y, body.y + body.h, b)) continue;
                if (b.type == 'o')
                {
                    if (body.vy <= 0 && prevY >= b.y1 - 0.02 && !(body.dropT > 0)) { body.y = b.y1; body.vy = 0; body.onGround = true; }
                    continue;
                }
                if (body.vy <= 0 && prevY >= b.y1 - 0.08) { body.y = b.y1; body.vy = 0; body.onGround = true; }
                else if (body.vy > 0) { body.y = b.y0 - body.h - 1e-4; body.vy = 0; body.hitCeil = true; }
                else
                {
                    // Embedded (e.g. stood up under a ceiling): push toward the nearer vertical side
                    double up = b.y1 - body.y, down = body.y + body.h - b.y0;
                    if (up < down) { body.y = b.y1; body.onGround = true; } else { body.y = b.y0 - body.h; }
                    body.vy = 0;
                }
            }
            // Wall contact probe (for wall cling while airborne). Gates are energy barriers: nothing to cling to.
            body.wallDir = 0;
            if (!body.onGround)
            {
                foreach (var b in BOXES)
                {
                    if (!IsSolid(b) || b.type == 'g') continue;
                    double ya = body.y + 0.3, yb = body.y + body.h - 0.2;
                    if (ya < b.y1 && yb > b.y0)
                    {
                        if (JMath.Abs(body.x + hw - b.x0) < 0.06) body.wallDir = 1;
                        else if (JMath.Abs(body.x - hw - b.x1) < 0.06) body.wallDir = -1;
                    }
                }
            }
        }

        // Can a body of height h stand at (x, y)?
        public static bool HasHeadroom(double x, double y, double w, double h)
        {
            double hw = w / 2;
            foreach (var b in BOXES)
            {
                if (!IsSolid(b)) continue;
                if (Overlaps(x - hw, x + hw, y + 0.05, y + h, b)) return false;
            }
            return true;
        }

        public static double GroundBelow(double x, double y)
        {
            double best = double.NegativeInfinity;
            foreach (var b in BOXES)
            {
                if (!(IsSolid(b) || b.type == 'o')) continue;
                if (x > b.x0 && x < b.x1 && b.y1 <= y + 0.01 && b.y1 > best) best = b.y1;
            }
            return best;
        }

        // Segment-vs-solid test for line of sight (slab method)
        public static bool SegmentBlocked(double ax, double ay, double bx, double by)
        {
            double dx = bx - ax, dy = by - ay;
            foreach (var b in BOXES)
            {
                if (!IsSolid(b)) continue;
                double t0 = 0, t1 = 1;
                if (Check(-dx, ax - b.x0, ref t0, ref t1) && Check(dx, b.x1 - ax, ref t0, ref t1) &&
                    Check(-dy, ay - b.y0, ref t0, ref t1) && Check(dy, b.y1 - ay, ref t0, ref t1))
                {
                    if (t0 <= t1) return true;
                }
            }
            return false;
        }
        static bool Check(double p, double q, ref double t0, ref double t1)
        {
            if (JMath.Abs(p) < 1e-9) return q >= 0;
            double r = q / p;
            if (p < 0) { if (r > t1) return false; if (r > t0) t0 = r; }
            else { if (r < t0) return false; if (r < t1) t1 = r; }
            return true;
        }

        // Slab test of a ray from (x, y) along (dx, dy) against a box: the entry distance and the face normal hit,
        // or null. A ray that starts inside the box enters at 0.
        public static RayHit? RayBoxT(double x, double y, double dx, double dy, double x0, double y0, double x1, double y1)
        {
            double tin = double.NegativeInfinity, tout = double.PositiveInfinity, nx = 0, ny = 0;
            if (JMath.Abs(dx) < 1e-9) { if (x <= x0 || x >= x1) return null; }
            else
            {
                double ta = (x0 - x) / dx, tb = (x1 - x) / dx, tn = JMath.Min(ta, tb);
                if (tn > tin) { tin = tn; nx = dx > 0 ? -1 : 1; ny = 0; }
                tout = JMath.Min(tout, JMath.Max(ta, tb));
            }
            if (JMath.Abs(dy) < 1e-9) { if (y <= y0 || y >= y1) return null; }
            else
            {
                double ta = (y0 - y) / dy, tb = (y1 - y) / dy, tn = JMath.Min(ta, tb);
                if (tn > tin) { tin = tn; nx = 0; ny = dy > 0 ? -1 : 1; }
                tout = JMath.Min(tout, JMath.Max(ta, tb));
            }
            if (tin > tout || tout < 0) return null;
            return new RayHit { t = JMath.Max(0, tin), nx = nx, ny = ny };
        }

        // The first solid surface along a ray (unit direction), up to `range` m: where it is, how far, and the
        // surface normal there (wall: false when nothing is hit within range)
        public static CastHit RayCast(double x, double y, double dx, double dy, double range)
        {
            double best = range, nx = 0, ny = 0; LevelBox box = null;
            foreach (var b in BOXES)
            {
                if (!IsSolid(b)) continue;
                var h = RayBoxT(x, y, dx, dy, b.x0, b.y0, b.x1, b.y1);
                if (h != null && h.Value.t < best) { best = h.Value.t; nx = h.Value.nx; ny = h.Value.ny; box = b; }
            }
            return new CastHit { t = best, x = x + dx * best, y = y + dy * best, nx = nx, ny = ny, wall = best < range, box = box };
        }

        public static bool PointInSolid(double x, double y)
        {
            foreach (var b in BOXES) if (IsSolid(b) && x > b.x0 && x < b.x1 && y > b.y0 && y < b.y1) return true;
            return false;
        }
    }
}
