// The enemies' and bosses' Blender models (Art/Blender/build_enemy_<type>.py; export_enemy.py writes
// Resources/NovaStriker/Models/enemy_<type>.json), hung on their procedural rigs (EnemyRigs.cs) so the rig's own
// animation still poses them. Each part names the rig node it belongs to (`body`, an EnemyParts field such as
// `torso` or `gun`, a list entry such as `rotors[1]` or `legsW[0].knee`) and is in that node's three.js space.
// The rig's meshes under a modelled node are hidden and the model's added in their place. Where the node is itself a
// mesh (an eye, a core, the state meshes `plates[i]`, `horns[i]`, `tubes[i]` that the rig hides or scales to show
// armour and charge), the part in that mesh's material becomes its geometry, so that logic keeps working.
// Materials map onto the rig's own (plate, joint, energy), so hit flashes and glows still drive them.
using System.Collections.Generic;
using NovaStriker.Game.Three;
using UnityEngine;
using static NovaStriker.Sim.Cfg;

namespace NovaStriker.Game
{
    public static class EnemyModels
    {
        [System.Serializable] sealed class Part { public string node, mat; public float[] p, n, uv; public int[] i; }
        [System.Serializable] sealed class Model { public Part[] parts; }
        sealed class Built { public string node, mat; public Mesh mesh; }
        // (built once per type and shared by every enemy of it; null where a type has no model)
        static readonly Dictionary<string, List<Built>> cache = new Dictionary<string, List<Built>>();
        static Mesh empty;
        static readonly HashSet<string> warned = new HashSet<string>();

        static List<Built> Load(string type, bool low)
        {
            string key = type + (low ? "_lod1" : "");
            if (cache.TryGetValue(key, out var got)) return got;
            List<Built> list = null;
            var src = Resources.Load<TextAsset>("NovaStriker/Models/enemy_" + key);
            if (src == null && low) src = Resources.Load<TextAsset>("NovaStriker/Models/enemy_" + type);
            if (src != null)
            {
                try
                {
                    var model = JsonUtility.FromJson<Model>(src.text);
                    if (model?.parts != null)
                    {
                        list = new List<Built>();
                        foreach (var P in model.parts)
                        {
                            if (P.p == null || P.n == null || P.uv == null || P.i == null || P.p.Length == 0 || P.p.Length % 3 != 0 || P.n.Length != P.p.Length || P.uv.Length != P.p.Length / 3 * 2 || P.i.Length % 3 != 0) throw new System.FormatException("Mesh array lengths");
                            foreach(var f in P.p) if(float.IsNaN(f)||float.IsInfinity(f)) throw new System.FormatException("Nonfinite vertex");
                            foreach(var index in P.i) if(index<0||index>=P.p.Length/3) throw new System.FormatException("Triangle index");
                            var g = new GeoBuilder();
                            for (int k = 0; k < P.p.Length / 3; k++)
                                g.V(new Vector3(P.p[3 * k], P.p[3 * k + 1], P.p[3 * k + 2]), new Vector3(P.n[3 * k], P.n[3 * k + 1], P.n[3 * k + 2]), new Vector2(P.uv[2 * k], P.uv[2 * k + 1]));
                            for (int k = 0; k + 2 < P.i.Length; k += 3) g.T(P.i[k], P.i[k + 1], P.i[k + 2]);
                            list.Add(new Built { node = P.node, mat = P.mat, mesh = g.ToMesh("enemy_" + type + "-" + P.node + "-" + P.mat) });
                        }
                    }
                }
                catch (System.Exception e) { Debug.LogWarning("Model of the " + type + " unreadable; its built-in rig is drawn: " + e.Message); if (list != null) foreach (var b in list) Object.Destroy(b.mesh); list = null; }
            }
            cache[key] = list;
            return list;
        }

        static TObj At<T>(List<T> l, int i) where T : TObj => l != null && i >= 0 && i < l.Count ? l[i] : null;

        // A part's node name -> the rig's object
        static TObj Node(EnemyRig R, string name)
        {
            var P = R.parts;
            if (string.IsNullOrEmpty(name)) return null;
            int b = name.IndexOf('[');
            if (b >= 0)
            {
                int e = name.IndexOf(']', b);
                if (e < 0 || !int.TryParse(name.Substring(b + 1, e - b - 1), out int i)) return null;
                string sub = name.Substring(e + 1);
                switch (name.Substring(0, b))
                {
                    case "plates": return At(P.plates, i);
                    case "horns": return At(P.horns, i);
                    case "tubes": return At(P.tubes, i);
                    case "rotors": return At(P.rotors, i);
                    case "legsC": return At(P.legsC, i);
                    case "legsW":
                        if (P.legsW == null || i < 0 || i >= P.legsW.Count) return null;
                        return sub == ".hip" ? P.legsW[i].hip : sub == ".knee" ? P.legsW[i].knee : null;
                }
                return null;
            }
            switch (name)
            {
                case "body": return R.body;
                case "core": return P.core;
                case "eye": return P.eye;
                case "torso": return P.torso;
                case "shield": return P.shield is TMesh ? null : P.shield;   // (never the Stormcaller's shield bubble)
                case "gun": return P.gun;
                case "muzzle": return P.muzzle;
                case "head": return P.head;
                case "armN": return P.armN;
                case "armF": return P.armF;
                case "arm": return P.arm;
                case "rotor": return P.rotor;
                case "tube": return P.tube;
                case "ram": return P.ram;
                case "chest": return P.chest;
                case "pod": return P.pod;
                case "hammer": return P.hammer;
                case "blade": return P.blade;
                case "hull": return P.hull;
                case "cannon": return P.cannon;
            }
            return null;
        }

        // Every object the rig names: hiding stops at these (each is modelled on its own or keeps its rig meshes)
        static HashSet<TObj> Named(EnemyRig R)
        {
            var P = R.parts; var s = new HashSet<TObj>();
            foreach (var o in new[] { R.body, P.core, P.eye, P.torso, P.shield, P.gun, P.muzzle, P.head, P.armN, P.armF, P.arm, P.rotor, P.tube, P.ram, P.chest, P.pod, P.hammer, P.blade, P.hull, P.cannon })
                if (o != null) s.Add(o);
            if (P.plates != null) foreach (var o in P.plates) s.Add(o);
            if (P.horns != null) foreach (var o in P.horns) s.Add(o);
            if (P.tubes != null) foreach (var o in P.tubes) s.Add(o);
            if (P.rotors != null) foreach (var o in P.rotors) s.Add(o);
            if (P.legsC != null) foreach (var o in P.legsC) s.Add(o);
            if (P.legsW != null) foreach (var (hip, knee) in P.legsW) { s.Add(hip); s.Add(knee); }
            s.Remove(null);
            return s;
        }

        static TMat Mat(EnemyRig R, string name)
        {
            switch (name)
            {
                case "Enemy_Plate": return R.mats.plate;
                case "Enemy_Joint": case "Enemy_Glass": return R.mats.joint;
                case "Enemy_Energy": return R.mats.energy;
            }
            return null;
        }

        // Hides the rig's meshes under a node: its own children and those in unnamed groups below it
        static void Hide(TObj n, HashSet<TObj> named)
        {
            foreach (var c in n.children)
            {
                if (named.Contains(c)) continue;
                if (c is TMesh) c.visible = false;
                else Hide(c, named);
            }
        }

        public static void Apply(EnemyRig R, bool low = false)
        {
            if (R != null) R.modelLow = low;
            if (R == null || R.type == null || !SETTINGS.enemyModels) return;
            var parts = Load(R.type, low);
            if (parts == null || parts.Count == 0) return;
            var named = Named(R);
            var byNode = new Dictionary<TObj, List<Built>>();
            var order = new List<TObj>();
            foreach (var b in parts)
            {
                var n = Node(R, b.node);
                if (n == null || Mat(R, b.mat) == null) { if (warned.Add(R.type)) Debug.LogWarning("Invalid model rig node for " + R.type + ": " + b.node + "; using the built-in rig"); return; }
                if (!byNode.TryGetValue(n, out var l)) { byNode[n] = l = new List<Built>(); order.Add(n); }
                l.Add(b);
            }
            foreach (var n in order) Hide(n, named);   // (all hidden first, so no model mesh is ever hidden)
            foreach (var n in order)
            {
                var self = n as TMesh; bool swapped = false;
                foreach (var b in byNode[n])
                {
                    var tm = Mat(R, b.mat);
                    if (self != null && !swapped && tm == self.material) { self.geometry = b.mesh; swapped = true; continue; }
                    var m = new TMesh(b.mesh, tm) { cast = true };
                    m.go.name = "model";
                    n.add(m);
                }
                if (self != null && !swapped)
                {
                    if (empty == null) empty = new Mesh { name = "enemy-empty" };
                    self.geometry = empty;
                }
            }
        }
    }
}
