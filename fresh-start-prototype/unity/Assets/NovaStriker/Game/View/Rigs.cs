// Procedural character rigs (rigs.js): placeholder art that keeps each silhouette's defining features: Nova's
// suit from his concept art (BuildNovaRig) with the Sentinel Bracer; Echo's scarf, collar, gauntlets, staff;
// RAM's tower shield, shoulder cannon and horned helmet on a far heavier frame; Fix's goggles, tool pack with its
// crane arm, wrench and welder. Every rig shares one skeleton (same joints; Nova's shoulders and head sit a little
// lower), so the animation drives them all; RAM and Fix are drawn bigger or smaller through `size` (CHARS[].scale).
using System.Collections.Generic;
using NovaStriker.Game.Three;
using NovaStriker.Sim;
using UnityEngine;
using static NovaStriker.Sim.Cfg;

namespace NovaStriker.Game
{
    public sealed class Limb { public TObj top, joint, end; }
    public sealed class RigMats { public TMat @base, trim, under, energy, visor, amber; }
    public struct Edge { public TObj a, b; public float from; public Edge(TObj a, TObj b, float from) { this.a = a; this.b = b; this.from = from; } }

    public sealed class RigExtra
    {
        public List<TMesh> blades = new List<TMesh>(), jets = new List<TMesh>();
        public Dictionary<string, Edge> edges = new Dictionary<string, Edge>();
        // Nova
        public TObj muzzle, bracer, shield, module, greave;
        public TMat plateMat, moduleMat, hardMat;
        public List<TMesh> gauntlets;
        public string moduleTint;
        // Echo
        public TMesh blade, bladeF, backStaff;
        public TMesh[] backTips, glaive;
        public TObj staffTip, hand, handStaff;
        public List<TObj> beltSnares;
        // RAM
        public List<TMesh> stacks, pistons;
        public TObj shieldEdge, cannon;
        // Fix
        public TObj ponytail, crane, craneFore, gun, weldTip, wrench;
        public TMat tipMat;
        public TMesh slung;
    }

    public sealed class Rig
    {
        public TObj root, size, flip, body, hips, spine, head, collar;
        public Limb armN, armF, legN, legF;
        public float absorbGlow, absorbFlash;   // (Nova's absorbed energy, shown on the rig: Anim.AbsorbGlow)
        public RigExtra extra;
        public RigMats mats;
        public string @char;
        public Dictionary<string, TObj> heads = new Dictionary<string, TObj>();
        // animation state
        public float phase, yaw, roll, stretch, lastVy, lastRocketT, hard, craneT, craneK, prevVx = float.NaN;
        public float? cannonA, tail, turn;
        public bool wasGround = true, wasCrouch;
        public readonly float[] cur = new float[Anim.NJ];
        public bool curSet;
        public string headMode;
        public float cloak;
        public bool helmetOff, helmetKnock;   // (Nova: his helmet knocked off at critical health: Anim, HelmetFx)
        // set by the view
        public TMesh ring;
        public List<TMesh> shells = new List<TMesh>();
        public bool? inked;
        public ModelSkin skin;
        public TObj dizzy; public List<(TMesh s, TMesh back)> stars; public TMesh tag;

        internal System.Action<string> setHeadFn;
        internal System.Action<float> setCloakFn;
        public void setHead(string mode) => setHeadFn?.Invoke(mode);
        public void setCloak(float k) => setCloakFn?.Invoke(k);
    }

    public static class Rigs
    {
        static Mesh RBox(float w, float h, float d, float r = 0.05f) =>
            Geo.RoundedBox(w, h, d, 3, Mathf.Min(r, Mathf.Min(w / 2 - 1e-3f, Mathf.Min(h / 2 - 1e-3f, d / 2 - 1e-3f))));
        static Mesh Cap(float r, float len) => Geo.Capsule(r, len, 4, 12);
        static Color C(string css) => Th.Hex(css);

        // Fresnel rim so characters separate from bright backgrounds (Echo's Veil turns it into a shimmering outline)
        public static void AddRim(TMat mat, string color, float strength = 0.45f, float power = 2.4f) => mat.AddRim(C(color), strength, power);

        static TMat Std(uint color, float rough, float metal = 0.08f) => TMat.Std(color, rough, metal);
        static TMat Std(string color, float rough, float metal = 0.08f) => new TMat { colorCss = color, roughness = rough, metalness = metal };
        // Armour (Version 13): painted metal under a clear lacquer
        static TMat Armour(string color, float rough, float metal) =>
            new TMat(TMat.Kind.Physical) { colorCss = color, roughness = rough, metalness = metal, clearcoat = 0.65f, clearcoatRoughness = 0.18f };
        static RigMats Mats(CharDef c) => new RigMats
        {
            @base = Armour(c.@base, 0.4f, 0.1f), trim = Armour(c.trim, 0.36f, 0.3f), under = Std(c.under, 0.62f, 0.15f),
            energy = new TMat { colorCss = c.energy, emissiveCss = c.energy, emissiveIntensity = 2.4f, roughness = 0.3f },
            visor = new TMat { colorHex = 0x0b1018, roughness = 0.12f, metalness = 0.7f },
            amber = new TMat { colorHex = 0xffa53a, emissiveHex = 0xff8a1a, emissiveIntensity = 0.6f, roughness = 0.1f, transparent = true, opacity = 0.72f },
        };
        static RigMats RimAll(RigMats M)
        {
            AddRim(M.@base, "#d6ecff", 0.5f); AddRim(M.trim, "#d6ecff", 0.5f); AddRim(M.under, "#d6ecff", 0.35f);
            return M;
        }
        static TMat GlowMat(string c, float k = 2.4f) => new TMat { colorCss = c, emissiveCss = c, emissiveIntensity = k, roughness = 0.3f };

        static TMesh MeshAt(Mesh geo, TMat mat, float x = 0, float y = 0, float z = 0) { var m = new TMesh(geo, mat, x, y, z); m.cast = true; return m; }
        static TObj G(float x = 0, float y = 0, float z = 0) => Group.Make(x, y, z);

        static Limb MakeLimb(TObj parent, RigMats M, float upperLen, float lowerLen, float r, float z, bool isArm)
        {
            var top = G(0, 0, z); parent.add(top);
            top.add(MeshAt(Cap(r, upperLen - r), M.under, 0, -upperLen / 2));
            var joint = G(0, -upperLen, 0); top.add(joint);
            joint.add(MeshAt(Cap(r * 0.92f, lowerLen - r), M.under, 0, -lowerLen / 2));
            var end = G(0, -lowerLen, 0); joint.add(end);
            return new Limb { top = top, joint = joint, end = end };
        }

        public static Rig BuildPlayerRig(string charId)
        {
            if (charId == "nova") return BuildNovaRig();
            if (charId == "ram" || charId == "fix") return BuildNewRig(charId);
            // Echo, as in his concept art: cream-white armour with bronze-gold trim over a dark charcoal undersuit, orange
            // energy, a bare face with spiky blond hair (the helmet and mask are the other looks: Settings, Echo's head)
            var c = CHARS[charId]; var D = Mats(c);
            var M = RimAll(new RigMats { @base = Armour("#efe7d6", 0.4f, 0.1f), trim = Armour("#c8964a", 0.34f, 0.6f), under = Std("#25242b", 0.62f, 0.15f), energy = D.energy, visor = D.visor, amber = D.amber });
            TMat steel = Std("#3a3d45", 0.35f, 0.7f), cloth = Std("#efe6d3", 0.85f, 0);
            TObj root = G(), size = G(), flip = G(), body = G();
            root.add(size); size.add(flip); flip.add(body);
            var hips = G(0, 0.95f, 0); body.add(hips);
            hips.add(MeshAt(RBox(0.34f, 0.2f, 0.38f, 0.07f), M.under, 0, 0.02f));
            var spine = G(0, 0.08f, 0); hips.add(spine);
            spine.add(MeshAt(RBox(0.3f, 0.3f, 0.34f, 0.08f), M.under, 0, 0.18f));
            const float chestW = 0.44f;
            spine.add(MeshAt(RBox(0.34f, 0.34f, chestW, 0.1f), M.@base, 0.02f, 0.42f));
            spine.add(MeshAt(RBox(0.05f, 0.05f, chestW * 0.7f, 0.02f), M.energy, 0.18f, 0.44f));
            foreach (var z in new[] { 0.28f, -0.28f })
            {
                spine.add(MeshAt(RBox(0.26f, 0.16f, 0.2f, 0.07f), M.@base, 0, 0.55f, z * 0.95f));
                spine.add(MeshAt(RBox(0.25f, 0.03f, 0.19f, 0.012f), M.trim, 0, 0.475f, z * 0.95f));
            }
            var head = G(0, 0.66f, 0); spine.add(head);
            var heads = new Dictionary<string, TObj>();
            {
                var faceMat = new TMat { colorHex = 0xe2b597, roughness = 0.85f };
                head.add(MeshAt(Geo.Sphere(0.14f, 24, 18), faceMat, 0, 0.1f));
                var eyeMat = new TMat { colorHex = 0x1a1a20, roughness = 0.4f };
                foreach (var z in new[] { 0.045f, -0.045f }) head.add(MeshAt(Geo.Sphere(0.018f, 8, 6), eyeMat, 0.128f, 0.125f, z));
                var hairMat = new TMat { colorHex = 0xd9b464, roughness = 0.8f };
                var hair = G(0, 0.1f, 0); head.add(hair);
                var hairCap = MeshAt(Geo.Sphere(0.15f, 24, 12, 0, Mathf.PI * 2, 0, Mathf.PI * 0.42f), hairMat);
                hairCap.rotation.z = 0.35f; hair.add(hairCap);
                foreach (var (x, y, z, rz) in new[] { (0.09f, 0.1f, 0.05f, -0.9f), (0.1f, 0.09f, -0.04f, -1.05f), (0.05f, 0.13f, 0.0f, -0.6f), (0.0f, 0.14f, -0.06f, -0.3f),
                    (0.02f, 0.14f, 0.06f, 0.2f), (-0.05f, 0.13f, 0.0f, 0.7f), (-0.08f, 0.1f, 0.07f, 1.2f), (-0.08f, 0.1f, -0.07f, 1.3f), (0.06f, 0.12f, -0.08f, -0.4f) })
                {
                    var tuft = MeshAt(Geo.Cone(0.045f, 0.12f, 5), hairMat, x, y, z); tuft.rotation.z = rz; hair.add(tuft);
                }
                var helmet = G(); head.add(helmet);
                helmet.add(MeshAt(Geo.Sphere(0.165f, 28, 18, Mathf.PI + 0.95f, Mathf.PI * 2 - 1.9f, 0, Mathf.PI * 0.78f), M.@base, 0, 0.1f));
                var visor = MeshAt(Geo.Sphere(0.17f, 20, 14, Mathf.PI - 0.98f, 1.96f, 0.62f, 1.2f), M.amber, 0, 0.1f);
                helmet.add(visor);
                foreach (var z in new[] { 0.15f, -0.15f }) helmet.add(MeshAt(RBox(0.07f, 0.03f, 0.02f, 0.008f), M.energy, 0.02f, 0.17f, z));
                var mask = G(); head.add(mask);
                mask.add(MeshAt(Geo.Sphere(0.152f, 24, 12, 0, Mathf.PI * 2, Mathf.PI * 0.55f, Mathf.PI * 0.45f), M.@base, 0, 0.1f));
                foreach (var z in new[] { 0.148f, -0.148f })
                {
                    var ear = MeshAt(Geo.Cylinder(0.06f, 0.06f, 0.04f, 16), M.trim, -0.01f, 0.1f, z); ear.rotation.x = Mathf.PI / 2; mask.add(ear);
                }
                foreach (var z in new[] { 0.03f, -0.03f }) mask.add(MeshAt(RBox(0.02f, 0.018f, 0.03f, 0.006f), M.energy, 0.148f, 0.03f, z));
                heads["helmet"] = helmet; heads["mask"] = mask; heads["hair"] = hair;
                var ring = MeshAt(Geo.Torus(0.17f, 0.06f, 10, 20), cloth, 0.0f, 0.62f);
                ring.rotation.x = Mathf.PI / 2; spine.add(ring);
            }
            var collar = G(-0.2f, 0.58f, 0.16f); spine.add(collar);

            var armN = MakeLimb(spine, M, 0.3f, 0.29f, 0.065f, 0.3f, true);
            var armF = MakeLimb(spine, M, 0.3f, 0.29f, 0.065f, -0.3f, true);
            armN.top.position.y = 0.53f; armF.top.position.y = 0.53f;
            foreach (var a in new[] { armN, armF }) a.end.add(MeshAt(Geo.Sphere(0.07f, 12, 10), M.under, 0, -0.03f));
            var legN = MakeLimb(hips, M, 0.46f, 0.46f, 0.085f, 0.13f, false);
            var legF = MakeLimb(hips, M, 0.46f, 0.46f, 0.085f, -0.13f, false);
            foreach (var l in new[] { legN, legF })
            {
                l.top.add(MeshAt(RBox(0.2f, 0.26f, 0.2f, 0.07f), M.@base, 0.02f, -0.18f));
                l.joint.add(MeshAt(RBox(0.19f, 0.3f, 0.18f, 0.06f), M.@base, 0.03f, -0.24f));
                l.joint.add(MeshAt(Geo.Sphere(0.058f, 14, 10), M.trim, 0.07f, 0.0f));
                l.end.add(MeshAt(RBox(0.26f, 0.1f, 0.16f, 0.04f), M.@base, 0.06f, -0.02f));
                l.end.add(MeshAt(RBox(0.28f, 0.022f, 0.165f, 0.008f), M.under, 0.06f, -0.07f));
            }
            var ex = new RigExtra();
            foreach (var a in new[] { armN, armF })
            {
                a.joint.add(MeshAt(RBox(0.15f, 0.24f, 0.16f, 0.04f), M.@base, 0.01f, -0.15f));
                a.joint.add(MeshAt(RBox(0.155f, 0.03f, 0.165f, 0.01f), M.trim, 0.01f, -0.04f));
                a.joint.add(MeshAt(RBox(0.03f, 0.2f, 0.03f, 0.01f), M.energy, 0.09f, -0.15f));
            }
            var bladeGeo = Geo.Box(0.035f, 0.62f, 0.11f);
            TMesh bladeN = MeshAt(bladeGeo, M.energy, 0.02f, -0.36f, 0), bladeF = MeshAt(bladeGeo, M.energy, 0.02f, -0.36f, 0);
            armN.end.add(bladeN); armF.end.add(bladeF); bladeN.visible = bladeF.visible = false;
            ex.blade = bladeN; ex.bladeF = bladeF;
            var staffGeo = Geo.Cylinder(0.035f, 0.035f, 1.5f, 10);
            var back = MeshAt(staffGeo, steel, -0.24f, 0.42f, 0); back.rotation.z = 0.9f; spine.add(back);
            TMesh bt1 = MeshAt(Geo.Cone(0.06f, 0.4f, 4), M.energy, 0, 0.95f, 0), bt2 = MeshAt(Geo.Cone(0.06f, 0.4f, 4), M.energy, 0, -0.95f, 0);
            bt2.rotation.z = Mathf.PI; bt1.scale.z = bt2.scale.z = 0.35f; back.add(bt1); back.add(bt2); ex.backTips = new[] { bt1, bt2 };
            var hand = G(0, -0.02f, 0); armN.end.add(hand);
            var hs = MeshAt(staffGeo, steel, 0, 0, 0); hs.rotation.z = Mathf.PI / 2; hand.add(hs);
            foreach (var s in new[] { -0.75f, 0.75f }) hand.add(MeshAt(Geo.Sphere(0.05f, 10, 8), M.energy, s, 0, 0));
            var tipGeo = Geo.Cone(0.07f, 0.5f, 4);
            TMesh tipA = MeshAt(tipGeo, M.energy, 1.0f, 0, 0), tipB = MeshAt(tipGeo, M.energy, -1.0f, 0, 0);
            tipA.rotation.z = -Mathf.PI / 2; tipB.rotation.z = Mathf.PI / 2; tipA.scale.z = tipB.scale.z = 0.35f;
            hand.add(tipA); hand.add(tipB); ex.glaive = new[] { tipA, tipB };
            var staffTip = G(1.05f, 0, 0); hand.add(staffTip); ex.staffTip = staffTip;
            var staffTail = G(-1.05f, 0, 0); hand.add(staffTail);
            TObj bladeTipN = G(0.02f, -0.67f, 0), bladeTipF = G(0.02f, -0.67f, 0); armN.end.add(bladeTipN); armF.end.add(bladeTipF);
            ex.edges["bladeN"] = new Edge(armN.end, bladeTipN, 0.15f); ex.edges["bladeF"] = new Edge(armF.end, bladeTipF, 0.15f);
            ex.edges["glaiveA"] = new Edge(hand, staffTip, 0.4f); ex.edges["glaiveB"] = new Edge(hand, staffTail, 0.4f);
            hand.visible = false; ex.hand = hand;
            ex.backStaff = back; ex.handStaff = hand;
            hips.add(MeshAt(RBox(0.36f, 0.07f, 0.4f, 0.03f), M.under, 0, 0.1f));
            foreach (var z in new[] { -0.12f, 0.12f }) hips.add(MeshAt(RBox(0.08f, 0.09f, 0.07f, 0.02f), M.trim, 0.17f, 0.08f, z));
            ex.beltSnares = new List<TObj>();
            foreach (var z in new[] { 0.22f, -0.22f })
            {
                var g = G(-0.03f, 0.05f, z); hips.add(g);
                var disc = MeshAt(Geo.Cylinder(0.085f, 0.085f, 0.035f, 18), M.under); disc.rotation.x = Mathf.PI / 2; g.add(disc);
                var ring = MeshAt(Geo.Torus(0.07f, 0.012f, 6, 18), M.energy, 0, 0, z > 0 ? 0.02f : -0.02f); g.add(ring);
                ex.beltSnares.Add(g);
            }

            var rig = FinishRig(new Rig
            {
                root = root, size = size, flip = flip, body = body, hips = hips, spine = spine, head = head, collar = collar,
                armN = armN, armF = armF, legN = legN, legF = legF, extra = ex, mats = M, @char = charId, heads = heads,
            });
            rig.setHeadFn = mode =>
            {
                if (rig.headMode == mode) return;
                rig.headMode = mode; heads["helmet"].visible = mode == "helmet"; heads["mask"].visible = mode == "mask"; heads["hair"].visible = mode != "helmet";
            };
            rig.setHead(SETTINGS.echoHead ?? "bare");
            return rig;
        }

        // ---- Nova: his approved concept art (portrait, turnaround and action sheets) ----
        // Pearl-white armour over a navy bodysuit with glowing gold seams: a high stand-up collar, a sculpted breastplate
        // with the gold four-point star, rounded pauldrons, a round gold belt buckle, white side panels down the thighs,
        // big knee pads, white greaves and boots, and the Sentinel Bracer: a long white shell over the right forearm that
        // reaches past the fist, gold along its edge and at its emitter. His face shows, with swept-back dark hair; the
        // full white helmet with its gold visor is the other look (Settings: Nova's head).
        static Rig BuildNovaRig()
        {
            var c = CHARS["nova"]; var M = RimAll(Mats(c));
            TMat glove = Std("#1d2230", 0.6f, 0.15f), gold = Std("#e3b25c", 0.25f, 0.85f);
            TMat skin = new TMat { colorHex = 0xd6a07e, roughness = 0.8f }, hairMat = new TMat { colorHex = 0x2e1d14, roughness = 0.75f };
            TMat goldVisor = new TMat { colorCss = "#ffcf6a", emissiveCss = "#ff9d1a", emissiveIntensity = 0.5f, roughness = 0.12f, metalness = 0.8f };
            var S = Skeleton("nova", M, 0.062f, 0.085f, 0.3f, 0.13f);
            TObj hips = S.hips, spine = S.spine, head = S.head; Limb armN = S.armN, armF = S.armF, legN = S.legN, legF = S.legF;
            armN.top.position.y = armF.top.position.y = 0.51f; head.position.y = 0.64f;
            var ex = new RigExtra();

            // Pelvis, white belt and the round gold buckle
            hips.add(MeshAt(RBox(0.26f, 0.2f, 0.33f, 0.07f), M.under, 0, 0.0f));
            hips.add(MeshAt(RBox(0.285f, 0.05f, 0.355f, 0.02f), M.@base, 0, 0.08f));
            var buckle = MeshAt(Geo.Cylinder(0.05f, 0.05f, 0.025f, 20), gold, 0.15f, 0.08f); buckle.rotation.z = Mathf.PI / 2; hips.add(buckle);
            var core = MeshAt(Geo.Cylinder(0.026f, 0.026f, 0.012f, 16), M.energy, 0.165f, 0.08f); core.rotation.z = Mathf.PI / 2; hips.add(core);

            // Torso: navy core, breastplate with its gold star and seams, high collar, back plate
            spine.add(MeshAt(RBox(0.26f, 0.34f, 0.36f, 0.08f), M.under, 0, 0.2f));
            spine.add(MeshAt(RBox(0.25f, 0.27f, 0.4f, 0.09f), M.@base, 0.035f, 0.41f));
            spine.add(MeshAt(RBox(0.2f, 0.1f, 0.18f, 0.05f), M.@base, 0.045f, 0.265f));
            var starV = MeshAt(Geo.Octahedron(0.055f), M.energy, 0.162f, 0.42f); starV.scale.set(0.15f, 1, 0.35f); spine.add(starV);
            var starH = MeshAt(Geo.Octahedron(0.055f), M.energy, 0.162f, 0.42f); starH.scale.set(0.15f, 0.3f, 0.75f); spine.add(starH);
            foreach (var sz in new[] { 1f, -1f })
            {
                var seam = MeshAt(RBox(0.01f, 0.012f, 0.13f, 0.004f), M.energy, 0.158f, 0.5f, sz * 0.1f); seam.rotation.x = sz * 0.5f; spine.add(seam);
                spine.add(MeshAt(RBox(0.16f, 0.012f, 0.01f, 0.004f), M.energy, 0.035f, 0.33f, sz * 0.2f));   // side seam, seen from the gameplay camera
                spine.add(MeshAt(RBox(0.01f, 0.08f, 0.01f, 0.004f), M.energy, 0.1f, 0.6f, sz * 0.032f));     // either side of the collar's opening
            }
            spine.add(MeshAt(Geo.Cylinder(0.1f, 0.12f, 0.11f, 20), M.@base, 0, 0.6f));
            spine.add(MeshAt(RBox(0.08f, 0.24f, 0.3f, 0.04f), M.@base, -0.15f, 0.42f));

            // Heads: his face with swept-back dark hair, or the full white helmet with its gold visor
            var heads = new Dictionary<string, TObj>();
            head.add(MeshAt(Geo.Cylinder(0.05f, 0.055f, 0.1f, 12), skin, 0, -0.03f));
            var bare = G(); head.add(bare);
            var face = MeshAt(Geo.Sphere(0.125f, 24, 18), skin, 0, 0.1f); face.scale.set(1, 1.08f, 0.95f); bare.add(face);
            var eye = new TMat { colorHex = 0x2a1a12, roughness = 0.4f };
            foreach (var z in new[] { 0.042f, -0.042f })
            {
                bare.add(MeshAt(Geo.Sphere(0.011f, 8, 6), eye, 0.116f, 0.112f, z * 0.95f));
                bare.add(MeshAt(RBox(0.008f, 0.008f, 0.035f, 0.003f), hairMat, 0.118f, 0.138f, z));
                bare.add(MeshAt(Geo.Sphere(0.03f, 8, 6), skin, -0.005f, 0.1f, z * 2.85f));
            }
            bare.add(MeshAt(RBox(0.025f, 0.04f, 0.025f, 0.01f), skin, 0.122f, 0.09f));
            var hair = G(0, 0.1f, 0); bare.add(hair);
            var cap = MeshAt(Geo.Sphere(0.135f, 24, 12, 0, Mathf.PI * 2, 0, Mathf.PI * 0.45f), hairMat); cap.rotation.z = 0.3f; hair.add(cap);
            foreach (var (x, y, z, rz) in new[] { (0.09f, 0.075f, 0.04f, 0.55f), (0.09f, 0.075f, -0.04f, 0.65f), (0.05f, 0.105f, 0.0f, 0.85f), (0.0f, 0.115f, 0.05f, 1.15f), (0.0f, 0.115f, -0.05f, 1.15f), (-0.06f, 0.095f, 0.0f, 1.5f) })
            {
                var tuft = MeshAt(Geo.Cone(0.042f, 0.11f, 5), hairMat, x, y, z); tuft.rotation.z = rz; hair.add(tuft);
            }
            var helmet = G(); head.add(helmet);
            var shell = MeshAt(Geo.Sphere(0.15f, 24, 18), M.@base, 0, 0.1f); shell.scale.set(1, 1.05f, 0.95f); helmet.add(shell);
            helmet.add(MeshAt(Geo.Sphere(0.153f, 20, 14, Mathf.PI - 1.15f, 2.3f, 0.55f, 1.15f), goldVisor, 0, 0.1f));
            helmet.add(MeshAt(RBox(0.05f, 0.03f, 0.03f, 0.01f), M.@base, 0.14f, 0.2f));
            foreach (var z in new[] { 0.14f, -0.14f }) helmet.add(MeshAt(RBox(0.06f, 0.025f, 0.02f, 0.008f), M.energy, 0.0f, 0.1f, z));
            heads["bare"] = bare; heads["helmet"] = helmet;

            // Arms: rounded pauldrons, navy sleeves with a white outer stripe, the left forearm guard, dark gloves
            foreach (var a in new[] { armN, armF })
            {
                float sz = Mathf.Sign(a.top.position.z);
                var pad = MeshAt(Geo.Sphere(0.09f, 18, 12), M.@base, 0, -0.01f, sz * 0.012f); pad.scale.set(1.15f, 0.62f, 1); a.top.add(pad);
                a.top.add(MeshAt(RBox(0.15f, 0.05f, 0.1f, 0.02f), M.@base, 0, -0.06f, sz * 0.035f));
                a.top.add(MeshAt(RBox(0.06f, 0.16f, 0.02f, 0.008f), M.@base, 0, -0.16f, sz * 0.058f));
                a.end.add(MeshAt(Geo.Sphere(0.068f, 12, 10), glove, 0, -0.03f));
                a.end.add(MeshAt(RBox(0.04f, 0.03f, 0.08f, 0.012f), M.@base, 0.05f, -0.03f));
            }
            armF.joint.add(MeshAt(RBox(0.13f, 0.22f, 0.13f, 0.05f), M.@base, 0.01f, -0.15f));
            armF.joint.add(MeshAt(RBox(0.01f, 0.16f, 0.01f, 0.004f), M.energy, 0.072f, -0.15f, -0.04f));

            // Legs: white side panels down the navy thighs, knee pads, greaves with a navy stripe, white boots
            foreach (var l in new[] { legN, legF })
            {
                float sz = Mathf.Sign(l.top.position.z);
                l.top.add(MeshAt(RBox(0.13f, 0.32f, 0.04f, 0.015f), M.@base, 0.0f, -0.2f, sz * 0.062f));
                var knee = MeshAt(Geo.Sphere(0.064f, 14, 10), M.@base, 0.065f, 0.0f); knee.scale.set(0.9f, 1.1f, 1.05f); l.joint.add(knee);
                l.joint.add(MeshAt(Geo.Sphere(0.013f, 8, 6), M.energy, 0.123f, 0.0f));
                l.joint.add(MeshAt(RBox(0.15f, 0.32f, 0.16f, 0.06f), M.@base, 0.03f, -0.26f));
                l.joint.add(MeshAt(RBox(0.03f, 0.2f, 0.01f, 0.004f), M.trim, 0.0f, -0.25f, sz * 0.081f));
                l.end.add(MeshAt(RBox(0.15f, 0.035f, 0.165f, 0.012f), M.trim, 0.0f, 0.06f));
                l.end.add(MeshAt(RBox(0.27f, 0.1f, 0.17f, 0.04f), M.@base, 0.06f, 0.0f));
                l.end.add(MeshAt(RBox(0.29f, 0.022f, 0.175f, 0.008f), glove, 0.06f, -0.06f));
            }
            // Light boosters and skate-blade boots
            var jetMat = new TMat(TMat.Kind.Basic) { colorHex = 0xfff1c9, transparent = true, opacity = 0.85f, blending = Blending.Additive, depthWrite = false };
            foreach (var l in new[] { legN, legF })
            {
                var j = new TMesh(Geo.Cylinder(0, 0.07f, 0.34f, 10, 1, true), jetMat); j.rotation.z = Mathf.PI; j.position.set(0.04f, -0.26f, 0);
                j.visible = false; l.end.add(j); ex.jets.Add(j);
            }
            var bladeMat = GlowMat(c.energy, 1.6f);
            foreach (var l in new[] { legN, legF })
                foreach (var z in new[] { 0.083f, -0.083f })
                {
                    var b = MeshAt(RBox(0.28f, 0.018f, 0.01f, 0.004f), bladeMat, 0.06f, -0.066f, z); l.end.add(b); ex.blades.Add(b);
                }

            // CP-07, the Sentinel Bracer (gun and shield in one device): the long white shell and its narrower nose past
            // the fist, gold along the edge and at the emitter, and the attachment slot on the side facing the camera
            var bracer = G(0, -0.14f, 0); armN.joint.add(bracer);
            bracer.add(MeshAt(RBox(0.17f, 0.36f, 0.17f, 0.07f), M.@base, 0.02f, -0.04f));
            bracer.add(MeshAt(RBox(0.13f, 0.15f, 0.13f, 0.05f), M.@base, 0.045f, -0.24f));
            bracer.add(MeshAt(RBox(0.012f, 0.4f, 0.03f, 0.005f), M.energy, 0.11f, -0.1f));
            bracer.add(MeshAt(RBox(0.1f, 0.2f, 0.01f, 0.004f), M.trim, 0.02f, -0.02f, -0.087f));
            var muzzle = MeshAt(Geo.Cylinder(0.035f, 0.045f, 0.04f, 14), M.energy, 0.05f, -0.33f, 0); bracer.add(muzzle);
            ex.muzzle = muzzle;
            var shield = G(0.14f, -0.1f, 0); bracer.add(shield);
            var plateMat = new TMat { colorHex = 0xfff1d6, emissiveCss = c.energy, emissiveIntensity = 1.6f, transparent = true, opacity = 0.55f, side = Side.Double, roughness = 0.2f };
            var plateM = new TMesh(Geo.Circle(0.55f, 6), plateMat); plateM.rotation.y = Mathf.PI / 2; shield.add(plateM);
            shield.scale.setScalar(0.001f);
            ex.bracer = bracer; ex.shield = shield; ex.plateMat = plateMat;
            var moduleMat = new TMat { colorCss = ATTACH_LOOK["lance"].tint, emissiveCss = ATTACH_LOOK["lance"].tint, emissiveIntensity = 2.2f, roughness = 0.3f };
            var module = MeshAt(RBox(0.07f, 0.09f, 0.03f, 0.01f), moduleMat, 0.02f, 0.06f, 0.09f); bracer.add(module);
            ex.module = module; ex.moduleMat = moduleMat;

            // Hard-light fists and greave
            var hardMat = new TMat { colorCss = "#fff4d6", emissiveCss = c.energy, emissiveIntensity = 2.6f, transparent = true, opacity = 0.82f, roughness = 0.15f };
            ex.gauntlets = new List<TMesh>();
            foreach (var a in new[] { armN, armF })
            {
                var g = new TMesh(Geo.Icosahedron(0.13f, 0), hardMat); g.position.set(0.02f, -0.05f, 0); g.scale.set(1.1f, 1.3f, 1); g.visible = false; a.end.add(g); ex.gauntlets.Add(g);
            }
            var greave = new TMesh(Geo.Icosahedron(0.15f, 0), hardMat); greave.position.set(0.08f, -0.03f, 0); greave.scale.set(1.7f, 0.9f, 1); greave.visible = false;
            legN.end.add(greave); ex.greave = greave; ex.hardMat = hardMat;
            ex.edges["fistN"] = new Edge(armN.joint, armN.end, 0.55f); ex.edges["fistF"] = new Edge(armF.joint, armF.end, 0.55f); ex.edges["boot"] = new Edge(legN.joint, legN.end, 0.5f);

            S.extra = ex; S.mats = M; S.@char = "nova"; S.heads = heads;
            var rig = FinishRig(S);
            rig.setHeadFn = mode =>
            {
                if (rig.headMode == mode) return;
                rig.headMode = mode; helmet.visible = mode == "helmet"; bare.visible = mode != "helmet";
            };
            rig.setHead(SETTINGS.novaHead ?? "helmet");
            return rig;
        }

        // The common tail of every rig: shadows, and Echo's Veil (any rig can fade)
        static Rig FinishRig(Rig rig)
        {
            var M = rig.mats;
            rig.root.traverse(o => { if (o is TMesh m) m.receive = false; });
            Color SHIMMER = C("#e6f4ff");
            var cloakMats = new List<TMat>();
            rig.root.traverse(o => { if (o is TMesh m && m.material != null && !cloakMats.Contains(m.material)) cloakMats.Add(m.material); });
            var cloakBase = new List<(TMat m, float op, bool tr, bool energy)>();
            foreach (var m in cloakMats) cloakBase.Add((m, m.opacity, m.transparent, m == M.energy));
            rig.cloak = 0;
            rig.setCloakFn = k =>
            {
                k = Mathf.Clamp01(k);
                if (k == rig.cloak) return;
                bool flipT = (rig.cloak > 0.001f) != (k > 0.001f); rig.cloak = k;
                foreach (var b in cloakBase)
                {
                    if (flipT) b.m.transparent = k > 0.001f || b.tr;
                    b.m.opacity = b.op * (1 - (b.energy ? 0.55f : 0.88f) * k);
                    if (b.m.rim != null)
                    {
                        float rb = (float)b.m.userData["rimBase"]; var rc = (Color)b.m.userData["rimCol"];
                        b.m.SetRim(rb + 1.8f * k, 0.95f * k, Color.Lerp(rc.linear, SHIMMER.linear, k).gamma);
                    }
                }
            };
            rig.setHeadFn = _ => { };
            return rig;
        }

        // ---- RAM and Fix ----
        static Rig Skeleton(string charId, RigMats M, float arm, float leg, float shoulderZ, float hipZ, float upperArm = 0.3f, float foreArm = 0.29f)
        {
            TObj root = G(), size = G(), flip = G(), body = G();
            root.add(size); size.add(flip); flip.add(body);
            size.scale.setScalar(CHARS[charId].scale > 0 ? (float)CHARS[charId].scale : 1);
            var hips = G(0, 0.95f, 0); body.add(hips);
            var spine = G(0, 0.08f, 0); hips.add(spine);
            var head = G(0, 0.66f, 0); spine.add(head);
            var collar = G(-0.2f, 0.58f, 0.16f); spine.add(collar);
            Limb armN = MakeLimb(spine, M, upperArm, foreArm, arm, shoulderZ, true), armF = MakeLimb(spine, M, upperArm, foreArm, arm, -shoulderZ, true);
            armN.top.position.y = 0.53f; armF.top.position.y = 0.53f;
            Limb legN = MakeLimb(hips, M, 0.46f, 0.46f, leg, hipZ, false), legF = MakeLimb(hips, M, 0.46f, 0.46f, leg, -hipZ, false);
            return new Rig { root = root, size = size, flip = flip, body = body, hips = hips, spine = spine, head = head, collar = collar, armN = armN, armF = armF, legN = legN, legF = legF };
        }

        static Rig BuildNewRig(string charId)
        {
            var c = CHARS[charId]; var M = RimAll(Mats(c)); var ex = new RigExtra();
            Rig S;
            if (charId == "ram")
            {
                S = Skeleton(charId, M, 0.095f, 0.11f, 0.42f, 0.16f, 0.31f);
                TObj hips = S.hips, spine = S.spine, head = S.head; Limb armN = S.armN, armF = S.armF, legN = S.legN, legF = S.legF;
                hips.add(MeshAt(RBox(0.44f, 0.24f, 0.56f, 0.08f), M.trim, 0, 0.02f));
                hips.add(MeshAt(RBox(0.12f, 0.22f, 0.28f, 0.04f), M.@base, 0.21f, -0.04f));
                spine.add(MeshAt(RBox(0.4f, 0.36f, 0.5f, 0.1f), M.under, 0, 0.18f));
                spine.add(MeshAt(RBox(0.54f, 0.5f, 0.68f, 0.13f), M.@base, 0.03f, 0.45f));
                spine.add(MeshAt(RBox(0.44f, 0.16f, 0.58f, 0.06f), M.trim, 0.04f, 0.2f));
                spine.add(MeshAt(RBox(0.38f, 0.05f, 0.02f, 0.01f), M.energy, 0.04f, 0.52f, 0.345f));
                spine.add(MeshAt(RBox(0.3f, 0.04f, 0.02f, 0.01f), M.energy, 0.06f, 0.4f, 0.345f));
                spine.add(MeshAt(RBox(0.32f, 0.13f, 0.46f, 0.05f), M.trim, -0.02f, 0.66f));
                spine.add(MeshAt(RBox(0.26f, 0.52f, 0.52f, 0.08f), M.trim, -0.36f, 0.44f));
                ex.stacks = new List<TMesh>();
                foreach (var z in new[] { 0.15f, -0.15f })
                {
                    var st = MeshAt(Geo.Cylinder(0.06f, 0.07f, 0.34f, 12), M.under, -0.42f, 0.78f, z); st.rotation.z = 0.35f; spine.add(st);
                    var rim = MeshAt(Geo.Torus(0.062f, 0.016f, 6, 14), M.energy, -0.48f, 0.94f, z); rim.rotation.set(Mathf.PI / 2, 0.35f, 0); spine.add(rim);
                    ex.stacks.Add(rim);
                }
                foreach (var z in new[] { 0.42f, -0.42f })
                {
                    spine.add(MeshAt(RBox(0.46f, 0.26f, 0.34f, 0.11f), M.@base, 0, 0.62f, z));
                    spine.add(MeshAt(RBox(0.36f, 0.06f, 0.36f, 0.03f), M.trim, 0, 0.76f, z));
                    spine.add(MeshAt(RBox(0.3f, 0.035f, 0.02f, 0.01f), M.energy, 0, 0.62f, z + Mathf.Sign(z) * 0.172f));
                }
                head.position.set(0.13f, 0.74f, 0);
                head.add(MeshAt(Geo.Sphere(0.165f, 20, 16), M.trim, 0, 0.06f));
                head.add(MeshAt(RBox(0.22f, 0.13f, 0.27f, 0.05f), M.@base, 0.07f, -0.04f));
                head.add(MeshAt(RBox(0.05f, 0.04f, 0.24f, 0.012f), M.energy, 0.15f, 0.065f));
                foreach (var z in new[] { 0.152f, -0.152f }) head.add(MeshAt(RBox(0.15f, 0.036f, 0.022f, 0.01f), M.energy, 0.07f, 0.065f, z));
                foreach (var z in new[] { 0.15f, -0.15f })
                {
                    var horn = MeshAt(Geo.Torus(0.1f, 0.038f, 8, 20, Mathf.PI * 1.45f), M.@base, -0.05f, 0.08f, z);
                    horn.rotation.set(0, 0, 0.9f); horn.scale.set(1, 1, 1.3f); head.add(horn);
                    head.add(MeshAt(Geo.Sphere(0.03f, 8, 6), M.energy, 0.0f, -0.01f, z * 1.06f));
                }
                foreach (var a in new[] { armN, armF }) { a.joint.add(MeshAt(RBox(0.24f, 0.3f, 0.26f, 0.06f), M.@base, 0.01f, -0.15f)); a.end.add(MeshAt(Geo.Sphere(0.11f, 12, 10), M.under, 0.01f, -0.04f)); }
                foreach (var z in new[] { 0.07f, -0.07f })
                {
                    armF.joint.add(MeshAt(Geo.Cylinder(0.03f, 0.03f, 0.3f, 10), M.under, -0.1f, -0.14f, z));
                    armF.joint.add(MeshAt(Geo.Torus(0.036f, 0.012f, 6, 12), M.energy, -0.1f, -0.2f, z));
                }
                ex.pistons = new List<TMesh>();
                foreach (var l in new[] { legN, legF })
                {
                    l.top.add(MeshAt(RBox(0.28f, 0.32f, 0.28f, 0.08f), M.@base, 0.02f, -0.18f));
                    l.joint.add(MeshAt(RBox(0.26f, 0.34f, 0.26f, 0.07f), M.@base, 0.04f, -0.24f));
                    var pis = MeshAt(Geo.Cylinder(0.035f, 0.035f, 0.32f, 10), M.trim, -0.1f, -0.22f, 0); l.joint.add(pis);
                    var ring = MeshAt(Geo.Torus(0.04f, 0.012f, 6, 12), M.energy, -0.1f, -0.32f, 0); ring.rotation.x = Mathf.PI / 2; l.joint.add(ring);
                    ex.pistons.Add(ring);
                    l.end.add(MeshAt(RBox(0.36f, 0.14f, 0.26f, 0.05f), M.trim, 0.07f, -0.03f));
                }
                // The Rampart: a tower shield posed by the animation in body space
                var shield = G(0.45f, 0.95f, 0.42f); S.body.add(shield);
                shield.add(MeshAt(RBox(0.7f, 1.12f, 0.1f, 0.06f), M.trim, 0, 0, -0.02f));
                shield.add(MeshAt(RBox(0.6f, 1.0f, 0.1f, 0.05f), M.@base, 0, 0, 0.02f));
                foreach (var y in new[] { 0.5f, -0.5f }) shield.add(MeshAt(RBox(0.56f, 0.04f, 0.02f, 0.01f), M.energy, 0, y, 0.075f));
                foreach (var x in new[] { 0.33f, -0.33f }) shield.add(MeshAt(RBox(0.035f, 0.9f, 0.02f, 0.01f), M.energy, x, 0, 0.06f));
                var crest = G(0, 0.12f, 0.08f); shield.add(crest);
                crest.add(MeshAt(Geo.Cylinder(0.1f, 0.1f, 0.03f, 18), M.energy)).rotation.x = Mathf.PI / 2;
                foreach (var sx in new[] { 1f, -1f }) { var h = MeshAt(Geo.Torus(0.11f, 0.024f, 6, 16, Mathf.PI * 1.3f), M.energy, sx * 0.1f, 0.06f, 0); h.rotation.z = sx > 0 ? -0.4f : Mathf.PI + 0.4f; crest.add(h); }
                var edge = G(0.32f, 0.56f, 0); shield.add(edge);
                ex.shield = shield; ex.shieldEdge = edge;
                // The Breach Cannon on the right shoulder: it turns to the aim
                var cannon = G(-0.08f, 0.84f, -0.3f); S.spine.add(cannon);
                cannon.add(MeshAt(RBox(0.26f, 0.18f, 0.2f, 0.05f), M.trim));
                var barrel = MeshAt(Geo.Cylinder(0.06f, 0.075f, 0.5f, 14), M.under, 0.3f, 0.02f, 0); barrel.rotation.z = -Mathf.PI / 2; cannon.add(barrel);
                var band = MeshAt(Geo.Torus(0.075f, 0.018f, 6, 14), M.energy, 0.42f, 0.02f, 0); band.rotation.y = Mathf.PI / 2; cannon.add(band);
                var muzzle = G(0.58f, 0.02f, 0); cannon.add(muzzle);
                ex.cannon = cannon; ex.muzzle = muzzle;
                ex.edges["shield"] = new Edge(shield, edge, 0.2f); ex.edges["fistF"] = new Edge(armF.joint, armF.end, 0.5f); ex.edges["fistN"] = new Edge(armN.joint, armN.end, 0.5f);
            }
            else
            {
                S = Skeleton(charId, M, 0.06f, 0.08f, 0.28f, 0.125f);
                TObj hips = S.hips, spine = S.spine, head = S.head; Limb armN = S.armN, armF = S.armF, legN = S.legN, legF = S.legF;
                TMat hazard = GlowMat("#ffd23f", 0.35f), skin = new TMat { colorHex = 0xd2ab90, roughness = 0.82f };
                TMat hair = new TMat { colorHex = 0x5a2d22, roughness = 0.75f }, lens = new TMat { colorHex = 0xffb547, emissiveHex = 0xff8a1a, emissiveIntensity = 0.6f, roughness = 0.15f, metalness = 0.3f };
                hips.add(MeshAt(RBox(0.32f, 0.18f, 0.36f, 0.06f), M.trim, 0, 0.02f));
                foreach (var (x, z) in new[] { (0.12f, 0.2f), (-0.08f, 0.21f) }) hips.add(MeshAt(RBox(0.09f, 0.11f, 0.07f, 0.02f), z > 0.2f ? hazard : M.under, x, 0.0f, z));
                spine.add(MeshAt(RBox(0.28f, 0.3f, 0.32f, 0.08f), M.under, 0, 0.18f));
                spine.add(MeshAt(RBox(0.34f, 0.32f, 0.42f, 0.09f), M.@base, 0.02f, 0.42f));
                spine.add(MeshAt(RBox(0.05f, 0.05f, 0.3f, 0.02f), M.energy, 0.19f, 0.44f));
                spine.add(MeshAt(RBox(0.36f, 0.035f, 0.02f, 0.01f), hazard, 0.02f, 0.32f, 0.215f));
                foreach (var z in new[] { 0.26f, -0.26f }) spine.add(MeshAt(RBox(0.2f, 0.12f, 0.17f, 0.06f), M.trim, 0, 0.55f, z));
                head.add(MeshAt(Geo.Sphere(0.13f, 22, 16), skin, 0, 0.1f));
                var eye = new TMat { colorHex = 0x1a1a20, roughness = 0.4f };
                foreach (var z in new[] { 0.042f, -0.042f }) head.add(MeshAt(Geo.Sphere(0.016f, 8, 6), eye, 0.118f, 0.115f, z));
                var cap = MeshAt(Geo.Sphere(0.138f, 22, 12, 0, Mathf.PI * 2, 0, Mathf.PI * 0.46f), hair, -0.01f, 0.105f); cap.rotation.z = 0.3f; head.add(cap);
                var tail = G(-0.12f, 0.17f, 0); head.add(tail);
                tail.add(MeshAt(Geo.Capsule(0.045f, 0.2f, 4, 10), hair, -0.03f, -0.12f)).rotation.z = 0.5f;
                ex.ponytail = tail;
                var strap = MeshAt(Geo.Torus(0.134f, 0.012f, 6, 24), M.trim, 0, 0.17f); strap.rotation.set(Mathf.PI / 2, 0.25f, 0); head.add(strap);
                foreach (var z in new[] { 0.05f, -0.05f })
                {
                    var gg = MeshAt(Geo.Cylinder(0.042f, 0.042f, 0.05f, 14), M.trim, 0.1f, 0.205f, z); gg.rotation.z = Mathf.PI / 2 - 0.6f; head.add(gg);
                    var ln = MeshAt(Geo.Cylinder(0.034f, 0.034f, 0.012f, 14), lens, 0.128f, 0.225f, z); ln.rotation.z = Mathf.PI / 2 - 0.6f; head.add(ln);
                }
                spine.add(MeshAt(RBox(0.18f, 0.34f, 0.32f, 0.05f), M.trim, -0.24f, 0.42f));
                spine.add(MeshAt(RBox(0.04f, 0.3f, 0.02f, 0.01f), hazard, -0.33f, 0.42f, 0.162f));
                var crane = G(-0.26f, 0.62f, -0.08f); spine.add(crane);
                crane.add(MeshAt(RBox(0.06f, 0.26f, 0.06f, 0.02f), M.under, 0, 0.12f));
                var fore = G(0, 0.24f, 0); crane.add(fore);
                fore.add(MeshAt(RBox(0.05f, 0.22f, 0.05f, 0.02f), hazard, 0, 0.1f));
                var claw = G(0, 0.22f, 0); fore.add(claw);
                foreach (var sx in new[] { 1f, -1f }) { var f = MeshAt(RBox(0.02f, 0.08f, 0.03f, 0.008f), M.energy, sx * 0.025f, 0.04f); f.rotation.z = -sx * 0.4f; claw.add(f); }
                crane.rotation.z = 1.9f; fore.rotation.z = -2.6f;
                ex.crane = crane; ex.craneFore = fore;
                foreach (var a in new[] { armN, armF }) a.end.add(MeshAt(Geo.Sphere(0.068f, 12, 10), M.under, 0, -0.03f));
                var gun = G(0, -0.14f, 0); armN.joint.add(gun);
                gun.add(MeshAt(RBox(0.14f, 0.3f, 0.17f, 0.04f), M.trim, 0.02f, 0));
                gun.add(MeshAt(RBox(0.06f, 0.12f, 0.11f, 0.02f), hazard, 0.1f, 0.05f));
                var gbar = MeshAt(Geo.Cylinder(0.032f, 0.04f, 0.16f, 12), M.under, 0.03f, -0.2f, 0); gun.add(gbar);
                var gmuz = G(0.03f, -0.29f, 0); gun.add(gmuz);
                ex.muzzle = gmuz; ex.gun = gun;
                var welder = G(0, -0.08f, 0); armF.end.add(welder);
                welder.add(MeshAt(Geo.Cylinder(0.03f, 0.05f, 0.14f, 10), M.trim, 0, -0.04f, 0));
                var tipMat = GlowMat(c.energy, 2.6f);
                welder.add(MeshAt(Geo.Cone(0.03f, 0.08f, 10), tipMat, 0, -0.14f, 0)).rotation.z = Mathf.PI;
                var weldTip = G(0, -0.19f, 0); welder.add(weldTip);
                ex.weldTip = weldTip; ex.tipMat = tipMat;
                var steel = new TMat { colorHex = 0xb8c0ca, roughness = 0.35f, metalness = 0.6f };
                var wrench = G(0, -0.03f, 0); armN.end.add(wrench);
                wrench.add(MeshAt(RBox(0.05f, 0.62f, 0.05f, 0.02f), steel, 0, -0.27f));
                wrench.add(MeshAt(RBox(0.06f, 0.12f, 0.06f, 0.02f), M.trim, 0, -0.02f));
                var jaw = G(0, -0.62f, 0); wrench.add(jaw);
                var ring = MeshAt(Geo.Torus(0.085f, 0.032f, 8, 18, Mathf.PI * 1.45f), steel, 0, 0, 0); ring.rotation.z = Mathf.PI * 0.27f; jaw.add(ring);
                jaw.add(MeshAt(RBox(0.12f, 0.03f, 0.035f, 0.01f), M.energy, 0, -0.05f, 0.04f));
                var wtip = G(0, -0.09f, 0); jaw.add(wtip);
                var slung = MeshAt(RBox(0.04f, 0.5f, 0.04f, 0.02f), steel, -0.31f, 0.38f, 0.1f); slung.rotation.z = 0.75f; spine.add(slung);
                ex.wrench = wrench; ex.slung = slung;
                ex.edges["wrench"] = new Edge(wrench, wtip, 0.45f);
                foreach (var l in new[] { legN, legF })
                {
                    l.top.add(MeshAt(RBox(0.18f, 0.24f, 0.18f, 0.06f), M.@base, 0.02f, -0.18f));
                    l.joint.add(MeshAt(RBox(0.17f, 0.28f, 0.17f, 0.05f), M.trim, 0.03f, -0.24f));
                    l.end.add(MeshAt(RBox(0.25f, 0.11f, 0.16f, 0.04f), M.under, 0.06f, -0.02f));
                    l.joint.add(MeshAt(RBox(0.02f, 0.2f, 0.02f, 0.006f), hazard, 0.12f, -0.24f, 0.07f));
                }
            }
            S.extra = ex; S.mats = M; S.@char = charId;
            return FinishRig(S);
        }
    }
}
