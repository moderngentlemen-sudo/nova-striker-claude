// Heat haze and shockwave refraction (PostFx, Fx.Cine): a see-through particle that bends the scene behind it. It
// samples the camera's opaque texture (NovaSetup turns it on) offset by a ripple texture: red and green hold the
// offset (0.5 = none), alpha how much of it; the particle's alpha scales it, so it can swell and fade.
Shader "NovaStriker/Distort"
{
    Properties
    {
        _MainTex("Ripple", 2D) = "gray" {}
        _Strength("Strength", Float) = 0.035
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Distort"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _Strength;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float4 screen : TEXCOORD1; half4 color : COLOR; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.screen = ComputeScreenPos(o.positionCS);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                half4 t = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                float k = t.a * i.color.a;
                float2 uv = i.screen.xy / i.screen.w + (t.rg - 0.5) * 2.0 * _Strength * k;
                half3 c = SampleSceneColor(uv);
                return half4(c, saturate(k * 4.0));
            }
            ENDHLSL
        }
    }
}
