// The designed sound effects: WAV data in Resources/NovaStriker/Audio/<name>.wav.bytes (Art/Audio/make_sfx.py
// writes them; a recorded WAV saved under the same name replaces one). The .bytes extension makes Unity import
// each as a TextAsset, so the PCM is parsed here and no import settings can change it. A sound comes in
// variations (`blast_big_1`, `blast_big_2`, ...) or alone (`beam_loop`). Loaded lazily, on the main thread, the
// first time each is asked for; a missing one is remembered as missing, and the caller falls back to the
// synthesized sound.
using System.Collections.Generic;
using UnityEngine;

namespace NovaStriker.Game.Audio
{
    public sealed class Sample
    {
        public float[] data;   // mono
        public int rate;
        public double Duration => data.Length / (double)rate;
    }

    public static class SampleBank
    {
        const string DIR = "NovaStriker/Audio/";
        const int MAX_VARIATIONS = 8;
        static readonly Dictionary<string, Sample> one = new Dictionary<string, Sample>();
        static readonly Dictionary<string, Sample[]> sets = new Dictionary<string, Sample[]>();
        // Where the bytes come from: Resources in the game; a test outside Unity can read the files from disk instead
        public static System.Func<string, byte[]> load = FromResources;
        static byte[] FromResources(string name)
        {
            var ta = Resources.Load<TextAsset>(DIR + name + ".wav");   // Unity drops the .bytes from the name
            if (ta == null) return null;
            var b = ta.bytes; Resources.UnloadAsset(ta); return b;
        }

        // One file by its exact name (without .wav), or null
        public static Sample Get(string name)
        {
            if (one.TryGetValue(name, out var s)) return s;
            var b = load(name);
            s = b != null ? Parse(b) : null;
            if (b != null && s == null) Debug.LogWarning($"Nova Striker: {name}.wav.bytes is not a WAV this game can read (PCM 8/16/24/32-bit or float); using the synthesized sound.");
            one[name] = s; return s;
        }

        // A sound's variations: name_1..name_8 if they exist, else the single file `name`; null when there is none
        public static Sample[] Set(string name)
        {
            if (sets.TryGetValue(name, out var a)) return a;
            var list = new List<Sample>();
            for (int i = 1; i <= MAX_VARIATIONS; i++) { var s = Get(name + "_" + i); if (s == null) break; list.Add(s); }
            if (list.Count == 0) { var s = Get(name); if (s != null) list.Add(s); }
            a = list.Count > 0 ? list.ToArray() : null;
            sets[name] = a; return a;
        }
        public static bool Has(string name) => Set(name) != null;

        // RIFF WAVE: PCM 8/16/24/32-bit or IEEE float 32/64-bit (also in WAVE_FORMAT_EXTENSIBLE), any channel count
        // (averaged to mono), any rate. Null when it isn't one.
        public static Sample Parse(byte[] b)
        {
            if (b == null || b.Length < 44 || Tag(b, 0) != "RIFF" || Tag(b, 8) != "WAVE") return null;
            int fmt = 0, ch = 0, rate = 0, bits = 0, dataAt = -1, dataLen = 0, p = 12;
            while (p + 8 <= b.Length)
            {
                string id = Tag(b, p); int len = I32(b, p + 4), body = p + 8;
                if (len < 0 || body + len > b.Length) len = b.Length - body;   // a truncated last chunk: read what is there
                if (id == "fmt " && len >= 16)
                {
                    fmt = U16(b, body); ch = U16(b, body + 2); rate = I32(b, body + 4); bits = U16(b, body + 14);
                    if (fmt == 0xFFFE && len >= 26) fmt = U16(b, body + 24);   // the sub-format's first two bytes
                }
                else if (id == "data") { dataAt = body; dataLen = len; break; }
                p = body + len + (len & 1);
            }
            if (dataAt < 0 || ch < 1 || rate < 1000) return null;
            int bps = bits / 8; if (bps < 1) return null;
            bool flt = fmt == 3; if (!(fmt == 1 || flt) || (flt && bps != 4 && bps != 8) || (!flt && bps > 4)) return null;
            int frames = dataLen / (bps * ch);
            var o = new float[frames];
            for (int f = 0; f < frames; f++)
            {
                double sum = 0;
                for (int c = 0; c < ch; c++)
                {
                    int q = dataAt + (f * ch + c) * bps;
                    sum += flt ? (bps == 4 ? System.BitConverter.ToSingle(b, q) : System.BitConverter.ToDouble(b, q))
                        : bps == 1 ? (b[q] - 128) / 128.0
                        : bps == 2 ? (short)(b[q] | b[q + 1] << 8) / 32768.0
                        : bps == 3 ? ((b[q] << 8 | b[q + 1] << 16 | b[q + 2] << 24) >> 8) / 8388608.0
                        : I32(b, q) / 2147483648.0;
                }
                double v = sum / ch;
                o[f] = double.IsNaN(v) ? 0 : (float)System.Math.Max(-1, System.Math.Min(1, v));
            }
            return new Sample { data = o, rate = rate };
        }
        static string Tag(byte[] b, int p) => p + 4 <= b.Length ? System.Text.Encoding.ASCII.GetString(b, p, 4) : "";
        static int U16(byte[] b, int p) => b[p] | b[p + 1] << 8;
        static int I32(byte[] b, int p) => b[p] | b[p + 1] << 8 | b[p + 2] << 16 | b[p + 3] << 24;
    }
}
