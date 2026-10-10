// Static landmarks have one spatial LOD owner, independent of the 48m deck bake.
// All source meshes are Geo's parameter caches; no regeneration on zone/quality changes.
using System.Collections.Generic;
using NovaStriker.Game.Three;
using UnityEngine;
using static NovaStriker.Sim.Cfg;
namespace NovaStriker.Game {
 public sealed class LandmarkLods {
  readonly TObj scene;readonly List<LODGroup> groups=new List<LODGroup>();bool? low;
  public int Count => groups.Count;
  public LandmarkLods(TObj scene) {this.scene=scene;}
  public void Add(string name,Mesh high,Mesh medium,Mesh distant,TMat material,Vector3 position,Vector3 rotation,bool shadow=false) {
   var root=scene.add(new TObj("landmark "+name));root.position.copy(position);root.rotation.set(rotation.x,rotation.y,rotation.z);
   var lods=new LOD[3];var meshes=new[]{high,medium,distant};
   for(int i=0;i<3;i++) {var mesh=root.add(new TMesh(meshes[i],material){cast=shadow,receive=true,noOutline=true});lods[i]=new LOD(i==0?.4f:i==1?.12f:.01f,new Renderer[]{mesh.mr});}
   var group=root.go.AddComponent<LODGroup>();group.fadeMode=LODFadeMode.None;group.SetLODs(lods);group.RecalculateBounds();groups.Add(group);group.ForceLOD(SETTINGS.quality=="low"?2:-1);
  }
  public void ApplyQuality() {bool next=SETTINGS.quality=="low";if(low==next)return;low=next;foreach(var group in groups)group.ForceLOD(next?2:-1);}
 }
}
