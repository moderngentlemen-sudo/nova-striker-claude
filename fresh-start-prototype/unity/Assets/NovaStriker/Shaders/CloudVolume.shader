Shader "NovaStriker/CloudVolume" {
 SubShader {
  Tags { "RenderPipeline"="UniversalPipeline" }
  Cull Off ZWrite Off ZTest Always
  HLSLINCLUDE
  #pragma target 4.5
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
  #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
  TEXTURE3D(_CloudDensity);SAMPLER(sampler_CloudDensity);
  float4x4 _WorldToCloud;float3 _CloudHalfSize;float _CloudSky,_CloudClock;int _CloudSteps;
  float3 _NovaSkyTop,_NovaSkyBot;
  float Density(float3 p) {
   float3 uv=p*.018+float3(_CloudClock*.0012,0,_CloudClock*.0003);
   float broad=SAMPLE_TEXTURE3D_LOD(_CloudDensity,sampler_CloudDensity,uv*.45,0).r;
   float detail=SAMPLE_TEXTURE3D_LOD(_CloudDensity,sampler_CloudDensity,uv*2.3,0).r;
   float f=smoothstep(.42,.72,broad*.78+detail*.22);
   float edge=saturate(min(_CloudHalfSize.x-abs(p.x),_CloudHalfSize.z-abs(p.z))*.025);
   float layers=exp(-pow((p.y+20)/27,2))+ .72*exp(-pow((p.y-34)/18,2));
   return f*edge*layers;
  }
  bool Interval(float2 uv,out float3 ro,out float3 rd,out float start,out float finish) {
   float raw=SampleSceneDepth(uv);
   #if !UNITY_REVERSED_Z
   raw=lerp(UNITY_NEAR_CLIP_VALUE,1,raw);
   #endif
   float3 opaque=ComputeWorldSpacePosition(uv,raw,UNITY_MATRIX_I_VP);
   #if UNITY_REVERSED_Z
   float farZ=.00001;
   #else
   float farZ=.99999;
   #endif
   float3 farPoint=ComputeWorldSpacePosition(uv,farZ,UNITY_MATRIX_I_VP);
   #if UNITY_REVERSED_Z
   float nearZ=1;
   #else
   float nearZ=UNITY_NEAR_CLIP_VALUE;
   #endif
   float3 nearPoint=ComputeWorldSpacePosition(uv,nearZ,UNITY_MATRIX_I_VP);
   float3 origin=unity_OrthoParams.w>.5?nearPoint:_WorldSpaceCameraPos;
   float3 ray=normalize(farPoint-origin);
   ro=mul(_WorldToCloud,float4(origin,1)).xyz;rd=mul((float3x3)_WorldToCloud,ray);
   // A signed epsilon avoids NaNs for a ray parallel to a slab.
   float3 safe=sign(rd+1e-8)*max(abs(rd),1e-6);
   float3 a=(-_CloudHalfSize-ro)/safe,b=(_CloudHalfSize-ro)/safe;
   float3 lo=min(a,b),hi=max(a,b);start=max(0,max(lo.x,max(lo.y,lo.z)));
   finish=min(min(hi.x,min(hi.y,hi.z)),max(0,dot(opaque-origin,ray)));
   return finish>start;
  }
  float4 Volume(Varyings input):SV_Target {
   float3 ro,rd;float start,finish;if(!Interval(input.texcoord,ro,rd,start,finish))return 0;
   int steps=clamp(_CloudSteps,8,40);float ds=(finish-start)/steps;
   float trans=1;float3 light=0;
   [loop] for(int i=0;i<40;i++) {
    if(i>=steps || trans<.025)break;
    float3 p=ro+rd*(start+(i+.5)*ds);float den=Density(p);
    float3 sun=normalize(mul((float3x3)_WorldToCloud,GetMainLight().direction));
    float shade=exp(-Density(p+sun*18)*1.6);
    float3 col=lerp(float3(.28,.39,.52),float3(.97,.96,.9),shade);
    col=lerp(col,_NovaSkyBot,saturate((start+i*ds)/1500)*.35);
    float alpha=(1-exp(-den*ds*.052))*_CloudSky;light+=trans*alpha*col;trans*=1-alpha;
   }
   float alpha=1-trans;return float4(light/max(alpha,.0001),alpha);
  }
  float4 Composite(Varyings input):SV_Target {
   float3 ro,rd;float start,finish;
   // Full-resolution depth rejection prevents half-resolution clouds bleeding onto player edges.
   if(!Interval(input.texcoord,ro,rd,start,finish))return 0;
   return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture,sampler_LinearClamp,input.texcoord,0);
  }
  ENDHLSL
  Pass {Name "CloudVolume" Blend Off HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Volume
   ENDHLSL
  }
  Pass {Name "CloudComposite" Blend SrcAlpha OneMinusSrcAlpha HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Composite
   ENDHLSL
  }
 }
}
