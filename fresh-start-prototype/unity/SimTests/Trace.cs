// Runs a scenario in the C# simulation and prints the world's state every tick, in the same form as trace.mjs.
// Usage: dotnet run -- <scenario> <ticks> <seed>
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using NovaStriker.Sim;

namespace NovaStriker.SimTests
{
    static class Trace
    {
        static readonly string[] BT = Buttons.Names;
        static readonly int[] PCT = { 22, 10, 25, 30, 12, 6, 3, 5, 4, 2 };

        // A scripted person: xorshift32 picks a stick direction and buttons, changing every few ticks (as trace.mjs)
        sealed class Person
        {
            uint s; int hold; double mx, my; readonly bool[] want = new bool[10]; readonly Buttons prev = new Buttons();
            public Person(uint seed) { s = unchecked(seed * 2654435761u); if (s == 0) s = 1; }
            uint Next() { s ^= s << 13; s ^= s >> 17; s ^= s << 5; return s; }
            public Cmd Step()
            {
                if (hold <= 0)
                {
                    hold = 4 + (int)(Next() % 30);
                    int d = (int)(Next() % 10);
                    mx = new double[] { 0, 1, 1, 1, 0, -1, -1, -1, 1, 0 }[d]; my = new double[] { 0, 0, 1, -1, 1, 0, 1, -1, 0, -1 }[d];
                    for (int i = 0; i < 10; i++) want[i] = Next() % 100 < PCT[i];
                }
                hold--;
                var c = new Cmd { mx = mx, my = my };
                for (int i = 0; i < 10; i++)
                {
                    c.held[i] = want[i] && (i != 3 || hold % 40 != 0);
                    c.pressed[i] = c.held[i] && !prev[i]; c.released[i] = !c.held[i] && prev[i];
                }
                prev.CopyFrom(c.held);
                return c;
            }
        }

        static string N(double v) => double.IsPositiveInfinity(v) ? "\"inf\"" : v.ToString("R", CultureInfo.InvariantCulture);
        static string S(string v) => v == null ? "null" : "\"" + v + "\"";

        public static int Run(string[] args)
        {
            string scenario = args.Length > 0 ? args[0] : "gym-nova";
            int ticks = args.Length > 1 ? int.Parse(args[1]) : 600;
            uint seed = args.Length > 2 ? uint.Parse(args[2]) : 1;
            JRandom.Seed(seed);
            var parts = scenario.Split('-');
            string zone = parts[0], who = parts[1]; int nb = parts.Length > 2 ? int.Parse(parts[2]) : 0;
            var S_ = Cfg.SETTINGS;
            S_.novaKit = "marksman"; S_.echoKit = "hunter"; S_.lockMode = "auto"; S_.difficulty = "normal";
            S_.aiSkill = "elite"; S_.barks = true; S_.dashCharge = true; S_.lockOn = true;
            string variant = parts.Length > 3 ? parts[3] : "";
            if (variant == "v") { S_.novaKit = "sentinel"; S_.echoKit = "pursuit"; S_.echoRanged = "B"; S_.echoBelt = "lb"; S_.lockMode = "manual";
                S_.difficulty = "hard"; S_.aiSkill = "rookie"; S_.dashIframes = true; S_.vbStop = "keep30"; S_.dashCharge = false; }
            if (variant == "w") { S_.echoBelt = "lb"; S_.echoRanged = "A"; S_.difficulty = "hard"; S_.aiSkill = "veteran"; S_.echoSpinStun = 2.5; S_.aimAssist = false; }
            var w = new World();
            if (zone != "warden" && zone != "beacon") w.Teleport(zone);
            var me = w.AddPlayer("kbm", who);
            var bots = new Bots(); var pad = new Person(seed);
            if (zone == "warden" || zone == "beacon") w.BossRush(zone == "warden" ? "warden" : "beacon");
            var sb = new StringBuilder();
            var stdout = Console.OpenStandardOutput();
            for (int t = 0; t < ticks; t++)
            {
                var cmds = new Dictionary<int, Cmd> { [me.slot] = pad.Step() };
                bots.Sync(w, nb); bots.Commands(w, cmds);
                w.Step(cmds);
                sb.Clear();
                sb.Append("{\"t\":").Append(N(w.tick)).Append(",\"seq\":").Append(w.instanceSeq).Append(",\"cp\":").Append(w.checkpoint).Append(",\"P\":[");
                for (int i = 0; i < w.players.Count; i++)
                {
                    var p = w.players[i]; if (i > 0) sb.Append(',');
                    sb.Append('[').Append(p.slot).Append(',').Append(S(p.@char)).Append(',').Append(S(p.state)).Append(',').Append(N(p.st)).Append(',')
                      .Append(N(p.x)).Append(',').Append(N(p.y)).Append(',').Append(N(p.vx)).Append(',').Append(N(p.vy)).Append(',').Append(N(p.hp)).Append(',')
                      .Append(N(p.facing)).Append(',').Append(N(p.chargeT)).Append(',').Append(N(p.ult)).Append(',').Append(N(p.plate)).Append(',')
                      .Append(N(p.aimX)).Append(',').Append(N(p.aimY)).Append(']');
                }
                sb.Append("],\"E\":[");
                for (int i = 0; i < w.enemies.Count; i++)
                {
                    var e = w.enemies[i]; if (i > 0) sb.Append(',');
                    sb.Append('[').Append(e.id).Append(',').Append(S(e.type)).Append(',').Append(S(e.state)).Append(',').Append(N(e.st)).Append(',')
                      .Append(N(e.x)).Append(',').Append(N(e.y)).Append(',').Append(N(e.vx)).Append(',').Append(N(e.vy)).Append(',').Append(N(e.hp)).Append(',').Append(N(e.poise)).Append(']');
                }
                sb.Append("],\"R\":[");
                for (int i = 0; i < w.projectiles.Count; i++)
                {
                    var q = w.projectiles[i]; if (i > 0) sb.Append(',');
                    sb.Append('[').Append(S(q.kind)).Append(',').Append(N(q.x)).Append(',').Append(N(q.y)).Append(']');
                }
                sb.Append("],\"ev\":[");
                for (int i = 0; i < w.events.Count; i++) { if (i > 0) sb.Append(','); sb.Append(S(w.events[i].type)); }
                sb.Append("]}\n");
                w.events.Clear();
                var bytes = Encoding.UTF8.GetBytes(sb.ToString()); stdout.Write(bytes, 0, bytes.Length);
            }
            return 0;
        }
    }
}
