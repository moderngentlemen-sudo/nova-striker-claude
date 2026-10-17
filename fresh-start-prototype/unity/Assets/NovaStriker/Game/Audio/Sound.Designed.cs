// Designed sound effects (Settings › Sound effects › Designed): layered samples from SampleBank played through
// the mixer (WebAudio.cs's Ctx), placed in stereo by where the event is on screen, with random pitch and level
// per play and a voice limit per sound. Each event either replaces the synthesized sound (Designed returns true)
// or adds weight under it (false, so the synth still plays): the timing cues (threat warnings, both parry grades,
// charge levels, the Perfect Release window, lock-ons) keep their synthesized tones so they stay unmistakable. A
// sound whose file is missing falls back to the synth on its own.
using System.Collections.Generic;
using NovaStriker.Sim;
using static NovaStriker.Sim.Cfg;

namespace NovaStriker.Game.Audio
{
    public sealed partial class Sound
    {
        public static Sound Main;   // for the menus (Ui)
        static bool DesignedOn => SETTINGS.sfxStyle != "classic";
        const double LEVEL = 0.85;   // the designed layer's overall level against the score and the synth cues
        World seen;   // the world as of the last Update, to place sounds on screen

        enum Bus { Sfx, Loops, Big, Ui }
        Gain BusOf(Bus b) => b == Bus.Loops ? ctx.loops : b == Bus.Big ? ctx.big : b == Bus.Ui ? ctx.ui : ctx.sfx;

        // ---- placing a sound: pan from where it is across the screen, a little quieter towards the edges and off
        // screen (the simulation's camera, which the view follows)
        (double pan, double lvl) PlaceAt(double x, double y)
        {
            var cam = seen?.cam;
            if (cam == null || cam.halfW <= 0) return (0, 1);
            double dx = (x - cam.x) / cam.halfW, dy = (y - cam.y) / System.Math.Max(1, cam.halfH);
            double off = System.Math.Max(System.Math.Abs(dx), System.Math.Abs(dy));
            double lvl = off <= 1 ? 1 - 0.12 * off : System.Math.Max(0.3, 0.88 - 0.45 * (off - 1));
            return (System.Math.Max(-0.85, System.Math.Min(0.85, dx * 0.75)), lvl);
        }
        (double pan, double lvl) Place(Ev ev)
        {
            if (ev == null) return (0, 1);
            if (ev.x != 0 || ev.y != 0) return PlaceAt(ev.x, ev.y);
            if (ev.e != null) return PlaceAt(ev.e.x, ev.e.y + ev.e.h / 2);
            if (ev.p != null) return PlaceAt(ev.p.x, ev.p.y + ev.p.h / 2);
            return (0, 1);
        }

        // ---- one-shots, with a voice limit per sound
        sealed class SVoice { public BufferSource src; public Gain g; public double t0, end; }
        readonly Dictionary<string, List<SVoice>> voices = new Dictionary<string, List<SVoice>>();
        readonly Dictionary<string, int> lastPick = new Dictionary<string, int>();

        // Plays one of the sound's variations. False when the sound has no file (the caller falls back to the synth).
        // semis: random pitch spread (±, semitones); max: voices of this sound at once (the oldest is faded out to
        // make room); rate: playback speed (pitch).
        bool S(string name, double gain, Ev ev = null, Bus bus = Bus.Sfx, double delay = 0, double semis = 0.5, int max = 4, double rate = 1)
        {
            var (pan, lvl) = Place(ev);
            return Fire(name, gain * lvl, pan, bus, delay, semis, max, rate) != null || SampleBank.Has(name);
        }
        SVoice Fire(string name, double gain, double pan, Bus bus, double delay, double semis, int max, double rate)
        {
            if (ctx == null || Vol <= 0) return null;
            var set = SampleBank.Set(name);
            if (set == null) return null;
            lock (ctx.gate)
            {
                double now = ctx.currentTime, t0 = now + delay;
                if (!voices.TryGetValue(name, out var list)) voices[name] = list = new List<SVoice>();
                list.RemoveAll(v => v.end <= now);
                int same = 0; foreach (var v in list) if (System.Math.Abs(v.t0 - t0) < 0.02) same++;
                if (same >= 2) return null;   // a pile-up in one frame (20 hits at once): two are plenty
                while (list.Count >= max) { var o = list[0]; o.g.gain.setTargetAtTime(0, now, 0.012); o.src.stop(now + 0.08); list.RemoveAt(0); }
                // a different variation from last time
                int k = 0;
                if (set.Length > 1) { k = rng.Next(set.Length - 1); if (lastPick.TryGetValue(name, out var lk) && k >= lk) k++; }
                lastPick[name] = k;
                var s = set[k];
                double r = rate * System.Math.Pow(2, (R() * 2 - 1) * semis / 12), g = gain * LEVEL * Vol * System.Math.Pow(10, (R() * 2 - 1) * 1.5 / 20);
                var src = ctx.createBufferSource(s.data, s.rate); src.playbackRate.value = r;
                var gn = ctx.createGain(g); var pn = ctx.createStereoPanner(); pn.pan.value = pan;
                src.connect(gn); gn.connect(pn); pn.connect(BusOf(bus)); src.start(t0);
                var sv = new SVoice { src = src, g = gn, t0 = t0, end = t0 + s.Duration / System.Math.Max(0.05, r) + 0.05 };
                list.Add(sv); return sv;
            }
        }
        void Cut(SVoice v, double fade = 0.03)
        {
            if (v == null || ctx == null) return;
            lock (ctx.gate) { double now = ctx.currentTime; v.g.gain.setTargetAtTime(0, now, fade); v.src.stop(now + fade * 6); v.end = now; }
        }

        // Debris settling after a kill or a blast: a few clinks scattered around the spot
        void Debris(Ev ev, int n, double spread = 0.9, double gain = 0.12)
        {
            var (pan, lvl) = Place(ev);
            for (int i = 0; i < n; i++)
                Fire("debris", gain * lvl * (0.5 + 0.5 * R()), System.Math.Max(-0.9, System.Math.Min(0.9, pan + (R() - 0.5) * 0.3)), Bus.Sfx, 0.18 + R() * spread, 1.5, 6, 1);
        }

        // ---- the menus: move, confirm, back, rebind (designed or a synthesized blip)
        public void Ui(string what)
        {
            if (ctx == null || Vol <= 0) return;
            if (DesignedOn && Fire("ui_" + what, 0.5, 0, Bus.Ui, 0, 0, 3, 1) != null) return;
            switch (what)
            {
                case "move": tone(1760, 0, 0.03, Wave.Sine, 0.03); break;
                case "confirm": tone(880, 0, 0.08, Wave.Triangle, 0.05); tone(1320, 0, 0.12, Wave.Triangle, 0.05, 0.06); break;
                case "back": tone(990, 0, 0.07, Wave.Triangle, 0.04); tone(660, 0, 0.1, Wave.Triangle, 0.04, 0.06); break;
                case "rebind": tone(1320, 0, 0.08, Wave.Sine, 0.05); tone(1568, 0, 0.08, Wave.Sine, 0.05, 0.05); tone(2093, 0, 0.12, Wave.Sine, 0.05, 0.1); break;
            }
        }

        // ---- the events. True: handled (the synth stays quiet); false: the synth plays too.
        bool Designed(Ev ev)
        {
            switch (ev.type)
            {
              // explosions
              case "blast": if (!S(ev.perfect ? "blast_big" : "blast_med", ev.perfect ? 0.75 : 0.65, ev, ev.perfect ? Bus.Big : Bus.Sfx, 0, 0.8, 4)) return false; Debris(ev, 2); return true;
              case "enemyBlast": return S(ev.r > 2.2 ? "blast_med" : "blast_small", ev.r > 2.2 ? 0.6 : 0.55, ev, Bus.Sfx, 0, 1, 4);
              case "splash": return S("blast_small", 0.22 + 0.06 * System.Math.Min(3, ev.level), ev, Bus.Sfx, 0, 1.5, 3, 1.15);
              case "rivetBlast": return S("blast_small", 0.45, ev, Bus.Sfx, 0, 1, 3);
              case "sentryRocket": S("blast_small", 0.18, ev, Bus.Sfx, 0, 2, 2, 1.3); return false;
              case "frag": if (!S("blast_med", 0.6, ev, Bus.Sfx, 0, 1, 4)) return false; Debris(ev, 3); return true;
              case "breachBlast": return S("blast_med", 0.62, ev, Bus.Sfx, 0, 0.8, 3, 0.92);
              case "wellCollapse": S("blast_med", 0.5, ev, Bus.Sfx, 0, 0.5, 2, 0.8); return false;   // keep the synth's implosion chime
              case "rocketJump": if (!S("blast_med", 0.45 + 0.25 * Or(ev.power, 0.5), ev, Bus.Sfx, 0, 0.6, 2)) return false; S("whoosh_heavy", 0.35, ev); return !ev.perfect;
              case "kineticRelease": if (!S(ev.k > 0.5 ? "blast_big" : "blast_med", 0.6 + 0.3 * ev.k, ev, ev.k > 0.5 ? Bus.Big : Bus.Sfx, 0, 0.5, 2)) return false; Debris(ev, 3); return true;
              case "quake": case "ramSlam": case "podLand":
                if (!S("blast_big", ev.type == "quake" ? 0.7 : 0.85, ev, Bus.Big, 0, 0.4, 3, ev.type == "podLand" ? 0.85 : 1)) return false;
                S("pound", 0.6, ev, Bus.Sfx, 0, 0.4, 2); Debris(ev, 5, 1.4); return true;
              case "bossCrash": if (!S("blast_big", 0.85, ev, Bus.Big, 0, 0.4, 2)) return false; Debris(ev, 5, 1.2); return true;
              case "aegisOff":
                if (ev.why == "break") { if (!S("shatter", 0.6, ev)) return false; Debris(ev, 4); return false; }   // the synth's glass tinkle on top
                if (ev.why == "detonate") return S("blast_big", 0.75, ev, Bus.Big, 0, 0.4, 2);
                return false;
              case "bossDown":
                // a run of blasts across the hull, then the big one
                if (!SampleBank.Has("blast_boss")) return false;
                for (int i = 0; i < 4; i++) { var (pn, lv) = Place(ev); Fire("blast_med", 0.45 * lv, System.Math.Max(-0.9, System.Math.Min(0.9, pn + (R() - 0.5) * 0.5)), Bus.Sfx, i * 0.26, 1.5, 4, 1); }
                S("blast_boss", 1.0, ev, Bus.Big, 1.15, 0.2, 2); Debris(ev, 8, 2.5, 0.14); return true;
              case "ultNova": return S("blast_boss", 0.95, ev, Bus.Big, 0, 0.2, 2);
              case "teamFinisher": S("blast_boss", 1.0, null, Bus.Big, 0.42, 0, 1); return false;   // the synth's swell and chord around it
              case "slam": return S("pound", 0.75, ev, Bus.Sfx, 0, 0.5, 2);
              case "bossSlam": if (!S(ev.big ? "blast_big" : "pound", ev.big ? 0.8 : 0.8, ev, ev.big ? Bus.Big : Bus.Sfx, 0, 0.4, 2)) return false; if (ev.big) Debris(ev, 4); return true;
              case "leapLand": return S("pound", 0.7, ev, Bus.Sfx, 0, 0.5, 2);
              case "poundLand": { var L = ev.level; if (!S("pound", 0.55 + 0.12 * L, ev, Bus.Sfx, 0, 0.4, 2, 1.06 - 0.04 * L)) return false; if (L >= 3) S("blast_med", 0.45, ev, Bus.Big, 0, 0.5, 2, 0.85); return true; }
              // beams (the loops follow the beam in DesignedLoops)
              case "beamStart": return S("beam_start", 0.8, ev, Bus.Big, 0, 0, 2);
              case "beamEnd": return S("beam_end", 0.6, ev, Bus.Sfx, 0, 0, 2);
              case "ultBegin": if (ev.kind == "nova") return S("nova_gather", 0.8, ev, Bus.Big, 0, 0, 2); return false;
              case "bossLaser": return S("laser_fire", 0.75, ev, Bus.Big, 0, 0, 2);
              // shots
              case "shot":
                if (ev.bolt) return false;
                if (ev.cannon) return S(ev.level >= 2 ? "cannon_big" : "cannon", 0.5 + 0.08 * ev.level, ev, Bus.Sfx, 0, 0.6, 3, 1.04 - 0.03 * ev.level);
                if (ev.rivet) return ev.level != 0 ? S("rivet_heavy", 0.5, ev, Bus.Sfx, 0, 0.8, 3) : S("rivet", 0.38, ev, Bus.Sfx, 0, 1.2, 4);
                if (ev.attach == "volley" || ev.attach == "arc" || ev.attach == "prism") return false;
                if (ev.perfect && S("shot_perfect", 0.7, ev, Bus.Sfx, 0, 0, 2)) return true;
                if (ev.level >= 2) return S("shot_heavy", 0.55 + 0.08 * (ev.level - 2), ev, Bus.Sfx, 0, 0.4, 3);
                if (ev.level == 1) return S("shot_charged", 0.5, ev, Bus.Sfx, 0, 0.6, 3);
                return S("shot_nova", 0.32, ev, Bus.Sfx, 0, 1.2, 4);
              case "snipe":
                if (!S("rifle", 0.55 + 0.25 * ev.f, ev, Bus.Sfx, 0, 0.6, 2, ev.full ? 0.94 : 1)) return false;
                S("rifle_bolt", 0.4, ev, Bus.Sfx, 0.34, 0.3, 2); return true;
              case "mortarShot": S("cannon", 0.3, ev, Bus.Sfx, 0, 1, 2, 1.25); return false;   // keep the synth's falling whistle
              // melee and impacts
              case "hit":
                if (ev.heavy) { if (!S("hit_heavy", 0.6, ev, Bus.Sfx, 0, 1, 4)) return false; }
                else if (!S("hit_light", 0.5, ev, Bus.Sfx, 0, 1.5, 5)) return false;
                return true;
              case "armorHit": if (!S("crack", 0.45, ev, Bus.Sfx, 0, 1.5, 4)) return false; S("clang", 0.2, ev, Bus.Sfx, 0, 1, 2); return true;
              case "armorBreak": if (!S("armor_break", 0.65, ev, Bus.Sfx, 0, 0.6, 3)) return false; Debris(ev, 3); return true;
              case "blocked": S("clang", 0.35, ev, Bus.Sfx, 0, 1, 3); return false;
              case "kill":
                if (ev.e != null && ev.e.boss) return false;   // bossDown has it
                S("shatter", ev.e != null && (ev.e.light || ev.e.flier) ? 0.45 : 0.6, ev, Bus.Sfx, 0, 1.2, 4); Debris(ev, 3);
                return false;   // the synth's kill blip confirms it
              case "chargeCrash": case "ramSplat": case "yank": case "ambush": case "plowCatch": S("hit_heavy", 0.5, ev, Bus.Sfx, 0, 1, 3); return false;
              case "ramBonk": S("clang", 0.45, ev, Bus.Sfx, 0, 1, 2); return false;
              case "playerHit": return S("hurt", ev.heavy ? 0.7 : 0.55, ev, Bus.Sfx, 0, 0.8, 3);
              // parries and blocks keep their synthesized tones (the grades must stay unmistakable); the samples add
              // the impact under them
              case "parry": S(ev.perfect ? "parry_perfect" : "parry", ev.perfect ? 0.7 : 0.5, ev, ev.perfect ? Bus.Big : Bus.Sfx, 0, 0, 3); return false;
              case "deflect": S(ev.perfect ? "parry_perfect" : "parry", ev.perfect ? 0.5 : 0.35, ev, Bus.Sfx, 0, 0.3, 3); return false;
              case "perfectGuard": S("parry_perfect", 0.55, ev, Bus.Sfx, 0, 0, 2); return false;
              case "nshieldBlock": case "guardBlock": case "barrierBlock": S("block", ev.heavy ? 0.55 : 0.4, ev, Bus.Sfx, 0, 0.8, 4); return false;
              case "aegisHit": S("block", 0.35, ev, Bus.Sfx, 0, 1, 3, 1.2); return false;
              case "nshieldBreak": case "rampartBreak": case "wallDown":
                if (ev.type == "wallDown" && !ev.broken) return false;
                S("shatter", 0.55, ev); Debris(ev, 3); return false;
              case "gadgetEnd": if (ev.why == "broken") { S("shatter", 0.45, ev); Debris(ev, 3); } return false;
              // movement
              case "dash":
                if (ev.level >= 2) { if (!S("whoosh_heavy", 0.55 + 0.1 * (ev.level - 2), ev, Bus.Sfx, 0, 0.6, 2)) return false; return ev.level < 3; }   // level 3 keeps the synth's sub boom too
                return S("whoosh", 0.45 + 0.1 * ev.level, ev, Bus.Sfx, 0, 1.2, 3);
              case "dodge": return S("whoosh", 0.4, ev, Bus.Sfx, 0, 1.5, 3, 1.1);
              case "dashSlash": S("whoosh_heavy", 0.45, ev, Bus.Sfx, 0, 1, 2); return false;
              case "swing":
              {
                var id = ev.id ?? "";
                bool big = id == "nova_rise" || id == "nova_k3" || System.Text.RegularExpressions.Regex.IsMatch(id, "b4|charged|riposte|ab3|g3|air3");
                return S(big ? "whoosh" : "swing", big ? 0.4 : 0.35, ev, Bus.Sfx, 0, 1.5, 3, big ? 0.85 : 1);
              }
              case "land":
                if (ev.vy < -16) return S("land_heavy", 0.45 + 0.25 * System.Math.Min(1, (-ev.vy - 16) / 12), ev, Bus.Sfx, 0, 0.8, 2);
                return S("land", 0.32, ev, Bus.Sfx, 0, 1.2, 3);
              // debris
              case "bounce": return S("debris", 0.25 * System.Math.Min(1, Or(ev.sp, 5) / 12), ev, Bus.Sfx, 0, 2, 4, 0.7);
              case "scrap": Debris(ev, 3, 0.2, 0.15); return true;
            }
            return false;
        }

        // ---- the sustained beams: start, follow and stop with what the simulation is doing (the same tests as
        // BeamFx, UltFx and the boss lasers' effects)
        sealed class LVoice { public BufferSource src; public Gain g; public Panner pan; }
        readonly Dictionary<Player, LVoice> beamL = new Dictionary<Player, LVoice>(), beamHit = new Dictionary<Player, LVoice>(), novaL = new Dictionary<Player, LVoice>();
        readonly Dictionary<Enemy, LVoice> laserL = new Dictionary<Enemy, LVoice>();
        readonly Dictionary<Enemy, SVoice> laserCharge = new Dictionary<Enemy, SVoice>();
        readonly HashSet<Player> novaFired = new HashSet<Player>();
        static readonly List<Player> dropP = new List<Player>();
        static readonly List<Enemy> dropE = new List<Enemy>();

        LVoice StartLoop(string name, double gain, double pan, Bus bus, double now, double fadeIn = 0.04)
        {
            var set = SampleBank.Set(name);
            if (set == null) return null;
            var s = set[0];
            var src = ctx.createBufferSource(s.data, s.rate); src.loop = true;
            var g = ctx.createGain(0); var pn = ctx.createStereoPanner(); pn.pan.value = pan;
            g.gain.setValueAtTime(0, now); g.gain.linearRampToValueAtTime(gain * LEVEL * Vol, now + fadeIn);
            src.connect(g); g.connect(pn); pn.connect(BusOf(bus)); src.start(now, R() * s.Duration * 0.5);
            return new LVoice { src = src, g = g, pan = pn };
        }
        void FollowLoop(LVoice v, double gain, double pan, double rate, double now)
        {
            v.g.gain.setTargetAtTime(gain * LEVEL * Vol, now, 0.05); v.pan.pan.setTargetAtTime(pan, now, 0.06); v.src.playbackRate.setTargetAtTime(rate, now, 0.08);
        }
        static void StopLoop(LVoice v, double now, double fade) { v.g.gain.cancelScheduledValues(now); v.g.gain.setTargetAtTime(0, now, fade); v.src.stop(now + fade * 8); }
        void Sync<TK>(Dictionary<TK, LVoice> map, TK k, bool on, string name, double gain, double pan, double rate, Bus bus, double now, double fade)
        {
            map.TryGetValue(k, out var v);
            if (on && v == null && (v = StartLoop(name, gain, pan, bus, now)) != null) map[k] = v;
            if (on && v != null) FollowLoop(v, gain, pan, rate, now);
            else if (!on && v != null) { StopLoop(v, now, fade); map.Remove(k); }
        }

        // True when the designed loops cover the beams (else Loops keeps the synthesized roar)
        bool DesignedBeams => DesignedOn && SampleBank.Has("beam_loop") && SampleBank.Has("nova_loop");

        void DesignedLoops(World world, List<Player> players, double now, double vol)
        {
            bool on = DesignedBeams && vol > 0;
            foreach (var p in players)
            {
                var (pan, lvl) = PlaceAt(p.x, p.y + p.h / 2);
                // Level 4 beam: the roar, rising a little as it runs, and the sizzle where it lands
                bool beam = on && p.state == "beam" && p.beam != null;
                double k = beam ? System.Math.Min(1, p.beam.t / MARKSMAN.beam.ticks) : 0;
                Sync(beamL, p, beam, "beam_loop", 0.5 * lvl, pan, 1 + 0.06 * k, Bus.Loops, now, 0.06);
                var (hp, hl) = beam ? PlaceAt(p.x + p.beam.dx * 12, p.y + p.h / 2 + p.beam.dy * 12) : (0, 1);
                Sync(beamHit, p, beam, "beam_impact_loop", 0.22 * hl, hp, 1, Bus.Sfx, now, 0.05);
                // Supernova: ignition when the beam opens, the loop while it burns
                var U = p.ultRun; bool nova = on && p.state == "ult" && U != null && U.kind == "nova" && U.segs != null;
                if (nova && novaFired.Add(p)) S("nova_fire", 1.0, null, Bus.Big, 0, 0, 1);
                if (!(p.state == "ult" && U != null && U.kind == "nova")) novaFired.Remove(p);
                double nk = nova ? System.Math.Max(0, System.Math.Min(1, (U.t - ULT.nova.gather) / ULT.nova.beam)) : 0;
                Sync(novaL, p, nova, "nova_loop", 0.75, pan * 0.5, 0.97 + 0.08 * nk, Bus.Loops, now, 0.12);
            }
            dropP.Clear(); foreach (var p in beamL.Keys) if (!players.Contains(p)) dropP.Add(p);
            foreach (var p in novaL.Keys) if (!players.Contains(p)) dropP.Add(p);
            foreach (var p in dropP) { Sync(beamL, p, false, null, 0, 0, 1, Bus.Loops, now, 0.05); Sync(beamHit, p, false, null, 0, 0, 1, Bus.Sfx, now, 0.05); Sync(novaL, p, false, null, 0, 0, 1, Bus.Loops, now, 0.1); novaFired.Remove(p); }

            // Boss lasers: the charge through the wind-up, then the beam's loop while it fires
            dropE.Clear(); foreach (var e in laserL.Keys) dropE.Add(e); foreach (var e in laserCharge.Keys) if (!dropE.Contains(e)) dropE.Add(e);
            if (world != null && on)
                foreach (var e in world.enemies)
                {
                    var A = e.atk;
                    if (!e.boss || e.dead || A == null) continue;
                    var (pan, lvl) = PlaceAt(e.x, e.y + e.h / 2);
                    bool charging = e.state == "windup" && (A.kind == "laser" || A.kind == "sweep"), live = e.state == "laser";
                    if (charging && !laserCharge.ContainsKey(e)) laserCharge[e] = Fire("laser_charge", 0.6 * lvl, pan, Bus.Sfx, 0, 0, 2, 1);
                    if (live && laserCharge.TryGetValue(e, out var cv) && cv != null && cv.end > now) Cut(cv, 0.02);
                    if (charging || live) dropE.Remove(e);
                    if (!charging && !live) continue;
                    if (live) Sync(laserL, e, true, "laser_loop", 0.5 * lvl, pan, 1, Bus.Loops, now, 0.08);
                    else if (laserL.ContainsKey(e)) Sync(laserL, e, false, null, 0, 0, 1, Bus.Loops, now, 0.08);
                }
            foreach (var e in dropE)
            {
                if (laserCharge.TryGetValue(e, out var cv) && cv != null && cv.end > now) Cut(cv, 0.05);
                laserCharge.Remove(e); Sync(laserL, e, false, null, 0, 0, 1, Bus.Loops, now, 0.08);
            }
        }
    }
}
