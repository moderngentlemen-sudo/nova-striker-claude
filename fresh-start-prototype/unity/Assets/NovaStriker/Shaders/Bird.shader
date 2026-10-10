// The gulls (Birds): one instanced mesh whose wings beat in the vertex shader. uv2.x is how far out along the wing a
// vertex is (0 at the body, 1 at the tip), uv2.y its side (-1 left, +1 right, 0 body); each bird's wing angle comes
// per instance (_Flap.x, radians). The inner wing turns about the shoulder and the outer part a little further, so
// the stroke bends. Simple lighting: the sun, the sky's ambient, and a soft rim; colours from the vertex colours.
Shader "NovaStriker/Bird"
{
    Properties { }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Pass
        {
            Name "Bird"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float4 _NovaSkyTop, _NovaSkyBot;
            CBUFFER_START(UnityPerMaterial)
                float _Unused;
            CBUFFER_END
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Flap)
            UNITY_INSTANCING_BUFFER_END(Props)

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; float2 uv2 : TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; half4 color : COLOR; float fog : TEXCOORD2; };

            float3 Turn(float3 p, float a, float pivotX)
            {
                float s = sin(a), c = cos(a);
                float x = p.x - pivotX;
                return float3(pivotX + x * c - p.y * s, x * s + p.y * c, p.z);
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                float flap = UNITY_ACCESS_INSTANCED_PROP(Props, _Flap).x;
                float side = v.uv2.y, w = v.uv2.x;
                float a = flap * side * (0.65 + 0.55 * w);
                float3 p = v.positionOS.xyz, n = v.normalOS;
                if (side != 0) { p = Turn(p, a, side * 0.06); n = Turn(n, a, 0); }
                o.positionWS = TransformObjectToWorld(p);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(n);
                o.color = v.color;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }
            half4 frag(Varyings i, bool front : SV_IsFrontFace) : SV_Target
            {
                float3 N = normalize(i.normalWS) * (front ? 1 : -1);
                Light sun = GetMainLight();
                float ndl = saturate(dot(N, sun.direction) * 0.7 + 0.3);
                float3 V = normalize(GetCameraPositionWS() - i.positionWS);
                float rim = pow(1 - saturate(dot(N, V)), 3) * 0.25;
                float3 ambient = lerp(_NovaSkyBot.rgb, _NovaSkyTop.rgb, N.y * 0.5 + 0.5);
                float3 c = i.color.rgb * (ambient * 0.55 + sun.color * ndl * 0.8) + rim * sun.color;
                c = lerp(c, MixFog(c, i.fog), 0.6);
                return half4(c, 1);
            }
            ENDHLSL
        }
    }
}
