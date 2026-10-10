// The effect settings as the effects read them. A preset other than Custom decides every option; Custom uses each
// setting (Settings › Effects). Low graphics quality then caps them. Effects read these, never the raw settings.
using static NovaStriker.Sim.Cfg;

namespace NovaStriker.Game
{
    public static class FxCfg
    {
        static string P => SETTINGS.fxPreset ?? "cinematic";
        static bool Low => SETTINGS.quality == "low";
        static string Pick(string cinematic, string balanced, string classic, string custom) =>
            P == "cinematic" ? cinematic : P == "balanced" ? balanced : P == "classic" ? classic : custom;
        static bool Pick(bool cinematic, bool balanced, bool classic, bool custom) =>
            P == "cinematic" ? cinematic : P == "balanced" ? balanced : P == "classic" ? classic : custom;

        public static string Explosions => Pick("volumetric", "volumetric", "classic", SETTINGS.fxExplosions ?? "volumetric");
        public static string Projectiles => Pick("energy", "energy", "classic", SETTINGS.fxProjectiles ?? "energy");
        public static string Trails => Pick("long", "short", "short", SETTINGS.fxTrails ?? "long");
        public static string Smoke => Pick("rich", "light", "light", SETTINGS.fxSmoke ?? "rich");
        public static string Screen => SETTINGS.reducedScreenEffects ? "clean" : Pick("cinematic", "clean", "clean", SETTINGS.fxScreen ?? "cinematic");
        public static string Density { get { var d = Pick("high", "medium", "medium", SETTINGS.fxDensity ?? "high"); return Low ? "low" : d; } }
        public static string Debris { get { var d = Pick("physics", "simple", "off", SETTINGS.fxDebris ?? "physics"); return Low && d == "physics" ? "simple" : d; } }
        public static bool Lights => !Low && Pick(true, true, false, SETTINGS.fxLights);
        public static bool Distortion => !SETTINGS.reducedScreenEffects && !Low && Pick(true, false, false, SETTINGS.fxDistortion);
        public static bool Decals => !Low && Pick(true, true, false, SETTINGS.fxDecals);
        public static bool Classic => Explosions == "classic";

        public static void CaptureCustom()
        {
            var explosions = Explosions; var projectiles = Projectiles; var trails = Trails; var smoke = Smoke; var screen = Screen;
            var density = Density; var debris = Debris; bool lights = Lights, distortion = Distortion, decals = Decals;
            SETTINGS.fxExplosions = explosions; SETTINGS.fxProjectiles = projectiles; SETTINGS.fxTrails = trails; SETTINGS.fxSmoke = smoke;
            SETTINGS.fxScreen = screen; SETTINGS.fxDensity = density; SETTINGS.fxDebris = debris; SETTINGS.fxLights = lights;
            SETTINGS.fxDistortion = distortion; SETTINGS.fxDecals = decals; SETTINGS.fxPreset = "custom";
        }

        // How much of everything: a share of the full particle counts the effects ask for
        public static float Amount => Density == "high" ? 1f : Density == "medium" ? 0.55f : 0.25f;
        public static int MaxParticles => Density == "high" ? 6000 : Density == "medium" ? 3000 : 1200;
        public static int MaxLights => Lights ? (Density == "high" ? 8 : 4) : 0;
        public static int MaxDecals => Decals ? (Density == "high" ? 32 : 16) : 0;
    }
}
