// Render scale and upscaling (Settings › Graphics): the 3D view can render below the screen's resolution and be
// scaled up (Unity's STP, AMD's FSR 1, or plain bilinear), the biggest single saving on the graphics card; the
// interface always stays sharp. 100% (the default) renders at full size. Applied to the pipeline asset when it
// changes (the asset is made by Set Up Project, not kept in the repository).
using UnityEngine;
using UnityEngine.Rendering.Universal;
using static NovaStriker.Sim.Cfg;

namespace NovaStriker.Game
{
    public static class RenderScale
    {
        static float shownScale = -1; static string shownFilter;
        public static void Apply()
        {
            float s = Mathf.Clamp((float)SETTINGS.renderScale, 0.5f, 1f); string f = SETTINGS.upscaler ?? "stp";
            if (s == shownScale && f == shownFilter) return;
            var asset = UniversalRenderPipeline.asset; if (asset == null) return;
            shownScale = s; shownFilter = f;
            asset.renderScale = s;
            asset.upscalingFilter = f == "fsr" ? UpscalingFilterSelection.FSR : f == "linear" ? UpscalingFilterSelection.Linear : UpscalingFilterSelection.STP;
        }
    }
}
