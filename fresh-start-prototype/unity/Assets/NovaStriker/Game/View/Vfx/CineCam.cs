// Cinematic moments (Settings › Graphics › Cinematic camera, Slow motion): a boss's entrance, its second phase, its
// fall, finishers and ultimates get a shot of their own. The camera eases toward the subject and in or out,
// letterbox bars slide in, and time slows (the whole game: it is real slow motion, so play stays fair), then it all
// eases back. Ultimates already push in on their caller (View.UpdateCamera); they get the bars. Presentation, plus
// the time scale GameMain applies to the simulation's clock.
using NovaStriker.Sim;
using UnityEngine;
using static NovaStriker.Sim.Cfg;

namespace NovaStriker.Game
{
    public sealed class CineCam
    {
        sealed class Shot { public double x, y; public float focus, zoom, slow, bars, dur, t; public int rank; }
        Shot cur;
        float barsUlt;
        // The live values: how far the camera leans to the subject (0-1), its zoom (distance multiplier), the time
        // scale, and the letterbox (0-1)
        public double X, Y; public float Focus, Zoom = 1, TimeScale = 1, Bars;

        void Start(double x, double y, float focus, float zoom, float slow, float bars, float dur, int rank)
        {
            if (cur != null && cur.rank > rank && cur.t < cur.dur * 0.6f) return;   // (a bigger moment isn't cut short by a smaller one)
            cur = new Shot { x = x, y = y, focus = focus, zoom = zoom, slow = slow, bars = bars, dur = dur, rank = rank };
        }

        public void OnEvent(Ev ev)
        {
            if (!SETTINGS.cineCamera) return;
            var e = ev.e;
            switch (ev.type)
            {
                case "bossIntro": if (e != null) Start(e.x, e.y + e.h * 0.55, 0.8f, 0.78f, 0.7f, 1, 2.4f, 2); break;
                case "bossPhase": if (e != null) Start(ev.x, ev.y, 0.55f, 0.85f, 0.4f, 0.8f, 1.3f, 2); break;
                case "bossDown": Start(ev.x, ev.y, 0.9f, 0.62f, 0.18f, 1, 2.6f, 3); break;
                case "teamFinisher": Start(ev.x, ev.y, 0.3f, 1.12f, 0.22f, 1, 1.5f, 3); break;
                case "ultFinisher": Start(ev.x, ev.y, 0.4f, 0.85f, 0.3f, 0.8f, 1.0f, 1); break;
            }
        }

        // Each frame, in real time (dt unscaled)
        public void Update(float dt, World world)
        {
            float focus = 0, zoom = 1, slow = 1, bars = 0;
            if (cur != null)
            {
                cur.t += dt;
                if (cur.t >= cur.dur) cur = null;
                else
                {
                    // eases in fast, holds, eases out over the last 40%
                    float a = Mathf.SmoothStep(0, 1, Mathf.Clamp01(cur.t / 0.18f)) * Mathf.SmoothStep(0, 1, Mathf.Clamp01((cur.dur - cur.t) / (cur.dur * 0.4f)));
                    X = cur.x; Y = cur.y; focus = cur.focus * a; zoom = Mathf.Lerp(1, cur.zoom, a); slow = Mathf.Lerp(1, cur.slow, a); bars = cur.bars * a;
                }
            }
            // an ultimate being called or playing out: the bars, held
            bool ult = SETTINGS.cineCamera && world != null && world.ultCast != null;
            barsUlt += ((ult ? 1 : 0) - barsUlt) * (1 - Mathf.Exp(-dt * 6));
            Focus = focus; Zoom = zoom; Bars = Mathf.Max(bars, barsUlt);
            TimeScale = SETTINGS.slowMo ? slow : 1;
        }
    }
}
