// A small software 2D canvas with the parts of the HTML canvas API the prototype draws its textures with
// (glows, sparks, cracks, decals, the level's surface plating): rectangles, paths of lines and arcs, filled
// (non-zero winding, antialiased) or stroked (round joins), solid colours and linear / radial gradients, a
// transform stack, global alpha, and 'lighten' compositing. ToTexture uploads it the way three.js's
// CanvasTexture does (the canvas's top row at the top of the texture).
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace NovaStriker.Game.Three
{
    public sealed class Gradient2D
    {
        readonly bool radial;
        readonly float x0, y0, r0, x1, y1, r1;
        readonly List<(float t, Color c)> stops = new List<(float, Color)>();
        internal Matrix2D space;   // the transform current when it was created (canvas gradients live in user space)
        internal Gradient2D(bool radial, float x0, float y0, float r0, float x1, float y1, float r1, Matrix2D space)
        { this.radial = radial; this.x0 = x0; this.y0 = y0; this.r0 = r0; this.x1 = x1; this.y1 = y1; this.r1 = r1; this.space = space; }
        public void addColorStop(float t, string css) { stops.Add((t, Paint.Css(css))); stops.Sort((a, b) => a.t.CompareTo(b.t)); }
        internal Color At(float ux, float uy)
        {
            float t;
            if (radial)
            {
                // (concentric gradients exactly; offset ones measured from the outer circle's centre)
                float d = Mathf.Sqrt((ux - x1) * (ux - x1) + (uy - y1) * (uy - y1));
                t = r1 - r0 == 0 ? 1 : (d - r0) / (r1 - r0);
            }
            else
            {
                float dx = x1 - x0, dy = y1 - y0, l2 = dx * dx + dy * dy;
                t = l2 == 0 ? 0 : ((ux - x0) * dx + (uy - y0) * dy) / l2;
            }
            if (stops.Count == 0) return new Color(0, 0, 0, 0);
            if (t <= stops[0].t) return stops[0].c;
            for (int i = 1; i < stops.Count; i++)
                if (t <= stops[i].t)
                {
                    var a = stops[i - 1]; var b = stops[i];
                    float k = b.t - a.t <= 0 ? 1 : (t - a.t) / (b.t - a.t);
                    // (canvas interpolates in premultiplied space)
                    Color pa = new Color(a.c.r * a.c.a, a.c.g * a.c.a, a.c.b * a.c.a, a.c.a), pb = new Color(b.c.r * b.c.a, b.c.g * b.c.a, b.c.b * b.c.a, b.c.a);
                    var p = Color.Lerp(pa, pb, k);
                    return p.a > 0 ? new Color(p.r / p.a, p.g / p.a, p.b / p.a, p.a) : new Color(0, 0, 0, 0);
                }
            return stops[stops.Count - 1].c;
        }
    }

    public struct Matrix2D
    {
        public float a, b, c, d, e, f;   // x' = a x + c y + e, y' = b x + d y + f
        public static Matrix2D Identity => new Matrix2D { a = 1, d = 1 };
        public Vector2 Apply(float x, float y) => new Vector2(a * x + c * y + e, b * x + d * y + f);
        public Matrix2D Mul(Matrix2D m) => new Matrix2D
        {
            a = a * m.a + c * m.b, b = b * m.a + d * m.b, c = a * m.c + c * m.d, d = b * m.c + d * m.d,
            e = a * m.e + c * m.f + e, f = b * m.e + d * m.f + f,
        };
        public Matrix2D Inverse()
        {
            float det = a * d - b * c; if (det == 0) return Identity;
            float ia = d / det, ib = -b / det, ic = -c / det, id = a / det;
            return new Matrix2D { a = ia, b = ib, c = ic, d = id, e = -(ia * e + ic * f), f = -(ib * e + id * f) };
        }
        public float Scale => Mathf.Sqrt(Mathf.Abs(a * d - b * c));
    }

    public sealed class Paint
    {
        public readonly int width, height;
        readonly Color[] px;           // straight alpha; row 0 is the canvas's top
        readonly float[] cov;          // coverage scratch for one shape
        Matrix2D tf = Matrix2D.Identity;
        readonly Stack<(Matrix2D, float, string, Gradient2D, string, Gradient2D, float, string)> saved = new Stack<(Matrix2D, float, string, Gradient2D, string, Gradient2D, float, string)>();
        readonly List<List<Vector2>> path = new List<List<Vector2>>();
        List<Vector2> sub;

        public float globalAlpha = 1, lineWidth = 1;
        public string globalCompositeOperation = "source-over", lineCap = "butt", lineJoin = "miter";
        string _fill = "#000"; Gradient2D _fillG; string _stroke = "#000"; Gradient2D _strokeG;
        Color fillC = Color.black, strokeC = Color.black;

        public Paint(int w, int h) { width = w; height = h; px = new Color[w * h]; cov = new float[w * h]; }

        // fillStyle / strokeStyle take a CSS colour or a gradient
        public object fillStyle { set { if (value is Gradient2D g) { _fillG = g; } else { _fillG = null; _fill = (string)value; fillC = Css(_fill); } } }
        public object strokeStyle { set { if (value is Gradient2D g) { _strokeG = g; } else { _strokeG = null; _stroke = (string)value; strokeC = Css(_stroke); } } }

        public void save() => saved.Push((tf, globalAlpha, _fill, _fillG, _stroke, _strokeG, lineWidth, globalCompositeOperation));
        public void restore()
        {
            if (saved.Count == 0) return;
            var s = saved.Pop(); tf = s.Item1; globalAlpha = s.Item2; fillStyle = (object)s.Item4 ?? s.Item3; strokeStyle = (object)s.Item6 ?? s.Item5;
            lineWidth = s.Item7; globalCompositeOperation = s.Item8;
        }
        public void translate(float x, float y) => tf = tf.Mul(new Matrix2D { a = 1, d = 1, e = x, f = y });
        public void rotate(float r) { float c = Mathf.Cos(r), s = Mathf.Sin(r); tf = tf.Mul(new Matrix2D { a = c, b = s, c = -s, d = c }); }
        public void scale(float x, float y) => tf = tf.Mul(new Matrix2D { a = x, d = y });
        public void setTransform(float a, float b, float c, float d, float e, float f) => tf = new Matrix2D { a = a, b = b, c = c, d = d, e = e, f = f };

        public Gradient2D createRadialGradient(float x0, float y0, float r0, float x1, float y1, float r1) => new Gradient2D(true, x0, y0, r0, x1, y1, r1, tf);
        public Gradient2D createLinearGradient(float x0, float y0, float x1, float y1) => new Gradient2D(false, x0, y0, 0, x1, y1, 0, tf);

        // ---- Paths ----
        public void beginPath() { path.Clear(); sub = null; }
        public void moveTo(float x, float y) { sub = new List<Vector2> { tf.Apply(x, y) }; path.Add(sub); }
        public void lineTo(float x, float y) { if (sub == null) moveTo(x, y); else sub.Add(tf.Apply(x, y)); }
        public void closePath() { if (sub != null && sub.Count > 1) { sub.Add(sub[0]); sub = new List<Vector2> { sub[0] }; path.Add(sub); } }
        public void rect(float x, float y, float w, float h) { moveTo(x, y); lineTo(x + w, y); lineTo(x + w, y + h); lineTo(x, y + h); closePath(); }
        public void arc(float x, float y, float r, float a0, float a1, bool ccw = false)
        {
            float sweep;
            if (!ccw) { sweep = a1 - a0; if (sweep >= Mathf.PI * 2) sweep = Mathf.PI * 2; else { sweep %= Mathf.PI * 2; if (sweep < 0) sweep += Mathf.PI * 2; } }
            else { sweep = a1 - a0; if (sweep <= -Mathf.PI * 2) sweep = -Mathf.PI * 2; else { sweep %= Mathf.PI * 2; if (sweep > 0) sweep -= Mathf.PI * 2; } }
            int n = Mathf.Max(4, Mathf.CeilToInt(Mathf.Abs(sweep) * Mathf.Max(4, r * tf.Scale) / 6f));
            n = Mathf.Min(n, 256);
            for (int i = 0; i <= n; i++)
            {
                float a = a0 + sweep * i / n, xx = x + Mathf.Cos(a) * r, yy = y + Mathf.Sin(a) * r;
                if (i == 0) { if (sub == null) moveTo(xx, yy); else lineTo(xx, yy); }
                else lineTo(xx, yy);
            }
        }

        public void fillRect(float x, float y, float w, float h)
        {
            if (w < 0) { x += w; w = -w; }
            if (h < 0) { y += h; h = -h; }
            // axis-aligned under the current transform: exact coverage, no path
            if (tf.b == 0 && tf.c == 0)
            {
                var p0 = tf.Apply(x, y); var p1 = tf.Apply(x + w, y + h);
                float x0 = Mathf.Min(p0.x, p1.x), x1 = Mathf.Max(p0.x, p1.x), y0 = Mathf.Min(p0.y, p1.y), y1 = Mathf.Max(p0.y, p1.y);
                int ix0 = Mathf.Max(0, Mathf.FloorToInt(x0)), ix1 = Mathf.Min(width - 1, Mathf.CeilToInt(x1) - 1);
                int iy0 = Mathf.Max(0, Mathf.FloorToInt(y0)), iy1 = Mathf.Min(height - 1, Mathf.CeilToInt(y1) - 1);
                for (int yy = iy0; yy <= iy1; yy++)
                {
                    float cy = Mathf.Min(yy + 1, y1) - Mathf.Max(yy, y0);
                    for (int xx = ix0; xx <= ix1; xx++)
                    {
                        float cx = Mathf.Min(xx + 1, x1) - Mathf.Max(xx, x0);
                        Blend(xx, yy, cx * cy, true);
                    }
                }
                return;
            }
            var keep = new List<List<Vector2>>(path); var keepSub = sub;
            beginPath(); rect(x, y, w, h); fill();
            path.Clear(); path.AddRange(keep); sub = keepSub;
        }
        public void strokeRect(float x, float y, float w, float h)
        {
            var keep = new List<List<Vector2>>(path); var keepSub = sub;
            beginPath(); rect(x, y, w, h); stroke();
            path.Clear(); path.AddRange(keep); sub = keepSub;
        }

        // Non-zero fill, 5 sample rows per pixel and exact horizontal coverage along each row
        public void fill()
        {
            float minY = float.MaxValue, maxY = float.MinValue, minX = float.MaxValue, maxX = float.MinValue;
            foreach (var s in path) foreach (var p in s) { minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y); minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); }
            if (minY > maxY) return;
            int iy0 = Mathf.Max(0, Mathf.FloorToInt(minY)), iy1 = Mathf.Min(height - 1, Mathf.CeilToInt(maxY));
            int ix0 = Mathf.Max(0, Mathf.FloorToInt(minX)), ix1 = Mathf.Min(width - 1, Mathf.CeilToInt(maxX));
            if (iy0 > iy1 || ix0 > ix1) return;
            const int SS = 5;
            var xs = new List<(float x, int dir)>();
            for (int yy = iy0; yy <= iy1; yy++)
            {
                for (int xx = ix0; xx <= ix1; xx++) cov[yy * width + xx] = 0;
                for (int k = 0; k < SS; k++)
                {
                    float sy = yy + (k + 0.5f) / SS;
                    xs.Clear();
                    foreach (var s in path)
                    {
                        int n = s.Count; if (n < 2) continue;
                        for (int i = 0; i < n; i++)
                        {
                            var a = s[i]; var b = s[(i + 1) % n];   // (each subpath closes itself for filling)
                            if ((a.y <= sy && b.y > sy) || (b.y <= sy && a.y > sy))
                                xs.Add((a.x + (sy - a.y) / (b.y - a.y) * (b.x - a.x), b.y > a.y ? 1 : -1));
                        }
                    }
                    if (xs.Count < 2) continue;
                    xs.Sort((p, q) => p.x.CompareTo(q.x));
                    int wind = 0;
                    for (int i = 0; i < xs.Count - 1; i++)
                    {
                        wind += xs[i].dir;
                        if (wind == 0) continue;
                        float xa = Mathf.Max(xs[i].x, ix0), xb = Mathf.Min(xs[i + 1].x, ix1 + 1);
                        if (xb <= xa) continue;
                        int ca = Mathf.FloorToInt(xa), cb = Mathf.Min(ix1, Mathf.FloorToInt(xb));
                        for (int cx = ca; cx <= cb; cx++)
                        {
                            float l = Mathf.Min(cx + 1, xb) - Mathf.Max(cx, xa);
                            if (l > 0) cov[yy * width + cx] += l / SS;
                        }
                    }
                }
                for (int xx = ix0; xx <= ix1; xx++) { float c = cov[yy * width + xx]; if (c > 0) Blend(xx, yy, Mathf.Min(1, c), true); }
            }
        }

        // Stroke: distance to each segment against half the line width (round joins and caps: at texture sizes the
        // difference from butt caps doesn't show)
        public void stroke()
        {
            float hw = Mathf.Max(0.5f, lineWidth * tf.Scale * 0.5f);
            var touched = new List<int>();
            foreach (var s in path)
            {
                for (int i = 0; i + 1 < s.Count; i++)
                {
                    Vector2 a = s[i], b = s[i + 1];
                    int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, b.x) - hw - 1)), x1 = Mathf.Min(width - 1, Mathf.CeilToInt(Mathf.Max(a.x, b.x) + hw + 1));
                    int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, b.y) - hw - 1)), y1 = Mathf.Min(height - 1, Mathf.CeilToInt(Mathf.Max(a.y, b.y) + hw + 1));
                    Vector2 ab = b - a; float l2 = Mathf.Max(1e-6f, ab.sqrMagnitude);
                    for (int yy = y0; yy <= y1; yy++)
                        for (int xx = x0; xx <= x1; xx++)
                        {
                            var p = new Vector2(xx + 0.5f, yy + 0.5f);
                            float t = Vector2.Dot(p - a, ab) / l2;
                            float d = Vector2.Distance(p, a + ab * Mathf.Clamp01(t));
                            float c = Mathf.Clamp01(hw - d + 0.5f);
                            if (c <= 0) continue;
                            int idx = yy * width + xx;
                            if (cov[idx] == 0) touched.Add(idx);
                            if (c > cov[idx]) cov[idx] = c;
                        }
                }
            }
            foreach (int idx in touched) { Blend(idx % width, idx / width, cov[idx], false); cov[idx] = 0; }
        }

        void Blend(int x, int y, float coverage, bool isFill)
        {
            var g = isFill ? _fillG : _strokeG;
            Color s;
            if (g != null) { var u = g.space.Inverse().Apply(x + 0.5f, y + 0.5f); s = g.At(u.x, u.y); }
            else s = isFill ? fillC : strokeC;
            float sa = s.a * coverage * globalAlpha;
            if (sa <= 0) return;
            int i = y * width + x;
            var d = px[i];
            if (globalCompositeOperation == "lighten")
            {
                var m = new Color(Mathf.Max(d.r, s.r), Mathf.Max(d.g, s.g), Mathf.Max(d.b, s.b));
                float oa = sa + d.a * (1 - sa);
                // (the backdrop's colour where it shows, the lighter of the two where they overlap)
                var c = (d.a > 0 ? m * sa * d.a + s * sa * (1 - d.a) + d * d.a * (1 - sa) : s * sa) / Mathf.Max(oa, 1e-6f);
                px[i] = new Color(c.r, c.g, c.b, oa);
            }
            else if (globalCompositeOperation == "lighter")
            {
                float oa = Mathf.Min(1, sa + d.a);
                var c = (s * sa + d * d.a) / Mathf.Max(oa, 1e-6f);
                px[i] = new Color(Mathf.Min(1, c.r), Mathf.Min(1, c.g), Mathf.Min(1, c.b), oa);
            }
            else
            {
                float oa = sa + d.a * (1 - sa);
                var c = (s * sa + d * d.a * (1 - sa)) / Mathf.Max(oa, 1e-6f);
                px[i] = new Color(c.r, c.g, c.b, oa);
            }
        }

        public Color Get(int x, int y) => px[y * width + x];
        public void Set(int x, int y, Color c) => px[y * width + x] = c;

        // Upload as a texture (sRGB colour by default; linear for data like normal maps)
        public Texture2D ToTexture(bool srgb = true, bool repeat = false, bool mips = true)
        {
            var t = new Texture2D(width, height, TextureFormat.RGBA32, mips, !srgb);
            var data = new Color32[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    data[(height - 1 - y) * width + x] = px[y * width + x];
            t.SetPixels32(data);
            t.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Bilinear; t.anisoLevel = repeat ? 8 : 1;
            t.Apply(mips, true);
            return t;
        }

        // ---- CSS colours: #rgb, #rrggbb, rgb(), rgba(), and a few names ----
        static readonly Dictionary<string, Color> cssCache = new Dictionary<string, Color>();
        public static Color Css(string s)
        {
            if (s == null) return Color.black;
            if (cssCache.TryGetValue(s, out var c)) return c;
            c = ParseCss(s.Trim());
            if (cssCache.Count < 4096) cssCache[s] = c;
            return c;
        }
        static Color ParseCss(string s)
        {
            if (s.StartsWith("#"))
            {
                string h = s.Substring(1);
                if (h.Length == 3 || h.Length == 4) { var e = ""; foreach (var ch in h) e += new string(ch, 2); h = e; }
                uint v = Convert.ToUInt32(h, 16);
                if (h.Length == 8) return new Color(((v >> 24) & 255) / 255f, ((v >> 16) & 255) / 255f, ((v >> 8) & 255) / 255f, (v & 255) / 255f);
                return new Color(((v >> 16) & 255) / 255f, ((v >> 8) & 255) / 255f, (v & 255) / 255f, 1);
            }
            if (s.StartsWith("rgb"))
            {
                int a = s.IndexOf('('), b = s.LastIndexOf(')');
                var parts = s.Substring(a + 1, b - a - 1).Split(',');
                float P(int i) => float.Parse(parts[i].Trim(), CultureInfo.InvariantCulture);
                return new Color(Mathf.Clamp(P(0), 0, 255) / 255f, Mathf.Clamp(P(1), 0, 255) / 255f, Mathf.Clamp(P(2), 0, 255) / 255f, parts.Length > 3 ? Mathf.Clamp01(P(3)) : 1);
            }
            switch (s)
            {
                case "white": return Color.white;
                case "black": return Color.black;
                case "transparent": return new Color(0, 0, 0, 0);
            }
            return Color.black;
        }
    }
}
