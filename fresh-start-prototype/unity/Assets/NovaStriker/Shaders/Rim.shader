// rigs.js addRim: a Fresnel rim drawn as an extra additive pass over a character's armour, so characters
// separate from bright backgrounds. _RimAlpha keeps the rim showing while Echo's Veil fades the body.
Shader "NovaStriker/Rim"
{
    Properties
    {
        _RimColor("Rim colour", Color) = (0.84, 0.93, 1, 1)
        _RimStrength("Strength", Float) = 0.45
        _RimPower("Power", Float) = 2.4
        _RimAlpha("Veil", Float) = 0
        _BodyAlpha("Body opacity", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-10" }
        Pass
        {
            Name "Rim"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _RimColor;
                float _RimStrength, _RimPower, _RimAlpha, _BodyAlpha;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 posWS : TEXCOORD1; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                o.posWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.posWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                float3 V = unity_OrthoParams.w > 0.5 ? -GetViewForwardDir() : normalize(GetCameraPositionWS() - i.posWS);
                float f = pow(1.0 - saturate(abs(dot(normalize(i.normalWS), V))), _RimPower);
                // (the rim is emission in the prototype: it shows in full whatever the body's opacity, and
                // under the Veil it is all that's left)
                float k = max(_BodyAlpha, f * _RimAlpha);
                return half4(_RimColor.rgb * f * _RimStrength * k, 0);
            }
            ENDHLSL
        }
    }
}
