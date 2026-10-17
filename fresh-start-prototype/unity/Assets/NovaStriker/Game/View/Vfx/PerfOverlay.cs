// A small frame-time readout in the top right corner (Settings › Effects › Frame time readout): frame ms and FPS
// (smoothed), and what the effects have live: particles, effect lights and decals.
using UnityEngine;
using static NovaStriker.Sim.Cfg;

namespace NovaStriker.Game
{
    public sealed class PerfOverlay : MonoBehaviour
    {
        public static int Particles, Lights, Decals;   // (set by the effects each frame)
        float ms = 16.7f;
        GUIStyle style;

        void Update() => ms += (Time.unscaledDeltaTime * 1000 - ms) * 0.05f;

        void OnGUI()
        {
            if (!SETTINGS.perfOverlay) return;
            style ??= new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.UpperRight, wordWrap = true, richText = true, normal = { textColor = Color.white } };
            string t = $"{ms:0.0} ms  {1000 / Mathf.Max(0.1f, ms):0} fps\nparticles {Particles}  lights {Lights}  decals {Decals}\n<size=11>{LoadTimer.Report}</size>";
            var r = new Rect(Screen.width - 520, 8, 510, 90);
            GUI.color = new Color(0, 0, 0, 0.6f); GUI.Label(new Rect(r.x + 1, r.y + 1, r.width, r.height), t, style);
            GUI.color = Color.white; GUI.Label(r, t, style);
        }
    }
}
