// The controls players can change (Controls › Edit controls): every action with up to two keys, a mouse button and
// a gamepad button. Kept with the settings (Settings.keyBindings: only what differs from the defaults). A key, mouse
// button or gamepad button belongs to one action at a time; giving it to another takes it from the first. Some
// inputs stay fixed so no one can lock themselves out: Esc always pauses, Enter and the arrows work the menus, ` opens
// the debug view, and on a gamepad Menu, View, the D-pad and the left stick keep their jobs.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.InputSystem;
using static NovaStriker.Sim.Cfg;

namespace NovaStriker.Game
{
    public static class Bindings
    {
        public sealed class Def
        {
            public string id, label, group;
            public Key[] keys = new Key[0]; public int mouse = -1, pad = -1;
            public bool kb = true, ms, pd;   // which columns it has
        }
        public sealed class Bind { public Key k1 = Key.None, k2 = Key.None; public int mouse = -1, pad = -1; }

        public static readonly string[] GROUPS = { "Movement", "Combat", "Characters and team", "Menus" };
        static Def D(string group, string id, string label, Key[] keys, int mouse = -2, int pad = -2, bool kb = true) =>
            new Def { group = group, id = id, label = label, keys = keys, mouse = mouse < -1 ? -1 : mouse, ms = mouse > -2, pad = pad < -1 ? -1 : pad, pd = pad > -2, kb = kb };
        static Key[] K(params Key[] k) => k;
        public static readonly Def[] DEFS =
        {
            D("Movement", "moveLeft", "Move left", K(Key.A, Key.LeftArrow)),
            D("Movement", "moveRight", "Move right", K(Key.D, Key.RightArrow)),
            D("Movement", "moveUp", "Up (aim up, up attacks)", K(Key.W, Key.UpArrow)),
            D("Movement", "moveDown", "Down (crouch, slide, ground pound)", K(Key.S, Key.DownArrow)),
            D("Movement", "jump", "Jump", K(Key.Space), pad: 0),
            D("Movement", "dash", "Dash", K(Key.LeftShift, Key.RightShift), pad: 1),
            D("Movement", "laneBack", "Depth lane: hop back", K(Key.B)),
            D("Movement", "laneFront", "Depth lane: hop front", K(Key.M)),
            D("Movement", "laneHop", "Depth lane hop (gamepad: stick up = back, down = front)", K(), pad: 10, kb: false),
            D("Combat", "fire", "Fire (hold to charge)", K(Key.K), mouse: 0, pad: 7),
            D("Combat", "melee", "Melee (hold to charge)", K(Key.J), mouse: 2, pad: 2),
            D("Combat", "parry", "Parry / dodge / guard / beam", K(Key.Q, Key.L), mouse: -1, pad: 6),
            D("Combat", "sig", "Suit ability", K(Key.E, Key.I), mouse: 1, pad: 3),
            D("Combat", "mode", "Switch mode", K(Key.R, Key.U), mouse: 3, pad: 5),
            D("Combat", "sub", "Character action (weapon, Provoke, power-up)", K(Key.T, Key.Y), mouse: -1, pad: 4),
            D("Combat", "lock", "Lock-on: switch target", K(Key.F, Key.O), mouse: 4, pad: 11),
            D("Combat", "ult", "Ultimate (parry + fire together also works)", K(Key.V, Key.N), mouse: -1, pad: -1),
            D("Characters and team", "swap", "Next character", K(Key.Tab)),
            D("Characters and team", "pick1", "Play as Nova", K(Key.Digit1)),
            D("Characters and team", "pick2", "Play as Echo", K(Key.Digit2)),
            D("Characters and team", "pick3", "Play as RAM", K(Key.Digit3)),
            D("Characters and team", "pick4", "Play as Fix", K(Key.Digit4)),
            D("Characters and team", "orderAttack", "Team: attack my target", K(Key.Z)),
            D("Characters and team", "orderCover", "Team: cover me", K(Key.G)),
            D("Characters and team", "orderRegroup", "Team: regroup on me", K(Key.X)),
            D("Characters and team", "orderHold", "Team: hold here", K(Key.C)),
            D("Menus", "pause", "Pause and settings (Esc always works too)", K(Key.P)),
            D("Menus", "help", "Controls", K(Key.H)),
        };
        public static Def Of(string id) => Array.Find(DEFS, d => d.id == id);

        // Inputs no action may take
        static readonly HashSet<Key> RESERVED_KEYS = new HashSet<Key> { Key.Escape, Key.Enter, Key.NumpadEnter, Key.Backquote, Key.Delete, Key.Backspace };
        public static bool ReservedKey(Key k) => RESERVED_KEYS.Contains(k);
        public static bool ReservedPad(int i) => i == 8 || i == 9 || i >= 12;   // View, Menu, the D-pad

        // ---- The current bindings (the defaults, then the saved changes) ----
        static Dictionary<string, Bind> cur; static string loadedFrom;
        public static int Version { get; private set; }
        static Dictionary<string, Bind> Cur
        {
            get
            {
                var raw = SETTINGS.keyBindings ?? "";
                if (cur == null || !ReferenceEquals(raw, loadedFrom)) { cur = Parse(raw); loadedFrom = raw; Version++; }
                return cur;
            }
        }
        static Bind Default(Def d) => new Bind { k1 = d.keys.Length > 0 ? d.keys[0] : Key.None, k2 = d.keys.Length > 1 ? d.keys[1] : Key.None, mouse = d.mouse, pad = d.pad };
        static Dictionary<string, Bind> Parse(string raw)
        {
            var o = DEFS.ToDictionary(d => d.id, Default);
            foreach (var part in raw.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                // id:key1/key2:mouse:pad (- for none)
                var f = part.Split(':'); if (f.Length != 4 || !o.ContainsKey(f[0])) continue;
                var b = new Bind(); var ks = f[1].Split('/');
                Key PK(string s) => s != "-" && Enum.TryParse<Key>(s, out var k) ? k : Key.None;
                b.k1 = ks.Length > 0 ? PK(ks[0]) : Key.None; b.k2 = ks.Length > 1 ? PK(ks[1]) : Key.None;
                b.mouse = int.TryParse(f[2], out var m) ? m : -1; b.pad = int.TryParse(f[3], out var p) ? p : -1;
                o[f[0]] = b;
            }
            return o;
        }
        static void Save()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var d in DEFS)
            {
                var b = cur[d.id]; var def = Default(d);
                if (b.k1 == def.k1 && b.k2 == def.k2 && b.mouse == def.mouse && b.pad == def.pad) continue;
                string KS(Key k) => k == Key.None ? "-" : k.ToString();
                sb.Append(d.id).Append(':').Append(KS(b.k1)).Append('/').Append(KS(b.k2)).Append(':').Append(b.mouse < 0 ? "-" : b.mouse.ToString()).Append(':').Append(b.pad < 0 ? "-" : b.pad.ToString()).Append(' ');
            }
            SETTINGS.keyBindings = sb.ToString().Trim(); loadedFrom = SETTINGS.keyBindings; Version++;
            SettingsStore.Save();
        }

        public static Bind Get(string id) => Cur[id];
        // The keys bound to an action (none, one or two), cached as arrays (they are read every frame)
        static readonly Dictionary<string, Key[]> keyArr = new Dictionary<string, Key[]>(); static int keyArrV = -1;
        public static Key[] Keys(string id)
        {
            var all = Cur;
            if (keyArrV != Version) { keyArr.Clear(); keyArrV = Version; }
            if (keyArr.TryGetValue(id, out var a)) return a;
            var b = all[id]; var l = new List<Key>(2); if (b.k1 != Key.None) l.Add(b.k1); if (b.k2 != Key.None && b.k2 != b.k1) l.Add(b.k2);
            return keyArr[id] = l.ToArray();
        }
        public static int Mouse(string id) => Cur[id].mouse;
        public static int Pad(string id) => Cur[id].pad;

        // Gives an input to an action's slot (0, 1 keys; 2 mouse; 3 gamepad). Whatever had it loses it: returns that
        // action's label, or null. A value of -1 (Key.None) clears the slot.
        public static string Assign(string id, int slot, int value)
        {
            var all = Cur; string took = null;
            if (value >= 0 && !(slot <= 1 && value == (int)Key.None))
                foreach (var d in DEFS)
                {
                    var o = all[d.id];
                    if (slot <= 1)
                    {
                        if (d.id == id && o.k1 == (Key)value && slot == 1) { o.k1 = Key.None; continue; }   // (moving it within the row)
                        if (d.id == id && o.k2 == (Key)value && slot == 0) { o.k2 = Key.None; continue; }
                        if (d.id != id && (o.k1 == (Key)value || o.k2 == (Key)value)) { if (o.k1 == (Key)value) o.k1 = Key.None; if (o.k2 == (Key)value) o.k2 = Key.None; took = d.label; }
                    }
                    else if (slot == 2 && d.id != id && o.mouse == value) { o.mouse = -1; took = d.label; }
                    else if (slot == 3 && d.id != id && o.pad == value) { o.pad = -1; took = d.label; }
                }
            var b = all[id];
            if (slot == 0) b.k1 = value < 0 ? Key.None : (Key)value;
            else if (slot == 1) b.k2 = value < 0 ? Key.None : (Key)value;
            else if (slot == 2) b.mouse = value;
            else b.pad = value;
            Save();
            return took;
        }
        public static void Reset(string id) { Cur[id] = Default(Of(id)); FixClashes(id); Save(); }
        public static void ResetAll() { cur = Parse(""); Save(); }
        // After a row goes back to its defaults, other rows that took those inputs since give them up
        static void FixClashes(string id)
        {
            var b = cur[id];
            foreach (var d in DEFS)
            {
                if (d.id == id) continue; var o = cur[d.id];
                if (o.k1 != Key.None && (o.k1 == b.k1 || o.k1 == b.k2)) o.k1 = Key.None;
                if (o.k2 != Key.None && (o.k2 == b.k1 || o.k2 == b.k2)) o.k2 = Key.None;
                if (o.mouse >= 0 && o.mouse == b.mouse) o.mouse = -1;
                if (o.pad >= 0 && o.pad == b.pad) o.pad = -1;
            }
        }

        // ---- Names ----
        static readonly string[] MOUSE = { "Left click", "Middle click", "Right click", "Mouse back", "Mouse forward" };
        static readonly string[] PAD = { "A", "B", "X", "Y", "LB", "RB", "LT", "RT", "View", "Menu", "L3", "R3", "D-pad up", "D-pad down", "D-pad left", "D-pad right" };
        public static string MouseName(int i) => i >= 0 && i < MOUSE.Length ? MOUSE[i] : "";
        public static string PadName(int i) => i >= 0 && i < PAD.Length ? PAD[i] : "";
        public static string KeyName(Key k)
        {
            switch (k)
            {
                case Key.None: return "";
                case Key.LeftShift: return "Shift"; case Key.RightShift: return "Right Shift";
                case Key.LeftCtrl: return "Ctrl"; case Key.RightCtrl: return "Right Ctrl";
                case Key.LeftAlt: return "Alt"; case Key.RightAlt: return "Right Alt";
                case Key.LeftArrow: return "Left"; case Key.RightArrow: return "Right"; case Key.UpArrow: return "Up"; case Key.DownArrow: return "Down";
                case Key.Space: return "Space"; case Key.Tab: return "Tab"; case Key.CapsLock: return "Caps Lock";
                case Key.Semicolon: return ";"; case Key.Quote: return "'"; case Key.Comma: return ","; case Key.Period: return "."; case Key.Slash: return "/";
                case Key.Backslash: return "\\"; case Key.LeftBracket: return "["; case Key.RightBracket: return "]"; case Key.Minus: return "-"; case Key.Equals: return "=";
            }
            var s = k.ToString();
            if (s.StartsWith("Digit")) return s.Substring(5);
            if (s.StartsWith("Numpad")) return "Num " + s.Substring(6);
            return s;
        }
        // An action's keyboard and mouse inputs as words: "Q or L", "E, I, or middle click" (both Shifts read as one)
        public static string KbmText(string id, bool firstOnly = false)
        {
            var ks = Keys(id).ToList();   // (a copy: the pairs below are merged in it)
            if (firstOnly) return ks.Count > 0 ? KeyName(ks[0]) : "(unbound)";
            var names = new List<string>();
            foreach (var pair in new[] { (Key.LeftShift, Key.RightShift), (Key.LeftCtrl, Key.RightCtrl), (Key.LeftAlt, Key.RightAlt) })
                if (ks.Contains(pair.Item1) && ks.Contains(pair.Item2)) { ks.Remove(pair.Item2); }
            names.AddRange(ks.Select(KeyName));
            int m = Mouse(id); if (m >= 0) names.Add(names.Count > 0 ? MouseName(m).ToLowerInvariant() : MouseName(m));
            if (names.Count == 0) return "(unbound)";
            if (names.Count == 1) return names[0];
            if (names.Count == 2) return names[0] + " or " + names[1];
            return string.Join(", ", names.Take(names.Count - 1)) + ", or " + names[names.Count - 1];
        }
        public static string PadText(string id) { int p = Pad(id); return p >= 0 ? PadName(p) : "(unbound)"; }
        // Help text with the live bindings: {k:id} its keys and mouse button, {k1:id} its first key, {p:id} its gamepad button
        static readonly System.Text.RegularExpressions.Regex TOKEN = new System.Text.RegularExpressions.Regex(@"\{(k1|k|p):(\w+)\}");
        public static string Expand(string s)
        {
            if (string.IsNullOrEmpty(s) || s.IndexOf('{') < 0) return s;
            return TOKEN.Replace(s, m => Of(m.Groups[2].Value) == null ? m.Value : m.Groups[1].Value == "p" ? PadText(m.Groups[2].Value) : KbmText(m.Groups[2].Value, m.Groups[1].Value == "k1"));
        }
    }
}
