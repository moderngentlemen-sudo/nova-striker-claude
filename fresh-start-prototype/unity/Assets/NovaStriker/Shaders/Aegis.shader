// aegisfx.js: Nova's hard-light Aegis. The dome and its flying shards are positioned on the CPU (AegisFx.cs);
// this draws each panel: a Fresnel rim, bright panel edges (barycentric), cracks revealed per panel from the
// crack texture, ripples spreading from recent hits, and a flicker while it is failing.
//   uv0: crack texture coordinates · uv1.xyz: barycentric · uv2: (crack, shard, fade) · uv3.xyz: panel direction
Shader "NovaStriker/Aegis"
{
    Properties
    {
        _CrackTex("Cracks", 2D) = "black" {}
        _Color("Colour", Color) = (1, 0.8, 0.4, 1)
        _Hot("Hot", Color) = (1, 0.96, 0.88, 1)
        _T("Time", Float) = 0
        _Alpha("Alpha", Float) = 1
        _Weak("Weak", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }
        Pass
        {
            Name "Aegis"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_CrackTex); SAMPLER(sampler_CrackTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _Color, _Hot;
                float _T, _Alpha, _Weak;
            CBUFFER_END
            float4 _Hits[4];
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; float3 bary : TEXCOORD1; float3 info : TEXCOORD2; float3 objN : TEXCOORD3; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 bary : TEXCOORD1; float3 info : TEXCOORD2; float3 objN : TEXCOORD3; float3 n : TEXCOORD4; float3 posWS : TEXCOORD5; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                o.posWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.posWS);
                o.n = TransformObjectToWorldNormal(v.normalOS);
                o.uv = v.uv; o.bary = v.bary; o.info = v.info; o.objN = v.objN;
                return o;
            }
            half4 frag(Varyings i, bool front : SV_IsFrontFace) : SV_Target
            {
                float crackK = i.info.x, shard = i.info.y, fade = i.info.z;
                if (fade <= 0.0) discard;
                float3 N = normalize(i.n), V = normalize(GetCameraPositionWS() - i.posWS);
                float rim = pow(saturate(1.0 - abs(dot(N, V))), 2.2);
                float e = min(min(i.bary.x, i.bary.y), i.bary.z), edge = 1.0 - smoothstep(0.0, 0.045, e);
                float c = SAMPLE_TEXTURE2D(_CrackTex, sampler_CrackTex, i.uv).r, crack = (c > 0.05 && c > 1.0 - crackK) ? 1.0 : 0.0;
                float3 objN = normalize(i.objN);
                float rip = 0.0;
                [unroll] for (int k = 0; k < 4; k++)
                {
                    float t = _T - _Hits[k].w;
                    if (t >= 0.0 && t < 0.5)
                    {
                        float ang = acos(clamp(dot(objN, _Hits[k].xyz), -1.0, 1.0));
                        rip += smoothstep(0.28, 0.0, abs(ang - t * 7.0)) * (1.0 - t / 0.5) + smoothstep(0.6, 0.0, ang) * max(0.0, 1.0 - t / 0.12);
                    }
                }
                float panel = frac(sin(dot(objN, float3(12.9898, 78.233, 37.719))) * 43758.5453);
                float a = 0.07 + 0.04 * panel + 0.5 * rim + 0.4 * edge + 0.75 * crack + 0.45 * rip;
                float3 col = _Color.rgb * (0.55 + 1.5 * rim + 1.4 * edge + 0.25 * panel) + _Hot.rgb * (2.0 * crack + 1.7 * rip);
                if (shard > 0.5) { a = (0.5 + 0.45 * edge + 0.9 * crack) * fade; col = _Color.rgb * (1.4 + 1.2 * edge) + _Hot.rgb * (2.4 * crack + 1.5 * fade); }
                a *= _Alpha * (front ? 1.0 : 0.45);
                a *= 1.0 - _Weak * 0.45 * step(0.5, frac(_T * 17.0 + panel * 0.3));
                return half4(col, saturate(a));
            }
            ENDHLSL
        }
    }
}
