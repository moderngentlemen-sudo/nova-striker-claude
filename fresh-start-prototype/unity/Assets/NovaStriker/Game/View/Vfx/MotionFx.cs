// Air and dust from moving fast. A dash pulls a subtle rush of wind past the body: thin streaks of air streaming
// back, a faint shimmer where it starts. A wall slide drags a plume of dust up from the foot scraping the wall, like
// the classic wall slide. Read from the players' states each frame. Presentation only.
using System.Collections.Generic;
using NovaStriker.Sim;
using UnityEngine;

namespace NovaStriker.Game
{
    public sealed class MotionFx
    {
        readonly View view;
        readonly Dictionary<Player, (bool dash, float acc, float dust)> st = new Dictionary<Player, (bool, float, float)>();
        public MotionFx(View view) { this.view = view; }

        public void Update(float dt, World world)
        {
            var V = view.vfx; if (V == null || world == null) return;
            float amt = FxCfg.Amount;
            foreach (var p in world.players)
            {
                st.TryGetValue(p, out var s);
                bool dash = (p.state == "dash" || p.state == "rush" || p.state == "zip") && System.Math.Abs(p.vx) + System.Math.Abs(p.vy) > 4;
                double depth = LevelFeatures.Depth(p);
                if (dash)
                {
                    var vel = S.Dir(p.x, p.vx, p.vy); float speed = vel.magnitude; var dir = vel / Mathf.Max(0.01f, speed);
                    // the shimmer where it starts
                    if (!s.dash && FxCfg.Distortion) V.haze.Emit(S.W(p.x, p.y + p.h * 0.5, depth), -dir * 2, (float)p.h * 1.3f, 0.22f, new Color(1, 1, 1, 0.5f));
                    // streaks of air rushing back past the body
                    s.acc += dt * 70 * amt;
                    while (s.acc >= 1)
                    {
                        s.acc -= 1;
                        var at = S.W(p.x + (Random.value - 0.5) * p.w * 1.4, p.y + Random.value * p.h * 1.1, depth + (Random.value - 0.5) * 1.0);
                        V.streak.Emit(at + dir * (0.3f + Random.value * 0.6f), -dir * (speed * 0.6f + 6 + Random.value * 6), 0.018f + Random.value * 0.016f, 0.14f + Random.value * 0.1f, new Color(1, 1, 1, 0.16f + Random.value * 0.12f));
                    }
                }
                s.dash = dash;
                // dust dragged up the wall from the scraping foot
                if (p.wallSliding && !p.onGround && FxCfg.Smoke != "off")
                {
                    s.dust += dt * 16 * amt;
                    while (s.dust >= 1)
                    {
                        s.dust -= 1;
                        var at = S.W(p.x + p.wallDir * p.w * 0.45, p.y + 0.15 + Random.value * 0.25, depth + (Random.value - 0.5) * 0.4);
                        var v = S.Dir(p.x, -p.wallDir * (0.3 + Random.value * 0.4), 0.5 + Random.value * 0.6);
                        V.smoke.Emit(at, v, 0.22f + Random.value * 0.18f, 0.7f + Random.value * 0.5f, new Color(0.92f, 0.92f, 0.94f, 0.5f), Random.value * 6.3f, 0, true);
                    }
                }
                st[p] = s;
            }
            if (st.Count > world.players.Count + 4) { var keep = new HashSet<Player>(world.players); foreach (var k in new List<Player>(st.Keys)) if (!keep.Contains(k)) st.Remove(k); }
        }
    }
}
