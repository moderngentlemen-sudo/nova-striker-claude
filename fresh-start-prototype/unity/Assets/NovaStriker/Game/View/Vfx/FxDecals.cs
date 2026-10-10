// Scorch marks and scuffs: flat quads laid just above the floor under a blast or where someone lands hard, fading
// out over several seconds. A pool of FxCfg.MaxDecals (none when off or on Low quality). Floors only: the floor
// height comes from the level (Level.GroundBelow), the orientation from the path.
using NovaStriker.Game.Three;
using NovaStriker.Sim;
using UnityEngine;

namespace NovaStriker.Game
{
    public sealed class FxDecals
    {
        const int N = 32;
        sealed class D { public TMesh m; public TMat mat; public float t, life, op; }
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
            }
        }

        public int Active { get { int n = 0; foreach (var d in ds) if (d.m.visible) n++; return n; } }

        // A mark on the floor under sim point (x, y): `size` m across, from the scorch or scuff texture
        public void Stamp(double x, double y, float size, string tex = "scorch", float life = 10, float opacity = 0.9f, float depth = 0, float rot = 0)
        {
            if (FxCfg.MaxDecals <= 0) return;
            double floor = Level.GroundBelow(x, y + 0.4, Mathf.Clamp(Mathf.RoundToInt(depth / (float)LevelFeatures.LANE_W), -1, 1));
            if (double.IsNegativeInfinity(floor) || y - floor > 1.2) return;   // (in the air: nothing to mark)
            int max = Mathf.Min(N, FxCfg.MaxDecals);
            var d = ds[next % max]; next = (next + 1) % max;
            d.mat.map = FxTex.Get(tex); d.mat.colorHex = 0xffffff;
            d.m.position.copy(S.W(x, floor + 0.02 + 0.002 * (next % 8), depth));
            d.m.rotation.set(-Mathf.PI / 2, 0, S.YawAt(x) + rot);
            d.m.scale.set(size, size, 1);
            d.t = 0; d.life = life; d.op = opacity; d.mat.opacity = opacity; d.m.visible = true;
        }

        public void Update(float dt)
        {
            foreach (var d in ds)
            {
                if (!FxCfg.Decals) { d.m.visible = false; continue; }
                if (!d.m.visible) continue;
                d.t += dt;
                if (d.t >= d.life) { d.m.visible = false; continue; }
                d.mat.opacity = d.op * Mathf.Clamp01((d.life - d.t) / (d.life * 0.4f));
            }
        }
    }
}
