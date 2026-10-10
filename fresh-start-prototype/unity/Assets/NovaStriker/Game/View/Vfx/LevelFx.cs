// What the level features look like (Sim/LevelFeatures.cs): the extra catwalks, the crumbling platforms, the
// hazards' machinery with their warnings and effects, and guide lines on the floor where depth lanes open. Rebuilt
// whenever the level's boxes change (Level.Version: a zone load with the features switched); the hazards animate from
// the same tick pattern the simulation runs, so what you see is what will hit you. Warnings are amber.
using System.Collections.Generic;
using NovaStriker.Game.Three;
using NovaStriker.Sim;
using UnityEngine;

namespace NovaStriker.Game
{
    public sealed class LevelFx
    {
        const string AMBER = "#ffb02e";
        readonly View view;
        TObj root;
        int built = -1;
        double emissionClock;
        sealed class HazView { public Hazard h; public TObj g; public TMesh part, beam, plate; public TMat glow; public float shake; }
        readonly List<HazView> hazards = new List<HazView>();
        readonly List<(LevelBox b, TObj g)> collapses = new List<(LevelBox, TObj)>();
        readonly TMat metal = TMat.Std(0x55606e, 0.45f, 0.6f), grate = TMat.Std(0x2c333d, 0.6f, 0.5f), pale = TMat.Std(0xc9d1da, 0.5f, 0.3f);
        TMat trim, warn;
        readonly Dictionary<(float,float,float), Mesh> boxCache = new Dictionary<(float,float,float), Mesh>();
        Mesh BoxMesh(float w,float h,float d) { var key=(w,h,d); if (!boxCache.TryGetValue(key,out var m)) boxCache[key]=m=Geo.Box(w,h,d); return m; }

        public LevelFx(View view) { this.view = view; }

        Vector3 W(double x, double y, float dz = 0) => S.W(x, y, dz);
        TMesh Box(TObj into, float w, float h, float d, TMat m, double x, double y, float dz = 0)
        {
            var mesh = new TMesh(BoxMesh(w, h, d), m) { cast = true, receive = true };
            mesh.position.copy(W(x, y, dz)); mesh.rotation.y = S.YawAt(x); into.add(mesh); return mesh;
        }

        void Build()
        {
            if (root != null) {
                var materials = new HashSet<TMat>(); var meshes = new HashSet<Mesh>();
                root.traverse(o => { if (o is TMesh m) { if (m.material != metal && m.material != grate && m.material != pale) materials.Add(m.material); if (!boxCache.ContainsValue(m.geometry)) meshes.Add(m.geometry); } });
                root.destroy(); foreach (var m in materials) if (m != null) Object.Destroy(m.m); foreach (var m in meshes) if (m != null) Object.Destroy(m);
            }
            hazards.Clear(); collapses.Clear();
            root = Group.Make(name: "level features"); view.scene.add(root);
            trim = TMat.Std(0x7fe3ff); trim.emissiveHex = 0x4fd6ff; trim.emissiveIntensity = 2f;
            warn = TMat.Std(0xffb02e); warn.emissiveCss = AMBER; warn.emissiveIntensity = 0.6f;
            // catwalks: a steel grating deck on a slim frame, a glowing edge toward the camera
            foreach (var b in Level.BOXES)
            {
                if (b.id < Level.EXTRA_ID) continue;
                double xm = (b.x0 + b.x1) / 2; float w = (float)(b.x1 - b.x0);
                var g = Group.Make(); root.add(g);
                bool crumble = b.tag == "collapse";
                Box(g, w, 0.22f, View.DepthFor(b), crumble ? pale : grate, xm, b.y1 - 0.11f);
                Box(g, w, 0.06f, 0.06f, crumble ? warn : trim, xm, b.y1 - 0.25f, View.DepthFor(b) / 2);
                Box(g, w * 0.96f, 0.12f, 0.12f, metal, xm, b.y1 - 0.3f, -1.1f);
                if (crumble) collapses.Add((b, g));
            }
            // hazards
            foreach (var h in Level.HAZARDS)
            {
                if (h.kind == "collapse") continue;
                var hv = new HazView { h = h, g = Group.Make() }; root.add(hv.g);
                double xm = (h.x0 + h.x1) / 2; float w = (float)(h.x1 - h.x0);
                hv.glow = TMat.Std(0x222222, 0.4f); hv.glow.emissiveCss = AMBER; hv.glow.emissiveIntensity = 0;
                switch (h.kind)
                {
                    case "vent":
                        Box(hv.g, w + 0.3f, 0.12f, 4.8f, metal, xm, h.y0 + 0.02f);
                        for (int k = 0; k < 4; k++) Box(hv.g, w - 0.2f, 0.04f, 0.18f, hv.glow, xm, h.y0 + 0.09f, -0.75f + k * 0.5f);
                        break;
                    case "shock":
                        hv.glow.emissiveCss = "#7fd7ff";
                        hv.plate = Box(hv.g, w, 0.05f, 4.8f, hv.glow, xm, h.y0 + 0.03f);
                        Box(hv.g, w + 0.2f, 0.04f, 3.4f, grate, xm, h.y0 + 0.005f);
                        break;
                    case "laser":
                    {
                        float yc = (float)(h.y0 + h.y1) / 2;
                        foreach (var dz in new[] { -2.5f, 2.5f }) { Box(hv.g, 0.3f, (float)h.y1 + 0.3f - (float)h.y0 + 0.4f, 0.3f, metal, xm, (h.y0 + h.y1) / 2 - 0.1, dz); Box(hv.g, 0.34f, 0.12f, 0.34f, hv.glow, xm, yc, dz); }
                        var beamMat = new TMat { colorCss = "#ff4a6a", emissiveCss = "#ff2e5a", emissiveIntensity = 4, transparent = true, opacity = 0.85f, depthWrite = false };
                        hv.beam = Box(hv.g, 0.08f, (float)(h.y1 - h.y0), 4.8f, beamMat, xm, yc); hv.beam.cast = false;
                        break;
                    }
                    case "crusher":
                        hv.part = Box(hv.g, w, 1.2f, 4.8f, metal, xm, h.y1);
                        Box(hv.g, 0.5f, 6, 0.5f, grate, xm, h.y1 + 3.6f);
                        Box(hv.g, w + 0.4f, 0.06f, 4.8f, hv.glow, xm, h.y0 + 0.02f);   // the warning plate it lands on
                        break;
                    case "slag":
                    {
                        var slag = TMat.Std(0xff6a1a, 0.3f); slag.emissiveHex = 0xff5a10; slag.emissiveIntensity = 2.6f;
                        hv.plate = Box(hv.g, w, 0.08f, 4.8f, slag, xm, h.y0 + 0.04f);
                        Box(hv.g, w + 0.4f, 0.2f, 3.4f, grate, xm, h.y0 - 0.05f);
                        break;
                    }
                    case "lightning":
                        Box(hv.g, w, 0.04f, 4.8f, hv.glow, xm, h.y0 + 0.02f);
                        Box(hv.g, 0.12f, 3.2f, 0.12f, metal, xm, h.y0 + 1.6f, -2.2f);   // a lightning rod at the back
                        break;
                    case "debris":
                        Box(hv.g, w, 0.04f, 4.8f, hv.glow, xm, h.y0 + 0.02f);
                        hv.part = Box(hv.g, w * 0.85f, 0.7f, 4.8f, metal, xm, h.y1);
                        Box(hv.g, w + 0.6f, 0.4f, 2.6f, grate, xm, h.y1 + 0.2f);       // the loose ceiling grid above
                        break;
                    case "wind":
                        foreach (var x in new[] { h.x0, h.x1 }) Box(hv.g, 0.2f, 2.4f, 0.2f, metal, x, h.y0 + 1.2f, -2.2f);   // wind socks on posts at each end
                        break;
                }
                hazards.Add(hv);
            }
            // lane guides: glowing strips between the lanes along each stretch
            var guide = new TMat { colorCss = "#9fe7ff", emissiveCss = "#7fdcff", emissiveIntensity = 1.4f, transparent = true, opacity = 0.55f, depthWrite = false };
            foreach (var (x0, x1) in Level.LANES)
                for (double x = x0; x < x1; x += 1.5)
                {
                    foreach (var b in Level.BOXES) {
                        if ((b.type != 's' && b.type != 'o') || b.laneMask != 7 || x < b.x0 || x + 0.9 > b.x1) continue;
                        TObj owner = root;
                        foreach (var c in collapses) if (c.b == b) { owner = c.g; break; }
                        foreach (var dz in new[] { -0.7f, 0.7f }) { var s = Box(owner, 0.9f, 0.012f, 0.06f, guide, x + 0.45, b.y1 + 0.012, dz); s.cast = false; s.receive = false; }
                    }
                }
            built = Level.Version;
        }

        public void OnEvent(Ev ev)
        {
            var V = view.vfx; if (V == null) return;
            switch (ev.type)
            {
                case "laneBlocked": view.fx.PopText(ev.x, ev.y + 1.8, "LANE BLOCKED", AMBER, 0.65f); break;
                case "laneHop":
                {
                    var b = (Body)ev.owner; if (b == null) break;
                    var c = new Color(0.85f, 0.95f, 1f, 0.6f);
                    for (int i = 0; i < 6; i++) V.smoke.Emit(W(ev.x, ev.y + 0.1, (float)LevelFeatures.Depth(b)), Random.insideUnitSphere * 1.2f + Vector3.up * 0.4f, 0.4f, 0.5f, c, Random.value * 6);
                    for (int i = 0; i < 4; i++) V.streak.Emit(W(ev.x, ev.y + 1, (float)LevelFeatures.Depth(b)), new Vector3(0, 0, (float)(ev.n * 6)) + Random.insideUnitSphere, 0.06f, 0.15f, new Color(2, 2.4f, 3, 1));
                    break;
                }
                case "hazardHit": { double previous=view.fx.effectDepth; view.fx.effectDepth=ev.depth??0; view.fx.Impact(ev.x, ev.y, ev.kind == "shock" ? "#9fe7ff" : ev.kind == "slag" ? "#ff8a3a" : AMBER, 1.1f); view.fx.effectDepth=previous; break; }
                case "hazardOn":
                    if (ev.kind == "lightning")
                    {
                        var at = W(ev.x, ev.y + 0.2);
                        for (int i = 0; i < 16; i++) V.streak.Emit(W(ev.x + (Random.value - 0.5f) * 0.6f, ev.y + 1 + i, -0.6f), Vector3.down * 40, 0.25f, 0.12f, new Color(3, 3.4f, 4, 1));
                        V.lights.Flash(at + Vector3.up * 4, new Color(0.75f, 0.85f, 1f), 14, 30, 0.35f);
                        view.fx.Blast(ev.x, ev.y + 0.2, 1.2f, "#cfe6ff", 1, "plasma"); V.decals.Stamp(ev.x, ev.y, 1.8f, "scorch", 8);
                    }
                    break;
            }
        }

        public void Update(float dt, World world, float t)
        {
            if (built != Level.Version) Build();
            var V = view.vfx;
            emissionClock += dt * 60;
            int steps = Mathf.Min(8, (int)emissionClock); emissionClock -= steps;
            bool advancing = ParticleBudget.Advancing;
            try {
                if(steps == 0) ParticleBudget.Advancing = false;
                for(int sample=0;sample<Mathf.Max(1,steps);sample++) {
            foreach (var (b, g) in collapses) g.visible = b.y1 > -500;
            foreach (var hv in hazards)
            {
                var h = hv.h; double xm = (h.x0 + h.x1) / 2; float w = (float)(h.x1 - h.x0);
                bool warning = h.state == "warn", on = h.state == "on";
                hv.glow.emissiveIntensity = on ? 3 : warning ? 0.8f + 0.8f * Mathf.Sin(t * 22) : 0.15f;
                switch (h.kind)
                {
                    case "vent":
                        if (warning && Random.value < 0.4f) V.smoke.Emit(W(xm + (Random.value - 0.5f) * w, h.y0 + 0.2, (Random.value - 0.5f) * 1.6f), Vector3.up * 1.5f, 0.5f, 0.6f, new Color(0.95f, 0.95f, 0.95f, 0.4f), Random.value * 6);
                        if (on) for (int i = 0; i < 3; i++) V.smoke.Emit(W(xm + (Random.value - 0.5f) * w, h.y0 + 0.3, (Random.value - 0.5f) * 1.6f), Vector3.up * (10 + Random.value * 5), 0.7f, 0.6f, new Color(1, 1, 1, 0.75f), Random.value * 6);
                        break;
                    case "shock":
                        hv.glow.emissiveIntensity = on ? 2.5f + 2.5f * Mathf.PerlinNoise(t * 30, (float)xm) : warning ? (Mathf.Sin(t * 30) > 0 ? 1.4f : 0.2f) : 0.3f;
                        if (on && Random.value < 0.6f) V.spark.Emit(W(xm + (Random.value - 0.5f) * w, h.y0 + 0.08, (Random.value - 0.5f) * 3), Random.insideUnitSphere * 4 + Vector3.up * 2, 0.04f, 0.2f, new Color(1.5f, 2.8f, 4, 1));
                        break;
                    case "laser":
                        hv.beam.visible = on || (warning && Mathf.Sin(t * 26) > 0.3f);
                        hv.beam.material.opacity = on ? 0.9f : 0.25f;
                        break;
                    case "crusher":
                    {
                        // down fast while on, back up through the idle stretch
                        double c = ((long)world.tick + h.phase) % h.period; float up;
                        if (on) up = 1 - Mathf.Clamp01((float)(c - (h.period - h.on)) / Mathf.Max(1, h.on * 0.65f)); else if (warning) up = 1; else { float k = (float)(c / System.Math.Max(1, h.period - h.warn - h.on)); up = Mathf.Clamp01(k * 1.4f); }
                        float jitter = warning ? Mathf.Sin((float)world.tick * 2.41f) * 0.02f : 0;
                        hv.part.position.copy(W(xm + jitter, h.y0 + 0.6 + up * (h.y1 - h.y0 - 0.6), 0));
                        break;
                    }
                    case "slag":
                        if (Random.value < 0.3f) V.ember.Emit(W(xm + (Random.value - 0.5f) * w, h.y0 + 0.1, (Random.value - 0.5f) * 2.6f), Vector3.up * (1 + Random.value * 2), 0.06f, 1.2f, new Color(3, 1.2f, 0.4f, 1));
                        if (FxCfg.Distortion && Random.value < 0.08f) V.haze.Emit(W(xm, h.y0 + 0.8), Vector3.up * 0.5f, w * 1.2f, 1, new Color(1, 1, 1, 0.5f));
                        break;
                    case "lightning":
                        if (warning && Random.value < 0.25f) V.glow.Emit(W(xm, h.y0 + 0.1, -0.6f), Vector3.zero, w * 1.6f, 0.15f, new Color(1.2f, 0.8f, 0.3f, 1));
                        break;
                    case "debris":
                        hv.part.visible = on;
                        if (on) { double c = ((long)world.tick + h.phase) % h.period; float drop = Mathf.Clamp01((float)(c-(h.period-h.on))/Mathf.Max(1,h.on*0.65f)); hv.part.position.copy(W(xm, h.y1+(h.y0+0.35-h.y1)*drop)); }
                        if (warning && Random.value < 0.4f) V.smokeDark.Emit(W(xm + (Random.value - 0.5f) * w, h.y1, (Random.value - 0.5f) * 1.6f), Vector3.down * 2, 0.25f, 1.2f, new Color(1, 1, 1, 0.5f));
                        if (on && Random.value < 0.7f) V.debris.Emit3D(W(xm + (Random.value - 0.5f) * w, h.y1, (Random.value - 0.5f) * 1.2f), Vector3.down * 14, Vector3.one * (0.15f + Random.value * 0.2f), 1.5f, new Color(0.6f, 0.62f, 0.66f, 1), Random.insideUnitSphere * 3, Random.insideUnitSphere * 8);
                        break;
                    case "wind":
                        if ((on && Random.value < 0.8f) || (warning && Random.value < 0.15f))
                            V.streak.Emit(W(h.dir > 0 ? h.x0 : h.x1, h.y0 + 0.5 + Random.value * (h.y1 - h.y0 - 0.5), (Random.value - 0.5f) * 4), S.Dir(xm, h.dir * 14, 0), 0.04f, 0.9f, new Color(1, 1, 1, 0.35f));
                        break;
                }
            }
            // crumbling platforms shake and shed dust before they go
            foreach (var hz in Level.HAZARDS)
                if (hz.kind == "collapse" && hz.state == "warn" && Random.value < 0.5f)
                    V.smokeDark.Emit(W(hz.x0 + Random.value * (hz.x1 - hz.x0), hz.y0, (Random.value - 0.5f) * 2), Vector3.down, 0.3f, 0.8f, new Color(1, 1, 1, 0.5f));
                }
            } finally { ParticleBudget.Advancing = advancing; }
        }
    }
}
