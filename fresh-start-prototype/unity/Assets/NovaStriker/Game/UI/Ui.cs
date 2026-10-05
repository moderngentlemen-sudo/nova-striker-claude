// The interface (ui.js) on a screen-space uGUI canvas: the start screen, the HUD (a panel per player in the
// corners), markers over the players, lock-on reticles, the rocket jump height, sparring post labels, character
// lines, banners and toasts, the boss bar, the ultimate's letterbox and name, the pause menu with every setting,
// the controls screen and the debug readout. Positions over the world come from View.ScreenOf (top-left pixels).
using System;
using System.Collections.Generic;
using System.Linq;
using NovaStriker.Sim;
using UnityEngine;
using UnityEngine.UI;
using static NovaStriker.Sim.Cfg;

namespace NovaStriker.Game.UI
{
    public sealed class UiHandlers
    {
        public Action resume; public Action<string> zone, boss; public Action<Player, string> pick; public Action<Player> remove;
    }

    public sealed partial class Ui
    {
        readonly UiHandlers H;
        public readonly Canvas canvas;
        readonly RectTransform root, hud, labels;
        public bool paused, helpOpen;
        float bannerT, toastT;
        readonly RectTransform banner, toastEl; readonly Text bannerTitle, bannerSub, toastText;
        RectTransform orderEl; Text orderText; string orderKey = "";
        readonly Text debugText;

        static Color PC(int slot) => Pal.C(PLAYER_COLORS[slot]);
        static string Mark(Player p) => $"{PLAYER_MARKS[p.slot]} P{p.slot + 1}";
        float Scale => canvas.scaleFactor > 0 ? canvas.scaleFactor : 1;
        Vector2 ToCanvas(float x, float y) => new Vector2(x / Scale, -y / Scale);

        public Ui(UiHandlers handlers)
        {
            H = handlers;
            var go = new GameObject("Interface");
            canvas = go.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 10;
            var sc = go.AddComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1280, 720); sc.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            root = (RectTransform)go.transform;
            // Ultimate presentation (letterbox, name, join prompts) sits under the HUD panels
            BuildUlt();
            hud = W.Fill(W.Rect(root, "hud"));
            labels = W.Fill(W.Rect(root, "labels"));
            banner = W.At(W.Rect(root, "banner"), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -100), new Vector2(520, 70));
            W.Box(banner, Pal.plate).rectTransform.SetParent(banner, false); W.Fill(banner.GetChild(0) as RectTransform);
            var bb = W.Box(banner, Pal.warm, "edge", false); bb.rectTransform.anchorMin = Vector2.zero; bb.rectTransform.anchorMax = new Vector2(1, 0); bb.rectTransform.pivot = new Vector2(0.5f, 0); bb.rectTransform.sizeDelta = new Vector2(0, 4);
            bannerTitle = W.Txt(banner, "", 36, Pal.text, true, FontStyle.BoldAndItalic, TextAnchor.UpperCenter); W.Fill(bannerTitle.rectTransform).offsetMax = new Vector2(0, -6);
            bannerSub = W.Txt(banner, "", 14, Pal.muted, false, FontStyle.Normal, TextAnchor.LowerCenter); W.Fill(bannerSub.rectTransform).offsetMin = new Vector2(0, 10);
            banner.gameObject.SetActive(false);
            toastEl = W.At(W.Rect(root, "toast"), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 22), new Vector2(600, 32));
            var tb = W.Box(toastEl, Pal.plate); W.Fill(tb.rectTransform);
            toastText = W.Txt(toastEl, "", 14, Pal.text, false, FontStyle.Normal, TextAnchor.MiddleCenter); W.Fill(toastText.rectTransform);
            toastEl.gameObject.SetActive(false);
            BuildBossBar();
            var dbg = W.At(W.Rect(root, "debug"), new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -120), new Vector2(1240, 400));
            debugText = W.Txt(dbg, "", 11, Pal.C("#d7f0ff")); debugText.font = W.Mono; W.Fill(debugText.rectTransform);
            W.Shadow(debugText, new Color(0, 0, 0, 0.8f), new Vector2(1, -1));
            dbg.gameObject.SetActive(false);
            BuildStart(); BuildPause(); BuildHelp();
        }

        // ---- In-game messages ----
        public void ShowBanner(string text, string sub)
        {
            bannerTitle.text = (text ?? "").ToUpperInvariant(); bannerSub.text = sub ?? "";
            banner.sizeDelta = new Vector2(Mathf.Max(bannerTitle.preferredWidth, bannerSub.preferredWidth) + 52, sub != null ? 74 : 56);
            banner.gameObject.SetActive(true); bannerT = 2.8f;
        }
        // The team command in force (Bots), shown above the toast line
        public void SetOrder(string name, int slot)
        {
            string key = name != null ? name + slot : "";
            if (key == orderKey) return; orderKey = key;
            if (orderEl == null)
            {
                orderEl = W.At(W.Rect(root, "order"), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 60), new Vector2(420, 28));
                W.Fill(W.Box(orderEl, Pal.plate).rectTransform);
                orderText = W.Txt(orderEl, "", 13, Pal.muted, false, FontStyle.Normal, TextAnchor.MiddleCenter); W.Fill(orderText.rectTransform);
            }
            orderEl.gameObject.SetActive(name != null);
            if (name != null) { orderText.text = $"<b><color={W.Hex(PC(slot))}>{PLAYER_MARKS[slot]} P{slot + 1}</color></b>  Team order · <b><color=#f3f7fc>{name.ToUpperInvariant()}</color></b>"; orderEl.sizeDelta = new Vector2(orderText.preferredWidth + 28, 28); }
        }
        public void Toast(string text)
        {
            toastText.text = text; toastEl.sizeDelta = new Vector2(Mathf.Min(1100, toastText.preferredWidth + 32), 32);
            toastEl.gameObject.SetActive(true); toastT = 2.2f;
        }

        sealed class Bark { public RectTransform r; public CanvasGroup g; public Player p; public float t; }
        readonly List<Bark> barks = new List<Bark>();
        public void BarkAt(Player p, string text)
        {
            var r = W.Rect(labels, "bark"); r.anchorMin = r.anchorMax = new Vector2(0, 1); r.pivot = new Vector2(0.5f, 0);
            var bg = r.gameObject.AddComponent<Image>(); bg.sprite = W.Round; bg.type = Image.Type.Sliced; bg.color = Pal.C("#fafcff", 0.95f); bg.raycastTarget = false;
            var edge = W.Box(r, PC(p.slot), "edge", false); edge.rectTransform.anchorMin = Vector2.zero; edge.rectTransform.anchorMax = new Vector2(0, 1); edge.rectTransform.pivot = new Vector2(0, 0.5f); edge.rectTransform.sizeDelta = new Vector2(4, 0);
            var t = W.Txt(r, $"<b>{CHARS[p.@char].name.ToUpperInvariant()}</b> {text}", 14, Pal.C("#101826"));
            t.rectTransform.anchorMin = Vector2.zero; t.rectTransform.anchorMax = Vector2.one; t.rectTransform.offsetMin = new Vector2(12, 6); t.rectTransform.offsetMax = new Vector2(-10, -6);
            float w = Mathf.Min(240, t.preferredWidth + 2); var gen = t.GetGenerationSettings(new Vector2(w, 0));
            float hgt = t.cachedTextGeneratorForLayout.GetPreferredHeight(t.text, gen) / t.pixelsPerUnit;
            r.sizeDelta = new Vector2(w + 22, hgt + 12);
            barks.Add(new Bark { r = r, g = r.gameObject.AddComponent<CanvasGroup>(), p = p, t = 2.6f });
        }

        public void OnEvent(Ev ev, World world)
        {
            switch (ev.type)
            {
                case "banner": ShowBanner(ev.text, ev.sub); break;
                case "checkpoint": Toast("Checkpoint reached"); break;
                case "join": Toast($"{(Bots.IsBot(ev.p) ? "AI teammate" : "Player")} {ev.p.slot + 1} joined as {CHARS[ev.p.@char].name}"); break;
                case "leave": Toast($"Player {ev.slot + 1} left"); break;
                case "downed": Toast(ev.secondWind ? "Second Wind: getting back up" : $"Player {ev.p.slot + 1} is down. Stand next to them to revive{(world.players.Any(q => q.@char == "fix" && q != ev.p) ? " (Fix: faster, and her beam works from range)" : "")}"); break;
                case "wipe": ShowBanner("Team down", "Returning to the last checkpoint"); break;
                case "bark": BarkAt(ev.p, ev.text); break;
                case "swap": Toast($"Player {ev.p.slot + 1} is now {CHARS[ev.p.@char].name}"); break;
                case "ultReady": if (Bots.IsBot(ev.p)) break; Toast($"P{ev.p.slot + 1} ultimate ready: {(ev.p.device == "kbm" ? "press V" : "pull both triggers")}"); break;
            }
        }

        // ---- The ultimate: letterbox bars slide in; during the call its name fills the screen and every teammate who
        // could join is told how; while it plays out the name sits small at the top ----
        RectTransform ultRoot, lbTop, lbBot, uname, ujoin; Text uSmall, uStrong; string ultKey = ""; float ultIn, ultAnim; string ultPhase; bool ultTeam;
        void BuildUlt()
        {
            ultRoot = W.Fill(W.Rect(root, "ultcast"));
            lbTop = W.Box(ultRoot, Pal.C("#03060c"), "lb top", false).rectTransform; lbTop.anchorMin = new Vector2(0, 1); lbTop.anchorMax = new Vector2(1, 1); lbTop.pivot = new Vector2(0.5f, 0);
            lbBot = W.Box(ultRoot, Pal.C("#03060c"), "lb bot", false).rectTransform; lbBot.anchorMin = new Vector2(0, 0); lbBot.anchorMax = new Vector2(1, 0); lbBot.pivot = new Vector2(0.5f, 1);
            uname = W.Rect(ultRoot, "uname"); uname.anchorMin = uname.anchorMax = new Vector2(0.5f, 1); uname.pivot = new Vector2(0.5f, 0.5f); uname.sizeDelta = new Vector2(1600, 150);
            uSmall = W.Txt(uname, "", 16, Pal.C("#7fe3ff"), true, FontStyle.Bold, TextAnchor.UpperCenter); W.Fill(uSmall.rectTransform);
            uStrong = W.Txt(uname, "", 96, Color.white, true, FontStyle.BoldAndItalic, TextAnchor.MiddleCenter); W.Fill(uStrong.rectTransform).offsetMax = new Vector2(0, -16);
            uStrong.horizontalOverflow = HorizontalWrapMode.Overflow;
            W.Outline(uStrong, Pal.C("#7fe3ff", 0.6f), 3); W.Shadow(uStrong, Pal.C("#3fb8ff", 0.5f), new Vector2(0, -4));
            ujoin = W.Rect(ultRoot, "ujoin"); ujoin.anchorMin = ujoin.anchorMax = new Vector2(0.5f, 0); ujoin.pivot = new Vector2(0.5f, 0); ujoin.sizeDelta = new Vector2(1200, 40);
            var hl = ujoin.gameObject.AddComponent<HorizontalLayoutGroup>(); hl.spacing = 10; hl.childAlignment = TextAnchor.MiddleCenter; hl.childControlWidth = hl.childControlHeight = true; hl.childForceExpandWidth = false;
        }
        void UpdateUlt(World world, float dt)
        {
            var U = world.ultCast;
            float barH = 0.07f * root.rect.height;
            ultIn = Mathf.MoveTowards(ultIn, U != null ? 1 : 0, dt / 0.22f);
            float k = 1 - Mathf.Pow(1 - ultIn, 2);
            lbTop.sizeDelta = new Vector2(0, barH); lbTop.anchoredPosition = new Vector2(0, -barH * k);
            lbBot.sizeDelta = new Vector2(0, barH); lbBot.anchoredPosition = new Vector2(0, barH * k);
            if (U == null) { uname.gameObject.SetActive(false); ujoin.gameObject.SetActive(false); ultKey = ""; return; }
            var joiners = U.phase == "cast" ? world.players.Where(q => !Bots.IsBot(q) && !U.members.Contains(q) && q.ult >= ULT.max && q.state != "dead" && q.state != "downed").ToList() : new List<Player>();
            string key = $"{U.phase}|{U.name}|{string.Join(",", U.members.Select(m => m.slot))}|{string.Join(",", joiners.Select(q => q.slot))}";
            if (key != ultKey)
            {
                if (U.phase == "cast" && ultPhase != "cast") { bannerT = 0; banner.gameObject.SetActive(false); ultAnim = 0; }   // the ultimate takes the stage
                ultKey = key; ultPhase = U.phase; ultTeam = U.team;
                uSmall.text = string.Join(" + ", U.members.Select(m => $"P{m.slot + 1} {CHARS[m.@char].name}")).ToUpperInvariant();
                uStrong.text = (U.name ?? "").ToUpperInvariant();
                foreach (Transform c in ujoin) UnityEngine.Object.Destroy(c.gameObject);
                foreach (var q in joiners)
                {
                    var j = W.Rect(ujoin, "join"); var bg = j.gameObject.AddComponent<Image>(); bg.sprite = W.Round; bg.type = Image.Type.Sliced; bg.color = Pal.C("#03060c", 0.85f);
                    var le = j.gameObject.AddComponent<LayoutElement>();
                    var t = W.Txt(j, $"P{q.slot + 1}: {(q.device == "kbm" ? "press V" : "pull both triggers")} to join".ToUpperInvariant(), 18, Color.white, true, FontStyle.Bold, TextAnchor.MiddleCenter); W.Fill(t.rectTransform);
                    W.Outline(t, PC(q.slot), 1);
                    le.preferredWidth = t.preferredWidth + 32; le.preferredHeight = 36;
                }
            }
            uname.gameObject.SetActive(true); ujoin.gameObject.SetActive(joiners.Count > 0);
            ujoin.anchoredPosition = new Vector2(0, barH + 18);
            float H = root.rect.height;
            ultAnim = Mathf.Min(1, ultAnim + dt / 0.35f);
            if (ultPhase == "cast")
            {
                float a = 1 - Mathf.Pow(1 - ultAnim, 3);
                uname.anchoredPosition = new Vector2(0, -0.34f * H); uname.localScale = Vector3.one * Mathf.Lerp(1.8f, 1, a);
                uStrong.color = new Color(1, 1, 1, a);
            }
            else if (ultPhase == "finish" && ultTeam) { uname.anchoredPosition = new Vector2(0, -0.34f * H); uname.localScale = Vector3.one * 1.05f; uStrong.color = Color.white; }
            else
            {
                uname.anchoredPosition = Vector2.Lerp(uname.anchoredPosition, new Vector2(0, -(barH + 10) - 30), 1 - Mathf.Exp(-dt * 14));
                uname.localScale = Vector3.Lerp(uname.localScale, Vector3.one * 0.42f, 1 - Mathf.Exp(-dt * 14)); uStrong.color = Color.white;
            }
            // join prompts pulse
            foreach (Transform c in ujoin) { var o = c.GetComponentInChildren<Outline>(); if (o != null) o.effectDistance = Vector2.one * (1 + 2 * Mathf.PingPong(Time.unscaledTime * 5, 1)); }
        }

        // ---- Boss health: name, the bar (with a notch at half, where its second phase starts) and its armor plates ----
        RectTransform bossBar, bossFill, bossArmor; Text bossName, bossTitle; Image bossEdge, bossFillImg; string bossKey = "";
        void BuildBossBar()
        {
            bossBar = W.At(W.Rect(root, "bossbar"), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -16), new Vector2(470, 52));
            W.Fill(W.Box(bossBar, Pal.plate, "bg", false).rectTransform);
            bossEdge = W.Box(bossBar, Pal.hostile, "edge", false); bossEdge.rectTransform.anchorMin = Vector2.zero; bossEdge.rectTransform.anchorMax = new Vector2(1, 0); bossEdge.rectTransform.pivot = new Vector2(0.5f, 0); bossEdge.rectTransform.sizeDelta = new Vector2(0, 3);
            bossName = W.Txt(bossBar, "", 16, Pal.C("#ffd2e4"), true, FontStyle.Bold); W.At(bossName.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(14, -6), new Vector2(200, 22));
            bossName.horizontalOverflow = HorizontalWrapMode.Overflow;
            bossTitle = W.Txt(bossBar, "", 12, Pal.muted); W.At(bossTitle.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(150, -9), new Vector2(300, 18));
            var hp = W.At(W.Box(bossBar, Pal.C("#ffffff", 0.12f), "hp", false).rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(14, -30), new Vector2(442, 10));
            bossFillImg = W.Box(hp, Pal.hostile, "fill", false); bossFill = bossFillImg.rectTransform; bossFill.anchorMin = Vector2.zero; bossFill.anchorMax = new Vector2(1, 1); bossFill.offsetMin = bossFill.offsetMax = Vector2.zero;
            var notch = W.Box(hp, Pal.C("#0c101a", 0.8f), "notch", false).rectTransform; notch.anchorMin = new Vector2(0.5f, 0); notch.anchorMax = new Vector2(0.5f, 1); notch.sizeDelta = new Vector2(2, 0);
            bossArmor = W.At(W.Rect(bossBar, "armor"), new Vector2(0, 1), new Vector2(0, 1), new Vector2(14, -43), new Vector2(442, 8));
            bossBar.gameObject.SetActive(false);
        }
        void UpdateBossBar(World world)
        {
            var e = world.enemies.FirstOrDefault(q => q.boss && !q.dead && Math.Abs(q.x - world.cam.x) < world.cam.halfW + 14);
            if (e == null) { if (bossBar.gameObject.activeSelf) bossBar.gameObject.SetActive(false); return; }
            bool shielded = e.state == "roar" || e.state == "intro";
            string key = $"{e.type}|{e.phase}|{e.armor}|{e.armorMax}|{shielded}";
            bossBar.gameObject.SetActive(true);
            bossFill.anchorMax = new Vector2((float)Math.Max(0, e.hp / e.maxHp), 1);
            if (key != bossKey)
            {
                bossKey = key;
                bossName.text = Bosses.NameOf(e.type).ToUpperInvariant(); bossTitle.text = e.phase == 2 ? "Phase two" : Bosses.TitleOf(e.type);
                bossTitle.rectTransform.anchoredPosition = new Vector2(14 + bossName.preferredWidth + 10, -9);
                foreach (Transform c in bossArmor) UnityEngine.Object.Destroy(c.gameObject);
                if (e.armorMax > 0)
                {
                    var lab = W.Txt(bossArmor, "ARMOR", 11, Pal.muted); W.At(lab.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(50, 14));
                    for (int i = 0; i < e.armorMax; i++)
                        W.At(W.Box(bossArmor, i < e.armor ? Pal.C("#e6e9f0") : Pal.C("#ffffff", 0.14f), "pip", false).rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(50 + i * 20, 0), new Vector2(16, 7));
                }
                bossFillImg.color = shielded ? Pal.C("#e4e9ef") : Pal.hostile;
                bossEdge.color = e.phase == 2 ? Color.white : Pal.hostile;
            }
        }

        // ---- The HUD panels, one per player in the corners ----
        sealed class Panel
        {
            public RectTransform r; public Text mark, name, role, ultTxt, status; public RectTransform fill, strain, plate, ult; public Image ultImg;
            public ChipRow chips; public bool? ultReady;
        }
        readonly Dictionary<int, Panel> panels = new Dictionary<int, Panel>();
        static RectTransform BarFill(RectTransform bar, Color c, string name)
        {
            var f = W.Box(bar, c, name, false).rectTransform; f.anchorMin = Vector2.zero; f.anchorMax = new Vector2(0, 1); f.pivot = new Vector2(0, 0.5f); f.offsetMin = f.offsetMax = Vector2.zero; return f;
        }
        Panel MakePanel(Player p)
        {
            var P = new Panel();
            bool right = p.slot % 2 == 1, bottom = p.slot >= 2;
            var anchor = new Vector2(right ? 1 : 0, bottom ? 0 : 1);
            P.r = W.At(W.Rect(hud, $"panel P{p.slot + 1}"), anchor, anchor, new Vector2(right ? -16 : 16, bottom ? 16 : -16), new Vector2(250, 80));
            W.Fill(W.Box(P.r, Pal.plate, "plate").rectTransform);
            var edge = W.Box(P.r, PC(p.slot), "edge", false).rectTransform; edge.anchorMin = Vector2.zero; edge.anchorMax = new Vector2(0, 1); edge.pivot = new Vector2(0, 0.5f); edge.sizeDelta = new Vector2(4, 0);
            var inner = W.Rect(P.r, "inner"); inner.anchorMin = inner.anchorMax = new Vector2(0, 1); inner.pivot = new Vector2(0, 1); inner.anchoredPosition = new Vector2(16, -7); inner.sizeDelta = new Vector2(222, 70);
            P.mark = W.Txt(inner, "", 15, PC(p.slot), true, FontStyle.Bold); W.At(P.mark.rectTransform, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(80, 22)); P.mark.horizontalOverflow = HorizontalWrapMode.Overflow;
            P.name = W.Txt(inner, "", 20, Pal.text, true, FontStyle.Bold); W.At(P.name.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(60, 2), new Vector2(120, 24)); P.name.horizontalOverflow = HorizontalWrapMode.Overflow;
            P.role = W.Txt(inner, "", 13, Pal.muted, true, FontStyle.Bold, TextAnchor.UpperRight); W.At(P.role.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(0, -2), new Vector2(100, 20));
            var bar = W.At(W.Box(inner, Pal.C("#ffffff", 0.12f), "hp", false).rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -27), new Vector2(222, 9));
            P.strain = BarFill(bar, Pal.C("#ffffff", 0.35f), "strain"); P.fill = BarFill(bar, Pal.C("#fff3dc"), "fill");
            P.plate = BarFill(bar, Pal.C("#a9c8ff", 0.95f), "plating"); P.plate.anchorMin = new Vector2(1, 0); P.plate.anchorMax = new Vector2(1, 1); P.plate.pivot = new Vector2(1, 0.5f);
            var ub = W.At(W.Box(inner, Pal.C("#ffffff", 0.1f), "ultbar", false).rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -39), new Vector2(222, 5));
            P.ult = BarFill(ub, Pal.C("#7fd4ff"), "ult"); P.ultImg = P.ult.GetComponent<Image>();
            P.ultTxt = W.Txt(inner, "", 11, Pal.C("#bff4ff"), true, FontStyle.Bold, TextAnchor.UpperRight); W.At(P.ultTxt.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(0, -46), new Vector2(160, 14));
            P.status = W.Txt(inner, "", 12, Pal.muted, true, FontStyle.Bold); W.At(P.status.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -48), new Vector2(222, 18));
            P.chips = new ChipRow(inner, 222);
            return P;
        }
        void UpdatePanels(World world)
        {
            var seen = new HashSet<int>();
            foreach (var p in world.players)
            {
                seen.Add(p.slot);
                if (!panels.TryGetValue(p.slot, out var P)) panels[p.slot] = P = MakePanel(p);
                P.mark.text = Mark(p) + (Bots.IsBot(p) ? " · AI" : "");
                P.name.rectTransform.anchoredPosition = new Vector2(P.mark.preferredWidth + 8, 2);
                P.name.text = CHARS[p.@char].name.ToUpperInvariant(); P.role.text = CHARS[p.@char].role.ToUpperInvariant();
                P.fill.anchorMax = new Vector2((float)Math.Max(0, p.hp / p.maxHp), 1);
                P.strain.anchorMax = new Vector2((float)Math.Min(1, Math.Max(0, (p.hp + p.strain) / p.maxHp)), 1);
                P.plate.anchorMin = new Vector2(1 - (float)Math.Min(1, p.plate / p.maxHp), 0);
                // The ultimate bar: it glows when full and says how to use it
                bool ready = p.ult >= ULT.max && p.state != "ult";
                P.ult.anchorMax = new Vector2((float)Math.Min(1, p.ult / ULT.max), 1);
                if (ready != P.ultReady) { P.ultReady = ready; P.ultTxt.text = ready ? $"ULTIMATE · {(p.device == "kbm" ? "V" : "LT + RT")}" : ""; }
                P.ultImg.color = ready ? Color.Lerp(Pal.C("#7fe3ff"), Color.white, Mathf.PingPong(Time.unscaledTime / 0.55f, 1)) : Pal.C("#7fd4ff");
                var list = HudChips.For(p, world, PC(p.slot), out var status);
                P.status.text = status ?? ""; P.status.gameObject.SetActive(status != null);
                P.chips.Set(list);
                float top = ready ? 62 : 50;
                P.chips.rect.anchoredPosition = new Vector2(0, -top);
                float h = 7 + top + Math.Max(status != null ? 18 : 0, P.chips.height) + 9;
                P.r.sizeDelta = new Vector2(250, Math.Max(56, h));
            }
            foreach (var slot in panels.Keys.ToList()) if (!seen.Contains(slot)) { UnityEngine.Object.Destroy(panels[slot].r.gameObject); panels.Remove(slot); }
        }

        // ---- Markers over the world: player tags, lock-on reticles, the rocket jump height, sparring post labels ----
        readonly Dictionary<int, Text> markers = new Dictionary<int, Text>();
        sealed class Reticle { public RectTransform r, spin; public Text tag; public Enemy target; public float pop; public Image[] corners; }
        readonly Dictionary<int, Reticle> reticles = new Dictionary<int, Reticle>();
        readonly Dictionary<int, Text> apexLabels = new Dictionary<int, Text>();
        readonly Dictionary<Enemy, Text> enemyLabels = new Dictionary<Enemy, Text>();
        Text Label(string name, int size, Color c, Vector2 pivot)
        {
            var t = W.Txt(labels, "", size, c, true, FontStyle.Bold, TextAnchor.LowerCenter, name);
            t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0, 1); t.rectTransform.pivot = pivot; t.rectTransform.sizeDelta = new Vector2(300, 24);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            W.Shadow(t, new Color(0, 0, 0, 0.6f), new Vector2(0, -1)); W.Outline(t, new Color(0, 0, 0, 0.35f), 1);
            return t;
        }
        void UpdateMarkers(World world, View view, float dt)
        {
            var seen = new HashSet<int>();
            foreach (var p in world.players)
            {
                seen.Add(p.slot);
                if (!markers.TryGetValue(p.slot, out var m)) markers[p.slot] = m = Label("marker", 14, PC(p.slot), new Vector2(0.5f, 0));
                var s = view.ScreenOf(p.x, p.y + p.h + 0.45);
                float x = Mathf.Clamp(s.x, 16, view.w - 16), y = Mathf.Clamp(s.y, 16, view.h - 16);
                m.text = Mark(p) + (Bots.IsBot(p) ? " AI" : "") + (p.state == "downed" ? " · DOWN" : p.veiled ? " · hidden" : "");
                m.rectTransform.anchoredPosition = ToCanvas(x, y);
                m.gameObject.SetActive(p.state != "dead");
            }
            foreach (var slot in markers.Keys.ToList()) if (!seen.Contains(slot)) { UnityEngine.Object.Destroy(markers[slot].gameObject); markers.Remove(slot); }
            // Lock-on reticles, one per locking player; several on one target nest inside each other
            var onTarget = new Dictionary<Enemy, int>();
            foreach (var p in world.players)
            {
                reticles.TryGetValue(p.slot, out var R);
                var t = p.lockT != null && p.state != "dead" && p.state != "downed" ? p.lockT : null;
                if (t == null) { if (R != null) R.r.gameObject.SetActive(false); continue; }
                if (R == null)
                {
                    R = new Reticle(); R.r = W.Rect(labels, "reticle"); R.r.anchorMin = R.r.anchorMax = new Vector2(0, 1); R.r.pivot = new Vector2(0.5f, 0.5f);
                    R.spin = W.Fill(W.Rect(R.r, "spin")); R.corners = new Image[4];
                    for (int i = 0; i < 4; i++)
                    {
                        var c = W.Rect(R.spin, "corner"); var img = c.gameObject.AddComponent<Image>(); img.sprite = W.Bracket; img.color = PC(p.slot); img.raycastTarget = false;
                        bool rt = i == 1 || i == 3, bt = i >= 2;
                        c.anchorMin = c.anchorMax = new Vector2(rt ? 1 : 0, bt ? 0 : 1); c.pivot = new Vector2(rt ? 1 : 0, bt ? 0 : 1);
                        c.sizeDelta = new Vector2(1, 1); c.localScale = new Vector3(rt ? -1 : 1, bt ? -1 : 1, 1);
                        W.Shadow(img, new Color(0, 0, 0, 0.65f), new Vector2(1, -1));
                        R.corners[i] = img;
                    }
                    R.tag = W.Txt(R.r, "", 12, PC(p.slot), true, FontStyle.Bold, TextAnchor.UpperCenter); R.tag.rectTransform.anchorMin = R.tag.rectTransform.anchorMax = new Vector2(0.5f, 0);
                    R.tag.rectTransform.pivot = new Vector2(0.5f, 1); R.tag.rectTransform.sizeDelta = new Vector2(120, 16); R.tag.rectTransform.anchoredPosition = new Vector2(0, -3);
                    W.Shadow(R.tag, new Color(0, 0, 0, 0.7f), new Vector2(0, -1));
                    reticles[p.slot] = R;
                }
                onTarget.TryGetValue(t, out int n); onTarget[t] = n + 1;
                if (R.target != t) { R.target = t; R.pop = 0; }
                var s = view.ScreenOf(t.x, t.y + t.h * 0.55); float size = (46 + Mathf.Min(90, (float)t.h * 18) + n * 14);
                R.r.sizeDelta = new Vector2(size, size);
                foreach (var c in R.corners) c.rectTransform.sizeDelta = new Vector2(size * 0.3f, size * 0.3f);
                R.r.anchoredPosition = ToCanvas(s.x, s.y);
                R.pop = Mathf.Min(1, R.pop + dt / 0.22f);
                R.spin.localRotation = Quaternion.Euler(0, 0, -(Time.unscaledTime / 3.2f * 360) % 360);
                R.spin.localScale = Vector3.one * Mathf.Lerp(1.9f, 1, R.pop);
                R.tag.text = n == 0 ? Mark(p) : "";
                R.r.gameObject.SetActive(s.vis);
            }
            foreach (var slot in reticles.Keys.ToList()) if (!seen.Contains(slot)) { UnityEngine.Object.Destroy(reticles[slot].r.gameObject); reticles.Remove(slot); }
            // Rocket jump height readout beside the apex marker while Nova lines one up
            foreach (var p in world.players)
            {
                apexLabels.TryGetValue(p.slot, out var l);
                var pv = p.@char == "nova" && p.chargeT > 0 ? world.RocketPreview(p) : null;
                if (pv == null) { if (l != null) l.gameObject.SetActive(false); continue; }
                if (l == null) { apexLabels[p.slot] = l = Label("apex", 14, Pal.C("#ffe2a8"), new Vector2(0, 0.5f)); l.alignment = TextAnchor.MiddleLeft; }
                var s = view.ScreenOf(pv.x, pv.apex);
                l.text = $"▲ {(pv.apex - p.y):0.0} m" + (pv.perfect ? " · Perfect" : "");
                l.color = pv.perfect ? Color.white : Pal.C("#ffe2a8");
                l.rectTransform.anchoredPosition = ToCanvas(s.x + 34, s.y);
                l.gameObject.SetActive(s.vis);
            }
            foreach (var slot in apexLabels.Keys.ToList()) if (!seen.Contains(slot)) { UnityEngine.Object.Destroy(apexLabels[slot].gameObject); apexLabels.Remove(slot); }
            // Drill post teaching labels
            var posts = new HashSet<Enemy>();
            foreach (var e in world.enemies)
            {
                if (e.type != "post") continue;
                posts.Add(e);
                if (!enemyLabels.TryGetValue(e, out var l))
                {
                    l = W.Txt(labels, "", 13, Pal.text, true, FontStyle.Bold, TextAnchor.MiddleCenter, "post label");
                    l.rectTransform.anchorMin = l.rectTransform.anchorMax = new Vector2(0, 1); l.rectTransform.pivot = new Vector2(0.5f, 0); l.horizontalOverflow = HorizontalWrapMode.Overflow;
                    var bg = W.Box(l.rectTransform, Pal.C("#0a1222", 0.78f), "bg"); W.Fill(bg.rectTransform); bg.transform.SetAsFirstSibling();
                    bg.rectTransform.offsetMin = new Vector2(-9, -3); bg.rectTransform.offsetMax = new Vector2(9, 3);
                    enemyLabels[e] = l;
                }
                var s = view.ScreenOf(e.x, e.y + e.h + 0.8);
                l.gameObject.SetActive(Math.Abs(e.x - world.cam.x) <= world.cam.halfW + 1);
                l.text = (string.IsNullOrEmpty(e.label) ? "Sparring post: step close" : e.label).ToUpperInvariant();
                string cat = e.state == "windup" && e.atk != null ? e.atk.cat : "";
                l.color = cat == "heavy" ? Pal.warm : cat == "unblockable" ? Pal.hostile : Pal.text;
                l.rectTransform.sizeDelta = new Vector2(l.preferredWidth, 20);
                l.rectTransform.anchoredPosition = ToCanvas(s.x, s.y);
            }
            foreach (var e in enemyLabels.Keys.ToList()) if (!posts.Contains(e)) { UnityEngine.Object.Destroy(enemyLabels[e].gameObject); enemyLabels.Remove(e); }
        }

        public void Update(float dt, World world, View view, float fps)
        {
            if (bannerT > 0) { bannerT -= dt; if (bannerT <= 0) banner.gameObject.SetActive(false); }
            if (toastT > 0) { toastT -= dt; if (toastT <= 0) toastEl.gameObject.SetActive(false); }
            UpdatePanels(world);
            UpdateMarkers(world, view, dt);
            UpdateBossBar(world);
            UpdateUlt(world, dt);
            foreach (var b in barks)
            {
                b.t -= dt;
                var s = view.ScreenOf(b.p.x, b.p.y + b.p.h + 1.1);
                b.r.anchoredPosition = ToCanvas(s.x, s.y);
                b.g.alpha = Mathf.Min(1, b.t * 2);
                if (b.t <= 0) UnityEngine.Object.Destroy(b.r.gameObject);
            }
            barks.RemoveAll(b => b.t <= 0);
            if (debugText.transform.parent.gameObject.activeSelf) UpdateDebug(world, fps);
            UpdateMenus(dt);
        }

        void UpdateDebug(World world, float fps)
        {
            var d = world.director;
            var lines = new List<string> { $"fps {fps:0} · tick {world.tick} · tokens melee {d.MeleeUsed}/{d.Cap("melee")} ranged {d.RangedUsed}/{d.Cap("ranged")} · cam dist {world.cam.dist:0.0}" +
                (world.ultCast != null ? $" · ultimate {world.ultCast.phase} {world.ultCast.t} {world.ultCast.name}" : "") + (world.wells.Count > 0 ? $" · wells {world.wells.Count}" : "") +
                (world.gadgets.Count > 0 ? $" · gadgets {world.gadgets.Count}" : "") + (world.pickups.Count > 0 ? $" · power-ups {world.pickups.Count}" : "") };
            foreach (var p in world.players)
                lines.Add($"P{p.slot + 1} {p.@char} {p.state}:{p.st} pos {p.x:0.00},{p.y:0.00} v {p.vx:0.0},{p.vy:0.0} ground {(p.onGround ? 1 : 0)} wall {p.wallDir}{(p.wallSliding ? " slide" : "")} vb {PlayerSim.VbTier(p)} air-dash {p.airDashes}" +
                    $" · dashC {p.dashChargeT} rifle {p.rifleT}/{p.rifleCd} lock {(p.lockT != null ? p.lockT.type : "-")} beam {(p.beam != null ? p.beam.t.ToString() : "-")} aegis {(p.aegis != null ? p.aegis.hp.ToString("0") : p.aegisCd.ToString())}" +
                    $" over {p.overcharge:0} pound {(p.pound != null ? p.pound.phase + p.pound.level : "-")} sub {p.sub} ult {p.ult:0}{(p.ultRun != null ? " " + p.ultRun.kind + p.ultRun.t : "")}" +
                    (p.@char == "ram" ? $" · integ {p.integrity:0}{(p.guardBroken ? " broken" : "")} kin {p.kinetic:0}" : "") +
                    (p.@char == "fix" ? $" · scrap {p.scrap:0} gadget {p.gadgetSel} power {p.powerSel}" : "") + $" · plate {p.plate:0} oc {p.overclockT} rate {p.ampK:0.00}");
            debugText.text = string.Join("\n", lines);
        }
        public void ToggleDebug() { var go = debugText.transform.parent.gameObject; go.SetActive(!go.activeSelf); }
    }
}
