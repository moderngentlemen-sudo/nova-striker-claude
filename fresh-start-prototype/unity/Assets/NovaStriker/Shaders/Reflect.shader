// The deck's sheen (PlanarReflection): a thin sheet over the floor that shows the mirrored scene, rendered each
// frame by a second camera below the floor into _NovaReflectionTex. The mirror image already lines up with the
// screen, so the sheet samples it at its own screen position, rippled a touch by the floor's plating; the
// reflection is strongest at grazing angles (Fresnel), as on a polished floor.
Shader "NovaStriker/Reflect"
{
    Properties
    {
        _Tint("Tint", Color) = (0.86, 0.93, 1, 1)
        _Strength("Strength", Float) = 0.55
        _Base("Head-on share", Float) = 0.18
        _Ripple("Ripple", Float) = 0.004
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-50" }
        Pass
        {
            Name "Reflect"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_NovaReflectionTex); SAMPLER(sampler_NovaReflectionTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
                float _Strength, _Base, _Ripple;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float4 screen : TEXCOORD0; float3 positionWS : TEXCOORD1; float3 normalWS : TEXCOORD2; float fog : TEXCOORD3; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.screen = ComputeScreenPos(o.positionCS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                float2 uv = i.screen.xy / i.screen.w;
                // a faint ripple, as off plating that is not quite flat
                float2 w = i.positionWS.xz * 3.1;
                uv += _Ripple * float2(sin(w.x + w.y * 0.7), cos(w.y * 1.3 - w.x * 0.4));
                half3 refl = SAMPLE_TEXTURE2D(_NovaReflectionTex, sampler_NovaReflectionTex, uv).rgb * _Tint.rgb;
                float3 V = normalize(GetCameraPositionWS() - i.positionWS);
                float f = pow(1 - saturate(dot(normalize(i.normalWS), V)), 4);
                half a = _Strength * lerp(_Base, 1, f);
                half3 c = MixFog(refl, i.fog);
                return half4(c, a);
            }
            ENDHLSL
        }
    }
}
