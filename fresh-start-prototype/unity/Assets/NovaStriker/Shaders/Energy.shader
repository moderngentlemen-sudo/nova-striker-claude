Shader "NovaStriker/Energy"
{
    Properties {
        [HDR] _Tint("Energy colour", Color) = (1,0.8,0.4,1)
        _Opacity("Opacity", Range(0,1)) = 1
        _EnergyTime("Presentation time", Float) = 0
    }
    SubShader {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+4" "RenderType"="Transparent" }
        Pass {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off ZTest LEqual Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:ParticleInstancingSetup
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ParticlesInstancing.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _Tint; float _Opacity, _EnergyTime;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; half4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; float2 uv:TEXCOORD2; half4 color:COLOR; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes i) {
                Varyings o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionWS=TransformObjectToWorld(i.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.positionWS);
                o.normalWS=TransformObjectToWorldNormal(i.normalOS);o.uv=i.uv;o.color=i.color;
                #if defined(UNITY_PARTICLE_INSTANCING_ENABLED)
                uint packed=unity_ParticleInstanceData[unity_InstanceID].color;
                half4 particleColour=half4(packed&255u,(packed>>8)&255u,(packed>>16)&255u,(packed>>24)&255u)/255.0;
                o.color=lerp(half4(1,1,1,1),i.color,unity_ParticleUseMeshColors)*particleColour;
                #endif
                return o;
            }
            half4 Frag(Varyings i):SV_Target {
                float face=abs(dot(normalize(i.normalWS),GetWorldSpaceNormalizeViewDir(i.positionWS)));
                float front=pow(saturate(face),1.5);
                float flow=0.7+0.3*pow(saturate(sin(i.uv.x*3.4-_EnergyTime*22)),6);
                float filament=0.85+0.15*sin(i.uv.x*13+i.uv.y*19-_EnergyTime*11);
                half3 colour=(_Tint.rgb*(0.6+front*1.5)+min(_Tint.rgb,1)*front*0.65)*flow*filament*i.color.rgb;
                return half4(colour,_Opacity*(0.2+0.8*front)*i.color.a);
            }
            ENDHLSL
        }
    }
}
