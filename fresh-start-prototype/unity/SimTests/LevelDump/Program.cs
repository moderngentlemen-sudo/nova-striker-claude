// Writes the level's data for the Blender dressing scripts (Art/Blender/zone_layout.py, render_zone.py) to
// Art/Blender/level_boxes.json, so they place pieces from the real boxes instead of hand-copied numbers. It only
// reads the simulation. The level features are applied first (default settings), and their boxes (id >= 10000:
// catwalks tagged "tier", crumbling platforms tagged "collapse"), the hazards and the lane stretches are written in
// arrays of their own: the view draws them (LevelFx), so the dressing keeps clear of them instead of dressing them.
// The path's frame (point, tangent) is sampled every 0.5 m so the preview can bend the curved zones as the game does.
// Run: dotnet run [out-path]
using System; using System.Collections.Generic; using System.Globalization; using System.IO; using System.Linq; using System.Text; using NovaStriker.Sim;
static class P {
  static string F(double v) => Math.Round(v, 4).ToString(CultureInfo.InvariantCulture);
  static string Q(string s) => s == null ? "null" : "\"" + s + "\"";
  static void Main(string[] args) {
    Cfg.SETTINGS = new Settings(); Level.RestoreBoxes();
    string Box(LevelBox b) => $"{{\"id\":{b.id},\"x0\":{F(b.x0)},\"x1\":{F(b.x1)},\"y0\":{F(b.y0)},\"y1\":{F(b.y1)},\"type\":\"{b.type}\",\"tag\":{Q(b.tag)}}}";
    var sb = new StringBuilder("{\n");
    void Arr(string name, IEnumerable<string> rows, bool last = false) =>
      sb.Append($"\"{name}\":[\n  ").Append(string.Join(",\n  ", rows)).Append(last ? "\n]\n" : "\n],\n");
    Arr("boxes", Level.BOXES.Where(b => b.id < Level.EXTRA_ID).Select(Box));
    Arr("extra", Level.BOXES.Where(b => b.id >= Level.EXTRA_ID).Select(Box));
    Arr("zones", Level.ZONES.Select(z => $"{{\"id\":\"{z.id}\",\"name\":\"{z.name}\",\"x0\":{F(z.x0)},\"x1\":{F(z.x1)}}}"));
    Arr("checkpoints", Level.CHECKPOINTS.Select(c => $"[{F(c.x)},{F(c.y)}]"));
    Arr("lanes", Level.LANES.Select(l => $"[{F(l.x0)},{F(l.x1)}]"));
    Arr("hazards", Level.HAZARDS.Select(h => $"{{\"kind\":\"{h.kind}\",\"zone\":\"{h.zone}\",\"x0\":{F(h.x0)},\"x1\":{F(h.x1)},\"y0\":{F(h.y0)},\"y1\":{F(h.y1)}}}"));
    Arr("lifts", Level.LIFTS.Select(l => $"[{F(l.x)},{F(l.y)},{F(l.top)}]"));
    var fr = new List<string>();
    for (double x = -20; x < 1200; x += 0.5) { var f = Level.Frame(x); fr.Add($"[{F(x)},{F(f.px)},{F(f.pz)},{F(f.tx)},{F(f.tz)}]"); }
    Arr("frames", fr, true);
    sb.Append("}\n");
    var path = args.Length > 0 ? args[0] : Path.Combine("..", "..", "Art", "Blender", "level_boxes.json");
    File.WriteAllText(path, sb.ToString());
    Console.WriteLine($"wrote {path}: {Level.BOXES.Count(b => b.id < Level.EXTRA_ID)} boxes, {Level.BOXES.Count(b => b.id >= Level.EXTRA_ID)} extra, {Level.HAZARDS.Count} hazards, {Level.LANES.Count} lanes");
  }
}
