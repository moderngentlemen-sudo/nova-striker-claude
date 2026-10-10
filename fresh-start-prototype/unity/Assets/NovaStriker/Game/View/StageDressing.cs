// Blender stage kits; spatial instanced batches share the exported mesh and material.
// Geometry has no gameplay collision. Each 48m cell owns one batch per mesh/material.
using System; using System.Collections.Generic;
using NovaStriker.Game.Three; using UnityEngine; using UnityEngine.Rendering;
using static NovaStriker.Sim.Cfg;
namespace NovaStriker.Game {
 public sealed class StageDressing {
  [Serializable] sealed class Part { public string asset,mat; public float[] p,n,uv; public int[] i; }
  [Serializable] sealed class Place { public string asset; public float x,y,dz,yaw,sx,sy,sz; }
  [Serializable] sealed class Kit {public Part[] parts; public Place[] place;}
  sealed class Batch {public Mesh mesh;public Material mat;public Matrix4x4[] transforms;public Bounds bounds;public bool small;}
  readonly List<Batch> batches=new List<Batch>();
  public StageDressing() {
   foreach(var zone in new[]{"arena","tower","skyline","foundry","undercity"}) {
    var src=Resources.Load<TextAsset>("NovaStriker/Env/"+zone+"_kit");if(src==null)continue;
    Kit kit;try {kit=JsonUtility.FromJson<Kit>(src.text);}catch(Exception e){Debug.LogWarning("Stage kit "+zone+": "+e.Message);continue;}
    if(kit?.parts==null||kit.place==null)continue;
    foreach(var p in kit.parts) {
     if(p.p==null||p.n==null||p.uv==null||p.i==null||p.n.Length!=p.p.Length||p.uv.Length!=p.p.Length/3*2)continue;
     var g=new GeoBuilder();for(int i=0;i<p.p.Length/3;i++)g.V(new Vector3(p.p[i*3],p.p[i*3+1],p.p[i*3+2]),new Vector3(p.n[i*3],p.n[i*3+1],p.n[i*3+2]),new Vector2(p.uv[i*2],p.uv[i*2+1]));
     for(int i=0;i<p.i.Length;i+=3)g.T(p.i[i],p.i[i+1],p.i[i+2]);var mesh=g.ToMesh(zone+"-"+p.asset);mesh.RecalculateTangents();mesh.UploadMeshData(true);
     var mat=GymDressing.MatFor(p.mat).m;mat.enableInstancing=true;
     var cells=new Dictionary<(int,int),List<Matrix4x4>>();
     foreach(var l in kit.place)if(l.asset==p.asset) {
      var pos=Th.P(S.W(l.x,l.y,l.dz));var key=(Mathf.FloorToInt(pos.x/48),Mathf.FloorToInt(pos.z/48));
      if(!cells.TryGetValue(key,out var list))cells[key]=list=new List<Matrix4x4>();
      list.Add(Matrix4x4.TRS(pos,Th.Quat(0,S.YawAt(l.x)+l.yaw*Mathf.Deg2Rad,0),new Vector3(l.sx,l.sy,l.sz)));
     }
     foreach(var list in cells.Values) {
      for(int start=0;start<list.Count;start+=500) {
       var transforms=list.GetRange(start,Mathf.Min(500,list.Count-start)).ToArray();var bounds=new Bounds(transforms[0].GetColumn(3),Vector3.zero);
       foreach(var m in transforms) {var c=m.MultiplyPoint3x4(mesh.bounds.center);var e=mesh.bounds.extents;var ext=new Vector3(Mathf.Abs(m.m00)*e.x+Mathf.Abs(m.m01)*e.y+Mathf.Abs(m.m02)*e.z,Mathf.Abs(m.m10)*e.x+Mathf.Abs(m.m11)*e.y+Mathf.Abs(m.m12)*e.z,Mathf.Abs(m.m20)*e.x+Mathf.Abs(m.m21)*e.y+Mathf.Abs(m.m22)*e.z);bounds.Encapsulate(new Bounds(c,ext*2));}
       batches.Add(new Batch{mesh=mesh,mat=mat,transforms=transforms,bounds=bounds,small=mesh.bounds.size.magnitude<4});
      }
     }
    }
   }
  }
  public void Draw(Camera camera) {
   foreach(var b in batches) {
    float distance=Vector3.Distance(camera.transform.position,b.bounds.ClosestPoint(camera.transform.position));
    if(distance>(SETTINGS.quality=="low"?(b.small?45:120):(b.small?100:260)))continue;
    var rp=new RenderParams(b.mat){worldBounds=b.bounds,shadowCastingMode=distance<65&&SETTINGS.quality!="low"?ShadowCastingMode.On:ShadowCastingMode.Off,receiveShadows=true};
    if(SystemInfo.supportsInstancing)Graphics.RenderMeshInstanced(rp,b.mesh,0,b.transforms,b.transforms.Length);
    else foreach(var m in b.transforms)Graphics.DrawMesh(b.mesh,m,b.mat,0);
   }
  }
 }
}
