// The time of day (Settings › Graphics › Time of day): each route's own light (as built), a day that keeps turning
// (golden hour, dusk, night, dawn and back to full day, about every 12 minutes), or a fixed sunset or night. It moves
// the targets the view's atmosphere blends toward (sky, fog, sun and ambient colours), the sun's height and its
// strength, so a change eases in like a change of route. Indoor routes take it more softly. Presentation only.
using UnityEngine;
using static NovaStriker.Sim.Cfg;

namespace NovaStriker.Game
{
    public static class TimeOfDay
    {
        public struct Sky
        {
            public Color top, mid, bot, fog, sun, hemi;
            public float key, fill, exposure, height;   // (sun strength and ambient multipliers, exposure, the sun's height: 1 is the usual)
            public float w;                             // how much of this replaces the route's own light
        }
        static Sky K(uint top, uint mid, uint bot, uint fog, uint sun, uint hemi, float key, float fill, float exposure, float height, float w) =>
            new Sky { top = Look.Lin(top), mid = Look.Lin(mid), bot = Look.Lin(bot), fog = Look.Lin(fog), sun = Look.Lin(sun), hemi = Look.Lin(hemi), key = key, fill = fill, exposure = exposure, height = height, w = w };
        static readonly Sky DAY = new Sky { key = 1, fill = 1, exposure = 1, height = 1, w = 0 };
        static readonly Sky GOLDEN = K(0x2f4f86, 0xf08a4b, 0xffc27a, 0xf0a070, 0xff9a4a, 0x9aa6cc, 0.9f, 0.75f, 1.0f, 0.3f, 0.85f);
        static readonly Sky DUSK = K(0x1b2550, 0x7a4a7a, 0xd9786a, 0x8a6a8a, 0xff7a5a, 0x6a78ac, 0.5f, 0.6f, 1.05f, 0.12f, 0.9f);
        static readonly Sky NIGHT = K(0x060a1a, 0x121d3c, 0x26365a, 0x1b2742, 0x9fb4ff, 0x4a5a8c, 0.4f, 0.5f, 1.18f, 0.75f, 0.95f);
        static readonly Sky DAWN = K(0x3a5d8f, 0xe9a27a, 0xf6c89a, 0xe8b898, 0xffb070, 0xa8b8d8, 0.75f, 0.8f, 1.0f, 0.3f, 0.8f);
        // The turning day: (where in the cycle, the look); day holds longest
        static readonly (float t, Sky s)[] CYCLE = { (0f, DAY), (0.34f, DAY), (0.42f, GOLDEN), (0.5f, DUSK), (0.58f, NIGHT), (0.8f, NIGHT), (0.88f, DAWN), (0.96f, DAY), (1f, DAY) };
        public const float CYCLE_SECONDS = 720;

        static Sky Mix(Sky a, Sky b, float f) => new Sky
        {
            top = Color.Lerp(a.w > 0 ? a.top : b.top, b.w > 0 ? b.top : a.top, f), mid = Color.Lerp(a.w > 0 ? a.mid : b.mid, b.w > 0 ? b.mid : a.mid, f),
            bot = Color.Lerp(a.w > 0 ? a.bot : b.bot, b.w > 0 ? b.bot : a.bot, f), fog = Color.Lerp(a.w > 0 ? a.fog : b.fog, b.w > 0 ? b.fog : a.fog, f),
            sun = Color.Lerp(a.w > 0 ? a.sun : b.sun, b.w > 0 ? b.sun : a.sun, f), hemi = Color.Lerp(a.w > 0 ? a.hemi : b.hemi, b.w > 0 ? b.hemi : a.hemi, f),
            key = Mathf.Lerp(a.key, b.key, f), fill = Mathf.Lerp(a.fill, b.fill, f), exposure = Mathf.Lerp(a.exposure, b.exposure, f), height = Mathf.Lerp(a.height, b.height, f), w = Mathf.Lerp(a.w, b.w, f),
        };

        // The look now: `t` is the game's running time in seconds, `route` the route the camera is on
        public static Sky Now(float t, string route)
        {
            Sky s;
            switch (SETTINGS.timeOfDay ?? "route")
            {
                case "sunset": s = GOLDEN; break;
                case "night": s = NIGHT; break;
                case "cycle":
                {
                    float u = Mathf.Repeat(t / CYCLE_SECONDS, 1); int i = 0;
                    while (i < CYCLE.Length - 2 && CYCLE[i + 1].t <= u) i++;
                    var (t0, a) = CYCLE[i]; var (t1, b) = CYCLE[i + 1];
                    s = Mix(a, b, Mathf.SmoothStep(0, 1, t1 > t0 ? (u - t0) / (t1 - t0) : 0));
                    break;
                }
                default: return DAY;
            }
            if (route != "skyport") s.w *= 0.6f;   // (the foundry and the undercity keep more of their own light)
            return s;
        }
        // The route's target colour moved toward the time of day's
        public static Color Toward(Color route, Color tod, float w) => w > 0 ? Color.Lerp(route, tod, w) : route;
    }
}
