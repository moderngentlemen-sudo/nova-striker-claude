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
        public bool helmetOff, helmetKnock;   // (Nova and RAM: the helmet knocked off at critical health: Anim, HelmetFx)
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
            if (charId == "ram") return BuildRamRig();
            if (charId == "fix") return BuildNewRig(charId);
            // Echo, as in his concept art: cream-white armour with bronze-gold trim over a dark charcoal undersuit, orange
            // energy, a bare face with spiky blond hair (the helmet and mask are the other looks: Settings, Echo's head)
            var c = CHARS[charId]; var D = Mats(c);
            var M = RimAll(new RigMats { @base = Armour("#efe7d6", 0.4f, 0.1f), trim = Armour("#c8964a", 0.34f, 0.6f), under = Std("#25242b", 0.62f, 0.15f), energy = D.energy, visor = D.visor, amber = D.amber });
            // These three defaults were replaced by the character-specific armour palette.
            D.@base.DestroyIfUnused(); D.trim.DestroyIfUnused(); D.under.DestroyIfUnused();
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

        // ---- RAM: his concept art ----
        // A towering frame in battle-worn gunmetal over dark joints and undersuit, electric blue light in the seams
        // (the abdomen's bands, the joint discs, the boots, the T visor), a rounded helm whose horns are armoured sensor fins, huge
        // rounded pauldrons, big blocky fists, segmented legs on round joint discs, the Breach Cannon over his right
        // shoulder with its blue muzzle ring, and the Rampart: a heavy stone-grey frame round a glowing hexagonal
        // hard-light panel, with a ram's-head emblem.
        static Rig BuildRamRig()
        {
            var c = CHARS["ram"]; var D = Mats(c);
            var M = RimAll(new RigMats { @base = Armour("#8f98a3", 0.5f, 0.55f), trim = Armour("#2a2f37", 0.45f, 0.5f), under = Std("#15181d", 0.6f, 0.3f), energy = D.energy, visor = D.visor, amber = D.amber });
            // These three defaults were replaced by the character-specific armour palette.
            D.@base.DestroyIfUnused(); D.trim.DestroyIfUnused(); D.under.DestroyIfUnused();
            TMat frame = Armour("#7b838d", 0.65f, 0.35f), core = GlowMat(c.energy, 3.2f);
            var ex = new RigExtra();
            var S = Skeleton("ram", M, 0.095f, 0.11f, 0.42f, 0.16f, 0.31f);
            TObj hips = S.hips, spine = S.spine, head = S.head; Limb armN = S.armN, armF = S.armF, legN = S.legN, legF = S.legF;

            // Pelvis: a dark core under armoured tassets, a glowing belt line
            hips.add(MeshAt(RBox(0.44f, 0.24f, 0.56f, 0.08f), M.under, 0, 0.02f));
            hips.add(MeshAt(RBox(0.14f, 0.24f, 0.32f, 0.05f), M.@base, 0.21f, -0.04f));
            foreach (var z in new[] { 0.27f, -0.27f }) hips.add(MeshAt(RBox(0.34f, 0.26f, 0.08f, 0.04f), M.@base, 0.0f, -0.05f, z));
            hips.add(MeshAt(RBox(0.46f, 0.03f, 0.58f, 0.01f), M.energy, 0, 0.13f));

            // Torso: the abdomen in dark bands with blue light between them, a layered chest, a collar ring
            for (int k = 0; k < 3; k++)
            {
                float y = 0.1f + k * 0.085f, w = 0.4f + k * 0.03f;
                spine.add(MeshAt(RBox(w, 0.065f, 0.5f + k * 0.03f, 0.03f), M.trim, 0.02f, y));
                spine.add(MeshAt(RBox(w - 0.04f, 0.02f, 0.46f + k * 0.03f, 0.008f), M.energy, 0.03f, y + 0.045f));
            }
            spine.add(MeshAt(RBox(0.4f, 0.36f, 0.5f, 0.1f), M.under, 0, 0.2f));
            spine.add(MeshAt(RBox(0.54f, 0.46f, 0.7f, 0.14f), M.@base, 0.03f, 0.47f));
            spine.add(MeshAt(RBox(0.3f, 0.2f, 0.4f, 0.08f), M.@base, 0.2f, 0.53f));
            foreach (var z in new[] { 0.19f, -0.19f })
            {
                var pec = MeshAt(RBox(0.26f, 0.24f, 0.26f, 0.08f), M.@base, 0.12f, 0.46f, z); pec.rotation.x = z > 0 ? 0.12f : -0.12f; spine.add(pec);
                spine.add(MeshAt(RBox(0.04f, 0.12f, 0.02f, 0.008f), M.energy, 0.24f, 0.32f, z * 1.15f));
                spine.add(MeshAt(RBox(0.36f, 0.035f, 0.02f, 0.01f), M.energy, 0.04f, 0.4f, z * 1.84f));   // (side seams, seen in play)
            }
            spine.add(MeshAt(RBox(0.04f, 0.06f, 0.14f, 0.015f), core, 0.33f, 0.38f));
            var collar = MeshAt(Geo.Torus(0.2f, 0.06f, 8, 20), M.trim, 0.02f, 0.69f); collar.rotation.x = Mathf.PI / 2; spine.add(collar);
            // Back: the pack and its exhaust stacks
            spine.add(MeshAt(RBox(0.28f, 0.54f, 0.54f, 0.08f), M.trim, -0.37f, 0.44f));
            foreach (var y in new[] { 0.36f, 0.44f, 0.52f }) spine.add(MeshAt(RBox(0.03f, 0.025f, 0.32f, 0.01f), M.energy, -0.515f, y));
            ex.stacks = new List<TMesh>();
            foreach (var z in new[] { 0.15f, -0.15f })
            {
                var st = MeshAt(Geo.Cylinder(0.065f, 0.075f, 0.36f, 12), M.under, -0.43f, 0.8f, z); st.rotation.z = 0.35f; spine.add(st);
                var rim = MeshAt(Geo.Torus(0.066f, 0.016f, 6, 14), M.energy, -0.49f, 0.97f, z); rim.rotation.set(Mathf.PI / 2, 0.35f, 0); spine.add(rim);
                ex.stacks.Add(rim);
            }
            // Huge rounded pauldrons with a dark rim and a light line
            foreach (var z in new[] { 0.44f, -0.44f })
            {
                var pad = MeshAt(Geo.Sphere(0.23f, 20, 14), M.@base, 0, 0.57f, z); pad.scale.set(1.08f, 0.7f, 0.92f); spine.add(pad);
                var lip = MeshAt(Geo.Torus(0.215f, 0.032f, 8, 24), M.trim, 0, 0.49f, z); lip.rotation.x = Mathf.PI / 2; spine.add(lip);
                spine.add(MeshAt(Geo.Cylinder(0.055f, 0.055f, 0.03f, 16), M.trim, 0.1f, 0.6f, z + Mathf.Sign(z) * 0.2f)).rotation.x = Mathf.PI / 2;
                spine.add(MeshAt(RBox(0.26f, 0.03f, 0.02f, 0.01f), M.energy, 0, 0.53f, z + Mathf.Sign(z) * 0.215f));
            }

            // Heads: the helmet (a rounded helm with a crest, the T visor in blue, a jaw guard, and the horn fins), knocked
            // off at critical health, and under it his face: a weathered veteran with a grey buzz cut and short grey beard,
            // a scar through his left brow, and a cybernetic right eye glowing blue (it reads the fins' sensors)
            head.position.set(0.13f, 0.74f, 0);
            var heads = new Dictionary<string, TObj>();
            var skin = Std("#8a5a3e", 0.62f, 0); var grey = Std("#8d8f93", 0.8f, 0); var scar = Std("#b07a5e", 0.55f, 0);
            var eye = new TMat { colorHex = 0x2a1a12, roughness = 0.4f };
            var bare = G(0, 0.02f, 0); bare.scale.setScalar(1.12f); head.add(bare);
            bare.add(MeshAt(Geo.Cylinder(0.075f, 0.085f, 0.12f, 14), skin, 0, -0.05f));
            var skull = MeshAt(Geo.Sphere(0.13f, 24, 18), skin, 0.01f, 0.065f); skull.scale.set(1, 1.1f, 0.95f); bare.add(skull);
            bare.add(MeshAt(RBox(0.15f, 0.1f, 0.21f, 0.05f), skin, 0.045f, 0.0f));                       // a square jaw
            bare.add(MeshAt(RBox(0.1f, 0.075f, 0.225f, 0.035f), grey, 0.075f, -0.025f));                 // short beard
            var cut = MeshAt(Geo.Sphere(0.136f, 24, 12, 0, Mathf.PI * 2, 0, Mathf.PI * 0.42f), grey, 0.008f, 0.07f); cut.rotation.z = 0.25f; bare.add(cut);
            bare.add(MeshAt(RBox(0.035f, 0.028f, 0.2f, 0.012f), skin, 0.118f, 0.105f));                  // heavy brow
            bare.add(MeshAt(RBox(0.032f, 0.055f, 0.034f, 0.012f), skin, 0.138f, 0.062f));                 // nose
            foreach (var z in new[] { 0.128f, -0.128f }) bare.add(MeshAt(Geo.Sphere(0.028f, 8, 6), skin, 0, 0.065f, z));
            bare.add(MeshAt(Geo.Sphere(0.012f, 8, 6), eye, 0.123f, 0.083f, -0.046f));
            var mark = MeshAt(RBox(0.006f, 0.07f, 0.008f, 0.003f), scar, 0.13f, 0.095f, -0.03f); mark.rotation.x = 0.35f; bare.add(mark);
            bare.add(MeshAt(RBox(0.03f, 0.05f, 0.05f, 0.012f), M.trim, 0.118f, 0.085f, 0.048f));          // the implant's housing
            bare.add(MeshAt(Geo.Sphere(0.016f, 10, 8), core, 0.13f, 0.083f, 0.048f));
            var helmet = G(); head.add(helmet);
            var helm = MeshAt(Geo.Sphere(0.17f, 20, 16), M.@base, 0, 0.07f); helm.scale.set(1.05f, 1, 0.92f); helmet.add(helm);
            helmet.add(MeshAt(RBox(0.2f, 0.04f, 0.06f, 0.015f), M.@base, -0.02f, 0.23f));
            helmet.add(MeshAt(RBox(0.12f, 0.12f, 0.24f, 0.04f), M.trim, 0.1f, -0.03f));
            helmet.add(MeshAt(RBox(0.04f, 0.035f, 0.22f, 0.012f), M.energy, 0.165f, 0.07f));
            helmet.add(MeshAt(RBox(0.04f, 0.1f, 0.035f, 0.01f), M.energy, 0.165f, 0.02f));
            // The horns, made to work: armoured sensor fins sweeping back and down round the helm (as in Art/Blender's
            // model): four overlapping gunmetal blade plates over a dark core, a blue channel along their outer face, a
            // sensor pod at the tip (the Breach Cannon's targeting array) and a powered actuator disc at the temple
            foreach (var sz in new[] { 1f, -1f })
            {
                float[] px = new float[9], py = new float[9], pz = new float[9];   // (plain numbers: RigPreview converts this to JavaScript)
                for (int k = 0; k < 9; k++)
                {
                    float t = k / 8f, a = Mathf.PI * (0.42f + 1.05f * t), r = 0.19f * (1 - 0.25f * t);
                    px[k] = -0.015f - (Mathf.Cos(a) * r * 1.05f + 0.08f) / 1.34f; py[k] = 0.134f + (Mathf.Sin(a) * r - 0.04f) / 1.34f; pz[k] = sz * (0.149f + (0.06f + 0.07f * t) / 1.34f);
                }
                helmet.add(MeshAt(Geo.Cylinder(0.06f, 0.06f, 0.045f, 16), M.trim, -0.015f, 0.134f, sz * 0.17f)).rotation.x = Mathf.PI / 2;
                for (int k = 0; k < 4; k++)
                {
                    int a0 = 2 * k, a1 = 2 * k + 2;
                    float dx = px[a1] - px[a0], dy = py[a1] - py[a0], dl = Mathf.Sqrt(dx * dx + dy * dy), rz = Mathf.Atan2(-dx, dy);
                    float mx = (px[a0] + px[a1]) / 2 + dx * 0.08f, my = (py[a0] + py[a1]) / 2 + dy * 0.08f, mz = (pz[a0] + pz[a1]) / 2;
                    var plate = MeshAt(RBox(0.1f - k * 0.016f, dl * 1.2f, 0.037f - k * 0.004f, 0.012f), M.@base, mx, my, mz); plate.rotation.z = rz; helmet.add(plate);
                    var coreP = MeshAt(RBox(0.07f - k * 0.011f, dl, 0.022f, 0.006f), M.trim, (px[a0] + px[a1]) / 2, (py[a0] + py[a1]) / 2, mz); coreP.rotation.z = rz; helmet.add(coreP);
                    var glowP = MeshAt(RBox(0.012f, dl * 1.08f, 0.006f, 0.003f), M.energy, mx, my, mz + sz * (0.02f - k * 0.002f)); glowP.rotation.z = rz; helmet.add(glowP);
                }
                float tx = px[8] - px[7], ty = py[8] - py[7], tl = Mathf.Sqrt(tx * tx + ty * ty);
                helmet.add(MeshAt(Geo.Sphere(0.032f, 10, 8), M.trim, px[8] + tx / tl * 0.03f, py[8] + ty / tl * 0.03f, pz[8]));
                helmet.add(MeshAt(Geo.Sphere(0.022f, 10, 8), core, px[8] + tx / tl * 0.05f, py[8] + ty / tl * 0.05f, pz[8]));
            }
            heads["bare"] = bare; heads["helmet"] = helmet;

            // Arms: dark sleeves on round joint discs, massive gauntlets, big blocky fists
            foreach (var a in new[] { armN, armF })
            {
                float sz = Mathf.Sign(a.top.position.z);
                a.top.add(MeshAt(RBox(0.24f, 0.22f, 0.24f, 0.06f), M.@base, 0.01f, -0.16f));
                var disc = MeshAt(Geo.Cylinder(0.1f, 0.1f, 0.06f, 18), M.trim, 0, 0, sz * 0.1f); disc.rotation.x = Mathf.PI / 2; a.joint.add(disc);
                var glow = MeshAt(Geo.Cylinder(0.045f, 0.045f, 0.062f, 14), core, 0, 0, sz * 0.1f); glow.rotation.x = Mathf.PI / 2; a.joint.add(glow);
                a.joint.add(MeshAt(RBox(0.26f, 0.32f, 0.28f, 0.07f), M.@base, 0.01f, -0.16f));
                a.joint.add(MeshAt(RBox(0.02f, 0.2f, 0.04f, 0.008f), M.energy, 0.145f, -0.16f));
                a.end.add(MeshAt(RBox(0.22f, 0.2f, 0.22f, 0.06f), M.trim, 0.01f, -0.05f));
                a.end.add(MeshAt(RBox(0.08f, 0.06f, 0.2f, 0.02f), M.under, 0.1f, -0.1f));
            }
            foreach (var z in new[] { 0.07f, -0.07f })
            {
                armF.joint.add(MeshAt(Geo.Cylinder(0.03f, 0.03f, 0.3f, 10), M.under, -0.1f, -0.14f, z));
                armF.joint.add(MeshAt(Geo.Torus(0.036f, 0.012f, 6, 12), M.energy, -0.1f, -0.2f, z));
            }

            // Legs: segmented plates over round hip and knee discs, pistons behind the shins, heavy boots lit at the toe
            ex.pistons = new List<TMesh>();
            foreach (var l in new[] { legN, legF })
            {
                float sz = Mathf.Sign(l.top.position.z);
                var hipD = MeshAt(Geo.Cylinder(0.11f, 0.11f, 0.06f, 18), M.trim, 0, -0.02f, sz * 0.12f); hipD.rotation.x = Mathf.PI / 2; l.top.add(hipD);
                l.top.add(MeshAt(RBox(0.3f, 0.17f, 0.3f, 0.07f), M.@base, 0.02f, -0.12f));
                l.top.add(MeshAt(RBox(0.28f, 0.15f, 0.28f, 0.06f), M.@base, 0.03f, -0.3f));
                var knee = MeshAt(Geo.Cylinder(0.11f, 0.11f, 0.07f, 18), M.trim, 0.02f, 0, sz * 0.12f); knee.rotation.x = Mathf.PI / 2; l.joint.add(knee);
                var kglow = MeshAt(Geo.Cylinder(0.05f, 0.05f, 0.072f, 14), core, 0.02f, 0, sz * 0.12f); kglow.rotation.x = Mathf.PI / 2; l.joint.add(kglow);
                l.joint.add(MeshAt(RBox(0.16f, 0.14f, 0.24f, 0.05f), M.@base, 0.12f, -0.02f));
                l.joint.add(MeshAt(RBox(0.27f, 0.34f, 0.27f, 0.07f), M.@base, 0.04f, -0.25f));
                var pis = MeshAt(Geo.Cylinder(0.035f, 0.035f, 0.32f, 10), M.trim, -0.11f, -0.22f, 0); l.joint.add(pis);
                var ring = MeshAt(Geo.Torus(0.04f, 0.012f, 6, 12), M.energy, -0.11f, -0.32f, 0); ring.rotation.x = Mathf.PI / 2; l.joint.add(ring);
                ex.pistons.Add(ring);
                l.end.add(MeshAt(RBox(0.38f, 0.15f, 0.28f, 0.05f), M.trim, 0.07f, -0.03f));
                l.end.add(MeshAt(RBox(0.14f, 0.1f, 0.26f, 0.04f), M.@base, 0.2f, 0.02f));
                l.end.add(MeshAt(RBox(0.03f, 0.03f, 0.22f, 0.01f), M.energy, 0.275f, 0.0f));
            }

            // The Rampart: posed by the animation in body space. A heavy stone-grey frame (bars and corner blocks)
            // round a glowing hexagonal hard-light panel, the ram's-head emblem at its heart.
            var shield = G(0.45f, 0.95f, 0.42f); S.body.add(shield);
            var panel = new TMat { colorCss = "#1f4fae", emissiveCss = c.energy, emissiveIntensity = 1.8f, map = HexTexture(), emissiveMap = HexTexture(), roughness = 0.2f, transparent = true, opacity = 0.88f };
            var face = MeshAt(Geo.Plane(0.62f, 1.02f), panel, 0, 0, 0.05f); shield.add(face);
            shield.add(MeshAt(RBox(0.6f, 1.0f, 0.06f, 0.02f), M.under, 0, 0, -0.01f));
            foreach (var y in new[] { 0.53f, -0.53f }) shield.add(MeshAt(RBox(0.78f, 0.1f, 0.14f, 0.03f), frame, 0, y, 0.02f));
            foreach (var x in new[] { 0.35f, -0.35f }) shield.add(MeshAt(RBox(0.1f, 1.12f, 0.14f, 0.03f), frame, x, 0, 0.02f));
            foreach (var (x, y) in new[] { (0.36f, 0.54f), (-0.36f, 0.54f), (0.36f, -0.54f), (-0.36f, -0.54f) }) shield.add(MeshAt(RBox(0.16f, 0.16f, 0.17f, 0.03f), frame, x, y, 0.03f));
            foreach (var y in new[] { 0.22f, -0.22f }) foreach (var x in new[] { 0.35f, -0.35f }) shield.add(MeshAt(RBox(0.13f, 0.12f, 0.16f, 0.025f), frame, x, y, 0.03f));
            var crest = G(0, 0.06f, 0.1f); shield.add(crest);
            // the ram's-head emblem: a tapering face, its glowing eyes, and two great curled horns
            var muzzleE = MeshAt(RBox(0.16f, 0.3f, 0.05f, 0.05f), frame, 0, -0.03f); crest.add(muzzleE);
            crest.add(MeshAt(RBox(0.22f, 0.12f, 0.05f, 0.04f), frame, 0, 0.1f));
            foreach (var sx in new[] { 1f, -1f })
            {
                crest.add(MeshAt(RBox(0.04f, 0.018f, 0.02f, 0.006f), M.energy, sx * 0.055f, 0.08f, 0.03f));
                var h = MeshAt(Geo.Torus(0.12f, 0.035f, 8, 18, Mathf.PI * 1.45f), frame, sx * 0.15f, 0.08f, 0); h.rotation.z = sx > 0 ? -0.55f : Mathf.PI + 0.55f; crest.add(h);
            }
            var edge = G(0.32f, 0.56f, 0); shield.add(edge);
            ex.shield = shield; ex.shieldEdge = edge;
            // The Breach Cannon over his right shoulder: it turns to the aim
            var cannon = G(-0.08f, 0.84f, -0.3f); S.spine.add(cannon);
            cannon.add(MeshAt(RBox(0.28f, 0.2f, 0.22f, 0.05f), M.trim));
            var barrel = MeshAt(Geo.Cylinder(0.065f, 0.08f, 0.52f, 14), M.under, 0.3f, 0.02f, 0); barrel.rotation.z = -Mathf.PI / 2; cannon.add(barrel);
            foreach (var x in new[] { 0.2f, 0.34f }) { var bd = MeshAt(Geo.Cylinder(0.088f, 0.088f, 0.05f, 14), M.@base, x, 0.02f, 0); bd.rotation.z = -Mathf.PI / 2; cannon.add(bd); }
            cannon.add(MeshAt(RBox(0.16f, 0.05f, 0.05f, 0.015f), M.@base, 0.12f, 0.13f));
            var band = MeshAt(Geo.Torus(0.078f, 0.02f, 8, 16), core, 0.55f, 0.02f, 0); band.rotation.y = Mathf.PI / 2; cannon.add(band);
            var muzzle = G(0.58f, 0.02f, 0); cannon.add(muzzle);
            ex.cannon = cannon; ex.muzzle = muzzle;
            ex.edges["shield"] = new Edge(shield, edge, 0.2f); ex.edges["fistF"] = new Edge(armF.joint, armF.end, 0.5f); ex.edges["fistN"] = new Edge(armN.joint, armN.end, 0.5f);
            S.extra = ex; S.mats = M; S.@char = "ram"; S.heads = heads;
            var rig = FinishRig(S);
            rig.setHeadFn = mode =>
            {
                if (rig.headMode == mode) return;
                rig.headMode = mode; helmet.visible = mode == "helmet"; bare.visible = mode != "helmet";
            };
            rig.setHead(SETTINGS.ramHead ?? "helmet");
            return rig;
        }

        // The Rampart's hard light: bright hexagon edges over a deeper glow (tileable, made once)
        static Texture2D hexTex;
        static Texture2D HexTexture()
        {
            if (hexTex != null) return hexTex;
            const int w = 128, h = 222; var px = new Color32[w * h];
            float R = 21;   // (cell radius in pixels: about three across the panel's width)
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    // distance to the nearest hexagon edge on a pointy-top grid
                    float best = 1e9f, second = 1e9f;
                    float cw = Mathf.Sqrt(3) * R, ch = 1.5f * R;
                    for (int j = -1; j <= Mathf.CeilToInt(h / ch) + 1; j++)
                        for (int i = -1; i <= Mathf.CeilToInt(w / cw) + 1; i++)
                        {
                            float cx = i * cw + (j % 2 != 0 ? cw / 2 : 0), cy = j * ch;
                            float d = (x - cx) * (x - cx) + (y - cy) * (y - cy);
                            if (d < best) { second = best; best = d; } else if (d < second) second = d;
                        }
                    float edgeD = (Mathf.Sqrt(second) - Mathf.Sqrt(best)) / 2;
                    float line = Mathf.Clamp01(1 - edgeD / 2.2f), inner = 0.35f + 0.25f * Mathf.Clamp01(edgeD / R);
                    float v = Mathf.Max(line, inner);
                    byte b = (byte)Mathf.RoundToInt(255 * v);
                    px[y * w + x] = new Color32(b, b, b, 255);
                }
            hexTex = new Texture2D(w, h, TextureFormat.RGBA32, true, false) { wrapMode = TextureWrapMode.Repeat, name = "rampart-hex" };
            hexTex.SetPixels32(px); hexTex.Apply(true, true);
            return hexTex;
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

        // ---- Fix (and the skeleton RAM and Fix share) ----
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
