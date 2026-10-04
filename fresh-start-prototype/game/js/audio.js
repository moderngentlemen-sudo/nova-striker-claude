// Synthesized placeholder audio. Timing cues matter more than fidelity here: every threat
// category and both parry grades have a distinct, unmistakable sound.
import { SETTINGS, MARKSMAN, NOVA, DASH_CHARGE, HUNTER, POUND, ULT, RAM, FIX } from './config.js';
import { marksman, rifleFocus } from './player.js';

// The pitch each secondary weapon hums at while it charges (and chimes at when selected)
const SUB_HUM = { scatter: 130, grenade: 110, chain: 260, disc: 180, well: 70 };

// How far along a player's current charge is (0-1), or -1 when nothing is charging. `perfect` marks the
// Perfect Release window.
function chargeOf(p) {
  if (p.state === 'dashCharge' && p.dashChargeT >= DASH_CHARGE.tap) return { k: Math.min(1, p.dashChargeT / DASH_CHARGE.charge[2]), kind: 'dash' };
  if (p.state === 'pound' && p.pound && p.pound.phase === 'hold' && p.pound.held && p.pound.t > POUND.windup) return { k: Math.min(1, p.pound.t / POUND.charge[2]), kind: 'pound' };
  if (p.char === 'echo') return p.rifleT >= HUNTER.rifle.raise ? { k: rifleFocus(p.rifleT), kind: 'rifle' } : { k: -1 };
  if (p.char === 'ram') return p.chargeT > 0 ? { k: Math.min(1, p.chargeT / RAM.cannon.charge[2]), kind: 'cannon' } : { k: -1 };
  if (p.char === 'fix') return p.chargeT > 0 ? { k: Math.min(1, p.chargeT / FIX.rivet.charge[2]), kind: 'rivet' } : { k: -1 };
  if (marksman(p)) {
    const C = MARKSMAN.charge, B = MARKSMAN.burst.charge, L4 = MARKSMAN.beam.at;
    if (p.chargeT > 0) return { k: Math.min(1, p.chargeT / C[2]), kind: 'shot', perfect: p.chargeT >= C[2] && p.chargeT < C[2] + MARKSMAN.perfectWindow,
      l4: p.chargeT > C[2] ? Math.min(1, (p.chargeT - C[2]) / (L4 - C[2])) : 0 };
    if (p.burstT > 0) return { k: Math.min(1, p.burstT / B[2]), kind: 'burst', sub: p.sub, perfect: p.burstT >= B[2] && p.burstT < B[2] + MARKSMAN.burst.perfectWindow };
    return { k: -1 };
  }
  return p.chargeT > 0 ? { k: Math.min(1, p.chargeT / NOVA.charge2), kind: 'shot' } : { k: -1 };
}

export class Sound {
  constructor() { this.ctx = null; this.last = {}; }

  unlock() {
    try {
      if (!this.ctx) {
        const AC = window.AudioContext || window.webkitAudioContext;
        if (!AC) return;
        this.ctx = new AC();
        // A gentle compressor keeps stacked blasts (rocket jump + splash + hits) from clipping
        this.comp = this.ctx.createDynamicsCompressor();
        this.comp.threshold.value = -12; this.comp.knee.value = 8; this.comp.ratio.value = 5; this.comp.attack.value = 0.003; this.comp.release.value = 0.2;
        this.master = this.ctx.createGain(); this.master.connect(this.comp); this.comp.connect(this.ctx.destination);
        const len = this.ctx.sampleRate * 0.5, buf = this.ctx.createBuffer(1, len, this.ctx.sampleRate), d = buf.getChannelData(0);
        for (let i = 0; i < len; i++) d[i] = Math.random() * 2 - 1;
        this.noiseBuf = buf;
      }
      if (this.ctx.state === 'suspended') this.ctx.resume();
    } catch (e) { this.ctx = null; }
  }

  tone(f, f2, dur, type = 'sine', gain = 0.1, delay = 0) {
    if (!this.ctx) return;
    const c = this.ctx, t0 = c.currentTime + delay, o = c.createOscillator(), g = c.createGain();
    o.type = type; o.frequency.setValueAtTime(f, t0);
    if (f2) o.frequency.exponentialRampToValueAtTime(Math.max(20, f2), t0 + dur);
    g.gain.setValueAtTime(0.0001, t0); g.gain.exponentialRampToValueAtTime(gain * SETTINGS.volume, t0 + 0.006);
    g.gain.exponentialRampToValueAtTime(0.0001, t0 + dur);
    o.connect(g); g.connect(this.master); o.start(t0); o.stop(t0 + dur + 0.02);
  }

  noise(dur, freq, gain = 0.1, type = 'bandpass', f2 = 0, delay = 0) {
    if (!this.ctx) return;
    const c = this.ctx, t0 = c.currentTime + delay, s = c.createBufferSource(), fl = c.createBiquadFilter(), g = c.createGain();
    s.buffer = this.noiseBuf; fl.type = type; fl.frequency.setValueAtTime(freq, t0);
    if (f2) fl.frequency.exponentialRampToValueAtTime(f2, t0 + dur);
    g.gain.setValueAtTime(gain * SETTINGS.volume, t0); g.gain.exponentialRampToValueAtTime(0.0001, t0 + dur);
    s.connect(fl); fl.connect(g); g.connect(this.master); s.start(t0); s.stop(t0 + dur + 0.02);
  }

  rising(dur) {
    if (!this.ctx) return;
    const c = this.ctx, t0 = c.currentTime, o = c.createOscillator(), g = c.createGain(), lfo = c.createOscillator(), lg = c.createGain();
    o.type = 'sawtooth'; o.frequency.setValueAtTime(180, t0); o.frequency.exponentialRampToValueAtTime(900, t0 + dur);
    lfo.frequency.value = 14; lg.gain.value = 0.03 * SETTINGS.volume; lfo.connect(lg); lg.connect(g.gain);
    g.gain.setValueAtTime(0.0001, t0); g.gain.exponentialRampToValueAtTime(0.06 * SETTINGS.volume, t0 + 0.05);
    g.gain.setValueAtTime(0.06 * SETTINGS.volume, t0 + dur - 0.02); g.gain.exponentialRampToValueAtTime(0.0001, t0 + dur + 0.05);
    o.connect(g); g.connect(this.master); o.start(t0); lfo.start(t0); o.stop(t0 + dur + 0.08); lfo.stop(t0 + dur + 0.08);
  }

  // Light-booster hiss: one looping noise voice per player while the boosters fire
  jet(p, on) {
    if (!this.ctx) return;
    this.jets = this.jets || new Map();
    const cur = this.jets.get(p);
    if (!on) { if (cur) { const t = this.ctx.currentTime; cur.g.gain.setTargetAtTime(0.0001, t, 0.04); cur.s.stop(t + 0.3); this.jets.delete(p); } return; }
    if (cur || SETTINGS.volume <= 0) return;
    const c = this.ctx, s = c.createBufferSource(), f = c.createBiquadFilter(), g = c.createGain();
    s.buffer = this.noiseBuf; s.loop = true; f.type = 'bandpass'; f.frequency.value = 2400; f.Q.value = 0.9;
    g.gain.setValueAtTime(0.0001, c.currentTime); g.gain.exponentialRampToValueAtTime(0.07 * SETTINGS.volume, c.currentTime + 0.05);
    s.connect(f); f.connect(g); g.connect(this.master); s.start();
    this.jets.set(p, { s, g });
  }

  // Continuous voices, retuned every frame: a hum while a charge builds (it climbs in pitch and
  // flutters in the Perfect Release window) and the grind of a wall slide (Nova's skate blades hiss).
  update(world) {
    if (!this.ctx) return;
    this.hums = this.hums || new Map(); this.grinds = this.grinds || new Map();
    const c = this.ctx, now = c.currentTime, vol = SETTINGS.volume, seen = new Set();
    for (const p of world.players) {
      seen.add(p);
      const ch = p.state === 'downed' || p.state === 'dead' ? { k: -1 } : chargeOf(p);
      let h = this.hums.get(p);
      if (ch.k >= 0 && vol > 0) {
        if (!h) {
          const o1 = c.createOscillator(), o2 = c.createOscillator(), lfo = c.createOscillator(), lg = c.createGain(), g = c.createGain(), f = c.createBiquadFilter();
          o1.type = 'sine'; o2.type = 'triangle'; lfo.type = 'sine'; f.type = 'lowpass'; f.frequency.value = 2400;
          lfo.connect(lg); lg.connect(g.gain); o1.connect(f); o2.connect(f); f.connect(g); g.connect(this.master);
          g.gain.setValueAtTime(0.0001, now); o1.start(); o2.start(); lfo.start();
          h = { o1, o2, lfo, lg, g }; this.hums.set(p, h);
        }
        const base = ch.kind === 'dash' ? (p.char === 'ram' ? 55 : 90) : ch.kind === 'rifle' ? 240 : ch.kind === 'burst' ? SUB_HUM[ch.sub] || 130 : ch.kind === 'pound' ? 70
          : ch.kind === 'cannon' ? 62 : ch.kind === 'rivet' ? 300 : 110;
        const l4 = ch.l4 || 0, f0 = base * (1 + 2.2 * ch.k) * (ch.perfect ? 2 : 1) * (1 + 0.6 * l4);
        h.o1.frequency.setTargetAtTime(f0, now, 0.03); h.o2.frequency.setTargetAtTime(f0 * (l4 >= 1 ? 2 : 1.5), now, 0.03);
        h.lfo.frequency.setTargetAtTime(ch.perfect || l4 >= 1 ? 26 : 5 + 10 * ch.k + 10 * l4, now, 0.05);
        const gain = (0.012 + 0.03 * ch.k) * vol * (ch.kind === 'rifle' ? 0.6 : 1);
        h.g.gain.setTargetAtTime(gain, now, 0.04); h.lg.gain.setTargetAtTime(gain * (ch.perfect ? 0.8 : 0.3), now, 0.04);
      } else if (h) this.stopHum(p);
      // Wall slide grind
      let w = this.grinds.get(p);
      if (p.wallSliding && vol > 0) {
        if (!w) {
          const src = c.createBufferSource(), f = c.createBiquadFilter(), g = c.createGain();
          src.buffer = this.noiseBuf; src.loop = true; f.type = 'bandpass';
          f.frequency.value = p.char === 'nova' && SETTINGS.novaKit === 'marksman' ? 3600 : 1100; f.Q.value = p.char === 'nova' ? 1.6 : 0.8;
          g.gain.setValueAtTime(0.0001, now); src.connect(f); f.connect(g); g.connect(this.master); src.start();
          w = { src, g }; this.grinds.set(p, w);
        }
        w.g.gain.setTargetAtTime((0.015 + 0.035 * Math.min(1, -p.vy / 6)) * vol, now, 0.05);
      } else if (w) { w.g.gain.setTargetAtTime(0.0001, now, 0.04); w.src.stop(now + 0.25); this.grinds.delete(p); }
    }
    for (const p of [...this.hums.keys()]) if (!seen.has(p)) this.stopHum(p);
    this.loops(world, now, vol);
  }
  // Two looping voices: the Level 4 beam's roar (a detuned buzz and rushing noise, wobbling) and the Aegis'
  // glassy hum, which drops in pitch and starts to waver as the shield weakens
  loops(world, now, vol) {
    const c = this.ctx; this.beamV = this.beamV || new Map(); this.aegisV = this.aegisV || new Map(); this.wellV = this.wellV || new Map();
    const want = new Set(), wantA = new Set(), wantW = new Set();
    // Each open gravity well: a deep pulsing drone that quickens as it nears its collapse
    for (const w of world.wells || []) {
      if (w.phase !== 'open' || vol <= 0) continue;
      wantW.add(w);
      let v = this.wellV.get(w);
      if (!v) {
        const o1 = c.createOscillator(), o2 = c.createOscillator(), g = c.createGain(), lfo = c.createOscillator(), lg = c.createGain();
        o1.type = 'sine'; o2.type = 'triangle'; o1.frequency.value = 52; o2.frequency.value = 104; lfo.frequency.value = 5; lg.gain.value = 0.02 * vol;
        lfo.connect(lg); lg.connect(g.gain); o1.connect(g); o2.connect(g); g.connect(this.master);
        g.gain.setValueAtTime(0.0001, now); g.gain.exponentialRampToValueAtTime(0.06 * vol, now + 0.1); o1.start(); o2.start(); lfo.start();
        v = { o1, o2, lfo, g }; this.wellV.set(w, v);
      }
      const left = Math.max(0, w.life - w.t) / w.life;
      v.lfo.frequency.setTargetAtTime(5 + 14 * (1 - left), now, 0.1); v.o2.frequency.setTargetAtTime(104 + 40 * (1 - left), now, 0.1);
    }
    for (const p of world.players) {
      // Supernova: the Level 4 roar, an octave down, far louder and wider
      if (p.state === 'ult' && p.ultRun && p.ultRun.segs && vol > 0) {
        want.add(p);
        let v = this.beamV.get(p);
        if (!v) v = this.beamVoice(p, now, vol, 46, 69, 0.13);
        const k = (p.ultRun.t - ULT.nova.gather) / ULT.nova.beam;
        v.o1.frequency.setTargetAtTime(44 + 18 * k, now, 0.05); v.o2.frequency.setTargetAtTime(66 + 26 * k, now, 0.05);
        continue;
      }
      if (p.state === 'beam' && p.beam && vol > 0) {
        want.add(p);
        let v = this.beamV.get(p);
        if (!v) v = this.beamVoice(p, now, vol, 92, 139, 0.07);
        const k = p.beam.t / MARKSMAN.beam.ticks;
        v.o1.frequency.setTargetAtTime(80 + 30 * k, now, 0.05); v.o2.frequency.setTargetAtTime(120 + 45 * k, now, 0.05);
      }
      if (p.aegis && vol > 0 && p.state !== 'dead' && p.state !== 'downed') {
        wantA.add(p);
        let a = this.aegisV.get(p);
        if (!a) {
          const o1 = c.createOscillator(), o2 = c.createOscillator(), g = c.createGain(), lfo = c.createOscillator(), lg = c.createGain();
          o1.type = 'sine'; o2.type = 'triangle'; lfo.frequency.value = 3; lg.gain.value = 0.004 * vol; lfo.connect(lg); lg.connect(g.gain);
          o1.connect(g); o2.connect(g); g.connect(this.master); g.gain.setValueAtTime(0.0001, now); g.gain.exponentialRampToValueAtTime(0.014 * vol, now + 0.1);
          o1.start(); o2.start(); lfo.start(); a = { o1, o2, lfo, g }; this.aegisV.set(p, a);
        }
        const frac = p.aegis.hp / p.aegis.max;
        a.o1.frequency.setTargetAtTime(420 + 240 * frac, now, 0.08); a.o2.frequency.setTargetAtTime(632 + 360 * frac, now, 0.08);
        a.lfo.frequency.setTargetAtTime(frac < 0.35 ? 13 : 3, now, 0.1);
      }
    }
    // Fix's Patch Beam: a bright buzzing hum with a crackle in it; RAM's charges: an engine's roar
    this.patchV = this.patchV || new Map(); this.rushV = this.rushV || new Map();
    const wantP = new Set(), wantR = new Set();
    for (const p of world.players) {
      if (p.state === 'patch' && p.patch && vol > 0) {
        wantP.add(p);
        let v = this.patchV.get(p);
        if (!v) {
          const o1 = c.createOscillator(), o2 = c.createOscillator(), n = c.createBufferSource(), nf = c.createBiquadFilter(), g = c.createGain(), lfo = c.createOscillator(), lg = c.createGain();
          o1.type = 'sine'; o2.type = 'triangle'; o1.frequency.value = 520; o2.frequency.value = 780; n.buffer = this.noiseBuf; n.loop = true; nf.type = 'highpass'; nf.frequency.value = 4000;
          lfo.frequency.value = 17; lg.gain.value = 0.006 * vol; lfo.connect(lg); lg.connect(g.gain);
          o1.connect(g); o2.connect(g); n.connect(nf); nf.connect(g); g.connect(this.master);
          g.gain.setValueAtTime(0.0001, now); g.gain.exponentialRampToValueAtTime(0.018 * vol, now + 0.08); o1.start(); o2.start(); n.start(); lfo.start();
          v = { o1, o2, n, lfo, g }; this.patchV.set(p, v);
        }
        const self = p.patch.self || !p.patch.target, down = p.patch.target && p.patch.target.state === 'downed';
        v.o1.frequency.setTargetAtTime(self ? 380 : down ? 660 : 520, now, 0.08); v.o2.frequency.setTargetAtTime(self ? 570 : down ? 990 : 780, now, 0.08);
      }
      const rushing = (p.state === 'rush' && p.rush) || (p.state === 'ult' && p.ultRun && p.ultRun.kind === 'ram' && p.ultRun.t > ULT.ram.brace && !p.ultRun.slamT);
      if (rushing && vol > 0) {
        wantR.add(p);
        let v = this.rushV.get(p);
        if (!v) v = this.beamVoice(p, now, vol, 38, 57, 0.06, this.rushV);
        const L = p.rush ? p.rush.level : 3;
        v.o1.frequency.setTargetAtTime(38 + 6 * L, now, 0.05); v.o2.frequency.setTargetAtTime(57 + 9 * L, now, 0.05);
      }
    }
    const stop = (map, p, fade = 0.05) => { const v = map.get(p); v.g.gain.setTargetAtTime(0.0001, now, fade); for (const k of ['o1', 'o2', 'n', 'lfo']) if (v[k]) v[k].stop(now + 0.3); map.delete(p); };
    for (const p of [...this.patchV.keys()]) if (!wantP.has(p)) stop(this.patchV, p, 0.04);
    for (const p of [...this.rushV.keys()]) if (!wantR.has(p)) stop(this.rushV, p, 0.08);
    for (const p of [...this.beamV.keys()]) if (!want.has(p)) stop(this.beamV, p, 0.06);
    for (const p of [...this.aegisV.keys()]) if (!wantA.has(p)) stop(this.aegisV, p, 0.03);
    for (const w of [...this.wellV.keys()]) if (!wantW.has(w)) stop(this.wellV, w, 0.05);
  }
  // The beam's roar: a detuned buzz and rushing noise, wobbling
  beamVoice(p, now, vol, f1, f2, gain, into = this.beamV) {
    const c = this.ctx, o1 = c.createOscillator(), o2 = c.createOscillator(), n = c.createBufferSource(), f = c.createBiquadFilter(), nf = c.createBiquadFilter(), g = c.createGain(), lfo = c.createOscillator(), lg = c.createGain();
    o1.type = 'sawtooth'; o2.type = 'square'; o1.frequency.value = f1; o2.frequency.value = f2; n.buffer = this.noiseBuf; n.loop = true;
    f.type = 'lowpass'; f.frequency.value = 1400; nf.type = 'bandpass'; nf.frequency.value = 2600; nf.Q.value = 0.7;
    lfo.frequency.value = 11; lg.gain.value = gain * 0.28 * vol; lfo.connect(lg); lg.connect(g.gain);
    o1.connect(f); o2.connect(f); n.connect(nf); nf.connect(g); f.connect(g); g.connect(this.master);
    g.gain.setValueAtTime(0.0001, now); g.gain.exponentialRampToValueAtTime(gain * vol, now + 0.06);
    o1.start(); o2.start(); n.start(); lfo.start();
    const v = { o1, o2, n, lfo, g }; into.set(p, v); return v;
  }
  stopHum(p) {
    const h = this.hums.get(p); if (!h) return;
    const t = this.ctx.currentTime; h.g.gain.setTargetAtTime(0.0001, t, 0.03);
    for (const o of [h.o1, h.o2, h.lfo]) o.stop(t + 0.2);
    this.hums.delete(p);
  }

  limit(key, gap) {
    const now = this.ctx ? this.ctx.currentTime : 0;
    if (this.last[key] && now - this.last[key] < gap) return false;
    this.last[key] = now; return true;
  }

  play(ev) {
    if (!this.ctx || SETTINGS.volume <= 0) return;
    switch (ev.type) {
      case 'jump': if (this.limit('jump', 0.05)) this.tone(280, 460, 0.09, 'triangle', 0.06); break;
      case 'djump': this.tone(380, 640, 0.09, 'triangle', 0.06); break;
      case 'walljump':
        if (ev.climb) { this.noise(0.05, 1800, 0.06); this.tone(360, 560, 0.07, 'triangle', 0.05); }
        else { this.noise(0.08, 1400, 0.08); this.tone(300, 620, 0.1, 'triangle', 0.055); this.tone(130, 70, 0.08, 'sine', 0.08); }
        break;
      case 'wallSlide': if (ev.on && this.limit('wallon', 0.1)) this.noise(0.06, 2400, 0.05, 'bandpass', 900); break;
      case 'dash': {
        const L = ev.level || 0;
        if (!L) { this.noise(0.16, 700, 0.12, 'bandpass', 2600); break; }
        this.noise(0.24 + 0.05 * L, 500, 0.14 + 0.04 * L, 'bandpass', 3000 + 600 * L);
        this.tone(170 - 20 * L, 60, 0.25, 'sawtooth', 0.04 + 0.02 * L);
        if (L >= 3) { this.tone(75, 34, 0.32, 'sine', 0.22); this.noise(0.08, 4200, 0.1, 'highpass'); }
        break;
      }
      case 'dashChargeStart': this.tone(220, 330, 0.1, 'triangle', 0.025); break;
      case 'dashLevel': this.tone([0, 523, 659, 880][ev.level] || 659, 0, 0.1, 'triangle', 0.06); if (ev.level === 3) this.tone(1320, 0, 0.1, 'sine', 0.04, 0.05); break;
      case 'rifleRaise': this.tone(1800, 0, 0.02, 'square', 0.03); this.tone(1200, 0, 0.03, 'square', 0.025, 0.04); break;
      case 'rifleFocus': this.tone(1760, 0, 0.18, 'sine', 0.06); this.tone(2637, 0, 0.12, 'sine', 0.035, 0.03); break;
      case 'rifleLower': this.tone(900, 0, 0.03, 'square', 0.02); break;
      case 'snipe': {
        // A sharp crack, a deep report, the supersonic whip down the line, a rolling tail; then the bolt cycles
        const f = ev.f || 0;
        this.noise(0.05, 5200, 0.2 + 0.1 * f, 'highpass'); this.tone(95 + 30 * f, 38, 0.3 + 0.15 * f, 'sine', 0.16 + 0.1 * f);
        this.tone(900, 180, 0.09, 'square', 0.05); this.noise(0.25, 3200, 0.08 + 0.05 * f, 'bandpass', 700, 0.01);
        this.noise(0.5 + 0.3 * f, 900, 0.1 + 0.05 * f, 'lowpass', 150, 0.02);
        if (ev.full) { this.tone(2400, 1100, 0.3, 'sine', 0.045); this.tone(260, 70, 0.32, 'sawtooth', 0.06); }
        this.tone(1900, 0, 0.025, 'square', 0.035, 0.34); this.noise(0.03, 3000, 0.06, 'bandpass', 0, 0.34);
        this.tone(1500, 0, 0.03, 'square', 0.035, 0.5); this.noise(0.04, 2400, 0.07, 'bandpass', 0, 0.5);
        break;
      }
      case 'crit': this.tone(2637, 0, 0.1, 'sine', 0.06); this.tone(3520, 0, 0.08, 'square', 0.025, 0.02); break;
      case 'deflect':
        // Staff meets shot: a bright metallic ting (brighter and ringing on a perfect)
        this.tone(2350, 0, ev.perfect ? 0.3 : 0.14, 'sine', 0.08); this.tone(3525, 0, ev.perfect ? 0.24 : 0.1, 'sine', 0.045);
        this.noise(0.03, 6000, 0.08, 'highpass'); if (ev.perfect) this.tone(1175, 2350, 0.12, 'triangle', 0.04);
        break;
      case 'dashSlash': this.noise(0.18, 1800, 0.14 + 0.04 * ev.tier, 'bandpass', 6500); this.tone(1100, 2400, 0.1, 'sine', 0.05); this.tone(140, 60, 0.14, 'sine', 0.06 + 0.03 * ev.tier); break;
      case 'crescent': this.tone(300, 900, 0.22, 'sawtooth', 0.05); this.noise(0.25, 900, 0.1, 'bandpass', 4200); this.tone(1800, 1200, 0.18, 'sine', 0.03); break;
      case 'pogo': this.tone(320, 900, 0.1, 'triangle', 0.07); this.noise(0.06, 2400, 0.05); break;
      case 'poundStart': this.noise(0.22, 3000, 0.1, 'bandpass', 400); this.tone(520, 260, 0.2, 'triangle', 0.04); break;
      case 'poundLevel': this.tone([0, 392, 523, 659][ev.level] || 523, 0, 0.12, 'triangle', 0.07); if (ev.level === 3) this.tone(1047, 0, 0.12, 'sine', 0.05, 0.05); break;
      case 'poundDrop': this.tone(1500, 380, 0.32, 'sine', 0.05); this.noise(0.3, 700, 0.08 + 0.02 * ev.level, 'bandpass', 3000); break;
      case 'poundLand': {
        const L = ev.level || 0;
        this.tone(88 - 6 * L, 30, 0.35 + 0.08 * L, 'sine', 0.2 + 0.05 * L); this.noise(0.3 + 0.1 * L, 700, 0.18 + 0.05 * L, 'lowpass', 120);
        this.noise(0.06, 4200, 0.1 + 0.03 * L, 'highpass'); this.tone(200, 60, 0.2, 'sawtooth', 0.05 + 0.02 * L);
        if (L >= 3) { this.tone(55, 28, 0.6, 'sine', 0.2); this.noise(0.7, 400, 0.12, 'lowpass', 90, 0.05); }
        break;
      }
      // Nova: the Level 4 beam and the Aegis
      case 'beamStart': this.tone(1760, 440, 0.3, 'sawtooth', 0.05); this.noise(0.35, 2000, 0.16, 'bandpass', 500); this.tone(70, 35, 0.4, 'sine', 0.2); break;
      case 'beamEnd': this.tone(420, 90, 0.35, 'sawtooth', 0.04); this.noise(0.3, 1500, 0.06, 'lowpass', 200); break;
      case 'erase': if (this.limit('erase', 0.05)) this.noise(0.05, 3500, 0.05, 'highpass'); break;
      case 'aegisOn': this.tone(660, 1320, 0.2, 'sine', 0.06); this.tone(990, 1980, 0.24, 'triangle', 0.035, 0.03); this.noise(0.25, 5000, 0.05, 'highpass', 2000); break;
      case 'aegisHit': {
        // Glass under strain: a ping that drops with the shield's strength, and a crackle as it weakens
        const fr = ev.frac ?? 1;
        if (this.limit('aegisHit', 0.03)) { this.tone(1900 + 1400 * fr, 0, 0.12, 'triangle', 0.07); this.noise(0.04, 6000, 0.08, 'highpass'); }
        if (fr < 0.6) for (let i = 0; i < 3; i++) this.tone(2400 + Math.random() * 2400, 0, 0.03, 'square', 0.02, 0.02 + i * 0.025);
        this.tone(160, 90, 0.1, 'sine', 0.06);
        break;
      }
      case 'aegisOff':
        if (ev.why === 'break') {
          // Shatter: a burst of glass, then tinkling shards
          this.noise(0.6, 4200, 0.2, 'highpass', 1800); this.tone(140, 50, 0.3, 'sine', 0.14);
          for (let i = 0; i < 9; i++) this.tone(2000 + Math.random() * 3000, 0, 0.06 + Math.random() * 0.06, 'triangle', 0.03, 0.03 + i * 0.045);
        } else if (ev.why === 'detonate') {
          this.tone(90, 32, 0.5, 'sine', 0.22); this.noise(0.45, 900, 0.22, 'lowpass', 150); this.tone(1320, 2640, 0.2, 'sine', 0.05); this.noise(0.3, 5000, 0.1, 'highpass');
        } else this.tone(1320, 440, 0.4, 'sine', 0.04);
        break;
      case 'lockOn': if (ev.why === 'auto') break;   // automatic lock-on is silent; the reticle shows it
        this.tone(1320, 0, 0.05, 'sine', 0.05); this.tone(1760, 0, 0.07, 'sine', 0.05, 0.05); break;
      case 'lockSwitch': this.tone(1560, 0, 0.05, 'sine', 0.045); break;
      case 'lockOff': this.tone(1320, 880, 0.08, 'sine', 0.035); break;
      case 'lockNone': this.tone(300, 0, 0.06, 'square', 0.025); break;
      case 'slide': this.noise(0.22, 500, 0.1, 'lowpass'); break;
      case 'land':
        if (!this.limit('land', 0.08)) break;
        if (ev.vy < -16) { const k = Math.min(1, (-ev.vy - 16) / 12); this.noise(0.14, 300, 0.12 + 0.1 * k, 'lowpass'); this.tone(85, 42, 0.18, 'sine', 0.12 + 0.1 * k); }
        else this.noise(0.05, 320, 0.08, 'lowpass');
        break;
      case 'shot':
        if (ev.bolt) { this.tone(720, 360, 0.07, 'triangle', 0.07); break; }
        if (ev.cannon) {
          // RAM's cannon: a deep boom, bigger with each level
          const L = ev.level; this.tone(150 - 15 * L, 45, 0.22 + 0.06 * L, 'sine', 0.16 + 0.04 * L); this.noise(0.16 + 0.05 * L, 700, 0.14 + 0.04 * L, 'lowpass', 160);
          if (L) this.tone(900, 300, 0.2, 'sawtooth', 0.04 + 0.01 * L);
          break;
        }
        if (ev.rivet) {
          if (ev.level) { this.tone(700, 300, 0.12, 'square', 0.05); this.noise(0.1, 2000, 0.07, 'bandpass', 600); }
          else if (this.limit('rivet', 0.03)) { this.tone(1400, 1000, 0.03, 'square', 0.03); this.noise(0.025, 5000, 0.04, 'highpass'); }
          break;
        }
        if (ev.attach === 'volley') { for (let i = 0; i < 3; i++) this.tone(1500, 900, 0.06, 'triangle', 0.05, i * 0.035); break; }
        if (ev.attach === 'arc') { this.tone(190, 110, 0.18, 'sine', 0.14); this.noise(0.1, 700, 0.08, 'lowpass'); break; }
        if (ev.attach === 'prism') { this.tone(2100, 1400, 0.16, 'sine', 0.06); this.tone(3150, 2100, 0.12, 'triangle', 0.03); break; }
        if (ev.level >= 2) this.tone(110, 42, 0.16 + 0.05 * ev.level, 'sine', 0.06 + 0.05 * (ev.level - 1));   // charged shots thump
        if (ev.level === 0) { if (this.limit('shot', 0.03)) { this.tone(900, 450, 0.05, 'square', 0.035); this.noise(0.03, 3000, 0.04); } }
        else if (ev.level === 1) { this.tone(540, 220, 0.16, 'sawtooth', 0.08); this.noise(0.1, 1500, 0.08); }
        else { this.tone(260, 80, 0.4, 'sawtooth', 0.12); this.tone(1700, 700, 0.2, 'sine', 0.05); this.noise(0.25, 900, 0.14, 'lowpass'); }
        break;
      case 'tracer': this.tone(1200, 1900, 0.1, 'sine', 0.06); break;
      case 'chargeLevel':
        if (ev.level >= 4) { for (const [f, d] of [[1568, 0], [2093, 0.04], [2637, 0.08], [3136, 0.12]]) this.tone(f, 0, 0.3, 'sine', 0.05, d); this.noise(0.3, 5000, 0.05, 'highpass', 2000); break; }
        this.tone([0, 660, 880, 1175][ev.level] || 990, 0, 0.14, 'sine', 0.07); if (ev.level === 3) this.tone(1760, 0, 0.1, 'sine', 0.04, 0.05);
        break;
      case 'burstLevel': this.tone([0, 440, 587, 784][ev.level] || 587, 0, 0.12, 'triangle', 0.07); break;
      case 'meleeCharged': this.tone(520, 1040, 0.12, 'sine', 0.06); break;
      case 'swing': {
        // Blades hiss, the glaive whooshes low and long, Nova's hard-light fists thump the air
        if (!this.limit('swing', 0.04)) break;
        const id = ev.id || '';
        if (id === 'nova_rise') { this.noise(0.32, 600, 0.13, 'bandpass', 3200); this.tone(150, 520, 0.26, 'sawtooth', 0.05); this.tone(90, 45, 0.15, 'sine', 0.1); }
        else if (id.startsWith('nova_k')) { this.noise(0.08, 900, 0.07, 'bandpass', 400); this.tone(220, 120, 0.07, 'triangle', 0.04); if (id === 'nova_k3') this.tone(90, 45, 0.15, 'sine', 0.1); }
        else if (id === 'echo_spin' || id === 'echo_rise') { for (let i = 0; i < 4; i++) this.noise(0.08, 1200 + i * 300, 0.06, 'bandpass', 700, i * 0.06); }
        else if (/b4|charged|riposte|ab3|g3|air3/.test(id)) { this.noise(0.16, 700, 0.09, 'bandpass', 2400); this.tone(180, 90, 0.12, 'sine', 0.05); }
        else this.noise(0.07, 2600, 0.06, 'bandpass', 5200);
        break;
      }
      case 'hit':
        if (!this.limit('hit', 0.025)) break;
        if (ev.heavy) { this.noise(0.12, 700, 0.22, 'lowpass'); this.tone(150, 60, 0.14, 'sine', 0.2); }
        else { this.noise(0.06, 1100, 0.14, 'lowpass'); this.tone(200, 100, 0.07, 'sine', 0.12); }
        break;
      case 'blocked': this.tone(1450, 0, 0.05, 'square', 0.04); this.tone(1950, 0, 0.07, 'square', 0.03, 0.01); break;
      case 'guardBreak': this.noise(0.25, 2000, 0.16, 'highpass'); this.tone(320, 140, 0.2, 'sawtooth', 0.1); break;
      case 'armorHit': if (this.limit('armor', 0.05)) this.tone(520, 480, 0.06, 'triangle', 0.08); break;
      case 'armorBreak': this.noise(0.35, 1200, 0.25, 'lowpass'); this.tone(200, 55, 0.35, 'sawtooth', 0.12); break;
      case 'spinStun': this.tone(990, 495, 0.18, 'triangle', 0.07); break;
      case 'stagger': this.tone(880, 440, 0.25, 'sine', 0.08); this.tone(1320, 660, 0.25, 'sine', 0.05); break;
      case 'kill': this.tone(300, 900, 0.1, 'triangle', 0.08); this.noise(0.08, 2500, 0.08); break;
      case 'parry':
        if (ev.perfect) { this.tone(1568, 0, 0.35, 'sine', 0.12); this.tone(2349, 0, 0.3, 'sine', 0.08); this.tone(3136, 0, 0.25, 'sine', 0.05); this.tone(1250, 0, 0.08, 'square', 0.05); }
        else { this.tone(1250, 0, 0.12, 'square', 0.07); this.tone(1870, 0, 0.1, 'square', 0.05); }
        break;
      case 'parryFail': this.tone(200, 110, 0.18, 'square', 0.07); break;
      case 'playerHit': this.tone(230, 110, 0.14, 'sawtooth', 0.14); this.noise(0.1, 800, 0.12, 'lowpass'); break;
      case 'telegraph':
        if (ev.cat === 'standard') this.tone(1760, 0, 0.05, 'sine', 0.09);
        else if (ev.cat === 'heavy') { this.tone(1320, 0, 0.06, 'sine', 0.1); this.tone(1320, 0, 0.06, 'sine', 0.1, 0.1); }
        else this.rising(ev.ticks / 60);
        break;
      case 'lock': this.tone(2100, 0, 0.09, 'sine', 0.08); break;
      case 'enemyShot': ev.heavy ? this.tone(420, 180, 0.18, 'sawtooth', 0.08) : this.tone(620, 300, 0.08, 'square', 0.04); break;
      case 'slam': this.noise(0.45, 220, 0.3, 'lowpass'); this.tone(70, 40, 0.35, 'sine', 0.25); break;
      case 'bulwark': this.tone(110, 50, 0.4, 'sine', 0.25); this.tone(880, 1320, 0.25, 'triangle', 0.05); this.noise(0.2, 400, 0.1, 'lowpass'); break;
      case 'barrierBlock': this.tone(1100, 0, 0.06, 'triangle', 0.06); break;
      case 'amplify': this.tone(1320, 1760, 0.08, 'sine', 0.05); break;
      case 'intercept': this.tone(1500, 700, 0.08, 'square', 0.05); this.noise(0.06, 2400, 0.06); break;
      case 'lash': this.noise(0.12, 2600, 0.1, 'bandpass', 900); this.tone(900, 300, 0.12, 'triangle', 0.05); break;
      case 'lashPull': case 'lashZip': this.tone(300, 700, 0.12, 'triangle', 0.07); break;
      case 'vbStart': this.noise(0.18, 420, 0.12 + ev.tier * 0.05, 'lowpass'); this.tone(95, 45, 0.2, 'sine', 0.1 + ev.tier * 0.05); break;
      case 'boost': this.tone(600, 1200, 0.15, 'sine', 0.07); break;
      case 'downed': this.tone(440, 220, 0.45, 'sine', 0.1); break;
      case 'revived': this.tone(784, 0, 0.18, 'sine', 0.08); this.tone(988, 0, 0.18, 'sine', 0.07, 0.08); this.tone(1175, 0, 0.25, 'sine', 0.06, 0.16); break;
      case 'recall': case 'respawn': case 'join': this.tone(520, 1040, 0.2, 'sine', 0.07); break;
      case 'banner': case 'checkpoint': this.tone(660, 0, 0.14, 'triangle', 0.06); this.tone(880, 0, 0.2, 'triangle', 0.06, 0.12); break;
      case 'wipe': this.tone(220, 110, 0.6, 'triangle', 0.1); break;
      case 'bark': this.tone(520, 0, 0.04, 'triangle', 0.03); this.tone(660, 0, 0.05, 'triangle', 0.03, 0.05); break;
      case 'snareThrow': this.noise(0.1, 1800, 0.07, 'bandpass', 900); this.tone(600, 900, 0.07, 'triangle', 0.05); break;
      case 'snarePlant': this.tone(300, 0, 0.05, 'square', 0.04); this.tone(900, 0, 0.06, 'sine', 0.05, 0.12); break;
      case 'snareTrigger': this.noise(0.15, 3000, 0.12, 'highpass'); this.tone(1200, 300, 0.15, 'sawtooth', 0.08); break;
      case 'snared': this.tone(440, 220, 0.14, 'square', 0.05); break;
      case 'leash': this.tone(180, 360, 0.2, 'triangle', 0.07); this.noise(0.08, 2000, 0.05); break;
      case 'leashEnd': this.tone(360, 200, 0.08, 'triangle', 0.03); break;
      case 'yank': this.noise(0.2, 420, 0.18, 'lowpass'); this.tone(120, 60, 0.2, 'sine', 0.18); this.noise(0.1, 2600, 0.08, 'bandpass', 900); break;
      // Scarf modes
      case 'scarfMode': ev.mode === 'veil' ? this.tone(760, 380, 0.14, 'sine', 0.05) : ev.mode === 'flare' ? (this.tone(440, 880, 0.12, 'sawtooth', 0.04), this.tone(660, 1320, 0.14, 'triangle', 0.04, 0.05)) : this.tone(520, 780, 0.1, 'triangle', 0.05); break;
      case 'veilOn': this.noise(0.3, 3000, 0.05, 'highpass', 800); this.tone(900, 450, 0.25, 'sine', 0.03); break;
      case 'veilBreak': if (ev.wasHidden) this.tone(700, 1400, 0.08, 'sine', 0.04); break;
      case 'vanish': this.noise(0.25, 4000, 0.1, 'bandpass', 600); this.tone(1200, 300, 0.2, 'sine', 0.05); break;
      case 'ambush': this.noise(0.18, 600, 0.2, 'lowpass'); this.tone(180, 60, 0.25, 'sine', 0.2); this.tone(2400, 1200, 0.06, 'square', 0.04); break;
      case 'challenge': this.tone(330, 660, 0.22, 'sawtooth', 0.07); this.tone(495, 990, 0.26, 'triangle', 0.06, 0.08); break;
      case 'lostTrack': if (this.limit('lost', 0.15)) this.tone(620, 520, 0.1, 'triangle', 0.03); break;
      // Nova, Marksman kit
      case 'attach': this.tone(1100, 0, 0.03, 'square', 0.03); this.tone({ lance: 660, volley: 880, arc: 520, prism: 1320 }[ev.attach] || 700, 0, 0.08, 'triangle', 0.05, 0.03); break;
      case 'perfectRelease': this.tone(1760, 0, 0.22, 'sine', 0.08); this.tone(2637, 0, 0.18, 'sine', 0.05, 0.02); break;
      case 'blast': this.noise(0.35, 500, 0.2 + (ev.perfect ? 0.06 : 0), 'lowpass'); this.tone(95, 40, 0.3, 'sine', 0.18); break;
      case 'split': this.tone(2600, 1800, 0.1, 'sine', 0.05); this.tone(3300, 0, 0.06, 'triangle', 0.03); break;
      case 'ricochet': if (this.limit('rico', 0.05)) this.tone(3000, 2000, 0.05, 'triangle', 0.03); break;
      case 'burst': { const k = ev.level || 0; this.noise(0.1 + k * 0.04, 1300, 0.14 + k * 0.03, 'bandpass', 500); this.tone(170 - k * 15, 70, 0.12 + k * 0.04, 'sine', 0.1 + k * 0.03); break; }
      case 'splash': if (this.limit('splash', 0.04)) { this.noise(0.08 + ev.r * 0.04, 900, 0.05 + ev.r * 0.03, 'lowpass'); this.tone(240, 90, 0.08, 'sine', 0.04 + (ev.level || 0) * 0.02); } break;
      case 'rocketJump': {
        // A deep boom, a sharp crack, a rumbling tail and the rising rush of the launch
        const k = ev.power || 0.5;
        this.tone(95, 30, 0.5 + 0.2 * k, 'sine', 0.22 + 0.14 * k);
        this.tone(190, 60, 0.26, 'sawtooth', 0.05 + 0.04 * k);
        this.noise(0.08, 4500, 0.12 + 0.08 * k, 'highpass');
        this.noise(0.6 + 0.25 * k, 1400, 0.2 + 0.1 * k, 'lowpass', 160);
        this.noise(0.5, 500, 0.07 + 0.05 * k, 'bandpass', 3500, 0.04);
        if (ev.perfect) this.tone(1760, 2637, 0.3, 'sine', 0.05, 0.04);
        break;
      }
      case 'thrustOn': this.jet(ev.p, true); break;
      case 'thrustOff': this.jet(ev.p, false); break;
      case 'mortarShot': this.tone(95, 55, 0.25, 'sine', 0.2); this.noise(0.12, 500, 0.1, 'lowpass'); this.tone(1700, 420, Math.max(0.3, ev.ticks / 60), 'sine', 0.03, 0.05); break;
      case 'enemyBlast': this.noise(0.45, 420, 0.3, 'lowpass'); this.tone(70, 32, 0.4, 'sine', 0.26); break;
      case 'chargeStart': this.tone(110, 230, 0.3, 'sawtooth', 0.08); this.noise(0.35, 300, 0.12, 'lowpass'); break;
      case 'chargeCrash': this.noise(0.3, 900, 0.24, 'lowpass'); this.tone(120, 50, 0.3, 'square', 0.1); break;
      case 'carve': if (this.limit('carve', 0.12)) this.noise(0.16, 4200, 0.05, 'highpass', 2000); break;
      case 'focusUp': this.tone(700 + ev.level * 120, 0, 0.08, 'triangle', 0.04); break;
      case 'focusLost': this.tone(520, 260, 0.16, 'triangle', 0.04); break;
      case 'gates': ev.closed ? this.tone(140, 110, 0.5, 'sawtooth', 0.06) : this.tone(220, 660, 0.4, 'triangle', 0.06); break;
      // Bosses
      case 'bossIntro': for (const [f, d] of [[73, 0], [110, 0.02], [147, 0.04]]) this.tone(f, f * 0.98, 1.1, 'sawtooth', 0.07, d); this.noise(0.8, 300, 0.1, 'lowpass', 90); this.tone(1100, 550, 0.6, 'sine', 0.03, 0.2); break;
      case 'bossSlam': this.tone(60, 28, 0.55, 'sine', ev.big ? 0.3 : 0.2); this.noise(0.45, 500, ev.big ? 0.28 : 0.18, 'lowpass', 80); this.noise(0.06, 3500, 0.1, 'highpass'); break;
      case 'bossPhase': this.tone(90, 45, 1.0, 'sawtooth', 0.12); this.tone(180, 70, 0.9, 'square', 0.05); this.noise(1.0, 1200, 0.18, 'bandpass', 200); this.tone(880, 1760, 0.4, 'sine', 0.04, 0.3); break;
      case 'bossLaser': this.tone(220, 110, (ev.ticks || 40) / 60, 'sawtooth', 0.08); this.noise((ev.ticks || 40) / 60, 2600, 0.1, 'bandpass', 1800); this.tone(1760, 0, 0.08, 'square', 0.04); break;
      case 'bossMissiles': for (let i = 0; i < (ev.n || 3); i++) { this.noise(0.12, 1600, 0.08, 'bandpass', 600, i * 0.06); this.tone(400, 900, 0.1, 'triangle', 0.035, i * 0.06); } break;
      case 'bossDive': this.tone(1800, 300, 0.45, 'sawtooth', 0.05); this.noise(0.45, 900, 0.12, 'bandpass', 3500); break;
      case 'bossCrash': this.tone(70, 30, 0.5, 'sine', 0.25); this.noise(0.5, 700, 0.24, 'lowpass', 90); for (let i = 0; i < 5; i++) this.tone(600 + Math.random() * 900, 0, 0.05, 'square', 0.03, 0.05 + i * 0.07); break;
      case 'bossDazed': this.tone(660, 330, 0.3, 'sine', 0.07); this.tone(990, 495, 0.3, 'sine', 0.05); break;
      case 'bossCall': this.tone(520, 1040, 0.25, 'sawtooth', 0.05); this.tone(780, 1560, 0.25, 'sawtooth', 0.04, 0.12); break;
      case 'bossDown':
        for (let i = 0; i < 8; i++) { this.noise(0.25, 700, 0.14, 'lowpass', 120, i * 0.13); this.tone(90, 40, 0.25, 'sine', 0.12, i * 0.13); }
        this.tone(55, 25, 1.4, 'sine', 0.3, 1.15); this.noise(1.4, 900, 0.3, 'lowpass', 60, 1.15); this.noise(0.1, 5000, 0.14, 'highpass', 0, 1.15);
        break;
      // Nova's secondary weapons
      case 'subSwitch': this.tone(1100, 0, 0.03, 'square', 0.03); this.tone(SUB_HUM[ev.sub] * 4, 0, 0.09, 'triangle', 0.05, 0.03); break;
      case 'grenadeThrow': this.tone(2400, 0, 0.02, 'square', 0.03); this.noise(0.1, 1200, 0.07, 'bandpass', 600, 0.01); this.tone(300, 190, 0.09, 'triangle', 0.04, 0.01); break;
      case 'bounce': if (this.limit('bounce', 0.05)) { const k = Math.min(1, (ev.sp || 5) / 12); this.tone(950 + Math.random() * 250, 700, 0.05, 'triangle', 0.035 * k); this.tone(2100, 0, 0.03, 'square', 0.012 * k); } break;
      case 'frag': { const L = ev.level || 0; this.noise(0.4 + 0.05 * L, 650, 0.22 + 0.03 * L, 'lowpass', 110); this.tone(115 - 8 * L, 36, 0.35 + 0.05 * L, 'sine', 0.2); this.noise(0.06, 4200, 0.12, 'highpass');
        for (let i = 0; i < 3; i++) this.tone(1400 + Math.random() * 1600, 0, 0.03, 'square', 0.015, 0.06 + i * 0.05); break; }
      case 'cluster': this.tone(700, 1400, 0.06, 'square', 0.04); this.noise(0.08, 2400, 0.06); break;
      case 'chain': {
        // Lightning: a sharp electric crack, a buzzing sweep and a crackle for every jump
        const L = ev.level || 0;
        this.noise(0.16 + 0.04 * L, 3600, 0.15 + 0.03 * L, 'bandpass', 900); this.tone(1900, 280, 0.16, 'sawtooth', 0.06 + 0.01 * L); this.tone(60, 40, 0.14, 'square', 0.05);
        for (let i = 0; i < (ev.n || 1); i++) { this.tone(2000 + Math.random() * 1800, 0, 0.03, 'square', 0.035, 0.02 + i * 0.035); this.noise(0.04, 5000, 0.05, 'highpass', 0, 0.02 + i * 0.035); }
        break;
      }
      case 'discThrow': this.noise(0.22, 2400, 0.09, 'bandpass', 5600); this.tone(700, 1150, 0.16, 'triangle', 0.05); break;
      case 'discRecall': this.tone(1150, 700, 0.1, 'sine', 0.04); break;
      case 'discCatch': this.tone(1500, 0, 0.05, 'triangle', 0.05); this.noise(0.04, 3000, 0.05); break;
      case 'wellLaunch': this.tone(260, 160, 0.2, 'sine', 0.07); this.noise(0.15, 800, 0.05, 'lowpass'); break;
      case 'wellOpen': this.noise(0.35, 300, 0.12, 'bandpass', 2600); this.tone(70, 48, 0.5, 'sine', 0.16); this.tone(1400, 350, 0.3, 'sine', 0.04); break;
      case 'wellCollapse': { const L = ev.level || 1; this.tone(50, 24, 0.55, 'sine', 0.24 + 0.02 * L); this.noise(0.45, 1200, 0.2, 'lowpass', 100); this.tone(1300, 2600, 0.14, 'sine', 0.04); this.noise(0.06, 5000, 0.08, 'highpass'); break; }
      // The dodge and the Solar Uppercut
      case 'dodge': this.noise(0.14, 2600, 0.07, 'bandpass', 5800); this.tone(900, 1500, 0.06, 'sine', 0.03); break;
      case 'perfectDodge': this.tone(520, 130, 0.65, 'sine', 0.13); this.tone(1560, 390, 0.6, 'triangle', 0.045); this.noise(0.6, 4000, 0.06, 'highpass', 800); this.tone(2637, 0, 0.3, 'sine', 0.05); break;
      case 'riseBlast': this.tone(120, 50, 0.3, 'sine', 0.16); this.tone(1320, 2640, 0.18, 'sine', 0.05); this.noise(0.25, 3000, 0.1, 'bandpass', 800); break;
      // Ultimates
      case 'ultReady': for (const [f, d] of [[1047, 0], [1319, 0.06], [1568, 0.12], [2093, 0.18]]) this.tone(f, 0, 0.22, 'sine', 0.05, d); break;
      case 'ultCast': case 'ultJoin': {
        // The call: a rising chord that swells, a rushing sweep and a deep hit under it
        const j = ev.type === 'ultJoin' ? 1.26 : 1;
        for (const [f, d] of [[110, 0], [165, 0.02], [220, 0.04]]) this.tone(f * j, f * j * 4, 0.8, 'sawtooth', 0.045, d);
        for (const [f, d] of [[523, 0.05], [659, 0.08], [784, 0.11]]) this.tone(f * j, 0, 0.9, 'sine', 0.035, d);
        this.noise(0.8, 400, 0.14, 'bandpass', 6000); this.tone(55, 30, 0.9, 'sine', 0.22); this.noise(0.08, 6000, 0.1, 'highpass');
        break;
      }
      case 'ultRun': this.tone(60, 30, 0.5, 'sine', 0.22); this.noise(0.3, 3000, 0.12, 'bandpass', 500); break;
      case 'ultBegin':
        if (ev.kind === 'nova') this.tone(220, 1760, 0.4, 'sine', 0.06);
        else if (ev.kind === 'ram') { this.tone(50, 110, 0.6, 'sawtooth', 0.1); this.noise(0.5, 300, 0.14, 'lowpass'); }
        else if (ev.kind === 'fix') { for (const d of [0, 0.12, 0.24]) this.tone(1600, 0, 0.06, 'square', 0.03, d); }
        else { this.noise(0.3, 5000, 0.12, 'bandpass', 800); this.tone(1400, 350, 0.25, 'sine', 0.05); }
        break;
      case 'ultNova': this.tone(45, 20, 1.2, 'sine', 0.32); this.noise(1.2, 900, 0.3, 'lowpass', 60); this.noise(0.1, 6000, 0.16, 'highpass'); this.tone(1760, 3520, 0.4, 'sine', 0.06); break;
      case 'ultCut': if (this.limit('cut', 0.02)) { this.noise(0.07, 3500 + Math.random() * 2000, 0.11, 'bandpass', 7000); this.tone(2400, 1200, 0.04, 'square', 0.025); } break;
      case 'ultFinisher':
        this.noise(0.5, 1800, 0.25, 'bandpass', 8000); this.tone(90, 30, 0.8, 'sine', 0.3); this.tone(2800, 1400, 0.3, 'sine', 0.05);
        for (let i = 0; i < 6; i++) this.noise(0.06, 3000 + i * 500, 0.06, 'bandpass', 7000, 0.05 + i * 0.03);
        break;
      case 'ultEnd': this.tone(880, 1320, 0.2, 'sine', 0.04); break;
      // ---- RAM ----
      case 'guardOn': this.tone(180, 140, 0.12, 'triangle', 0.06); this.noise(0.08, 900, 0.06, 'lowpass'); this.tone(1100, 0, 0.05, 'sine', 0.025); break;
      case 'guardOff': this.tone(160, 120, 0.06, 'triangle', 0.025); break;
      case 'guardBlock':
        if (!this.limit('block', 0.03)) break;
        this.tone(ev.heavy ? 140 : 220, ev.heavy ? 70 : 150, 0.12 + (ev.heavy ? 0.1 : 0), 'square', 0.06 + (ev.heavy ? 0.04 : 0)); this.noise(0.1, 2600, 0.08, 'bandpass', 1200);
        this.tone(1900 + 700 * (ev.frac || 1), 0, 0.06, 'sine', 0.03);
        break;
      case 'perfectGuard': this.tone(1320, 2640, 0.2, 'sine', 0.07); this.tone(220, 110, 0.25, 'square', 0.07); this.noise(0.15, 4000, 0.08, 'highpass'); break;
      case 'rampartBreak': this.noise(0.4, 2500, 0.24, 'highpass', 800); for (let i = 0; i < 6; i++) this.tone(1800 + Math.random() * 1600, 600, 0.12, 'triangle', 0.03, i * 0.03); this.tone(200, 60, 0.4, 'sawtooth', 0.1); break;
      case 'rampartReady': this.tone(660, 990, 0.14, 'sine', 0.05); this.tone(990, 0, 0.12, 'triangle', 0.03, 0.08); break;
      case 'kineticRelease': { const k = ev.k || 0; this.tone(90, 30, 0.4 + 0.3 * k, 'sine', 0.2 + 0.12 * k); this.noise(0.4 + 0.2 * k, 900, 0.22 + 0.1 * k, 'lowpass', 90); this.tone(400, 1800, 0.25, 'sawtooth', 0.04 + 0.03 * k); break; }
      case 'rush': this.tone(70 - 5 * ev.level, 40, 0.3, 'sawtooth', 0.08 + 0.02 * ev.level); this.noise(0.25, 400, 0.12 + 0.03 * ev.level, 'lowpass'); break;
      case 'plowCatch': if (this.limit('plow', 0.04)) { this.noise(0.12, 700, 0.14, 'lowpass'); this.tone(160, 80, 0.12, 'square', 0.06); } break;
      case 'ramSplat': this.tone(60, 25, 0.6, 'sine', 0.3); this.noise(0.5, 600, 0.3, 'lowpass', 70); this.noise(0.08, 3500, 0.14, 'highpass'); this.tone(240, 80, 0.3, 'square', 0.07); break;
      case 'ramBonk': this.tone(300, 150, 0.15, 'square', 0.08); this.tone(900, 600, 0.1, 'triangle', 0.05); this.noise(0.12, 1200, 0.1, 'lowpass'); break;
      case 'wallUp': this.tone(110, 330, 0.3, 'sawtooth', 0.07); this.tone(660, 1320, 0.25, 'sine', 0.05); this.noise(0.25, 300, 0.12, 'lowpass'); break;
      case 'wallHit': if (this.limit('wallhit', 0.04)) this.tone(1300, 900, 0.06, 'triangle', 0.04); break;
      case 'wallDown': if (ev.broken) { this.noise(0.35, 3000, 0.16, 'highpass', 700); this.tone(500, 120, 0.3, 'triangle', 0.05); } else this.tone(660, 220, 0.3, 'sine', 0.03); break;
      case 'link': this.tone(440, 880, 0.2, 'sine', 0.06); this.tone(660, 1320, 0.22, 'triangle', 0.04, 0.05); this.noise(0.12, 2600, 0.04); break;
      case 'linkHit': if (this.limit('linkhit', 0.08)) this.tone(520, 260, 0.1, 'triangle', 0.04); break;
      case 'linkEnd': this.tone(880, 440, 0.15, 'sine', 0.03); break;
      case 'linkNone': case 'notReady': if (this.limit('nope', 0.15)) this.tone(240, 0, 0.06, 'square', 0.025); break;
      case 'leap': this.tone(120, 360, 0.25, 'sawtooth', 0.07); this.noise(0.2, 500, 0.1, 'lowpass'); break;
      case 'leapLand': this.tone(70, 30, 0.35, 'sine', 0.24); this.noise(0.3, 500, 0.2, 'lowpass', 80); break;
      case 'provoke':
        // A war cry: a growling chord and a drum hit
        for (const [f, d] of [[98, 0], [147, 0.02], [196, 0.04]]) this.tone(f, f * 1.12, 0.5, 'sawtooth', 0.06, d);
        this.tone(55, 30, 0.4, 'sine', 0.2); this.noise(0.4, 600, 0.12, 'bandpass', 200);
        break;
      case 'quake': this.tone(48, 22, 0.6, 'sine', 0.3); this.noise(0.55, 400, 0.26, 'lowpass', 60); this.noise(0.06, 3000, 0.1, 'highpass'); break;
      case 'upliftBlast': this.tone(140, 60, 0.3, 'sine', 0.16); this.noise(0.25, 1800, 0.12, 'bandpass', 500); this.tone(700, 1400, 0.15, 'triangle', 0.04); break;
      case 'breachBlast': this.noise(0.4, 600, 0.22, 'lowpass', 90); this.tone(85, 35, 0.35, 'sine', 0.2); break;
      case 'fortify': for (const [f, d] of [[392, 0], [523, 0.05], [659, 0.1]]) this.tone(f, 0, 0.4, 'triangle', 0.05, d); this.noise(0.3, 3000, 0.05, 'highpass'); break;
      case 'ramSlam': this.tone(38, 18, 1.4, 'sine', 0.36); this.noise(1.2, 700, 0.34, 'lowpass', 50); this.noise(0.12, 5000, 0.18, 'highpass'); this.tone(330, 82, 0.6, 'square', 0.08); break;
      // ---- Fix ----
      case 'gadgetSelect': case 'powerSelect': this.tone(1300, 0, 0.03, 'square', 0.03); this.tone(ev.type === 'gadgetSelect' ? 880 : 1175, 0, 0.08, 'triangle', 0.05, 0.03); break;
      case 'gadgetDeploy':
        // A ratchet and a clunk as it unfolds
        for (let i = 0; i < 5; i++) this.tone(2200 - i * 150, 0, 0.02, 'square', 0.025, i * 0.03);
        this.tone(180, 90, 0.15, 'square', 0.07, 0.16); this.noise(0.1, 900, 0.08, 'lowpass', 0, 0.16);
        break;
      case 'gadgetUp': for (const [f, d] of [[784, 0], [988, 0.06], [1175, 0.12], [1568, 0.18]]) this.tone(f, 0, 0.16, 'triangle', 0.05, d); break;
      case 'gadgetWrench': this.tone(2400, 1800, 0.06, 'square', 0.045); this.tone(3200, 0, 0.04, 'sine', 0.03, 0.02); break;
      case 'gadgetHit': if (this.limit('ghit', 0.05)) this.tone(900, 600, 0.05, 'square', 0.03); break;
      case 'gadgetEnd': if (ev.why === 'broken') { this.noise(0.3, 1500, 0.16, 'lowpass', 200); this.tone(400, 100, 0.3, 'sawtooth', 0.06); } else if (ev.why === 'expire') this.tone(660, 330, 0.2, 'sine', 0.03); break;
      case 'sentryShot': if (this.limit('sentry', 0.03)) { this.tone(1100, 700, 0.04, 'square', 0.025); this.noise(0.03, 3000, 0.025); } break;
      case 'sentryRocket': this.noise(0.25, 1500, 0.08, 'bandpass', 600); this.tone(300, 700, 0.15, 'triangle', 0.035); break;
      case 'padPlace': this.tone(200, 400, 0.12, 'triangle', 0.06); this.noise(0.1, 600, 0.08, 'lowpass'); break;
      case 'padBounce': if (this.limit('pad', 0.06)) { this.tone(220, 880, 0.18, 'sine', 0.08); this.tone(330, 1320, 0.15, 'triangle', 0.04); } break;
      case 'powerToss': this.tone(500, 900, 0.1, 'triangle', 0.05); this.noise(0.08, 2000, 0.04); break;
      case 'powerUp': {
        const f = { overclock: 1175, plating: 784, medkit: 988 }[ev.kind] || 988;
        for (const [m, d] of [[1, 0], [1.25, 0.05], [1.5, 0.1]]) this.tone(f * m, 0, 0.18, 'sine', 0.05, d);
        break;
      }
      case 'powerFade': this.tone(660, 330, 0.15, 'sine', 0.025); break;
      case 'noScrap': if (this.limit('noscrap', 0.4)) { this.tone(260, 200, 0.08, 'square', 0.03); this.tone(200, 0, 0.08, 'square', 0.03, 0.09); } break;
      case 'scrap': if (this.limit('scrap', 0.06)) for (let i = 0; i < 4; i++) this.tone(1800 + Math.random() * 1200, 0, 0.03, 'square', 0.015, i * 0.04); break;
      case 'rivetStick': this.tone(2000, 1200, 0.05, 'square', 0.035); this.tone(600, 0, 0.3, 'sine', 0.02, 0.05); break;
      case 'rivetBlast': this.noise(0.28, 900, 0.16 + 0.03 * (ev.level || 1), 'lowpass', 100); this.tone(130, 50, 0.25, 'sine', 0.12); break;
      case 'sparkRing': this.noise(0.25, 5000, 0.12, 'highpass', 1500); for (let i = 0; i < 5; i++) this.tone(2400 + Math.random() * 2000, 0, 0.03, 'square', 0.02, i * 0.03); break;
      case 'repairPulse': this.tone(660, 1320, 0.2, 'sine', 0.05); this.tone(990, 1980, 0.18, 'triangle', 0.03, 0.04); break;
      case 'patchOn': this.tone(880, 1320, 0.1, 'sine', 0.04); break;
      case 'patchOff': this.tone(1320, 880, 0.08, 'sine', 0.025); break;
      case 'podCall': this.tone(2200, 300, 0.9, 'sine', 0.05); this.noise(0.9, 3000, 0.06, 'bandpass', 600); break;
      case 'podLand': this.tone(45, 20, 1.0, 'sine', 0.34); this.noise(0.9, 800, 0.3, 'lowpass', 60); this.noise(0.1, 5000, 0.14, 'highpass'); break;
      case 'overhaulPulse': for (const [f, d] of [[523, 0], [659, 0.04], [784, 0.08], [1047, 0.12]]) this.tone(f, f * 1.5, 0.45, 'sine', 0.05, d); this.noise(0.4, 3000, 0.08, 'bandpass', 800); break;
      case 'overhaulDone': for (const [f, d] of [[1047, 0], [1319, 0.06], [1568, 0.12], [2093, 0.18]]) this.tone(f, 0, 0.3, 'triangle', 0.045, d); break;
      case 'teamFinisher':
        // The eclipse: a swelling rumble, then the loudest hit in the game and a bright chord
        this.tone(40, 70, 0.42, 'sawtooth', 0.08); this.noise(0.42, 200, 0.12, 'lowpass', 1400);
        this.tone(40, 20, 1.6, 'sine', 0.36, 0.42); this.noise(1.4, 1200, 0.32, 'lowpass', 60, 0.42); this.noise(0.15, 6000, 0.18, 'highpass', 0, 0.42);
        for (const [f, d] of [[523, 0.45], [784, 0.47], [1047, 0.49], [1568, 0.51]]) this.tone(f, 0, 1.1, 'sine', 0.05, d);
        break;
    }
  }
}
