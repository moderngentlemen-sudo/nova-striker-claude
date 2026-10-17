#!/usr/bin/env python3
"""Designed sound effects for Nova Striker, rendered offline.

Writes 16-bit mono 44.1 kHz WAV data as `Assets/NovaStriker/Resources/NovaStriker/Audio/<name>.wav.bytes`: raw .wav
bytes in a .bytes file, so Unity imports each as a TextAsset and `Game/Audio/SampleBank.cs` parses the PCM itself
(no importer settings to get wrong). A missing file falls back to the synthesized sound, and a recorded WAV dropped
in under the same name replaces a designed one (16/24-bit PCM or 32-bit float, mono or stereo, any rate).

Every sound is layered the way a sound designer would build it in a DAW:
  transient  a click or crack, a few milliseconds long, so the hit reads at once;
  body       filtered noise and pitched parts with pitch envelopes (the character of the sound);
  sub        a low sine thump with a fast pitch drop (weight);
  tail       decaying filtered noise and a short convolution reverb (space);
then soft clipping glues the layers, and each sound gets several variations (`blast_big_1..3`) so repeats differ.
Loops (`*_loop`) are built from whole cycles of every tone and LFO and crossfaded end-to-start, so they loop
seamlessly.

Usage:
  python3 make_sfx.py                 write every sound into Resources (and print sizes)
  python3 make_sfx.py --list          print the sound list with descriptions
  python3 make_sfx.py --only blast    write only the sounds whose names start with `blast`
  python3 make_sfx.py --reel out.wav  also write a listening reel: every sound once, with gaps

Needs Python 3 with numpy and scipy (`pip install --user numpy scipy`).
"""
import argparse, io, os, struct, sys, wave
import numpy as np
from scipy import signal

SR = 44100
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.normpath(os.path.join(HERE, "../../Assets/NovaStriker/Resources/NovaStriker/Audio"))

# ---------------------------------------------------------------- building blocks
rng = np.random.default_rng(1)


def n_of(d): return max(1, int(round(d * SR)))
def tt(d): return np.arange(n_of(d)) / SR
def white(d): return rng.uniform(-1, 1, n_of(d))


def colored(d, slope):
    """Noise with a spectral slope in dB/octave (-3 pink, -6 brown)."""
    n = n_of(d); spec = np.fft.rfft(rng.standard_normal(n)); f = np.fft.rfftfreq(n, 1 / SR); f[0] = f[1]
    spec *= (f / 1000.0) ** (slope / 6.02); x = np.fft.irfft(spec, n)
    return x / (np.max(np.abs(x)) + 1e-9)


def pink(d): return colored(d, -3)
def brown(d): return colored(d, -6)


def bq(kind, f, q=0.707):
    """RBJ biquad coefficients (b, a); q is linear."""
    f = min(max(f, 10.0), SR * 0.45); w = 2 * np.pi * f / SR; cs, sn = np.cos(w), np.sin(w); al = sn / (2 * q)
    if kind == "lp": b = [(1 - cs) / 2, 1 - cs, (1 - cs) / 2]
    elif kind == "hp": b = [(1 + cs) / 2, -(1 + cs), (1 + cs) / 2]
    elif kind == "bp": b = [al, 0, -al]
    else: raise ValueError(kind)
    a = [1 + al, -2 * cs, 1 - al]
    return np.array(b) / a[0], np.array(a) / a[0]


def filt(x, kind, f, q=0.707, order=1):
    """A static filter (order = how many biquads in series)."""
    b, a = bq(kind, f, q)
    for _ in range(order): x = signal.lfilter(b, a, x)
    return x


def sweep(x, kind, f, q=0.707, blk=64):
    """A filter whose cutoff follows the array (or callable of time) f, a block at a time."""
    if callable(f): f = f(np.arange(len(x)) / SR)
    f = np.broadcast_to(np.asarray(f, float), x.shape)
    q = np.broadcast_to(np.asarray(q, float), x.shape)
    y = np.empty_like(x); zi = np.zeros(2)
    for i in range(0, len(x), blk):
        b, a = bq(kind, f[i], q[i]); y[i:i + blk], zi = signal.lfilter(b, a, x[i:i + blk], zi=zi)
    return y


def osc(freq, d=None, shape="sine", ph=0.0):
    """An oscillator following freq (a number or per-sample array)."""
    if np.isscalar(freq): freq = np.full(n_of(d), float(freq))
    p = ph + np.cumsum(freq) / SR
    if shape == "sine": return np.sin(2 * np.pi * p)
    fr = p % 1.0
    if shape == "saw": return filt(2 * fr - 1, "lp", 9000)
    if shape == "square": return filt(np.where(fr < 0.5, 1.0, -1.0), "lp", 8000)
    if shape == "tri": return 4 * np.abs(fr - 0.5) - 1
    raise ValueError(shape)


def drop(f0, f1, tau, d):
    """A pitch envelope: f0 falling to f1 with time constant tau (or rising when f1 > f0)."""
    t = tt(d); return f1 + (f0 - f1) * np.exp(-t / tau)


def glide(f0, f1, d, curve=1.0):
    """An exponential glide from f0 to f1 over d."""
    k = (np.arange(n_of(d)) / max(1, n_of(d) - 1)) ** curve; return f0 * (f1 / f0) ** k


def env(d, a=0.002, tau=0.1, hold=0.0):
    """Attack (linear), hold, then exponential decay."""
    t = tt(d); e = np.where(t < a, t / max(a, 1e-6), 1.0)
    return e * np.where(t < a + hold, 1.0, np.exp(-(t - a - hold) / tau))


def swell(d, peak, a_curve=2.0, tau=0.1):
    """Rises to 1 at `peak` (curved), then decays."""
    t = tt(d); up = np.clip(t / peak, 0, 1) ** a_curve
    return np.where(t <= peak, up, np.exp(-(t - peak) / tau))


def sat(x, drive=2.0): return np.tanh(drive * x) / np.tanh(drive)


def mix(*parts):
    """Sums (array, gain[, offset seconds]) layers into one buffer as long as needed."""
    at = [n_of(p[2]) if len(p) > 2 and p[2] else 0 for p in parts]
    out = np.zeros(max(len(p[0]) + a for p, a in zip(parts, at)))
    for p, a in zip(parts, at):
        x = p[0].copy(); k = min(len(x), n_of(0.004)); x[-k:] *= np.linspace(1, 0, k)   # no layer ends on a step
        out[a:a + len(x)] += p[1] * x
    return out


def modal(freqs, decays, amps, d, strike=0.002, jitter=0.0):
    """Struck metal, glass or ceramic: decaying sine modes plus a filtered strike."""
    t = tt(d); x = np.zeros_like(t)
    for f, dc, a in zip(freqs, decays, amps):
        f *= 1 + jitter * rng.uniform(-1, 1)
        x += a * np.sin(2 * np.pi * f * t + rng.uniform(0, 6.28)) * np.exp(-t / dc)
    x *= np.clip(t / strike, 0, 1)
    x *= np.clip((t[-1] - t) / max(0.005, 0.2 * d), 0, 1)   # fade out, so a cut-off mode never clicks
    return x


def grains(d, rate, f_lo, f_hi, dur=(0.002, 0.008), decay_tau=None, amp_decay=None):
    """Sparse tiny clicks (crackle, frying, shards): `rate` per second, each a short resonant ping."""
    n = n_of(d); out = np.zeros(n); t_hit = 0.0
    while True:
        t_hit += rng.exponential(1 / rate)
        if t_hit >= d: break
        g_d = rng.uniform(*dur); f = np.exp(rng.uniform(np.log(f_lo), np.log(f_hi)))
        tg = tt(g_d); g = np.sin(2 * np.pi * f * tg + rng.uniform(0, 6)) * np.exp(-tg / (g_d / 3))
        a = rng.uniform(0.3, 1.0) * (np.exp(-t_hit / amp_decay) if amp_decay else 1.0)
        i = n_of(t_hit); m = min(len(g), n - i); out[i:i + m] += a * g[:m]
    return out


def tinkles(d, count, start, spread, f_lo=2500, f_hi=7000, amp=1.0, fall=0.5):
    """Debris settling: small inharmonic clinks scattered after `start`, quieter as they go."""
    out = np.zeros(n_of(d))
    for k in range(count):
        at = start + spread * (k / max(1, count - 1)) ** 1.5 + rng.uniform(0, 0.04)
        if at >= d - 0.05: continue
        f0 = np.exp(rng.uniform(np.log(f_lo), np.log(f_hi)))
        c = modal([f0, f0 * 2.76, f0 * 5.4], [0.05, 0.025, 0.012], [1, 0.5, 0.25], 0.12, strike=0.0005)
        a = amp * rng.uniform(0.4, 1.0) * np.exp(-(at - start) / max(0.05, fall))
        i = n_of(at); m = min(len(c), len(out) - i); out[i:i + m] += a * c[:m]
    return out


def impulse_response(d, rt60, lp_start=8000, lp_end=1500, pre=0.008, early=6, width=0.04):
    """A reverb impulse: a few early reflections then exponentially decaying noise that darkens as it fades."""
    t = tt(d); x = rng.standard_normal(len(t)) * np.exp(-6.9 * t / rt60)
    x = sweep(x, "lp", np.geomspace(lp_start, lp_end, len(t)))
    x[:n_of(pre)] = 0
    for k in range(early):
        i = n_of(pre + rng.uniform(0, width)); x[i] += rng.uniform(-1, 1) * 2.5 * np.exp(-k * 0.3)
    return x / np.sqrt(np.sum(x ** 2))


IRS = {}
def verb(x, wet=0.25, size="room", tail=None):
    """Convolution reverb with one of a few generated rooms (hall: big and dark; room: short; plate: bright)."""
    spec = {"room": (0.6, 0.45, 9000, 2500), "hall": (2.6, 2.2, 7000, 900), "plate": (1.6, 1.3, 12000, 4000),
            "small": (0.3, 0.22, 10000, 3500)}[size]
    if size not in IRS: IRS[size] = impulse_response(spec[0], spec[1], spec[2], spec[3])
    ir = IRS[size]; w = signal.fftconvolve(x, ir)
    if tail is not None: w = w[:len(x) + n_of(tail)]
    y = np.zeros(len(w)); y[:len(x)] += x; y += wet * w * 0.6
    return y


def fade(x, fi=0.001, fo=0.02):
    x = x.copy(); a, b = n_of(fi), n_of(fo)
    x[:a] *= np.linspace(0, 1, a); x[-b:] *= np.linspace(1, 0, b) ** 2
    return x


def trim(x, floor_db=-60):
    """Cuts the silent end (below floor relative to the peak), keeping a short fade."""
    pk = np.max(np.abs(x)) + 1e-12; above = np.nonzero(np.abs(x) > pk * 10 ** (floor_db / 20))[0]
    end = min(len(x), (above[-1] if len(above) else len(x)) + n_of(0.03))
    return fade(x[:end], 0.0005, min(0.08, end / SR * 0.3))


def finish(x, peak=0.89, drive=1.3):
    """Removes DC, glues with a soft clip, trims the tail and sets the peak."""
    x = filt(x, "hp", 18)
    x = x / (np.max(np.abs(x)) + 1e-12)
    x = sat(x, drive)
    x = trim(x)
    return x / (np.max(np.abs(x)) + 1e-12) * peak


def loop_seam(x, xfade):
    """Makes x[:L] loop seamlessly: x holds L + xfade samples; the overhang is crossfaded onto the start."""
    X = n_of(xfade); L = len(x) - X; y = x[:L].copy()
    head, tail = x[:X], x[L:L + X]
    # equal power, corrected for how alike the two ends are (whole-cycle tones match; noise doesn't), so the
    # level holds steady through the crossfade
    rho = np.sum(head * tail) / (np.sqrt(np.sum(head ** 2) * np.sum(tail ** 2)) + 1e-12)
    k = np.linspace(0, np.pi / 2, X); a, b = np.sin(k), np.cos(k)
    y[:X] = (head * a + tail * b) / np.sqrt(1 + 2 * rho * a * b)
    return y


def finish_loop(x, xfade=0.12, peak=0.7, drive=1.2):
    x = filt(x, "hp", 18)
    x = sat(x / (np.max(np.abs(x)) + 1e-12), drive)
    y = loop_seam(x, xfade); y -= np.mean(y)
    return y / (np.max(np.abs(y)) + 1e-12) * peak


def pitch(v, spread=0.06):
    """A per-variation pitch factor."""
    return 1 + spread * (v - 1) * (1 if v % 2 else -1) * 0.7

# ---------------------------------------------------------------- explosions

def explosion(v, size):
    """size 0 small, 1 medium, 2 big, 3 boss."""
    P = pitch(v, 0.08)
    D = [1.0, 1.8, 3.0, 5.0][size]
    # transient: a crack (broadband click, then a bright noise snap)
    crack = white(0.03) * env(0.03, 0.0003, 0.004 + 0.002 * size)
    crack = filt(crack, "hp", 1800) + 0.6 * filt(white(0.03), "bp", 5000, 0.8) * env(0.03, 0.0005, 0.008)
    # body: noise whose low-pass closes as the fireball rolls out
    body_d = [0.6, 1.1, 1.8, 3.0][size]
    body = 0.6 * pink(body_d) + 0.4 * white(body_d)
    f0 = [7000, 5500, 4500, 3500][size] * P; f1 = [500, 320, 200, 120][size] * P
    body = sweep(body, "lp", lambda t: f1 + (f0 - f1) * np.exp(-t / (0.06 + 0.05 * size)), 0.9)
    body *= swell(body_d, 0.004 + 0.004 * size, 1.0, [0.12, 0.22, 0.38, 0.6][size])
    body = sat(body * 2.5, 2.5)
    # crackle of burning fragments inside the fireball
    crk = grains(body_d, [180, 260, 340, 420][size], 800, 5000, (0.001, 0.004), amp_decay=0.25 + 0.15 * size)
    crk = filt(crk, "hp", 600)
    # sub: a sine thump falling fast
    sub_d = [0.5, 0.9, 1.5, 2.6][size]
    fs0, fs1 = [140, 105, 80, 62][size] * P, [48, 38, 30, 24][size] * P
    sub = osc(drop(fs0, fs1, [0.05, 0.07, 0.1, 0.14][size], sub_d), shape="sine") * env(sub_d, 0.002, [0.12, 0.22, 0.4, 0.7][size])
    sub = sat(sub * 1.5, 1.5)
    # tail: dark rolling rumble
    tail_d = D
    rum = filt(brown(tail_d), "lp", [400, 260, 180, 130][size], order=2)
    rum *= swell(tail_d, 0.05 + 0.04 * size, 1.0, [0.25, 0.45, 0.8, 1.4][size])
    rum *= 1 + 0.25 * np.sin(2 * np.pi * (3 + rng.uniform(0, 2)) * tt(tail_d))
    parts = [(crack, [1.1, 1.0, 0.95, 0.9][size]), (body, 0.75), (crk, [0.18, 0.2, 0.22, 0.22][size]), (sub, [0.6, 0.7, 0.8, 0.9][size]), (rum, [0.35, 0.5, 0.65, 0.8][size])]
    if size >= 2:
        # secondary detonations and falling debris
        for k, at in enumerate([0.18, 0.42] if size == 2 else [0.22, 0.5, 0.95, 1.4]):
            s2 = sweep(white(0.6), "lp", lambda t: 300 + 3000 * np.exp(-t / 0.06)) * env(0.6, 0.002, 0.14)
            s2 += 0.8 * osc(drop(95 * P, 35, 0.06, 0.6)) * env(0.6, 0.002, 0.18)
            parts.append((sat(s2, 2.0), 0.45 * (0.85 ** k), at))
        parts.append((tinkles(D, 10 + 6 * (size - 2), 0.35, D * 0.55, 1800, 6000, 1.0, D * 0.4), 0.12))
    if size == 3:
        # a deep downward groan under it all
        g = osc(glide(110, 32, 2.5), shape="saw"); g = filt(g, "lp", 300, order=2) * swell(2.5, 0.3, 1.5, 0.8)
        parts.append((g, 0.35, 0.05))
    x = mix(*parts)
    x = verb(x, [0.18, 0.25, 0.32, 0.4][size], "hall" if size >= 2 else "room")
    return finish(x, 0.89, 1.6 + 0.2 * size)

# ---------------------------------------------------------------- beams

def beam_start(v):
    # charge whine snapping into ignition: rising sweep, a sub hit, a blooming roar
    whine = osc(glide(300, 2200, 0.16, 0.6), shape="saw"); whine = filt(whine, "lp", 5000) * swell(0.16, 0.15, 2.0, 0.02)
    ign = sweep(white(1.2), "lp", lambda t: 500 + 9000 * np.exp(-t / 0.08), 1.2) * env(1.2, 0.002, 0.25)
    sub = osc(drop(95, 38, 0.08, 1.0)) * env(1.0, 0.002, 0.3)
    roar = filt(osc(92, 1.2, "saw") + osc(92.6, 1.2, "saw") + 0.6 * osc(139, 1.2, "square"), "lp", 1600) * swell(1.2, 0.25, 1.0, 0.25)
    shimmer = osc(1840 * (1 + 0.01 * np.sin(2 * np.pi * 6 * tt(1.2))), shape="sine") * swell(1.2, 0.1, 1, 0.3)
    x = mix((whine, 0.35), (ign, 0.7, 0.15), (sub, 1.0, 0.15), (roar, 0.45, 0.15), (shimmer, 0.12, 0.15))
    return finish(verb(x, 0.25, "plate"), 0.89, 1.8)


def beam_loop(v, low=False):
    """Nova's Level 4 beam (gold, smooth and searing), or the Supernova an octave down (low=True)."""
    L, X = 2.0, 0.12; d = L + X; t = tt(d)
    k = 0.5 if low else 1.0
    # tones on whole cycles of L (multiples of 0.5 Hz), LFOs too, so the loop is seamless before the crossfade
    lfo = 0.5 + 0.5 * np.sin(2 * np.pi * 1.0 * t)   # 1 Hz cutoff swell
    buzz = osc(92 * k, d, "saw") + osc(92.5 * k, d, "saw") + 0.7 * osc(138.5 * k, d, "saw") + 0.35 * osc(184 * k, d, "square")
    buzz = sweep(buzz, "lp", (900 + 1300 * lfo) * (0.8 if low else 1.0), 1.4)
    trem = 1 + 0.18 * np.sin(2 * np.pi * 11 * t)
    rush = sweep(white(d), "bp", 2600 + 600 * np.sin(2 * np.pi * 0.5 * t), 0.7) * trem
    sub = osc(46 * k * (1 if not low else 2), d, "sine") * (1 + 0.1 * np.sin(2 * np.pi * 2 * t))
    shim = osc(1840 * k * (1 + 0.004 * np.sin(2 * np.pi * 6 * t)), shape="sine") * (0.6 + 0.4 * np.sin(2 * np.pi * 0.5 * t) ** 2)
    crk = filt(grains(d, 90, 2000, 9000, (0.0008, 0.003)), "hp", 1500)
    parts = [(buzz, 0.32), (rush, 0.35), (sub, 0.5), (shim, 0.07), (crk, 0.25)]
    if low:
        # a choir-like formant pad: the Supernova sings as it burns
        pad = osc(138.5, d, "saw") + osc(139, d, "saw") + osc(207.5, d, "saw") + osc(277, d, "saw")
        pad = filt(pad, "bp", 700, 3) + 0.6 * filt(pad, "bp", 1150, 4)
        parts += [(pad, 0.25), (filt(brown(d), "lp", 220, order=2), 0.8)]
    x = mix(*parts)[:n_of(d)]
    return finish_loop(x, X, 0.75, 1.6)


def beam_impact_loop(v):
    """Where the beam meets something: a frying sizzle with a hum."""
    L, X = 1.5, 0.1; d = L + X; t = tt(d)
    fry = filt(grains(d, 900, 1500, 9000, (0.0005, 0.002)), "hp", 1200)
    hiss = sweep(white(d), "bp", 4500 + 1500 * np.sin(2 * np.pi * 2 * t), 0.9) * (0.7 + 0.3 * np.sin(2 * np.pi * 8 * t))
    hum = osc(120, d, "square") * 0.5 + osc(60, d, "sine")
    hum = filt(hum, "lp", 700)
    x = mix((fry, 0.5), (hiss, 0.4), (hum, 0.25))[:n_of(d)]
    return finish_loop(x, X, 0.6, 1.4)


def beam_end(v):
    # power-down: falling buzz, a filter closing on the roar, a spark tail
    fall = osc(glide(420, 55, 0.5, 0.7), shape="saw") + osc(glide(630, 80, 0.5, 0.7), shape="saw")
    fall = sweep(fall, "lp", lambda t: 3000 * np.exp(-t / 0.15) + 200) * env(0.5, 0.004, 0.18)
    air = sweep(white(0.6), "lp", lambda t: 4000 * np.exp(-t / 0.12) + 150) * env(0.6, 0.002, 0.15)
    sparks = filt(grains(0.7, 60, 2500, 8000, (0.001, 0.004), amp_decay=0.2), "hp", 1500)
    pop = osc(drop(160, 60, 0.03, 0.2)) * env(0.2, 0.001, 0.05)
    x = mix((fall, 0.5), (air, 0.4), (sparks, 0.2), (pop, 0.5))
    return finish(verb(x, 0.3, "plate"), 0.8, 1.4)


def nova_gather(v):
    """The Supernova gathering light: a swelling chord and a reverse-swell, cut at the 0.4 s mark."""
    D = 0.75; t = tt(D); P = 0.4
    chord = sum(osc(glide(f / 4, f, D, 0.5), shape="sine") for f in (587, 880, 1175, 1760))
    chord *= swell(D, P, 2.2, 0.06)
    suck = sweep(white(D), "bp", lambda t: 300 + 5000 * np.clip(t / P, 0, 1) ** 2, 1.5) * swell(D, P, 3.0, 0.02)
    rumble = filt(brown(D), "lp", 160) * swell(D, P, 1.5, 0.15)
    x = mix((chord, 0.12), (suck, 0.5), (rumble, 0.6))
    return finish(verb(x, 0.2, "plate"), 0.8, 1.5)


def nova_fire(v):
    """The Supernova ignites: the biggest hit Nova makes, with a bright chord blooming out of it."""
    D = 2.6
    boom = explosion(1, 2)[:n_of(D)]
    chord = sum(osc(f * (1 + 0.003 * np.sin(2 * np.pi * 5 * tt(D) + i)), shape="sine") for i, f in enumerate((587.3, 880, 1174.7, 1760)))
    chord *= swell(D, 0.08, 1.0, 0.7)
    sweep_up = osc(glide(220, 1760, 0.35), shape="saw"); sweep_up = filt(sweep_up, "lp", 4000) * env(0.35, 0.002, 0.15)
    sub = osc(drop(70, 22, 0.15, D)) * env(D, 0.002, 0.8)
    x = mix((boom, 0.8), (chord, 0.1), (sweep_up, 0.2), (sub, 0.9))
    return finish(verb(x, 0.35, "hall"), 0.89, 1.8)


def laser_charge(v):
    """A boss charging its laser: a menacing rising whine whose pulse speeds up (red, harsh, not Nova's gold)."""
    D = 1.4; t = tt(D)
    rate = 4 + 30 * (t / D) ** 2; am = 0.6 + 0.4 * np.sign(np.sin(2 * np.pi * np.cumsum(rate) / SR))
    whine = osc(glide(180, 2400, D, 1.4), shape="square") * osc(glide(90, 1200, D, 1.4) * 1.5, shape="sine")
    whine = filt(whine, "lp", 6000) * am * swell(D, D - 0.02, 1.5, 0.02)
    suck = sweep(white(D), "bp", lambda t: 400 + 4000 * (t / D) ** 2, 2.0) * swell(D, D - 0.02, 2.5, 0.02)
    growl = filt(osc(glide(55, 110, D), shape="saw"), "lp", 400) * swell(D, D - 0.02, 1.0, 0.02)
    x = mix((whine, 0.3), (suck, 0.35), (growl, 0.5))
    return finish(x, 0.8, 1.6)


def laser_fire(v):
    D = 1.0
    zap = osc(drop(1600, 220, 0.04, D), shape="square") * osc(drop(800, 55, 0.05, D), shape="sine")
    zap = filt(zap, "lp", 7000) * env(D, 0.001, 0.15)
    burst = sweep(white(D), "bp", lambda t: 800 + 5000 * np.exp(-t / 0.05), 1.0) * env(D, 0.001, 0.12)
    sub = osc(drop(110, 40, 0.06, D)) * env(D, 0.002, 0.25)
    x = mix((zap, 0.5), (burst, 0.6), (sub, 0.9))
    return finish(verb(x, 0.25, "room"), 0.89, 2.2)


def laser_loop(v):
    """A boss laser: a harsh ring-modulated square buzz with grit (unlike Nova's smooth gold beam)."""
    L, X = 2.0, 0.12; d = L + X; t = tt(d)
    buzz = (osc(110, d, "square") + osc(110.5, d, "saw")) * (0.6 + 0.4 * osc(55, d, "sine"))
    buzz = sweep(buzz, "lp", 1800 + 900 * np.sin(2 * np.pi * 1.5 * t), 2.0)
    grit = 1 + 0.35 * np.sign(np.sin(2 * np.pi * 20 * t))
    hiss = sweep(white(d), "bp", 1800 + 400 * np.sin(2 * np.pi * 0.5 * t), 1.2)
    sub = osc(55, d, "sine")
    crk = filt(grains(d, 140, 1500, 7000, (0.001, 0.003)), "hp", 1200)
    x = mix((buzz * grit, 0.35), (hiss, 0.3), (sub, 0.45), (crk, 0.3))[:n_of(d)]
    return finish_loop(x, X, 0.75, 2.0)

# ---------------------------------------------------------------- shots

def shot_nova(v):
    """Nova's light shot: a quick energy chirp with a puff and a little thump."""
    P = pitch(v, 0.07); D = 0.35
    zap = osc(drop(1100 * P, 320 * P, 0.025, D), shape="square"); zap = filt(zap, "lp", 6000) * env(D, 0.0005, 0.035)
    sine = osc(drop(1800 * P, 600, 0.02, D)) * env(D, 0.0005, 0.03)
    puff = filt(white(D), "bp", 3200 * P, 0.9) * env(D, 0.0005, 0.018)
    thump = osc(drop(160, 70, 0.02, D)) * env(D, 0.001, 0.04)
    x = mix((zap, 0.35), (sine, 0.3), (puff, 0.4), (thump, 0.5))
    return finish(verb(x, 0.15, "small"), 0.8, 1.6)


def shot_charged(v):
    D = 0.7
    zap = sweep(osc(drop(620, 200, 0.05, D), shape="saw"), "lp", lambda t: 400 + 5000 * np.exp(-t / 0.06)) * env(D, 0.001, 0.09)
    puff = filt(white(D), "bp", 1600, 0.8) * env(D, 0.001, 0.05)
    sub = osc(drop(130, 55, 0.04, D)) * env(D, 0.001, 0.09)
    crack = filt(white(0.02), "hp", 3000) * env(0.02, 0.0002, 0.003)
    x = mix((zap, 0.5), (puff, 0.4), (sub, 0.7), (crack, 0.5))
    return finish(verb(x, 0.2, "room"), 0.85, 1.8)


def shot_heavy(v):
    D = 1.2
    roar = sweep(osc(drop(300, 80, 0.12, D), shape="saw") + osc(drop(302, 81, 0.12, D), shape="saw"), "lp", lambda t: 300 + 6000 * np.exp(-t / 0.08)) * env(D, 0.001, 0.25)
    bright = osc(glide(1900, 700, 0.25)) * env(0.25, 0.001, 0.08)
    blast = sweep(pink(D), "lp", lambda t: 250 + 6000 * np.exp(-t / 0.07)) * env(D, 0.001, 0.2)
    sub = osc(drop(115, 38, 0.06, D)) * env(D, 0.001, 0.3)
    crack = filt(white(0.03), "hp", 2500) * env(0.03, 0.0002, 0.005)
    x = mix((roar, 0.45), (bright, 0.2), (blast, 0.55), (sub, 0.9), (crack, 0.6))
    return finish(verb(x, 0.25, "hall"), 0.89, 1.9)


def shot_perfect(v):
    """Perfect Release: the heavy shot plus a bright bell chord and sparkle, unmistakably rewarding."""
    D = 1.4
    base = shot_heavy(1)[:n_of(D)]
    bell = modal([1760, 2637, 3520, 5274], [0.6, 0.45, 0.3, 0.15], [1, 0.7, 0.4, 0.2], D, 0.001)
    sparkle = filt(grains(0.8, 120, 4000, 11000, (0.001, 0.004), amp_decay=0.25), "hp", 3000)
    x = mix((base, 0.7), (bell, 0.25), (sparkle, 0.15, 0.03))
    return finish(verb(x, 0.3, "plate"), 0.89, 1.4)


def cannon(v, big=False):
    """RAM's Breach Cannon: a heavy mechanical boom with a ringing barrel."""
    P = pitch(v, 0.06); D = 1.9 if big else 1.4
    clank = modal([310 * P, 742 * P, 1310 * P, 2040 * P], [0.25, 0.15, 0.08, 0.05], [1, 0.6, 0.4, 0.25], D, 0.0005)
    boom = sweep(pink(D), "lp", lambda t: 180 + 4000 * np.exp(-t / (0.05 if not big else 0.07))) * env(D, 0.001, 0.3 if big else 0.2)
    sub = osc(drop(150 * P, 42, 0.05, D)) * env(D, 0.001, 0.35 if big else 0.25)
    crack = filt(white(0.04), "hp", 2000) * env(0.04, 0.0002, 0.006)
    hiss = sweep(white(D), "bp", lambda t: 800 + 2500 * np.exp(-t / 0.3), 0.7) * env(D, 0.05, 0.25) * 0.5
    parts = [(clank, 0.25), (sat(boom * 2, 2), 0.6), (sub, 1.0), (crack, 0.6), (hiss, 0.15, 0.05)]
    if big: parts.append((filt(brown(D), "lp", 200, order=2) * env(D, 0.03, 0.5), 0.6))
    return finish(verb(mix(*parts), 0.25, "hall"), 0.89, 2.0)


def rivet(v, heavy=False):
    """Fix's rivet gun: a pneumatic puff, a metal tick and a thunk."""
    P = pitch(v, 0.1); D = 0.45 if heavy else 0.2
    tick = modal([2400 * P, 4130 * P, 6100 * P], [0.03, 0.02, 0.01], [1, 0.6, 0.3], D, 0.0003)
    puff = filt(white(D), "bp", 1500 * P, 0.7) * env(D, 0.0005, 0.02 if not heavy else 0.05)
    thunk = osc(drop(400 if heavy else 600, 150, 0.015, D), shape="tri") * env(D, 0.0005, 0.03 if heavy else 0.015)
    parts = [(tick, 0.4), (puff, 0.5), (thunk, 0.5)]
    if heavy: parts += [(modal([620, 1490, 2310], [0.15, 0.08, 0.05], [1, 0.5, 0.3], D), 0.3), (osc(drop(140, 60, 0.03, D)) * env(D, 0.001, 0.06), 0.7)]
    return finish(verb(mix(*parts), 0.12, "small"), 0.75 if not heavy else 0.85, 1.5)


def rifle(v):
    """Echo's rifle: the supersonic crack, a deep report, the whip down the line and a rolling tail."""
    P = pitch(v, 0.05); D = 1.6
    nwave = np.concatenate([np.linspace(1, -1, n_of(0.0008)), np.zeros(10)])
    crack = filt(np.concatenate([nwave, white(0.02) * env(0.02, 0.0001, 0.002)]), "hp", 3000)
    report = osc(drop(110 * P, 38, 0.03, D)) * env(D, 0.0005, 0.12)
    whip = sweep(white(0.35), "bp", lambda t: 700 + 4000 * np.exp(-t / 0.05), 2.5) * env(0.35, 0.001, 0.08)
    body = sweep(pink(D), "lp", lambda t: 150 + 4000 * np.exp(-t / 0.05)) * env(D, 0.0005, 0.12)
    tail = filt(brown(D), "lp", 600) * env(D, 0.03, 0.45)
    x = mix((crack, 0.9), (report, 0.8), (whip, 0.3, 0.01), (body, 0.5), (tail, 0.3, 0.02))
    return finish(verb(x, 0.4, "hall"), 0.89, 2.0)


def rifle_bolt(v):
    """The bolt cycling after the shot: two mechanical clicks."""
    D = 0.3
    def click(f): return modal([f, f * 1.73, f * 2.9], [0.02, 0.012, 0.008], [1, 0.6, 0.4], 0.12, 0.0003) + filt(white(0.12), "bp", f, 2) * env(0.12, 0.0003, 0.004)
    x = mix((click(1900), 0.6), (click(1500), 0.6, 0.16))
    return finish(verb(x, 0.12, "small"), 0.6, 1.3)

# ---------------------------------------------------------------- melee and impacts

def hit_light(v):
    P = pitch(v, 0.08); D = 0.3
    click = filt(white(0.01), "hp", 2500) * env(0.01, 0.0001, 0.0015)
    body = sweep(white(D), "lp", lambda t: 300 + 2400 * np.exp(-t / 0.015)) * env(D, 0.0005, 0.03)
    thump = osc(drop(220 * P, 85, 0.02, D)) * env(D, 0.0005, 0.045)
    smack = filt(white(0.05), "bp", 1100 * P, 1.2) * env(0.05, 0.0003, 0.008)
    x = mix((click, 0.5), (sat(body * 2), 0.5), (thump, 0.8), (smack, 0.4))
    return finish(verb(x, 0.1, "small"), 0.8, 2.0)


def hit_heavy(v):
    P = pitch(v, 0.07); D = 0.6
    click = filt(white(0.015), "hp", 2000) * env(0.015, 0.0001, 0.002)
    crunch = sweep(white(D), "lp", lambda t: 250 + 5000 * np.exp(-t / 0.02)) * env(D, 0.0005, 0.07)
    crunch = sat(crunch * 3, 3)
    sub = osc(drop(160 * P, 48, 0.03, D)) * env(D, 0.0005, 0.12)
    debris = filt(grains(0.3, 120, 1500, 6000, (0.001, 0.004), amp_decay=0.08), "hp", 1000)
    x = mix((click, 0.6), (crunch, 0.45), (sub, 1.0), (debris, 0.2, 0.01))
    return finish(verb(x, 0.15, "room"), 0.89, 2.2)


def clang(v):
    """Metal on armour: an inharmonic plate ring with a hard strike."""
    P = pitch(v, 0.09); D = 0.9
    ring = modal([523 * P, 1247 * P, 1980 * P, 2870 * P, 3920 * P, 5110 * P], [0.4, 0.28, 0.2, 0.12, 0.08, 0.05], [0.8, 1, 0.7, 0.5, 0.35, 0.2], D, 0.0005, 0.02)
    strike = filt(white(0.02), "hp", 2000) * env(0.02, 0.0001, 0.003)
    thud = osc(drop(260 * P, 120, 0.02, D)) * env(D, 0.0005, 0.04)
    x = mix((ring, 0.3), (strike, 0.6), (thud, 0.5))
    return finish(verb(x, 0.15, "room"), 0.85, 1.4)


def crack(v):
    """A ceramic armour plate cracking: a sharp snap, brittle pings and a rattle of grains."""
    P = pitch(v, 0.1); D = 0.45
    snap = filt(white(0.008), "hp", 3500) * env(0.008, 0.0001, 0.0012)
    pings = modal([3100 * P, 4420 * P, 6230 * P, 7700 * P], [0.03, 0.025, 0.018, 0.012], [1, 0.8, 0.6, 0.4], D, 0.0002, 0.05)
    rattle = filt(grains(0.07, 900, 2500, 9000, (0.0005, 0.002)), "hp", 2000)
    body = filt(white(D), "bp", 1800 * P, 0.8) * env(D, 0.0003, 0.012)
    x = mix((snap, 0.7), (pings, 0.25), (rattle, 0.35, 0.002), (body, 0.4))
    return finish(verb(x, 0.12, "small"), 0.8, 1.7)


def armor_break(v):
    """Armour shattering off: a big crack, a metal clang, falling shards and a thump."""
    P = pitch(v, 0.07); D = 1.3
    snap = filt(white(0.02), "hp", 2500) * env(0.02, 0.0001, 0.004)
    shards = filt(grains(0.25, 1400, 2000, 9000, (0.0005, 0.003), amp_decay=0.08), "hp", 1500)
    ring = modal([410 * P, 1033 * P, 1720 * P, 2610 * P], [0.45, 0.3, 0.2, 0.1], [1, 0.8, 0.5, 0.3], D, 0.0005)
    thump = osc(drop(180 * P, 50, 0.03, D)) * env(D, 0.0005, 0.15)
    body = sweep(white(D), "lp", lambda t: 400 + 6000 * np.exp(-t / 0.03)) * env(D, 0.0005, 0.1)
    tink = tinkles(D, 9, 0.12, 0.8, 2500, 7000, 1.0, 0.5)
    x = mix((snap, 0.6), (shards, 0.4), (ring, 0.25), (thump, 0.8), (sat(body * 2), 0.4), (tink, 0.2))
    return finish(verb(x, 0.22, "room"), 0.89, 1.8)


def shatter(v):
    """An enemy breaking up: a thump, a burst of ceramic and metal, clangs, and debris settling."""
    P = pitch(v, 0.08); D = 1.5
    thump = osc(drop(150 * P, 45, 0.04, D)) * env(D, 0.0005, 0.16)
    burst = sweep(white(D), "lp", lambda t: 600 + 8000 * np.exp(-t / 0.04)) * env(D, 0.0005, 0.12)
    shards = filt(grains(0.35, 1100, 2000, 10000, (0.0005, 0.003), amp_decay=0.1), "hp", 1500)
    clangs = mix(*[(modal([f, f * 2.41, f * 3.87], [0.25, 0.12, 0.06], [1, 0.6, 0.3], 0.5, 0.0005), 1, at)
                   for f, at in [(rng.uniform(500, 900) * P, 0.0), (rng.uniform(900, 1500) * P, 0.07), (rng.uniform(1300, 2200) * P, 0.16)]])
    tink = tinkles(D, 14, 0.18, 1.0, 2000, 7500, 1.0, 0.5)
    x = mix((thump, 0.8), (sat(burst * 2), 0.45), (shards, 0.35), (clangs, 0.18), (tink, 0.25))
    return finish(verb(x, 0.25, "room"), 0.89, 1.7)


def debris(v):
    """One small piece of debris bouncing: a short clink."""
    f0 = [2100, 2900, 3600, 4700, 1700, 5600][(v - 1) % 6] * rng.uniform(0.95, 1.05); D = 0.25
    c = modal([f0, f0 * 2.31, f0 * 4.1], [0.06, 0.03, 0.015], [1, 0.5, 0.25], D, 0.0003)
    k = filt(white(0.01), "hp", 3000) * env(0.01, 0.0001, 0.001)
    return finish(mix((c, 0.6), (k, 0.4)), 0.55, 1.2)


def hurt(v):
    """The player taking a hit: a body blow with an electric crackle from the suit."""
    P = pitch(v, 0.07); D = 0.5
    blow = osc(drop(200 * P, 70, 0.03, D)) * env(D, 0.0005, 0.08)
    buzz = osc(drop(260 * P, 110, 0.08, D), shape="saw") * (0.5 + 0.5 * osc(73, D, "square"))
    buzz = filt(buzz, "lp", 2500) * env(D, 0.001, 0.09)
    crunch = sweep(white(D), "lp", lambda t: 400 + 4000 * np.exp(-t / 0.02)) * env(D, 0.0005, 0.05)
    zap = filt(grains(0.15, 400, 2000, 8000, (0.0005, 0.002), amp_decay=0.05), "hp", 1500)
    x = mix((blow, 0.9), (buzz, 0.35), (sat(crunch * 2), 0.4), (zap, 0.25, 0.005))
    return finish(verb(x, 0.1, "small"), 0.89, 2.0)


def parry(v):
    """A normal parry: a dry, hard metallic clank (the square-ish 1250/1870 Hz pair of the classic cue)."""
    P = 1 + 0.015 * (v - 1); D = 0.45
    ring = modal([1250 * P, 1870 * P, 2500 * P, 3740 * P], [0.08, 0.06, 0.03, 0.02], [1, 0.8, 0.35, 0.2], D, 0.0003)
    strike = filt(white(0.02), "hp", 2500) * env(0.02, 0.0001, 0.003)
    knock = osc(drop(300, 140, 0.015, D)) * env(D, 0.0005, 0.03)
    x = mix((sat(ring * 1.5, 1.5), 0.45), (strike, 0.6), (knock, 0.5))
    return finish(verb(x, 0.1, "small"), 0.8, 1.5)


def parry_perfect(v):
    """A perfect parry: a bright ringing bell chord (G6, D7, G7), a shimmer sweeping up and a sub hit: long and
    sparkling where the normal parry is short and dry."""
    D = 1.6
    bell = modal([1568, 2349, 3136, 4699, 6272], [0.8, 0.6, 0.45, 0.25, 0.15], [1, 0.8, 0.6, 0.3, 0.15], D, 0.0005)
    clank = parry(1)[:n_of(D)]
    shimmer = sweep(white(0.6), "bp", lambda t: 3000 + 7000 * t / 0.6, 4) * swell(0.6, 0.15, 1, 0.15)
    sub = osc(drop(140, 50, 0.04, D)) * env(D, 0.0005, 0.15)
    sparkle = filt(grains(1.0, 60, 5000, 11000, (0.001, 0.004), amp_decay=0.35), "hp", 4000)
    x = mix((bell, 0.35), (clank, 0.4), (shimmer, 0.15, 0.02), (sub, 0.7), (sparkle, 0.12, 0.05))
    return finish(verb(x, 0.35, "plate"), 0.89, 1.3)


def block(v):
    """A hit on a hard-light shield: a solid thump, a glassy ping and a short sizzle."""
    P = pitch(v, 0.06); D = 0.45
    thump = osc(drop(240 * P, 140, 0.03, D), shape="tri") * env(D, 0.0005, 0.05)
    glass = modal([1980 * P, 3170 * P, 4460 * P], [0.12, 0.07, 0.04], [1, 0.6, 0.35], D, 0.0005)
    sizzle = filt(grains(0.15, 700, 3000, 9000, (0.0005, 0.002), amp_decay=0.05), "hp", 2500)
    body = filt(white(D), "bp", 2600, 1) * env(D, 0.0005, 0.03)
    x = mix((thump, 0.7), (glass, 0.25), (sizzle, 0.3), (body, 0.35))
    return finish(verb(x, 0.15, "plate"), 0.8, 1.5)


def whoosh(v, heavy=False):
    """A dash: air torn past, the band sweeping up and back."""
    P = pitch(v, 0.1); D = 0.75 if heavy else 0.42
    c = lambda t: (500 + 3800 * np.sin(np.pi * np.clip(t / D, 0, 1)) ** 1.5) * P
    air = sweep(white(D) + 0.5 * pink(D), "bp", c, 1.3) * np.sin(np.pi * np.clip(tt(D) / D, 0, 1)) ** 1.2
    parts = [(air, 0.8)]
    if heavy:
        parts += [(osc(drop(80, 34, 0.08, D)) * env(D, 0.004, 0.2), 0.6), (filt(white(0.06), "hp", 4000) * env(0.06, 0.001, 0.01), 0.3)]
    return finish(mix(*parts), 0.7 if not heavy else 0.85, 1.2)


def swing(v):
    """A quick blade or fist swish."""
    P = pitch(v, 0.12); D = 0.22
    air = sweep(white(D), "bp", lambda t: (2400 + 3500 * t / D) * P, 2.0) * np.sin(np.pi * np.clip(tt(D) / D, 0, 1)) ** 2
    return finish(air, 0.6, 1.1)


def land(v, heavy=False):
    """Boots on the deck: a thud and a little rattle of gear."""
    P = pitch(v, 0.08); D = 0.7 if heavy else 0.35
    thud = osc(drop((95 if heavy else 120) * P, 40, 0.03, D)) * env(D, 0.001, 0.12 if heavy else 0.05)
    scuff = sweep(white(D), "lp", lambda t: 250 + 1800 * np.exp(-t / 0.015)) * env(D, 0.001, 0.04 if heavy else 0.025)
    gear = modal([1450 * P, 2380 * P], [0.04, 0.025], [1, 0.6], D, 0.001)
    parts = [(thud, 0.9), (scuff, 0.5), (gear, 0.08, 0.01)]
    if heavy: parts.append((sat(filt(white(D), "lp", 500) * env(D, 0.001, 0.1) * 2), 0.4))
    return finish(verb(mix(*parts), 0.1, "small"), 0.85, 1.6)


def pound(v):
    """A ground pound: a huge thud, the deck cracking, a rumble and grit falling."""
    P = pitch(v, 0.06); D = 1.6
    sub = osc(drop(85 * P, 28, 0.06, D)) * env(D, 0.001, 0.4)
    crack = filt(white(0.03), "hp", 2000) * env(0.03, 0.0001, 0.005)
    crunch = sat(sweep(white(D), "lp", lambda t: 200 + 5000 * np.exp(-t / 0.03)) * env(D, 0.0005, 0.15) * 3, 3)
    rumble = filt(brown(D), "lp", 180, order=2) * swell(D, 0.05, 1, 0.5)
    ring = modal([180 * P, 433 * P, 760 * P], [0.4, 0.25, 0.15], [1, 0.6, 0.3], D, 0.001)
    grit = tinkles(D, 10, 0.12, 0.9, 1500, 5000, 1.0, 0.4)
    x = mix((sub, 1.0), (crack, 0.6), (crunch, 0.5), (rumble, 0.6), (ring, 0.15), (grit, 0.12))
    return finish(verb(x, 0.25, "hall"), 0.89, 2.0)

# ---------------------------------------------------------------- interface

def ui_move(v):
    D = 0.09; x = osc(1760, D) * env(D, 0.001, 0.015) + 0.4 * osc(3520, D) * env(D, 0.0005, 0.006)
    return finish(x, 0.45, 1.0)


def ui_confirm(v):
    D = 0.4
    a = modal([880, 1760, 2640], [0.12, 0.06, 0.03], [1, 0.4, 0.2], D, 0.001)
    b = modal([1318.5, 2637], [0.2, 0.08], [1, 0.4], D, 0.001)
    x = mix((a, 0.5), (b, 0.5, 0.06))
    return finish(verb(x, 0.2, "plate"), 0.55, 1.0)


def ui_back(v):
    D = 0.35
    a = modal([987.8, 1975], [0.1, 0.04], [1, 0.3], D, 0.001)
    b = modal([659.3, 1318.5], [0.14, 0.05], [1, 0.3], D, 0.001)
    x = mix((a, 0.5), (b, 0.5, 0.06))
    return finish(verb(x, 0.15, "plate"), 0.5, 1.0)


def ui_rebind(v):
    D = 0.5
    parts = [(modal([f, f * 2], [0.15, 0.05], [1, 0.3], D, 0.001), 0.4, i * 0.05) for i, f in enumerate((1318.5, 1568, 2093))]
    parts.append((filt(white(0.3), "hp", 5000) * env(0.3, 0.01, 0.06), 0.06, 0.1))
    return finish(verb(mix(*parts), 0.2, "plate"), 0.55, 1.0)

# ---------------------------------------------------------------- the list
# name: (builder, variations, what it is and which events use it)
SOUNDS = {
    "blast_small": (lambda v: explosion(v, 0), 3, "Small explosion: crack, closing-filter fireball, sub thump. Splash, rivet blasts, small enemy blasts, sentry rockets."),
    "blast_med": (lambda v: explosion(v, 1), 3, "Medium explosion. Nova's blast, enemy blasts, frags, breach blasts, rocket jumps, gravity-well collapse."),
    "blast_big": (lambda v: explosion(v, 2), 3, "Big explosion with secondary detonations and falling debris. Kinetic release, slams, quake, pod landing, boss crash, Aegis detonation."),
    "blast_boss": (lambda v: explosion(v, 3), 2, "Boss-sized explosion: stacked detonations, deep groan, long rumble, debris rain. A boss going down, the Supernova's nova, the team finisher."),
    "beam_start": (beam_start, 1, "Level 4 beam firing up: charge whine into ignition. beamStart."),
    "beam_loop": (lambda v: beam_loop(v), 1, "Level 4 beam sustained (2 s seamless loop): detuned buzz, rushing air, sub, shimmer, crackle. While the beam is live."),
    "beam_impact_loop": (beam_impact_loop, 1, "Where the beam meets something (1.5 s seamless loop): frying sizzle and hum. Under the beam loop."),
    "beam_end": (beam_end, 1, "Level 4 beam shutting off: falling buzz, closing filter, sparks. beamEnd."),
    "nova_gather": (nova_gather, 1, "Supernova gathering light: a reverse-swell cut at 0.4 s. ultBegin (Nova)."),
    "nova_fire": (nova_fire, 1, "Supernova igniting: the biggest hit Nova makes. When the Supernova's beam opens."),
    "nova_loop": (lambda v: beam_loop(v, True), 1, "Supernova sustained (2 s seamless loop): the beam an octave down with a singing formant pad."),
    "laser_charge": (laser_charge, 1, "Boss laser charging: rising whine with a pulse that speeds up. Boss laser or sweep wind-up."),
    "laser_fire": (laser_fire, 1, "Boss laser firing: a harsh zap and thump. bossLaser."),
    "laser_loop": (laser_loop, 1, "Boss laser sustained (2 s seamless loop): ring-modulated square buzz with grit. While a boss laser is live."),
    "shot_nova": (shot_nova, 3, "Nova's light shot: energy chirp, puff, little thump. shot (level 0)."),
    "shot_charged": (shot_charged, 1, "Nova's charged shot (level 1)."),
    "shot_heavy": (shot_heavy, 1, "Nova's full charged shot (level 2 and up)."),
    "shot_perfect": (shot_perfect, 1, "Perfect Release: the heavy shot with a bell chord and sparkle. shot with perfect."),
    "cannon": (lambda v: cannon(v), 2, "RAM's Breach Cannon: mechanical boom with a ringing barrel. shot (cannon)."),
    "cannon_big": (lambda v: cannon(v, True), 1, "RAM's charged cannon (level 2 and up)."),
    "rivet": (lambda v: rivet(v), 3, "Fix's rivet gun: pneumatic puff, metal tick, thunk. shot (rivet)."),
    "rivet_heavy": (lambda v: rivet(v, True), 1, "Fix's charged rivet."),
    "rifle": (rifle, 2, "Echo's rifle: supersonic crack, report, whip, rolling tail. snipe."),
    "rifle_bolt": (rifle_bolt, 1, "Echo's bolt cycling after a shot."),
    "hit_light": (hit_light, 4, "Light melee or shot hit. hit."),
    "hit_heavy": (hit_heavy, 3, "Heavy hit: crunch and sub. hit (heavy), charge crashes, yanks, RAM's bonks."),
    "clang": (clang, 3, "Metal clang on armour: an inharmonic plate ring. blocked, armorHit (metal)."),
    "crack": (crack, 3, "Ceramic armour plate cracking: snap, brittle pings, grain rattle. armorHit."),
    "armor_break": (armor_break, 2, "Armour shattering off: crack, clang, shards, thump. armorBreak."),
    "shatter": (shatter, 3, "Enemy breaking up: ceramic-and-metal burst, clangs, debris settling. kill; shield breaks."),
    "debris": (debris, 6, "One piece of debris clinking. Scattered after kills and big blasts; grenade bounces; scrap."),
    "hurt": (hurt, 3, "Player taking a hit: body blow and suit crackle. playerHit."),
    "parry": (parry, 2, "Normal parry: a short, dry metallic clank. parry."),
    "parry_perfect": (parry_perfect, 1, "Perfect parry: a long bright bell chord with shimmer and a sub hit. parry (perfect)."),
    "block": (block, 3, "Hit on a hard-light shield: thump, glassy ping, sizzle. Nova's shield blocks, RAM's guard blocks."),
    "whoosh": (lambda v: whoosh(v), 3, "Dash: air torn past. dash (levels 0-1), dodges."),
    "whoosh_heavy": (lambda v: whoosh(v, True), 1, "Charged dash with a sub push. dash (level 2 and up), dash slashes."),
    "swing": (swing, 3, "Quick blade or fist swish. swing."),
    "land": (lambda v: land(v), 2, "Landing thud. land."),
    "land_heavy": (lambda v: land(v, True), 1, "Hard landing from a long fall. land (fast)."),
    "pound": (pound, 2, "Ground pound: huge thud, deck crack, rumble, grit. poundLand, slams, leap landings."),
    "ui_move": (ui_move, 1, "Menu cursor move."),
    "ui_confirm": (ui_confirm, 1, "Menu confirm."),
    "ui_back": (ui_back, 1, "Menu back."),
    "ui_rebind": (ui_rebind, 1, "A control rebound (input captured)."),
}


def wav_bytes(x):
    pcm = np.clip(np.round(x * 32767), -32768, 32767).astype("<i2").tobytes()
    b = io.BytesIO()
    with wave.open(b, "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR); w.writeframes(pcm)
    return b.getvalue()


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--list", action="store_true"); ap.add_argument("--only", default="")
    ap.add_argument("--reel", default=""); ap.add_argument("--out", default=OUT)
    a = ap.parse_args()
    if a.list:
        for k, (_, n, doc) in SOUNDS.items(): print(f"{k + ('_1..' + str(n) if n > 1 else ''):24s} {doc}")
        return
    os.makedirs(a.out, exist_ok=True); total = 0; reel = []
    for name, (fn, count, _) in SOUNDS.items():
        if a.only and not name.startswith(a.only): continue
        for v in range(1, count + 1):
            global rng
            rng = np.random.default_rng(sum(map(ord, name)) * 7919 + v * 104729)   # stable per sound and variation
            x = fn(v); fname = f"{name}_{v}" if count > 1 else name
            if not np.all(np.isfinite(x)): sys.exit(f"{fname}: not finite")
            b = wav_bytes(x); total += len(b)
            with open(os.path.join(a.out, fname + ".wav.bytes"), "wb") as f: f.write(b)
            print(f"{fname:24s} {len(x) / SR:5.2f} s  peak {np.max(np.abs(x)):.2f}  rms {np.sqrt(np.mean(x ** 2)):.3f}")
            reel += [x, np.zeros(n_of(0.45))] if v == 1 or not name.startswith("debris") else [x, np.zeros(n_of(0.1))]
            if name.endswith("_loop"): reel += [np.tile(x, 2), np.zeros(n_of(0.45))]   # loops twice more, to hear the seam
    print(f"total {total / 1e6:.2f} MB")
    if a.reel:
        r = np.concatenate(reel)
        with open(a.reel, "wb") as f: f.write(wav_bytes(r))
        print(f"reel {a.reel}: {len(r) / SR:.1f} s")


if __name__ == "__main__":
    main()
