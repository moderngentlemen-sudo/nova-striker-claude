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
    }

    public sealed class ModelSkin
    {
        static readonly Dictionary<string, CharacterModel> defs = new Dictionary<string, CharacterModel>();
        public static CharacterModel Def(string charId)
        {
            if (!defs.TryGetValue(charId, out var d)) { d = Resources.Load<CharacterModel>("NovaStriker/Models/" + charId); defs[charId] = d; }
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
            SetRigHidden(on);
            if (!on) return;
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

        // The built-in rig's body parts stay in the scene graph (effects and gear follow their joints) but aren't
        // drawn; glowing and see-through parts are gear and stay
        void SetRigHidden(bool hide)
        {
            if (body == null)
            {
                body = new List<TMesh>();
                rig.root.traverse(o =>
                {
                    if (!(o is TMesh me) || o == rig.ring || o.outline || o == holder) return;
                    var m = me.material;
                    if (m == null || m.transparent || m.emissiveIntensity > 1 || m.kind == TMat.Kind.Basic || m.kind == TMat.Kind.Sprite) return;
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
            if (body != null) foreach (var o in body) o.visible = true;
            ready = false;
        }
    }
}
