// three.js's geometry generators (box, rounded box, sphere, cylinder, cone, capsule, torus, lathe, polyhedra,
// circle, ring, plane), producing Unity meshes. Vertices are generated in three.js space with three.js's own
// formulas and then mirrored into Unity (z negated, triangle order reversed: three.js fronts are counter-clockwise,
// Unity's clockwise). Meshes are cached by their parameters and shared.
using System.Collections.Generic;
using UnityEngine;

namespace NovaStriker.Game.Three
{
    public sealed class GeoBuilder
    {
        public readonly List<Vector3> pos = new List<Vector3>();
        public readonly List<Vector3> nrm = new List<Vector3>();
        public readonly List<Vector2> uv = new List<Vector2>();
        public readonly List<int> idx = new List<int>();
        public int V(Vector3 p, Vector3 n, Vector2 t) { pos.Add(p); nrm.Add(n); uv.Add(t); return pos.Count - 1; }
        public void T(int a, int b, int c) { idx.Add(a); idx.Add(b); idx.Add(c); }
        public Mesh ToMesh(string name)
        {
            var m = new Mesh { name = name };
            if (pos.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            var p = new Vector3[pos.Count]; var n = new Vector3[pos.Count];
            for (int i = 0; i < pos.Count; i++) { var a = pos[i]; p[i] = new Vector3(a.x, a.y, -a.z); var b = nrm[i]; n[i] = new Vector3(b.x, b.y, -b.z); }
            var t = new int[idx.Count];
            for (int i = 0; i < idx.Count; i += 3) { t[i] = idx[i]; t[i + 1] = idx[i + 2]; t[i + 2] = idx[i + 1]; }
            m.vertices = p; m.normals = n; m.SetUVs(0, uv); m.triangles = t;
            m.RecalculateBounds();
            return m;
        }
    }

    public static class Geo
    {
        static readonly Dictionary<string, Mesh> cache = new Dictionary<string, Mesh>();
        static Mesh Cached(string key, System.Func<Mesh> make)
        {
            if (!cache.TryGetValue(key, out var m)) { m = make(); cache[key] = m; }
            return m;
        }
        static string K(params object[] a) => string.Join(",", a);
        const float PI = Mathf.PI;
        static float Sign0(float v) => v > 0 ? 1 : v < 0 ? -1 : 0;

        // ---- Box ----
        public static Mesh Box(float w, float h, float d) => Cached(K("box", w, h, d), () =>
        {
            var b = new GeoBuilder();
            void Face(Vector3 n, Vector3 u, Vector3 v, float hu, float hv, float hn)
            {
                Vector3 c = n * hn;
                int i0 = b.V(c - u * hu - v * hv, n, new Vector2(0, 0)), i1 = b.V(c + u * hu - v * hv, n, new Vector2(1, 0));
                int i2 = b.V(c + u * hu + v * hv, n, new Vector2(1, 1)), i3 = b.V(c - u * hu + v * hv, n, new Vector2(0, 1));
                b.T(i0, i1, i2); b.T(i0, i2, i3);
            }
            float x = w / 2, y = h / 2, z = d / 2;
            Face(Vector3.right, new Vector3(0, 0, -1), Vector3.up, z, y, x);   // +x: u=-z, v=+y (u x v = +x)
            Face(Vector3.left, Vector3.forward, Vector3.up, z, y, x);    // -x: u=+z, v=+y
            Face(Vector3.up, Vector3.right, Vector3.back, x, z, y);      // +y: u=+x, v=-z
            Face(Vector3.down, Vector3.right, Vector3.forward, x, z, y); // -y: u=+x, v=+z
            Face(Vector3.forward, Vector3.right, Vector3.up, x, y, z);   // +z: u=+x, v=+y
            Face(Vector3.back, Vector3.left, Vector3.up, x, y, z);       // -z: u=-x, v=+y
            return b.ToMesh("box");
        });

        // RoundedBoxGeometry (three/addons): a subdivided box whose vertices are pulled onto rounded edges
        public static Mesh RBox(float w, float h, float d, float r = 0.05f, int segments = 3)
        {
            r = Mathf.Min(r, w / 2 - 1e-3f, h / 2 - 1e-3f, d / 2 - 1e-3f);
            return RoundedBox(w, h, d, segments, r);
        }
        public static Mesh RoundedBox(float w, float h, float d, int segments, float radius) => Cached(K("rbox", w, h, d, segments, radius), () =>
        {
            int segs = segments * 2 + 1;
            radius = Mathf.Min(w / 2, h / 2, d / 2, radius);
            var b = new GeoBuilder();
            var box = new Vector3(w / 2 - radius, h / 2 - radius, d / 2 - radius);
            float half = 0.5f / segs;
            void Plane(Vector3 n, Vector3 u, Vector3 v)
            {
                // a unit face subdivided segs x segs, at distance 0.5 along n
                int start = b.pos.Count;
                for (int iy = 0; iy <= segs; iy++)
                    for (int ix = 0; ix <= segs; ix++)
                    {
                        Vector3 p = n * 0.5f + u * (ix / (float)segs - 0.5f) + v * (iy / (float)segs - 0.5f);
                        Vector3 nn = p;
                        nn.x -= Sign0(nn.x) * half; nn.y -= Sign0(nn.y) * half; nn.z -= Sign0(nn.z) * half;
                        nn.Normalize();
                        var q = new Vector3(box.x * Sign0(p.x) + nn.x * radius, box.y * Sign0(p.y) + nn.y * radius, box.z * Sign0(p.z) + nn.z * radius);
                        b.V(q, nn, new Vector2(ix / (float)segs, iy / (float)segs));
                    }
                for (int iy = 0; iy < segs; iy++)
                    for (int ix = 0; ix < segs; ix++)
                    {
                        int a = start + iy * (segs + 1) + ix, c = a + 1, e = a + segs + 1, f = e + 1;
                        b.T(a, c, f); b.T(a, f, e);
                    }
            }
            Plane(Vector3.right, new Vector3(0, 0, -1), Vector3.up);
            Plane(Vector3.left, new Vector3(0, 0, 1), Vector3.up);
            Plane(Vector3.up, Vector3.right, new Vector3(0, 0, -1));
            Plane(Vector3.down, Vector3.right, new Vector3(0, 0, 1));
            Plane(new Vector3(0, 0, 1), Vector3.right, Vector3.up);
            Plane(new Vector3(0, 0, -1), Vector3.left, Vector3.up);
            return b.ToMesh("rbox");
        });

        // ---- Sphere ----
        public static Mesh Sphere(float radius, int wSeg = 32, int hSeg = 16, float phiStart = 0, float phiLength = PI * 2, float thetaStart = 0, float thetaLength = PI) =>
            Cached(K("sph", radius, wSeg, hSeg, phiStart, phiLength, thetaStart, thetaLength), () =>
            {
                wSeg = Mathf.Max(3, wSeg); hSeg = Mathf.Max(2, hSeg);
                float thetaEnd = Mathf.Min(thetaStart + thetaLength, PI);
                var b = new GeoBuilder(); var grid = new int[hSeg + 1, wSeg + 1];
                for (int iy = 0; iy <= hSeg; iy++)
                {
                    float v = iy / (float)hSeg, uOff = 0;
                    if (iy == 0 && thetaStart == 0) uOff = 0.5f / wSeg; else if (iy == hSeg && thetaEnd == PI) uOff = -0.5f / wSeg;
                    for (int ix = 0; ix <= wSeg; ix++)
                    {
                        float u = ix / (float)wSeg;
                        var p = new Vector3(-radius * Mathf.Cos(phiStart + u * phiLength) * Mathf.Sin(thetaStart + v * thetaLength),
                            radius * Mathf.Cos(thetaStart + v * thetaLength),
                            radius * Mathf.Sin(phiStart + u * phiLength) * Mathf.Sin(thetaStart + v * thetaLength));
                        grid[iy, ix] = b.V(p, p.normalized, new Vector2(u + uOff, 1 - v));
                    }
                }
                for (int iy = 0; iy < hSeg; iy++)
                    for (int ix = 0; ix < wSeg; ix++)
                    {
                        int a = grid[iy, ix + 1], bb = grid[iy, ix], c = grid[iy + 1, ix], d = grid[iy + 1, ix + 1];
                        if (iy != 0 || thetaStart > 0) b.T(a, bb, d);
                        if (iy != hSeg - 1 || thetaEnd < PI) b.T(bb, c, d);
                    }
                return b.ToMesh("sphere");
            });

        // ---- Cylinder / cone ----
        public static Mesh Cylinder(float rt, float rb, float h, int radial = 32, int heightSeg = 1, bool openEnded = false, float thetaStart = 0, float thetaLength = PI * 2) =>
            Cached(K("cyl", rt, rb, h, radial, heightSeg, openEnded, thetaStart, thetaLength), () =>
            {
                var b = new GeoBuilder(); float halfH = h / 2;
                var rows = new int[heightSeg + 1, radial + 1];
                float slope = (rb - rt) / h;
                for (int y = 0; y <= heightSeg; y++)
                {
                    float v = y / (float)heightSeg, radius = v * (rb - rt) + rt;
                    for (int x = 0; x <= radial; x++)
                    {
                        float u = x / (float)radial, theta = u * thetaLength + thetaStart, s = Mathf.Sin(theta), c = Mathf.Cos(theta);
                        rows[y, x] = b.V(new Vector3(radius * s, -v * h + halfH, radius * c), new Vector3(s, slope, c).normalized, new Vector2(u, 1 - v));
                    }
                }
                for (int x = 0; x < radial; x++)
                    for (int y = 0; y < heightSeg; y++)
                    {
                        int a = rows[y, x], bb = rows[y + 1, x], c = rows[y + 1, x + 1], d = rows[y, x + 1];
                        b.T(a, bb, d); b.T(bb, c, d);
                    }
                void Cap(bool top)
                {
                    float radius = top ? rt : rb, sign = top ? 1 : -1;
                    int centerStart = b.pos.Count;
                    for (int x = 1; x <= radial; x++) b.V(new Vector3(0, halfH * sign, 0), new Vector3(0, sign, 0), new Vector2(0.5f, 0.5f));
                    int centerEnd = b.pos.Count;
                    for (int x = 0; x <= radial; x++)
                    {
                        float u = x / (float)radial, theta = u * thetaLength + thetaStart, c = Mathf.Cos(theta), s = Mathf.Sin(theta);
                        b.V(new Vector3(radius * s, halfH * sign, radius * c), new Vector3(0, sign, 0), new Vector2(c * 0.5f + 0.5f, s * 0.5f * sign + 0.5f));
                    }
                    for (int x = 0; x < radial; x++)
                    {
                        int c = centerStart + x, i = centerEnd + x;
                        if (top) b.T(i, i + 1, c); else b.T(i + 1, i, c);
                    }
                }
                if (!openEnded) { if (rt > 0) Cap(true); if (rb > 0) Cap(false); }
                return b.ToMesh("cylinder");
            });
        public static Mesh Cone(float radius, float height, int radial = 32, int heightSeg = 1, bool openEnded = false) => Cylinder(0, radius, height, radial, heightSeg, openEnded);

        // ---- Lathe and capsule ----
        public static Mesh Lathe(Vector2[] points, int segments = 12, float phiStart = 0, float phiLength = PI * 2, string key = null) =>
            Cached(key ?? K("lathe", System.Guid.NewGuid()), () => LatheBuild(points, segments, phiStart, phiLength).ToMesh("lathe"));
        static GeoBuilder LatheBuild(Vector2[] points, int segments, float phiStart, float phiLength)
        {
            var b = new GeoBuilder(); int n = points.Length;
            var initN = new Vector3[n]; Vector3 prev = Vector3.zero;
            for (int j = 0; j < n; j++)
            {
                if (j == 0)
                {
                    float dx = points[1].x - points[0].x, dy = points[1].y - points[0].y;
                    var nn = new Vector3(dy, -dx, 0); prev = nn; initN[j] = nn.normalized;
                }
                else if (j == n - 1) initN[j] = prev;
                else
                {
                    float dx = points[j + 1].x - points[j].x, dy = points[j + 1].y - points[j].y;
                    var cur = new Vector3(dy, -dx, 0); initN[j] = (cur + prev).normalized; prev = cur;
                }
            }
            float inv = 1f / segments;
            for (int i = 0; i <= segments; i++)
            {
                float phi = phiStart + i * inv * phiLength, s = Mathf.Sin(phi), c = Mathf.Cos(phi);
                for (int j = 0; j < n; j++)
                    b.V(new Vector3(points[j].x * s, points[j].y, points[j].x * c), new Vector3(initN[j].x * s, initN[j].y, initN[j].x * c), new Vector2(i / (float)segments, j / (float)(n - 1)));
            }
            for (int i = 0; i < segments; i++)
                for (int j = 0; j < n - 1; j++)
                {
                    int bse = j + i * n, a = bse, bb = bse + n, c = bse + n + 1, d = bse + 1;
                    b.T(a, bb, d); b.T(c, d, bb);
                }
            return b;
        }
        public static Mesh Capsule(float radius, float length, int capSeg = 4, int radialSeg = 8) => Cached(K("cap", radius, length, capSeg, radialSeg), () =>
        {
            var pts = new List<Vector2>();
            void Add(Vector2 p) { if (pts.Count == 0 || pts[pts.Count - 1] != p) pts.Add(p); }
            int res = capSeg * 2;
            for (int i = 0; i <= res; i++) { float a = PI * 1.5f + (PI * 0.5f) * i / res; Add(new Vector2(radius * Mathf.Cos(a), -length / 2 + radius * Mathf.Sin(a))); }
            Add(new Vector2(radius, length / 2));
            for (int i = 0; i <= res; i++) { float a = (PI * 0.5f) * i / res; Add(new Vector2(radius * Mathf.Cos(a), length / 2 + radius * Mathf.Sin(a))); }
            return LatheBuild(pts.ToArray(), radialSeg, 0, PI * 2).ToMesh("capsule");
        });

        // ---- Torus ----
        public static Mesh Torus(float radius, float tube, int radialSeg = 12, int tubularSeg = 48, float arc = PI * 2) => Cached(K("torus", radius, tube, radialSeg, tubularSeg, arc), () =>
        {
            var b = new GeoBuilder();
            for (int j = 0; j <= radialSeg; j++)
                for (int i = 0; i <= tubularSeg; i++)
                {
                    float u = i / (float)tubularSeg * arc, v = j / (float)radialSeg * PI * 2;
                    var p = new Vector3((radius + tube * Mathf.Cos(v)) * Mathf.Cos(u), (radius + tube * Mathf.Cos(v)) * Mathf.Sin(u), tube * Mathf.Sin(v));
                    var c = new Vector3(radius * Mathf.Cos(u), radius * Mathf.Sin(u), 0);
                    b.V(p, (p - c).normalized, new Vector2(i / (float)tubularSeg, j / (float)radialSeg));
                }
            for (int j = 1; j <= radialSeg; j++)
                for (int i = 1; i <= tubularSeg; i++)
                {
                    int a = (tubularSeg + 1) * j + i - 1, bb = (tubularSeg + 1) * (j - 1) + i - 1, c = (tubularSeg + 1) * (j - 1) + i, d = (tubularSeg + 1) * j + i;
                    b.T(a, bb, d); b.T(bb, c, d);
                }
            return b.ToMesh("torus");
        });

        // ---- Polyhedra (flat-shaded at detail 0, smooth above) ----
        static readonly float T5 = (1 + Mathf.Sqrt(5)) / 2;
        static readonly float[] ICO_V = { -1, T5, 0, 1, T5, 0, -1, -T5, 0, 1, -T5, 0, 0, -1, T5, 0, 1, T5, 0, -1, -T5, 0, 1, -T5, T5, 0, -1, T5, 0, 1, -T5, 0, -1, -T5, 0, 1 };
        static readonly int[] ICO_I = { 0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8, 3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1 };
        static readonly float[] OCT_V = { 1, 0, 0, -1, 0, 0, 0, 1, 0, 0, -1, 0, 0, 0, 1, 0, 0, -1 };
        static readonly int[] OCT_I = { 0, 2, 4, 0, 4, 3, 0, 3, 5, 0, 5, 2, 1, 2, 5, 1, 5, 3, 1, 3, 4, 1, 4, 2 };
        static readonly float[] TET_V = { 1, 1, 1, -1, -1, 1, -1, 1, -1, 1, -1, -1 };
        static readonly int[] TET_I = { 2, 1, 0, 0, 3, 2, 1, 3, 0, 2, 3, 1 };
        public static Mesh Icosahedron(float radius, int detail = 0) => Cached(K("ico", radius, detail), () => Poly(ICO_V, ICO_I, radius, detail, "ico"));
        public static Mesh Octahedron(float radius, int detail = 0) => Cached(K("oct", radius, detail), () => Poly(OCT_V, OCT_I, radius, detail, "oct"));
        public static Mesh Tetrahedron(float radius, int detail = 0) => Cached(K("tet", radius, detail), () => Poly(TET_V, TET_I, radius, detail, "tet"));
        static Mesh Poly(float[] verts, int[] ind, float radius, int detail, string name)
        {
            var tris = new List<Vector3>();
            Vector3 VV(int i) => new Vector3(verts[i * 3], verts[i * 3 + 1], verts[i * 3 + 2]);
            for (int f = 0; f < ind.Length; f += 3)
            {
                Vector3 a = VV(ind[f]), bb = VV(ind[f + 1]), c = VV(ind[f + 2]);
                int cols = detail + 1;
                var v = new List<Vector3>[cols + 1];
                for (int i = 0; i <= cols; i++)
                {
                    v[i] = new List<Vector3>();
                    Vector3 aj = Vector3.Lerp(a, c, i / (float)cols), bj = Vector3.Lerp(bb, c, i / (float)cols);
                    int rows = cols - i;
                    for (int j = 0; j <= rows; j++) v[i].Add(j == 0 && i == cols ? aj : Vector3.Lerp(aj, bj, j / (float)rows));
                }
                for (int i = 0; i < cols; i++)
                    for (int j = 0; j < 2 * (cols - i) - 1; j++)
                    {
                        int k = j / 2;
                        if (j % 2 == 0) { tris.Add(v[i][k + 1]); tris.Add(v[i + 1][k]); tris.Add(v[i][k]); }
                        else { tris.Add(v[i][k + 1]); tris.Add(v[i + 1][k + 1]); tris.Add(v[i + 1][k]); }
                    }
            }
            var b = new GeoBuilder();
            for (int i = 0; i < tris.Count; i += 3)
            {
                Vector3 p0 = tris[i].normalized * radius, p1 = tris[i + 1].normalized * radius, p2 = tris[i + 2].normalized * radius;
                if (detail == 0)
                {
                    var n = Vector3.Cross(p1 - p0, p2 - p0).normalized;   // three.js space is right-handed: (b-a) x (c-a) points out of a CCW face
                    b.T(b.V(p0, n, Vector2.zero), b.V(p1, n, Vector2.zero), b.V(p2, n, Vector2.zero));
                }
                else b.T(b.V(p0, p0.normalized, Vector2.zero), b.V(p1, p1.normalized, Vector2.zero), b.V(p2, p2.normalized, Vector2.zero));
            }
            return b.ToMesh(name);
        }

        // ---- Flat shapes (facing +z) ----
        public static Mesh Circle(float radius, int segments = 32, float thetaStart = 0, float thetaLength = PI * 2) => Cached(K("circ", radius, segments, thetaStart, thetaLength), () =>
        {
            var b = new GeoBuilder(); var n = new Vector3(0, 0, 1);
            b.V(Vector3.zero, n, new Vector2(0.5f, 0.5f));
            for (int s = 0; s <= segments; s++)
            {
                float a = thetaStart + s / (float)segments * thetaLength;
                b.V(new Vector3(radius * Mathf.Cos(a), radius * Mathf.Sin(a), 0), n, new Vector2(Mathf.Cos(a) * 0.5f + 0.5f, Mathf.Sin(a) * 0.5f + 0.5f));
            }
            for (int i = 1; i <= segments; i++) b.T(i, i + 1, 0);
            return b.ToMesh("circle");
        });
        public static Mesh Ring(float inner, float outer, int thetaSeg = 32, int phiSeg = 1, float thetaStart = 0, float thetaLength = PI * 2) => Cached(K("ring", inner, outer, thetaSeg, phiSeg, thetaStart, thetaLength), () =>
        {
            var b = new GeoBuilder(); var n = new Vector3(0, 0, 1);
            float radius = inner, step = (outer - inner) / phiSeg;
            for (int j = 0; j <= phiSeg; j++)
            {
                for (int i = 0; i <= thetaSeg; i++)
                {
                    float seg = thetaStart + i / (float)thetaSeg * thetaLength;
                    var p = new Vector3(radius * Mathf.Cos(seg), radius * Mathf.Sin(seg), 0);
                    b.V(p, n, new Vector2((p.x / outer + 1) / 2, (p.y / outer + 1) / 2));
                }
                radius += step;
            }
            for (int j = 0; j < phiSeg; j++)
            {
                int level = j * (thetaSeg + 1);
                for (int i = 0; i < thetaSeg; i++)
                {
                    int s = i + level, a = s, bb = s + thetaSeg + 1, c = s + thetaSeg + 2, d = s + 1;
                    b.T(a, bb, d); b.T(bb, c, d);
                }
            }
            return b.ToMesh("ring");
        });
        public static Mesh Plane(float w, float h, int ws = 1, int hs = 1) => Cached(K("plane", w, h, ws, hs), () =>
        {
            var b = new GeoBuilder(); var n = new Vector3(0, 0, 1);
            int gx1 = ws + 1, gy1 = hs + 1; float sw = w / ws, sh = h / hs;
            for (int iy = 0; iy < gy1; iy++)
                for (int ix = 0; ix < gx1; ix++)
                    b.V(new Vector3(ix * sw - w / 2, -(iy * sh - h / 2), 0), n, new Vector2(ix / (float)ws, 1 - iy / (float)hs));
            for (int iy = 0; iy < hs; iy++)
                for (int ix = 0; ix < ws; ix++)
                {
                    int a = ix + gx1 * iy, bb = ix + gx1 * (iy + 1), c = ix + 1 + gx1 * (iy + 1), d = ix + 1 + gx1 * iy;
                    b.T(a, bb, d); b.T(bb, c, d);
                }
            return b.ToMesh("plane");
        });
    }
}
