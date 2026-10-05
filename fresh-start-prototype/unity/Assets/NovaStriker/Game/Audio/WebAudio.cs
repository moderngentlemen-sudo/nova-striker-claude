// A small Web Audio work-alike, so the prototype's synthesized sound (audio.js) and score (music.js) port line
// for line: oscillators, noise sources, biquad filters, gains, a delay, a reverb and a compressor, joined into a
// graph, with Web Audio's parameter automation (setValueAtTime, linear and exponential ramps, setTargetAtTime,
// cancelScheduledValues) and audio-rate modulation of parameters (an LFO into a gain or a detune).
// The graph renders in Unity's audio callback (AudioOut, on the listener's object) in mono, copied to every
// channel. The main thread builds and schedules nodes under the context's lock; times are in the context's
// clock (currentTime, seconds of audio rendered).
// Differences from a browser: the reverb is algorithmic (comb and all-pass filters) instead of a convolution
// with a generated impulse, and parameters are evaluated every 16 samples (Web Audio's k-rate is 128).
using System;
using System.Collections.Generic;
using UnityEngine;

namespace NovaStriker.Game.Audio
{
    public enum Wave { Sine, Square, Sawtooth, Triangle }
    public enum FilterType { Lowpass, Highpass, Bandpass }

    // An automatable parameter (AudioParam)
    public sealed class Param
    {
        enum K { Set, Lin, Exp, Target }
        struct E { public K k; public double t, v, tau; }
        readonly List<E> evs = new List<E>();
        public double value;                 // the intrinsic value (and the current one)
        double segT, segV;                   // where the current segment started
        bool targeting; double tgT, tgV0, tgTarget, tgTau;
        internal readonly List<Node> mods = new List<Node>();   // audio-rate inputs, summed onto the value
        public Param(double v) { value = v; segV = v; }

        void Insert(E e) { int i = evs.Count; while (i > 0 && evs[i - 1].t > e.t) i--; evs.Insert(i, e); }
        public Param setValueAtTime(double v, double t) { Insert(new E { k = K.Set, t = t, v = v }); return this; }
        public Param linearRampToValueAtTime(double v, double t) { Insert(new E { k = K.Lin, t = t, v = v }); return this; }
        public Param exponentialRampToValueAtTime(double v, double t) { Insert(new E { k = K.Exp, t = t, v = v }); return this; }
        public Param setTargetAtTime(double v, double t, double tau) { Insert(new E { k = K.Target, t = t, v = v, tau = Math.Max(1e-4, tau) }); return this; }
        public Param cancelScheduledValues(double t) { evs.RemoveAll(e => e.t >= t); return this; }

        // The value at time t (t only moves forward)
        internal double At(double t)
        {
            while (evs.Count > 0 && evs[0].t <= t)
            {
                var e = evs[0]; evs.RemoveAt(0);
                double cur = Eval(e.t);
                switch (e.k)
                {
                    case K.Set: value = e.v; targeting = false; break;
                    case K.Lin: case K.Exp: value = e.v; targeting = false; break;
                    case K.Target: targeting = true; tgT = e.t; tgV0 = cur; tgTarget = e.v; tgTau = e.tau; value = cur; break;
                }
                segT = e.t; segV = e.k == K.Target ? cur : value;
            }
            value = Eval(t);
            return value;
        }
        double Eval(double t)
        {
            if (evs.Count > 0 && (evs[0].k == K.Lin || evs[0].k == K.Exp))
            {
                var e = evs[0]; double t0 = segT, v0 = targeting ? Target(segT) : segV;
                if (e.t <= t0) return e.v;
                double f = Math.Max(0, Math.Min(1, (t - t0) / (e.t - t0)));
                if (e.k == K.Lin) return v0 + (e.v - v0) * f;
                if (v0 <= 0 || e.v <= 0 || v0 * e.v <= 0) return f < 1 ? v0 : e.v;
                return v0 * Math.Pow(e.v / v0, f);
            }
            return targeting ? Target(t) : value;
        }
        double Target(double t) => tgTarget + (tgV0 - tgTarget) * Math.Exp(-(t - tgT) / tgTau);
    }

    public abstract class Node
    {
        protected readonly Ctx c;
        internal readonly List<Node> inputs = new List<Node>();
        internal readonly float[] buf;
        long stamp = -1;
        public bool keep;   // a bus that stays in the graph with nothing playing into it
        protected Node(Ctx c) { this.c = c; buf = new float[Ctx.BLOCK]; }
        public T connect<T>(T dest) where T : Node { lock (c.gate) dest.inputs.Add(this); return dest; }
        public void connect(Param p) { lock (c.gate) p.mods.Add(this); }
        internal float[] Pull(long block) { if (stamp != block) { stamp = block; Render(block); } return buf; }
        protected abstract void Render(long block);
        internal virtual bool Done => !keep && inputs.Count == 0;
        // Sums the live inputs into buf (and drops the finished ones)
        protected void SumInputs(long block)
        {
            Array.Clear(buf, 0, buf.Length);
            for (int i = inputs.Count - 1; i >= 0; i--)
            {
                var n = inputs[i]; var b = n.Pull(block);
                for (int j = 0; j < buf.Length; j++) buf[j] += b[j];
                if (n.Done) inputs.RemoveAt(i);
            }
        }
        // Per-sample modulation of a parameter: the sum of its audio-rate inputs this block (or null)
        protected float[] Mods(Param p, long block)
        {
            if (p.mods.Count == 0) return null;
            var m = c.scratch(p);
            Array.Clear(m, 0, m.Length);
            for (int i = p.mods.Count - 1; i >= 0; i--)
            {
                var n = p.mods[i]; var b = n.Pull(block);
                for (int j = 0; j < m.Length; j++) m[j] += b[j];
                if (n.Done) p.mods.RemoveAt(i);
            }
            return m;
        }
    }

    // A scheduled source (oscillator or noise): silent before start and after stop, then finished
    public abstract class Source : Node
    {
        protected double startT = double.MaxValue, stopT = double.MaxValue;
        bool finished;
        protected Source(Ctx c) : base(c) { }
        public void start(double t = 0) { startT = Math.Max(t, 0); }
        public void stop(double t = 0) { stopT = t; }
        internal override bool Done => finished;
        protected override void Render(long block)
        {
            double t0 = c.BlockTime(block), dt = 1.0 / c.sampleRate;
            Generate(block, t0, dt);
            if (t0 + Ctx.BLOCK * dt >= stopT) finished = true;
        }
        protected abstract void Generate(long block, double t0, double dt);
        protected bool On(double t) => t >= startT && t < stopT;
    }

    public sealed class Oscillator : Source
    {
        public Wave type = Wave.Sine;
        public readonly Param frequency = new Param(440), detune = new Param(0);
        double phase;
        public Oscillator(Ctx c) : base(c) { }
        static double Blep(double t, double dt)
        {
            if (t < dt) { t /= dt; return t + t - t * t - 1; }
            if (t > 1 - dt) { t = (t - 1) / dt; return t * t + t + t + 1; }
            return 0;
        }
        protected override void Generate(long block, double t0, double dt)
        {
            var fm = Mods(frequency, block); var dm = Mods(detune, block);
            double f = 0, d = 0;
            for (int i = 0; i < buf.Length; i++)
            {
                double t = t0 + i * dt;
                if ((i & 15) == 0) { f = frequency.At(t); d = detune.At(t); }
                if (!On(t)) { buf[i] = 0; continue; }
                double hz = (f + (fm != null ? fm[i] : 0)) * Math.Pow(2, (d + (dm != null ? dm[i] : 0)) / 1200);
                double inc = Math.Max(0, Math.Min(0.5, hz * dt)), s;
                switch (type)
                {
                    case Wave.Square: s = (phase < 0.5 ? 1 : -1) + Blep(phase, inc) - Blep((phase + 0.5) % 1, inc); break;
                    case Wave.Sawtooth: s = 2 * phase - 1 - Blep(phase, inc); break;
                    case Wave.Triangle: s = phase < 0.5 ? 4 * phase - 1 : 3 - 4 * phase; break;
                    default: s = Math.Sin(phase * 2 * Math.PI); break;
                }
                buf[i] = (float)s;
                phase += inc; if (phase >= 1) phase -= 1;
            }
        }
    }

    // White noise (the prototype loops a buffer of random samples; fresh random samples sound the same)
    public sealed class Noise : Source
    {
        readonly System.Random rng;
        public Noise(Ctx c) : base(c) { rng = c.rng; }
        protected override void Generate(long block, double t0, double dt)
        {
            for (int i = 0; i < buf.Length; i++) buf[i] = On(t0 + i * dt) ? (float)(rng.NextDouble() * 2 - 1) : 0;
        }
    }

    public sealed class Gain : Node
    {
        public readonly Param gain = new Param(1);
        public Gain(Ctx c, double g = 1) : base(c) { gain.value = g; }
        protected override void Render(long block)
        {
            SumInputs(block);
            var m = Mods(gain, block);
            double t0 = c.BlockTime(block), dt = 1.0 / c.sampleRate, g = 0;
            for (int i = 0; i < buf.Length; i++)
            {
                if ((i & 15) == 0) g = gain.At(t0 + i * dt);
                buf[i] *= (float)(g + (m != null ? m[i] : 0));
            }
        }
    }

    // RBJ biquads; Q is linear for the band-pass and, as in Web Audio, in dB for the low- and high-pass
    public sealed class Biquad : Node
    {
        public FilterType type = FilterType.Lowpass;
        public readonly Param frequency = new Param(350), Q = new Param(1);
        double x1, x2, y1, y2, b0, b1, b2, a1, a2;
        public Biquad(Ctx c) : base(c) { }
        void Coef(double f, double q)
        {
            double w = 2 * Math.PI * Math.Max(10, Math.Min(f, c.sampleRate * 0.49)) / c.sampleRate, cs = Math.Cos(w), sn = Math.Sin(w);
            double Ql = type == FilterType.Bandpass ? Math.Max(1e-3, q) : Math.Max(1e-3, Math.Pow(10, q / 20));
            double alpha = sn / (2 * Ql), a0;
            switch (type)
            {
                case FilterType.Highpass: b0 = (1 + cs) / 2; b1 = -(1 + cs); b2 = (1 + cs) / 2; break;
                case FilterType.Bandpass: b0 = alpha; b1 = 0; b2 = -alpha; break;
                default: b0 = (1 - cs) / 2; b1 = 1 - cs; b2 = (1 - cs) / 2; break;
            }
            a0 = 1 + alpha; a1 = -2 * cs / a0; a2 = (1 - alpha) / a0; b0 /= a0; b1 /= a0; b2 /= a0;
        }
        protected override void Render(long block)
        {
            SumInputs(block);
            double t0 = c.BlockTime(block), dt = 1.0 / c.sampleRate;
            for (int i = 0; i < buf.Length; i++)
            {
                if ((i & 15) == 0) Coef(frequency.At(t0 + i * dt), Q.At(t0 + i * dt));
                double x = buf[i], y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
                x2 = x1; x1 = x; y2 = y1; y1 = y;
                buf[i] = (float)y;
            }
        }
        internal override bool Done => base.Done && Math.Abs(y1) < 1e-5 && Math.Abs(y2) < 1e-5;
    }

    // A delay line (its input is read after its output is written, so it can sit in a feedback loop)
    public sealed class Delay : Node
    {
        public double delayTime;
        readonly float[] ring, inBuf = new float[Ctx.BLOCK]; int w;
        public Delay(Ctx c, double maxSeconds) : base(c) { ring = new float[(int)(c.sampleRate * maxSeconds) + Ctx.BLOCK * 2]; keep = true; }
        protected override void Render(long block)
        {
            int d = Math.Max(Ctx.BLOCK, (int)(delayTime * c.sampleRate));
            for (int i = 0; i < buf.Length; i++) buf[i] = ring[(w + i - d + ring.Length * 2) % ring.Length];
            Array.Clear(inBuf, 0, inBuf.Length);
            for (int k = inputs.Count - 1; k >= 0; k--) { var b = inputs[k].Pull(block); for (int j = 0; j < buf.Length; j++) inBuf[j] += b[j]; if (inputs[k].Done) inputs.RemoveAt(k); }
            for (int i = 0; i < buf.Length; i++) ring[(w + i) % ring.Length] = inBuf[i];
            w = (w + buf.Length) % ring.Length;
        }
    }

    // A reverb (Freeverb's comb and all-pass network), standing in for the prototype's convolution with a
    // generated, exponentially decaying noise impulse of `seconds`
    public sealed class Reverb : Node
    {
        readonly float[][] comb; readonly int[] ci; readonly float[] cf; readonly float[][] ap; readonly int[] ai;
        readonly float feedback, damp = 0.25f;
        public Reverb(Ctx c, double seconds) : base(c)
        {
            keep = true;
            int[] cl = { 1116, 1188, 1277, 1356, 1422, 1491, 1557, 1617 }, al = { 556, 441, 341, 225 };
            double k = c.sampleRate / 44100.0;
            comb = new float[8][]; ci = new int[8]; cf = new float[8];
            for (int i = 0; i < 8; i++) comb[i] = new float[(int)(cl[i] * k)];
            ap = new float[4][]; ai = new int[4];
            for (int i = 0; i < 4; i++) ap[i] = new float[(int)(al[i] * k)];
            // feedback so a comb decays by 60 dB in about the impulse's length
            double avg = 1400 * k / c.sampleRate;
            feedback = (float)Math.Min(0.97, Math.Pow(10, -3 * avg / Math.Max(0.3, seconds * 0.6)));
        }
        protected override void Render(long block)
        {
            SumInputs(block);
            for (int i = 0; i < buf.Length; i++)
            {
                float x = buf[i] * 0.015f, o = 0;
                for (int k = 0; k < 8; k++)
                {
                    var b = comb[k]; float y = b[ci[k]];
                    cf[k] = y * (1 - damp) + cf[k] * damp;
                    b[ci[k]] = x + cf[k] * feedback;
                    if (++ci[k] >= b.Length) ci[k] = 0;
                    o += y;
                }
                for (int k = 0; k < 4; k++)
                {
                    var b = ap[k]; float y = b[ai[k]];
                    b[ai[k]] = o + y * 0.5f; o = y - o;
                    if (++ai[k] >= b.Length) ai[k] = 0;
                }
                buf[i] = o;
            }
        }
    }

    // DynamicsCompressorNode: soft knee, ratio, attack and release, and Web Audio's automatic make-up gain
    public sealed class Compressor : Node
    {
        public double threshold = -24, knee = 30, ratio = 12, attack = 0.003, release = 0.25;
        double env = 0;   // gain reduction, dB
        public Compressor(Ctx c) : base(c) { keep = true; }
        double Curve(double db)
        {
            double over = db - threshold;
            if (2 * over < -knee) return db;
            if (2 * Math.Abs(over) <= knee) return db + (1 / ratio - 1) * (over + knee / 2) * (over + knee / 2) / (2 * Math.Max(1e-6, knee));
            return threshold + over / ratio;
        }
        protected override void Render(long block)
        {
            SumInputs(block);
            double makeup = Math.Pow(10, 0.6 * -Curve(0) / 20), dt = 1.0 / c.sampleRate;
            double ka = 1 - Math.Exp(-dt / attack), kr = 1 - Math.Exp(-dt / release);
            for (int i = 0; i < buf.Length; i++)
            {
                double x = Math.Abs(buf[i]), db = x > 1e-6 ? 20 * Math.Log10(x) : -120, want = db - Curve(db);
                env += (want - env) * (want > env ? ka : kr);
                buf[i] = (float)(buf[i] * Math.Pow(10, -env / 20) * makeup);
            }
        }
    }

    // The context: the clock, the graph's root and the lock shared with the audio thread
    public sealed class Ctx
    {
        public const int BLOCK = 128;
        public readonly int sampleRate;
        public readonly object gate = new object();
        public readonly Gain destination;
        internal readonly System.Random rng = new System.Random(7);
        long block;
        double now;
        float[] pending = new float[0]; int pendingAt;
        readonly Dictionary<Param, float[]> scratchBufs = new Dictionary<Param, float[]>();

        public Ctx(int sampleRate) { this.sampleRate = sampleRate; destination = new Gain(this) { keep = true }; }
        public double currentTime => System.Threading.Volatile.Read(ref now);
        internal double BlockTime(long b) => (double)b * BLOCK / sampleRate;
        internal float[] scratch(Param p) { if (!scratchBufs.TryGetValue(p, out var b)) scratchBufs[p] = b = new float[BLOCK]; return b; }

        public Oscillator createOscillator() => new Oscillator(this);
        public Noise createNoise() => new Noise(this);
        public Gain createGain(double g = 1) => new Gain(this, g);
        public Biquad createBiquadFilter() => new Biquad(this);
        public Delay createDelay(double max) => new Delay(this, max);
        public Reverb createReverb(double seconds) => new Reverb(this, seconds);
        public Compressor createDynamicsCompressor() => new Compressor(this);

        // Unity's audio callback: render blocks into its buffer (mono, to every channel)
        public void Fill(float[] data, int channels)
        {
            int frames = data.Length / channels, f = 0;
            lock (gate)
            {
                while (f < frames)
                {
                    if (pendingAt >= pending.Length)
                    {
                        pending = destination.Pull(block); pendingAt = 0; block++;
                        if (scratchBufs.Count > 4096) scratchBufs.Clear();
                    }
                    int n = Math.Min(frames - f, pending.Length - pendingAt);
                    for (int i = 0; i < n; i++)
                    {
                        float s = Mathf.Clamp(pending[pendingAt + i], -1, 1);
                        for (int ch = 0; ch < channels; ch++) data[(f + i) * channels + ch] += s;
                    }
                    f += n; pendingAt += n;
                }
                System.Threading.Volatile.Write(ref now, BlockTime(block));
            }
        }
    }

    // Feeds the context to Unity's mixer: sits on the object with the AudioListener
    public sealed class AudioOut : MonoBehaviour
    {
        public Ctx ctx;
        void OnAudioFilterRead(float[] data, int channels) { ctx?.Fill(data, channels); }
    }
}
