// Conservative tier waypoints from real support intervals and this character's jump capability.
// Search is deterministic. It never teleports and excludes imminent damage volumes.
using System; using System.Collections.Generic;
using static NovaStriker.Sim.Cfg;
namespace NovaStriker.Sim {
 public static class LevelNavigation {
  static LevelBox Support(double x,double y,int lane) {
   LevelBox best=null;foreach(var b in Level.BOXES)if(!b.broken&&b.tag!="bound"&&b.type!='g'&&Level.InLane(b.laneMask,lane)&&x>b.x0&&x<b.x1&&b.y1<=y+.2&&(best==null||b.y1>best.y1))best=b;
   return best;
  }
  public static V2 Waypoint(Player p,double x,double y) {
   var start=Support(p.x,p.y,p.lane);var goal=Support(x,y,p.lane);if(start==null||goal==null||start==goal||y<=p.y+1)return new V2(x,y);
   var c=CHARS[p.@char];double rise=(c.jumpV*c.jumpV+c.dblV*c.dblV)/(2*GRAVITY)-.25;
   double reach=c.run*(2*c.jumpV+2*c.dblV)/GRAVITY*.75;
   var queue=new Queue<LevelBox>();var parent=new Dictionary<LevelBox,LevelBox>();queue.Enqueue(start);parent[start]=null;
   while(queue.Count>0) {
    var a=queue.Dequeue();if(a==goal)break;
    foreach(var b in Level.BOXES) {
     if(parent.ContainsKey(b)||b.broken||b.tag=="bound"||b.type=='g'||!Level.InLane(b.laneMask,p.lane)||b.y1<-900)continue;
     double gap=Math.Max(0,Math.Max(a.x0-b.x1,b.x0-a.x1));if(gap>reach||b.y1-a.y1>rise||a.y1-b.y1>12)continue;
     double take=Math.Max(a.x0+p.w,Math.Min(a.x1-p.w,(b.x0+b.x1)/2));double land=Math.Max(b.x0+p.w,Math.Min(b.x1-p.w,take));
     if(LevelFeatures.Danger(land,b.y1,p.lane)||!Level.HasHeadroom(land,b.y1,p.w,p.h,p.lane))continue;
     if(Level.SegmentBlocked(take,a.y1+p.h+.15,land,b.y1+p.h+.15,p.lane))continue;
     parent[b]=a;queue.Enqueue(b);
    }
   }
   if(!parent.ContainsKey(goal))return new V2(x,y);
   var next=goal;while(parent[next]!=start&&parent[next]!=null)next=parent[next];
   return new V2(Math.Max(next.x0+p.w,Math.Min(next.x1-p.w,p.x)),next.y1);
  }
 }
}
