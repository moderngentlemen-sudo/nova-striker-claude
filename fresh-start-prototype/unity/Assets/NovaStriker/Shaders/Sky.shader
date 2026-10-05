// render.js buildSky: the sky dome's three-colour gradient (blended toward each route's as the camera arrives)
Shader "NovaStriker/Sky"
{
    Properties
    {
        _Top("Top", Color) = (0.17, 0.5, 0.83, 1)
        _Mid("Mid", Color) = (0.5, 0.75, 0.93, 1)
        _Bot("Bottom", Color) = (0.85, 0.93, 0.98, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry+500" }
        Pass
        {
            Name "Sky"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _Top, _Mid, _Bot;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 p : TEXCOORD0; };
            Varyings vert(Attributes v) { Varyings o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.p = normalize(v.positionOS.xyz); return o; }
            half4 frag(Varyings i) : SV_Target
            {
                float h = normalize(i.p).y;
                float3 c = h > 0.15 ? lerp(_Mid.rgb, _Top.rgb, smoothstep(0.15, 0.7, h)) : lerp(_Bot.rgb, _Mid.rgb, smoothstep(-0.2, 0.15, h));
                return half4(c, 1);
            }
            ENDHLSL
        }
    }
}
