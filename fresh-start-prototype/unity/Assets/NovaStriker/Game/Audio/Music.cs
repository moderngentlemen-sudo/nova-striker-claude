// Adaptive score (music.js), synthesized live (no audio files). A driving synthwave loop in D minor at 124 BPM:
// kick, snare and hats, a rolling bass, a delayed arpeggio, a reverb pad and a heroic lead. Three intensity levels
// crossfade: 0 explore (pad, arp, half-time beat), 1 combat (four-on-the-floor, driving bass), 2 intense (adds the
// lead). Update() picks the level from what is happening on screen.
using System.Collections.Generic;
using NovaStriker.Sim;
using static NovaStriker.Sim.Cfg;
using W = NovaStriker.Game.Audio.Wave;
using F = NovaStriker.Game.Audio.FilterType;

namespace NovaStriker.Game.Audio
{
    public sealed class Music
    {
        const double BPM = 124, STEP = 60 / BPM / 4;
        const int BAR = 16, LOOP = BAR * 8;
        static double hz(double n) => 440 * System.Math.Pow(2, (n - 69) / 12);

        // Dm  Bb  F  C | Dm  Bb  Gm  A
        static readonly (int root, char q)[] CHORDS = { (38, 'm'), (34, 'M'), (41, 'M'), (36, 'M'), (38, 'm'), (34, 'M'), (43, 'm'), (45, 'M') };
        static int[] Triad(char q) => q == 'm' ? new[] { 0, 3, 7 } : new[] { 0, 4, 7 };
        // Arpeggio walks these chord tones (root, third, fifth, then an octave up) in 16ths
        static readonly int[] ARP = { 0, 2, 3, 2, 4, 2, 3, 1, 0, 2, 3, 2, 5, 4, 3, 2 };
        // Lead melody: (step in the 8-bar loop, midi note, length in 16ths)
        static readonly (int st, int n, int len)[] LEAD =
        {
            (0, 69, 3), (3, 74, 3), (6, 77, 4), (10, 76, 2), (12, 74, 4),
            (16, 77, 6), (22, 74, 2), (24, 70, 4), (28, 74, 4),
            (32, 72, 3), (35, 77, 3), (38, 81, 6), (44, 79, 4),
            (48, 76, 6), (54, 79, 2), (56, 76, 4), (60, 72, 4),
            (64, 74, 3), (67, 77, 3), (70, 81, 4), (74, 79, 2), (76, 77, 4),
            (80, 77, 4), (84, 79, 4), (88, 77, 2), (90, 74, 2), (92, 70, 4),
            (96, 67, 3), (99, 70, 3), (102, 74, 4), (106, 79, 2), (108, 77, 4),
            (112, 76, 6), (118, 73, 2), (120, 76, 4), (124, 81, 4),
        };
        static readonly string[] LAYERS = { "drums", "bass", "arp", "pad", "lead" };
        // Layer levels per intensity
        static readonly Dictionary<string, double>[] MIX =
        {
            new Dictionary<string, double> { ["drums"] = 0.5, ["bass"] = 0.55, ["arp"] = 0.8, ["pad"] = 0.95, ["lead"] = 0 },
            new Dictionary<string, double> { ["drums"] = 1, ["bass"] = 1, ["arp"] = 0.8, ["pad"] = 0.45, ["lead"] = 0 },
            new Dictionary<string, double> { ["drums"] = 1.1, ["bass"] = 1.05, ["arp"] = 0.85, ["pad"] = 0.4, ["lead"] = 0.85 },
        };

        // How hot is the moment? Enemies near the camera, and whether a gate has the team locked in.
        public static int Intensity(World world)
        {
            if (world.ultCast != null) return 2;   // an ultimate is always full intensity
            var cam = world.cam; int near = 0; bool heavy = false;
            foreach (var e in world.enemies)
            {
                if (e.dead || e.type == "post" || e.type == "turret") continue;
                if (e.boss && System.Math.Abs(e.x - cam.x) < cam.halfW + 14) return 2;   // a boss fight is always full intensity
                if (System.Math.Abs(e.x - cam.x) < cam.halfW + 8 && System.Math.Abs(e.y - cam.y) < cam.halfH + 10)
                {
                    near++;
                    if (e.type == "brute" || e.type == "charger" || e.type == "mortar") heavy = true;
                }
            }
            if (near == 0) return 0;
            bool locked = world.arena.state == "wave1" || world.arena.state == "wave2" || world.encounters.Exists(S => S.state == "active" && S.def.gates != null && S.def.gates.Length > 0);
            return near >= 5 || (locked && (heavy || near >= 3)) ? 2 : 1;
        }

        Ctx ctx;
        public int level, notes, step;
        int want; float wantT; double vol = -1, next;
        Gain @out, duckBass, duckPad;
        Reverb verb; Delay delay;
        readonly Dictionary<string, Gain> bus = new Dictionary<string, Gain>();
        readonly System.Random rng = new System.Random();

        public void Start(Ctx c)
        {
            if (ctx != null || c == null) return;
            ctx = c;
            lock (c.gate)
            {
                @out = c.createGain(0); @out.keep = true;
                var comp = c.createDynamicsCompressor();
                comp.threshold = -16; comp.ratio = 3.5; comp.attack = 0.01; comp.release = 0.2;
                @out.connect(comp); comp.connect(c.destination);
                // Reverb and a tempo-synced delay, shared by the melodic layers
                verb = c.createReverb(2.6);
                var verbOut = c.createGain(0.32); verbOut.keep = true; verb.connect(verbOut); verbOut.connect(@out);
                delay = c.createDelay(1); delay.delayTime = STEP * 3;
                var fb = c.createGain(0.34); fb.keep = true; var damp = c.createBiquadFilter(); damp.keep = true; damp.type = F.Lowpass; damp.frequency.value = 2600;
                delay.connect(damp); damp.connect(fb); fb.connect(delay);
                var delayOut = c.createGain(0.3); delayOut.keep = true; damp.connect(delayOut); delayOut.connect(@out);
                // One bus per layer; bass and pad also duck under the kick
                foreach (var k in LAYERS) { var g = c.createGain(MIX[0][k]); g.keep = true; g.connect(@out); bus[k] = g; }
                duckBass = c.createGain(); duckBass.keep = true; duckPad = c.createGain(); duckPad.keep = true;
                duckBass.connect(bus["bass"]); duckPad.connect(bus["pad"]);
                step = 0; next = c.currentTime + 0.12;
            }
        }

        // Schedule everything that starts in the next 150 ms (called every frame). After a stall, skip ahead
        // instead of playing the backlog all at once.
        void Pump()
        {
            double now = ctx.currentTime;
            if (next < now - 0.05) next = now + 0.05;
            while (next < now + 0.15)
            {
                Play(step, next);
                next += STEP; step = (step + 1) % LOOP;
            }
        }

        public void Update(float dt, World world, bool paused)
        {
            if (ctx == null) return;
            lock (ctx.gate)
            {
                Pump();
                double t = ctx.currentTime;
                // Volume and pause duck
                double v = SETTINGS.music * 0.55 * (paused ? 0.35 : 1);
                if (v != vol) { vol = v; @out.gain.setTargetAtTime(v, t, 0.25); }
                // Intensity: rises quickly, settles slowly, so the music doesn't flap between states
                int w = world != null && world.players.Count > 0 ? Intensity(world) : 0;
                if (w != want) { want = w; wantT = 0; } else wantT += dt;
                if (w != level && wantT > (w > level ? 0.4f : 4))
                {
                    level = w;
                    foreach (var k in LAYERS) bus[k].gain.setTargetAtTime(MIX[w][k], t, 0.7);
                }
            }
        }

        // ---- The arrangement ----
        void Play(int st, double t)
        {
            int L = level, s = st % BAR, bar = st / BAR; var (root, q) = CHORDS[bar];
            var tones = Triad(q);
            bool fill = bar == 7 && s >= 12;
            // Drums
            if (L == 0)
            {
                if (s == 0 || s == 10) Kick(t, 0.55);
                if (s % 4 == 2) Hat(t, 0.1, false);
                if (s == 12) Rim(t, 0.12);
            }
            else
            {
                if (s % 4 == 0 || (L == 2 && s == 10)) Kick(t, 0.9);
                if ((s == 4 || s == 12) && !fill) Snare(t, 0.55);
                if (fill) Snare(t, 0.25 + (s - 12) * 0.1);
                Hat(t, s % 4 == 2 ? 0.16 : 0.07, s == 14 && bar % 2 == 1);
                if (st == 0 && L == 2) Crash(t);
            }
            // Bass
            if (L == 0) { if (s == 0) Bass(t, root, STEP * 14, 0.28, 500); }
            else if (s % 2 == 0)
            {
                int oct = s == 6 || s == 14 ? 12 : 0, fifth = s == 10 && bar % 2 == 1 ? 7 : 0;
                Bass(t, root + oct + fifth, STEP * 1.6, 0.32, L == 2 ? 1500 : 1100);
            }
            // Arpeggio: chord tones placed around D4
            int @base = root + 12 * (int)System.Math.Round((62 - root) / 12.0, System.MidpointRounding.AwayFromZero);
            var ladder = new[] { 0, tones[1], tones[2], 12, 12 + tones[1], 12 + tones[2] };
            if (L > 0 || s % 2 == 0) Arp(t, @base + ladder[ARP[s]], L == 0 ? 0.22 : 0.26);
            // Pad: the chord, held for the bar
            if (s == 0) Pad(t, new[] { root + 12, root + 12 + tones[1], root + 12 + tones[2], root + 24 }, STEP * BAR);
            // Lead (heard only at intensity 2, but always scheduled so it enters mid-phrase)
            foreach (var (ls, n, len) in LEAD) if (ls == st) Lead(t, n, STEP * len);
        }

        // ---- Instruments ----
        static void Env(Gain g, double t, double a, double peak, double dur, double rel)
        {
            g.gain.setValueAtTime(0.0001, t);
            g.gain.exponentialRampToValueAtTime(peak, t + a);
            g.gain.setTargetAtTime(0.0001, t + a + dur, rel);
        }
        Noise NoiseSrc(double t, double dur) { var s = ctx.createNoise(); s.start(t); s.stop(t + dur); return s; }

        void Kick(double t, double v)
        {
            var c = ctx; var o = c.createOscillator(); var g = c.createGain();
            o.frequency.setValueAtTime(155, t); o.frequency.exponentialRampToValueAtTime(42, t + 0.13);
            g.gain.setValueAtTime(v, t); g.gain.exponentialRampToValueAtTime(0.001, t + 0.38);
            o.connect(g); g.connect(bus["drums"]); o.start(t); o.stop(t + 0.4);
            // Sidechain: bass and pad dip under the kick, the glue of the genre
            foreach (var d in new[] { duckBass, duckPad })
            {
                d.gain.cancelScheduledValues(t); d.gain.setValueAtTime(0.35, t); d.gain.setTargetAtTime(1, t + 0.02, 0.09);
            }
            notes++;
        }
        void Snare(double t, double v)
        {
            var c = ctx; var n = NoiseSrc(t, 0.25); var f = c.createBiquadFilter(); var g = c.createGain();
            f.type = F.Bandpass; f.frequency.value = 1900; f.Q.value = 0.7;
            g.gain.setValueAtTime(v, t); g.gain.exponentialRampToValueAtTime(0.001, t + 0.2);
            n.connect(f); f.connect(g); g.connect(bus["drums"]);
            var o = c.createOscillator(); var og = c.createGain(); o.type = W.Triangle;
            o.frequency.setValueAtTime(210, t); o.frequency.exponentialRampToValueAtTime(150, t + 0.08);
            og.gain.setValueAtTime(v * 0.6, t); og.gain.exponentialRampToValueAtTime(0.001, t + 0.1);
            o.connect(og); og.connect(bus["drums"]); o.start(t); o.stop(t + 0.12);
            var send = c.createGain(0.25); g.connect(send); send.connect(verb);
            notes++;
        }
        void Hat(double t, double v, bool open)
        {
            var c = ctx; double dur = open ? 0.24 : 0.045; var n = NoiseSrc(t, dur + 0.02); var f = c.createBiquadFilter(); var g = c.createGain();
            f.type = F.Highpass; f.frequency.value = 7200;
            g.gain.setValueAtTime(v, t); g.gain.exponentialRampToValueAtTime(0.001, t + dur);
            n.connect(f); f.connect(g); g.connect(bus["drums"]);
            notes++;
        }
        void Rim(double t, double v)
        {
            var c = ctx; var o = c.createOscillator(); var g = c.createGain(); o.type = W.Square; o.frequency.value = 820;
            g.gain.setValueAtTime(v, t); g.gain.exponentialRampToValueAtTime(0.001, t + 0.04);
            o.connect(g); g.connect(bus["drums"]); o.start(t); o.stop(t + 0.05);
            notes++;
        }
        void Crash(double t)
        {
            var c = ctx; var n = NoiseSrc(t, 1.6); var f = c.createBiquadFilter(); var g = c.createGain();
            f.type = F.Highpass; f.frequency.value = 4500;
            g.gain.setValueAtTime(0.16, t); g.gain.exponentialRampToValueAtTime(0.001, t + 1.5);
            n.connect(f); f.connect(g); g.connect(bus["drums"]);
            var send = c.createGain(0.4); g.connect(send); send.connect(verb);
            notes++;
        }
        void Bass(double t, int note, double dur, double v, double cutoff)
        {
            var c = ctx; var f = c.createBiquadFilter(); var g = c.createGain();
            f.type = F.Lowpass; f.Q.value = 6;
            f.frequency.setValueAtTime(cutoff * 1.8, t); f.frequency.setTargetAtTime(cutoff * 0.35, t + 0.01, 0.07);
            foreach (var (type, n, lvl) in new[] { (W.Sawtooth, note, 1.0), (W.Square, note - 12, 0.55) })
            {
                var o = c.createOscillator(); o.type = type; o.frequency.value = hz(n);
                var og = c.createGain(lvl); o.connect(og); og.connect(f); o.start(t); o.stop(t + dur + 0.3);
            }
            Env(g, t, 0.008, v, dur, 0.05);
            f.connect(g); g.connect(duckBass);
            notes++;
        }
        void Arp(double t, int note, double v)
        {
            var c = ctx; var o = c.createOscillator(); var f = c.createBiquadFilter(); var g = c.createGain();
            o.type = W.Square; o.frequency.value = hz(note);
            f.type = F.Lowpass; f.frequency.setValueAtTime(4200, t); f.frequency.setTargetAtTime(900, t + 0.01, 0.06);
            g.gain.setValueAtTime(0.0001, t); g.gain.exponentialRampToValueAtTime(v, t + 0.004); g.gain.setTargetAtTime(0.0001, t + 0.03, 0.05);
            o.connect(f); f.connect(g); g.connect(bus["arp"]); o.start(t); o.stop(t + 0.4);
            var send = c.createGain(0.55); g.connect(send); send.connect(delay);
            notes++;
        }
        void Pad(double t, int[] chord, double dur)
        {
            var c = ctx; var f = c.createBiquadFilter(); var g = c.createGain();
            f.type = F.Lowpass; f.frequency.setValueAtTime(700, t); f.frequency.linearRampToValueAtTime(1500, t + dur * 0.6);
            foreach (var n in chord) foreach (var det in new[] { -9, 9 })
            {
                var o = c.createOscillator(); o.type = W.Sawtooth; o.frequency.value = hz(n); o.detune.value = det;
                o.connect(f); o.start(t); o.stop(t + dur + 1.2);
            }
            g.gain.setValueAtTime(0.0001, t); g.gain.linearRampToValueAtTime(0.13, t + 0.5); g.gain.setTargetAtTime(0.0001, t + dur - 0.1, 0.45);
            f.connect(g); g.connect(duckPad);
            var send = c.createGain(0.7); g.connect(send); send.connect(verb);
            notes++;
        }
        void Lead(double t, int note, double dur)
        {
            var c = ctx; var f = c.createBiquadFilter(); var g = c.createGain(); var lfo = c.createOscillator(); var depth = c.createGain();
            f.type = F.Lowpass; f.frequency.value = 3200; f.Q.value = 2;
            lfo.frequency.value = 5.6; depth.gain.setValueAtTime(0, t); depth.gain.linearRampToValueAtTime(9, t + 0.25); lfo.connect(depth);
            foreach (var (type, det, lvl) in new[] { (W.Sawtooth, -6.0, 0.6), (W.Square, 6.0, 0.4) })
            {
                var o = c.createOscillator(); o.type = type; o.frequency.value = hz(note); o.detune.value = det;
                depth.connect(o.detune);
                var og = c.createGain(lvl); o.connect(og); og.connect(f); o.start(t); o.stop(t + dur + 0.4);
            }
            lfo.start(t); lfo.stop(t + dur + 0.4);
            Env(g, t, 0.02, 0.4, dur * 0.85, 0.08);
            f.connect(g); g.connect(bus["lead"]);
            var send = c.createGain(0.45); g.connect(send); send.connect(delay);
            var vs = c.createGain(0.35); g.connect(vs); vs.connect(verb);
            notes++;
        }
    }
}
