// Lit cloud puffs (CloudLayer): camera-facing quads drawn instanced, each a puff from a 2 x 2 atlas (alpha = density,
// RGB = the puff's surface normal). Lit by the sun from the side it faces, shadowed where more cloud lies toward the
// sun, bright at the thin edges when the sun is behind (forward scatter), tinted by the sky above and below, and only
// partly lost to fog, so far banks still read as volume.
Shader "NovaStriker/Cloud"
{
    Properties
    {
        _MainTex("Puffs", 2D) = "white" {}
        _Opacity("Opacity", Float) = 0.9
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-100" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Cloud"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            float4 _NovaSkyTop, _NovaSkyBot;
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _Opacity;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 right : TEXCOORD1; float3 up : TEXCOORD2;
                float3 back : TEXCOORD3; float fog : TEXCOORD4; float shade : TEXCOORD5;
            };

            float hash(float3 p) { return frac(sin(dot(p, float3(12.9898, 78.233, 37.719))) * 43758.5453); }

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                float3 centre = float3(UNITY_MATRIX_M._m03, UNITY_MATRIX_M._m13, UNITY_MATRIX_M._m23);
                float sx = length(float3(UNITY_MATRIX_M._m00, UNITY_MATRIX_M._m10, UNITY_MATRIX_M._m20));
                float sy = length(float3(UNITY_MATRIX_M._m01, UNITY_MATRIX_M._m11, UNITY_MATRIX_M._m21));
                float3 right = normalize(UNITY_MATRIX_V[0].xyz), up = normalize(UNITY_MATRIX_V[1].xyz);
                float3 ws = centre + right * v.positionOS.x * sx + up * v.positionOS.y * sy;
                o.positionCS = TransformWorldToHClip(ws);
                float h = hash(float3(sx, sy, sx + sy));
                float variant = floor(h * 3.999);
                o.uv = v.uv * 0.5 + float2(fmod(variant, 2.0), floor(variant / 2.0)) * 0.5;
                o.right = right; o.up = up; o.back = normalize(GetCameraPositionWS() - centre);
                o.fog = ComputeFogFactor(o.positionCS.z);
                o.shade = 0.88 + 0.24 * hash(float3(sy, sx, sx * sy));
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                half4 t = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                float dens = t.a;
                float3 n = t.rgb * 2.0 - 1.0;
                float3 N = normalize(i.right * n.x + i.up * n.y + i.back * max(n.z, 0.2));
                Light sun = GetMainLight();
                float3 L = sun.direction;
                float wrap = saturate(dot(N, L) * 0.6 + 0.45);
                // more cloud toward the sun: self-shadow
                float2 toSun = float2(dot(L, i.right), dot(L, i.up)) * 0.05;
                float d2 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + toSun).a;
                float self = lerp(1.0, 0.55, saturate(d2 * 1.3 - dens * 0.3));
                // the sun behind the cloud lights its thin edges
                float scatter = pow(saturate(dot(-i.back, L)), 6.0) * (1.0 - dens) * 2.0;
                float3 ambient = lerp(_NovaSkyBot.rgb, _NovaSkyTop.rgb, N.y * 0.5 + 0.5);
                float3 c = (ambient * 0.6 + sun.color * (wrap * self * 0.85 + scatter)) * i.shade;
                c = lerp(c, MixFog(c, i.fog), 0.45);
                return half4(c, saturate(dens * _Opacity));
            }
            ENDHLSL
        }
    }
}
