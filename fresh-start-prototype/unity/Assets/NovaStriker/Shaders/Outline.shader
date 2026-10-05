// look.js outlineMaterial: an inverted-hull outline. The mesh drawn again, back faces only, pushed out along
// its normals by _Width (in the mesh's own space, so a rig drawn bigger gets a bolder line), in one dark colour.
Shader "NovaStriker/Outline"
{
    Properties
    {
        _BaseColor("Color", Color) = (0.04, 0.06, 0.09, 1)
        _Width("Width", Float) = 0.016
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry+10" }
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _Width;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float fog : TEXCOORD0; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 p = v.positionOS.xyz + normalize(v.normalOS) * _Width;
                o.positionCS = TransformObjectToHClip(p);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }
            half4 frag(Varyings i) : SV_Target { return half4(MixFog(_BaseColor.rgb, i.fog), 1); }
            ENDHLSL
        }
    }
}
