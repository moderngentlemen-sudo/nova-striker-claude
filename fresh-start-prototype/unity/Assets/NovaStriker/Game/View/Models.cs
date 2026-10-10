// Character models (models.js): the path from authored, skinned and animated 3D models into the game. The
// procedural rig keeps running underneath, invisibly, so everything that follows its joints (effects, weapon
// muzzles, ghosts) still works; the model is drawn in its place and plays the clip that matches the character's
// state, cross-fading between them.
//
// To bring in a character: import the model (FBX, or glTF/GLB through the glTFast package), then create a
// Character Model asset (Assets > Create > Nova Striker > Character Model) at Resources/NovaStriker/Models/<id>
// (nova, echo, ram or fix): its prefab, the turn that makes it face +x (every rig's forward), colour overrides by
// material name, and the clips for each state. Settings > Character models then switches between the built-in
// rigs and the models. Until such an asset exists the built-in rig is drawn.
//
// A model can instead copy the built-in rig's pose every frame (driveFromRig), so it moves exactly as the rig does
// with no clips at all: every state, the aim, the turns. Its bones must carry the rig's joint names (root, hips,
// spine, head, upperarm/forearm/hand and thigh/shin/foot, .L and .R), as the Art/Blender scripts make them. A model
// file at Resources/NovaStriker/Models/<id>_model (Nova's nova_model.fbx and RAM's ram_model.fbx come with the
// project) is used this way with no asset to set up. Its materials are swapped, by name, for the rig's own (so
// Nova's gold seams pulse with his charge and RAM's armour wears the rig's battle-worn finish), and Nova's helmet
// and face come and go with the rig's head look.
using System.Collections.Generic;
using NovaStriker.Game.Three;
using NovaStriker.Sim;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using static NovaStriker.Sim.Cfg;

namespace NovaStriker.Game
{
    [CreateAssetMenu(menuName = "Nova Striker/Character Model", fileName = "nova")]
    public sealed class CharacterModel : ScriptableObject
    {
        [System.Serializable] public struct Tint { public string material; public Color color; }
        [System.Serializable] public struct StateClips { public string state; public AnimationClip[] clips; }
        public GameObject prefab;
        public string credit;
        [Tooltip("Turn about the vertical axis (degrees) so the model faces +x, the way every rig faces")] public float turn = 90;
        public Tint[] tint = new Tint[0];
        [Tooltip("idle, walk, run, jump, fall, land, dash, attack, shoot, hurt, down, victory: the first clip that exists is used")]
        public StateClips[] clips = new StateClips[0];
        [Tooltip("States whose clip plays through once and holds")] public string[] once = { "jump", "attack", "shoot", "hurt", "down" };
        [Tooltip("Pose the skeleton from the built-in rig's joints every frame instead of playing clips (bones named as the Art/Blender scripts name them)")]
        public bool driveFromRig;
    }

    public sealed class ModelSkin
    {
        static readonly Dictionary<string, CharacterModel> defs = new Dictionary<string, CharacterModel>();
        public static CharacterModel Def(string charId)
        {
            if (!defs.TryGetValue(charId, out var d))
            {
                d = Resources.Load<CharacterModel>("NovaStriker/Models/" + charId);
                if (d == null)   // a bare model file driven by the rig
                {
                    var go = Resources.Load<GameObject>("NovaStriker/Models/" + charId + "_model");
                    if (go != null) { d = ScriptableObject.CreateInstance<CharacterModel>(); d.prefab = go; d.driveFromRig = true; d.name = charId + "_model"; }
                }
                defs[charId] = d;
            }
            return d != null && d.prefab != null ? d : null;
        }
        public static bool HasModel(string charId) => Def(charId) != null;

        static string StateOf(Player p)
        {
            string st = p.state;
            if (st == "dead" || st == "downed") return "down";
            if (st == "hitstun") return "hurt";
            if (st == "dash" || st == "slide" || st == "dodge" || st == "rush" || st == "zip") return "dash";
            if (st == "attack" || st == "dashslash" || st == "vb" || st == "pound" || st == "lash") return "attack";
            if (p.shootT > 0 || p.chargeT > 0) return "shoot";
            if (!p.onGround) return p.vy > 0 ? "jump" : "fall";
            double s = System.Math.Abs(p.vx);
            return s > 4.5 ? "run" : s > 0.6 ? "walk" : "idle";
        }

        readonly Rig rig; readonly CharacterModel D; readonly string @char;
        public bool ready, failed;
        TObj holder; GameObject model;
        PlayableGraph graph; AnimationMixerPlayable mixer;
        readonly Dictionary<string, int> slot = new Dictionary<string, int>();
        readonly List<AnimationClipPlayable> plays = new List<AnimationClipPlayable>();
        readonly HashSet<string> once = new HashSet<string>();
        float[] weight; int cur = -1; string curState;
        readonly List<Renderer> renderers = new List<Renderer>();
        readonly List<(Material m, float w)> outlineMats = new List<(Material, float)>();
        List<TMesh> body; bool rigHidden;
        readonly List<(TObj model, TObj on)> gearModels = new List<(TObj, TObj)>();   // (RAM's modelled Rampart and Breach Cannon: BuildGear)

        public ModelSkin(Rig rig, string charId)
        {
            this.rig = rig; @char = charId; D = Def(charId);
            if (D == null) { failed = true; return; }
            try { Build(); } catch (System.Exception e) { failed = true; Debug.LogWarning("Character model failed to load; keeping the built-in rig: " + e); }
        }

        void Build()
        {
            model = Object.Instantiate(D.prefab);
            model.name = "model-" + @char;
            holder = Group.Make(0, 0, 0, "model");
            model.transform.SetParent(holder.tr, false);
            if (D.driveFromRig) { BuildDriven(); return; }
            // stand it up: feet on the ground, as tall as the character, facing +x
            var b = new Bounds(); bool any = false;
            foreach (var r in model.GetComponentsInChildren<Renderer>()) { if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
            float h = any ? b.size.y : 1; if (h <= 0) h = 1;
            float k = (float)CHARS[@char].height / h;
            model.transform.localScale *= k;
            model.transform.localPosition = new Vector3(0, -(any ? b.min.y : 0) * k, 0);
            // (three.js's +y turn is a -y turn once mirrored into Unity)
            model.transform.localRotation = Quaternion.Euler(0, -D.turn, 0);
            foreach (var r in model.GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                if (r is SkinnedMeshRenderer sk) sk.updateWhenOffscreen = true;
                var mats = r.sharedMaterials; var list = new List<Material>();
                foreach (var m in mats)
                {
                    if (m == null) continue;
                    var c = new Material(m);
                    foreach (var t in D.tint) if (t.material == m.name) { if (c.HasProperty("_BaseColor")) c.SetColor("_BaseColor", t.color); else if (c.HasProperty("_Color")) c.SetColor("_Color", t.color); }
                    if (c.HasProperty("_Smoothness")) c.SetFloat("_Smoothness", Mathf.Max(c.GetFloat("_Smoothness"), 0.55f));
                    list.Add(c);
                }
                // the outline follows the skeleton too: an extra pass on the same renderer, its width set from
                // how big the part is drawn against its own units
                var om = new Material(Templates.Outline);
                float units = Mathf.Max(1e-4f, r.transform.lossyScale.magnitude / Mathf.Sqrt(3));
                om.SetColor("_BaseColor", Th.Hex(0x0b0f18)); om.SetFloat("_Width", 0.016f / units);
                list.Add(om); outlineMats.Add((om, 0.016f / units));
                r.sharedMaterials = list.ToArray();
                renderers.Add(r);
            }
            // clips: one playable per state, mixed by weight
            var anim = model.GetComponentInChildren<Animator>() ?? model.AddComponent<Animator>();
            graph = PlayableGraph.Create("model-" + @char);
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var output = AnimationPlayableOutput.Create(graph, "anim", anim);
            var states = new List<(string s, AnimationClip c)>();
            foreach (var sc in D.clips)
            {
                AnimationClip clip = null;
                foreach (var c in sc.clips) if (c != null) { clip = c; break; }
                if (clip != null) states.Add((sc.state, clip));
            }
            mixer = AnimationMixerPlayable.Create(graph, states.Count);
            output.SetSourcePlayable(mixer);
            foreach (var s in D.once) once.Add(s);
            for (int i = 0; i < states.Count; i++)
            {
                var pl = AnimationClipPlayable.Create(graph, states[i].c);
                if (once.Contains(states[i].s)) pl.SetDuration(states[i].c.length);
                graph.Connect(pl, 0, mixer, i);
                mixer.SetInputWeight(i, 0);
                slot[states[i].s] = i; plays.Add(pl);
            }
            weight = new float[states.Count];
            graph.Play();
            rig.flip.add(holder);   // (under the flip, so it turns to face the way the character faces)
            ready = true;
        }

        // Each frame: hide the built-in meshes, play the right clip
        public void Update(Player p, float dt, bool on)
        {
            if (!ready) return;
            holder.visible = on;
            foreach (var (g, _) in gearModels) g.visible = on;
            SetRigHidden(on);
            if (!on) return;
            if (D.driveFromRig) { Drive(); return; }
            string want = StateOf(p);
            int next = slot.TryGetValue(want, out var n) ? n : slot.TryGetValue("idle", out var id) ? id : -1;
            if (next >= 0 && next != cur)
            {
                plays[next].SetTime(0); plays[next].SetDone(false);
                cur = next; curState = want;
            }
            // cross-fade over 0.12 s
            for (int i = 0; i < weight.Length; i++)
            {
                weight[i] = Mathf.MoveTowards(weight[i], i == cur ? 1 : 0, dt / 0.12f);
                mixer.SetInputWeight(i, weight[i]);
            }
            if (cur >= 0)
            {
                // running plays faster with speed; everything else at its own pace
                double speed = curState == "run" ? System.Math.Max(0.8, System.Math.Abs(p.vx) / 6.5) : curState == "dash" ? 1.8 : 1;
                plays[cur].SetSpeed(speed);
                if (once.Contains(curState) && plays[cur].GetTime() >= plays[cur].GetAnimationClip().length) plays[cur].SetTime(plays[cur].GetAnimationClip().length);
            }
            graph.Evaluate(dt);
            // (no ink line while the Veil fades the body: a zero-width shell hides behind it)
            bool inked = rig.cloak < 0.05f;
            foreach (var (m, w) in outlineMats) m.SetFloat("_Width", inked ? w : 0);
        }

        // ---- Driven by the rig ----
        // Each mapped bone keeps the rotation it has in the model's rest pose, turned so its limb lies the way the
        // rig's does at rest (arms and legs straight down, the spine up); each frame it takes the rig joint's rotation
        // on top of that. Everything is measured in the holder's space, under the rig's flip, so the mirror that turns
        // the rig round never enters a rotation.
        sealed class Bone { public Transform b; public TObj j; public Quaternion offset; public Vector3 restPos; }
        readonly List<Bone> bones = new List<Bone>();
        Bone hipsBone; float hipScale = 1;
        public Transform helmet, face;
        static readonly (string bone, string child, int dir)[] MAP =
        {
            ("hips", null, 0), ("spine", "head", 1), ("head", null, 0),
            ("upperarm.R", "forearm.R", -1), ("forearm.R", "hand.R", -1), ("hand.R", null, 0),
            ("upperarm.L", "forearm.L", -1), ("forearm.L", "hand.L", -1), ("hand.L", null, 0),
            ("thigh.R", "shin.R", -1), ("shin.R", "foot.R", -1), ("foot.R", null, 0),
            ("thigh.L", "shin.L", -1), ("shin.L", "foot.L", -1), ("foot.L", null, 0),
        };
        TObj JointOf(string bone) => bone switch
        {
            "hips" => rig.hips, "spine" => rig.spine, "head" => rig.head,
            "upperarm.R" => rig.armN.top, "forearm.R" => rig.armN.joint, "hand.R" => rig.armN.end,
            "upperarm.L" => rig.armF.top, "forearm.L" => rig.armF.joint, "hand.L" => rig.armF.end,
            "thigh.R" => rig.legN.top, "shin.R" => rig.legN.joint, "foot.R" => rig.legN.end,
            "thigh.L" => rig.legF.top, "shin.L" => rig.legF.joint, "foot.L" => rig.legF.end,
            _ => null,
        };
        static Transform Find(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t) { var f = Find(c, name); if (f != null) return f; }
            return null;
        }
        Matrix4x4 InHolder(Transform t) => holder.tr.worldToLocalMatrix * t.localToWorldMatrix;
        Matrix4x4 InFlip(TObj o) => rig.flip.tr.worldToLocalMatrix * o.tr.localToWorldMatrix;
        Vector3 HolderPos(Transform t) => InHolder(t).GetColumn(3);

        void BuildDriven()
        {
            var mt = model.transform;
            Transform B(string n) => Find(mt, n);
            var hips = B("hips"); var footR = B("foot.R"); var footL = B("foot.L"); var armR = B("upperarm.R");
            if (hips == null || footR == null || armR == null) throw new System.Exception("the model's skeleton lacks the rig's bone names");
            // face +x (the rig's forward) with his right side toward +z in three.js space, which is -z here
            Vector3 fwd = Vector3.ProjectOnPlane(Child(footR) - HolderPos(footR), Vector3.up);
            if (fwd.sqrMagnitude > 1e-8f) mt.localRotation = Quaternion.AngleAxis(Vector3.SignedAngle(fwd, Vector3.right, Vector3.up), Vector3.up) * mt.localRotation;
            if (HolderPos(armR).z > HolderPos(hips).z) { var sc = mt.localScale; sc.z = -sc.z; mt.localScale = sc; }
            // stand him on the ground with his hips at the rig's hip height
            float floor = float.MaxValue;
            foreach (var r in model.GetComponentsInChildren<Renderer>()) floor = Mathf.Min(floor, holder.tr.InverseTransformPoint(r.bounds.min).y);
            float hipH = HolderPos(hips).y - floor;
            float k = hipH > 0.1f ? 0.95f / hipH : 1;
            mt.localScale *= k; mt.localPosition = new Vector3(0, -floor * k, 0);
            var hp = HolderPos(hips); mt.localPosition += new Vector3(-hp.x, 0, -hp.z);
            hipScale = HolderPos(hips).y / 0.95f;
            foreach (var (bn, child, dir) in MAP)
            {
                var b = B(bn); var j = JointOf(bn);
                if (b == null || j == null) continue;
                var q = InHolder(b).rotation;
                if (dir != 0)
                {
                    var c = B(child); var d = (c != null ? HolderPos(c) : Child(b)) - HolderPos(b);
                    if (d.sqrMagnitude > 1e-8f) q = Quaternion.FromToRotation(d.normalized, Vector3.up * dir) * q;
                }
                var e = new Bone { b = b, j = j, offset = q, restPos = HolderPos(b) };
                bones.Add(e); if (bn == "hips") hipsBone = e;
            }
            helmet = B(@char[0].ToString().ToUpper() + @char.Substring(1) + "_Helmet");
            face = B(@char[0].ToString().ToUpper() + @char.Substring(1) + "_FaceAndHair");
            SkinMaterials();
            BuildGear("shield", rig.extra.shield); BuildGear("cannon", rig.extra.cannon);
            rig.flip.add(holder);
            ready = true;
        }
        // RAM's Rampart and Breach Cannon as modelled in Blender (build_ram_shield.py, build_ram_cannon.py;
        // export_ram_gear.py writes Resources/NovaStriker/Models/<id>_<gear>.json in the group's own three.js space):
        // each hangs on the rig's own group, so the game poses and aims it as before, in its place. The shield's
        // glowing hard-light panel stays the rig's, inside the modelled frame.
        [System.Serializable] sealed class GearPart { public string mat; public float[] p, n, uv; public int[] i; }
        [System.Serializable] sealed class Gear { public GearPart[] parts; }
        void BuildGear(string name, TObj on)
        {
            var src = Resources.Load<TextAsset>("NovaStriker/Models/" + @char + "_" + name);
            if (src == null || on == null) return;
            Gear gear;
            try { gear = JsonUtility.FromJson<Gear>(src.text); }
            catch (System.Exception e) { Debug.LogWarning("Model of RAM's " + name + " unreadable; the built-in one is drawn: " + e.Message); return; }
            if (gear?.parts == null) return;
            var model = Group.Make(0, 0, 0, name + "-model");
            foreach (var P in gear.parts)
            {
                var g = new GeoBuilder();
                for (int k = 0; k < P.p.Length / 3; k++)
                    g.V(new Vector3(P.p[3 * k], P.p[3 * k + 1], P.p[3 * k + 2]), new Vector3(P.n[3 * k], P.n[3 * k + 1], P.n[3 * k + 2]), new Vector2(P.uv[2 * k], P.uv[2 * k + 1]));
                for (int k = 0; k < P.i.Length; k += 3) g.T(P.i[k], P.i[k + 1], P.i[k + 2]);
                var tm = MatFor(P.mat) ?? TMat.Std(0x7b838d, 0.65f, 0.35f);
                model.add(new TMesh(g.ToMesh(name + "-" + P.mat), tm) { cast = true });
            }
            Look.AddOutlines(model, 0x0b0f18, 0.016f);
            on.add(model); gearModels.Add((model, on));
        }
        // where a bone's first child sits (its tail), or a step along it when it has none
        Vector3 Child(Transform b) => b.childCount > 0 ? HolderPos(b.GetChild(0)) : HolderPos(b) + (Vector3)(InHolder(b).GetColumn(1)) * 0.1f;

        void Drive()
        {
            foreach (var e in bones)
            {
                var R = InFlip(e.j).rotation;
                var want = R * e.offset;
                var parent = e.b.parent;
                var P = parent != null ? InHolder(parent).rotation : Quaternion.identity;
                e.b.localRotation = Quaternion.Inverse(P) * want;
                if (e == hipsBone)
                {
                    Vector3 rigPos = InFlip(e.j).GetColumn(3), delta = rigPos - new Vector3(0, 0.95f, 0);
                    var target = e.restPos + delta * hipScale;
                    e.b.localPosition = parent != null ? InHolder(parent).inverse.MultiplyPoint3x4(target) : target;
                }
            }
            bool helm = rig.headMode == "helmet";
            if (helmet != null && helmet.gameObject.activeSelf != helm) helmet.gameObject.SetActive(helm);
            if (face != null && face.gameObject.activeSelf == helm) face.gameObject.SetActive(!helm);
            foreach (var (r, slots) in matSlots)
            {
                var ms = r.sharedMaterials; bool dirty = false;
                foreach (var (i, tm) in slots) if (ms[i] != tm.m) { ms[i] = tm.m; dirty = true; }
                if (dirty) r.sharedMaterials = ms;
            }
            bool inked = rig.cloak < 0.05f;
            foreach (var (m, w) in outlineMats) m.SetFloat("_Width", inked ? w : 0);
        }

        // The model's materials, by name, become the rig's own where they match (the armour, suit and gold seams
        // then glow and rim-light exactly as the rig's do) or game materials made to match
        readonly List<(Renderer r, List<(int i, TMat tm)> slots)> matSlots = new List<(Renderer, List<(int, TMat)>)>();
        static readonly Dictionary<string, TMat> extraMats = new Dictionary<string, TMat>();
        TMat MatFor(string name)
        {
            var M = rig.mats;
            switch (name.Substring(name.IndexOf('_') + 1))   // (Nova_Armour, Ram_Armour, ...)
            {
                case "Armour": return M.@base;
                case "Suit": return M.under;
                case "SuitPanel": case "Trim": return M.trim;
                case "GoldGlow": case "Glow": return M.energy;
            }
            if (extraMats.TryGetValue(name, out var t)) return t;
            t = name switch
            {
                "Nova_GoldMetal" => TMat.Std(0xd9a54e, 0.25f, 0.95f),
                "Nova_Glove" => TMat.Std(0x1a1d26, 0.55f, 0.1f),
                "Nova_Sole" => TMat.Std(0x20232b, 0.7f, 0.05f),
                "Nova_Skin" => TMat.Std(0xd6a07e, 0.6f, 0),
                "Nova_Hair" => TMat.Std(0x2b1a12, 0.5f, 0.05f),
                "Nova_EyeWhite" => TMat.Std(0xf2eee8, 0.25f, 0),
                "Nova_Iris" => TMat.Std(0x3a2414, 0.15f, 0.1f),
                "Nova_Lip" => TMat.Std(0xb77a62, 0.5f, 0),
                "Nova_PanelLine" => TMat.Std(0x9aa3ad, 0.5f, 0.2f),
                "Nova_Visor" => new TMat { colorCss = "#ffc867", emissiveCss = "#ff9d1a", emissiveIntensity = 0.5f, roughness = 0.12f, metalness = 0.85f },
                "Ram_Skin" => TMat.Std(0x6b412c, 0.55f, 0),
                "Ram_Hair" => TMat.Std(0x4f5257, 0.75f, 0.05f),
                "Ram_EyeWhite" => TMat.Std(0xece6dc, 0.25f, 0),
                "Ram_Iris" => TMat.Std(0x3a2414, 0.15f, 0.1f),
                "Ram_Lip" => TMat.Std(0x7a4a36, 0.5f, 0),
                "Ram_Scar" => TMat.Std(0x9a6650, 0.45f, 0),
                "Ram_Frame" => Worn(new TMat(TMat.Kind.Physical) { colorCss = "#7b838d", roughness = 0.65f, metalness = 0.35f, clearcoat = 0.65f, clearcoatRoughness = 0.18f }),
                _ => null,
            };
            if (t != null) { t.retained = true; extraMats[name] = t; }
            return t;
        }
        static TMat Worn(TMat m) { Look.Wear(m); return m; }
        void SkinMaterials()
        {
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                if (r is SkinnedMeshRenderer sk) sk.updateWhenOffscreen = true;
                var src = r.sharedMaterials; var list = new List<Material>(); var slots = new List<(int, TMat)>();
                for (int i = 0; i < src.Length; i++)
                {
                    var m = src[i]; if (m == null) continue;
                    var tm = MatFor(m.name.Replace(" (Instance)", ""));
                    if (tm != null) { slots.Add((list.Count, tm)); list.Add(tm.m); } else list.Add(new Material(m));
                }
                var om = new Material(Templates.Outline);
                float units = Mathf.Max(1e-4f, r.transform.lossyScale.magnitude / Mathf.Sqrt(3));
                om.SetColor("_BaseColor", Th.Hex(0x0b0f18)); om.SetFloat("_Width", 0.016f / units);
                list.Add(om); outlineMats.Add((om, 0.016f / units));
                r.sharedMaterials = list.ToArray();
                renderers.Add(r); matSlots.Add((r, slots));
            }
        }

        // The built-in rig's body parts stay in the scene graph (effects and gear follow their joints) but aren't
        // drawn; glowing and see-through parts are gear and stay
        void SetRigHidden(bool hide)
        {
            if (body == null)
            {
                body = new List<TMesh>();
                // (a driven model has its own glowing seams and emitter, so the rig's go too, and RAM's model its own
                // core, stack rims and piston lights; the gear stays: the shield, the hard light, jets, skate blades,
                // the attachment light, and RAM's Rampart and Breach Cannon)
                var gear = new HashSet<TObj>();
                var ex = rig.extra;
                foreach (var g in new TObj[] { ex.shield, ex.greave, ex.module, ex.cannon }) g?.traverse(o => gear.Add(o));
                // (modelled gear takes the place of the rig's: only the shield's see-through hard-light panel stays)
                foreach (var (g, on) in gearModels) { on.traverse(o => gear.Remove(o)); g.traverse(o => gear.Add(o)); }
                foreach (var l in new[] { ex.jets, ex.blades, ex.gauntlets }) if (l != null) foreach (var g in l) gear.Add(g);
                rig.root.traverse(o =>
                {
                    if (!(o is TMesh me) || o == rig.ring || o.outline || o == holder || gear.Contains(o)) return;
                    var m = me.material;
                    if (m == null || m.kind == TMat.Kind.Basic || m.kind == TMat.Kind.Sprite) return;
                    bool glow = m.transparent || m.emissiveIntensity > 1;
                    if (glow && !(D.driveFromRig && (m == rig.mats.energy || (rig.@char == "ram" && !m.transparent)))) return;
                    body.Add(me);
                });
            }
            if (hide) foreach (var o in body) o.visible = false;
            else if (rigHidden) foreach (var o in body) o.visible = true;
            rigHidden = hide;
        }

        public void Dispose()
        {
            if (graph.IsValid()) graph.Destroy();
            if (holder != null) holder.destroy();
            foreach (var (g, _) in gearModels) g.destroy();
            if (body != null) foreach (var o in body) o.visible = true;
            ready = false;
        }
    }
}
