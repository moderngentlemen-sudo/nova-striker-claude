// Static landmarks have one spatial LOD owner, independent of the 48m deck bake.
// All source meshes are Geo's parameter caches; no regeneration on zone/quality changes.
using System.Collections.Generic;
using NovaStriker.Game.Three;
using UnityEngine;
using UnityEngine.Rendering;
using static NovaStriker.Sim.Cfg;
namespace NovaStriker.Game {
 public sealed class LandmarkLods {
  readonly TObj scene;readonly List<LODGroup> groups=new List<LODGroup>();bool? low;
  sealed class CityCell {public readonly List<CombineInstance> high=new List<CombineInstance>(),far=new List<CombineInstance>();public readonly List<Mesh> inputs=new List<Mesh>();}
  readonly Dictionary<(int,int),CityCell> city=new Dictionary<(int,int),CityCell>();
  public int Count => groups.Count;
  public LandmarkLods(TObj scene) {this.scene=scene;}
  public void Add(string name,Mesh high,Mesh medium,Mesh distant,TMat material,Vector3 position,Vector3 rotation,bool shadow=false,TMat distantMaterial=null) {
   var root=scene.add(new TObj("landmark "+name));root.position.copy(position);root.rotation.set(rotation.x,rotation.y,rotation.z);
   var lods=new LOD[3];var meshes=new[]{high,medium,distant};
   for(int i=0;i<3;i++) {var mesh=root.add(new TMesh(meshes[i],i==2&&distantMaterial!=null?distantMaterial:material){cast=shadow,receive=true,noOutline=true});lods[i]=new LOD(i==0?.4f:i==1?.12f:.01f,new Renderer[]{mesh.mr});}
   var group=root.go.AddComponent<LODGroup>();group.fadeMode=LODFadeMode.None;group.SetLODs(lods);group.RecalculateBounds();groups.Add(group);group.ForceLOD(SETTINGS.quality=="low"?2:-1);
  }
  // Keep city blocks in the same 48m world cells instead of creating hundreds of draw owners.
  // Close/mid preserve real box silhouettes; distant cells use two-triangle facade impostors.
  public void QueueCity(Mesh ownedBox,float width,float height,Vector3 position,float yaw) {
   var key=(Mathf.FloorToInt(position.x/48),Mathf.FloorToInt(position.z/48));if(!city.TryGetValue(key,out var cell))city[key]=cell=new CityCell();
   var transform=Matrix4x4.TRS(Th.P(position),Th.Quat(0,yaw,0),Vector3.one);
   var facade=Geo.Copy(Geo.Plane(width,height));var uv=facade.uv;for(int i=0;i<uv.Length;i++)uv[i]=new Vector2(uv[i].x*width/6,uv[i].y*height/12);facade.uv=uv;
   cell.high.Add(new CombineInstance{mesh=ownedBox,transform=transform});cell.far.Add(new CombineInstance{mesh=facade,transform=transform});cell.inputs.Add(ownedBox);cell.inputs.Add(facade);
  }
  public void FlushCity(TMat windows) {
   var distant=windows.clone();distant.side=Side.Double;
   foreach(var entry in city) {
    Mesh Merge(List<CombineInstance> parts,string name) {var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(parts.ToArray(),true,true,false);mesh.UploadMeshData(true);return mesh;}
    var close=Merge(entry.Value.high,"city cell");var far=Merge(entry.Value.far,"city facade cell");
    Add("city cell",close,close,far,windows,Vector3.zero,Vector3.zero,distantMaterial:distant);
    // These unique merged meshes belong to this scene, unlike shared Geo cache primitives.
    var owner=groups[groups.Count-1].gameObject.AddComponent<LandmarkMeshOwner>();owner.close=close;owner.far=far;
    foreach(var input in entry.Value.inputs)Object.Destroy(input);
   }
   city.Clear();
  }
  public void ApplyQuality() {bool next=SETTINGS.quality=="low";if(low==next)return;low=next;foreach(var group in groups)group.ForceLOD(next?2:-1);}
 }
 public sealed class LandmarkMeshOwner : MonoBehaviour {public Mesh close,far;void OnDestroy(){if(close!=null)Object.Destroy(close);if(far!=null)Object.Destroy(far);}}
}
