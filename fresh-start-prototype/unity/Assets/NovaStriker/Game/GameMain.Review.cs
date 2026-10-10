// Optional player capture harness. It runs only with --nova-review <folder>; ordinary launch is unchanged.
using System; using System.IO; using System.Collections; using System.Collections.Generic;
using NovaStriker.Sim; using NovaStriker.Game.Three; using UnityEngine; using static NovaStriker.Sim.Cfg;
namespace NovaStriker.Game {
 public sealed partial class GameMain {
  [Serializable] sealed class ReviewReport {public string unity,device,cpu,gpu,api,commit;public int ramMB,width,height;public List<string> retainedMaterialOrigins=new List<string>();public List<string> errors=new List<string>();public List<string> captures=new List<string>();public List<int> resetMeshes=new List<int>(),resetMaterials=new List<int>(),resetLights=new List<int>(),characterMaterials=new List<int>(),characterMeshes=new List<int>();public double cpuMs,gpuMs;public float p95;public long drawCalls,setPass,triangles,gcBytes;public int particles,particleCap,meshes,materials,lights,peakParticles,peakEffectLights,peakWeatherLights,cloudFrames,cloudSteps,landmarkLods,projectedDecals,floorMarkPixels,wallMarkPixels;public bool settingsRoundtrip;}
  [Serializable] sealed class BlastReview {public string preset;public int fire,smoke,sparks;public bool fireInCamera;}
  ReviewReport review;string reviewFolder;bool reviewBudgetFailed,reviewChangePending,reviewHoldSimulation;
  bool reviewWallCamera;Vector3 reviewWallEye,reviewWallTarget;
  void LateUpdate() {
   if (review == null || reviewChangePending) return;
   if (reviewWallCamera) {var eye=Th.P(reviewWallEye);view.camPos=reviewWallEye;view.camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(Th.P(reviewWallTarget)-eye,Vector3.up));}
   int particles = ParticleBudget.Live;
   review.peakParticles=Math.Max(review.peakParticles,particles);review.peakEffectLights=Math.Max(review.peakEffectLights,PerfOverlay.Lights);review.peakWeatherLights=Math.Max(review.peakWeatherLights,PerfOverlay.WeatherLights);
   if (!reviewBudgetFailed && (particles > FxCfg.MaxParticles || PerfOverlay.Lights > FxCfg.MaxLights || PerfOverlay.WeatherLights > 3 || PerfOverlay.Feedback > 82 || PerfOverlay.ChargeFlashes > ChargeFX.MaxFlashes || EnergyTube.Active > EnergyTube.MaxActive)) {
    reviewBudgetFailed = true;
    review.errors.Add("Visual budget exceeded: particles="+particles+"/"+FxCfg.MaxParticles+", lights="+PerfOverlay.Lights+"/"+FxCfg.MaxLights+", weather="+PerfOverlay.WeatherLights+", feedback="+PerfOverlay.Feedback+", charge="+PerfOverlay.ChargeFlashes);
   }
  }
  void StartReviewIfRequested() {
   var startup=Environment.GetCommandLineArgs();int probe=Array.IndexOf(startup,"--nova-settings-check");
   if(probe>=0&&probe+1<startup.Length) {SettingsStore.Load();bool pass=PersistedReviewSettings();File.WriteAllText(Path.Combine(startup[probe+1],"settings-reload.json"),"{\"passed\":"+(pass?"true":"false")+"}");Debug.Log("NOVA_SETTINGS_RELOAD "+pass);Application.Quit(pass?0:1);return;}
   var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"--nova-surface-review");bool surfacesOnly=i>=0;
   if(!surfacesOnly)i=Array.IndexOf(args,"--nova-review");if(i<0||i+1>=args.Length)return;
   reviewFolder=Path.GetFullPath(args[i+1]);Directory.CreateDirectory(reviewFolder);
   int c=Array.IndexOf(args,"--nova-commit");review=new ReviewReport {unity=Application.unityVersion,device=SystemInfo.deviceModel,cpu=SystemInfo.processorType,gpu=SystemInfo.graphicsDeviceName,api=SystemInfo.graphicsDeviceType.ToString(),ramMB=SystemInfo.systemMemorySize,width=Screen.width,height=Screen.height,commit=c>=0&&c+1<args.Length?args[c+1]:"local"};
   TMat.ReviewOriginsEnabled=true;
   Application.logMessageReceived+=(message,trace,type)=>{if(type==LogType.Error||type==LogType.Exception)review.errors.Add(message+"\n"+trace);};
   Application.runInBackground=true;QualitySettings.vSyncCount=0;Application.targetFrameRate=60;
   StartCoroutine(surfacesOnly?ReviewSurfaceScenes():ReviewScenes());
  }
  IEnumerator Settle(int frames=12) {for(int i=0;i<frames;i++)yield return null;}
  void FrameReviewPlayer(Player human,float lead=3,float height=2.1f,float distance=14) {
   double halfH=distance*Math.Tan(SETTINGS.fov*Math.PI/360);world.cam=new Cam{x=human.x+lead,y=human.y+height,dist=distance,halfH=halfH,halfW=halfH*Screen.width/Screen.height};view.SnapCamera(world);
  }
  IEnumerator Capture(string name, bool counted = true) {
   // A transient zone banner should not cover the asset being reviewed.
   var banner=ui.canvas.transform.Find("banner");if(banner!=null)banner.gameObject.SetActive(false);
   yield return new WaitForEndOfFrame();var texture=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0,false);texture.Apply(false);File.WriteAllBytes(Path.Combine(reviewFolder,name+".png"),texture.EncodeToPNG());Destroy(texture);if(counted)review.captures.Add(name);
  }
  // Compare a frozen surface before/after projection; an enabled component alone is not rendering proof.
  int MarkPixels(string name,Vector3 surface) {
   var before=new Texture2D(2,2);var after=new Texture2D(2,2);
   before.LoadImage(File.ReadAllBytes(Path.Combine(reviewFolder,name+"_before.png")));after.LoadImage(File.ReadAllBytes(Path.Combine(reviewFolder,name+".png")));
   var a=before.GetPixels32();var b=after.GetPixels32();var p=view.camera.WorldToScreenPoint(Th.P(surface));int changed=0;
   for(int y=Math.Max(0,(int)p.y-70);y<Math.Min(before.height,(int)p.y+70);y++)for(int x=Math.Max(0,(int)p.x-70);x<Math.Min(before.width,(int)p.x+70);x++) {int i=y*before.width+x;if(a[i].r+a[i].g+a[i].b-b[i].r-b[i].g-b[i].b>75)changed++;}
   Destroy(before);Destroy(after);if(changed<24)review.errors.Add(name+" surface mark was not visibly rendered ("+changed+" darkened pixels)");return changed;
  }
  IEnumerator ReviewSurfaceScenes() {
   SETTINGS.aiTeammates=0;SETTINGS.quality="high";SETTINGS.fxPreset="cinematic";SETTINGS.levelHazards=false;
   world.AddPlayer("keyboard","nova");started=true;ui.HideStart();reviewHoldSimulation=true;
   yield return ReviewSurfaces();
   File.WriteAllText(Path.Combine(reviewFolder,"runtime.json"),JsonUtility.ToJson(review,true));
   Debug.Log("NOVA_SURFACE_REVIEW_COMPLETE "+review.errors.Count+" errors");Application.Quit(review.errors.Count==0?0:1);
  }
  IEnumerator ReviewScenes() {
   SETTINGS.levelTiers=true;SETTINGS.levelHazards=true;SETTINGS.depthLanes=true;SETTINGS.quality="high";SETTINGS.fxPreset="cinematic";SETTINGS.clouds="volumetric";SETTINGS.birds="realistic";SETTINGS.enemyModels=true;SETTINGS.aiTeammates=3;
   world.AddPlayer("keyboard","nova");started=true;ui.HideStart();
   foreach(var zone in new[]{"gym","arena","tower","skyline","foundry","undercity"}) {world.Teleport(zone);yield return Settle();yield return Capture(zone);}
   foreach(var boss in new[]{"warden","stormcaller"}) {world.BossRush(boss);yield return Settle();yield return Capture(boss);}
   SETTINGS.aiTeammates=0;world.Teleport("gym");world.enemies.Clear();var human=world.players[0];human.x=human.prevX=12;human.y=human.prevY=0;
   yield return Settle();
   foreach(var preset in new[]{"cinematic","balanced","classic","custom"}) {
    reviewChangePending=true;SETTINGS.fxPreset=preset;yield return null;reviewChangePending=false;
    view.vfx.ClearParticles();ParticleBudget.Advancing=true;
    view.OnEvent(new Ev {type="blast",x=human.x+2,y=human.y+1,r=2,level=3,p=human,depth=LevelFeatures.LANE_W});yield return Settle(2);
    var fireRenderer=view.vfx.fire.ps.GetComponent<ParticleSystemRenderer>();
    var sample=new BlastReview {preset=preset,fire=view.vfx.fire.Count,smoke=view.vfx.smoke.Count,sparks=view.vfx.spark.Count,fireInCamera=GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(view.camera),fireRenderer.bounds)};
    File.WriteAllText(Path.Combine(reviewFolder,"blast_"+preset+".json"),JsonUtility.ToJson(sample,true));
    if(FxCfg.Explosions=="volumetric"&&(sample.fire==0||!sample.fireInCamera))review.errors.Add("Modern blast fire missing from camera: "+preset);
    // Freeze the ordinary effect clock at a matched age without drawing the pause menu over it.
    paused=true;yield return Capture("effects_"+preset);paused=false;ParticleBudget.Advancing=true;yield return Settle(16);
   }
   SetPaused(true);yield return Settle(3);yield return Capture("paused");SetPaused(false);
   yield return ReviewEnergy();
   yield return ReviewBackdrop();
   SETTINGS.aiTeammates=3;world.Teleport("skyline");yield return Settle();Directory.CreateDirectory(Path.Combine(reviewFolder,"motion"));
   for(int frame=0;frame<40;frame++){yield return Settle(2);yield return Capture("motion/"+frame.ToString("D3"),false);}
   SETTINGS.aiTeammates=0;SETTINGS.levelHazards=false;world.Teleport("gym");yield return Settle();
   foreach(var type in new[]{"swarmer","shield","sniper","brute","post","turret","drone","mortar","charger","warden","stormcaller"}) {
    world.Teleport("gym");world.enemies.Clear();world.players[0].mercy=0;var enemy=Enemies.CreateEnemy(type,world.players[0].x+4,0);world.enemies.Add(enemy);yield return Settle(6);yield return Capture("enemy_"+type);
   }
   // Reset/settings lifecycle counts are evidence, not an assertion of no leak.
   SETTINGS.levelHazards=true;
   var materialsBeforeResets=new HashSet<int>();foreach(var m in Resources.FindObjectsOfTypeAll<Material>())materialsBeforeResets.Add(m.GetInstanceID());
   for(int i=0;i<10;i++){world.Teleport(i%2==0?"gym":"arena");SETTINGS.enemyModels=i%2==0;yield return Settle(3);review.resetMeshes.Add(Resources.FindObjectsOfTypeAll<Mesh>().Length);review.resetMaterials.Add(Resources.FindObjectsOfTypeAll<Material>().Length);review.resetLights.Add(Resources.FindObjectsOfTypeAll<Light>().Length);}
   // Allow a small bounded difference for active cues; reject the reproduced +38-material/reset leak.
   if(review.resetMaterials[9]-review.resetMaterials[2]>16)review.errors.Add("Reset material count did not stabilize: "+string.Join(",",review.resetMaterials));
   foreach(var m in Resources.FindObjectsOfTypeAll<Material>())if(!materialsBeforeResets.Contains(m.GetInstanceID()))review.retainedMaterialOrigins.Add(TMat.ReviewOrigin(m));
   world.enemies.Clear();SetPaused(true);
   for(int i=0;i<12;i++){world.SwapCharacter(world.players[0],ROSTER[i%4]);yield return Settle(3);review.characterMaterials.Add(Resources.FindObjectsOfTypeAll<Material>().Length);review.characterMeshes.Add(Resources.FindObjectsOfTypeAll<Mesh>().Length);}
   for(int character=0;character<4;character++)if(review.characterMaterials[character+8]-review.characterMaterials[character+4]>4||review.characterMeshes[character+8]-review.characterMeshes[character+4]>4)review.errors.Add("Character resources did not stabilize for "+ROSTER[character]);
   review.cpuMs=PerfOverlay.CpuMs;review.gpuMs=PerfOverlay.GpuMs;review.p95=PerfOverlay.P95;review.drawCalls=PerfOverlay.DrawCalls;review.setPass=PerfOverlay.SetPass;review.triangles=PerfOverlay.Triangles;review.gcBytes=PerfOverlay.GCBytes;review.particles=ParticleBudget.Live;review.particleCap=FxCfg.MaxParticles;review.meshes=Resources.FindObjectsOfTypeAll<Mesh>().Length;review.materials=Resources.FindObjectsOfTypeAll<Material>().Length;review.lights=Resources.FindObjectsOfTypeAll<Light>().Length;
   VerifySettingsPersistence();
   File.WriteAllText(Path.Combine(reviewFolder,"runtime.json"),JsonUtility.ToJson(review,true));
   Debug.Log("NOVA_REVIEW_COMPLETE "+review.captures.Count+" captures, "+review.errors.Count+" errors");Application.Quit(review.errors.Count==0?0:1);
  }

  static bool PersistedReviewSettings() => SETTINGS.fxPreset=="custom"&&SETTINGS.fxExplosions=="classic"&&SETTINGS.fxProjectiles=="energy"&&SETTINGS.fxSmoke=="off"&&SETTINGS.fxTrails=="off"&&SETTINGS.reducedScreenEffects&&SETTINGS.clouds=="volumetric"&&SETTINGS.birds=="realistic";
  void VerifySettingsPersistence() {
   var original=JsonUtility.ToJson(SETTINGS);
   SETTINGS.fxPreset="custom";SETTINGS.fxExplosions="classic";SETTINGS.fxProjectiles="energy";SETTINGS.fxSmoke="off";SETTINGS.fxTrails="off";SETTINGS.reducedScreenEffects=true;SETTINGS.clouds="volumetric";SETTINGS.birds="realistic";
   SettingsStore.Save();SETTINGS=new Settings();SettingsStore.Load();review.settingsRoundtrip=PersistedReviewSettings();if(!review.settingsRoundtrip)review.errors.Add("Settings save/load roundtrip failed");
   var saved=PlayerPrefs.GetString("novaStriker.settings");
   PlayerPrefs.SetString("novaStriker.settings","{\"settingsVersion\":11,\"fxPreset\":\"invalid\",\"fxSmoke\":\"invalid\",\"clouds\":\"invalid\",\"birds\":\"invalid\"}");SettingsStore.Load();
   if(SETTINGS.fxPreset!="cinematic"||SETTINGS.fxSmoke!="rich"||SETTINGS.clouds!="volumetric"||SETTINGS.birds!="realistic")review.errors.Add("Invalid graphics settings were not migrated");
   PlayerPrefs.SetString("novaStriker.settings",saved);PlayerPrefs.Save();SETTINGS=new Settings();JsonUtility.FromJsonOverwrite(original,SETTINGS);
  }

  IEnumerator ReviewBackdrop() {
   SETTINGS.aiTeammates=0;SETTINGS.fxPreset="cinematic";SETTINGS.clouds="volumetric";SETTINGS.quality="high";SETTINGS.levelHazards=false;
   world.Teleport("skyline");world.enemies.Clear();yield return Settle(12);reviewHoldSimulation=true;world.players[0].mercy=0;
   int before=BackdropCloudFeature.RenderedFrames;yield return Settle(2);
   if(!BackdropCloudFeature.Active||BackdropCloudFeature.RenderedFrames<=before)review.errors.Add("Backdrop cloud Render Graph feature did not render");
   review.cloudSteps=BackdropCloudFeature.Steps;yield return Capture("cloud_volume");
   paused=true;yield return Capture("cloud_volume_paused");paused=false;
   SETTINGS.camera="ortho";SETTINGS.fov=50;yield return Settle(2);yield return Capture("cloud_ortho");
   SETTINGS.camera="perspective";SETTINGS.fov=90;yield return Settle(2);yield return Capture("cloud_wide");
   SETTINGS.fxPreset="balanced";yield return Settle(2);if(BackdropCloudFeature.Active)review.errors.Add("Balanced clouds did not select impostors");yield return Capture("cloud_balanced");
   SETTINGS.clouds="off";yield return Settle(2);if(BackdropCloudFeature.Active)review.errors.Add("Cloud Off did not stop volume");yield return Capture("cloud_off");
   SETTINGS.fxPreset="cinematic";SETTINGS.clouds="volumetric";SETTINGS.fov=54;review.cloudFrames=BackdropCloudFeature.RenderedFrames;
   yield return ReviewSurfaces();
   reviewHoldSimulation=false;SETTINGS.levelHazards=true;
  }
  IEnumerator ReviewSurfaces() {
   world.Teleport("gym");yield return Settle(12);var human=world.players[0];human.mercy=0;
   FrameReviewPlayer(human,lead:1,height:1.3f,distance:10);yield return Settle(2);paused=true;
   yield return Capture("projected_decal_before",false);
   view.vfx.decals.Stamp(human.x+1,human.y,2,"scorch",20);yield return Settle(2);review.projectedDecals=view.vfx.decals.ProjectedActive;
   if(review.projectedDecals==0)review.errors.Add("Native URP projected mark did not activate");yield return Capture("projected_decal");
   review.floorMarkPixels=MarkPixels("projected_decal",S.W(human.x+1,human.y,0));paused=false;
   // The gym's real floating wall, using its collision face rather than an approximate art coordinate.
   var panel=Level.BOXES.Find(box=>box.tag=="panel"&&box.x0<60);
   if(panel!=null) {
    human.x=human.prevX=panel.x0-3;human.y=human.prevY=0;FrameReviewPlayer(human,lead:-3,height:2.4f,distance:10);yield return Settle(2);paused=true;
    // Face the collision side directly: the ordinary side-view camera can hide it behind the front cladding.
    var surface=S.W(panel.x0,panel.y0+1.2,0);reviewWallEye=S.W(panel.x0-6,panel.y0+2.5,5);reviewWallTarget=surface;reviewWallCamera=true;
    yield return Settle(2);yield return Capture("projected_wall_decal_before",false);
    view.vfx.decals.StampSurface(surface,S.Dir(panel.x0,-1,0),2,"scorch",20);yield return Settle(2);yield return Capture("projected_wall_decal");
    review.wallMarkPixels=MarkPixels("projected_wall_decal",surface);reviewWallCamera=false;paused=false;
   } else review.errors.Add("Gym wall projection test surface missing");
   review.landmarkLods=view.landmarkLods?.Count??0;if(review.landmarkLods==0)review.errors.Add("Landmark LODs missing");
  }

  // Seed a valid charge state, release through the production simulation, then render the real owner.
  // Hold only simulation during captures; presentation advances, and ordinary pause still freezes both.
  IEnumerator ReviewEnergy() {
   SETTINGS.aiTeammates=0;SETTINGS.levelHazards=false;SETTINGS.fxPreset="cinematic";
   world.Teleport("gym");world.enemies.Clear();yield return Settle();reviewHoldSimulation=true;
   var human=world.players[0];
   foreach(var character in new[]{"nova","ram"}) {
    world.SwapCharacter(human,character);human.x=human.prevX=52;human.y=human.prevY=0;human.aimX=1;human.aimY=0;
    human.state="normal";human.mercy=0;human.onGround=true;human.lane=human.laneTo=human.laneFrom=1;human.laneT=0;
    double readyAt=PlayerSim.BeamSpec(human).at;human.chargeT=0;
    FrameReviewPlayer(human);
    view.vfx.ClearParticles();Directory.CreateDirectory(Path.Combine(reviewFolder,"energy",character));
    // Controlled production charge states; this is presentation evidence, not a controller playthrough.
    for(int frame=0;frame<8;frame++) {human.chargeT=readyAt*(frame+1)/8;yield return Settle(2);yield return Capture("energy/"+character+"/"+frame.ToString("D3"),false);}
    yield return Capture(character+"_beam_charge");
    var viewport=view.camera.WorldToViewportPoint(Th.P(S.W(human.x,human.y+1,LevelFeatures.Depth(human))));
    if(viewport.z<=0||viewport.x<0||viewport.x>1||viewport.y<0||viewport.y>1)review.errors.Add(character+" charge owner is outside the captured camera");
    world.Step(new Dictionary<int,Cmd>{{human.slot,new Cmd{ax=1,ay=0,aimFree=true,released=new Buttons{fire=true}}}});
    foreach(var ev in world.events)view.OnEvent(ev);world.events.Clear();
    if(human.beam==null) {review.errors.Add(character+" production beam release failed");continue;}
    world.BeamTick(human);yield return Settle(6);
    if(EnergyTube.Active<3||view.vfx.energy.Count==0)review.errors.Add(character+" 3D beam/mesh particles missing");
    yield return Capture(character+"_beam_release");
    for(int frame=8;frame<20;frame++) {yield return Settle(2);yield return Capture("energy/"+character+"/"+frame.ToString("D3"),false);}
    paused=true;yield return Capture(character+"_beam_paused");paused=false;
    SETTINGS.fxPreset="classic";yield return Settle(3);yield return Capture(character+"_beam_classic");
    SETTINGS.fxPreset="cinematic";world.EndBeam(human,"review");human.state="normal";yield return Settle(12);
   }
   world.SwapCharacter(human,"nova");human.sub="chain";human.state="normal";human.aimX=1;human.aimY=0;
   for(int lane=-1;lane<=1;lane++) {var enemy=Enemies.CreateEnemy("brute",human.x+3+(lane+1)*2,0);enemy.hp=100;enemy.lane=enemy.laneTo=enemy.laneFrom=lane;world.enemies.Add(enemy);}
   world.FireSub(human,3,true);foreach(var ev in world.events)view.OnEvent(ev);world.events.Clear();
   yield return Settle(2);yield return Capture("chain_lightning");
   paused=true;yield return Capture("chain_lightning_paused");paused=false;
   Directory.CreateDirectory(Path.Combine(reviewFolder,"energy","chain"));
   for(int frame=0;frame<6;frame++) {yield return Settle(2);yield return Capture("energy/chain/"+frame.ToString("D3"),false);}
   yield return Settle(16);yield return Capture("electrified_enemies");
   world.enemies.Clear();human.sub="scatter";human.chargeT=0;human.beam=null;human.lane=human.laneTo=human.laneFrom=0;
   yield return Settle(20);reviewHoldSimulation=false;SETTINGS.levelHazards=true;
  }
 }
}
