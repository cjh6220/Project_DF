using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Proto.Core;

namespace Proto.UI
{
    /// <summary>
    /// 장비 칸 하나 (가방 · 장착 칸 · 대장장이 창이 같이 쓴다) + 장비 설명 글.
    ///
    ///   바탕   어두운 유리 · 등급 색 테두리 · 아래쪽에 등급 색이 은은하게 차오름
    ///   그림   Resources/Icons/item_&lt;부위&gt; (없으면 부위 이름 글자)
    ///   표시   왼쪽 위 던전 단계(Ⅰ~Ⅳ) · 오른쪽 위 NEW · 고르면 금색 테
    /// </summary>
    public class ItemCell
    {
        public RectTransform Root;
        public Image Bg, Edge, Fill, Glow, Art, Focus, Mark;
        public TextMeshProUGUI Glyph, Tier, New, Caption;
        public Button Button;
        public Item Item;

        static readonly Color ColBg = new Color(0.08f, 0.09f, 0.13f, 0.95f);
        static readonly Color ColEmptyEdge = new Color(0.40f, 0.38f, 0.48f, 0.55f);
        static readonly string[] Roman = { "Ⅰ", "Ⅱ", "Ⅲ", "Ⅳ" };

        // GPT 그림 (Resources/UI) — 없으면 예전 둥근 테로 그린다
        static Sprite _frame, _glow, _sel; static bool _loaded;
        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _frame = UiKit.Ui("ItemSlot"); _glow = UiKit.Ui("ItemSlotGlow"); _sel = UiKit.Ui("ItemSlotSel");
        }
        static readonly Color ColFrameEmpty = new Color(0.78f, 0.80f, 0.88f, 0.45f);
        static readonly float[] GlowAlpha = { 0f, 0.30f, 0.55f, 0.80f, 1f };   // 일반 · 고급 · 희귀 · 영웅 · 전설

        /// <summary>그림 가장자리 여백만큼 바깥으로 늘려 그림 속 선이 칸 끝에 오게 한다.</summary>
        static void Bleed(RectTransform rt, float px)
        {
            UiKit.Stretch(rt);
            rt.offsetMin = new Vector2(-px, -px);
            rt.offsetMax = new Vector2(px, px);
        }

        public static ItemCell Create(Transform parent, string name, Vector2 pos, float size, bool topLeftAnchor = true)
        {
            Load();
            var c = new ItemCell();
            c.Bg = UiKit.Image(parent, name, ColBg, UiArt.RoundRectFill, true);
            c.Bg.type = Image.Type.Sliced;
            c.Root = c.Bg.rectTransform;
            if (topLeftAnchor)
            {
                c.Root.anchorMin = c.Root.anchorMax = new Vector2(0f, 1f);
                c.Root.pivot = new Vector2(0f, 1f);
                c.Root.anchoredPosition = pos;
                c.Root.sizeDelta = new Vector2(size, size);
            }
            else UiKit.Place(c.Root, new Vector2(0.5f, 0.5f), pos, new Vector2(size, size));

            c.Fill = UiKit.Image(c.Root, "Fill", Color.clear, UiKit.Soft);
            UiKit.Stretch(c.Fill.rectTransform);
            c.Fill.rectTransform.offsetMin = new Vector2(4f, 4f);
            c.Fill.rectTransform.offsetMax = new Vector2(-4f, -size * 0.35f);

            // 등급 빛 — 희귀 이상일수록 진하게 (색은 등급 색)
            c.Glow = UiKit.Image(c.Root, "Glow", Color.clear, _glow);
            Bleed(c.Glow.rectTransform, size * 0.06f);
            c.Glow.enabled = _glow != null;

            c.Glyph = UiKit.Text(c.Root, "Glyph", "", size * 0.22f);
            UiKit.Stretch(c.Glyph.rectTransform);
            c.Glyph.color = new Color(1f, 1f, 1f, 0.75f);

            c.Art = UiKit.Image(c.Root, "Art", Color.white);
            UiKit.Stretch(c.Art.rectTransform);
            float inset = size * 0.14f;
            c.Art.rectTransform.offsetMin = new Vector2(inset, inset);
            c.Art.rectTransform.offsetMax = new Vector2(-inset, -inset);
            c.Art.preserveAspect = true;

            if (_frame != null)
            {
                // 은색 다이아 테 — 그림 속 선이 그림 끝에서 7% 안쪽이라 그만큼 바깥으로 늘린다
                c.Edge = UiKit.Image(c.Root, "Edge", ColFrameEmpty, _frame);
                Bleed(c.Edge.rectTransform, size * 0.08f);
            }
            else
            {
                c.Edge = UiKit.Image(c.Root, "Edge", ColEmptyEdge, UiArt.RoundRectOutline);
                c.Edge.type = Image.Type.Sliced;
                UiKit.Stretch(c.Edge.rectTransform);
            }

            c.Tier = UiKit.Text(c.Root, "Tier", "", Mathf.Max(13f, size * 0.17f), TextAlignmentOptions.TopLeft);
            UiKit.Stretch(c.Tier.rectTransform);
            c.Tier.rectTransform.offsetMin = new Vector2(7f, 4f);
            c.Tier.rectTransform.offsetMax = new Vector2(-4f, -4f);
            c.Tier.color = new Color(0.85f, 0.83f, 0.92f, 0.85f);

            c.New = UiKit.Text(c.Root, "New", "NEW", Mathf.Max(11f, size * 0.15f), TextAlignmentOptions.TopRight);
            UiKit.Stretch(c.New.rectTransform);
            c.New.rectTransform.offsetMin = new Vector2(4f, 4f);
            c.New.rectTransform.offsetMax = new Vector2(-6f, -4f);
            c.New.color = new Color(1f, 0.84f, 0.35f, 1f);
            c.New.fontStyle = FontStyles.Bold;

            c.Mark = UiKit.Image(c.Root, "Mark", new Color(1f, 0.80f, 0.42f, 0f), UiArt.RoundRectOutline);
            c.Mark.type = Image.Type.Sliced;
            UiKit.Stretch(c.Mark.rectTransform);
            c.Mark.rectTransform.offsetMin = new Vector2(3f, 3f);
            c.Mark.rectTransform.offsetMax = new Vector2(-3f, -3f);

            if (_sel != null)
            {
                // 금색 모서리 꺾쇠 — 칸 바깥 모서리에 걸친다
                c.Focus = UiKit.Image(c.Root, "Focus", new Color(1f, 1f, 1f, 0f), _sel);
                Bleed(c.Focus.rectTransform, size * 0.18f);
            }
            else
            {
                c.Focus = UiKit.Image(c.Root, "Focus", new Color(1f, 0.80f, 0.42f, 0f), UiArt.RoundRectOutline);
                c.Focus.type = Image.Type.Sliced;
                UiKit.Stretch(c.Focus.rectTransform);
                c.Focus.rectTransform.offsetMin = new Vector2(-5f, -5f);
                c.Focus.rectTransform.offsetMax = new Vector2(5f, 5f);
            }

            c.Button = c.Bg.gameObject.AddComponent<Button>();
            c.Button.transition = Selectable.Transition.None;
            UiFocus.NoNav(c.Button);
            c.Set(null, "");
            return c;
        }

        /// <summary>칸에 장비를 넣는다. emptyLabel은 빈 칸에 쓰는 글자 (장착 칸이면 부위 이름).</summary>
        public void Set(Item it, string emptyLabel, Sprite emptyArt = null)
        {
            Item = it;
            if (it == null)
            {
                Edge.color = _frame != null ? ColFrameEmpty : ColEmptyEdge;
                Fill.color = Color.clear;
                Glow.color = Color.clear;
                Tier.text = "";
                New.gameObject.SetActive(false);
                Art.sprite = emptyArt;
                Art.enabled = emptyArt != null;
                Art.color = new Color(0.85f, 0.88f, 1f, 0.09f);
                Glyph.text = emptyArt == null ? emptyLabel : "";
                Glyph.color = new Color(1f, 1f, 1f, 0.28f);
                return;
            }
            var col = ItemDb.RarityColor(it.rarity);
            if (_frame != null)
                Edge.color = it.rarity == Rarity.Common ? new Color(0.92f, 0.93f, 0.97f, 0.9f)
                                                         : new Color(Mathf.Min(1f, col.r * 1.25f), Mathf.Min(1f, col.g * 1.25f), Mathf.Min(1f, col.b * 1.25f), 1f);
            else
                Edge.color = new Color(col.r, col.g, col.b, 0.95f);
            Fill.color = new Color(col.r, col.g, col.b, (_glow != null ? 0.04f : 0.10f) + 0.05f * (int)it.rarity * (_glow != null ? 0.5f : 1f));
            Glow.color = new Color(col.r, col.g, col.b, GlowAlpha[Mathf.Clamp((int)it.rarity, 0, 4)]);
            Tier.text = Roman[Mathf.Clamp(it.tier, 1, 4) - 1];
            New.gameObject.SetActive(it.fresh);
            var spr = ItemUi.Icon(it);
            Art.sprite = spr;
            Art.enabled = spr != null;
            Art.color = Color.white;
            Glyph.text = spr == null ? ItemDb.SlotName(it.slot) : "";
            Glyph.color = new Color(col.r, col.g, col.b, 0.95f);
        }

        /// <summary>고른 칸 — 금색 테가 숨 쉰다.</summary>
        public void SetFocus(bool on, float pulse)
        {
            Focus.color = _sel != null ? new Color(1f, 1f, 1f, on ? Mathf.Lerp(0.75f, 1f, pulse) : 0f)
                                       : new Color(1f, 0.80f, 0.42f, on ? pulse : 0f);
            Root.localScale = Vector3.one * (on ? 1.04f : 1f);
        }

        /// <summary>표시 테 — 대장장이 창의 '분해할 장비'처럼 여러 칸을 고를 때.</summary>
        public void SetMark(bool on, Color c)
        {
            Mark.color = on ? c : new Color(c.r, c.g, c.b, 0f);
        }
    }

    public static class ItemUi
    {
        static readonly System.Collections.Generic.Dictionary<EquipSlot, Sprite> _icons = new();

        static readonly System.Collections.Generic.Dictionary<string, Sprite> _keyIcons = new();

        /// <summary>장비 아이콘 — 종류별 그림(Icons/item_&lt;key&gt;)이 있으면 그것, 없으면 부위 그림.</summary>
        public static Sprite Icon(Item it)
        {
            if (it == null) return null;
            if (!string.IsNullOrEmpty(it.key))
            {
                if (!_keyIcons.TryGetValue(it.key, out var k))
                {
                    k = Resources.Load<Sprite>("Icons/item_" + it.key);
                    _keyIcons[it.key] = k;
                }
                if (k != null) return k;
            }
            return Icon(it.slot);
        }

        public static Sprite Icon(EquipSlot s)
        {
            if (_icons.TryGetValue(s, out var spr)) return spr;
            spr = Resources.Load<Sprite>("Icons/item_" + s.ToString().ToLowerInvariant());
            _icons[s] = spr;
            return spr;
        }

        const string StatAsset = "StatIcons";
        static int _hasStat = -1;

        /// <summary>능력치 아이콘 글자 (&lt;sprite&gt;) — 글자 색을 따른다. 아이콘 에셋이 없으면 빈 글자.</summary>
        public static string StatIcon(StatId s)
        {
            if (_hasStat < 0) _hasStat = Resources.Load<TMPro.TMP_SpriteAsset>("Sprite Assets/" + StatAsset) != null ? 1 : 0;
            return _hasStat == 1 ? "<sprite=\"" + StatAsset + "\" name=\"" + s.ToString().ToLowerInvariant() + "\" tint=1> " : "";
        }

        /// <summary>장비 설명 — 이름(등급 색) · 등급 · 부위 · 능력치.</summary>
        public static string Describe(Item it, bool withName = true)
        {
            if (it == null) return "";
            var sb = new StringBuilder();
            string hex = ItemDb.Hex(it.rarity);
            if (withName) sb.Append($"<size=120%><b><color=#{hex}>{it.name}</color></b></size>\n");
            sb.Append($"<color=#{hex}>{ItemDb.RarityName(it.rarity)}</color>  <color=#a8a4b8>·  {ItemDb.SlotName(it.slot)}  ·  {it.tier}단계 {ItemDb.TierName[Mathf.Clamp(it.tier, 1, 4) - 1]}</color>\n\n");
            if (it.main != null) sb.Append($"<color=#ffd45a>{StatIcon(it.main.stat)}{ItemDb.Line(it.main)}</color>\n");
            if (it.subs != null)
                foreach (var s in it.subs) sb.Append($"<color=#cfd8ff>{StatIcon(s.stat)}{ItemDb.Line(s)}</color>\n");
            if (it.subs == null || it.subs.Count == 0) sb.Append("<color=#7d7a8c>부가 능력치 없음</color>\n");
            return sb.ToString();
        }

        /// <summary>고른 장비를 끼면 능력치가 어떻게 바뀌나 — 지금 낀 것과 비교.</summary>
        public static string Compare(Item sel, Item cur)
        {
            if (sel == null) return "";
            if (cur == null) return "<color=#9be27a>빈 칸 — 끼면 전부 더해진다</color>";
            if (cur == sel) return "<color=#a8a4b8>장착 중</color>";
            var sb = new StringBuilder();
            foreach (StatId st in System.Enum.GetValues(typeof(StatId)))
            {
                float a = Sum(sel, st), b = Sum(cur, st);
                if (Mathf.Approximately(a, b)) continue;
                float d = a - b;
                string val = ItemDb.IsPercent(st) ? $"{Mathf.Abs(d) * 100f:0.#}%" : $"{Mathf.RoundToInt(Mathf.Abs(d))}";
                sb.Append(d > 0 ? $"<color=#9be27a>▲ {StatIcon(st)}{ItemDb.StatName(st)} +{val}</color>\n" : $"<color=#ff7a6a>▼ {StatIcon(st)}{ItemDb.StatName(st)} -{val}</color>\n");
            }
            return sb.Length == 0 ? "<color=#a8a4b8>능력치 변화 없음</color>" : sb.ToString();
        }

        static float Sum(Item it, StatId st)
        {
            float v = 0f;
            foreach (var r in it.AllStats()) if (r.stat == st) v += r.value;
            return v;
        }
    }
}

namespace Proto.UI
{
    /// <summary>장비 창 · 대장장이 창이 같이 쓰는 창 틀 (스킬 창과 같은 모양).</summary>
    public static class WinKit
    {
        public static readonly Color ColPanel = new Color(0.09f, 0.075f, 0.12f, 0.98f);
        public static readonly Color ColBorder = new Color(0.86f, 0.64f, 0.30f, 1f);
        public static readonly Color ColGold = new Color(1f, 0.80f, 0.42f, 1f);
        public static readonly Color ColMuted = new Color(0.66f, 0.63f, 0.74f, 1f);
        public static readonly Color ColEdge = new Color(0.40f, 0.38f, 0.48f, 0.9f);
        public static readonly Color ColSub = new Color(0.12f, 0.10f, 0.16f, 1f);

        /// <summary>어둡게 덮는 판 + 가운데 창. panel(내용을 붙일 곳)을 돌려준다.</summary>
        public static RectTransform Build(Transform host, string name, float w, float h, string title, string subtitle,
                                          System.Action onClose, out RectTransform root)
        {
            root = UiKit.Stretch(UiKit.Rect(host, name));
            var dim = UiKit.Image(root, "Dim", new Color(0f, 0f, 0f, 0.55f), null, true);
            UiKit.Stretch(dim.rectTransform);

            var border = UiKit.Image(root, "Border", ColBorder, UiArt.RoundRectFill, true);
            border.type = Image.Type.Sliced;
            UiKit.Place(border.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(w + 4f, h + 4f));
            var panelImg = UiKit.Image(border.transform, "Panel", ColPanel, UiArt.RoundRectFill);
            panelImg.type = Image.Type.Sliced;
            var panel = UiKit.Stretch(panelImg.rectTransform);
            panel.offsetMin = new Vector2(2f, 2f);
            panel.offsetMax = new Vector2(-2f, -2f);
            if (UiKit.Frame(panel, 0.5f) != null) border.color = new Color(0f, 0f, 0f, 0f);

            var t = UiKit.Text(panel, "Title", title, 50, TextAlignmentOptions.Left);
            Corner(t.rectTransform, new Vector2(0f, 1f), new Vector2(44f, -20f), new Vector2(400f, 64f));
            t.fontStyle = FontStyles.Bold;
            var sub = UiKit.Text(panel, "Subtitle", subtitle, 20, TextAlignmentOptions.Left);
            Corner(sub.rectTransform, new Vector2(0f, 1f), new Vector2(46f, -84f), new Vector2(700f, 28f));
            sub.color = ColMuted;

            // ✕
            var xSpr = Resources.Load<Sprite>("UI/IconClose");
            var x = UiKit.Image(panel, "CloseX", xSpr != null ? new Color(0.88f, 0.89f, 0.93f, 1f) : new Color(1f, 1f, 1f, 0.001f), xSpr, true);
            Corner(x.rectTransform, new Vector2(1f, 1f), new Vector2(-24f, -26f), new Vector2(52f, 52f));
            if (xSpr == null)
                for (int k = 0; k < 2; k++)
                {
                    var bar = UiKit.Image(x.transform, "Bar", new Color(0.88f, 0.86f, 0.92f, 1f));
                    UiKit.Place(bar.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(32f, 3f));
                    bar.rectTransform.localRotation = Quaternion.Euler(0f, 0f, k == 0 ? 45f : -45f);
                }
            var xb = x.gameObject.AddComponent<Button>();
            UiFocus.NoNav(xb);
            xb.onClick.AddListener(() => onClose?.Invoke());

            Line(panel, -120f);
            return panel;
        }

        public static void Line(RectTransform p, float y)
        {
            if (Resources.Load<Sprite>("UI/DividerLine") != null) { UiKit.Divider(p, y - 1f); return; }
            var line = UiKit.Image(p, "Line", new Color(1f, 1f, 1f, 0.12f));
            line.rectTransform.anchorMin = new Vector2(0f, 1f); line.rectTransform.anchorMax = new Vector2(1f, 1f);
            line.rectTransform.pivot = new Vector2(0.5f, 1f);
            line.rectTransform.offsetMin = new Vector2(26f, y - 2f); line.rectTransform.offsetMax = new Vector2(-26f, y);
        }

        public static void Corner(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        /// <summary>안쪽 판 (설명 칸 등) — 어두운 바탕 + 가는 테.</summary>
        public static RectTransform Box(RectTransform p, string name, Vector2 pos, Vector2 size)
        {
            var bg = UiKit.Image(p, name, ColSub, UiArt.RoundRectFill);
            bg.type = Image.Type.Sliced;
            Corner(bg.rectTransform, new Vector2(0f, 1f), pos, size);
            var ol = UiKit.Image(bg.transform, "Outline", ColEdge, UiArt.RoundRectOutline);
            ol.type = Image.Type.Sliced;
            UiKit.Stretch(ol.rectTransform);
            return bg.rectTransform;
        }

        public static System.Collections.IEnumerator PopIn(RectTransform win)
        {
            var cg = win.GetComponent<CanvasGroup>();
            if (cg == null) cg = win.gameObject.AddComponent<CanvasGroup>();
            float t = 0f;
            while (t < 0.18f)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / 0.18f);
                cg.alpha = k;
                win.localScale = Vector3.one * Mathf.Lerp(0.96f, 1f, 1f - (1f - k) * (1f - k));
                yield return null;
            }
            cg.alpha = 1f;
            win.localScale = Vector3.one;
        }

        /// <summary>창 띄울 자리 — 스킬 창과 같은 캔버스.</summary>
        public static Transform Host()
        {
            var sw = Object.FindFirstObjectByType<SkillWindowView>(FindObjectsInactive.Include);
            if (sw != null && sw.transform.parent != null) return sw.transform.parent;
            var canvas = Object.FindFirstObjectByType<Canvas>();
            return canvas != null ? canvas.transform : null;
        }
    }
}
