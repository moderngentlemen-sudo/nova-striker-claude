// The cinematic effects (Settings › Effects; FxCfg): every blast, impact, muzzle flash and projectile drawn with the
// particle layers, lights and floor marks of Vfx. OnEvent runs before the classic reactions: for the purely visual
// ones (deaths, blasts, slams) it replaces them; for those with side effects (craters, charge releases) it adds on
// top. Explosions come in three styles: Volumetric (flash, fireball, lit smoke, embers, a shock ring, debris and a
// scorch mark), Plasma (an implosion that bursts into rings and motes) and Stylised (crisp two-tone toon puffs).
using System.Collections.Generic;
using System.Linq;
using NovaStriker.Game.Three;
using NovaStriker.Sim;
using UnityEngine;
using static NovaStriker.Sim.Cfg;

namespace NovaStriker.Game
{
    public sealed partial class Fx
    {
        Vfx V => view?.vfx;
        static float Amt => FxCfg.Amount;
        static int Count(float n) => Mathf.Max(1, Mathf.RoundToInt(n * Amt));
        static Vector3 Rnd3() => Random.insideUnitSphere;
        static Color Hdr(string css, float k) { var c = S.Lin(css) * k; c.a = 1; return c; }
        static Color Hdr(Color lin, float k) { var c = lin * k; c.a = 1; return c; }

        static readonly HashSet<string> blastEvents = new HashSet<string> { "kill", "enemyBlast", "blast", "splash", "bossSlam", "armorBreak", "chargeCrash", "breachBlast", "rocketJump", "poundLand", "ramSlam", "quake", "cluster", "bossDown", "boxBreak", "frag", "rivetBlast" };
        // ---- Events ----
        // True when the classic reaction should be skipped
        bool CineEvent(Ev ev, World world)
        {
            if (V == null || (FxCfg.Classic && FxCfg.Projectiles == "classic")) return false;
            bool classicBlast = FxCfg.Explosions == "classic";
            if (classicBlast && blastEvents.Contains(ev.type)) return false;
            string own = ev.owner is Player op ? CHARS[op.@char].energy : ev.p != null ? CHARS[ev.p.@char].energy : HOSTILE;
            switch (ev.type)
            {
                // replaced
                case "kill": if (ev.e != null && ev.e.boss) return true; Shatter(ev.x, ev.y, ev.e); return true;
                case "frag": case "rivetBlast": Blast(ev.x, ev.y, Or(ev.r, 1.5), own, 1); return true;
                case "enemyBlast": Blast(ev.x, ev.y + 0.3, Or(ev.r, 1.8), HOSTILE, 1); return true;
                case "blast": Blast(ev.x, ev.y, Or(ev.r, 1.5), ATTACH_LOOK["arc"].tint, 0.7f + 0.15f * Or(ev.level, 1)); return true;
                case "splash": if (ev.r > 1.2) Blast(ev.x, ev.y, F(ev.r) * 0.75f, NOVA_GOLD, 0.45f); else Impact(ev.x, ev.y, NOVA_GOLD, 0.7f); return true;
                case "bossSlam": Slam(ev.x, ev.y, ev.big ? 1.3f : 0.8f, HOSTILE); return true;
                case "armorBreak": Shards(ev.x, ev.y + 0.4, 14, new Color(0.92f, 0.93f, 0.96f), 7); Flash(ev.x, ev.y + 0.4, HOSTILE, 1.4f, 0.12f); return true;
                case "chargeCrash": { var e = ev.e; double x = ev.x + e.facing * 0.8; Debris(x, ev.y + 0.9, 10, new Color(0.85f, 0.87f, 0.9f), 6); Flash(x, ev.y + 0.9, "#ffffff", 1.4f, 0.1f); DustWave(x, ev.y, 0.7f); return true; }
                // added on top of the classic reaction
                case "hit":
                {
                    if (FxCfg.Projectiles == "classic") break;
                    float d = Or(ev.dmg, 1);
                    Impact(ev.x, ev.y, own, (ev.heavy ? 1.3f : 0.7f) + Mathf.Min(1, d * 0.08f));
                    if (ev.heavy || d >= 6) V.lights.Flash(W(ev.x, ev.y, 0.3), Hdr(own, 1), 3, 4, 0.18f);
                    break;
                }
                case "playerHit": Impact(ev.x, ev.y, "#ffffff", 0.8f); return true;
                case "projWall": case "ricochet": case "rivetStick": SurfaceImpact(ev); return true;
                case "deflect": case "blocked": case "plateHit": Impact(ev.x, ev.y, "#dff2ff", 0.6f); return true;
                case "guardBreak": Shards(ev.x, ev.y, 10, new Color(1, 0.6f, 0.8f), 6); Flash(ev.x, ev.y, "#ffffff", 1.6f, 0.12f); break;
                case "shot": Muzzle(ev, own); break;
                case "tracer": Muzzle(ev, ECHO_ORANGE); return true;
                case "enemyShot": { var e=ev.e; if(e!=null) {var shot=new Ev{x=e.x+e.facing*e.w*0.55,y=e.y+e.h*0.65,cannon=ev.heavy};Muzzle(shot,HOSTILE);} return true; }
                case "breachBlast": Blast(ev.x, ev.y, Or(ev.r, 1.6), CHARS["ram"].energy, 0.9f); break;
                case "rocketJump": Blast(ev.x, ev.y, 1.3f, CHARS["fix"].energy, 0.6f); break;
                case "poundLand": Slam(ev.x, ev.y, 0.6f + 0.25f * Or(ev.level, 1), own); break;
                case "ramSlam": Slam(ev.x, ev.y, 1.2f, CHARS["ram"].energy); break;
                case "quake": Slam(ev.x, ev.y, 0.7f, CHARS["ram"].energy); break;
                case "kineticRelease": Blast(ev.x, ev.y, Or(ev.r, 2.5), CHARS["ram"].energy, 1, "plasma"); break;
                case "rampartBreak": Shards(ev.x, ev.y + 0.8, 20, S.Lin(CHARS["ram"].energy) * 1.5f, 8); Flash(ev.x, ev.y + 0.8, CHARS["ram"].energy, 2, 0.15f); break;
                case "cluster": Blast(ev.x, ev.y, 1.2f, SUB_LOOK["grenade"].tint, 0.6f); break;
                case "sentryRocket": Muzzle(ev, "#ffcf5a"); break;
                case "bossDown": Blast(ev.x, ev.y, 4.5f, HOSTILE, 2); Shatter(ev.x, ev.y, ev.e, 2.5f); break;
                case "ultFinisher": case "teamFinisher": Blast(ev.x, ev.y, 3.5f, own, 1.5f, "plasma"); break;
                case "boxBreak": BoxDebris(ev); break;
                case "land":
                    if (ev.p != null && (ev.hard || ev.fall > 6)) V.decals.Stamp(ev.p.x, ev.p.y, 1.1f, "scuff", 6, 0.6f, (float)effectDepth, Random.value * 6.3f);
                    break;
                case "slide": if (ev.p != null && Random.value < 0.3f) V.decals.Stamp(ev.p.x, ev.p.y, 0.9f, "scuff", 5, 0.45f, (float)effectDepth, Random.value * 0.4f - 0.2f); break;
            }
            return false;
        }

        // ---- Building blocks ----
        public double effectDepth;
        // A value scope keeps sustained owners on the same origin contract as immutable event snapshots.
        public DepthScope AtDepth(double depth) => new DepthScope(this, depth);
        public readonly struct DepthScope : System.IDisposable
        {
            readonly Fx fx; readonly double previous;
            public DepthScope(Fx fx, double depth) { this.fx = fx; previous = fx.effectDepth; fx.effectDepth = depth; }
            public void Dispose() { fx.effectDepth = previous; }
        }
        public Vector3 W(double x, double y, double depth = 0) => S.W(x, y, effectDepth + depth);

        void Flash(double x, double y, string css, float size, float life) => V.flash.Emit(W(x, y, 0.4f), Vector3.zero, size, life, Hdr(css, 4));

        // A hit or a ricochet: a star flash, a fan of sparks, a puff of glow
        public void Impact(double x, double y, string css, float k = 1, Vector3? normal = null)
        {
            if (V == null) return;
            var c = S.Lin(css);
            V.flash.Emit(W(x, y, 0.4f), Vector3.zero, 0.9f * k, 0.08f, Hdr(c, 5));
            V.glow.Emit(W(x, y, 0.3f), Vector3.zero, 1.1f * k, 0.18f, Hdr(c, 1.6f));
            int n = Count(8 * k);
            for (int i = 0; i < n; i++) {
                var direction = Rnd3();
                if (normal.HasValue) { var axis = normal.Value.normalized; if (Vector3.Dot(direction, axis) < 0) direction = -direction; direction = (direction + axis * .7f).normalized; }
                V.spark.Emit(W(x, y, .3f), direction * (6 + 5 * k) + Vector3.up * 2, .05f + .03f * k, .25f + Random.value * .35f, Hdr(c, 3 + Random.value * 2));
            }
        }

        void SurfaceImpact(Ev ev)
        {
            var normal = S.Dir(ev.x, ev.nx, ev.ny); bool known = normal.sqrMagnitude > .001f;
            if (known) normal.Normalize();
            string tint = ev.pr != null ? TrailColor(ev.pr) : "#ffffff";
            Impact(ev.x, ev.y, tint, .6f, known ? normal : (Vector3?)null);
            if (!known || ev.box == null) return;
            // Small contact chips differ from a destroyed object; all use the existing decorative budget.
            if (FxCfg.Debris != "off") {
                var color = ev.surface == "glass" ? new Color(.72f,.9f,1) : ev.surface == "crate" ? new Color(.5f,.32f,.18f) : new Color(.58f,.6f,.65f);
                for (int i = 0; i < Count(3); i++) {
                    var scatter = Rnd3(); if (Vector3.Dot(scatter, normal) < 0) scatter = -scatter;
                    var velocity = (normal + scatter).normalized * (2 + Random.value * 4) + Vector3.up;
                    float size = .04f + Random.value * .04f;
                    V.debris.Emit3D(W(ev.x,ev.y,.05f),velocity,new Vector3(size,size*.5f,size),.6f+Random.value*.4f,color,Rnd3()*3,Rnd3()*8);
                }
            }
            bool abrasion = SOLID.Contains(ev.pr?.kind ?? "") || ev.surface == "glass" || ev.surface == "crate";
            V.decals.StampSurface(W(ev.x,ev.y),normal,.55f,abrasion?"scuff":"scorch",6,.7f);
        }

        void Muzzle(Ev ev, string css)
        {
            float big = ev.cannon || ev.level > 1 ? 1.6f : ev.rivet ? 0.5f : 0.9f;
            var at = W(ev.x, ev.y, 0.3f);
            V.flash.Emit(at, Vector3.zero, 0.8f * big, 0.06f, Hdr(css, 5));
            V.glow.Emit(at, Vector3.zero, 1.2f * big, 0.1f, Hdr(css, 1.4f));
            if (big > 1) { V.lights.Flash(at, Hdr(css, 1), 2.5f * big, 3.5f * big, 0.1f); if (FxCfg.Smoke != "off") for (int i = 0; i < Count(3); i++) V.smokeDark.Emit(at, Rnd3() * 0.8f + Vector3.up * 0.6f, 0.6f, 0.9f, new Color(1, 1, 1, 0.5f)); }
        }

        // A blast of radius r in a colour; power scales everything. The style follows the settings unless forced.
        public void Blast(double x, double y, float r, string css, float power = 1, string style = null)
        {
            if (V == null) return;
            style ??= FxCfg.Explosions;
            if (style == "classic") style = "volumetric";
            var c = S.Lin(css); var at = W(x, y, 0.2f);
            float p = power;
            // the flash and its light
            V.flash.Emit(at, Vector3.zero, r * 2.6f, 0.07f, Hdr(c * 0.5f + Color.white * 0.5f, 6));
            V.lights.Flash(at, Hdr(Color.Lerp(c, new Color(1, 0.75f, 0.45f), style == "volumetric" ? 0.6f : 0.1f), 1), 6 * p, r * 4.5f, 0.3f + 0.1f * p);
            if (style == "plasma") PlasmaBurst(at, r, c, p);
            else if (style == "stylised") ToonBurst(at, r, c, p);
            else FireBurst(at, r, c, p);
            // a ring of heat that bends the picture as it spreads
            if (FxCfg.Distortion) V.haze.Emit(at, Vector3.zero, r * 4, 0.4f, new Color(1, 1, 1, Mathf.Min(1, 0.5f + 0.25f * p)));
            // shock ring along the floor, dust both ways, and a scorch mark
            GroundRing(x, y, Color.white, 0.3f, r * 1.9f, 0.32f, 0.7f);
            GroundRing(x, y, c, 0.2f, r * 1.5f, 0.28f, 0.85f);
            if (FloorUnder(x, y, 1.2) != null)
            {
                DustWave(x, y, Mathf.Min(1.4f, 0.4f + r * 0.25f));
                if (style != "plasma") V.decals.Stamp(x, y, r * 1.3f, "scorch", 10, 0.85f, (float)effectDepth, Random.value * 6.3f);
            }
            if (FxCfg.Debris != "off" && style != "plasma") Debris(x, y + 0.2, 4 + r * 3 * p, new Color(0.5f, 0.52f, 0.56f), 5 + r * 2);
            view.Kick(Mathf.Min(1.2f, 0.25f + 0.2f * r * p));
            view.Startle(Mathf.Min(1, 0.3f + r * 0.2f));
        }

        void FireBurst(Vector3 at, float r, Color c, float p)
        {
            int nf = Count(6 + 4 * r * p);
            for (int i = 0; i < nf; i++)
            {
                var d = Rnd3(); d.y = Mathf.Abs(d.y) * 0.8f + 0.2f;
                V.fire.Emit(at + d * r * 0.25f, d * (2 + 2.5f * r), r * (0.9f + Random.value * 0.7f), 0.5f + Random.value * 0.35f, Hdr(Color.Lerp(new Color(1, 0.8f, 0.55f), c, 0.25f), 2.2f), Random.value * 6.3f);
            }
            if (FxCfg.Smoke != "off")
            {
                int ns = Count((FxCfg.Smoke == "rich" ? 8 : 4) + 4 * r * p);
                for (int i = 0; i < ns; i++)
                {
                    var d = Rnd3(); d.y = Mathf.Abs(d.y) + 0.3f;
                    var tint = Color.Lerp(new Color(0.42f, 0.41f, 0.4f), Color.white * 0.75f, Random.value * 0.4f); tint.a = 0.85f;
                    V.smoke.Emit(at + d * r * 0.3f, d * (0.8f + r * 0.6f), r * (1.2f + Random.value * 0.8f), 1.8f + Random.value * 1.8f, tint, Random.value * 6.3f, (Random.value - 0.5f) * 0.6f);
                }
            }
            int ne = Count(10 + 8 * r * p);
            for (int i = 0; i < ne; i++)
            {
                var d = Rnd3(); d.y = Mathf.Abs(d.y) * 1.2f + 0.2f;
                V.ember.Emit(at, d * (4 + 6 * Random.value) * (0.6f + 0.25f * r), 0.06f + Random.value * 0.06f, 0.7f + Random.value * 1.1f, Hdr(new Color(1, 0.65f, 0.3f), 3));
                if (i % 2 == 0) V.spark.Emit(at, d * (8 + 8 * Random.value), 0.05f, 0.4f + Random.value * 0.4f, Hdr(new Color(1, 0.85f, 0.6f), 4));
            }
            V.glow.Emit(at, Vector3.zero, r * 3.2f, 0.35f, Hdr(Color.Lerp(c, new Color(1, 0.6f, 0.3f), 0.5f), 1.2f));
        }

        void PlasmaBurst(Vector3 at, float r, Color c, float p)
        {
            int n = Count(18 + 10 * r * p);
            for (int i = 0; i < n; i++)
            {
                var d = Random.onUnitSphere;
                V.mote.Emit(at + d * r * 1.4f, -d * r * 7, 0.12f, 0.16f, Hdr(c, 4));            // drawn in...
                V.mote.Emit(at, d * (5 + 6 * r) * (0.7f + 0.6f * Random.value), 0.1f + 0.06f * Random.value, 0.5f + Random.value * 0.5f, Hdr(c, 3));   // ...and thrown out
            }
            V.glow.Emit(at, Vector3.zero, r * 3.6f, 0.3f, Hdr(c, 2.5f));
            V.flash.Emit(at, Vector3.zero, r * 1.8f, 0.22f, Hdr(c, 3));
            int ns = Count(8 + 6 * r);
            for (int i = 0; i < ns; i++) V.spark.Emit(at, Random.onUnitSphere * (10 + 6 * r), 0.06f, 0.35f, Hdr(c, 4));
        }

        void ToonBurst(Vector3 at, float r, Color c, float p)
        {
            int n = Count(10 + 6 * r * p);
            var warm = Color.Lerp(new Color(1, 0.86f, 0.5f), c, 0.3f);
            for (int i = 0; i < n; i++)
            {
                var d = Rnd3(); d.y = Mathf.Abs(d.y) * 0.9f + 0.15f;
                var col = i % 3 == 0 ? Color.white : warm; col.a = 1;
                V.toon.Emit(at + d * r * 0.3f, d * (2.5f + 3 * r), r * (0.9f + 0.6f * Random.value), 0.45f + Random.value * 0.25f, col);
            }
            int ns = Count(8 + 4 * r);
            for (int i = 0; i < ns; i++) V.spark.Emit(at, Rnd3() * (7 + 5 * r) + Vector3.up * 2, 0.07f, 0.4f, Hdr(warm, 3));
        }

        // A heavy landing or slam: a blast low to the floor, a wave of dust both ways and thrown debris
        void Slam(double x, double y, float k, string css)
        {
            Blast(x, y + 0.2, 1.4f * k, css, 0.8f * k, "plasma");
            DustWave(x, y, 0.8f + 0.4f * k);
            Debris(x, y + 0.3, 8 * k, new Color(0.55f, 0.57f, 0.6f), 7 * k);
        }

        void DustWave(double x, double y, float k)
        {
            if (FxCfg.Smoke == "off") return;
            int n = Count(10 * k);
            for (int i = 0; i < n; i++)
            {
                float side = i % 2 == 0 ? 1 : -1;
                var v = S.Dir(x, side * (3 + Random.value * 5) * k, 0.4f + Random.value * 0.8f);
                var tint = new Color(0.8f, 0.78f, 0.74f, 0.6f);
                V.smoke.Emit(W(x + side * Random.value * 0.6f, y + 0.15f, (Random.value - 0.5f) * 1.6f), v, 0.6f + 0.6f * k * Random.value, 1.0f + Random.value * 0.8f, tint, Random.value * 6.3f);
            }
        }

        // Chunks thrown out that bounce off the level (physics) or a few quick bits (simple)
        void Debris(double x, double y, float n, Color c, float speed)
        {
            if (FxCfg.Debris == "off") return;
            if (FxCfg.Debris == "simple") { Burst(x, y, "#9aa3ad", Mathf.Min(14, n), speed, 0.25f, 0.5f, grav: 14); return; }
            int m = Count(n);
            for (int i = 0; i < m; i++)
            {
                var d = Rnd3(); d.y = Mathf.Abs(d.y) + 0.4f;
                float s = 0.08f + Random.value * 0.14f;
                var col = c * (0.8f + 0.4f * Random.value); col.a = 1;
                V.debris.Emit3D(W(x, y, (Random.value - 0.5f) * 0.6f), d * speed * (0.5f + Random.value * 0.7f), new Vector3(s, s * (0.6f + Random.value), s), 1.6f + Random.value * 1.2f, col,
                    Rnd3() * 3, Rnd3() * 12);
            }
        }

        void Shards(double x, double y, float n, Color c, float speed)
        {
            if (FxCfg.Debris == "off") return;
            int m = Count(n);
            for (int i = 0; i < m; i++)
            {
                var d = Rnd3(); d.y = Mathf.Abs(d.y) * 0.8f + 0.3f;
                float s = 0.1f + Random.value * 0.16f;
                V.shard.Emit3D(W(x, y, (Random.value - 0.5f) * 0.5f), d * speed * (0.5f + Random.value * 0.8f), new Vector3(s, s, s), 1.3f + Random.value, c, Rnd3() * 3, Rnd3() * 15);
            }
        }

        // An enemy construct breaks apart: ceramic shards, its magenta core flaring and collapsing, a puff of smoke
        void Shatter(double x, double y, Enemy e, float scale = 1)
        {
            float k = (e == null ? 1 : e.boss ? 2.2f : e.type == "brute" || e.type == "charger" || e.type == "mortar" ? 1.4f : 0.9f) * scale;
            double cy = e != null ? e.y + e.h * 0.5 : y;
            double cx = e != null ? e.x : x;
            Shards(cx, cy, 10 * k, new Color(0.9f, 0.91f, 0.95f), 5 + 2 * k);
            var at = W(cx, cy, 0.3f); var c = S.Lin(HOSTILE);
            int n = Count(12 * k);
            for (int i = 0; i < n; i++) { var d = Random.onUnitSphere; V.mote.Emit(at + d * 0.9f * k, -d * 5 * k, 0.1f, 0.18f, Hdr(c, 4)); }
            V.glow.Emit(at, Vector3.zero, 2.4f * k, 0.3f, Hdr(c, 2.2f));
            V.flash.Emit(at, Vector3.zero, 1.6f * k, 0.12f, Hdr(c, 4));
            V.lights.Flash(at, Hdr(c, 1), 4 * k, 4 * k, 0.25f);
            int ns = Count(10 * k);
            for (int i = 0; i < ns; i++) V.spark.Emit(at, Rnd3() * 9 + Vector3.up * 2, 0.06f, 0.4f + Random.value * 0.3f, Hdr(c, 3));
            if (FxCfg.Smoke != "off") for (int i = 0; i < Count(4 * k); i++) V.smokeDark.Emit(at, Rnd3() * 1.2f + Vector3.up * 0.8f, 0.8f * k, 1.2f + Random.value, new Color(1, 1, 1, 0.6f));
            if (e != null && !e.flier) DustWave(cx, e.y, 0.4f * k);
        }

        // Breakable pieces burst into what they are made of
        void BoxDebris(Ev ev)
        {
            if (ev.box == null) return;
            var b = ev.box; double x = (b.x0 + b.x1) / 2, y = (b.y0 + b.y1) / 2;
            string tag = b.tag;
            if (tag == "glass") { Shards(x, y, 18, new Color(0.75f, 0.9f, 1f) * 1.4f, 6); Flash(x, y, "#dff6ff", 1.2f, 0.08f); return; }
            var col = tag == "crate" ? new Color(0.55f, 0.38f, 0.22f) : tag == "pillar" ? new Color(0.7f, 0.72f, 0.76f) : new Color(0.6f, 0.62f, 0.66f);
            Debris(x, y, tag == "pillar" ? 22 : 12, col, tag == "pillar" ? 7 : 5);
            DustWave(x, b.y0, tag == "pillar" ? 1.2f : 0.6f);
        }

        // ---- Projectiles ----
        static readonly HashSet<string> HEAVY = new HashSet<string> { "lance", "rail", "breach", "heavy", "reflected" };
        static readonly HashSet<string> SOLID = new HashSet<string> { "slug", "rivet", "hotRivet", "pellet", "shell" };
        static readonly HashSet<string> EXPLOSIVE = new HashSet<string> { "grenade", "bomblet", "mortar", "missile", "sentryRocket" };

        // Each frame for each projectile in flight: a bright halo and a trail (Energy), or a streak (Tracer)
        readonly Dictionary<Projectile, double> projectileClock = new Dictionary<Projectile, double>();
        float renderDt;
        void CineProjectile(Projectile pr, Vector3 at, Vector3 dir)
        {
            if (V == null) return;
            projectileClock.TryGetValue(pr, out var clock); clock += renderDt * 60;
            int steps = Mathf.Min(8, (int)clock); projectileClock[pr] = clock - steps;
            for (int step = 0; step < steps; step++) {
                var position = at - S.Dir(pr.x, pr.vx, pr.vy) * (step / 60f);
                if (FxCfg.Projectiles == "classic") LegacyProjectileStep(pr, pr.x - pr.vx * (step / 60.0), pr.y - pr.vy * (step / 60.0), position);
                else CineProjectileStep(pr, position, dir);
            }
        }
        void CineProjectileStep(Projectile pr, Vector3 at, Vector3 dir)
        {
            string kind = pr.deflected ? (pr.reflected ? "reflected" : "deflected") : pr.kind ?? "std";
            var c = S.Lin(TrailColor(pr));
            float trail = FxCfg.Trails == "long" ? 0.32f : FxCfg.Trails == "short" ? 0.14f : 0;
            var vel = S.Dir(pr.x, pr.vx, pr.vy);
            if (FxCfg.Projectiles == "tracer" || SOLID.Contains(kind))
            {
                // a hot streak stretched along its flight
                V.streak.Emit(at, vel * 0.35f, kind == "pellet" ? 0.05f : 0.09f, 0.06f + trail * 0.25f, Hdr(c, kind == "hotRivet" ? 5 : 3.5f));
                if (trail > 0 && kind == "hotRivet" && Random.value < 0.4f && FxCfg.Smoke != "off") V.smokeDark.Emit(at, Vector3.up * 0.5f, 0.25f, 0.6f, new Color(1, 1, 1, 0.35f));
                return;
            }
            if (EXPLOSIVE.Contains(kind))
            {
                if (pr.rest || trail == 0) return;
                V.fire.Emit(at - dir * 0.2f, -vel * 0.1f + Rnd3() * 0.3f, 0.3f, 0.18f, Hdr(new Color(1, 0.75f, 0.45f), 2));
                if (FxCfg.Smoke != "off" && Random.value < 0.8f) V.smoke.Emit(at - dir * 0.3f, Rnd3() * 0.3f + Vector3.up * 0.3f, 0.35f, 0.8f + Random.value * 0.6f, new Color(0.8f, 0.8f, 0.82f, 0.6f), Random.value * 6.3f);
                return;
            }
            bool heavy = HEAVY.Contains(kind);
            float s = heavy ? 1.1f : kind == "disc" || kind == "wave" ? 0.9f : 0.55f;
            V.glow.Emit(at, Vector3.zero, s, 0.07f, Hdr(c, heavy ? 2.2f : 1.6f));
            if (trail > 0) V.mote.Emit(at - dir * 0.1f, -vel * 0.04f + Rnd3() * 0.25f, s * 0.22f, trail, Hdr(c, 3));
            if (heavy)
            {
                if (trail > 0) for (int i = 0; i < 2; i++) { var o = Random.onUnitSphere * 0.25f; V.mote.Emit(at + o, -vel * 0.06f + o * 2, 0.08f, trail * 1.4f, Hdr(c, 3.5f)); }
                if (Random.value < 0.35f) V.lights.Flash(at, Hdr(c, 1), 1.6f, 3, 0.12f);
                if (FxCfg.Distortion && Random.value < 0.5f) V.haze.Emit(at - dir * 0.3f, Vector3.zero, 0.9f, 0.25f, new Color(1, 1, 1, 0.6f));
            }
        }
    }
}
