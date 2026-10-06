// Nova's absorbing shield (Settings: Nova's LT move = shield; Unity build only): a hard-light pane of gold
// hexagons where the sim's shield stands, facing his aim. It flares on every block and pulls the hit's energy into
// him as a stream of motes; a perfect block flashes white. The pane burns brighter the more he holds, and a soft
// aura grows around him with the stored energy (with Anim.AbsorbGlow on his suit), so he visibly powers up with
// each hit it takes. Worn thin it flickers; broken, it bursts into shards. Presentation only.
using System.Collections.Generic;
using NovaStriker.Game.Three;
using NovaStriker.Sim;
using UnityEngine;
using static NovaStriker.Sim.Cfg;

namespace NovaStriker.Game
{
    public sealed class NovaShieldFX
    {
        static string GOLD => CHARS["nova"].energy;
        const string PALE = "#fff1c9", WHITE = "#ffffff", HOT = "#ffe2a8";
        readonly Fx fx; readonly TObj scene; float t;
        readonly Texture2D hexT;

        sealed class Pane { public TMesh face, halo, rim; public TMat faceM, haloM, rimM; public TMesh aura; public TMat auraM; public float k, flash, white; public int level; }
        readonly Dictionary<Player, Pane> panes = new Dictionary<Player, Pane>();

        public NovaShieldFX(Fx fx) { this.fx = fx; scene = fx.scene; hexT = RamFX.HexTex(false); }

        Pane PaneOf(Player p)
        {
            if (panes.TryGetValue(p, out var P)) return P;
            var g = Geo.Plane(1, 1);
            TMat Plane(Texture map, string color, float op) => new TMat(TMat.Kind.Basic) { map = map, colorCss = color, transparent = true, opacity = op, blending = Blending.Additive, depthWrite = false, side = Side.Double, fog = false };
            P = new Pane { faceM = Plane(hexT, GOLD, 0.5f), haloM = Plane(fx.tex.glow, GOLD, 0), rimM = new TMat(TMat.Kind.Basic) { colorLin = S.Lin(PALE) * 1.6f, transparent = true, opacity = 0.9f, blending = Blending.Additive, depthWrite = false, fog = false } };
            P.face = new TMesh(g, P.faceM); P.halo = new TMesh(g, P.haloM); P.rim = new TMesh(g, P.rimM);
            foreach (var m in new[] { P.halo, P.face, P.rim }) { m.visible = false; m.RenderOrder = 5; m.cast = false; m.receive = false; m.noOutline = true; scene.add(m); }
            P.auraM = new TMat(TMat.Kind.Sprite) { map = fx.tex.glow, colorCss = GOLD, transparent = true, opacity = 0, depthWrite = false, blending = Blending.Additive, fog = false };
            P.aura = Three.Sprite.Make(P.auraM); P.aura.visible = false; P.aura.RenderOrder = 4; scene.add(P.aura);
            panes[p] = P; return P;
        }

        // A quad along the sim-plane direction (tx, ty) at (x, y), facing the camera side (as RAM's pane)
        static void PlaceQ(TObj mesh, double x, double y, double tx, double ty, double depth = 0)
        {
            var T = S.Dir(x, tx, ty).normalized; var f = Level.Frame(x); var Z = new Vector3((float)f.nx, 0, (float)f.nz).normalized;
            var X = Vector3.Cross(T, Z).normalized;
            mesh.SetQuaternion(ThQ.FromBasis(X, T, Z));
            mesh.position.copy(S.W(x, y, depth));
        }
        static (double cx, double cy, double nx, double ny) Where(Player p)
        {
            var c = PlayerSim.Chest(p); double nx = p.guardDir.x, ny = p.guardDir.y;
            return (c.x + nx * NOVA_SHIELD.reach, c.y + ny * NOVA_SHIELD.reach, nx, ny);
        }

        // The energy of a blocked hit streams from where it struck into his chest
        void Stream(Player p, double x, double y, float n, bool perfect)
        {
            var c = PlayerSim.Chest(p); var to = S.W(c.x, c.y, 0.2); var from = S.W(x, y, 0.25);
            for (int i = 0; i < n; i++)
            {
                float life = 0.2f + S.Rnd() * 0.18f; var o = new Vector3((S.Rnd() - 0.5f) * 0.6f, (S.Rnd() - 0.5f) * 0.6f, (S.Rnd() - 0.5f) * 0.3f);
                var P = fx.Particle(from + o, perfect && S.Rnd() < 0.5f ? WHITE : S.Rnd() < 0.5f ? HOT : GOLD, 0.15f + S.Rnd() * 0.08f, life);
                P.v = (to - from - o) / life; P.drag = 1; P.grav = 0;
            }
        }

        public void OnEvent(Ev ev)
        {
            var F = fx; var p = ev.p;
            switch (ev.type)
            {
                case "nshieldOn":
                {
                    var (cx, cy, nx, ny) = Where(p); var P = PaneOf(p); P.flash = Mathf.Max(P.flash, 0.6f);
                    F.Sprite(cx, cy, "ring", GOLD, 0.9f, 0.18f, 2.2f);
                    F.Burst(cx, cy, PALE, 8, 4, 0.22f, 0.22f, dir: Mathf.Atan2((float)ny, (float)nx), spread: 1.4f);
                    break;
                }
                case "nshieldBlock":
                {
                    var P = PaneOf(p); var (cx, cy, nx, ny) = Where(p); float d = (float)ev.dmg, k = (float)ev.k;
                    P.flash = Mathf.Min(1.4f, P.flash + 0.6f + d * 0.03f);
                    var rig = fx.RigOf(p); if (rig != null) rig.absorbFlash = Mathf.Min(1.5f, rig.absorbFlash + (ev.perfect ? 1.2f : 0.6f + d * 0.02f));
                    float a = Mathf.Atan2((float)ny, (float)nx);
                    F.Burst(ev.x, ev.y, ev.heavy ? WHITE : PALE, ev.heavy ? 16 : 9, ev.heavy ? 8 : 5, 0.28f, 0.26f, dir: a, spread: 1.6f, grav: 5);
                    F.Sprite(ev.x, ev.y, "star", WHITE, (ev.heavy ? 1.2f : 0.75f) + k * 0.4f, 0.12f, 1.4f);
                    Stream(p, ev.x, ev.y, 8 + Mathf.Min(14, d * 0.9f) + (ev.perfect ? 8 : 0), ev.perfect);
                    if (ev.perfect)
                    {
                        P.white = 1;
                        F.Sprite(cx, cy, "star", WHITE, 2.0f, 0.18f, 1.5f); F.Sprite(cx, cy, "ring", GOLD, 1.1f, 0.3f, 3.2f);
                        F.Burst(cx, cy, GOLD, 22, 9, 0.3f, 0.32f); F.PopText(cx, cy + 0.6, "PERFECT", PALE, 0.6f);
                    }
                    // Each quarter of the gauge he fills: a pulse out from him (and a word at the top)
                    int level = (int)ev.level;
                    if (level > P.level || ev.max)
                    {
                        var c = PlayerSim.Chest(p);
                        F.Sprite(c.x, c.y, "ring", HOT, 1.0f + level * 0.2f, 0.35f, 2.6f);
                        F.Sprite(c.x, c.y, "glow", GOLD, 1.6f + level * 0.4f, 0.3f, 1.4f);
                        if (ev.max) F.PopText(c.x, c.y + 1.3, "MAX POWER", HOT, 0.9f);
                    }
                    P.level = level;
                    break;
                }
                case "nshieldBreak":
                {
                    var P = PaneOf(p); var (cx, cy, nx, ny) = Where(p); P.k = 0; P.flash = 0;
                    float a = Mathf.Atan2((float)ny, (float)nx);
                    for (int i = 0; i < 24; i++)
                    {
                        double u = (S.Rnd() - 0.5f) * NOVA_SHIELD.half * 2, x = cx - ny * u, y = cy + nx * u;
                        F.Burst(x, y, S.Rnd() < 0.5f ? PALE : GOLD, 1, 7, 0.28f, 0.5f, dir: a + (S.Rnd() - 0.5f) * 2, spread: 0.5f, grav: 12);
                    }
                    F.Sprite(cx, cy, "ring", WHITE, 1.4f, 0.35f, 3); F.Sprite(cx, cy, "star", WHITE, 1.8f, 0.16f, 1.5f);
                    F.PopText(cx, cy + 1.0, "BROKEN", "#ff8aa8", 0.8f);
                    break;
                }
                case "nshieldReady": { var c = PlayerSim.Chest(p); F.Sprite(c.x, c.y, "ring", GOLD, 1.0f, 0.3f, 2.4f); F.Burst(c.x, c.y, PALE, 10, 4, 0.22f, 0.3f); break; }
                case "absorbSpill":
                {
                    // Half the stored energy spills out of him as falling motes
                    var c = PlayerSim.Chest(p); PaneOf(p).level = (int)System.Math.Floor(p.absorb / 25);
                    F.Burst(c.x, c.y, GOLD, 16, 5, 0.25f, 0.5f, grav: 9); F.Burst(c.x, c.y, "#9aa6b8", 6, 3, 0.22f, 0.4f, grav: 6);
                    break;
                }
                case "parryStun":
                {
                    var e = ev.e; if (e == null) break;
                    double ex = e.x, ey = e.y + e.h + 0.3;
                    F.Sprite(ex, ey, "star", WHITE, 1.1f, 0.22f, 1.6f); F.Sprite(ex, ey, "ring", "#ffe36b", 0.8f, 0.3f, 2.4f);
                    F.Burst(ex, ey, "#ffe36b", 12, 4, 0.22f, 0.35f);
                    F.PopText(ex, ey + 0.4, "STUNNED", "#ffe36b", 0.7f);
                    break;
                }
            }
        }

        public void Update(float dt, World world, View view)
        {
            t += dt;
            var seen = new HashSet<Player>();
            foreach (var p in world.players)
            {
                if (p.@char != "nova") continue;
                var rig = fx.RigOf(p); bool vis = rig != null && rig.root.visible && p.state != "dead";
                bool any = p.state == "nshield" || p.absorb > 0 || panes.ContainsKey(p);
                if (!any) continue;
                var P = PaneOf(p); seen.Add(p);
                bool up = vis && p.state == "nshield";
                P.k += ((up ? 1 : 0) - P.k) * (1 - Mathf.Exp(-dt * (up ? 32 : 16)));
                P.flash = Mathf.Max(0, P.flash - dt * 4.5f); P.white = Mathf.Max(0, P.white - dt * 4);
                float g = rig != null ? rig.absorbGlow : Mathf.Clamp01((float)p.absorb / 100);
                // The pane
                if (P.k > 0.02f)
                {
                    var (cx, cy, nx, ny) = Where(p);
                    float frac = Mathf.Max(0, (float)(p.nshieldStab / NOVA_SHIELD.stability)), weak = frac < 0.3f ? (Mathf.Sin(t * 38) > 0 ? 1 : 0.5f) : 1;
                    float len = (float)NOVA_SHIELD.half * 2 * (0.55f + 0.45f * P.k);
                    PlaceQ(P.halo, cx - nx * 0.05, cy - ny * 0.05, -ny, nx, 0.28f); P.halo.scale.set(1.6f, len * 1.35f, 1);
                    PlaceQ(P.face, cx, cy, -ny, nx, 0.3f); P.face.scale.set(0.9f, len, 1);
                    PlaceQ(P.rim, cx + nx * 0.45, cy + ny * 0.45, -ny, nx, 0.32f); P.rim.scale.set(0.05f, len, 1);
                    var col = Color.Lerp(S.Lin(GOLD), S.Lin(WHITE), Mathf.Min(1, P.white + g * 0.25f));
                    P.faceM.colorLin = col * (1 + 0.8f * g);
                    P.faceM.opacity = (0.28f + 0.22f * frac + 0.2f * g + 0.6f * P.flash) * P.k * weak;
                    P.haloM.opacity = (0.12f + 0.35f * g + 0.4f * P.flash) * P.k;
                    P.rimM.opacity = (0.5f + 0.3f * g + 0.4f * P.flash) * P.k;
                    P.faceM.offset = new Vector2(0, t * (0.2f + 0.5f * g));
                    P.face.visible = P.halo.visible = P.rim.visible = true;
                }
                else P.face.visible = P.halo.visible = P.rim.visible = false;
                // The aura: a soft glow around him that grows and breathes with the energy he holds
                float flash = rig != null ? rig.absorbFlash : 0;
                if (vis && (g > 0.01f || flash > 0.01f))
                {
                    var c = PlayerSim.Chest(p); float breathe = 1 + 0.12f * Mathf.Sin(t * (2.2f + 2.5f * g));
                    P.aura.position.copy(S.W(c.x, c.y, 0.1f));
                    P.aura.scale.setScalar((1.6f + 1.4f * g) * breathe);
                    P.auraM.opacity = Mathf.Min(0.5f, 0.05f + 0.2f * g + 0.15f * flash) * breathe;
                    P.aura.visible = true;
                    // at higher levels, motes of light rise off him
                    if (S.Rnd() < g * g * 0.5f * dt * 60)
                    {
                        var q = fx.Particle(S.W(c.x + (S.Rnd() - 0.5f) * 0.8f, p.y + S.Rnd() * p.h, 0.2f), S.Rnd() < 0.5f ? HOT : GOLD, 0.08f + S.Rnd() * 0.06f, 0.5f + S.Rnd() * 0.4f);
                        q.v = new Vector3(0, 0.8f + S.Rnd() * 0.8f, 0); q.drag = 1; q.grav = 0;
                    }
                }
                else P.aura.visible = false;
            }
            foreach (var kv in new List<KeyValuePair<Player, Pane>>(panes))
                if (!seen.Contains(kv.Key))
                {
                    var P = kv.Value; P.face.visible = P.halo.visible = P.rim.visible = P.aura.visible = false;
                    if (!world.players.Contains(kv.Key)) panes.Remove(kv.Key);
                }
        }
    }
}
