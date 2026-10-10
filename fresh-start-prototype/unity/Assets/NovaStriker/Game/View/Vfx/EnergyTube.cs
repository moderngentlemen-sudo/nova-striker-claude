// Round, bounded world-space energy geometry shared by the existing beam/charge/lightning owners.
// Inputs are three-space. This is the one conversion into the raw Unity mesh; no simulation types/RNG.
using System.Collections.Generic;
using NovaStriker.Game.Three;
using UnityEngine;
using UnityEngine.Rendering;

namespace NovaStriker.Game
{
    public sealed class EnergyTube
    {
        public const int MaxPoints = 72, Sides = 8;
        public const int MaxActive = 66; // 4 beam owners, 4 charge cages, 6 discharge paths, 12 shocked bodies
        public static int Active { get; private set; }
        readonly GameObject go; readonly Mesh mesh; readonly Material material; readonly MeshRenderer renderer;
        readonly Vector3[] vertices = new Vector3[MaxPoints * Sides], normals = new Vector3[MaxPoints * Sides];
        readonly Vector2[] uv = new Vector2[MaxPoints * Sides];
        bool visible, disposed;
        public bool Visible { get => visible; set { if (disposed || value == visible) return; visible = value; Active += value ? 1 : -1; renderer.enabled = value; } }
        public int Points { get; private set; }

        public EnergyTube(Transform parent, string name)
        {
            go = new GameObject(name); go.transform.SetParent(parent, false);
            mesh = new Mesh { name = name }; mesh.MarkDynamic();
            var triangles = new int[(MaxPoints - 1) * Sides * 6]; int k = 0;
            for (int i = 0; i < MaxPoints - 1; i++) for (int j = 0; j < Sides; j++) {
                int a = i * Sides + j, b = i * Sides + (j + 1) % Sides;
                triangles[k++] = a; triangles[k++] = a + Sides; triangles[k++] = b;
                triangles[k++] = b; triangles[k++] = a + Sides; triangles[k++] = b + Sides;
            }
            mesh.vertices = vertices; mesh.triangles = triangles;
            var white = new Color[vertices.Length]; for (int i = 0; i < white.Length; i++) white[i] = Color.white; mesh.colors = white;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            material = new Material(Templates.Energy) { name = name + " material" };
            renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false; renderer.enabled = false;
        }

        public static void Basis(Vector3 tangent, out Vector3 side, out Vector3 up)
        {
            tangent = tangent.sqrMagnitude < 1e-8f ? Vector3.right : tangent.normalized;
            side = Vector3.Cross(tangent, Mathf.Abs(tangent.y) > 0.94f ? Vector3.forward : Vector3.up).normalized;
            up = Vector3.Cross(side, tangent).normalized;
        }

        // coilRadius makes a helical filament around the same collision path. Endpoints remain exact.
        public void Build(IList<Vector3> path, float radius, Color tint, float opacity, float time, float coilRadius = 0, float phase = 0)
        {
            int n = Mathf.Min(MaxPoints, path.Count); Points = n;
            if (n < 2 || radius <= 0 || opacity <= 0) { Visible = false; return; }
            float distance = 0;
            for (int i = 0; i < MaxPoints; i++) {
                int p = Mathf.Min(i, n - 1); var at = path[p];
                var tangent = path[Mathf.Min(n - 1, p + 1)] - path[Mathf.Max(0, p - 1)];
                Basis(tangent, out var side, out var up);
                if (i > 0 && i < n) distance += Vector3.Distance(path[i], path[i - 1]);
                float envelope = Mathf.Sin(Mathf.PI * p / (n - 1));
                float twist = distance * 2.4f - time * 13 + phase;
                at += coilRadius * envelope * (side * Mathf.Cos(twist) + up * Mathf.Sin(twist));
                float r = i < n ? radius * (0.65f + 0.35f * envelope) : 0;
                for (int j = 0; j < Sides; j++) {
                    float a = j * Mathf.PI * 2 / Sides; var normal = side * Mathf.Cos(a) + up * Mathf.Sin(a);
                    int v = i * Sides + j;
                    vertices[v] = Th.P(at + normal * r); normals[v] = Th.P(normal); uv[v] = new Vector2(distance, j / (float)Sides);
                }
            }
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uv); mesh.RecalculateBounds();
            material.SetColor("_Tint", tint); material.SetFloat("_Opacity", opacity);
            material.SetFloat("_EnergyTime", time); Visible = true;
        }

        public void Dispose() { if (disposed) return; Visible = false; disposed = true; Object.Destroy(mesh); Object.Destroy(material); Object.Destroy(go); }
    }
}
