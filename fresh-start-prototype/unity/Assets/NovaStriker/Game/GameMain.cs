// The bootstrap (main.js): a fixed 60 Hz simulation with interpolated rendering, drop-in joining, menus, the AI
// teammates and their team commands, and the hit-pause that holds the world still under an impact frame.
// Put it on one object in an empty scene (the editor setup's scene has it); it builds everything else.
using System.Collections.Generic;
using System.Linq;
using NovaStriker.Game.Audio;
using NovaStriker.Game.UI;
using NovaStriker.Sim;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using static NovaStriker.Sim.Cfg;

namespace NovaStriker.Game
{
    public sealed partial class GameMain : MonoBehaviour
    {
        World world;
        View view;
        Controls input;
        Ui ui;
        Sound sound;
        Music music;
        Haptics haptics;
        Bots bots;
        Ctx audio;
        bool started, paused;
        double acc;
        float fps = 60, fpsT; int fpsN;
        readonly List<(float at, System.Action fn)> later = new List<(float, System.Action)>();   // (setTimeout)

        void Awake()
        {
            Application.targetFrameRate = -1;
            QualitySettings.vSyncCount = 1;
            SettingsStore.Load();
            input = new Controls();
            world = new World();
            view = new View();
            haptics = new Haptics();
            bots = new Bots();
            // Sound and score: a synthesized graph rendered in the listener's audio callback
            audio = new Ctx(AudioSettings.outputSampleRate);
            view.camera.gameObject.AddComponent<AudioOut>().ctx = audio;
            sound = new Sound(); sound.Unlock(audio);
            music = new Music(); music.Start(audio);
            // UI events (mouse clicks) through the Input System; the menus do their own controller navigation
            if (EventSystem.current == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem));
                var m = es.AddComponent<InputSystemUIInputModule>();
                m.move = null; m.submit = null; m.cancel = null;
            }
            ui = new Ui(new UiHandlers
            {
                resume = () => SetPaused(false),
                zone = id => { world.Teleport(id); SetPaused(false); },
                boss = id => { world.BossRush(id); SetPaused(false); },
                pick = (p, c) => world.SwapCharacter(p, c),
                // removing an AI teammate turns the setting down by one, so it isn't simply added back
                remove = p =>
                {
                    if (Bots.IsBot(p)) { SETTINGS.aiTeammates = System.Math.Max(0, SETTINGS.aiTeammates - 1); SettingsStore.Save(); }
                    sound.jet(p, false); world.RemovePlayer(p.slot);
                },
            });
            StartReviewIfRequested();
        }

        void SetPaused(bool on)
        {
            paused = on; ui.SetPaused(on, world);
            ParticleBudget.Advancing = !on;
            if (!on) input.SwallowAll();
            if (on) haptics.StopAll();
        }

        void TryJoin()
        {
            var devices = input.PollJoins(new HashSet<string>(world.players.Select(p => p.device)));
            foreach (var dev in devices)
            {
                if (paused) break;
                if (world.players.Count >= 4 && !bots.MakeRoom(world)) break;   // a person joining a full team takes an AI teammate's place
                // Each new player takes the next character no one is playing yet (Nova, Echo, RAM, Fix)
                var used = new HashSet<string>(world.players.Select(p => p.@char));
                string ch = ROSTER.FirstOrDefault(c => !used.Contains(c)) ?? ROSTER[world.players.Count % ROSTER.Length];
                world.AddPlayer(dev, ch);
                if (!started) { started = true; ui.HideStart(); }
            }
        }

        // A team command to the AI teammates: they answer, and the HUD shows it while it stands
        void GiveOrder(Player p, string type)
        {
            if (p.state == "dead" || Bots.IsBot(p)) return;
            var answers = bots.Issue(world, p, type);
            if (answers.Count == 0) { ui.Toast("No AI teammates to command (Settings: AI teammates)"); return; }
            ui.Toast(bots.order != null ? $"P{p.slot + 1}: {Bots.ORDERS.names[type]}" : $"P{p.slot + 1}: back to following");
            for (int i = 0; i < answers.Count; i++)
            {
                var (b, line) = answers[i];
                later.Add((Time.unscaledTime + 0.15f + i * 0.35f, () => { if (world.players.Contains(b)) ui.BarkAt(b, line); }));
            }
            if (bots.order != null) view.fx.GroundRing(p.x, p.y, PLAYER_COLORS[p.slot], 0.4f, 2.4f, 0.5f, 0.85f);
        }

        void HandleMenuEvents()
        {
            foreach (var ev in input.TakeMenuEvents())
            {
                // The controls screen closes with any controller's B, A, Start or View (H or Esc on the keyboard); the
                // buttons that closed it don't also act in the game
                if (started && ui.helpOpen)
                {
                    if (ev.type == "back" || ev.type == "confirm" || ev.type == "pause" || ev.type == "help") { ui.ToggleHelp(false); input.SwallowAll(); }
                    else if (ev.type == "up" || ev.type == "down") ui.ScrollHelp(ev.type == "down" ? 1 : -1);   // the D-pad scrolls it
                    continue;
                }
                var p = world.players.FirstOrDefault(q => q.device == ev.dev);
                if (!started || (p == null && ev.type != "help")) continue;
                if (ev.type == "pause") SetPaused(!paused);
                else if (ev.type == "help") ui.ToggleHelp();
                else if (ev.type == "debug") ui.ToggleDebug();
                else if (paused) ui.MenuNav(ev);
                else if (ev.type == "swap") world.SwapCharacter(p, NextChar(p.@char, ev.dir != 0 ? ev.dir : 1));
                else if (ev.type == "pick") world.SwapCharacter(p, ev.@char);
                else if (ev.type == "order") GiveOrder(p, ev.order);
            }
        }

        void StepSim()
        {
            if (review != null && reviewHoldSimulation) return;
            bots.Sync(world, (int)SETTINGS.aiTeammates);
            var cmds = new Dictionary<int, Cmd>();
            foreach (var p in world.players.ToList())
            {
                if (Bots.IsBot(p)) continue;
                var pp = p;
                cmds[p.slot] = input.Sample(p.device, (mx, my) => view.AimFromMouse(mx, my, pp), SETTINGS.p1Aim);
            }
            bots.Commands(world, cmds);
            // The command in force: on the HUD, a marker where they hold, and a word when an attack order's target falls
            var O = bots.order;
            if (O != null && world.players.Contains(O.by)) ui.SetOrder(Bots.ORDERS.names[O.type], O.by.slot); else ui.SetOrder(null, 0);
            if (O != null && O.type == "hold" && world.tick % 50 == 0) view.fx.GroundRing(O.x, O.y, PLAYER_COLORS[O.by.slot], 0.5f, 1.8f, 0.45f, 0.6f);
            if (bots.done == world.tick) { var b = world.players.FirstOrDefault(Bots.IsBot); if (b != null) ui.BarkAt(b, Bots.ORDERS.lines[b.@char]["done"]); }
            world.Step(cmds);
            foreach (var ev in world.events) { view.OnEvent(ev); sound.Play(ev); ui.OnEvent(ev, world); haptics.OnEvent(ev); }
            world.events.Clear();
        }

        void Update()
        {
            float dt = Mathf.Min(0.1f, Mathf.Max(0, Time.unscaledDeltaTime));
            fpsT += dt; fpsN++; if (fpsT >= 0.5f) { fps = fpsN / fpsT; fpsT = 0; fpsN = 0; }
            for (int i = later.Count - 1; i >= 0; i--) if (Time.unscaledTime >= later[i].at) { var f = later[i].fn; later.RemoveAt(i); f(); }
            input.menuOpen = paused || ui.helpOpen;
            input.Poll();
            HandleMenuEvents();
            TryJoin();
            if (!started) ui.GamepadNotice(UnityEngine.InputSystem.Gamepad.all.Count == 0 && UnityEngine.InputSystem.Keyboard.current == null);
            world.aspect = (double)Screen.width / System.Math.Max(1, Screen.height);
            bool halted = paused || ui.helpOpen;   // the game waits while a menu or the controls screen is open
            if (started && !halted)
            {
                if (view.hitPause > 0) { view.hitPause -= dt; acc = 0; }   // an impact frame's hit-pause holds the world still
                else
                {
                    acc += dt; int steps = 0;
                    while (acc >= DT && steps < 5) { StepSim(); acc -= DT; steps++; }
                    if (steps == 5) acc = 0;
                }
            }
            music.Update(dt, started ? world : null, halted);
            sound.Update(started && !halted ? world : null);   // charge hums and wall-slide grind
            haptics.Update(world, started && !halted);
            view.Render(world, halted ? 1 : Mathf.Min(1, (float)(acc / DT)), halted ? 0 : dt);
            ui.Update(dt, world, view, fps);
        }

        void OnApplicationQuit() { haptics?.StopAll(); }
        void OnDisable() { haptics?.StopAll(); }
    }
}
