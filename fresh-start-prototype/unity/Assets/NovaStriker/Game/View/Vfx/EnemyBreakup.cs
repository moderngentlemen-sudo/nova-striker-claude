// Enemies break apart into their own pieces (Settings › Effects › Debris: Physics). The moment one dies, every visible
// part of its model (armour plates, limbs, the head, the gun, with their ink outlines) becomes a loose physics body
// that is thrown out from the killing blow, tumbles, bounces off the level (FxColliders) and cools: its glow fades,
// then it shrinks away. A boss holds its death throes first, then bursts. Each piece is a rigidbody with a box
// collider and the visual as a child carrying the part's exact transform (the rigs mirror by negative scale, which
// colliders can't take). Presentation only.
using System.Collections.Generic;
using NovaStriker.Game.Three;
using NovaStriker.Sim;
using UnityEngine;

namespace NovaStriker.Game
{
    public sealed class EnemyBreakup
    {
        const int MAX_PIECES = 320;        // oldest go first past this
        const float LIFE = 4.5f, SHRINK = 0.7f, COOL = 1.6f;
        public const int LAYER = 10;       // the pieces: they hit the level's colliders (FxPool.FX_LAYER) and each other only
        sealed class Group { public int alive; public readonly List<Material> mats = new List<Material>(); }   // (one enemy's material copies, freed with its last piece)
        sealed class Piece { public Rigidbody rb; public Transform tr; public float t; public Vector3 scale; public Material[] mats; public Color[] emit; public Group g; }
        readonly List<Piece> pieces = new List<Piece>();
        readonly HashSet<Enemy> done = new HashSet<Enemy>();
        readonly Transform root;
        static bool layersSet;

        public EnemyBreakup()
        {
            root = new GameObject("Enemy Pieces").transform;
            if (!layersSet)
            {
                layersSet = true;
                for (int i = 0; i < 32; i++) if (i != LAYER && i != FxPool.FX_LAYER) Physics.IgnoreLayerCollision(LAYER, i, true);
            }
        }

        public static bool On => FxCfg.Debris == "physics";

        // Each frame for each enemy rig, after it is posed: break it up once when it dies (a boss when its throes end)
        public bool Update(EnemyRig R, Enemy e, Vector3 hitFrom)
        {
            if (!e.dead || !On) return false;
            if (done.Contains(e)) { R.root.visible = false; return true; }
            if (e.boss && e.deathT < 66) return false;
            done.Add(e);
            Break(R, e, hitFrom);
            R.root.visible = false;
            return true;
        }
        public void Forget(Enemy e) => done.Remove(e);

        void Break(EnemyRig R, Enemy e, Vector3 hitFrom) => Detach(R.root, R.root.tr.position + Vector3.up * (float)(e.h * 0.5), e, hitFrom);
        // An armour plate knocked off: the plate itself flies (the rig hides it from now on)
        public void BreakPart(TObj part, EnemyRig R, Enemy e, Vector3 hitFrom) { if (On && part != null && part.go.activeInHierarchy) Detach(part, R.root.tr.position + Vector3.up * (float)(e.h * 0.5), e, hitFrom); }

        void Detach(TObj from, Vector3 centre, Enemy e, Vector3 hitFrom)
        {
            var copies = new Dictionary<Material, Material>(); var grp = new Group();
            Material Copy(Material m) { if (m == null) return null; if (!copies.TryGetValue(m, out var c)) { copies[m] = c = new Material(m); grp.mats.Add(c); } return c; }
            float power = e.boss ? 2.2f : e.type == "brute" || e.type == "charger" || e.type == "mortar" ? 1.4f : 1f;
            var away = centre - hitFrom; away.y = 0; away = away.sqrMagnitude > 1e-4f ? away.normalized : Vector3.zero;
            from.traverse(o =>
            {
                if (!(o is TMesh me) || o.outline || me.mr == null || !me.go.activeInHierarchy || !me.mr.enabled) return;
                var tm = me.material;
                if (tm == null || tm.transparent || tm.kind == TMat.Kind.Sprite) return;   // (the shield bubble, the stun stars, the tag)
                var mf = me.mf; if (mf == null || mf.sharedMesh == null) return;
                var b = me.mr.bounds; if (b.size.sqrMagnitude < 1e-4f) return;
                var go = new GameObject("piece"); go.layer = LAYER; go.transform.SetParent(root, false);
                go.transform.position = b.center;
                var col = go.AddComponent<BoxCollider>(); col.size = Vector3.Max(b.size * 0.8f, Vector3.one * 0.06f);
                var rb = go.AddComponent<Rigidbody>();
                rb.mass = Mathf.Clamp(b.size.x * b.size.y * b.size.z * 40, 0.2f, 40);
                rb.interpolation = RigidbodyInterpolation.Interpolate; rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
                // thrown out from the middle and away from the blow, up, and spinning
                var outward = b.center - centre; outward = outward.sqrMagnitude > 1e-4f ? outward.normalized : Random.onUnitSphere;
                rb.linearVelocity = (outward * (2.5f + Random.value * 3) + away * 3 + Vector3.up * (3 + Random.value * 3)) * power * 0.8f;
                rb.angularVelocity = Random.insideUnitSphere * 9;
                var mats = new List<Material>(); var emit = new List<Color>();
                void Visual(MeshRenderer src, MeshFilter srcF)
                {
                    var v = new GameObject("visual"); v.layer = LAYER; v.transform.SetParent(go.transform, false);
                    v.transform.SetPositionAndRotation(src.transform.position, src.transform.rotation);
                    v.transform.localScale = src.transform.lossyScale;
                    v.AddComponent<MeshFilter>().sharedMesh = srcF.sharedMesh;
                    var r = v.AddComponent<MeshRenderer>(); var sm = src.sharedMaterials; var dm = new Material[sm.Length];
                    for (int i = 0; i < sm.Length; i++)
                    {
                        dm[i] = Copy(sm[i]);
                        if (dm[i] != null && dm[i].HasProperty("_EmissionColor")) { mats.Add(dm[i]); emit.Add(dm[i].GetColor("_EmissionColor")); }
                    }
                    r.sharedMaterials = dm; r.shadowCastingMode = src.shadowCastingMode; r.receiveShadows = true;
                }
                Visual(me.mr, mf);
                foreach (var c in me.children) if (c is TMesh sh && sh.outline && sh.mr != null && sh.mf != null) Visual(sh.mr, sh.mf);
                pieces.Add(new Piece { rb = rb, tr = go.transform, scale = Vector3.one, mats = mats.ToArray(), emit = emit.ToArray(), g = grp }); grp.alive++;
            });
            while (pieces.Count > MAX_PIECES) { Kill(pieces[0]); pieces.RemoveAt(0); }
        }

        public void Tick(float dt)
        {
            for (int i = pieces.Count - 1; i >= 0; i--)
            {
                var p = pieces[i]; p.t += dt;
                if (p.tr == null || p.t >= LIFE) { Kill(p); pieces.RemoveAt(i); continue; }
                // the glow cools, then it shrinks away
                float cool = Mathf.Clamp01(1 - p.t / COOL);
                for (int k = 0; k < p.mats.Length; k++) if (p.mats[k] != null) p.mats[k].SetColor("_EmissionColor", p.emit[k] * cool);
                float s = Mathf.Clamp01((LIFE - p.t) / SHRINK);
                if (s < 1) p.tr.localScale = p.scale * Mathf.Max(0.01f, s * s);
            }
        }

        static void Kill(Piece p)
        {
            if (p.tr != null) Object.Destroy(p.tr.gameObject);
            if (p.g != null && --p.g.alive <= 0) { foreach (var m in p.g.mats) Object.Destroy(m); p.g.mats.Clear(); }
        }
        public int Count => pieces.Count;
    }
}
