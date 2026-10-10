// URP 17.3 Render Graph, before foreground transparency. The seeded density is shared by both
// quality renderers; the feature owns its material, Render Graph owns both transient targets.
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using NovaStriker.Game.Three;
using static NovaStriker.Sim.Cfg;

namespace NovaStriker.Game {
 public sealed class BackdropCloudFeature : ScriptableRendererFeature {
  static Texture3D density;
  static Camera owner;
  static Matrix4x4 worldToVolume;
  static Vector3 centre, halfSize;
  static float sky, clock;
  public static bool Available { get; private set; }
  public static bool Active => Available && GraphicsSettings.GetRenderPipelineSettings<RenderGraphSettings>()?.enableRenderCompatibilityMode != true && sky > .02f && SETTINGS.clouds == "volumetric" && SETTINGS.quality != "low" && SETTINGS.fxPreset != "balanced";
  public static float MinimumSeparation { get; private set; }
  public static int RenderedFrames { get; private set; }
  public static int Steps => SETTINGS.quality == "ultra" ? 40 : 24;
  public static void Place(Camera camera, Vector3 p, Vector3 tangent, Vector3 normal, float envelope, float skyWeight, float dt) {
   owner=camera; sky=skyWeight; clock+=Mathf.Max(0,dt);
   // Full oriented volume, including its front face, is outside the curved playable envelope.
   halfSize=new Vector3(380,72,100); MinimumSeparation=70;
   centre=Th.P(p + Vector3.up*10 - normal*(70+envelope+halfSize.z));
   var basis=Matrix4x4.identity;basis.SetColumn(0,Th.P(tangent));basis.SetColumn(1,Vector3.up);basis.SetColumn(2,Th.P(normal));basis.SetColumn(3,new Vector4(centre.x,centre.y,centre.z,1));
   worldToVolume=basis.inverse;
  }
  Material material; CloudPass pass;
  public override void Create() {
   CoreUtils.Destroy(material);var template=Resources.Load<Material>("NovaStriker/CloudVolume");
   Available=template!=null && template.shader.isSupported && SystemInfo.supports3DTextures;
   if(!Available)return;
   material=new Material(template);material.hideFlags=HideFlags.HideAndDontSave;
   if(density==null) {
    const int n=32;var voxels=new Color[n*n*n];var random=new System.Random(77013);
    for(int i=0;i<voxels.Length;i++)voxels[i]=new Color((float)random.NextDouble(),0,0,1);
    density=new Texture3D(n,n,n,TextureFormat.R8,false){name="Seeded backdrop density",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Bilinear};density.SetPixels(voxels);density.Apply(false,true);
   }
   material.SetTexture("_CloudDensity",density);pass=new CloudPass(material);
  }
  protected override void Dispose(bool disposing) {CoreUtils.Destroy(material);material=null;}
  public override void AddRenderPasses(ScriptableRenderer renderer,ref RenderingData data) {
   if(pass==null || !Active || data.cameraData.camera!=owner)return;
   material.SetMatrix("_WorldToCloud",worldToVolume);material.SetVector("_CloudHalfSize",halfSize);
   material.SetFloat("_CloudSky",sky);material.SetFloat("_CloudClock",clock);material.SetInt("_CloudSteps",Steps);
   renderer.EnqueuePass(pass);
  }
  sealed class CloudPass : ScriptableRenderPass {
   readonly Material material;
   sealed class Data {public Material material; public TextureHandle source;public int index;}
   public CloudPass(Material m) {material=m;renderPassEvent=RenderPassEvent.BeforeRenderingTransparents;ConfigureInput(ScriptableRenderPassInput.Depth);}
   public override void RecordRenderGraph(RenderGraph graph,ContextContainer frame) {
    var resources=frame.Get<UniversalResourceData>();
    if(!resources.cameraDepthTexture.IsValid() || resources.isActiveTargetBackBuffer)return;
    var desc=graph.GetTextureDesc(resources.activeColorTexture);desc.name="Nova backdrop clouds half resolution";
    desc.width=Mathf.Max(1,desc.width/2);desc.height=Mathf.Max(1,desc.height/2);
    desc.colorFormat=GraphicsFormat.R16G16B16A16_SFloat;desc.depthBufferBits=DepthBits.None;desc.msaaSamples=MSAASamples.None;desc.bindTextureMS=false;desc.clearBuffer=false;desc.filterMode=FilterMode.Bilinear;
    var clouds=graph.CreateTexture(desc);
    using(var b=graph.AddRasterRenderPass<Data>("Nova cloud volume",out var d)) {
     d.material=material;d.source=resources.cameraDepthTexture;d.index=0;b.UseTexture(d.source,AccessFlags.Read);b.SetRenderAttachment(clouds,0,AccessFlags.Write);
     b.SetRenderFunc((Data data,RasterGraphContext ctx)=>Blitter.BlitTexture(ctx.cmd,data.source,new Vector4(1,1,0,0),data.material,data.index));
    }
    using(var b=graph.AddRasterRenderPass<Data>("Nova cloud depth composite",out var d)) {
     d.material=material;d.source=clouds;d.index=1;b.UseTexture(clouds,AccessFlags.Read);b.UseTexture(resources.cameraDepthTexture,AccessFlags.Read);
     // Preserve the sky/opaque colour while blending the volume, then draw players/effects normally.
     b.SetRenderAttachment(resources.activeColorTexture,0,AccessFlags.ReadWrite);
     b.SetRenderFunc((Data data,RasterGraphContext ctx)=>Blitter.BlitTexture(ctx.cmd,data.source,new Vector4(1,1,0,0),data.material,data.index));
    }
    RenderedFrames++;
   }
  }
 }
}
