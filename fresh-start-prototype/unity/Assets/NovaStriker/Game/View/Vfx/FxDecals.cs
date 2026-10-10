// Scorch/scuff marks use a bounded native URP projector pool with surface-aligned quad fallback.
// Floor support comes from Level.GroundBelow; wall marks carry the actual hit normal.
using NovaStriker.Game.Three;
using NovaStriker.Sim;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace NovaStriker.Game
{
    public sealed class FxDecals
    {
        const int N = 32;
        sealed class D { public TMesh m; public TMat mat; public DecalProjector projector; public Material projected; public float t, life, op; public bool live; }
        readonly D[] ds = new D[N];
        int next;

        public FxDecals(TObj scene)
        {
            for (int i = 0; i < N; i++)
            {
                var mat = new TMat(TMat.Kind.Basic) { transparent = true, opacity = 0, depthWrite = false, fog = true };
                var m = new TMesh(Geo.Plane(1, 1), mat) { cast = false, receive = false, noOutline = true };
                m.rotation.x = -Mathf.PI / 2; m.visible = false; m.RenderOrder = 1; scene.add(m);
                ds[i] = new D { m = m, mat = mat };
                var template = Resources.Load<Material>("NovaStriker/ProjectedDecal");
                if (template != null && template.shader.isSupported)
                {
                    var root = scene.add(new TObj("projected surface mark"));
                    var projector = root.go.AddComponent<DecalProjector>(); projector.enabled = false;
                    var projected = new Material(template); projector.material = projected;
                    projector.drawDistance = 140; projector.pivot = Vector3.zero;
                    if (projector.IsValid()) { ds[i].projector = projector; ds[i].projected = projected; root.go.AddComponent<DecalMaterialOwner>().material = projected; }
                    else { Object.Destroy(projected); root.destroy(); }
                }
            }
        }

        public int Active { get { int n = 0; foreach (var d in ds) if (d.live) n++; return n; } }
        public int ProjectedActive { get { int n = 0; foreach (var d in ds) if (d.live && d.projector != null && d.projector.enabled) n++; return n; } }

        // A mark on the floor under sim point (x, y): `size` m across, from the scorch or scuff texture
        public void Stamp(double x, double y, float size, string tex = "scorch", float life = 10, float opacity = 0.9f, float depth = 0, float rot = 0)
        {
            if (FxCfg.MaxDecals <= 0) return;
            double floor = Level.GroundBelow(x, y + 0.4, Mathf.Clamp(Mathf.RoundToInt(depth / (float)LevelFeatures.LANE_W), -1, 1));
            if (double.IsNegativeInfinity(floor) || y - floor > 1.2) return;   // (in the air: nothing to mark)
            StampSurface(S.W(x, floor, depth), Vector3.up, size, tex, life, opacity, S.YawAt(x) + rot);
        }

        // Three-space point/normal from the actual hit. Projector and safe quad use one Unity conversion.
        public void StampSurface(Vector3 point, Vector3 normal, float size, string tex = "scorch", float life = 10, float opacity = .9f, float rot = 0)
        {
            if (FxCfg.MaxDecals <= 0 || normal.sqrMagnitude < .001f) return;
            normal.Normalize(); int max = Mathf.Min(N, FxCfg.MaxDecals);
            var d = ds[next % max]; next = (next + 1) % max;
            d.mat.map = FxTex.Get(tex); d.mat.colorHex = 0xffffff;
            var n = Th.P(normal); var up = Mathf.Abs(n.y) > .95f ? Vector3.forward : Vector3.up;
            var rotation = Quaternion.LookRotation(-n, up) * Quaternion.AngleAxis(rot * Mathf.Rad2Deg, Vector3.forward);
            d.m.position.copy(point + normal * (.02f + .002f * (next % 8))); d.m.tr.rotation = rotation;
            d.m.scale.set(size, size, 1);
            d.t = 0; d.life = life; d.op = opacity; d.mat.opacity = opacity; d.live = true;
            d.m.visible = d.projector == null;
            if (d.projector != null) { d.projected.SetTexture("Base_Map", FxTex.Get(tex)); d.projector.transform.SetPositionAndRotation(Th.P(point) + n * .08f, rotation); d.projector.size = new Vector3(size,size,.8f); d.projector.fadeFactor = opacity; d.projector.enabled = true; }
        }

        static void Hide(D d) { d.live = false; d.m.visible = false; if (d.projector != null) d.projector.enabled = false; }

        public void Update(float dt)
        {
            for (int i = 0; i < ds.Length; i++)
            {
                var d = ds[i];
                if (i >= FxCfg.MaxDecals) { Hide(d); continue; }
                if (!d.live) continue;
                d.t += dt;
                if (d.t >= d.life) { Hide(d); continue; }
                d.mat.opacity = d.op * Mathf.Clamp01((d.life - d.t) / (d.life * 0.4f));
                if (d.projector != null) d.projector.fadeFactor = d.mat.opacity;
            }
        }
    }
    // The fixed projector pool owns these clones for its scene lifetime.
    public sealed class DecalMaterialOwner : MonoBehaviour { public Material material; void OnDestroy() { if (material != null) Object.Destroy(material); } }
}
