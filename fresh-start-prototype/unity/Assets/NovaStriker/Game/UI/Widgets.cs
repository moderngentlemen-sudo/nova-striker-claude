// The interface's building blocks on uGUI, standing in for the prototype's HTML and CSS: rounded plates,
// text in the prototype's palette, chips that wrap like the CSS flex rows, small bars, scrolling cards, and the
// controls of the settings menu (a cycler for a list, a slider, an on/off button).
// Fonts: Unity's built-in LegacyRuntime font stands in for Saira Condensed, Barlow and IBM Plex Mono (drop the
// real fonts into Resources/NovaStriker/Fonts with those names to use them).
using System;
using System.Collections.Generic;
using Paint = NovaStriker.Game.Three.Paint;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NovaStriker.Game.UI
{
    public static class Pal
    {
        public static Color C(string css, float a = 1) { var c = Paint.Css(css); c.a *= a; return c; }
        public static readonly Color plate = C("#0a1222", 0.74f), edge = C("#ecf4ff", 0.16f), text = C("#f3f7fc"), muted = C("#a8b7ca"),
            warm = C("#ffb547"), hostile = C("#ff2e7e"), dim = C("#081020", 0.5f), dark = C("#1d1204");
    }

    public static class W
    {
        static Font display, body, mono;
        static Font Load(string name)
        {
            var f = Resources.Load<Font>("NovaStriker/Fonts/" + name);
            return f != null ? f : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
        public static Font Display => display ??= Load("SairaCondensed-Bold");
        public static Font Body => body ??= Load("Barlow-Medium");
        public static Font Mono => mono ??= Load("IBMPlexMono-Regular");

        // A rounded-rectangle sprite (9-sliced) for plates, chips and buttons
        static Sprite round, bracket;
        public static Sprite Round
        {
            get
            {
                if (round != null) return round;
                const int S = 32, R = 6;
                var t = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                var px = new Color32[S * S];
                for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
                {
                    float dx = Mathf.Max(0, Mathf.Max(R - x - 0.5f, x + 0.5f - (S - R))), dy = Mathf.Max(0, Mathf.Max(R - y - 0.5f, y + 0.5f - (S - R)));
                    float a = Mathf.Clamp01(R + 0.5f - Mathf.Sqrt(dx * dx + dy * dy));
                    px[y * S + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
                t.SetPixels32(px); t.Apply();
                return round = Sprite.Create(t, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(R + 1, R + 1, R + 1, R + 1));
            }
        }
        // One corner of the lock-on reticle (an L of 3 px lines)
        public static Sprite Bracket
        {
            get
            {
                if (bracket != null) return bracket;
                const int S = 32;
                var t = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[S * S];
                for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
                {
                    bool on = (y >= S - 4) || (x < 4);   // the top edge and the left edge (the top-left corner)
                    px[y * S + x] = on ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
                }
                t.SetPixels32(px); t.Apply();
                return bracket = Sprite.Create(t, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100);
            }
        }

        public static RectTransform Rect(Transform parent, string name = "el")
        {
            var go = new GameObject(name, typeof(RectTransform));
            var r = (RectTransform)go.transform; r.SetParent(parent, false);
            return r;
        }
        public static RectTransform Fill(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; return r; }
        // Top-left anchored at (x, y) pixels in canvas units, with a pivot
        public static RectTransform At(RectTransform r, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2? size = null)
        {
            r.anchorMin = r.anchorMax = anchor; r.pivot = pivot; r.anchoredPosition = pos;
            if (size.HasValue) r.sizeDelta = size.Value;
            return r;
        }
        public static Image Box(Transform parent, Color c, string name = "box", bool rounded = true)
        {
            var r = Rect(parent, name); var i = r.gameObject.AddComponent<Image>();
            i.color = c; if (rounded) { i.sprite = Round; i.type = Image.Type.Sliced; }
            i.raycastTarget = false;
            return i;
        }
        public static Text Txt(Transform parent, string s, int size, Color c, bool display = false, FontStyle style = FontStyle.Normal, TextAnchor align = TextAnchor.UpperLeft, string name = "text")
        {
            var r = Rect(parent, name); var t = r.gameObject.AddComponent<Text>();
            t.font = display ? Display : Body; t.fontSize = size; t.color = c; t.fontStyle = style; t.alignment = align;
            t.supportRichText = true; t.text = s; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }
        public static T Add<T>(Component c) where T : Component => c.gameObject.AddComponent<T>();
        public static void Shadow(Graphic g, Color c, Vector2 d) { var s = g.gameObject.AddComponent<Shadow>(); s.effectColor = c; s.effectDistance = d; }
        public static void Outline(Graphic g, Color c, float w = 1) { var s = g.gameObject.AddComponent<Outline>(); s.effectColor = c; s.effectDistance = new Vector2(w, -w); }

        // The prototype's small HTML in text: <kbd>, <b>, <i> become rich text, other tags go
        public static string Rich(string html)
        {
            if (html == null) return "";
            var s = html.Replace("<kbd>", "<b><color=#ffffff>[").Replace("</kbd>", "]</color></b>");
            s = System.Text.RegularExpressions.Regex.Replace(s, "<(?!/?(b|i|color)\\b)[^>]*>", "");
            return s.Replace("&amp;", "&").Replace("&lt;", "<").Replace("&gt;", ">");
        }
        public static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);
    }

    // A chip (a coloured label), or a small resource bar, as the HUD rows show them
    public struct Chip
    {
        public string text; public Color bg, fg; public Color? ring; public float barW, frac; public Color barFill; public bool pulse;
        public static Chip Of(string text, string cls = "", Color? tint = null) => Style(new Chip { text = text }, cls, tint);
        public static Chip Bar(float width, float frac, Color fill) => new Chip { barW = width, frac = Mathf.Clamp01(frac), barFill = fill, bg = Pal.C("#ffffff", 0.14f) };
        static Chip Style(Chip c, string cls, Color? tint)
        {
            c.bg = Pal.C("#ffffff", 0.1f); c.fg = Pal.muted;
            foreach (var k in cls.Split(' '))
                switch (k)
                {
                    case "ready": c.bg = Pal.C("#ffb547", 0.2f); c.fg = Pal.warm; break;
                    case "vb": c.bg = Pal.warm; c.fg = Pal.dark; break;
                    case "tether": c.bg = Pal.C("#ff9a1f", 0.16f); c.fg = Pal.C("#ffc27a"); break;
                    case "veil": c.bg = Pal.C("#dcecff", 0.14f); c.fg = Pal.C("#dcecff"); break;
                    case "veilon": c.bg = Pal.C("#dcecff"); c.fg = Pal.C("#14202e"); break;
                    case "flare": c.bg = Pal.C("#ff9a1f"); c.fg = Pal.dark; break;
                    case "perfect": c.bg = Pal.C("#fff1c9"); c.fg = Pal.dark; break;
                    case "focuson": c.fg = Pal.warm; break;
                    case "over": c.bg = Pal.C("#fff6e0"); c.fg = Pal.dark; c.pulse = true; break;
                    case "red": c.bg = Pal.C("#ff2414"); c.fg = Color.white; break;
                    case "ram": c.bg = Pal.C("#58a6ff", 0.2f); c.fg = Pal.C("#bfe0ff"); break;
                    case "kin": c.fg = Pal.C("#8fb8e8"); break;
                    case "kinon": c.bg = Pal.C("#58a6ff"); c.fg = Pal.C("#071426"); break;
                    case "fix": c.bg = Pal.C("#3cf0b0", 0.14f); c.fg = Pal.C("#9ff5d0"); break;
                    case "fixon": c.bg = Pal.C("#3cf0b0"); c.fg = Pal.C("#062018"); break;
                    case "plate": c.bg = Pal.C("#a9c8ff", 0.2f); c.fg = Pal.C("#d6e6ff"); c.ring = Pal.C("#a9c8ff", 0.6f); break;
                    case "over2": c.bg = Pal.C("#6fe3ff", 0.2f); c.fg = Pal.C("#c8f4ff"); break;
                    case "fury": c.bg = Pal.C("#ff5a4a", 0.24f); c.fg = Pal.C("#ffc2b8"); break;
                    case "lock": c.bg = Pal.C("#0a1222", 0.35f); c.fg = tint ?? Pal.text; c.ring = tint; break;
                    case "attach": c.fg = tint ?? Pal.text; c.ring = tint; break;
                }
            return c;
        }
        public string Key => barW > 0 ? $"[{barW}:{frac:0.00}:{barFill}]" : $"<{text}|{bg}|{fg}|{ring}>";
    }

    // A row of chips that wraps like a CSS flex row (gap 5 px), reusing its pieces
    public sealed class ChipRow
    {
        sealed class View { public RectTransform r; public Image bg, fill; public Text t; public Outline ring; }
        readonly RectTransform root; readonly List<View> pool = new List<View>();
        readonly float width; string key = "";
        public float height { get; private set; }
        public List<Chip> chips;
        public ChipRow(RectTransform parent, float width) { root = W.Rect(parent, "chips"); root.anchorMin = root.anchorMax = new Vector2(0, 1); root.pivot = new Vector2(0, 1); this.width = width; }
        public RectTransform rect => root;
        public void Set(List<Chip> list)
        {
            chips = list;
            var k = new System.Text.StringBuilder(); foreach (var c in list) k.Append(c.Key);
            string nk = k.ToString(); if (nk == key) { Pulse(); return; }
            key = nk;
            while (pool.Count < list.Count)
            {
                var v = new View(); v.r = W.Rect(root, "chip"); v.r.anchorMin = v.r.anchorMax = new Vector2(0, 1); v.r.pivot = new Vector2(0, 1);
                v.bg = v.r.gameObject.AddComponent<Image>(); v.bg.sprite = W.Round; v.bg.type = Image.Type.Sliced; v.bg.raycastTarget = false;
                v.ring = v.r.gameObject.AddComponent<Outline>(); v.ring.effectDistance = new Vector2(1, -1);
                v.fill = W.Box(v.r, Color.white, "fill"); v.fill.rectTransform.anchorMin = Vector2.zero; v.fill.rectTransform.anchorMax = new Vector2(0, 1); v.fill.rectTransform.pivot = new Vector2(0, 0.5f);
                v.t = W.Txt(v.r, "", 12, Color.white, true, FontStyle.Bold, TextAnchor.MiddleCenter); W.Fill(v.t.rectTransform);
                v.t.horizontalOverflow = HorizontalWrapMode.Overflow;
                pool.Add(v);
            }
            float x = 0, y = 0, rowH = 18;
            for (int i = 0; i < pool.Count; i++)
            {
                var v = pool[i]; bool on = i < list.Count; v.r.gameObject.SetActive(on); if (!on) continue;
                var c = list[i]; float w, h;
                if (c.barW > 0)
                {
                    w = c.barW; h = 6; v.t.text = ""; v.bg.color = c.bg; v.ring.enabled = false;
                    v.fill.gameObject.SetActive(true); v.fill.color = c.barFill; v.fill.rectTransform.sizeDelta = new Vector2(w * c.frac, 0); v.fill.rectTransform.anchoredPosition = Vector2.zero;
                }
                else
                {
                    v.fill.gameObject.SetActive(false);
                    v.t.text = c.text.ToUpperInvariant(); v.t.color = c.fg; v.bg.color = c.bg;
                    v.ring.enabled = c.ring.HasValue; if (c.ring.HasValue) v.ring.effectColor = c.ring.Value;
                    w = v.t.preferredWidth + 14; h = rowH;
                }
                if (x > 0 && x + w > width) { x = 0; y += rowH + 4; }
                v.r.sizeDelta = new Vector2(w, h); v.r.anchoredPosition = new Vector2(x, -y - (rowH - h) / 2);
                x += w + 5;
            }
            height = list.Count > 0 ? y + rowH : 0;
            Pulse();
        }
        // The Overcharged chip glows on and off (.chip.over's animation)
        void Pulse()
        {
            if (chips == null) return;
            for (int i = 0; i < chips.Count && i < pool.Count; i++)
                if (chips[i].pulse) { pool[i].ring.enabled = true; pool[i].ring.effectColor = Pal.C("#fff1c9", 0.4f + 0.4f * Mathf.PingPong(Time.unscaledTime * 4, 1)); pool[i].ring.effectDistance = new Vector2(2, -2); }
        }
    }

    // A scrolling card on a dimmed overlay (the prototype's .overlay > .card)
    public sealed class Card
    {
        public readonly RectTransform overlay, card, content;
        public readonly ScrollRect scroll;
        public Card(RectTransform parent, string name, float maxWidth)
        {
            overlay = W.Fill(W.Rect(parent, name));
            var dim = overlay.gameObject.AddComponent<Image>(); dim.color = Pal.C("#081020", 0.55f); dim.raycastTarget = true;
            card = W.Rect(overlay, "card");
            card.anchorMin = new Vector2(0.5f, 0); card.anchorMax = new Vector2(0.5f, 1); card.pivot = new Vector2(0.5f, 0.5f);
            card.sizeDelta = new Vector2(maxWidth, -60); card.anchoredPosition = Vector2.zero;
            var bg = card.gameObject.AddComponent<Image>(); bg.sprite = W.Round; bg.type = Image.Type.Sliced; bg.color = Pal.C("#0c1628", 0.92f);
            W.Outline(bg, Pal.edge);
            var vp = W.Rect(card, "viewport"); W.Fill(vp); vp.offsetMin = new Vector2(26, 20); vp.offsetMax = new Vector2(-26, -20);
            vp.gameObject.AddComponent<RectMask2D>(); var vpi = vp.gameObject.AddComponent<Image>(); vpi.color = new Color(0, 0, 0, 0);
            content = W.Rect(vp, "content"); content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = Vector2.zero;
            var lay = content.gameObject.AddComponent<VerticalLayoutGroup>(); lay.spacing = 10; lay.childControlWidth = true; lay.childControlHeight = true; lay.childForceExpandHeight = false; lay.childForceExpandWidth = true;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll = card.gameObject.AddComponent<ScrollRect>(); scroll.viewport = vp; scroll.content = content; scroll.horizontal = false; scroll.scrollSensitivity = 30;
            scroll.movementType = ScrollRect.MovementType.Clamped;
        }
        public bool visible { get => overlay.gameObject.activeSelf; set => overlay.gameObject.SetActive(value); }
        // Content helpers: an eyebrow, a heading, a paragraph, a row of buttons
        public Text Eyebrow(string s) => Para(s, 13, Pal.warm, true, FontStyle.Bold);
        public Text H1(string s) => Para(s.ToUpperInvariant(), 64, Pal.text, true, FontStyle.BoldAndItalic);
        public Text H2(string s) => Para(s.ToUpperInvariant(), 30, Pal.text, true, FontStyle.Bold);
        public Text Para(string s, int size = 15, Color? c = null, bool display = false, FontStyle st = FontStyle.Normal, Transform into = null)
        {
            var t = W.Txt(into ?? content, W.Rich(s), size, c ?? Pal.text, display, st);
            t.lineSpacing = 1.1f;
            return t;
        }
        public RectTransform Row(Transform into = null, float spacing = 8)
        {
            var r = W.Rect(into ?? content, "row");
            var g = r.gameObject.AddComponent<FlowLayout>(); g.spacing = spacing;
            return r;
        }
    }

    // A wrapping row layout for buttons (CSS flex-wrap)
    public sealed class FlowLayout : LayoutGroup
    {
        public float spacing = 8;
        float h;
        public override void CalculateLayoutInputVertical() { Lay(false); SetLayoutInputForAxis(h, h, -1, 1); }
        public override void CalculateLayoutInputHorizontal() { base.CalculateLayoutInputHorizontal(); SetLayoutInputForAxis(0, 0, 1, 0); }
        public override void SetLayoutHorizontal() { Lay(true); }
        public override void SetLayoutVertical() { Lay(true); }
        void Lay(bool apply)
        {
            float width = rectTransform.rect.width, x = 0, y = 0, rowH = 0;
            foreach (RectTransform c in rectChildren)
            {
                float w = LayoutUtility.GetPreferredWidth(c), hh = LayoutUtility.GetPreferredHeight(c);
                if (x > 0 && x + w > width) { x = 0; y += rowH + spacing; rowH = 0; }
                if (apply) { SetChildAlongAxis(c, 0, x, w); SetChildAlongAxis(c, 1, y, hh); }
                x += w + spacing; rowH = Mathf.Max(rowH, hh);
            }
            h = y + rowH;
        }
    }

    // A button in the prototype's style (.btn), with a highlighted state for controller focus
    public static class Btn
    {
        public static Button Make(Transform parent, string label, Action onClick, int size = 15, bool small = false)
        {
            var r = W.Rect(parent, "btn: " + label);
            var img = r.gameObject.AddComponent<Image>(); img.sprite = W.Round; img.type = Image.Type.Sliced; img.color = Pal.C("#ffffff", 0.08f);
            W.Outline(img, Pal.edge);
            var b = r.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;   // (the menu draws its own focus ring)
            var t = W.Txt(r, label.ToUpperInvariant(), small ? 13 : size, Pal.text, true, FontStyle.Bold, TextAnchor.MiddleCenter);
            W.Fill(t.rectTransform); t.horizontalOverflow = HorizontalWrapMode.Overflow;
            var le = r.gameObject.AddComponent<LayoutElement>(); le.preferredWidth = t.preferredWidth + (small ? 20 : 28); le.preferredHeight = small ? 26 : 34;
            if (onClick != null) b.onClick.AddListener(() => onClick());
            return b;
        }
        // The pressed look (.btn.on): warm, with dark text
        public static void SetOn(Button b, bool on)
        {
            var img = b.GetComponent<Image>(); var t = b.GetComponentInChildren<Text>();
            img.color = on ? Pal.warm : Pal.C("#ffffff", 0.08f); t.color = on ? Pal.dark : Pal.text;
        }
    }

    // A slider (input type=range): drag or click to set; left/right on a controller steps it
    public sealed class RangeCtl : Selectable, IPointerDownHandler, IDragHandler
    {
        public float min, max, step, value;
        public Action<float> onChange;
        public Image fill; public Text label; public Func<float, string> fmt;
        public void SetValue(float v, bool notify = true)
        {
            v = Mathf.Clamp(Mathf.Round((v - min) / step) * step + min, min, max);
            value = v;
            if (fill != null) fill.rectTransform.anchorMax = new Vector2(max > min ? (v - min) / (max - min) : 0, 1);
            if (label != null) label.text = fmt != null ? fmt(v) : (Mathf.Abs(v - Mathf.Round(v)) < 1e-4f ? v.ToString("0") : v.ToString("0.##"));
            if (notify) onChange?.Invoke(v);
        }
        void SetFrom(PointerEventData e)
        {
            var r = (RectTransform)transform;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(r, e.position, e.pressEventCamera, out var lp))
                SetValue(min + (max - min) * Mathf.Clamp01((lp.x - r.rect.xMin) / r.rect.width));
        }
        public override void OnPointerDown(PointerEventData e) { base.OnPointerDown(e); SetFrom(e); }
        public void OnDrag(PointerEventData e) => SetFrom(e);
        public static RangeCtl Make(Transform parent, float min, float max, float step, float value, Func<float, string> fmt, Action<float> onChange)
        {
            var r = W.Rect(parent, "range");
            var bg = r.gameObject.AddComponent<Image>(); bg.sprite = W.Round; bg.type = Image.Type.Sliced; bg.color = Pal.C("#16233a");
            var ctl = r.gameObject.AddComponent<RangeCtl>();
            ctl.transition = Selectable.Transition.None;
            ctl.targetGraphic = bg;
            var f = W.Box(r, Pal.C("#ffb547", 0.55f), "fill"); f.rectTransform.anchorMin = Vector2.zero; f.rectTransform.anchorMax = new Vector2(0.5f, 1); f.rectTransform.offsetMin = f.rectTransform.offsetMax = Vector2.zero;
            var t = W.Txt(r, "", 13, Pal.text, false, FontStyle.Normal, TextAnchor.MiddleCenter); W.Fill(t.rectTransform);
            ctl.fill = f; ctl.label = t; ctl.min = min; ctl.max = max; ctl.step = step; ctl.fmt = fmt;
            ctl.SetValue(value, false); ctl.onChange = onChange;
            var le = r.gameObject.AddComponent<LayoutElement>(); le.preferredWidth = 170; le.preferredHeight = 26;
            return ctl;
        }
    }

    // A list choice (select): shows the current option; a click or A steps to the next, left/right steps either way
    public sealed class Cycler : Selectable, IPointerClickHandler
    {
        public string[] values, labels; public int index; public Action<string> onChange; public Text label;
        public void Step(int d, bool wrap = false)
        {
            int i = index + d;
            if (wrap) i = (i % values.Length + values.Length) % values.Length; else i = Mathf.Clamp(i, 0, values.Length - 1);
            if (i == index) return;
            index = i; label.text = "‹ " + labels[i] + " ›"; onChange?.Invoke(values[i]);
        }
        public void OnPointerClick(PointerEventData e) { Step(e.button == PointerEventData.InputButton.Right ? -1 : 1, true); }
        public static Cycler Make(Transform parent, (string v, string t)[] opts, string cur, Action<string> onChange)
        {
            var r = W.Rect(parent, "select");
            var bg = r.gameObject.AddComponent<Image>(); bg.sprite = W.Round; bg.type = Image.Type.Sliced; bg.color = Pal.C("#16233a");
            W.Outline(bg, Pal.edge);
            var c = r.gameObject.AddComponent<Cycler>(); c.targetGraphic = bg;
            c.transition = Selectable.Transition.None;
            c.values = Array.ConvertAll(opts, o => o.v); c.labels = Array.ConvertAll(opts, o => o.t);
            c.index = Mathf.Max(0, Array.IndexOf(c.values, cur));
            c.label = W.Txt(r, "‹ " + c.labels[c.index] + " ›", 13, Pal.text, false, FontStyle.Normal, TextAnchor.MiddleCenter); W.Fill(c.label.rectTransform);
            c.label.rectTransform.offsetMin = new Vector2(6, 0); c.label.rectTransform.offsetMax = new Vector2(-6, 0);
            c.label.horizontalOverflow = HorizontalWrapMode.Wrap; c.label.resizeTextForBestFit = true; c.label.resizeTextMinSize = 9; c.label.resizeTextMaxSize = 13;
            c.onChange = onChange;
            var le = r.gameObject.AddComponent<LayoutElement>(); le.preferredWidth = 220; le.preferredHeight = 30;
            return c;
        }
    }
}
