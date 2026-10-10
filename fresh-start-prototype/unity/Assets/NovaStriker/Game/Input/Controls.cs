// Input (input.js), on the Input System: keyboard + mouse (one device) and any number of gamepads.
// Each simulation tick, a device produces one command frame (Sim.Cmd) with held/pressed/released edges.
// Gamepad buttons follow the standard layout the prototype used (0 A, 1 B, 2 X, 3 Y, 4 LB, 5 RB, 6 LT, 7 RT,
// 8 View, 9 Menu, 10 L3, 11 R3, 12-15 D-pad up/down/left/right).
using System.Collections.Generic;
using System.Linq;
using NovaStriker.Sim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace NovaStriker.Game
{
    public sealed class MenuEv
    {
        public string dev, type, @char, order;
        public int dir;
        public bool repeat;
    }

    public sealed class Controls
    {
        static readonly string[] BTNS = { "jump", "dash", "melee", "fire", "parry", "sig", "mode", "lock", "sub", "ult" };
        static int B(string name) => System.Array.IndexOf(BTNS, name);
        const float ORDER_HOLD = 0.38f;   // s of D-pad up/down held for the second command (Cover me, Hold here)
        static readonly Dictionary<Key, string> KEYMAP = new Dictionary<Key, string>
        {
            [Key.Space] = "jump", [Key.LeftShift] = "dash", [Key.RightShift] = "dash",
            [Key.J] = "melee", [Key.K] = "fire", [Key.L] = "parry", [Key.Q] = "parry", [Key.E] = "sig", [Key.I] = "sig",
            [Key.R] = "mode", [Key.U] = "mode",   // Echo: cycle scarf mode; Nova: cycle bracer attachment
            [Key.F] = "lock", [Key.O] = "lock",   // lock-on
            [Key.T] = "sub", [Key.Y] = "sub",     // Nova: switch secondary weapon · RAM: Provoke · Fix: switch power-up
            [Key.V] = "ult", [Key.N] = "ult",     // ultimate (a gamepad pulls both triggers)
        };

        sealed class DevState { public bool[] prevHeld = new bool[10]; public int grace; public double lfx = 1, lfy; public bool swallow, prevL3; }
        int laneKb;   // a depth-lane hop asked for on the keyboard since the last sample: B toward the back, M toward the camera
        sealed class Rep { public float t0, next; public bool done; }

        readonly HashSet<string> kbPressed = new HashSet<string>();   // presses since last sample (so fast taps aren't lost)
        readonly HashSet<int> mousePressed = new HashSet<int>();
        readonly Dictionary<string, DevState> devices = new Dictionary<string, DevState>();
        readonly Dictionary<string, Dictionary<string, Rep>> repeat = new Dictionary<string, Dictionary<string, Rep>>();
        readonly Dictionary<string, bool[]> prevPads = new Dictionary<string, bool[]>();
        List<MenuEv> menuEvents = new List<MenuEv>();
        public bool menuOpen;   // a menu is up: the stick navigates it, and held directions repeat
        bool anyKbm;            // any keyboard/mouse input since last join poll
        public float mouseX, mouseY;   // pixels from the top left

        public static string IdOf(Gamepad p) => "pad" + p.deviceId;
        public static Gamepad PadOf(string dev) => Gamepad.all.FirstOrDefault(p => IdOf(p) == dev);

        static ButtonControl Btn(Gamepad p, int i)
        {
            switch (i)
            {
                case 0: return p.buttonSouth; case 1: return p.buttonEast; case 2: return p.buttonWest; case 3: return p.buttonNorth;
                case 4: return p.leftShoulder; case 5: return p.rightShoulder; case 6: return p.leftTrigger; case 7: return p.rightTrigger;
                case 8: return p.selectButton; case 9: return p.startButton; case 10: return p.leftStickButton; case 11: return p.rightStickButton;
                case 12: return p.dpad.up; case 13: return p.dpad.down; case 14: return p.dpad.left; default: return p.dpad.right;
            }
        }
        static bool[] PadButtons(Gamepad p) { var b = new bool[16]; for (int i = 0; i < 16; i++) b[i] = Btn(p, i).isPressed; return b; }

        // Once a frame: the keyboard and mouse presses since the last frame, and the menu keys
        public void Poll()
        {
            var kb = Keyboard.current; var ms = Mouse.current;
            if (kb != null)
            {
                foreach (var k in KEYMAP.Keys) if (kb[k].wasPressedThisFrame) { kbPressed.Add(KEYMAP[k]); anyKbm = true; }
                if (kb[Key.B].wasPressedThisFrame) laneKb = -1; if (kb[Key.M].wasPressedThisFrame) laneKb = 1;
                if (kb.anyKey.wasPressedThisFrame) anyKbm = true;
                void Ev(Key k, MenuEv e) { if (kb[k].wasPressedThisFrame) { e.dev = "kbm"; menuEvents.Add(e); } }
                Ev(Key.Escape, new MenuEv { type = "pause" }); Ev(Key.P, new MenuEv { type = "pause" });
                Ev(Key.H, new MenuEv { type = "help" }); Ev(Key.Backquote, new MenuEv { type = "debug" });
                Ev(Key.Tab, new MenuEv { type = "swap", dir = 1 });
                Ev(Key.Digit1, new MenuEv { type = "pick", @char = "nova" }); Ev(Key.Digit2, new MenuEv { type = "pick", @char = "echo" });
                Ev(Key.Digit3, new MenuEv { type = "pick", @char = "ram" }); Ev(Key.Digit4, new MenuEv { type = "pick", @char = "fix" });
                // Team commands to the AI teammates (Bot): Z attack my target · G cover me · X regroup on me · C hold here
                Ev(Key.Z, new MenuEv { type = "order", order = "attack" }); Ev(Key.G, new MenuEv { type = "order", order = "cover" });
                Ev(Key.X, new MenuEv { type = "order", order = "regroup" }); Ev(Key.C, new MenuEv { type = "order", order = "hold" });
                // (menus are also driven by the keyboard's arrows and Enter)
                if (menuOpen)
                {
                    Ev(Key.UpArrow, new MenuEv { type = "up" }); Ev(Key.DownArrow, new MenuEv { type = "down" });
                    Ev(Key.Enter, new MenuEv { type = "confirm" });
                }
            }
            if (ms != null)
            {
                var pos = ms.position.ReadValue(); mouseX = pos.x; mouseY = Screen.height - pos.y;
                var bs = new[] { ms.leftButton, ms.middleButton, ms.rightButton, ms.backButton, ms.forwardButton };
                for (int i = 0; i < 5; i++) if (bs[i].wasPressedThisFrame) { mousePressed.Add(i); anyKbm = true; }
            }
            PollPadMenus();
        }

        // Devices that pressed something this frame and are not yet assigned
        public List<string> PollJoins(ICollection<string> assigned)
        {
            var o = new List<string>();
            if (anyKbm && !assigned.Contains("kbm")) o.Add("kbm");
            anyKbm = false;
            foreach (var p in Gamepad.all)
            {
                string id = IdOf(p);
                if (assigned.Contains(id)) continue;
                if (PadButtons(p).Any(b => b)) o.Add(id);
            }
            return o;
        }

        // Pad menu buttons (Menu/View/D-pad) become UI events. While a menu is open the left stick moves through it
        // too, and a direction held on the D-pad or stick repeats after a short pause.
        void PollPadMenus()
        {
            float t = Time.unscaledTime;
            foreach (var p in Gamepad.all)
            {
                string id = IdOf(p);
                prevPads.TryGetValue(id, out var prev); prev ??= new bool[16];
                var now = PadButtons(p);
                bool Edge(int i) => now[i] && !prev[i];
                void Push(MenuEv e) { e.dev = id; menuEvents.Add(e); }
                if (Edge(9)) Push(new MenuEv { type = "pause" });
                if (Edge(8)) Push(new MenuEv { type = "help" });
                if (Edge(0)) Push(new MenuEv { type = "confirm" });
                if (Edge(1)) Push(new MenuEv { type = "back" });
                if (Edge(4)) Push(new MenuEv { type = "prevTab" });
                if (Edge(5)) Push(new MenuEv { type = "nextTab" });
                var st = p.leftStick.ReadValue();
                float sx = menuOpen ? st.x : 0, sy = menuOpen ? -st.y : 0;   // (the prototype's axes run downward)
                var dirs = new List<(string k, bool on, MenuEv ev)>
                {
                    ("up", now[12] || sy < -0.6f, new MenuEv { type = "up" }), ("down", now[13] || sy > 0.6f, new MenuEv { type = "down" }),
                    ("left", now[14] || sx < -0.6f, new MenuEv { type = "swap", dir = -1 }), ("right", now[15] || sx > 0.6f, new MenuEv { type = "swap", dir = 1 }),
                };
                if (!repeat.TryGetValue(id, out var R)) repeat[id] = R = new Dictionary<string, Rep>();
                // In play, D-pad up and down are team commands: a tap of up is Attack my target and holding it Cover me;
                // a tap of down is Regroup on me and holding it Hold here (ORDER_HOLD)
                if (!menuOpen)
                {
                    foreach (var (btn, tap, hold) in new[] { (12, "attack", "cover"), (13, "regroup", "hold") })
                    {
                        string k = "o" + btn; R.TryGetValue(k, out var r);
                        if (now[btn] && r == null) R[k] = new Rep { t0 = t };
                        else if (now[btn] && r != null && !r.done && t - r.t0 >= ORDER_HOLD) { r.done = true; Push(new MenuEv { type = "order", order = hold }); }
                        else if (!now[btn] && r != null) { if (!r.done) Push(new MenuEv { type = "order", order = tap }); R[k] = null; }
                    }
                    dirs.RemoveRange(0, 2);   // (so they are not menu directions too)
                }
                foreach (var (k, on, ev) in dirs)
                {
                    R.TryGetValue(k, out var r);
                    if (!on) { R[k] = null; continue; }
                    if (r == null) { R[k] = new Rep { next = t + 0.32f }; Push(ev); }
                    else if (menuOpen && t >= r.next) { r.next = t + 0.11f; ev.repeat = true; Push(ev); }
                }
                prevPads[id] = now;
            }
        }

        static (double, double) Deadzone(double x, double y, double dz)
        {
            double m = System.Math.Sqrt(x * x + y * y);
            if (m < dz) return (0, 0);
            double s = System.Math.Min(1, (m - dz) / (1 - dz)) / m;
            return (x * s, y * s);
        }

        // aimFromMouse(screenX, screenY) is supplied by the caller: a unit sim-space vector, or null
        public Cmd Sample(string dev, System.Func<float, float, double[]> aimFromMouse, string p1AimMode)
        {
            if (!devices.TryGetValue(dev, out var st)) devices[dev] = st = new DevState();
            var held = new bool[10]; var extra = new bool[10];
            double mx = 0, my = 0, ax = 0, ay = 0; bool aimFree = false;
            if (dev == "kbm")
            {
                var kb = Keyboard.current; var ms = Mouse.current;
                bool K(Key k) => kb != null && kb[k].isPressed;
                mx = (K(Key.D) || K(Key.RightArrow) ? 1 : 0) - (K(Key.A) || K(Key.LeftArrow) ? 1 : 0);
                my = (K(Key.W) || K(Key.UpArrow) ? 1 : 0) - (K(Key.S) || K(Key.DownArrow) ? 1 : 0);
                foreach (var kv in KEYMAP) if (K(kv.Key)) held[B(kv.Value)] = true;
                if (ms != null)
                {
                    if (ms.leftButton.isPressed) held[B("fire")] = true;
                    if (ms.middleButton.isPressed) held[B("sig")] = true;
                    if (ms.rightButton.isPressed) held[B("melee")] = true;
                    if (ms.backButton.isPressed) held[B("mode")] = true;
                    if (ms.forwardButton.isPressed) held[B("lock")] = true;
                }
                if (p1AimMode == "mouse" && aimFromMouse != null)
                {
                    var v = aimFromMouse(mouseX, mouseY);
                    if (v != null) { aimFree = true; ax = v[0]; ay = v[1]; }
                }
                // Presses that happened and released between samples still count as presses
                foreach (var b in kbPressed) extra[B(b)] = true;
                if (mousePressed.Contains(0)) extra[B("fire")] = true;
                if (mousePressed.Contains(2)) extra[B("melee")] = true;
                if (mousePressed.Contains(1)) extra[B("sig")] = true;
                if (mousePressed.Contains(3)) extra[B("mode")] = true;
                if (mousePressed.Contains(4)) extra[B("lock")] = true;
                kbPressed.Clear(); mousePressed.Clear();
                var kc = Finish(st, held, extra, mx, my, aimFree, ax, ay); kc.lane = laneKb; laneKb = 0;
                return kc;
            }
            var pad = PadOf(dev);
            if (pad != null)
            {
                bool Bt(int i) { var c = Btn(pad, i); return c.isPressed || c.ReadValue() > 0.5f; }
                var l = pad.leftStick.ReadValue(); var r = pad.rightStick.ReadValue();
                (mx, my) = Deadzone(l.x, l.y, 0.22);
                var (rx, ry) = Deadzone(r.x, r.y, 0.3);
                if (Bt(12)) my = 1; if (Bt(13)) my = -1;
                held[B("jump")] = Bt(0);
                held[B("sub")] = Bt(4);     // LB: switch secondary weapon
                held[B("dash")] = Bt(1);
                held[B("melee")] = Bt(2);
                held[B("sig")] = Bt(3);
                held[B("mode")] = Bt(5);
                held[B("parry")] = pad.leftTrigger.ReadValue() > 0.5f;
                held[B("fire")] = pad.rightTrigger.ReadValue() > 0.35f;
                held[B("lock")] = Bt(11);   // right stick click
                double rm = System.Math.Sqrt(rx * rx + ry * ry);
                if (rm > 0.35) { aimFree = true; ax = rx / rm; ay = ry / rm; st.grace = 18; st.lfx = ax; st.lfy = ay; }
                else if (st.grace > 0) { st.grace--; aimFree = true; ax = st.lfx; ay = st.lfy; }
                // a depth-lane hop: click the left stick, pushed down for the front lane (up or level for the back)
                bool l3 = Bt(10); int lane = l3 && !st.prevL3 ? (l.y < -0.5f ? 1 : -1) : 0; st.prevL3 = l3;
                var pc = Finish(st, held, extra, mx, my, aimFree, ax, ay); pc.lane = lane;
                return pc;
            }
            return Finish(st, held, extra, mx, my, aimFree, ax, ay);
        }

        // After a menu closes, buttons still held from closing it count as already held (no stray jump or dash)
        public void SwallowAll() { foreach (var st in devices.Values) st.swallow = true; kbPressed.Clear(); mousePressed.Clear(); }

        static Cmd Finish(DevState st, bool[] held, bool[] extra, double mx, double my, bool aimFree, double ax, double ay)
        {
            if (st.swallow) { st.swallow = false; st.prevHeld = (bool[])held.Clone(); extra = new bool[10]; }
            var c = new Cmd { mx = mx, my = my, aimFree = aimFree, ax = ax, ay = ay };
            for (int i = 0; i < 10; i++)
            {
                c.held[i] = held[i];
                c.pressed[i] = (held[i] && !st.prevHeld[i]) || extra[i];
                c.released[i] = !held[i] && st.prevHeld[i];
            }
            st.prevHeld = (bool[])held.Clone();
            return c;
        }

        public List<MenuEv> TakeMenuEvents() { var e = menuEvents; menuEvents = new List<MenuEv>(); return e; }
    }
}
