// The last full-screen pass (look.js GradeShader + fx.js ImpactShader), after URP's bloom:
//   three.js's ACES Filmic tone mapping (with the route's exposure), then up to four screen-space shockwaves,
//   the colour grade and vignette (in display space, as in the prototype), then the impact frame in one of its
//   seven looks, or the dimming while an ultimate is called. URP's own tone mapping is off (GameLook.cs).
// HDR output (Settings: HDR output, on an HDR display): URP hands this pass the frame in the display's gamut,
// scaled to its paper white in nits, and only encodes it afterwards. The pass undoes that, makes the same picture
// as in SDR, then lets the highlights above a knee rise toward the display's peak (energy, bloom, blasts), and
// hands it back in the display's gamut and nits. Below the knee the picture matches SDR.
Shader "NovaStriker/Grade"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Blend Off Cull Off
        Pass
        {
            Name "Grade"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            // (for Vulkan, compile with DXC: the default cross-compiler mistranslates this shader for glslang)
            #pragma use_dxc vulkan
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

            float2 _Res;
            float _Exposure;
            float3 _Lift, _Gamma, _Gain;
            float _Sat, _Contrast, _Vignette;
            float4 _Waves[4];
            float _Amount, _Invert, _Zoom, _Split, _Seed, _InkTime, _Ring, _Glitch, _Dim, _Style, _Tinted, _Phase;
            float2 _Center;
            float3 _Accent;
            float _Hdr, _HdrPaperWhite, _HdrPeak, _HdrGamut;   // (_HdrGamut 1: Rec. 2020 primaries; 0: Rec. 709)

            static const float3x3 TO2020 = float3x3(0.627402, 0.329292, 0.043306, 0.069095, 0.919544, 0.011360, 0.016394, 0.088028, 0.895578);
            static const float3x3 FROM2020 = float3x3(1.660496, -0.587656, -0.072840, -0.124547, 1.132895, -0.008348, -0.018154, -0.100597, 1.118751);
            // The frame as rendered (linear, Rec. 709, 1 = paper white), whichever way URP is outputting
            float3 Scene(float3 c)
            {
                if (_Hdr < 0.5) return c;
                c /= max(_HdrPaperWhite, 1.0);
                return _HdrGamut > 0.5 ? mul(FROM2020, c) : c;
            }
            // The finished picture (display values, 0..1) for an HDR display: the highlights extended toward its peak
            float3 HdrOut(float3 c)
            {
                float3 lin = SRGBToLinear(saturate(c));
                float peak = clamp(_HdrPeak / max(_HdrPaperWhite, 1.0), 1.0, 6.0);
                float m = max(max(lin.r, lin.g), lin.b), e = smoothstep(0.55, 1.0, m);
                lin *= 1.0 + (peak - 1.0) * e * e;
                if (_HdrGamut > 0.5) lin = mul(TO2020, lin);
                return lin * max(_HdrPaperWhite, 1.0);
            }

            // GLSL's mod (HLSL's fmod truncates toward zero)
            float  gmod(float x, float y)   { return x - y * floor(x / y); }
            float2 gmod2(float2 x, float2 y) { return x - y * floor(x / y); }

            // three.js ACESFilmicToneMapping
            float3 RRTAndODTFit(float3 v)
            {
                float3 a = v * (v + 0.0245786) - 0.000090537;
                float3 b = v * (0.983729 * v + 0.4329510) + 0.238081;
                return a / b;
            }
            float3 ACESFilmic(float3 c)
            {
                const float3x3 inM = float3x3(0.59719, 0.35458, 0.04823, 0.07600, 0.90834, 0.01566, 0.02840, 0.13383, 0.83777);
                const float3x3 outM = float3x3(1.60475, -0.53108, -0.07367, -0.10208, 1.10813, -0.00605, -0.00327, -0.07276, 1.07602);
                c *= _Exposure / 0.6;
                c = mul(inM, c);
                c = RRTAndODTFit(c);
                c = mul(outM, c);
                return saturate(c);
            }
            // The rendered frame at uv, tone mapped and in display (sRGB) values, as three's OutputPass leaves it
            float3 Display(float2 uv)
            {
                float3 c = Scene(SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0).rgb);
                return LinearToSRGB(ACESFilmic(max(c, 0.0)));
            }
            // look.js GradeShader at uv: the shockwaves bend the picture, then the grade
            float3 Graded(float2 vUv)
            {
                float2 uv = vUv, asp = float2(_Res.x / _Res.y, 1.0);
                float glint = 0.0;
                [unroll] for (int i = 0; i < 4; i++)
                {
                    float4 w = _Waves[i];
                    if (w.w <= 0.0) continue;
                    float2 d = (uv - w.xy) * asp; float r = length(d);
                    float band = exp(-pow((r - w.z) / 0.035, 2.0));
                    uv -= normalize(d + 1e-5) / asp * band * w.w * 0.03;
                    glint += band * w.w;
                }
                float3 c = Display(uv) + glint * 0.06;
                c = pow(max(c * _Gain + _Lift * (1.0 - c), 0.0), 1.0 / _Gamma);
                float l = dot(c, float3(0.2126, 0.7152, 0.0722));
                c = lerp(float3(l, l, l), c, _Sat);
                c = (c - 0.5) * _Contrast + 0.5;
                float2 v = (vUv - 0.5) * asp;
                c *= 1.0 - _Vignette * smoothstep(0.35, 1.0, length(v));
                return saturate(c);
            }

            // ---- fx.js ImpactShader (tDiffuse is the graded picture) ----
            float hash(float n) { return frac(sin(n) * 43758.5453); }
            float hash2(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float2 hash22(float2 p) { return frac(sin(float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)))) * 43758.5453); }
            float noise(float2 p)
            {
                float2 i = floor(p), f = frac(p); f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(hash2(i), hash2(i + float2(1.0, 0.0)), f.x), lerp(hash2(i + float2(0.0, 1.0)), hash2(i + float2(1.0, 1.0)), f.x), f.y);
            }
            float luma(float3 c) { return dot(c, float3(0.299, 0.587, 0.114)); }
            float hexEdge(float2 p)
            {
                float2 r = float2(1.0, 1.732), h = r * 0.5, a = gmod2(p, r) - h, b = gmod2(p - h, r) - h, g = dot(a, a) < dot(b, b) ? a : b;
                g = abs(g); return smoothstep(0.43, 0.5, max(dot(g, normalize(r)), g.x));
            }
            float3 split3(float2 uv, float2 off) { return float3(Graded(uv + off).r, Graded(uv).g, Graded(uv - off).b); }
            float edgeAt(float2 uv)
            {
                float2 px = 1.5 / _Res;
                float e = abs(luma(Graded(uv + px * float2(-1.0, 1.0))) - luma(Graded(uv + px * float2(1.0, -1.0))))
                        + abs(luma(Graded(uv + px)) - luma(Graded(uv - px)));
                return saturate(e * 3.2);
            }

            float3 Ink(float2 vUv)
            {
                float3 src = Graded(vUv);
                if (_Amount <= 0.0 && _Dim <= 0.0) return src;
                float2 asp = float2(_Res.x / _Res.y, 1.0), d = vUv - _Center, dir = normalize(d * asp + float2(1e-5, 1e-5));
                float r = length(d * asp), ang = atan2(dir.y, dir.x), a = (ang + 3.14159) / 6.28318;
                if (_Amount <= 0.0)
                {
                    float l0 = luma(src), keep = smoothstep(0.72, 1.05, max(max(src.r, src.g), src.b));
                    float3 cool = float3(l0, l0, l0) * float3(0.46, 0.62, 0.8) * 0.55;
                    return lerp(src, lerp(cool, src, keep), _Dim);
                }
                float wv = _Ring - r, rm = _Ring > 0.0 ? exp(-wv * wv * 900.0) : 0.0;
                float3 acc = _Accent, outc, c, neg;
                int st = (int)(_Style + 0.5);
                float time = _InkTime, seed = _Seed, zoom = _Zoom, split = _Split, phase = _Phase, tinted = _Tinted, invert = _Invert, glitch = _Glitch;

                if (st == 0)   // Sci-fi hologram
                {
                    float2 uv = _Center + d * (1.0 - zoom) - dir / asp * rm * 0.03;
                    float band = floor(vUv.y * 30.0), g = step(0.84, hash(band * 1.31 + floor(time * 24.0) * 3.7 + seed)) * glitch;
                    uv.x += (hash(band * 7.13 + seed + floor(time * 24.0)) - 0.5) * 0.09 * g;
                    c = split3(uv, dir / asp * (split + g * 0.012));
                    float l = luma(c), edge = edgeAt(uv);
                    float3 mid = acc * float3(0.6, 0.85, 1.0) + float3(0.0, 0.05, 0.05), hot = lerp(acc, 1.0, 0.55);
                    float3 holo = lerp(acc * 0.04 + float3(0.005, 0.015, 0.03), mid, smoothstep(0.04, 0.95, l)) + hot * edge * 1.5;
                    float sat = max(max(c.r, c.g), c.b) - min(min(c.r, c.g), c.b);
                    holo = lerp(holo, c * 1.3, smoothstep(0.3, 0.6, sat) * smoothstep(0.5, 0.85, l) * (1.0 - tinted * 0.6));
                    holo *= 0.86 + 0.14 * sin(vUv.y * _Res.y * 1.35 - time * 40.0);
                    holo += hot * 0.6 * hexEdge(vUv * asp * 26.0) * 0.24 * (1.0 - smoothstep(0.15, 0.85, r));
                    float cell = floor(a * 96.0), w = hash(cell * 1.37 + seed), bnd = frac(a * 96.0);
                    holo += hot * step(0.6, w) * smoothstep(0.12, 0.0, abs(bnd - 0.5)) * smoothstep(0.1 + 0.2 * w, 0.42 + 0.3 * w, r) * 0.6;
                    holo += hot * rm * 0.75;
                    neg = (1.0 - c).bgr * lerp(float3(0.5, 0.92, 1.15), acc * 1.2 + 0.2, tinted);
                    neg = lerp(neg, lerp(float3(0.9, 0.99, 1.0), hot, tinted), smoothstep(0.24, 0.0, r));
                    outc = lerp(holo, neg, invert);
                }
                else if (st == 1)   // Comic ink
                {
                    float2 uv = _Center + d * (1.0 - zoom);
                    c = split3(uv, dir / asp * split);
                    float l = luma(c);
                    float2 px = vUv * _Res / 5.0; float dots = length(frac(px) - 0.5);
                    float3 paper = float3(1.0, 0.97, 0.9), inkc = float3(0.06, 0.05, 0.08);
                    float3 dotc = lerp(inkc, acc * 0.85, tinted);
                    float3 comic = l < 0.28 ? inkc : (l < 0.55 ? lerp(dotc, paper, step(0.32, dots)) : paper);
                    float sat = max(max(c.r, c.g), c.b) - min(min(c.r, c.g), c.b);
                    comic = lerp(comic, c * 1.2, smoothstep(0.35, 0.6, sat) * step(0.5, l));
                    float n = 110.0, cell = floor(a * n), w = hash(cell * 1.37 + seed), band = frac(a * n);
                    float speedLine = step(0.5, w) * step(abs(band - 0.5), 0.05 + 0.15 * hash(cell + seed * 3.1)) * smoothstep(0.16 + 0.22 * w, 0.42 + 0.3 * w, r);
                    comic = lerp(comic, lerp(inkc, acc * 0.7, tinted), speedLine);
                    neg = lerp(1.0 - c, lerp(float3(1.0, 0.98, 0.92), lerp(float3(1.0, 1.0, 1.0), acc, 0.5), tinted), smoothstep(0.22, 0.0, r));
                    outc = lerp(comic, neg, invert);
                }
                else if (st == 2)   // Eclipse
                {
                    float2 uv = _Center + d * (1.0 - zoom * 1.4);
                    c = split3(uv, dir / asp * split * 0.6);
                    float l = luma(c), edge = edgeAt(uv);
                    float rays = pow(abs(sin(ang * 9.0 + seed)), 18.0) * 0.6 + pow(abs(sin(ang * 23.0 - seed * 2.0)), 40.0) * 0.5;
                    float3 hot = lerp(acc, float3(1.0, 0.98, 0.9), 0.5);
                    float3 corona = hot * (0.35 / (r * 3.0 + 0.15)) + acc * rays * smoothstep(0.9, 0.05, r) * 1.4 + acc * 0.35 * smoothstep(1.2, 0.0, r);
                    float sil = smoothstep(0.62, 0.38, l);
                    float3 ecl = lerp(corona, float3(0.01, 0.005, 0.015), sil) + float3(1.0, 0.97, 0.9) * edge * 1.3 * (0.6 + 0.4 * sil);
                    float sunR = 0.055 + 0.02 * (1.0 - phase);
                    ecl = lerp(ecl, 0.0, smoothstep(sunR, sunR - 0.006, r));
                    ecl += lerp(float3(1.0, 1.0, 1.0), acc, 0.3) * exp(-pow((r - sunR) * 90.0, 2.0)) * 2.5;
                    ecl += hot * rm * 0.9;
                    neg = lerp(1.0 - float3(l, l, l), float3(1.0, 0.96, 0.85), smoothstep(0.35, 0.0, r));
                    outc = lerp(ecl, neg, invert);
                }
                else if (st == 3)   // Shatter
                {
                    float2 p = (vUv - _Center) * asp * 7.0;
                    float2 ip = floor(p), fp = frac(p); float md = 8.0, md2 = 8.0; float2 id = float2(0.0, 0.0), mo = float2(0.0, 0.0);
                    [unroll] for (int j = -1; j <= 1; j++)
                    [unroll] for (int i = -1; i <= 1; i++)
                    {
                        float2 g = float2(i, j), o = hash22(ip + g + seed), rr = g + o - fp; float dd = dot(rr, rr);
                        if (dd < md) { md2 = md; md = dd; id = ip + g; mo = rr; } else if (dd < md2) md2 = dd;
                    }
                    float crack = 1.0 - smoothstep(0.0, 0.06, sqrt(md2) - sqrt(md));
                    float2 sc = (id + 0.5) / 7.0 / asp, sdir = normalize(sc * asp + float2(1e-4, 1e-4));
                    float h = hash2(id + seed * 1.7), push = (0.012 + 0.03 * h) * (1.0 - phase * 0.7) * smoothstep(0.0, 0.25, length(sc * asp));
                    float2 uv = _Center + d * (1.0 - zoom) + sdir / asp * push + (hash22(id) - 0.5) * 0.01;
                    c = split3(uv, sdir / asp * (split + 0.004 * h));
                    float3 glass = c * (0.75 + 0.5 * h) + lerp(float3(0.85, 0.95, 1.0), acc, 0.6) * step(0.82, h) * smoothstep(0.4, 1.0, sin(dot(vUv, float2(30.0, 18.0)) + h * 9.0)) * 0.35;
                    glass = lerp(glass * float3(0.85, 0.92, 1.05), glass, 0.5) + lerp(float3(1.0, 1.0, 1.0), acc, 0.5) * crack * (1.4 - r);
                    glass += lerp(float3(1.0, 1.0, 1.0), acc, 0.4) * rm * 0.6;
                    neg = lerp(1.0 - c, lerp(float3(1.0, 1.0, 1.0), acc, 0.4), smoothstep(0.25, 0.0, r));
                    outc = lerp(glass, neg, invert);
                }
                else if (st == 4)   // Thunderclap
                {
                    float flick = 0.65 + 0.35 * step(0.45, hash(floor(time * 28.0) + seed));
                    float2 uv = _Center + d * (1.0 - zoom) + float2(hash(floor(time * 30.0)) - 0.5, hash(floor(time * 30.0) + 7.0) - 0.5) * 0.006 * glitch;
                    c = split3(uv, dir / asp * split * 1.4);
                    float l = luma(c), edge = edgeAt(uv);
                    float3 elec = lerp(float3(0.02, 0.0, 0.06), acc * 0.55, smoothstep(0.2, 0.9, 1.0 - l)) + lerp(acc, 1.0, 0.6) * edge * 1.2;
                    float bolt = 0.0;
                    [unroll] for (int i = 0; i < 7; i++)
                    {
                        float fi = (float)i, a0 = hash(fi * 3.7 + seed) * 6.28318;
                        float wob = (noise(float2(r * 14.0, fi * 5.0 + seed)) - 0.5) * 0.7 + (noise(float2(r * 45.0, fi * 9.0 + seed)) - 0.5) * 0.25;
                        float da = abs(gmod(ang - a0 - wob + 3.14159, 6.28318) - 3.14159) * r;
                        float len = 0.35 + 0.6 * hash(fi + seed * 2.0);
                        bolt += (0.0025 / (da + 0.0025)) * smoothstep(len, len * 0.6, r) * step(0.02, r);
                    }
                    bolt = min(bolt, 3.0);
                    elec += lerp(acc, 1.0, 0.65) * bolt * flick + acc * 0.4 * smoothstep(0.5, 0.0, r) * flick;
                    elec += lerp(float3(1.0, 1.0, 1.0), acc, 0.4) * rm;
                    neg = lerp(1.0 - c, float3(0.92, 0.94, 1.0), smoothstep(0.3, 0.0, r));
                    outc = lerp(elec * flick + elec * (1.0 - flick) * 0.5, neg, invert);
                }
                else if (st == 5)   // Sumi ink
                {
                    float2 uv = _Center + d * (1.0 - zoom * 0.8);
                    uv += (float2(noise(vUv * 40.0 + seed), noise(vUv * 40.0 - seed)) - 0.5) * 0.004;
                    c = split3(uv, float2(0.0, 0.0));
                    float l = luma(c), edge = edgeAt(uv);
                    float grain = noise(vUv * float2(600.0, 90.0)) * 0.06 + noise(vUv * 180.0) * 0.05;
                    float3 paper = float3(0.95, 0.92, 0.84) - grain;
                    float wash = smoothstep(0.62, 0.12, l + (noise(vUv * 12.0 + seed) - 0.5) * 0.18);
                    float3 inkc = float3(0.05, 0.045, 0.06);
                    float3 sumi = lerp(paper, lerp(float3(0.42, 0.4, 0.4), inkc, smoothstep(0.35, 0.9, wash)), wash);
                    sumi = lerp(sumi, inkc, smoothstep(0.25, 0.7, edge));
                    float cell = floor(a * 46.0), w = hash(cell * 2.1 + seed), bnd = frac(a * 46.0);
                    float dry = step(0.35, noise(float2(r * 60.0, cell * 3.0)));
                    float stroke = step(0.62, w) * step(abs(bnd - 0.5), 0.08 + 0.3 * hash(cell + seed)) * smoothstep(0.2 + 0.2 * w, 0.32 + 0.2 * w, r) * smoothstep(1.1, 0.7, r) * dry;
                    sumi = lerp(sumi, inkc, stroke);
                    float er = 0.17 + 0.05 * phase, th = 0.012 + 0.018 * (0.5 + 0.5 * sin(ang * 1.3 + seed)) * smoothstep(-2.6, 2.4, ang);
                    float enso = smoothstep(th, th * 0.4, abs(r - er)) * step(0.4, noise(float2(ang * 12.0, r * 80.0)) + 0.35);
                    sumi = lerp(sumi, lerp(float3(0.78, 0.08, 0.1), acc, tinted), enso * 0.9);
                    neg = lerp(1.0 - float3(l, l, l), paper, smoothstep(0.3, 0.0, r));
                    outc = lerp(sumi, neg, invert);
                }
                else   // Gravity well
                {
                    float pull = (1.0 - phase) * 2.6 * exp(-r * 3.0);
                    float sa = ang + pull, sr = r * (1.0 - 0.25 * exp(-r * 6.0) * (1.0 - phase));
                    float2 uv = _Center + float2(cos(sa), sin(sa)) * sr / asp * (1.0 - zoom);
                    c = split3(uv, dir / asp * split * 1.8);
                    float3 space = c * float3(0.55, 0.5, 0.75) * smoothstep(0.0, 0.35, r) + acc * 0.08;
                    float2 sg = floor(vUv * _Res / 3.0); float star = step(0.996, hash2(sg + seed));
                    float streak = pow(abs(sin(sa * 40.0 + seed)), 60.0) * smoothstep(0.15, 0.7, r) * 0.6;
                    space += float3(0.9, 0.9, 1.0) * star * smoothstep(0.1, 0.5, r) + lerp(acc, 1.0, 0.5) * streak;
                    float hr = 0.06 + 0.03 * phase;
                    space = lerp(space, 0.0, smoothstep(hr, hr - 0.01, r));
                    space += lerp(acc, float3(1.0, 0.95, 0.85), 0.45) * exp(-pow((r - hr * 1.55) * 55.0, 2.0)) * 2.4;
                    space += acc * exp(-pow((r - hr * 1.55) * 16.0, 2.0)) * 0.6;
                    space += lerp(acc, 1.0, 0.5) * rm * 0.8;
                    neg = lerp(1.0 - c, lerp(float3(1.0, 1.0, 1.0), acc, 0.35), smoothstep(0.28, 0.0, r));
                    outc = lerp(space, neg, invert);
                }
                return lerp(c, outc, _Amount);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 c = Ink(input.texcoord.xy);
                // back to linear: URP writes the display encoding on its final blit
                if (_Hdr > 0.5) return half4(HdrOut(c), 1.0);
                return half4(SRGBToLinear(saturate(c)), 1.0);
            }
            ENDHLSL
        }
    }
}
