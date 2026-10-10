// Screen effects on top of the bloom (Settings › Effects › Screen effects, Cinematic): chromatic aberration that
// kicks on big blasts and settles, a brief lens punch on the biggest, a fine film grain, lens dirt that bloom lights
// up, and a lens flare off the sun (SRP Lens Flare) that follows the cloud cover and only shows on the open-sky
// Skyport route. Tone mapping and the vignette stay the Grade pass's.
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NovaStriker.Game
{
    public sealed class PostFx
    {
        readonly ChromaticAberration ca; readonly LensDistortion lens; readonly FilmGrain grain; readonly Bloom bloom;
        readonly LensFlareComponentSRP flare;
        float kick;

        public PostFx(VolumeProfile profile, Bloom bloom, Vector3 sunThree)
        {
            this.bloom = bloom;
            ca = profile.Add<ChromaticAberration>(true); ca.intensity.Override(0);
            lens = profile.Add<LensDistortion>(true); lens.intensity.Override(0);
            grain = profile.Add<FilmGrain>(true); grain.type.Override(FilmGrainLookup.Thin1); grain.intensity.Override(0); grain.response.Override(0.8f);
            var dirt = FxTex.Get("lensdirt");
            if (dirt != null) { bloom.dirtTexture.Override(dirt); bloom.dirtIntensity.Override(0); }
            // the sun's flare: a soft glow, a ring and a few ghosts toward the screen's centre
            var data = ScriptableObject.CreateInstance<LensFlareDataSRP>();
            data.elements = new[]
            {
                Element(SRPLensFlareType.Image, FxTex.Get("glow"), 0, 6, new Color(1, 0.93f, 0.8f), 0.5f),
                Element(SRPLensFlareType.Image, FxTex.Get("ring"), 0, 10, new Color(1, 0.9f, 0.75f), 0.12f),
                Element(SRPLensFlareType.Circle, null, 0.45f, 0.8f, new Color(0.6f, 0.85f, 1f), 0.18f),
                Element(SRPLensFlareType.Polygon, null, 0.85f, 1.3f, new Color(1f, 0.75f, 0.5f), 0.12f),
                Element(SRPLensFlareType.Circle, null, 1.3f, 0.5f, new Color(0.7f, 1f, 0.8f), 0.14f),
            };
            var go = new GameObject("Sun Flare"); go.transform.position = new Vector3(sunThree.x, sunThree.y, -sunThree.z);
            flare = go.AddComponent<LensFlareComponentSRP>();
            flare.lensFlareData = data; flare.intensity = 0; flare.maxAttenuationDistance = 2000; flare.useOcclusion = true; flare.occlusionRadius = 4;
            flare.allowOffScreen = false; flare.attenuationByLightShape = false;
        }

        static LensFlareDataElementSRP Element(SRPLensFlareType type, Texture tex, float pos, float scale, Color tint, float intensity)
        {
            var e = new LensFlareDataElementSRP
            {
                flareType = type, lensFlareTexture = tex, position = pos, uniformScale = scale, sizeXY = Vector2.one, tint = tint,
                blendMode = SRPLensFlareBlendMode.Additive, visible = true, sideCount = 6, fallOff = 0.6f, edgeOffset = 0.2f,
            };
            e.localIntensity = intensity;
            return e;
        }

        public void Kick(float k) => kick = Mathf.Max(kick, k);

        // Each frame: `sky` is 1 on the open-sky route, `cover` 1 when the sun is out
        public void Update(float dt, float sky, float cover)
        {
            kick = Mathf.Max(0, kick - dt * 3.5f);
            bool on = FxCfg.Screen == "cinematic";
            ca.intensity.Override(on ? Mathf.Min(0.6f, 0.04f + kick * 0.45f) : 0);
            lens.intensity.Override(on ? -Mathf.Max(0, kick - 0.6f) * 0.25f : 0);
            grain.intensity.Override(on ? 0.12f : 0);
            bloom.dirtIntensity.Override(on ? 1.5f : 0);
            flare.intensity = on ? sky * (0.25f + 0.75f * cover) : 0;
        }
    }
}
