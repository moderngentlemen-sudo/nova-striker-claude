// Haptics (haptics.js): each player feels their own events on their own controller, through the Input
// System's dual motors (the strong, low-frequency motor and the weak, high-frequency one). Keyboard and mouse
// have nothing to shake. Effects are timed here: a motor runs until its effect's length is up.
using System.Collections.Generic;
using NovaStriker.Sim;
using UnityEngine;
using UnityEngine.InputSystem;
using static NovaStriker.Sim.Cfg;

namespace NovaStriker.Game
{
    public sealed class Haptics
    {
        // (strong motor 0-1, weak motor 0-1, milliseconds, priority). Lower-priority effects never cut off a
        // stronger one that is still playing.
        struct E { public float s, w, ms; public int prio; public E(float s, float w, float ms, int prio) { this.s = s; this.w = w; this.ms = ms; this.prio = prio; } }
        static E? Level(double L) => L >= 3 ? new E(0.35f, 0.7f, 80, 2) : L == 2 ? new E(0.15f, 0.45f, 55, 2) : new E(0, 0.3f, 45, 2);

        static E? Fx(Ev ev)
        {
            float lv = (float)ev.level, k = ev.power != 0 ? (float)ev.power : 0.5f;
            switch (ev.type)
            {
                case "playerHit": return ev.heavy ? new E(0.9f, 0.7f, 240, 3) : new E(0.55f, 0.45f, 150, 3);
                case "downed": return new E(1, 1, 420, 4);
                case "revived": return new E(0.3f, 0.6f, 160, 3);
                case "parry": return ev.perfect ? new E(0.25f, 0.9f, 110, 3) : new E(0.1f, 0.55f, 70, 3);
                case "hit": return ev.heavy ? new E(0.35f, 0.6f, 80, 1) : new E(0.08f, 0.35f, 40, 1);
                case "kill": return new E(0.2f, 0.55f, 70, 2);
                case "chargeLevel": case "burstLevel": case "dashLevel": case "poundLevel": return Level(ev.level);
                case "rifleRaise": return new E(0, 0.2f, 30, 1);
                case "rifleFocus": return new E(0.2f, 0.55f, 70, 2);
                case "shot": return ev.level != 0 ? new E(Mathf.Min(1, 0.2f + 0.2f * lv + (ev.perfect ? 0.25f : 0)), 0.4f + 0.1f * lv, 70 + 30 * lv, 3) : (E?)null;
                case "snipe": { float f = (float)ev.f; return new E(0.45f + 0.45f * f, 0.55f + 0.2f * f, 100 + 80 * f, 3); }
                case "crit": return new E(0.2f, 0.6f, 50, 2);
                case "deflect": return ev.perfect ? new E(0.3f, 0.9f, 100, 3) : new E(0.12f, 0.6f, 60, 2);
                case "dashSlash": { float t = (float)ev.tier; return new E(0.25f + 0.15f * t, 0.5f, 80 + 20 * t, 2); }
                case "crescent": return new E(0.2f, 0.5f, 70, 2);
                case "pogo": return new E(0.1f, 0.5f, 50, 2);
                case "poundStart": return new E(0, 0.3f, 40, 1);
                case "poundDrop": return new E(0.1f, 0.35f, 60, 1);
                case "poundLand": return new E(Mathf.Min(1, 0.5f + 0.17f * lv), Mathf.Min(1, 0.6f + 0.12f * lv), 140 + 50 * lv, 3);
                case "beamStart": return new E(0.7f, 0.9f, 220, 3);
                case "beamEnd": return new E(0.1f, 0.3f, 80, 1);
                case "aegisOn": return new E(0.15f, 0.5f, 90, 2);
                case "aegisHit": return new E(0.2f + Mathf.Min(0.5f, (float)ev.dmg * 0.02f), 0.5f, 60, 2);
                case "aegisOff": return ev.why == "break" ? new E(0.7f, 0.8f, 220, 3) : ev.why == "detonate" ? new E(0.85f, 0.85f, 240, 3) : new E(0, 0.2f, 60, 1);
                case "rocketJump": return new E(Mathf.Min(1, 0.6f + 0.4f * k), Mathf.Min(1, 0.5f + 0.4f * k), 180 + 220 * k, 4);
                case "dash": return ev.level != 0 ? new E(0.2f + 0.2f * lv, 0.45f, 80 + 40 * lv, 2) : new E(0, 0.22f, 35, 1);
                case "walljump": return new E(0, 0.25f, 30, 1);
                case "wallSlide": return ev.on ? new E(0, 0.18f, 40, 1) : (E?)null;
                case "land": { if (ev.vy >= -16) return null; float q = Mathf.Min(1, (float)(-ev.vy - 16) / 12); return new E(0.3f + 0.4f * q, 0.2f, 80 + 60 * q, 2); }
                case "blast": return new E(0.35f, 0.5f, 110, 2);
                case "burst": return new E(0.2f + 0.12f * lv, 0.45f, 60 + 20 * lv, 2);
                case "vbStart": return new E(0.2f + 0.15f * (float)ev.tier, 0.5f, 80, 2);
                case "intercept": return new E(0, 0.35f, 40, 1);
                case "thrustOn": return new E(0, 0.2f, 60, 1);
                case "lockOn": return ev.why == "auto" ? (E?)null : new E(0, 0.3f, 35, 1);
                case "lockSwitch": return new E(0, 0.2f, 25, 1);
                case "lockOff": return new E(0, 0.12f, 25, 1);
                case "challenge": case "vanish": return new E(0.1f, 0.4f, 60, 2);
                // Version 9
                case "subSwitch": return new E(0, 0.2f, 30, 1);
                case "frag": return new E(0.3f + 0.1f * lv, 0.5f, 90 + 20 * lv, 2);
                case "chain": return new E(0.15f + 0.1f * lv, 0.6f, 70 + 20 * lv, 2);
                case "discThrow": return new E(0, 0.3f, 40, 1);
                case "discCatch": return new E(0.1f, 0.4f, 50, 2);
                case "wellOpen": return new E(0.2f, 0.4f, 90, 2);
                case "wellCollapse": return new E(0.4f + 0.1f * (lv != 0 ? lv : 1), 0.6f, 140, 3);
                case "dodge": return new E(0, 0.3f, 40, 1);
                case "perfectDodge": return new E(0.4f, 0.9f, 200, 3);
                case "riseBlast": return new E(0.3f, 0.6f, 90, 2);
                case "ultReady": return new E(0.2f, 0.7f, 150, 2);
                case "ultCut": return new E(0.15f, 0.5f, 40, 2);
                // Nova's absorbing shield: blocks in his bracer arm, stronger as he stores more
                case "nshieldOn": return new E(0, 0.2f, 35, 1);
                case "nshieldBlock": { float d = (float)ev.dmg, q = (float)ev.k; return ev.perfect ? new E(0.3f, 0.9f, 120, 3) : new E(Mathf.Min(0.7f, 0.15f + d * 0.025f), 0.4f + 0.3f * q, 60 + Mathf.Min(70, d * 3), 2); }
                case "nshieldBreak": return new E(0.8f, 0.8f, 220, 4);
                case "absorbSpill": return new E(0.2f, 0.3f, 90, 2);
                case "parryStun": return new E(0.1f, 0.5f, 70, 2);
                // Version 10: RAM's blocks land in the hands that hold the shield; Fix feels her tools
                case "guardOn": return new E(0, 0.25f, 40, 1);
                case "guardBlock": { float d = (float)ev.dmg; return new E(Mathf.Min(0.8f, 0.2f + d * 0.03f), 0.5f, 70 + Mathf.Min(80, d * 3), 2); }
                case "perfectGuard": return new E(0.3f, 0.9f, 120, 3);
                case "rampartBreak": return new E(0.9f, 0.9f, 260, 4);
                case "kineticRelease": { float q = (float)ev.k; return new E(0.4f + 0.5f * q, 0.7f, 140 + 120 * q, 3); }
                case "rush": return new E(0.3f + 0.15f * lv, 0.4f, 120 + 40 * lv, 2);
                case "plowCatch": return new E(0.3f, 0.4f, 60, 2);
                case "ramSplat": return new E(0.9f, 0.8f, 220, 3);
                case "ramBonk": return new E(0.6f, 0.6f, 140, 3);
                case "wallUp": return new E(0.3f, 0.5f, 110, 2);
                case "link": return new E(0.1f, 0.5f, 80, 2);
                case "linkHit": return new E(0.3f, 0.3f, 60, 2);
                case "leap": return new E(0.3f, 0.4f, 90, 2);
                case "leapLand": return new E(0.7f, 0.6f, 160, 3);
                case "provoke": return new E(0.5f, 0.6f, 180, 3);
                case "quake": return new E(0.8f, 0.7f, 200, 3);
                case "upliftBlast": return new E(0.3f, 0.6f, 90, 2);
                case "gadgetDeploy": return new E(0.15f, 0.4f, 80, 2);
                case "gadgetUp": return new E(0.1f, 0.6f, 90, 2);
                case "gadgetWrench": return new E(0.05f, 0.45f, 40, 1);
                case "padBounce": return new E(0.25f, 0.5f, 80, 2);
                case "powerUp": return new E(0.1f, 0.55f, 90, 2);
                case "rivetBlast": return new E(0.25f, 0.45f, 70, 2);
                case "sparkRing": return new E(0.2f, 0.7f, 100, 2);
                case "patchOn": return new E(0, 0.2f, 40, 1);
                case "plateHit": return new E(0.15f, 0.35f, 50, 2);
            }
            return null;
        }
        // Boss moments and ultimates everyone feels, on every pad at once
        static E? All(Ev ev)
        {
            switch (ev.type)
            {
                case "bossSlam": return ev.big ? new E(0.8f, 0.6f, 220, 3) : new E(0.5f, 0.45f, 140, 2);
                case "bossPhase": return new E(0.9f, 0.8f, 380, 4);
                case "bossCrash": return new E(0.7f, 0.5f, 200, 3);
                case "bossDown": return new E(1, 1, 700, 4);
                case "bossIntro": return new E(0.4f, 0.5f, 300, 2);
                case "ultCast": return new E(0.5f, 0.8f, 300, 4);
                case "ultJoin": return new E(0.5f, 0.8f, 250, 4);
                case "ultNova": return new E(1, 1, 520, 4);
                case "ultFinisher": return new E(0.9f, 0.9f, 420, 4);
                case "teamFinisher": return new E(1, 1, 750, 4);
                case "ramSlam": return new E(1, 1, 520, 4);
                case "podLand": return new E(0.9f, 0.8f, 380, 4);
            }
            return null;
        }

        // How far along a player's current charge is (0-1), or -1 when nothing is charging
        static double ChargeOf(Player p)
        {
            if (p.state == "dashCharge") return p.dashChargeT >= DASH_CHARGE.tap ? System.Math.Min(1, p.dashChargeT / DASH_CHARGE.charge[2]) : -1;
            if (p.state == "pound" && p.pound != null && p.pound.phase == "hold" && p.pound.held && p.pound.t > POUND.windup) return System.Math.Min(1, p.pound.t / POUND.charge[2]);
            if (p.@char == "echo") return p.rifleT >= HUNTER.rifle.raise ? PlayerSim.RifleFocus(p.rifleT) : -1;
            if (p.@char == "ram" || p.@char == "fix") return p.chargeT > 0 ? System.Math.Min(1, p.chargeT / (p.@char == "ram" ? RAM.cannon.charge[2] : FIX.rivet.charge[2])) : -1;
            if (p.chargeT > 0) return System.Math.Min(1, p.chargeT / (SETTINGS.novaKit == "marksman" ? MARKSMAN.charge[2] : NOVA.charge2));
            if (p.burstT > 0) return System.Math.Min(1, p.burstT / MARKSMAN.burst.charge[2]);
            return -1;
        }

        readonly Dictionary<string, float> until = new Dictionary<string, float>(), humT = new Dictionary<string, float>(), lastHit = new Dictionary<string, float>();
        readonly Dictionary<string, int> prio = new Dictionary<string, int>();
        readonly HashSet<string> running = new HashSet<string>();
        World world;

        static float Now => Time.unscaledTime * 1000;
        static float Get(Dictionary<string, float> d, string k, float def = 0) => d.TryGetValue(k, out var v) ? v : def;

        // Play one effect on a player's device
        public bool Play(Player p, float strong, float weak, float ms, int pr = 1)
        {
            if (!SETTINGS.haptics || p == null) return false;
            float k = (float)SETTINGS.hapticStrength;
            if (k <= 0) return false;
            string dev = p.device; float t = Now;
            if (Get(until, dev) > t && (prio.TryGetValue(dev, out var q) ? q : 0) > pr) return false;   // a stronger effect is still playing
            var pad = dev != null && dev.StartsWith("pad") ? Controls.PadOf(dev) : null;
            if (pad == null) return false;
            pad.SetMotorSpeeds(Mathf.Min(1, strong * k), Mathf.Min(1, weak * k));
            until[dev] = t + Mathf.Round(ms); prio[dev] = pr; running.Add(dev);
            return true;
        }

        public void OnEvent(Ev ev)
        {
            var a = All(ev);
            if (a is E e0 && world != null) { foreach (var p in world.players) Play(p, e0.s, e0.w, e0.ms, e0.prio); return; }
            var e = Fx(ev); if (e == null) return;
            var who = ev.type == "hit" || ev.type == "kill" || ev.type == "intercept" ? ev.owner as Player : ev.p;
            if (who == null) return;
            if (ev.type == "hit")   // hit ticks: at most one every 50 ms per player
            {
                float t = Now; if (t - Get(lastHit, who.device, -1e9f) < 50) return; lastHit[who.device] = t;
            }
            Play(who, e.Value.s, e.Value.w, e.Value.ms, e.Value.prio);
        }

        // Ends effects whose time is up; while a charge builds, a faint rumble that grows with it
        public void Update(World w, bool live)
        {
            world = w;
            float t = Now;
            foreach (var dev in new List<string>(running))
                if (Get(until, dev) <= t) { Controls.PadOf(dev)?.SetMotorSpeeds(0, 0); running.Remove(dev); }
            if (!live || !SETTINGS.haptics || w == null) return;
            foreach (var p in w.players)
            {
                if (p.state == "downed" || p.state == "dead" || p.device == null || Controls.PadOf(p.device) == null) continue;
                string d = p.device;
                if (p.state == "beam" && p.beam != null)   // the beam shakes the pad the whole time it fires
                {
                    if (t - Get(humT, d) >= 110) { humT[d] = t; Play(p, 0.35f, 0.55f, 130, 1); }
                    continue;
                }
                if (p.state == "ult" && p.ultRun != null && p.ultRun.segs != null)   // and Supernova's far harder
                {
                    if (t - Get(humT, d) >= 110) { humT[d] = t; Play(p, 0.65f, 0.85f, 130, 1); }
                    continue;
                }
                float k = (float)ChargeOf(p);
                if (k < 0.3f || t - Get(humT, d) < 110) continue;
                humT[d] = t;
                Play(p, k > 0.95f ? 0.08f : 0, 0.05f + 0.13f * k, 130, 0);
            }
        }

        // Every motor off (pausing, quitting)
        public void StopAll() { foreach (var p in Gamepad.all) p.SetMotorSpeeds(0, 0); running.Clear(); until.Clear(); }
    }

    // Settings kept between sessions (config.js loadSettings / saveSettings), in PlayerPrefs as JSON
    public static class SettingsStore
    {
        const string KEY = "novaStriker.settings";
        public static void Load()
        {
            try
            {
                string raw = PlayerPrefs.GetString(KEY, null);
                if (string.IsNullOrEmpty(raw)) return;
                var saved = new Settings { settingsVersion = 0 }; JsonUtility.FromJsonOverwrite(raw, saved);
                // Settings saved before Version 8 pick up its new default once: impact frames on
                if (!(saved.settingsVersion >= 8)) { saved.impactFrames = true; saved.settingsVersion = 8; }
                // Version 9 introduces automatic lock-on as the default
                if (!(saved.settingsVersion >= 9)) { saved.lockMode = "auto"; saved.settingsVersion = 9; }
                // Version 10 (Unity): Echo shows his face by default, as in his concept art
                if (!(saved.settingsVersion >= 10)) { saved.echoHead = "bare"; saved.settingsVersion = 10; }
                // Version 11 (Unity): Nova is drawn with his 3D model and wears his helmet, which critical health knocks off
                if (!(saved.settingsVersion >= 11)) { saved.novaHead = "helmet"; saved.charModels = "models"; saved.settingsVersion = 11; }
                string Valid(string value, string fallback, params string[] options) => System.Array.IndexOf(options, value) >= 0 ? value : fallback;
                saved.fxPreset = Valid(saved.fxPreset, "cinematic", "cinematic", "balanced", "classic", "custom");
                saved.fxExplosions = Valid(saved.fxExplosions, "volumetric", "volumetric", "plasma", "stylised", "classic");
                saved.fxProjectiles = Valid(saved.fxProjectiles, "energy", "energy", "tracer", "classic");
                saved.fxTrails = Valid(saved.fxTrails, "long", "long", "short", "off");
                saved.fxDensity = Valid(saved.fxDensity, "high", "high", "medium", "low");
                saved.fxSmoke = Valid(saved.fxSmoke, "rich", "rich", "light", "off");
                saved.fxDebris = Valid(saved.fxDebris, "physics", "physics", "simple", "off");
                saved.fxScreen = Valid(saved.fxScreen, "cinematic", "cinematic", "clean");
                SETTINGS = saved;
            }
            catch (System.Exception) { /* unreadable: keep defaults */ }
        }
        public static void Save()
        {
            try { PlayerPrefs.SetString(KEY, JsonUtility.ToJson(SETTINGS)); PlayerPrefs.Save(); } catch (System.Exception) { }
        }
    }
}
