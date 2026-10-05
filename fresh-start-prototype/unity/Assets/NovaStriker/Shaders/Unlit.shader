// three.js's MeshBasicMaterial and SpriteMaterial in one shader: a colour times an optional texture and
// vertex colours, with three's blending, depth and culling switches as properties (no keywords, so nothing
// needs a variant). With _Billboard on, the quad faces the camera like a THREE.Sprite: its centre is the
// object's origin, its size the object's x and y scale, turned by _Rotation in the screen plane.
Shader "NovaStriker/Unlit"
{
    Properties
    {
        _BaseColor("Color", Color) = (1, 1, 1, 1)
        _BaseMap("Map", 2D) = "white" {}
        _VertexColors("Vertex colours", Float) = 0
        _Fog("Fog", Float) = 1
        _Billboard("Billboard", Float) = 0
        _Rotation("Rotation", Float) = 0
        _SrcBlend("Src", Float) = 1
        _DstBlend("Dst", Float) = 0
        _ZWrite("ZWrite", Float) = 1
        _ZTest("ZTest", Float) = 4
        _Cull("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            ZTest [_ZTest]
            Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _BaseMap_ST;
                float _VertexColors, _Fog, _Billboard, _Rotation;
                float _SrcBlend, _DstBlend, _ZWrite, _ZTest, _Cull;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; float fog : TEXCOORD1; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                if (_Billboard > 0.5)
                {
                    float3 centreWS = TransformObjectToWorld(float3(0, 0, 0));
                    float sx = length(float3(UNITY_MATRIX_M._m00, UNITY_MATRIX_M._m10, UNITY_MATRIX_M._m20));
                    float sy = length(float3(UNITY_MATRIX_M._m01, UNITY_MATRIX_M._m11, UNITY_MATRIX_M._m21));
                    float2 p = v.positionOS.xy * float2(sx, sy);
                    float c = cos(_Rotation), s = sin(_Rotation);
                    p = float2(c * p.x - s * p.y, s * p.x + c * p.y);
                    float3 centreVS = TransformWorldToView(centreWS);
                    o.positionCS = TransformWViewToHClip(centreVS + float3(p, 0));
                }
                else o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.color = _VertexColors > 0.5 ? v.color : float4(1, 1, 1, 1);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor * i.color;
                if (_Fog > 0.5) c.rgb = MixFog(c.rgb, i.fog);
                return c;
            }
            ENDHLSL
        }
    }
}
