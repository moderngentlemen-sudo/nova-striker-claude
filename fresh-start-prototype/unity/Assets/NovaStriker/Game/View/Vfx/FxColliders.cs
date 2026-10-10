// Invisible copies of the level's boxes for particles and debris to bounce off (Unity physics colliders on their
// own layer, FxPool.FX_LAYER; no rigid bodies). Curved stretches are cut into pieces of at most 2 m that follow the
// path. Breakable pieces lose their collider once broken. Rebuilt when the level's boxes change (Level.Version).
using System.Collections.Generic;
using NovaStriker.Game.Three;
using NovaStriker.Sim;
using UnityEngine;

namespace NovaStriker.Game
{
    public sealed class FxColliders
    {
        readonly Transform root;
        readonly List<(LevelBox b, BoxCollider c)> breakable = new List<(LevelBox, BoxCollider)>();
        int built = -1;

        public FxColliders(Transform parent) { root = new GameObject("fx colliders").transform; root.SetParent(parent, false); Build(); }

        void Build()
        {
            for (int i = root.childCount - 1; i >= 0; i--) Object.Destroy(root.GetChild(i).gameObject);
            breakable.Clear();
            foreach (var b in Level.BOXES)
            {
                if (b.type == 'g') continue;   // (gates open and close; particles pass them)
                float depth = View.DepthFor(b), hgt = (float)(b.y1 - b.y0);
                bool curved = Level.CurvedSpan(b.x0, b.x1);
                int n = curved ? Mathf.Max(1, (int)System.Math.Ceiling((b.x1 - b.x0) / 2)) : 1;
                for (int i = 0; i < n; i++)
                {
                    double x0 = b.x0 + (b.x1 - b.x0) * i / n, x1 = b.x0 + (b.x1 - b.x0) * (i + 1) / n, xm = (x0 + x1) / 2;
                    var go = new GameObject("c"); go.layer = FxPool.FX_LAYER; go.transform.SetParent(root, false);
                    go.transform.position = Th.P(S.W(xm, (b.y0 + b.y1) / 2));
                    go.transform.rotation = Th.Quat(0, S.YawAt(xm), 0);
                    var c = go.AddComponent<BoxCollider>(); c.size = new Vector3((float)(x1 - x0) * (curved ? 1.04f : 1), hgt, depth);
                    if (b.type == 'd') breakable.Add((b, c));
                }
            }
            built = Level.Version;
        }

        public void Update()
        {
            if (built != Level.Version) Build();
            foreach (var (b, c) in breakable) { bool on = !b.broken; if (c.enabled != on) c.enabled = on; }
        }
    }
}
