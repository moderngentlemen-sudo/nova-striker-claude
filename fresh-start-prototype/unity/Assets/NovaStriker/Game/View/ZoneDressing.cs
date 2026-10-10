// The zones' dressing (Art/Blender): modelled kits laid over the level and behind it. The Movement Gym's comes from
// build_gym_kit.py and gym_layout.py, the other zones' from build_zone_kit.py and zone_layout.py (placed from the
// level's real boxes, hazards, catwalks and lane stretches); export_kit.py writes each as
// Resources/NovaStriker/Env/<zone>_kit.json. Facade panels over the decks' faces, deck plating and markings, and each
// zone's props: the gym's training gear and sign, the Concourse Lock's balustrades, holo columns and lock-gate frames,
// the Storm Spire's weather station, the Skyline Relay's relay towers and dishes, the Helix Foundry's furnaces,
// crucibles and cranes on lower terraces, the Undercity's fire escapes, neon signs, poles and street clutter. It only
// dresses: the level's boxes, and so the play, are unchanged. Its meshes are in three.js space like the game's own
// and are baked with the rest of the level. Painted, worn, rubber, tread and concrete surfaces come from Look's
// texture sets, tiled in world space.
using System.Collections.Generic;
using NovaStriker.Game.Three;
using UnityEngine;
using UnityEngine.UI;

namespace NovaStriker.Game
{
    public static class ZoneDressing
    {
        [System.Serializable] sealed class Part { public string asset, mat; public float[] p, n, uv; public int[] i; }
        [System.Serializable] sealed class Place { public string asset; public float x, y, dz, yaw, sx, sy, sz; }
        [System.Serializable] sealed class Kit { public Part[] parts; public Place[] place; }

        // Flat markings, plating and contact shadows sit on surfaces: they neither cast shadows nor need to (the
        // gym's by name; the other zones' pieces are named Flat*)
        static readonly HashSet<string> FLAT = new HashSet<string> { "StartLine", "Chevrons", "HazardEdge", "DeckFloor", "DeckFloorDrain", "Shadow" };
        static bool Flat(string asset) => FLAT.Contains(asset) || asset.StartsWith("Flat", System.StringComparison.Ordinal);

        // Builds a zone's dressing from its kit file (gym, arena, tower, skyline, foundry, undercity); a zone without
        // one is drawn as before
        public static void Build(View view, string zone)
        {
            var src = Resources.Load<TextAsset>("NovaStriker/Env/" + zone + "_kit");
            if (src == null) return;
            Kit kit;
            try { kit = JsonUtility.FromJson<Kit>(src.text); }
            catch (System.Exception e) { Debug.LogWarning("Dressing for '" + zone + "' unreadable; the zone is drawn without it: " + e.Message); return; }
            if (kit?.parts == null || kit.place == null) return;
            if (zone == "gym") Ticker();
            var byAsset = new Dictionary<string, List<(Mesh m, TMat mat)>>();
            foreach (var P in kit.parts)
            {
                var g = new GeoBuilder();
                for (int k = 0; k < P.p.Length / 3; k++)
                    g.V(new Vector3(P.p[3 * k], P.p[3 * k + 1], P.p[3 * k + 2]), new Vector3(P.n[3 * k], P.n[3 * k + 1], P.n[3 * k + 2]), new Vector2(P.uv[2 * k], P.uv[2 * k + 1]));
                for (int k = 0; k < P.i.Length; k += 3) g.T(P.i[k], P.i[k + 1], P.i[k + 2]);
                var mat = MatFor(P.mat);
                if (!byAsset.TryGetValue(P.asset, out var list)) byAsset[P.asset] = list = new List<(Mesh, TMat)>();
                list.Add((g.ToMesh(zone + "-kit-" + P.asset), mat));
            }
            foreach (var L in kit.place)
            {
                if (!byAsset.TryGetValue(L.asset, out var list)) continue;
                var pos = S.W(L.x, L.y, L.dz); var rot = new Vector3(0, S.YawAt(L.x) + L.yaw * Mathf.Deg2Rad, 0);
                bool cast = !Flat(L.asset);
                foreach (var (m, mat) in list) view.Bake(m, mat, pos, rot, new Vector3(L.sx, L.sy, L.sz), cast && mat.kind != TMat.Kind.Basic && !mat.transparent);
            }
        }

        // The holo displays and agility rings flicker and shimmer; the signs breathe; warning lamps blink; furnace
        // vents glow and dim; the Undercity's neon stutters now and then
        // A scrolling stats ticker under the gym's sign: a world-space panel whose text slides through a masked window
        const float SIGN_X = 28, SIGN_Y = 8, SIGN_D = -15, TICKER_W = 1180, TICKER_H = 44;   // (the sign's place, gym_layout.py; the panel in pixels at 1 cm each)
        const string TICKER = "MOVEMENT GYM  ·  WALL JUMPS TODAY 1,284  ·  BEST DASH CHAIN 41  ·  SLIDE TUNNEL RECORD 2.31 s  ·  " +
                              "LIFT PADS ONLINE  ·  STRETCH BEFORE YOU SPRINT  ·  SKYPORT WINDS 12 KM/H  ·  ";
        static RectTransform tickerText;
        static void Ticker()
        {
            var go = new GameObject("gym ticker", typeof(RectTransform), typeof(Canvas));
            go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)go.transform; rt.sizeDelta = new Vector2(TICKER_W, TICKER_H); rt.localScale = Vector3.one * 0.01f;
            var f = Sim.Level.Frame(SIGN_X);
            rt.position = S.ToUnity(S.W(SIGN_X, SIGN_Y - 1.3f, SIGN_D + 0.4f));
            rt.rotation = Quaternion.LookRotation(S.ToUnity(new Vector3((float)-f.nx, 0, (float)-f.nz)), Vector3.up);   // (facing the camera)
            var bg = go.AddComponent<Image>(); bg.color = new Color(0.05f, 0.09f, 0.16f, 0.92f); bg.raycastTarget = false;
            var win = new GameObject("window", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
            win.SetParent(rt, false); win.anchorMin = Vector2.zero; win.anchorMax = Vector2.one; win.offsetMin = new Vector2(12, 2); win.offsetMax = new Vector2(-12, -2);
            var t = new GameObject("text", typeof(RectTransform)).AddComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); t.fontSize = 30; t.fontStyle = FontStyle.Bold; t.raycastTarget = false;
            t.color = new Color(0.6f, 0.93f, 1f); t.alignment = TextAnchor.MiddleLeft;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.text = TICKER + TICKER;   // (twice, so it wraps round without a gap)
            tickerText = t.rectTransform; tickerText.SetParent(win, false);
            tickerText.anchorMin = tickerText.anchorMax = tickerText.pivot = new Vector2(0, 0.5f);
            tickerText.sizeDelta = new Vector2(t.preferredWidth, TICKER_H);
        }

        public static void Animate(float t)
        {
            if (tickerText != null) { float half = tickerText.sizeDelta.x * 0.5f; tickerText.anchoredPosition = new Vector2(-Mathf.Repeat(t * 90, Mathf.Max(1, half)), 0); }
            if (mats.TryGetValue("Kit_Holo", out var h))
            {
                float flick = Mathf.PerlinNoise(t * 9, 0.3f) > 0.86f ? 0.45f : 1;
                h.emissiveIntensity = (1.5f + 0.35f * Mathf.Sin(t * 2.2f) + 0.15f * Mathf.Sin(t * 13)) * flick;
                h.opacity = 0.62f + 0.12f * Mathf.Sin(t * 3.1f + 1);
            }
            if (mats.TryGetValue("Kit_Sign", out var s)) s.emissiveIntensity = 1.35f + 0.45f * (0.5f + 0.5f * Mathf.Sin(t * 1.4f));
            if (mats.TryGetValue("Kit_Warn", out var w)) { float b = Mathf.Repeat(t * 1.1f, 1); w.emissiveIntensity = b < 0.5f ? 2.6f : 0.35f; }
            if (mats.TryGetValue("Kit_Furnace", out var f)) f.emissiveIntensity = 2.3f + 0.5f * Mathf.Sin(t * 1.3f) + 0.35f * (Mathf.PerlinNoise(t * 2.5f, 0.7f) - 0.5f);
            if (mats.TryGetValue("Kit_Neon", out var n)) n.emissiveIntensity = Mathf.PerlinNoise(t * 7, 0.1f) > 0.82f ? 0.4f : 2.3f + 0.15f * Mathf.Sin(t * 31);
        }

        static readonly Dictionary<string, TMat> mats = new Dictionary<string, TMat>();
        static TMat MatFor(string name)
        {
            if (mats.TryGetValue(name, out var t)) return t;
            TMat Paint(string c, float rough, float metal, float tile = 2.5f) => Look.ApplySurface(new TMat { colorCss = c, roughness = rough, metalness = metal }, "paint", tile, 0.6f);
            TMat Worn(string c, float rough, float metal, float tile = 2f) => Look.ApplySurface(new TMat { colorCss = c, roughness = rough, metalness = metal }, "worn", tile, 0.6f);
            TMat Concrete(string c, float rough) => Look.ApplySurface(new TMat { colorCss = c, roughness = rough }, "concrete", 3f, 0.6f);
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
                // Storm Spire: wet steel (glossy), grating, copper conductors, cables
                "Kit_Steel" => Paint("#4a5563", 0.18f, 0.7f, 2f),
                "Kit_SteelB" => Paint("#55606e", 0.2f, 0.7f, 2f),
                "Kit_SteelLight" => Paint("#7d8a99", 0.22f, 0.7f, 2f),
                "Kit_Grate" => Look.ApplySurface(new TMat { colorCss = "#2c333d", roughness = 0.45f, metalness = 0.5f }, "tread", 0.6f, 1f),
                "Kit_Copper" => Paint("#b86b3c", 0.3f, 0.9f, 1.5f),
                "Kit_Cable" => Look.ApplySurface(new TMat { colorCss = "#1a1c20", roughness = 0.7f }, "rubber", 0.5f, 0.6f),
                // lamps and lit things (Kit_Warn blinks, Kit_Furnace breathes, Kit_Neon stutters: Animate)
                "Kit_Warn" => Glow("#ff5a3a", 2.4f),
                "Kit_Amber" => Glow("#ffc060", 2.0f),
                "Kit_WinLit" => Glow("#ffd9a0", 1.4f),
                "Kit_Furnace" => Glow("#ff8a2a", 2.4f),
                "Kit_Molten" => Glow("#ffc060", 3.0f),
                "Kit_Neon" => Glow("#ff7ad9", 2.3f),
                "Kit_NeonB" => Glow("#7fe9ff", 2.2f),
                "Kit_Window" => new TMat { colorCss = "#1d2a3d", roughness = 0.08f, metalness = 0.3f },
                // Helix Foundry: battle-worn gunmetal and rust
                "Kit_Gunmetal" => Worn("#3a4049", 0.45f, 0.5f),
                "Kit_GunmetalB" => Worn("#474d57", 0.5f, 0.5f),
                "Kit_Rust" => Worn("#7a4a32", 0.75f, 0.3f, 1.5f),
                // Undercity: concrete, plaster, wet paving and puddles, painted bins, crates
                "Kit_Concrete" => Concrete("#8d8f99", 0.85f),
                "Kit_Plaster" => Concrete("#7a7682", 0.8f),
                "Kit_Pave" => Concrete("#4d525b", 0.6f),
                "Kit_Wet" => new TMat { colorCss = "#5a6272", roughness = 0.04f, metalness = 0.6f },
                "Kit_Dumpster" => Worn("#2f6b4f", 0.5f, 0.3f, 1.5f),
                "Kit_Wood" => Worn("#a87444", 0.8f, 0f, 1f),
                _ => new TMat { colorCss = "#e9eef3", roughness = 0.45f },
            };
            mats[name] = t;
            return t;
        }
    }
}
