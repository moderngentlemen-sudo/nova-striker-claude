// Optional player capture harness. It runs only with --nova-review <folder>; ordinary launch is unchanged.
using System; using System.IO; using System.Collections; using System.Collections.Generic;
using NovaStriker.Sim; using UnityEngine; using static NovaStriker.Sim.Cfg;
namespace NovaStriker.Game {
 public sealed partial class GameMain {
  [Serializable] sealed class ReviewReport {public string unity,device,cpu,gpu,api,commit;public int ramMB,width,height;public List<string> errors=new List<string>();public List<string> captures=new List<string>();public List<int> resetMeshes=new List<int>(),resetMaterials=new List<int>(),resetLights=new List<int>();public double cpuMs,gpuMs;public float p95;public long drawCalls,setPass,triangles,gcBytes;public int particles,particleCap,meshes,materials,lights;}
  ReviewReport review;string reviewFolder;
  void StartReviewIfRequested() {
   var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"--nova-review");if(i<0||i+1>=args.Length)return;
   reviewFolder=Path.GetFullPath(args[i+1]);Directory.CreateDirectory(reviewFolder);
   int c=Array.IndexOf(args,"--nova-commit");review=new ReviewReport {unity=Application.unityVersion,device=SystemInfo.deviceModel,cpu=SystemInfo.processorType,gpu=SystemInfo.graphicsDeviceName,api=SystemInfo.graphicsDeviceType.ToString(),ramMB=SystemInfo.systemMemorySize,width=Screen.width,height=Screen.height,commit=c>=0&&c+1<args.Length?args[c+1]:"local"};
   Application.logMessageReceived+=(message,trace,type)=>{if(type==LogType.Error||type==LogType.Exception)review.errors.Add(message+"\n"+trace);};
   QualitySettings.vSyncCount=0;Application.targetFrameRate=60;
   StartCoroutine(ReviewScenes());
  }
  IEnumerator Settle(int frames=60) {for(int i=0;i<frames;i++)yield return null;}
  IEnumerator Capture(string name, bool counted = true) {
   yield return new WaitForEndOfFrame();var texture=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Path.Combine(reviewFolder,name+".png"),texture.EncodeToPNG());Destroy(texture);if(counted)review.captures.Add(name);
  }
  IEnumerator ReviewScenes() {
   SETTINGS.levelTiers=true;SETTINGS.levelHazards=true;SETTINGS.depthLanes=true;SETTINGS.quality="high";SETTINGS.fxPreset="cinematic";SETTINGS.clouds="volumetric";SETTINGS.birds="realistic";SETTINGS.enemyModels=true;SETTINGS.aiTeammates=3;
   world.AddPlayer("keyboard","nova");started=true;ui.HideStart();
   foreach(var zone in new[]{"gym","arena","tower","skyline","foundry","undercity"}) {world.Teleport(zone);yield return Settle();yield return Capture(zone);}
   foreach(var boss in new[]{"warden","stormcaller"}) {world.BossRush(boss);yield return Settle();yield return Capture(boss);}
   world.Teleport("gym");world.enemies.Clear();var human=world.players[0];human.x=52;human.y=0;
   yield return Settle();
   foreach(var preset in new[]{"cinematic","balanced","classic","custom"}) {
    SETTINGS.fxPreset=preset;view.OnEvent(new Ev {type="blast",x=52,y=1,r=2,level=3,p=human,depth=LevelFeatures.LANE_W});yield return Settle(4);yield return Capture("effects_"+preset);
   }
   SetPaused(true);yield return Settle(3);yield return Capture("paused");SetPaused(false);
   world.Teleport("skyline");yield return Settle();Directory.CreateDirectory(Path.Combine(reviewFolder,"motion"));
   for(int frame=0;frame<40;frame++){yield return Settle(6);yield return Capture("motion/"+frame.ToString("D3"),false);}
   foreach(var type in new[]{"swarmer","shield","sniper","brute","post","turret","drone","mortar","charger","warden","stormcaller"}) {
    world.Teleport("gym");world.enemies.Clear();var enemy=Enemies.CreateEnemy(type,world.players[0].x+4,0);world.enemies.Add(enemy);yield return Settle(15);yield return Capture("enemy_"+type);
   }
   // Reset/settings lifecycle counts are evidence, not an assertion of no leak.
   for(int i=0;i<10;i++){world.Teleport(i%2==0?"gym":"arena");SETTINGS.enemyModels=i%2==0;yield return Settle(3);review.resetMeshes.Add(Resources.FindObjectsOfTypeAll<Mesh>().Length);review.resetMaterials.Add(Resources.FindObjectsOfTypeAll<Material>().Length);review.resetLights.Add(Resources.FindObjectsOfTypeAll<Light>().Length);}
   review.cpuMs=PerfOverlay.CpuMs;review.gpuMs=PerfOverlay.GpuMs;review.p95=PerfOverlay.P95;review.drawCalls=PerfOverlay.DrawCalls;review.setPass=PerfOverlay.SetPass;review.triangles=PerfOverlay.Triangles;review.gcBytes=PerfOverlay.GCBytes;review.particles=ParticleBudget.Live;review.particleCap=FxCfg.MaxParticles;review.meshes=Resources.FindObjectsOfTypeAll<Mesh>().Length;review.materials=Resources.FindObjectsOfTypeAll<Material>().Length;review.lights=Resources.FindObjectsOfTypeAll<Light>().Length;
   File.WriteAllText(Path.Combine(reviewFolder,"runtime.json"),JsonUtility.ToJson(review,true));
   Debug.Log("NOVA_REVIEW_COMPLETE "+review.captures.Count+" captures, "+review.errors.Count+" errors");Application.Quit(review.errors.Count==0?0:1);
  }
 }
}
