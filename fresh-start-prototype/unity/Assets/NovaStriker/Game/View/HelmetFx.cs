// Nova's helmet knocked off (Anim decides when: critical health). A copy of the helmet as it sits that moment, on
// the rig or on his 3D model, flies up and back, tumbling, bounces on the ground, then shrinks away. The head
// underneath already shows his face.
using System.Collections.Generic;
using NovaStriker.Sim;
using UnityEngine;

namespace NovaStriker.Game
{
    public static class HelmetFx
    {
        sealed class Piece { public Transform t; public Vector3 v, spin; public float ground, age; public Vector3 scale; }
        static readonly List<Piece> pieces = new List<Piece>();
        const float LIFE = 2.4f, SHRINK = 0.45f, GRAVITY = 22, RADIUS = 0.15f;

        // Called once the frame his health turns critical
        public static void Knock(Rig rig, Player p)
        {
            Transform src = rig.skin != null && rig.skin.ready && rig.skin.helmet != null ? rig.skin.helmet
                : rig.heads.TryGetValue("helmet", out var h) ? h.tr : null;
            if (src == null) return;
            var go = Object.Instantiate(src.gameObject, src.parent);
            go.name = "helmet-knocked"; var t = go.transform;
            t.SetParent(null, true); go.SetActive(true);
            foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.enabled = true;
            // knocked up and back from the way he faces, with a sideways wobble and a tumble
            Vector3 fwd = rig.root.tr.right * (float)p.facing;
            var v = -fwd * (2.2f + Random.value) + Vector3.up * (5.5f + Random.value * 1.5f) + Vector3.Cross(fwd, Vector3.up) * (Random.value - 0.5f);
            var spin = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), Random.Range(-1f, 1f)).normalized * Random.Range(500f, 800f);
            pieces.Add(new Piece { t = t, v = v, spin = spin, ground = rig.root.tr.position.y, scale = t.localScale });
        }

        public static void Update(float dt)
        {
            for (int i = pieces.Count - 1; i >= 0; i--)
            {
                var P = pieces[i]; P.age += dt;
                if (P.t == null) { pieces.RemoveAt(i); continue; }
                if (P.age > LIFE) { Object.Destroy(P.t.gameObject); pieces.RemoveAt(i); continue; }
                P.v.y -= GRAVITY * dt;
                var pos = P.t.position + P.v * dt;
                if (pos.y - RADIUS < P.ground && P.v.y < 0)
                {
                    pos.y = P.ground + RADIUS; P.v.y *= -0.38f; P.v.x *= 0.6f; P.v.z *= 0.6f; P.spin *= 0.55f;
                }
                P.t.position = pos;
                P.t.Rotate(P.spin * dt, Space.World);
                float left = LIFE - P.age;
                if (left < SHRINK) P.t.localScale = P.scale * Mathf.Max(0.001f, left / SHRINK);
            }
        }
    }
}
