// Synthesized placeholder audio (audio.js). Timing cues matter more than fidelity here: every threat
// category and both parry grades have a distinct, unmistakable sound. Built on the Web Audio work-alike
// (WebAudio.cs), call for call.
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NovaStriker.Sim;
using static NovaStriker.Sim.Cfg;
using W = NovaStriker.Game.Audio.Wave;
using F = NovaStriker.Game.Audio.FilterType;

namespace NovaStriker.Game.Audio
{
    public sealed class Sound
    {
        // The pitch each secondary weapon hums at while it charges (and chimes at when selected)
        static double SubHum(string s) => s == "scatter" ? 130 : s == "grenade" ? 110 : s == "chain" ? 260 : s == "disc" ? 180 : s == "well" ? 70 : 0;
        static double Or(double v, double d) => v != 0 ? v : d;
        static double Pick(double[] a, double i, double d) { int k = (int)i; return k >= 0 && k < a.Length && a[k] != 0 ? a[k] : d; }
        readonly System.Random rng = new System.Random();
        double R() => rng.NextDouble();

        struct Charge { public double k, l4; public string kind, sub; public bool perfect; }
        // How far along a player's current charge is (0-1), or -1 when nothing is charging. `perfect` marks the
        // Perfect Release window.
        static Charge ChargeOf(Player p)
        {
            var none = new Charge { k = -1 };
            if (p.state == "dashCharge" && p.dashChargeT >= DASH_CHARGE.tap) return new Charge { k = System.Math.Min(1, p.dashChargeT / DASH_CHARGE.charge[2]), kind = "dash" };
            if (p.state == "pound" && p.pound != null && p.pound.phase == "hold" && p.pound.held && p.pound.t > POUND.windup) return new Charge { k = System.Math.Min(1, p.pound.t / POUND.charge[2]), kind = "pound" };
            if (p.@char == "echo") return p.rifleT >= HUNTER.rifle.raise ? new Charge { k = PlayerSim.RifleFocus(p.rifleT), kind = "rifle" } : none;
            if (p.@char == "ram") return p.chargeT > 0 ? new Charge { k = System.Math.Min(1, p.chargeT / RAM.cannon.charge[2]), kind = "cannon" } : none;
            if (p.@char == "fix") return p.chargeT > 0 ? new Charge { k = System.Math.Min(1, p.chargeT / FIX.rivet.charge[2]), kind = "rivet" } : none;
            if (PlayerSim.marksman(p))
            {
                var C = MARKSMAN.charge; var B = MARKSMAN.burst.charge; double L4 = MARKSMAN.beam.at;
                if (p.chargeT > 0) return new Charge { k = System.Math.Min(1, p.chargeT / C[2]), kind = "shot", perfect = p.chargeT >= C[2] && p.chargeT < C[2] + MARKSMAN.perfectWindow,
                    l4 = p.chargeT > C[2] ? System.Math.Min(1, (p.chargeT - C[2]) / (L4 - C[2])) : 0 };
                if (p.burstT > 0) return new Charge { k = System.Math.Min(1, p.burstT / B[2]), kind = "burst", sub = p.sub, perfect = p.burstT >= B[2] && p.burstT < B[2] + MARKSMAN.burst.perfectWindow };
                return none;
            }
            return p.chargeT > 0 ? new Charge { k = System.Math.Min(1, p.chargeT / NOVA.charge2), kind = "shot" } : none;
        }

        public Ctx ctx;
        Gain master;
        readonly Dictionary<string, double> last = new Dictionary<string, double>();
        static double Vol => SETTINGS.volume;

        // (the prototype waits for a click before audio may start; Unity has no such rule, so this runs at once)
        public void Unlock(Ctx c)
        {
            if (ctx != null) return;
            ctx = c;
            // A gentle compressor keeps stacked blasts (rocket jump + splash + hits) from clipping
            var comp = c.createDynamicsCompressor();
            comp.threshold = -12; comp.knee = 8; comp.ratio = 5; comp.attack = 0.003; comp.release = 0.2;
            master = c.createGain(); master.keep = true; master.connect(comp); comp.connect(c.destination);
        }

        void tone(double f, double f2, double dur, W type = W.Sine, double gain = 0.1, double delay = 0)
        {
            if (ctx == null) return;
            lock (ctx.gate)
            {
                var c = ctx; double t0 = c.currentTime + delay; var o = c.createOscillator(); var g = c.createGain();
                o.type = type; o.frequency.setValueAtTime(f, t0);
                if (f2 != 0) o.frequency.exponentialRampToValueAtTime(System.Math.Max(20, f2), t0 + dur);
                g.gain.setValueAtTime(0.0001, t0); g.gain.exponentialRampToValueAtTime(gain * Vol, t0 + 0.006);
                g.gain.exponentialRampToValueAtTime(0.0001, t0 + dur);
                o.connect(g); g.connect(master); o.start(t0); o.stop(t0 + dur + 0.02);
            }
        }

        void noise(double dur, double freq, double gain = 0.1, F type = F.Bandpass, double f2 = 0, double delay = 0)
        {
            if (ctx == null) return;
            lock (ctx.gate)
            {
                var c = ctx; double t0 = c.currentTime + delay; var s = c.createNoise(); var fl = c.createBiquadFilter(); var g = c.createGain();
                fl.type = type; fl.frequency.setValueAtTime(freq, t0);
                if (f2 != 0) fl.frequency.exponentialRampToValueAtTime(f2, t0 + dur);
                g.gain.setValueAtTime(gain * Vol, t0); g.gain.exponentialRampToValueAtTime(0.0001, t0 + dur);
                s.connect(fl); fl.connect(g); g.connect(master); s.start(t0); s.stop(t0 + dur + 0.02);
            }
        }

        void rising(double dur)
        {
            if (ctx == null) return;
            lock (ctx.gate)
            {
                var c = ctx; double t0 = c.currentTime; var o = c.createOscillator(); var g = c.createGain(); var lfo = c.createOscillator(); var lg = c.createGain();
                o.type = W.Sawtooth; o.frequency.setValueAtTime(180, t0); o.frequency.exponentialRampToValueAtTime(900, t0 + dur);
                lfo.frequency.value = 14; lg.gain.value = 0.03 * Vol; lfo.connect(lg); lg.connect(g.gain);
                g.gain.setValueAtTime(0.0001, t0); g.gain.exponentialRampToValueAtTime(0.06 * Vol, t0 + 0.05);
                g.gain.setValueAtTime(0.06 * Vol, t0 + dur - 0.02); g.gain.exponentialRampToValueAtTime(0.0001, t0 + dur + 0.05);
                o.connect(g); g.connect(master); o.start(t0); lfo.start(t0); o.stop(t0 + dur + 0.08); lfo.stop(t0 + dur + 0.08);
            }
        }

        // A looping voice: up to two oscillators (through a low-pass, or straight), a filtered noise, and an LFO on
        // its level
        sealed class Voice { public Oscillator o1, o2, lfo; public Noise n; public Gain g, lg; }
        void StopVoice(Voice v, double fade)
        {
            double now = ctx.currentTime; v.g.gain.setTargetAtTime(0.0001, now, fade);
            foreach (var s in new Source[] { v.o1, v.o2, v.lfo, v.n }) s?.stop(now + 0.3);
        }

        // Light-booster hiss: one looping noise voice per player while the boosters fire
        readonly Dictionary<Player, Voice> jets = new Dictionary<Player, Voice>();
        public void jet(Player p, bool on)
        {
            if (ctx == null || p == null) return;
            lock (ctx.gate)
            {
                jets.TryGetValue(p, out var cur);
                if (!on) { if (cur != null) { double t = ctx.currentTime; cur.g.gain.setTargetAtTime(0.0001, t, 0.04); cur.n.stop(t + 0.3); jets.Remove(p); } return; }
                if (cur != null || Vol <= 0) return;
                var c = ctx; var s = c.createNoise(); var f = c.createBiquadFilter(); var g = c.createGain();
                f.type = F.Bandpass; f.frequency.value = 2400; f.Q.value = 0.9;
                g.gain.setValueAtTime(0.0001, c.currentTime); g.gain.exponentialRampToValueAtTime(0.07 * Vol, c.currentTime + 0.05);
                s.connect(f); f.connect(g); g.connect(master); s.start(c.currentTime);
                jets[p] = new Voice { n = s, g = g };
            }
        }

        // Continuous voices, retuned every frame: a hum while a charge builds (it climbs in pitch and flutters in
        // the Perfect Release window) and the grind of a wall slide (Nova's skate blades hiss).
        readonly Dictionary<Player, Voice> hums = new Dictionary<Player, Voice>(), grinds = new Dictionary<Player, Voice>();
        public void Update(World world)
        {
            if (ctx == null) return;
            lock (ctx.gate)
            {
                var c = ctx; double now = c.currentTime, vol = Vol; var seen = new HashSet<Player>();
                var players = world != null ? world.players : new List<Player>();
                foreach (var p in players)
                {
                    seen.Add(p);
                    var ch = p.state == "downed" || p.state == "dead" ? new Charge { k = -1 } : ChargeOf(p);
                    hums.TryGetValue(p, out var h);
                    if (ch.k >= 0 && vol > 0)
                    {
                        if (h == null)
                        {
                            var o1 = c.createOscillator(); var o2 = c.createOscillator(); var lfo = c.createOscillator(); var lg = c.createGain(); var g = c.createGain(); var f = c.createBiquadFilter();
                            o1.type = W.Sine; o2.type = W.Triangle; lfo.type = W.Sine; f.type = F.Lowpass; f.frequency.value = 2400;
                            lfo.connect(lg); lg.connect(g.gain); o1.connect(f); o2.connect(f); f.connect(g); g.connect(master);
                            g.gain.setValueAtTime(0.0001, now); o1.start(now); o2.start(now); lfo.start(now);
                            h = new Voice { o1 = o1, o2 = o2, lfo = lfo, lg = lg, g = g }; hums[p] = h;
                        }
                        double @base = ch.kind == "dash" ? (p.@char == "ram" ? 55 : 90) : ch.kind == "rifle" ? 240 : ch.kind == "burst" ? Or(SubHum(ch.sub), 130) : ch.kind == "pound" ? 70
                            : ch.kind == "cannon" ? 62 : ch.kind == "rivet" ? 300 : 110;
                        double l4 = ch.l4, f0 = @base * (1 + 2.2 * ch.k) * (ch.perfect ? 2 : 1) * (1 + 0.6 * l4);
                        h.o1.frequency.setTargetAtTime(f0, now, 0.03); h.o2.frequency.setTargetAtTime(f0 * (l4 >= 1 ? 2 : 1.5), now, 0.03);
                        h.lfo.frequency.setTargetAtTime(ch.perfect || l4 >= 1 ? 26 : 5 + 10 * ch.k + 10 * l4, now, 0.05);
                        double gain = (0.012 + 0.03 * ch.k) * vol * (ch.kind == "rifle" ? 0.6 : 1);
                        h.g.gain.setTargetAtTime(gain, now, 0.04); h.lg.gain.setTargetAtTime(gain * (ch.perfect ? 0.8 : 0.3), now, 0.04);
                    }
                    else if (h != null) StopHum(p);
                    // Wall slide grind
                    grinds.TryGetValue(p, out var w);
                    if (p.wallSliding && vol > 0)
                    {
                        if (w == null)
                        {
                            var src = c.createNoise(); var f = c.createBiquadFilter(); var g = c.createGain();
                            f.type = F.Bandpass;
                            f.frequency.value = p.@char == "nova" && SETTINGS.novaKit == "marksman" ? 3600 : 1100; f.Q.value = p.@char == "nova" ? 1.6 : 0.8;
                            g.gain.setValueAtTime(0.0001, now); src.connect(f); f.connect(g); g.connect(master); src.start(now);
                            w = new Voice { n = src, g = g }; grinds[p] = w;
                        }
                        w.g.gain.setTargetAtTime((0.015 + 0.035 * System.Math.Min(1, -p.vy / 6)) * vol, now, 0.05);
                    }
                    else if (w != null) { w.g.gain.setTargetAtTime(0.0001, now, 0.04); w.n.stop(now + 0.25); grinds.Remove(p); }
                }
                foreach (var p in hums.Keys.ToList()) if (!seen.Contains(p)) StopHum(p);
                foreach (var p in grinds.Keys.ToList()) if (!seen.Contains(p)) { StopVoice(grinds[p], 0.04); grinds.Remove(p); }
                Loops(world, players, now, vol);
            }
        }

        // Looping voices: the Level 4 beam's roar (a detuned buzz and rushing noise, wobbling), the Aegis' glassy
        // hum (it drops in pitch and starts to waver as the shield weakens), a gravity well's drone, Fix's Patch
        // Beam and RAM's charges
        readonly Dictionary<Player, Voice> beamV = new Dictionary<Player, Voice>(), aegisV = new Dictionary<Player, Voice>(), patchV = new Dictionary<Player, Voice>(), rushV = new Dictionary<Player, Voice>();
        readonly Dictionary<Well, Voice> wellV = new Dictionary<Well, Voice>();
        void Loops(World world, List<Player> players, double now, double vol)
        {
            var c = ctx;
            var want = new HashSet<Player>(); var wantA = new HashSet<Player>(); var wantW = new HashSet<Well>();
            // Each open gravity well: a deep pulsing drone that quickens as it nears its collapse
            if (world != null) foreach (var w in world.wells)
            {
                if (w.phase != "open" || vol <= 0) continue;
                wantW.Add(w);
                if (!wellV.TryGetValue(w, out var v))
                {
                    var o1 = c.createOscillator(); var o2 = c.createOscillator(); var g = c.createGain(); var lfo = c.createOscillator(); var lg = c.createGain();
                    o1.type = W.Sine; o2.type = W.Triangle; o1.frequency.value = 52; o2.frequency.value = 104; lfo.frequency.value = 5; lg.gain.value = 0.02 * vol;
                    lfo.connect(lg); lg.connect(g.gain); o1.connect(g); o2.connect(g); g.connect(master);
                    g.gain.setValueAtTime(0.0001, now); g.gain.exponentialRampToValueAtTime(0.06 * vol, now + 0.1); o1.start(now); o2.start(now); lfo.start(now);
                    v = new Voice { o1 = o1, o2 = o2, lfo = lfo, g = g }; wellV[w] = v;
                }
                double left = System.Math.Max(0, w.life - w.t) / w.life;
                v.lfo.frequency.setTargetAtTime(5 + 14 * (1 - left), now, 0.1); v.o2.frequency.setTargetAtTime(104 + 40 * (1 - left), now, 0.1);
            }
            foreach (var p in players)
            {
                // Supernova: the Level 4 roar, an octave down, far louder and wider
                if (p.state == "ult" && p.ultRun != null && p.ultRun.segs != null && vol > 0)
                {
                    want.Add(p);
                    if (!beamV.TryGetValue(p, out var v)) v = BeamVoice(p, now, vol, 46, 69, 0.13, beamV);
                    double k = (p.ultRun.t - ULT.nova.gather) / ULT.nova.beam;
                    v.o1.frequency.setTargetAtTime(44 + 18 * k, now, 0.05); v.o2.frequency.setTargetAtTime(66 + 26 * k, now, 0.05);
                    continue;
                }
                if (p.state == "beam" && p.beam != null && vol > 0)
                {
                    want.Add(p);
                    if (!beamV.TryGetValue(p, out var v)) v = BeamVoice(p, now, vol, 92, 139, 0.07, beamV);
                    double k = p.beam.t / MARKSMAN.beam.ticks;
                    v.o1.frequency.setTargetAtTime(80 + 30 * k, now, 0.05); v.o2.frequency.setTargetAtTime(120 + 45 * k, now, 0.05);
                }
                if (p.aegis != null && vol > 0 && p.state != "dead" && p.state != "downed")
                {
                    wantA.Add(p);
                    if (!aegisV.TryGetValue(p, out var a))
                    {
                        var o1 = c.createOscillator(); var o2 = c.createOscillator(); var g = c.createGain(); var lfo = c.createOscillator(); var lg = c.createGain();
                        o1.type = W.Sine; o2.type = W.Triangle; lfo.frequency.value = 3; lg.gain.value = 0.004 * vol; lfo.connect(lg); lg.connect(g.gain);
                        o1.connect(g); o2.connect(g); g.connect(master); g.gain.setValueAtTime(0.0001, now); g.gain.exponentialRampToValueAtTime(0.014 * vol, now + 0.1);
                        o1.start(now); o2.start(now); lfo.start(now); a = new Voice { o1 = o1, o2 = o2, lfo = lfo, g = g }; aegisV[p] = a;
                    }
                    double frac = p.aegis.hp / p.aegis.max;
                    a.o1.frequency.setTargetAtTime(420 + 240 * frac, now, 0.08); a.o2.frequency.setTargetAtTime(632 + 360 * frac, now, 0.08);
                    a.lfo.frequency.setTargetAtTime(frac < 0.35 ? 13 : 3, now, 0.1);
                }
            }
            // Fix's Patch Beam: a bright buzzing hum with a crackle in it; RAM's charges: an engine's roar
            var wantP = new HashSet<Player>(); var wantR = new HashSet<Player>();
            foreach (var p in players)
            {
                if (p.state == "patch" && p.patch != null && vol > 0)
                {
                    wantP.Add(p);
                    if (!patchV.TryGetValue(p, out var v))
                    {
                        var o1 = c.createOscillator(); var o2 = c.createOscillator(); var n = c.createNoise(); var nf = c.createBiquadFilter(); var g = c.createGain(); var lfo = c.createOscillator(); var lg = c.createGain();
                        o1.type = W.Sine; o2.type = W.Triangle; o1.frequency.value = 520; o2.frequency.value = 780; nf.type = F.Highpass; nf.frequency.value = 4000;
                        lfo.frequency.value = 17; lg.gain.value = 0.006 * vol; lfo.connect(lg); lg.connect(g.gain);
                        o1.connect(g); o2.connect(g); n.connect(nf); nf.connect(g); g.connect(master);
                        g.gain.setValueAtTime(0.0001, now); g.gain.exponentialRampToValueAtTime(0.018 * vol, now + 0.08); o1.start(now); o2.start(now); n.start(now); lfo.start(now);
                        v = new Voice { o1 = o1, o2 = o2, n = n, lfo = lfo, g = g }; patchV[p] = v;
                    }
                    bool self = p.patch.self || p.patch.target == null, down = p.patch.target != null && p.patch.target.state == "downed";
                    v.o1.frequency.setTargetAtTime(self ? 380 : down ? 660 : 520, now, 0.08); v.o2.frequency.setTargetAtTime(self ? 570 : down ? 990 : 780, now, 0.08);
                }
                bool rushing = (p.state == "rush" && p.rush != null) || (p.state == "ult" && p.ultRun != null && p.ultRun.kind == "ram" && p.ultRun.t > ULT.ram.brace && p.ultRun.slamT == 0);
                if (rushing && vol > 0)
                {
                    wantR.Add(p);
                    if (!rushV.TryGetValue(p, out var v)) v = BeamVoice(p, now, vol, 38, 57, 0.06, rushV);
                    double L = p.rush != null ? p.rush.level : 3;
                    v.o1.frequency.setTargetAtTime(38 + 6 * L, now, 0.05); v.o2.frequency.setTargetAtTime(57 + 9 * L, now, 0.05);
                }
            }
            void Stop<TK>(Dictionary<TK, Voice> map, HashSet<TK> keep, double fade) { foreach (var k in map.Keys.ToList()) if (!keep.Contains(k)) { StopVoice(map[k], fade); map.Remove(k); } }
            Stop(patchV, wantP, 0.04); Stop(rushV, wantR, 0.08); Stop(beamV, want, 0.06); Stop(aegisV, wantA, 0.03); Stop(wellV, wantW, 0.05);
        }
        // The beam's roar: a detuned buzz and rushing noise, wobbling
        Voice BeamVoice(Player p, double now, double vol, double f1, double f2, double gain, Dictionary<Player, Voice> into)
        {
            var c = ctx; var o1 = c.createOscillator(); var o2 = c.createOscillator(); var n = c.createNoise(); var f = c.createBiquadFilter(); var nf = c.createBiquadFilter(); var g = c.createGain(); var lfo = c.createOscillator(); var lg = c.createGain();
            o1.type = W.Sawtooth; o2.type = W.Square; o1.frequency.value = f1; o2.frequency.value = f2;
            f.type = F.Lowpass; f.frequency.value = 1400; nf.type = F.Bandpass; nf.frequency.value = 2600; nf.Q.value = 0.7;
            lfo.frequency.value = 11; lg.gain.value = gain * 0.28 * vol; lfo.connect(lg); lg.connect(g.gain);
            o1.connect(f); o2.connect(f); n.connect(nf); nf.connect(g); f.connect(g); g.connect(master);
            g.gain.setValueAtTime(0.0001, now); g.gain.exponentialRampToValueAtTime(gain * vol, now + 0.06);
            o1.start(now); o2.start(now); n.start(now); lfo.start(now);
            var v = new Voice { o1 = o1, o2 = o2, n = n, lfo = lfo, g = g }; into[p] = v; return v;
        }
        void StopHum(Player p)
        {
            if (!hums.TryGetValue(p, out var h)) return;
            double t = ctx.currentTime; h.g.gain.setTargetAtTime(0.0001, t, 0.03);
            foreach (var o in new[] { h.o1, h.o2, h.lfo }) o.stop(t + 0.2);
            hums.Remove(p);
        }

        bool limit(string key, double gap)
        {
            double now = ctx != null ? ctx.currentTime : 0;
            if (last.TryGetValue(key, out var l) && now - l < gap) return false;
            last[key] = now; return true;
        }

        public void Play(Ev ev)
        {
            if (ctx == null || SETTINGS.volume <= 0) return;
            switch (ev.type) {
              case "jump": if (limit("jump", 0.05)) tone(280, 460, 0.09, W.Triangle, 0.06); break;
              case "djump": tone(380, 640, 0.09, W.Triangle, 0.06); break;
              case "walljump":
                if (ev.climb) { noise(0.05, 1800, 0.06); tone(360, 560, 0.07, W.Triangle, 0.05); }
                else { noise(0.08, 1400, 0.08); tone(300, 620, 0.1, W.Triangle, 0.055); tone(130, 70, 0.08, W.Sine, 0.08); }
                break;
              case "wallSlide": if (ev.on && limit("wallon", 0.1)) noise(0.06, 2400, 0.05, F.Bandpass, 900); break;
              case "dash": {
                var L = ev.level;
                if (L == 0) { noise(0.16, 700, 0.12, F.Bandpass, 2600); break; }
                noise(0.24 + 0.05 * L, 500, 0.14 + 0.04 * L, F.Bandpass, 3000 + 600 * L);
                tone(170 - 20 * L, 60, 0.25, W.Sawtooth, 0.04 + 0.02 * L);
                if (L >= 3) { tone(75, 34, 0.32, W.Sine, 0.22); noise(0.08, 4200, 0.1, F.Highpass); }
                break;
              }
              case "dashChargeStart": tone(220, 330, 0.1, W.Triangle, 0.025); break;
              case "dashLevel": tone(Pick(new double[] { 0, 523, 659, 880 }, ev.level, 659), 0, 0.1, W.Triangle, 0.06); if (ev.level == 3) tone(1320, 0, 0.1, W.Sine, 0.04, 0.05); break;
              case "rifleRaise": tone(1800, 0, 0.02, W.Square, 0.03); tone(1200, 0, 0.03, W.Square, 0.025, 0.04); break;
              case "rifleFocus": tone(1760, 0, 0.18, W.Sine, 0.06); tone(2637, 0, 0.12, W.Sine, 0.035, 0.03); break;
              case "rifleLower": tone(900, 0, 0.03, W.Square, 0.02); break;
              case "snipe": {
                // A sharp crack, a deep report, the supersonic whip down the line, a rolling tail; then the bolt cycles
                var f = ev.f;
                noise(0.05, 5200, 0.2 + 0.1 * f, F.Highpass); tone(95 + 30 * f, 38, 0.3 + 0.15 * f, W.Sine, 0.16 + 0.1 * f);
                tone(900, 180, 0.09, W.Square, 0.05); noise(0.25, 3200, 0.08 + 0.05 * f, F.Bandpass, 700, 0.01);
                noise(0.5 + 0.3 * f, 900, 0.1 + 0.05 * f, F.Lowpass, 150, 0.02);
                if (ev.full) { tone(2400, 1100, 0.3, W.Sine, 0.045); tone(260, 70, 0.32, W.Sawtooth, 0.06); }
                tone(1900, 0, 0.025, W.Square, 0.035, 0.34); noise(0.03, 3000, 0.06, F.Bandpass, 0, 0.34);
                tone(1500, 0, 0.03, W.Square, 0.035, 0.5); noise(0.04, 2400, 0.07, F.Bandpass, 0, 0.5);
                break;
              }
              case "crit": tone(2637, 0, 0.1, W.Sine, 0.06); tone(3520, 0, 0.08, W.Square, 0.025, 0.02); break;
              case "deflect":
                // Staff meets shot: a bright metallic ting (brighter and ringing on a perfect)
                tone(2350, 0, ev.perfect ? 0.3 : 0.14, W.Sine, 0.08); tone(3525, 0, ev.perfect ? 0.24 : 0.1, W.Sine, 0.045);
                noise(0.03, 6000, 0.08, F.Highpass); if (ev.perfect) tone(1175, 2350, 0.12, W.Triangle, 0.04);
                break;
              case "dashSlash": noise(0.18, 1800, 0.14 + 0.04 * ev.tier, F.Bandpass, 6500); tone(1100, 2400, 0.1, W.Sine, 0.05); tone(140, 60, 0.14, W.Sine, 0.06 + 0.03 * ev.tier); break;
              case "crescent": tone(300, 900, 0.22, W.Sawtooth, 0.05); noise(0.25, 900, 0.1, F.Bandpass, 4200); tone(1800, 1200, 0.18, W.Sine, 0.03); break;
              case "pogo": tone(320, 900, 0.1, W.Triangle, 0.07); noise(0.06, 2400, 0.05); break;
              case "poundStart": noise(0.22, 3000, 0.1, F.Bandpass, 400); tone(520, 260, 0.2, W.Triangle, 0.04); break;
              case "poundLevel": tone(Pick(new double[] { 0, 392, 523, 659 }, ev.level, 523), 0, 0.12, W.Triangle, 0.07); if (ev.level == 3) tone(1047, 0, 0.12, W.Sine, 0.05, 0.05); break;
              case "poundDrop": tone(1500, 380, 0.32, W.Sine, 0.05); noise(0.3, 700, 0.08 + 0.02 * ev.level, F.Bandpass, 3000); break;
              case "poundLand": {
                var L = ev.level;
                tone(88 - 6 * L, 30, 0.35 + 0.08 * L, W.Sine, 0.2 + 0.05 * L); noise(0.3 + 0.1 * L, 700, 0.18 + 0.05 * L, F.Lowpass, 120);
                noise(0.06, 4200, 0.1 + 0.03 * L, F.Highpass); tone(200, 60, 0.2, W.Sawtooth, 0.05 + 0.02 * L);
                if (L >= 3) { tone(55, 28, 0.6, W.Sine, 0.2); noise(0.7, 400, 0.12, F.Lowpass, 90, 0.05); }
                break;
              }
              // Nova: the Level 4 beam and the Aegis
              case "beamStart": tone(1760, 440, 0.3, W.Sawtooth, 0.05); noise(0.35, 2000, 0.16, F.Bandpass, 500); tone(70, 35, 0.4, W.Sine, 0.2); break;
              case "beamEnd": tone(420, 90, 0.35, W.Sawtooth, 0.04); noise(0.3, 1500, 0.06, F.Lowpass, 200); break;
              case "erase": if (limit("erase", 0.05)) noise(0.05, 3500, 0.05, F.Highpass); break;
              case "aegisOn": tone(660, 1320, 0.2, W.Sine, 0.06); tone(990, 1980, 0.24, W.Triangle, 0.035, 0.03); noise(0.25, 5000, 0.05, F.Highpass, 2000); break;
              case "aegisHit": {
                // Glass under strain: a ping that drops with the shield"s strength, and a crackle as it weakens
                var fr = ev.frac;
                if (limit("aegisHit", 0.03)) { tone(1900 + 1400 * fr, 0, 0.12, W.Triangle, 0.07); noise(0.04, 6000, 0.08, F.Highpass); }
                if (fr < 0.6) for (var i = 0; i < 3; i++) tone(2400 + R() * 2400, 0, 0.03, W.Square, 0.02, 0.02 + i * 0.025);
                tone(160, 90, 0.1, W.Sine, 0.06);
                break;
              }
              case "aegisOff":
                if (ev.why == "break") {
                  // Shatter: a burst of glass, then tinkling shards
                  noise(0.6, 4200, 0.2, F.Highpass, 1800); tone(140, 50, 0.3, W.Sine, 0.14);
                  for (var i = 0; i < 9; i++) tone(2000 + R() * 3000, 0, 0.06 + R() * 0.06, W.Triangle, 0.03, 0.03 + i * 0.045);
                } else if (ev.why == "detonate") {
                  tone(90, 32, 0.5, W.Sine, 0.22); noise(0.45, 900, 0.22, F.Lowpass, 150); tone(1320, 2640, 0.2, W.Sine, 0.05); noise(0.3, 5000, 0.1, F.Highpass);
                } else tone(1320, 440, 0.4, W.Sine, 0.04);
                break;
              case "lockOn": if (ev.why == "auto") break;   // automatic lock-on is silent; the reticle shows it
                tone(1320, 0, 0.05, W.Sine, 0.05); tone(1760, 0, 0.07, W.Sine, 0.05, 0.05); break;
              case "lockSwitch": tone(1560, 0, 0.05, W.Sine, 0.045); break;
              case "lockOff": tone(1320, 880, 0.08, W.Sine, 0.035); break;
              case "lockNone": tone(300, 0, 0.06, W.Square, 0.025); break;
              case "slide": noise(0.22, 500, 0.1, F.Lowpass); break;
              case "land":
                if (!limit("land", 0.08)) break;
                if (ev.vy < -16) { var k = System.Math.Min(1, (-ev.vy - 16) / 12); noise(0.14, 300, 0.12 + 0.1 * k, F.Lowpass); tone(85, 42, 0.18, W.Sine, 0.12 + 0.1 * k); }
                else noise(0.05, 320, 0.08, F.Lowpass);
                break;
              case "shot":
                if (ev.bolt) { tone(720, 360, 0.07, W.Triangle, 0.07); break; }
                if (ev.cannon) {
                  // RAM"s cannon: a deep boom, bigger with each level
                  var L = ev.level; tone(150 - 15 * L, 45, 0.22 + 0.06 * L, W.Sine, 0.16 + 0.04 * L); noise(0.16 + 0.05 * L, 700, 0.14 + 0.04 * L, F.Lowpass, 160);
                  if (L != 0) tone(900, 300, 0.2, W.Sawtooth, 0.04 + 0.01 * L);
                  break;
                }
                if (ev.rivet) {
                  if (ev.level != 0) { tone(700, 300, 0.12, W.Square, 0.05); noise(0.1, 2000, 0.07, F.Bandpass, 600); }
                  else if (limit("rivet", 0.03)) { tone(1400, 1000, 0.03, W.Square, 0.03); noise(0.025, 5000, 0.04, F.Highpass); }
                  break;
                }
                if (ev.attach == "volley") { for (var i = 0; i < 3; i++) tone(1500, 900, 0.06, W.Triangle, 0.05, i * 0.035); break; }
                if (ev.attach == "arc") { tone(190, 110, 0.18, W.Sine, 0.14); noise(0.1, 700, 0.08, F.Lowpass); break; }
                if (ev.attach == "prism") { tone(2100, 1400, 0.16, W.Sine, 0.06); tone(3150, 2100, 0.12, W.Triangle, 0.03); break; }
                if (ev.level >= 2) tone(110, 42, 0.16 + 0.05 * ev.level, W.Sine, 0.06 + 0.05 * (ev.level - 1));   // charged shots thump
                if (ev.level == 0) { if (limit("shot", 0.03)) { tone(900, 450, 0.05, W.Square, 0.035); noise(0.03, 3000, 0.04); } }
                else if (ev.level == 1) { tone(540, 220, 0.16, W.Sawtooth, 0.08); noise(0.1, 1500, 0.08); }
                else { tone(260, 80, 0.4, W.Sawtooth, 0.12); tone(1700, 700, 0.2, W.Sine, 0.05); noise(0.25, 900, 0.14, F.Lowpass); }
                break;
              case "tracer": tone(1200, 1900, 0.1, W.Sine, 0.06); break;
              case "chargeLevel":
                if (ev.level >= 4) { foreach (var (f, d) in new (double, double)[] { (1568, 0), (2093, 0.04), (2637, 0.08), (3136, 0.12) }) tone(f, 0, 0.3, W.Sine, 0.05, d); noise(0.3, 5000, 0.05, F.Highpass, 2000); break; }
                tone(Pick(new double[] { 0, 660, 880, 1175 }, ev.level, 990), 0, 0.14, W.Sine, 0.07); if (ev.level == 3) tone(1760, 0, 0.1, W.Sine, 0.04, 0.05);
                break;
              case "burstLevel": tone(Pick(new double[] { 0, 440, 587, 784 }, ev.level, 587), 0, 0.12, W.Triangle, 0.07); break;
              case "meleeCharged": tone(520, 1040, 0.12, W.Sine, 0.06); break;
              case "swing": {
                // Blades hiss, the glaive whooshes low and long, Nova"s hard-light fists thump the air
                if (!limit("swing", 0.04)) break;
                var id = ev.id ?? "";
                if (id == "nova_rise") { noise(0.32, 600, 0.13, F.Bandpass, 3200); tone(150, 520, 0.26, W.Sawtooth, 0.05); tone(90, 45, 0.15, W.Sine, 0.1); }
                else if (id.StartsWith("nova_k")) { noise(0.08, 900, 0.07, F.Bandpass, 400); tone(220, 120, 0.07, W.Triangle, 0.04); if (id == "nova_k3") tone(90, 45, 0.15, W.Sine, 0.1); }
                else if (id == "echo_spin" || id == "echo_rise") { for (var i = 0; i < 4; i++) noise(0.08, 1200 + i * 300, 0.06, F.Bandpass, 700, i * 0.06); }
                else if (Regex.IsMatch(id, "b4|charged|riposte|ab3|g3|air3")) { noise(0.16, 700, 0.09, F.Bandpass, 2400); tone(180, 90, 0.12, W.Sine, 0.05); }
                else noise(0.07, 2600, 0.06, F.Bandpass, 5200);
                break;
              }
              case "hit":
                if (!limit("hit", 0.025)) break;
                if (ev.heavy) { noise(0.12, 700, 0.22, F.Lowpass); tone(150, 60, 0.14, W.Sine, 0.2); }
                else { noise(0.06, 1100, 0.14, F.Lowpass); tone(200, 100, 0.07, W.Sine, 0.12); }
                break;
              case "blocked": tone(1450, 0, 0.05, W.Square, 0.04); tone(1950, 0, 0.07, W.Square, 0.03, 0.01); break;
              case "guardBreak": noise(0.25, 2000, 0.16, F.Highpass); tone(320, 140, 0.2, W.Sawtooth, 0.1); break;
              case "armorHit": if (limit("armor", 0.05)) tone(520, 480, 0.06, W.Triangle, 0.08); break;
              case "armorBreak": noise(0.35, 1200, 0.25, F.Lowpass); tone(200, 55, 0.35, W.Sawtooth, 0.12); break;
              case "spinStun": tone(990, 495, 0.18, W.Triangle, 0.07); break;
              case "stagger": tone(880, 440, 0.25, W.Sine, 0.08); tone(1320, 660, 0.25, W.Sine, 0.05); break;
              case "kill": tone(300, 900, 0.1, W.Triangle, 0.08); noise(0.08, 2500, 0.08); break;
              case "parry":
                if (ev.perfect) { tone(1568, 0, 0.35, W.Sine, 0.12); tone(2349, 0, 0.3, W.Sine, 0.08); tone(3136, 0, 0.25, W.Sine, 0.05); tone(1250, 0, 0.08, W.Square, 0.05); }
                else { tone(1250, 0, 0.12, W.Square, 0.07); tone(1870, 0, 0.1, W.Square, 0.05); }
                break;
              case "parryFail": tone(200, 110, 0.18, W.Square, 0.07); break;
              case "playerHit": tone(230, 110, 0.14, W.Sawtooth, 0.14); noise(0.1, 800, 0.12, F.Lowpass); break;
              case "telegraph":
                if (ev.cat == "standard") tone(1760, 0, 0.05, W.Sine, 0.09);
                else if (ev.cat == "heavy") { tone(1320, 0, 0.06, W.Sine, 0.1); tone(1320, 0, 0.06, W.Sine, 0.1, 0.1); }
                else rising(ev.ticks / 60);
                break;
              case "lock": tone(2100, 0, 0.09, W.Sine, 0.08); break;
              case "enemyShot": if (ev.heavy) tone(420, 180, 0.18, W.Sawtooth, 0.08); else tone(620, 300, 0.08, W.Square, 0.04); break;
              case "slam": noise(0.45, 220, 0.3, F.Lowpass); tone(70, 40, 0.35, W.Sine, 0.25); break;
              case "bulwark": tone(110, 50, 0.4, W.Sine, 0.25); tone(880, 1320, 0.25, W.Triangle, 0.05); noise(0.2, 400, 0.1, F.Lowpass); break;
              case "barrierBlock": tone(1100, 0, 0.06, W.Triangle, 0.06); break;
              case "amplify": tone(1320, 1760, 0.08, W.Sine, 0.05); break;
              case "intercept": tone(1500, 700, 0.08, W.Square, 0.05); noise(0.06, 2400, 0.06); break;
              case "lash": noise(0.12, 2600, 0.1, F.Bandpass, 900); tone(900, 300, 0.12, W.Triangle, 0.05); break;
              case "lashPull": case "lashZip": tone(300, 700, 0.12, W.Triangle, 0.07); break;
              case "vbStart": noise(0.18, 420, 0.12 + ev.tier * 0.05, F.Lowpass); tone(95, 45, 0.2, W.Sine, 0.1 + ev.tier * 0.05); break;
              case "boost": tone(600, 1200, 0.15, W.Sine, 0.07); break;
              case "downed": tone(440, 220, 0.45, W.Sine, 0.1); break;
              case "revived": tone(784, 0, 0.18, W.Sine, 0.08); tone(988, 0, 0.18, W.Sine, 0.07, 0.08); tone(1175, 0, 0.25, W.Sine, 0.06, 0.16); break;
              case "recall": case "respawn": case "join": tone(520, 1040, 0.2, W.Sine, 0.07); break;
              case "banner": case "checkpoint": tone(660, 0, 0.14, W.Triangle, 0.06); tone(880, 0, 0.2, W.Triangle, 0.06, 0.12); break;
              case "wipe": tone(220, 110, 0.6, W.Triangle, 0.1); break;
              case "bark": tone(520, 0, 0.04, W.Triangle, 0.03); tone(660, 0, 0.05, W.Triangle, 0.03, 0.05); break;
              case "snareThrow": noise(0.1, 1800, 0.07, F.Bandpass, 900); tone(600, 900, 0.07, W.Triangle, 0.05); break;
              case "snarePlant": tone(300, 0, 0.05, W.Square, 0.04); tone(900, 0, 0.06, W.Sine, 0.05, 0.12); break;
              case "snareTrigger": noise(0.15, 3000, 0.12, F.Highpass); tone(1200, 300, 0.15, W.Sawtooth, 0.08); break;
              case "snared": tone(440, 220, 0.14, W.Square, 0.05); break;
              case "leash": tone(180, 360, 0.2, W.Triangle, 0.07); noise(0.08, 2000, 0.05); break;
              case "leashEnd": tone(360, 200, 0.08, W.Triangle, 0.03); break;
              case "yank": noise(0.2, 420, 0.18, F.Lowpass); tone(120, 60, 0.2, W.Sine, 0.18); noise(0.1, 2600, 0.08, F.Bandpass, 900); break;
              // Scarf modes
              case "scarfMode":
                if (ev.mode == "veil") tone(760, 380, 0.14, W.Sine, 0.05);
                else if (ev.mode == "flare") { tone(440, 880, 0.12, W.Sawtooth, 0.04); tone(660, 1320, 0.14, W.Triangle, 0.04, 0.05); }
                else tone(520, 780, 0.1, W.Triangle, 0.05);
                break;
              case "veilOn": noise(0.3, 3000, 0.05, F.Highpass, 800); tone(900, 450, 0.25, W.Sine, 0.03); break;
              case "veilBreak": if (ev.wasHidden) tone(700, 1400, 0.08, W.Sine, 0.04); break;
              case "vanish": noise(0.25, 4000, 0.1, F.Bandpass, 600); tone(1200, 300, 0.2, W.Sine, 0.05); break;
              case "ambush": noise(0.18, 600, 0.2, F.Lowpass); tone(180, 60, 0.25, W.Sine, 0.2); tone(2400, 1200, 0.06, W.Square, 0.04); break;
              case "challenge": tone(330, 660, 0.22, W.Sawtooth, 0.07); tone(495, 990, 0.26, W.Triangle, 0.06, 0.08); break;
              case "lostTrack": if (limit("lost", 0.15)) tone(620, 520, 0.1, W.Triangle, 0.03); break;
              // Nova, Marksman kit
              case "attach": tone(1100, 0, 0.03, W.Square, 0.03); tone((ev.attach == "lance" ? 660 : ev.attach == "volley" ? 880 : ev.attach == "arc" ? 520 : ev.attach == "prism" ? 1320 : 700), 0, 0.08, W.Triangle, 0.05, 0.03); break;
              case "perfectRelease": tone(1760, 0, 0.22, W.Sine, 0.08); tone(2637, 0, 0.18, W.Sine, 0.05, 0.02); break;
              case "blast": noise(0.35, 500, 0.2 + (ev.perfect ? 0.06 : 0), F.Lowpass); tone(95, 40, 0.3, W.Sine, 0.18); break;
              case "split": tone(2600, 1800, 0.1, W.Sine, 0.05); tone(3300, 0, 0.06, W.Triangle, 0.03); break;
              case "ricochet": if (limit("rico", 0.05)) tone(3000, 2000, 0.05, W.Triangle, 0.03); break;
              case "burst": { var k = ev.level; noise(0.1 + k * 0.04, 1300, 0.14 + k * 0.03, F.Bandpass, 500); tone(170 - k * 15, 70, 0.12 + k * 0.04, W.Sine, 0.1 + k * 0.03); break; }
              case "splash": if (limit("splash", 0.04)) { noise(0.08 + ev.r * 0.04, 900, 0.05 + ev.r * 0.03, F.Lowpass); tone(240, 90, 0.08, W.Sine, 0.04 + ev.level * 0.02); } break;
              case "rocketJump": {
                // A deep boom, a sharp crack, a rumbling tail and the rising rush of the launch
                var k = Or(ev.power, 0.5);
                tone(95, 30, 0.5 + 0.2 * k, W.Sine, 0.22 + 0.14 * k);
                tone(190, 60, 0.26, W.Sawtooth, 0.05 + 0.04 * k);
                noise(0.08, 4500, 0.12 + 0.08 * k, F.Highpass);
                noise(0.6 + 0.25 * k, 1400, 0.2 + 0.1 * k, F.Lowpass, 160);
                noise(0.5, 500, 0.07 + 0.05 * k, F.Bandpass, 3500, 0.04);
                if (ev.perfect) tone(1760, 2637, 0.3, W.Sine, 0.05, 0.04);
                break;
              }
              case "thrustOn": jet(ev.p, true); break;
              case "thrustOff": jet(ev.p, false); break;
              case "mortarShot": tone(95, 55, 0.25, W.Sine, 0.2); noise(0.12, 500, 0.1, F.Lowpass); tone(1700, 420, System.Math.Max(0.3, ev.ticks / 60), W.Sine, 0.03, 0.05); break;
              case "enemyBlast": noise(0.45, 420, 0.3, F.Lowpass); tone(70, 32, 0.4, W.Sine, 0.26); break;
              case "chargeStart": tone(110, 230, 0.3, W.Sawtooth, 0.08); noise(0.35, 300, 0.12, F.Lowpass); break;
              case "chargeCrash": noise(0.3, 900, 0.24, F.Lowpass); tone(120, 50, 0.3, W.Square, 0.1); break;
              case "carve": if (limit("carve", 0.12)) noise(0.16, 4200, 0.05, F.Highpass, 2000); break;
              case "focusUp": tone(700 + ev.level * 120, 0, 0.08, W.Triangle, 0.04); break;
              case "focusLost": tone(520, 260, 0.16, W.Triangle, 0.04); break;
              case "gates": if (ev.closed) tone(140, 110, 0.5, W.Sawtooth, 0.06); else tone(220, 660, 0.4, W.Triangle, 0.06); break;
              // Bosses
              case "bossIntro": foreach (var (f, d) in new (double, double)[] { (73, 0), (110, 0.02), (147, 0.04) }) tone(f, f * 0.98, 1.1, W.Sawtooth, 0.07, d); noise(0.8, 300, 0.1, F.Lowpass, 90); tone(1100, 550, 0.6, W.Sine, 0.03, 0.2); break;
              case "bossSlam": tone(60, 28, 0.55, W.Sine, ev.big ? 0.3 : 0.2); noise(0.45, 500, ev.big ? 0.28 : 0.18, F.Lowpass, 80); noise(0.06, 3500, 0.1, F.Highpass); break;
              case "bossPhase": tone(90, 45, 1.0, W.Sawtooth, 0.12); tone(180, 70, 0.9, W.Square, 0.05); noise(1.0, 1200, 0.18, F.Bandpass, 200); tone(880, 1760, 0.4, W.Sine, 0.04, 0.3); break;
              case "bossLaser": tone(220, 110, Or(ev.ticks, 40) / 60, W.Sawtooth, 0.08); noise(Or(ev.ticks, 40) / 60, 2600, 0.1, F.Bandpass, 1800); tone(1760, 0, 0.08, W.Square, 0.04); break;
              case "bossMissiles": for (var i = 0; i < Or(ev.n, 3); i++) { noise(0.12, 1600, 0.08, F.Bandpass, 600, i * 0.06); tone(400, 900, 0.1, W.Triangle, 0.035, i * 0.06); } break;
              case "bossDive": tone(1800, 300, 0.45, W.Sawtooth, 0.05); noise(0.45, 900, 0.12, F.Bandpass, 3500); break;
              case "bossCrash": tone(70, 30, 0.5, W.Sine, 0.25); noise(0.5, 700, 0.24, F.Lowpass, 90); for (var i = 0; i < 5; i++) tone(600 + R() * 900, 0, 0.05, W.Square, 0.03, 0.05 + i * 0.07); break;
              case "bossDazed": tone(660, 330, 0.3, W.Sine, 0.07); tone(990, 495, 0.3, W.Sine, 0.05); break;
              case "bossCall": tone(520, 1040, 0.25, W.Sawtooth, 0.05); tone(780, 1560, 0.25, W.Sawtooth, 0.04, 0.12); break;
              case "bossDown":
                for (var i = 0; i < 8; i++) { noise(0.25, 700, 0.14, F.Lowpass, 120, i * 0.13); tone(90, 40, 0.25, W.Sine, 0.12, i * 0.13); }
                tone(55, 25, 1.4, W.Sine, 0.3, 1.15); noise(1.4, 900, 0.3, F.Lowpass, 60, 1.15); noise(0.1, 5000, 0.14, F.Highpass, 0, 1.15);
                break;
              // Nova"s secondary weapons
              case "subSwitch": tone(1100, 0, 0.03, W.Square, 0.03); tone(SubHum(ev.sub) * 4, 0, 0.09, W.Triangle, 0.05, 0.03); break;
              case "grenadeThrow": tone(2400, 0, 0.02, W.Square, 0.03); noise(0.1, 1200, 0.07, F.Bandpass, 600, 0.01); tone(300, 190, 0.09, W.Triangle, 0.04, 0.01); break;
              case "bounce": if (limit("bounce", 0.05)) { var k = System.Math.Min(1, Or(ev.sp, 5) / 12); tone(950 + R() * 250, 700, 0.05, W.Triangle, 0.035 * k); tone(2100, 0, 0.03, W.Square, 0.012 * k); } break;
              case "frag": { var L = ev.level; noise(0.4 + 0.05 * L, 650, 0.22 + 0.03 * L, F.Lowpass, 110); tone(115 - 8 * L, 36, 0.35 + 0.05 * L, W.Sine, 0.2); noise(0.06, 4200, 0.12, F.Highpass);
                for (var i = 0; i < 3; i++) tone(1400 + R() * 1600, 0, 0.03, W.Square, 0.015, 0.06 + i * 0.05); break; }
              case "cluster": tone(700, 1400, 0.06, W.Square, 0.04); noise(0.08, 2400, 0.06); break;
              case "chain": {
                // Lightning: a sharp electric crack, a buzzing sweep and a crackle for every jump
                var L = ev.level;
                noise(0.16 + 0.04 * L, 3600, 0.15 + 0.03 * L, F.Bandpass, 900); tone(1900, 280, 0.16, W.Sawtooth, 0.06 + 0.01 * L); tone(60, 40, 0.14, W.Square, 0.05);
                for (var i = 0; i < Or(ev.n, 1); i++) { tone(2000 + R() * 1800, 0, 0.03, W.Square, 0.035, 0.02 + i * 0.035); noise(0.04, 5000, 0.05, F.Highpass, 0, 0.02 + i * 0.035); }
                break;
              }
              case "discThrow": noise(0.22, 2400, 0.09, F.Bandpass, 5600); tone(700, 1150, 0.16, W.Triangle, 0.05); break;
              case "discRecall": tone(1150, 700, 0.1, W.Sine, 0.04); break;
              case "discCatch": tone(1500, 0, 0.05, W.Triangle, 0.05); noise(0.04, 3000, 0.05); break;
              case "wellLaunch": tone(260, 160, 0.2, W.Sine, 0.07); noise(0.15, 800, 0.05, F.Lowpass); break;
              case "wellOpen": noise(0.35, 300, 0.12, F.Bandpass, 2600); tone(70, 48, 0.5, W.Sine, 0.16); tone(1400, 350, 0.3, W.Sine, 0.04); break;
              case "wellCollapse": { var L = Or(ev.level, 1); tone(50, 24, 0.55, W.Sine, 0.24 + 0.02 * L); noise(0.45, 1200, 0.2, F.Lowpass, 100); tone(1300, 2600, 0.14, W.Sine, 0.04); noise(0.06, 5000, 0.08, F.Highpass); break; }
              // The dodge and the Solar Uppercut
              case "dodge": noise(0.14, 2600, 0.07, F.Bandpass, 5800); tone(900, 1500, 0.06, W.Sine, 0.03); break;
              case "perfectDodge": tone(520, 130, 0.65, W.Sine, 0.13); tone(1560, 390, 0.6, W.Triangle, 0.045); noise(0.6, 4000, 0.06, F.Highpass, 800); tone(2637, 0, 0.3, W.Sine, 0.05); break;
              case "riseBlast": tone(120, 50, 0.3, W.Sine, 0.16); tone(1320, 2640, 0.18, W.Sine, 0.05); noise(0.25, 3000, 0.1, F.Bandpass, 800); break;
              // Ultimates
              case "ultReady": foreach (var (f, d) in new (double, double)[] { (1047, 0), (1319, 0.06), (1568, 0.12), (2093, 0.18) }) tone(f, 0, 0.22, W.Sine, 0.05, d); break;
              case "ultCast": case "ultJoin": {
                // The call: a rising chord that swells, a rushing sweep and a deep hit under it
                var j = ev.type == "ultJoin" ? 1.26 : 1;
                foreach (var (f, d) in new (double, double)[] { (110, 0), (165, 0.02), (220, 0.04) }) tone(f * j, f * j * 4, 0.8, W.Sawtooth, 0.045, d);
                foreach (var (f, d) in new (double, double)[] { (523, 0.05), (659, 0.08), (784, 0.11) }) tone(f * j, 0, 0.9, W.Sine, 0.035, d);
                noise(0.8, 400, 0.14, F.Bandpass, 6000); tone(55, 30, 0.9, W.Sine, 0.22); noise(0.08, 6000, 0.1, F.Highpass);
                break;
              }
              case "ultRun": tone(60, 30, 0.5, W.Sine, 0.22); noise(0.3, 3000, 0.12, F.Bandpass, 500); break;
              case "ultBegin":
                if (ev.kind == "nova") tone(220, 1760, 0.4, W.Sine, 0.06);
                else if (ev.kind == "ram") { tone(50, 110, 0.6, W.Sawtooth, 0.1); noise(0.5, 300, 0.14, F.Lowpass); }
                else if (ev.kind == "fix") { foreach (var d in new double[] { 0, 0.12, 0.24 }) tone(1600, 0, 0.06, W.Square, 0.03, d); }
                else { noise(0.3, 5000, 0.12, F.Bandpass, 800); tone(1400, 350, 0.25, W.Sine, 0.05); }
                break;
              case "ultNova": tone(45, 20, 1.2, W.Sine, 0.32); noise(1.2, 900, 0.3, F.Lowpass, 60); noise(0.1, 6000, 0.16, F.Highpass); tone(1760, 3520, 0.4, W.Sine, 0.06); break;
              case "ultCut": if (limit("cut", 0.02)) { noise(0.07, 3500 + R() * 2000, 0.11, F.Bandpass, 7000); tone(2400, 1200, 0.04, W.Square, 0.025); } break;
              case "ultFinisher":
                noise(0.5, 1800, 0.25, F.Bandpass, 8000); tone(90, 30, 0.8, W.Sine, 0.3); tone(2800, 1400, 0.3, W.Sine, 0.05);
                for (var i = 0; i < 6; i++) noise(0.06, 3000 + i * 500, 0.06, F.Bandpass, 7000, 0.05 + i * 0.03);
                break;
              case "ultEnd": tone(880, 1320, 0.2, W.Sine, 0.04); break;
              // ---- RAM ----
              case "guardOn": tone(180, 140, 0.12, W.Triangle, 0.06); noise(0.08, 900, 0.06, F.Lowpass); tone(1100, 0, 0.05, W.Sine, 0.025); break;
              case "guardOff": tone(160, 120, 0.06, W.Triangle, 0.025); break;
              case "guardBlock":
                if (!limit("block", 0.03)) break;
                tone(ev.heavy ? 140 : 220, ev.heavy ? 70 : 150, 0.12 + (ev.heavy ? 0.1 : 0), W.Square, 0.06 + (ev.heavy ? 0.04 : 0)); noise(0.1, 2600, 0.08, F.Bandpass, 1200);
                tone(1900 + 700 * Or(ev.frac, 1), 0, 0.06, W.Sine, 0.03);
                break;
              case "perfectGuard": tone(1320, 2640, 0.2, W.Sine, 0.07); tone(220, 110, 0.25, W.Square, 0.07); noise(0.15, 4000, 0.08, F.Highpass); break;
              case "rampartBreak": noise(0.4, 2500, 0.24, F.Highpass, 800); for (var i = 0; i < 6; i++) tone(1800 + R() * 1600, 600, 0.12, W.Triangle, 0.03, i * 0.03); tone(200, 60, 0.4, W.Sawtooth, 0.1); break;
              case "rampartReady": tone(660, 990, 0.14, W.Sine, 0.05); tone(990, 0, 0.12, W.Triangle, 0.03, 0.08); break;
              case "kineticRelease": { var k = ev.k; tone(90, 30, 0.4 + 0.3 * k, W.Sine, 0.2 + 0.12 * k); noise(0.4 + 0.2 * k, 900, 0.22 + 0.1 * k, F.Lowpass, 90); tone(400, 1800, 0.25, W.Sawtooth, 0.04 + 0.03 * k); break; }
              case "rush": tone(70 - 5 * ev.level, 40, 0.3, W.Sawtooth, 0.08 + 0.02 * ev.level); noise(0.25, 400, 0.12 + 0.03 * ev.level, F.Lowpass); break;
              case "plowCatch": if (limit("plow", 0.04)) { noise(0.12, 700, 0.14, F.Lowpass); tone(160, 80, 0.12, W.Square, 0.06); } break;
              case "ramSplat": tone(60, 25, 0.6, W.Sine, 0.3); noise(0.5, 600, 0.3, F.Lowpass, 70); noise(0.08, 3500, 0.14, F.Highpass); tone(240, 80, 0.3, W.Square, 0.07); break;
              case "ramBonk": tone(300, 150, 0.15, W.Square, 0.08); tone(900, 600, 0.1, W.Triangle, 0.05); noise(0.12, 1200, 0.1, F.Lowpass); break;
              case "wallUp": tone(110, 330, 0.3, W.Sawtooth, 0.07); tone(660, 1320, 0.25, W.Sine, 0.05); noise(0.25, 300, 0.12, F.Lowpass); break;
              case "wallHit": if (limit("wallhit", 0.04)) tone(1300, 900, 0.06, W.Triangle, 0.04); break;
              case "wallDown": if (ev.broken) { noise(0.35, 3000, 0.16, F.Highpass, 700); tone(500, 120, 0.3, W.Triangle, 0.05); } else tone(660, 220, 0.3, W.Sine, 0.03); break;
              case "link": tone(440, 880, 0.2, W.Sine, 0.06); tone(660, 1320, 0.22, W.Triangle, 0.04, 0.05); noise(0.12, 2600, 0.04); break;
              case "linkHit": if (limit("linkhit", 0.08)) tone(520, 260, 0.1, W.Triangle, 0.04); break;
              case "linkEnd": tone(880, 440, 0.15, W.Sine, 0.03); break;
              case "linkNone": case "notReady": if (limit("nope", 0.15)) tone(240, 0, 0.06, W.Square, 0.025); break;
              case "leap": tone(120, 360, 0.25, W.Sawtooth, 0.07); noise(0.2, 500, 0.1, F.Lowpass); break;
              case "leapLand": tone(70, 30, 0.35, W.Sine, 0.24); noise(0.3, 500, 0.2, F.Lowpass, 80); break;
              case "provoke":
                // A war cry: a growling chord and a drum hit
                foreach (var (f, d) in new (double, double)[] { (98, 0), (147, 0.02), (196, 0.04) }) tone(f, f * 1.12, 0.5, W.Sawtooth, 0.06, d);
                tone(55, 30, 0.4, W.Sine, 0.2); noise(0.4, 600, 0.12, F.Bandpass, 200);
                break;
              case "quake": tone(48, 22, 0.6, W.Sine, 0.3); noise(0.55, 400, 0.26, F.Lowpass, 60); noise(0.06, 3000, 0.1, F.Highpass); break;
              case "upliftBlast": tone(140, 60, 0.3, W.Sine, 0.16); noise(0.25, 1800, 0.12, F.Bandpass, 500); tone(700, 1400, 0.15, W.Triangle, 0.04); break;
              case "breachBlast": noise(0.4, 600, 0.22, F.Lowpass, 90); tone(85, 35, 0.35, W.Sine, 0.2); break;
              case "fortify": foreach (var (f, d) in new (double, double)[] { (392, 0), (523, 0.05), (659, 0.1) }) tone(f, 0, 0.4, W.Triangle, 0.05, d); noise(0.3, 3000, 0.05, F.Highpass); break;
              case "ramSlam": tone(38, 18, 1.4, W.Sine, 0.36); noise(1.2, 700, 0.34, F.Lowpass, 50); noise(0.12, 5000, 0.18, F.Highpass); tone(330, 82, 0.6, W.Square, 0.08); break;
              // ---- Fix ----
              case "gadgetSelect": case "powerSelect": tone(1300, 0, 0.03, W.Square, 0.03); tone(ev.type == "gadgetSelect" ? 880 : 1175, 0, 0.08, W.Triangle, 0.05, 0.03); break;
              case "gadgetDeploy":
                // A ratchet and a clunk as it unfolds
                for (var i = 0; i < 5; i++) tone(2200 - i * 150, 0, 0.02, W.Square, 0.025, i * 0.03);
                tone(180, 90, 0.15, W.Square, 0.07, 0.16); noise(0.1, 900, 0.08, F.Lowpass, 0, 0.16);
                break;
              case "gadgetUp": foreach (var (f, d) in new (double, double)[] { (784, 0), (988, 0.06), (1175, 0.12), (1568, 0.18) }) tone(f, 0, 0.16, W.Triangle, 0.05, d); break;
              case "gadgetWrench": tone(2400, 1800, 0.06, W.Square, 0.045); tone(3200, 0, 0.04, W.Sine, 0.03, 0.02); break;
              case "gadgetHit": if (limit("ghit", 0.05)) tone(900, 600, 0.05, W.Square, 0.03); break;
              case "gadgetEnd": if (ev.why == "broken") { noise(0.3, 1500, 0.16, F.Lowpass, 200); tone(400, 100, 0.3, W.Sawtooth, 0.06); } else if (ev.why == "expire") tone(660, 330, 0.2, W.Sine, 0.03); break;
              case "sentryShot": if (limit("sentry", 0.03)) { tone(1100, 700, 0.04, W.Square, 0.025); noise(0.03, 3000, 0.025); } break;
              case "sentryRocket": noise(0.25, 1500, 0.08, F.Bandpass, 600); tone(300, 700, 0.15, W.Triangle, 0.035); break;
              case "padPlace": tone(200, 400, 0.12, W.Triangle, 0.06); noise(0.1, 600, 0.08, F.Lowpass); break;
              case "padBounce": if (limit("pad", 0.06)) { tone(220, 880, 0.18, W.Sine, 0.08); tone(330, 1320, 0.15, W.Triangle, 0.04); } break;
              case "powerToss": tone(500, 900, 0.1, W.Triangle, 0.05); noise(0.08, 2000, 0.04); break;
              case "powerUp": {
                var f = (ev.kind == "overclock" ? 1175 : ev.kind == "plating" ? 784 : 988);
                foreach (var (m, d) in new (double, double)[] { (1, 0), (1.25, 0.05), (1.5, 0.1) }) tone(f * m, 0, 0.18, W.Sine, 0.05, d);
                break;
              }
              case "powerFade": tone(660, 330, 0.15, W.Sine, 0.025); break;
              case "noScrap": if (limit("noscrap", 0.4)) { tone(260, 200, 0.08, W.Square, 0.03); tone(200, 0, 0.08, W.Square, 0.03, 0.09); } break;
              case "scrap": if (limit("scrap", 0.06)) for (var i = 0; i < 4; i++) tone(1800 + R() * 1200, 0, 0.03, W.Square, 0.015, i * 0.04); break;
              case "rivetStick": tone(2000, 1200, 0.05, W.Square, 0.035); tone(600, 0, 0.3, W.Sine, 0.02, 0.05); break;
              case "rivetBlast": noise(0.28, 900, 0.16 + 0.03 * Or(ev.level, 1), F.Lowpass, 100); tone(130, 50, 0.25, W.Sine, 0.12); break;
              case "sparkRing": noise(0.25, 5000, 0.12, F.Highpass, 1500); for (var i = 0; i < 5; i++) tone(2400 + R() * 2000, 0, 0.03, W.Square, 0.02, i * 0.03); break;
              case "repairPulse": tone(660, 1320, 0.2, W.Sine, 0.05); tone(990, 1980, 0.18, W.Triangle, 0.03, 0.04); break;
              case "patchOn": tone(880, 1320, 0.1, W.Sine, 0.04); break;
              case "patchOff": tone(1320, 880, 0.08, W.Sine, 0.025); break;
              case "podCall": tone(2200, 300, 0.9, W.Sine, 0.05); noise(0.9, 3000, 0.06, F.Bandpass, 600); break;
              case "podLand": tone(45, 20, 1.0, W.Sine, 0.34); noise(0.9, 800, 0.3, F.Lowpass, 60); noise(0.1, 5000, 0.14, F.Highpass); break;
              case "overhaulPulse": foreach (var (f, d) in new (double, double)[] { (523, 0), (659, 0.04), (784, 0.08), (1047, 0.12) }) tone(f, f * 1.5, 0.45, W.Sine, 0.05, d); noise(0.4, 3000, 0.08, F.Bandpass, 800); break;
              case "overhaulDone": foreach (var (f, d) in new (double, double)[] { (1047, 0), (1319, 0.06), (1568, 0.12), (2093, 0.18) }) tone(f, 0, 0.3, W.Triangle, 0.045, d); break;
              case "teamFinisher":
                // The eclipse: a swelling rumble, then the loudest hit in the game and a bright chord
                tone(40, 70, 0.42, W.Sawtooth, 0.08); noise(0.42, 200, 0.12, F.Lowpass, 1400);
                tone(40, 20, 1.6, W.Sine, 0.36, 0.42); noise(1.4, 1200, 0.32, F.Lowpass, 60, 0.42); noise(0.15, 6000, 0.18, F.Highpass, 0, 0.42);
                foreach (var (f, d) in new (double, double)[] { (523, 0.45), (784, 0.47), (1047, 0.49), (1568, 0.51) }) tone(f, 0, 1.1, W.Sine, 0.05, d);
                break;
            }
        }
    }
}
