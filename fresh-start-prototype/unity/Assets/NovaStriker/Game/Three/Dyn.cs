// Geometry rebuilt every frame (particles, ribbons, beams, the scarf, ghosts), and orientation helpers that work in
// the prototype's space (a rotation there is mirrored into Unity as (-x, -y, z, w)).
using System.Collections.Generic;
using UnityEngine;

namespace NovaStriker.Game.Three
{
    public static class ThQ
    {
        // three.js space quaternion -> Unity
        public static Quaternion ToUnity(Quaternion q) => new Quaternion(-q.x, -q.y, q.z, q.w);
        // quaternion.setFromUnitVectors(from, to), both in three.js space
        public static Quaternion FromUnitVectors(Vector3 from, Vector3 to) => ToUnity(Quaternion.FromToRotation(from, to));
        // makeBasis(X, Y, Z) then setFromRotationMatrix (three.js space)
        public static Quaternion FromBasis(Vector3 X, Vector3 Y, Vector3 Z)
        {
            var m = new Matrix4x4();
            m.SetColumn(0, new Vector4(X.x, X.y, X.z, 0)); m.SetColumn(1, new Vector4(Y.x, Y.y, Y.z, 0)); m.SetColumn(2, new Vector4(Z.x, Z.y, Z.z, 0)); m.SetColumn(3, new Vector4(0, 0, 0, 1));
            return ToUnity(m.rotation);
        }
    }

    public static class TObjExt
    {
        public static void SetQuaternion(this TObj o, Quaternion unityLocal) => o.tr.localRotation = unityLocal;
        // Face the camera (quaternion.copy(camera.quaternion)), for objects at the scene root
        public static void FaceCamera(this TObj o, Camera cam) => o.tr.rotation = cam.transform.rotation;
        // rotateZ on top of the current orientation (three.js space, local axis)
        public static void RotateZ(this TObj o, float a) => o.tr.localRotation = o.tr.localRotation * Th.Quat(0, 0, a);
    }

    // A mesh whose vertices are written each frame, in Unity world space (it sits at the scene root)
    public sealed class DynMesh
    {
        public readonly TMesh obj;
        public readonly Mesh mesh;
        public Vector3[] pos; public Color[] col; public Vector2[] uv;
        public DynMesh(int verts, int[] tris, TMat mat, bool colors = true, bool uvs = false, string name = "dyn")
        {
            mesh = new Mesh { name = name };
            mesh.MarkDynamic();
            if (verts > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            pos = new Vector3[verts]; mesh.vertices = pos;
            if (colors) { col = new Color[verts]; mesh.colors = col; }
            if (uvs) { uv = new Vector2[verts]; mesh.uv = uv; }
            mesh.triangles = tris;
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100000);   // (never culled)
            obj = new TMesh(mesh, mat) { };
            obj.go.name = name; obj.cast = false; obj.receive = false; obj.noOutline = true;
        }
        // positions given in three.js space
        public void Set(int i, Vector3 three) => pos[i] = new Vector3(three.x, three.y, -three.z);
        public void Upload(bool normals = false)
        {
            mesh.vertices = pos;
            if (col != null) mesh.colors = col;
            if (uv != null) mesh.uv = uv;
            if (normals) mesh.RecalculateNormals();
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100000);
        }
        public bool visible { get => obj.visible; set => obj.visible = value; }
        public static int[] StripTris(int n)
        {
            var t = new List<int>();
            for (int i = 0; i < n - 1; i++) { int a = i * 2; t.Add(a); t.Add(a + 1); t.Add(a + 2); t.Add(a + 1); t.Add(a + 3); t.Add(a + 2); }
            return t.ToArray();
        }
    }

    // A camera-facing strip through a polyline, with a width and an RGBA colour per point (beamfx.js Strip: the
    // beams, chain lightning, tethers and arcs)
    public sealed class Strip
    {
        public const int MAXP = 72;
        public readonly DynMesh dm;
        public Strip(TObj scene, int order)
        {
            var mat = new TMat(TMat.Kind.Basic) { vertexColors = true, transparent = true, depthWrite = false, side = Side.Double, fog = false };
            dm = new DynMesh(MAXP * 2, DynMesh.StripTris(MAXP), mat, true, false, "strip");
            dm.obj.RenderOrder = order; scene.add(dm.obj); dm.visible = false;
        }
        public TMesh mesh => dm.obj;
        // pts: points in three.js space; w(i), rgba(i) per point
        public void Build(IList<Vector3> pts, Vector3 cam, System.Func<int, float> w, System.Func<int, Vector4> rgba)
        {
            int n = Mathf.Min(MAXP, pts.Count);
            if (n < 1) { dm.visible = false; return; }
            for (int i = 0; i < MAXP; i++)
            {
                int j = Mathf.Min(i, n - 1); var p = pts[j];
                var d = pts[Mathf.Min(n - 1, j + 1)] - pts[Mathf.Max(0, j - 1)];
                if (d.sqrMagnitude < 1e-8f) d = new Vector3(1, 0, 0); d.Normalize();
                var s = Vector3.Cross(d, (cam - p).normalized).normalized * (i < n ? w(j) : 0);
                dm.Set(i * 2, p + s); dm.Set(i * 2 + 1, p - s);
                var c = rgba(j); var cc = new Color(c.x, c.y, c.z, i < n ? c.w : 0);
                dm.col[i * 2] = cc; dm.col[i * 2 + 1] = cc;
            }
            dm.Upload(); dm.visible = true;
        }
    }
}
