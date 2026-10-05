// A thin three.js-style scene graph over Unity GameObjects, so the prototype's procedural rigs, animation and
// effects port almost line for line. Coordinates are the prototype's (right-handed, y up, +z toward the camera
// side); they are mirrored into Unity's left-handed space here (z is negated, and Euler rotations about x and y
// change sign), so what the camera sees is the same picture. Euler angles use three.js's default XYZ order.
using System.Collections.Generic;
using UnityEngine;

namespace NovaStriker.Game.Three
{
    public static class Th
    {
        // three.js Euler (XYZ order) -> three.js quaternion -> mirrored into Unity
        public static Quaternion Quat(float x, float y, float z)
        {
            float c1 = Mathf.Cos(x / 2), c2 = Mathf.Cos(y / 2), c3 = Mathf.Cos(z / 2);
            float s1 = Mathf.Sin(x / 2), s2 = Mathf.Sin(y / 2), s3 = Mathf.Sin(z / 2);
            float qx = s1 * c2 * c3 + c1 * s2 * s3;
            float qy = c1 * s2 * c3 - s1 * c2 * s3;
            float qz = c1 * c2 * s3 + s1 * s2 * c3;
            float qw = c1 * c2 * c3 - s1 * s2 * s3;
            return new Quaternion(-qx, -qy, qz, qw);
        }
        // A point in the prototype's world -> Unity
        public static Vector3 P(double x, double y, double z) => new Vector3((float)x, (float)y, (float)-z);
        public static Vector3 P(Vector3 three) => new Vector3(three.x, three.y, -three.z);
        public static Color Hex(uint hex) => new Color(((hex >> 16) & 255) / 255f, ((hex >> 8) & 255) / 255f, (hex & 255) / 255f, 1);
        public static Color Hex(string css)
        {
            if (css != null && css.StartsWith("#"))
            {
                string h = css.Substring(1);
                if (h.Length == 3) h = "" + h[0] + h[0] + h[1] + h[1] + h[2] + h[2];
                return Hex(System.Convert.ToUInt32(h, 16));
            }
            return Color.white;
        }
    }

    // Position / scale in three.js space, written through to the transform
    public sealed class TVec
    {
        readonly TObj o; readonly bool isScale;
        float _x, _y, _z;
        internal TVec(TObj o, bool isScale, float d) { this.o = o; this.isScale = isScale; _x = _y = _z = d; }
        public float x { get => _x; set { _x = value; Push(); } }
        public float y { get => _y; set { _y = value; Push(); } }
        public float z { get => _z; set { _z = value; Push(); } }
        public TVec set(float x, float y, float z) { _x = x; _y = y; _z = z; Push(); return this; }
        public TVec setScalar(float s) => set(s, s, s);
        public TVec copy(Vector3 v) => set(v.x, v.y, v.z);
        public Vector3 v => new Vector3(_x, _y, _z);
        void Push()
        {
            if (isScale) o.tr.localScale = new Vector3(_x, _y, _z);
            else o.tr.localPosition = new Vector3(_x, _y, -_z);
        }
    }
    // Euler rotation (XYZ) in three.js space
    public sealed class TRot
    {
        readonly TObj o;
        float _x, _y, _z;
        internal TRot(TObj o) { this.o = o; }
        public float x { get => _x; set { _x = value; Push(); } }
        public float y { get => _y; set { _y = value; Push(); } }
        public float z { get => _z; set { _z = value; Push(); } }
        public TRot set(float x, float y, float z) { _x = x; _y = y; _z = z; Push(); return this; }
        void Push() { o.tr.localRotation = Th.Quat(_x, _y, _z); }
    }

    public class TObj
    {
        public readonly GameObject go;
        public readonly Transform tr;
        public readonly TVec position, scale;
        public readonly TRot rotation;
        public TObj parent;
        public readonly List<TObj> children = new List<TObj>();
        public object userData;
        public bool outline, noOutline, castShadow = true;
        int renderOrder;

        public TObj(string name = "o")
        {
            go = new GameObject(name);
            tr = go.transform;
            position = new TVec(this, false, 0); scale = new TVec(this, true, 1); rotation = new TRot(this);
        }
        public bool visible
        {
            get => go.activeSelf;
            set { if (go.activeSelf != value) go.SetActive(value); }
        }
        public T add<T>(T child) where T : TObj
        {
            if (child.parent != null) child.parent.children.Remove(child);
            child.parent = this; children.Add(child);
            child.tr.SetParent(tr, false);
            return child;
        }
        public void removeFromParent()
        {
            if (parent != null) { parent.children.Remove(this); parent = null; }
            tr.SetParent(null, false);
        }
        public void traverse(System.Action<TObj> fn)
        {
            fn(this);
            for (int i = 0; i < children.Count; i++) children[i].traverse(fn);
        }
        public void destroy() { removeFromParent(); Object.Destroy(go); }
        // World position in three.js space
        public Vector3 worldPos { get { var p = tr.position; return new Vector3(p.x, p.y, -p.z); } }
        public virtual int RenderOrder { get => renderOrder; set => renderOrder = value; }
    }

    public static class Group
    {
        public static TObj Make(float x = 0, float y = 0, float z = 0, string name = "g")
        {
            var g = new TObj(name); g.position.set(x, y, z); return g;
        }
    }

    // A mesh: geometry and one material (plus, for characters, a rim pass and an outline shell)
    public sealed class TMesh : TObj
    {
        public readonly MeshFilter mf;
        public readonly MeshRenderer mr;
        TMat _mat;
        readonly List<Material> extra = new List<Material>();
        public TMesh(Mesh geo, TMat mat, float x = 0, float y = 0, float z = 0) : base("m")
        {
            mf = go.AddComponent<MeshFilter>(); mr = go.AddComponent<MeshRenderer>();
            mf.sharedMesh = geo;
            position.set(x, y, z);
            material = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = true;
        }
        public Mesh geometry => mf.sharedMesh;
        public TMat material
        {
            get => _mat;
            set { _mat = value; Apply(); }
        }
        // Extra passes drawn over the whole mesh (the rim light; outlines use their own shell)
        public void AddPass(Material m) { extra.Add(m); Apply(); }
        void Apply()
        {
            if (_mat == null) return;
            if (extra.Count == 0) mr.sharedMaterial = _mat.m;
            else { var a = new Material[1 + extra.Count]; a[0] = _mat.m; for (int i = 0; i < extra.Count; i++) a[i + 1] = extra[i]; mr.sharedMaterials = a; }
        }
        public bool cast
        {
            get => mr.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off;
            set { castShadow = value; mr.shadowCastingMode = value ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off; }
        }
        public bool receive { get => mr.receiveShadows; set => mr.receiveShadows = value; }
        public override int RenderOrder { get => mr.sortingOrder; set => mr.sortingOrder = value; }
    }
}
