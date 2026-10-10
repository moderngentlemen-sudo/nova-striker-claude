// Read-only authoritative layout export. Blender never guesses route curves or tier coordinates.
using System; using System.IO; using System.Linq; using System.Collections.Generic; using System.Text.Json; using NovaStriker.Sim;
static class ExportLayout {
 static void Main(string[] args) {
  Cfg.SETTINGS = new Settings { levelTiers = true, levelHazards = true, depthLanes = true };
  var w = new World(); Level.RestoreBoxes();
  var frames = new List<object>();
  for(double x=-12; x<=1190; x+=0.5) {var f=Level.Frame(x); frames.Add(new {x,f.px,f.pz,f.tx,f.tz,f.nx,f.nz});}
  var data = new { schema=1, lanes=Level.LANES.Select(l=>new {l.x0,l.x1}), zones=Level.ZONES.Select(z=>new{z.id,z.x0,z.x1}),
   boxes=Level.BOXES.Select(b=>new{b.id,b.x0,b.x1,b.y0,b.y1,b.type,b.tag,b.laneMask}),
   hazards=Level.HAZARDS.Select(h=>new{h.id,h.kind,h.x0,h.x1,h.y0,h.y1,h.laneMask}),frames };
  File.WriteAllText(args.Length>0?args[0]:"stage_layout.json",JsonSerializer.Serialize(data));
  Console.WriteLine("Exported authoritative layout, "+Level.BOXES.Count+" boxes and "+frames.Count+" route frames");
 }
}
