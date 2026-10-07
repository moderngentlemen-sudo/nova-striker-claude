// The Movement Gym's dressing (Art/Blender: build_gym_kit.py models it, gym_layout.py places it, export_kit.py
// writes both to Resources/NovaStriker/Env/gym_kit.json): hull panels over the deck's faces, deck plating and
// markings, dressing on the wall-jump panel and the slide tunnel, and training gear, a railing, floodlights and the
// sign on the back terrace. It only dresses: the level's boxes, and so the play, are unchanged. Its meshes are in
// three.js space like the game's own and are baked with the rest of the level. Painted, rubber and tread surfaces
// come from Look's texture sets (paint, rubber, tread), tiled in world space.
using System.Collections.Generic;
using NovaStriker.Game.Three;
using UnityEngine;

namespace NovaStriker.Game
{
    public static class GymDressing
    {
        [System.Serializable] sealed class Part { public string asset, mat; public float[] p, n, uv; public int[] i; }
        [System.Serializable] sealed class Place { public string asset; public float x, y, dz, yaw, sx, sy, sz; }
        [System.Serializable] sealed class Kit { public Part[] parts; public Place[] place; }

        // Flat markings and contact shadows sit on surfaces: they neither cast shadows nor need to
        static readonly HashSet<string> FLAT = new HashSet<string> { "StartLine", "Chevrons", "HazardEdge", "DeckFloor", "DeckFloorDrain", "Shadow" };

        public static void Build(View view)
        {
            var src = Resources.Load<TextAsset>("NovaStriker/Env/gym_kit");
            if (src == null) return;
            Kit kit;
            try { kit = JsonUtility.FromJson<Kit>(src.text); }
            catch (System.Exception e) { Debug.LogWarning("Gym dressing unreadable; the gym is drawn without it: " + e.Message); return; }
            var byAsset = new Dictionary<string, List<(Mesh m, TMat mat)>>();
            foreach (var P in kit.parts)
            {
                var g = new GeoBuilder();
                for (int k = 0; k < P.p.Length / 3; k++)
                    g.V(new Vector3(P.p[3 * k], P.p[3 * k + 1], P.p[3 * k + 2]), new Vector3(P.n[3 * k], P.n[3 * k + 1], P.n[3 * k + 2]), new Vector2(P.uv[2 * k], P.uv[2 * k + 1]));
                for (int k = 0; k < P.i.Length; k += 3) g.T(P.i[k], P.i[k + 1], P.i[k + 2]);
                var mat = MatFor(P.mat);
                if (!byAsset.TryGetValue(P.asset, out var list)) byAsset[P.asset] = list = new List<(Mesh, TMat)>();
                list.Add((g.ToMesh("kit-" + P.asset), mat));
            }
            foreach (var L in kit.place)
            {
                if (!byAsset.TryGetValue(L.asset, out var list)) continue;
                var pos = S.W(L.x, L.y, L.dz); var rot = new Vector3(0, S.YawAt(L.x) + L.yaw * Mathf.Deg2Rad, 0);
                bool cast = !FLAT.Contains(L.asset);
                foreach (var (m, mat) in list) view.Bake(m, mat, pos, rot, new Vector3(L.sx, L.sy, L.sz), cast && mat.kind != TMat.Kind.Basic && !mat.transparent);
            }
        }

        // The holo displays and agility rings flicker and shimmer; the sign breathes
        public static void Animate(float t)
        {
            if (mats.TryGetValue("Kit_Holo", out var h))
            {
                float flick = Mathf.PerlinNoise(t * 9, 0.3f) > 0.86f ? 0.45f : 1;
                h.emissiveIntensity = (1.5f + 0.35f * Mathf.Sin(t * 2.2f) + 0.15f * Mathf.Sin(t * 13)) * flick;
                h.opacity = 0.62f + 0.12f * Mathf.Sin(t * 3.1f + 1);
            }
            if (mats.TryGetValue("Kit_Sign", out var s)) s.emissiveIntensity = 1.35f + 0.45f * (0.5f + 0.5f * Mathf.Sin(t * 1.4f));
        }

        static readonly Dictionary<string, TMat> mats = new Dictionary<string, TMat>();
        static TMat MatFor(string name)
        {
            if (mats.TryGetValue(name, out var t)) return t;
            TMat Paint(string c, float rough, float metal, float tile = 2.5f) => Look.ApplySurface(new TMat { colorCss = c, roughness = rough, metalness = metal }, "paint", tile, 0.6f);
            TMat Glow(string c, float k) => new TMat { colorCss = c, emissiveCss = c, emissiveIntensity = k, roughness = 0.3f };
            t = name switch
            {
                "Kit_Hull" => Paint("#e9eef3", 0.3f, 0.1f),
                "Kit_HullB" => Paint("#dce3eb", 0.34f, 0.1f),
                "Kit_HullDark" => Paint("#3d4f66", 0.4f, 0.2f),
                "Kit_Navy" => Paint("#2b4f7e", 0.32f, 0.15f),
                "Kit_Metal" => Paint("#9aa6b2", 0.3f, 0.85f, 1.5f),
                "Kit_Pad" => Look.ApplySurface(new TMat { colorCss = "#2fb5c9", roughness = 0.8f }, "rubber", 0.8f, 0.8f),
                "Kit_PadOrange" => Look.ApplySurface(new TMat { colorCss = "#f08a3c", roughness = 0.8f }, "rubber", 0.8f, 0.8f),
                "Kit_Grip" => Look.ApplySurface(new TMat { colorCss = "#7d8794", roughness = 0.9f, metalness = 0.4f }, "tread", 0.5f, 1f),
                "Kit_Light" => Glow("#8fecff", 2.2f),
                "Kit_Holo" => new TMat { colorCss = "#7fe3ff", emissiveCss = "#5fd8ff", emissiveIntensity = 1.8f, transparent = true, opacity = 0.75f, roughness = 0.2f },
                "Kit_Sign" => Glow("#ffffff", 1.6f),
                "Kit_Hazard" => new TMat { colorCss = "#f2c230", roughness = 0.55f },
                "Kit_Black" => new TMat { colorCss = "#14181f", roughness = 0.6f },
                "Kit_Seam" => new TMat { colorCss = "#9aa6b4", roughness = 0.6f },
                "Kit_Stencil" => new TMat { colorCss = "#5d6b7d", roughness = 0.6f },
                "Kit_Glass" => new TMat { colorCss = "#cfeeff", roughness = 0.05f, metalness = 0.1f, transparent = true, opacity = 0.28f, depthWrite = false },
                "Kit_Shadow" => new TMat(TMat.Kind.Basic) { colorHex = 0x000000, map = Look.ShadowTexture(), transparent = true, opacity = 0.55f, depthWrite = false },
                _ => new TMat { colorCss = "#e9eef3", roughness = 0.45f },
            };
            mats[name] = t;
            return t;
        }
    }
}
