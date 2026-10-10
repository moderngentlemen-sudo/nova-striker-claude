// Metrics are sampled in the running player. Invalid platform counters are shown as unavailable (-1).
using UnityEngine; using UnityEngine.Profiling; using Unity.Profiling;
using static NovaStriker.Sim.Cfg;
namespace NovaStriker.Game {
 public sealed class PerfOverlay : MonoBehaviour {
  public static int Particles,Lights,WeatherLights,Decals;public static double CpuMs,GpuMs;public static float P95;public static long DrawCalls=-1,SetPass=-1,Triangles=-1,GCBytes=-1;
  readonly float[] samples=new float[3600],sorted=new float[3600];readonly FrameTiming[] timings=new FrameTiming[1];int count,next;float ms=16.7f;GUIStyle style;
  ProfilerRecorder draws,passes,tris,gc;
  void OnEnable(){draws=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Draw Calls Count");passes=ProfilerRecorder.StartNew(ProfilerCategory.Render,"SetPass Calls Count");tris=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Triangles Count");gc=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame");}
  void OnDisable(){draws.Dispose();passes.Dispose();tris.Dispose();gc.Dispose();}
  void Update(){
   float current=Time.unscaledDeltaTime*1000;ms+=(current-ms)*.05f;samples[next]=current;next=(next+1)%samples.Length;count=Mathf.Min(count+1,samples.Length);
   if(next%60==0){System.Array.Copy(samples,sorted,count);System.Array.Sort(sorted,0,count);P95=sorted[Mathf.Clamp(Mathf.CeilToInt(count*.95f)-1,0,count-1)];}
   FrameTimingManager.CaptureFrameTimings();if(FrameTimingManager.GetLatestTimings(1,timings)>0){CpuMs=timings[0].cpuFrameTime;GpuMs=timings[0].gpuFrameTime;}
   DrawCalls=draws.Valid?draws.LastValue:-1;SetPass=passes.Valid?passes.LastValue:-1;Triangles=tris.Valid?tris.LastValue:-1;GCBytes=gc.Valid?gc.LastValue:-1;
  }
  void OnGUI(){if(!SETTINGS.perfOverlay)return;style??=new GUIStyle(GUI.skin.label){fontSize=14,alignment=TextAnchor.UpperRight,normal={textColor=Color.white}};
   string t=$"{ms:0.0} ms  p95 {P95:0.0} ms  CPU {CpuMs:0.0} GPU {GpuMs:0.0}\nparticles {ParticleBudget.Live}/{FxCfg.MaxParticles}  lights {Lights}/{FxCfg.MaxLights} weather {WeatherLights}/3 marks {Decals}\ndraws {DrawCalls} SetPass {SetPass} triangles {Triangles} GC {GCBytes} B\nmanaged {Profiler.GetMonoUsedSizeLong()/1048576} MB";
   var r=new Rect(Screen.width-460,8,450,88);GUI.color=new Color(0,0,0,.65f);GUI.Label(new Rect(r.x+1,r.y+1,r.width,r.height),t,style);GUI.color=Color.white;GUI.Label(r,t,style);
  }
 }
}
