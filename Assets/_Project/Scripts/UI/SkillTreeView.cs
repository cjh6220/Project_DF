using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Proto.Core;

namespace Proto.UI
{
    /// <summary>
    /// 노드형 스킬트리 화면.
    ///
    ///   가운데 시작점에서 갈래가 뻗는다. 선행 노드를 찍어야 다음 노드가 열린다.
    ///   드래그로 이동, 휠로 확대/축소. 노드에 올리면 툴팁, 클릭하면 구매.
    ///   아래 막대에 보유 자원, [화면 중앙으로], [시작].
    ///
    /// 화면 구성은 전부 코드로 만든다. 노드는 Progression.Nodes 데이터에서 읽으므로
    /// 노드를 늘릴 때 이 파일은 손대지 않는다.
    /// </summary>
    public class SkillTreeView : MonoBehaviour
    {
        [SerializeField] RunManager run;
        [SerializeField] IrisTransition iris;

        [Header("보기")]
        [SerializeField] float minZoom = 0.55f;
        [SerializeField] float maxZoom = 1.8f;
        [SerializeField] Vector2 homeOffset = new Vector2(0f, -150f);   // 트리가 위로 뻗으므로 시작점을 약간 아래에 둔다

        // ── 색 ──
        static readonly Color ColBg = new Color(0.10f, 0.08f, 0.15f, 1f);
        static readonly Color ColEdgeOff = new Color(0.30f, 0.28f, 0.36f, 1f);
        static readonly Color ColEdgeOn = new Color(0.93f, 0.70f, 0.52f, 1f);
        static readonly Color ColFillBought = new Color(0.95f, 0.74f, 0.56f, 1f);
        static readonly Color ColFillMax = new Color(1f, 0.86f, 0.45f, 1f);
        static readonly Color ColFillOpen = new Color(0.26f, 0.22f, 0.32f, 1f);
        static readonly Color ColFillLocked = new Color(0.15f, 0.14f, 0.19f, 1f);
        static readonly Color ColRingBought = new Color(0.55f, 0.30f, 0.18f, 1f);
        static readonly Color ColRingOpen = new Color(0.80f, 0.60f, 0.45f, 1f);
        static readonly Color ColRingAfford = new Color(1f, 0.62f, 0.25f, 1f);
        static readonly Color ColRingLocked = new Color(0.32f, 0.30f, 0.38f, 1f);
        static readonly Color ColKeystone = new Color(0.95f, 0.35f, 0.40f, 1f);

        class NodeView
        {
            public UpgradeNode Data;
            public RectTransform Root;
            public Image Glow, Ring, Fill;
            public TextMeshProUGUI Icon, Level, Cost;
            public Image Plate;
            public readonly List<Image> InEdges = new List<Image>();
        }

        RectTransform _root, _content, _edges, _nodes, _tooltip;
        TextMeshProUGUI _tipName, _tipBody, _tipCost, _bankText;
        Canvas _canvas;
        float _zoom = 1f;
        NodeView _hover;
        readonly Dictionary<NodeId, NodeView> _views = new Dictionary<NodeId, NodeView>();

        public bool IsOpen => _root != null && _root.gameObject.activeSelf;

        void Awake()
        {
            _canvas = GetComponentInParent<Canvas>();
            Build();
            _root.gameObject.SetActive(false);
        }

        void Start()
        {
            run.Bank.Changed += (_, __) => { if (IsOpen) Refresh(); };
            run.Progression.Changed += () => { if (IsOpen) Refresh(); };
        }

        public void Open()
        {
            _root.gameObject.SetActive(true);
            transform.SetAsLastSibling();
            _zoom = 1f;
            _content.localScale = Vector3.one;
            _content.anchoredPosition = homeOffset;
            HideTip();
            Refresh();
        }

        public void Close()
        {
            HideTip();
            _root.gameObject.SetActive(false);
        }

        // ───────────────────────────── 조립 ─────────────────────────────

        void Build()
        {
            _root = UiKit.Stretch(UiKit.Rect(transform, "SkillTree"));

            var bg = UiKit.Image(_root, "Background", ColBg, null, true);
            UiKit.Stretch(bg.rectTransform);
            var drag = bg.gameObject.AddComponent<TreeDragArea>();
            drag.View = this;

            // 보라빛 성운 몇 덩이 — 영상 레퍼런스의 배경 느낌. 고정 시드라 매번 같다.
            var rng = new System.Random(3);
            for (int i = 0; i < 7; i++)
            {
                var blob = UiKit.Image(bg.transform, "Nebula", new Color(0.35f, 0.22f, 0.55f, 0.10f + (float)rng.NextDouble() * 0.08f), UiKit.Soft);
                UiKit.Place(blob.rectTransform, new Vector2((float)rng.NextDouble(), (float)rng.NextDouble()),
                    Vector2.zero, Vector2.one * (500f + (float)rng.NextDouble() * 700f));
            }

            _content = UiKit.Rect(_root, "Graph");
            UiKit.Place(_content, new Vector2(0.5f, 0.5f), homeOffset, Vector2.zero);
            _edges = UiKit.Rect(_content, "Edges");
            _nodes = UiKit.Rect(_content, "Nodes");

            // 시작점
            var core = MakeCircle(_nodes, "Core", Vector2.zero, 92f);
            core.Fill.color = ColFillBought;
            core.Ring.color = ColRingBought;
            core.Icon.text = "시작";
            core.Icon.fontSize = 24;
            core.Level.text = "";
            core.Cost.text = "";

            foreach (var n in Progression.Nodes)
            {
                var v = MakeCircle(_nodes, "Node_" + n.Id, n.Pos, n.Keystone ? 86f : 66f);
                v.Data = n;
                v.Icon.text = n.Icon;
                v.Icon.fontSize = n.Keystone ? 34 : 28;
                var hover = v.Fill.gameObject.AddComponent<TreeNodeHover>();
                var captured = v;
                hover.Enter = () => ShowTip(captured);
                hover.Exit = () => { if (_hover == captured) HideTip(); };
                hover.Click = () => TryBuy(captured);
                _views[n.Id] = v;
            }

            // 연결선 — 노드보다 먼저 그려져야(아래에 깔려야) 한다
            foreach (var v in _views.Values)
            {
                if (v.Data.Requires.Length == 0) v.InEdges.Add(Edge(Vector2.zero, v.Data.Pos));
                foreach (var r in v.Data.Requires)
                    v.InEdges.Add(Edge(Progression.Find(r).Pos, v.Data.Pos));
            }

            BuildTooltip();
            BuildBottomBar();
            BuildHeader();
        }

        NodeView MakeCircle(Transform parent, string name, Vector2 pos, float size)
        {
            var v = new NodeView();
            v.Root = UiKit.Place(UiKit.Rect(parent, name), new Vector2(0.5f, 0.5f), pos, Vector2.one * size);

            v.Glow = UiKit.Image(v.Root, "Glow", new Color(1f, 0.6f, 0.25f, 0f), UiKit.Soft);
            UiKit.Place(v.Glow.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * size * 2.1f);

            v.Ring = UiKit.Image(v.Root, "Ring", ColRingLocked, UiKit.Circle);
            UiKit.Stretch(v.Ring.rectTransform);

            v.Fill = UiKit.Image(v.Root, "Fill", ColFillLocked, UiKit.Circle, true);
            UiKit.Stretch(v.Fill.rectTransform);
            float inset = size * 0.1f;
            v.Fill.rectTransform.offsetMin = new Vector2(inset, inset);
            v.Fill.rectTransform.offsetMax = new Vector2(-inset, -inset);

            v.Icon = UiKit.Text(v.Root, "Icon", "", 28);
            UiKit.Stretch(v.Icon.rectTransform);
            v.Icon.fontStyle = FontStyles.Bold;

            // 레벨·비용 글씨 뒤에 어두운 받침 — 세로 연결선이 글씨를 가로지르지 않게
            v.Plate = UiKit.Image(v.Root, "Plate", new Color(0.06f, 0.05f, 0.09f, 0.85f));
            UiKit.Place(v.Plate.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, -24f), new Vector2(96f, 44f));
            v.Plate.gameObject.SetActive(false);

            v.Level = UiKit.Text(v.Root, "Level", "", 16);
            UiKit.Place(v.Level.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, -14f), new Vector2(120f, 22f));

            v.Cost = UiKit.Text(v.Root, "Cost", "", 16);
            UiKit.Place(v.Cost.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, -34f), new Vector2(160f, 22f));
            return v;
        }

        Image Edge(Vector2 a, Vector2 b)
        {
            var img = UiKit.Image(_edges, "Edge", ColEdgeOff);
            var rt = img.rectTransform;
            Vector2 d = b - a;
            UiKit.Place(rt, new Vector2(0.5f, 0.5f), (a + b) * 0.5f, new Vector2(d.magnitude, 7f));
            rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            return img;
        }

        void BuildHeader()
        {
            var t = UiKit.Text(_root, "Header", "스킬트리", 34, TextAlignmentOptions.Left);
            t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0f, 1f);
            t.rectTransform.pivot = new Vector2(0f, 1f);
            t.rectTransform.anchoredPosition = new Vector2(40f, -30f);
            t.rectTransform.sizeDelta = new Vector2(600f, 44f);
            t.fontStyle = FontStyles.Bold;

            var h = UiKit.Text(_root, "Hint", "드래그: 이동   휠: 확대/축소   클릭: 구매", 18, TextAlignmentOptions.Left);
            h.rectTransform.anchorMin = h.rectTransform.anchorMax = new Vector2(0f, 1f);
            h.rectTransform.pivot = new Vector2(0f, 1f);
            h.rectTransform.anchoredPosition = new Vector2(42f, -76f);
            h.rectTransform.sizeDelta = new Vector2(800f, 26f);
            h.color = new Color(1f, 1f, 1f, 0.45f);
        }

        void BuildTooltip()
        {
            // 테두리 → 판 → 글씨 순서로 쌓는다 (자식이 부모 위에 그려지므로 루트는 빈 상자)
            _tooltip = UiKit.Rect(_root, "Tooltip");
            _tooltip.anchorMin = _tooltip.anchorMax = new Vector2(0.5f, 0.5f);
            _tooltip.pivot = new Vector2(0.5f, 0f);
            _tooltip.sizeDelta = new Vector2(330f, 150f);

            var border = UiKit.Image(_tooltip, "Border", new Color(0.12f, 0.12f, 0.14f, 1f));
            UiKit.Stretch(border.rectTransform);
            border.rectTransform.offsetMin = new Vector2(-3f, -3f);
            border.rectTransform.offsetMax = new Vector2(3f, 3f);

            var bg = UiKit.Image(_tooltip, "Panel", new Color(0.58f, 0.64f, 0.62f, 1f));
            UiKit.Stretch(bg.rectTransform);

            _tipName = UiKit.Text(_tooltip, "Name", "", 24);
            UiKit.Place(_tipName.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(310f, 30f));
            _tipName.color = new Color(0.08f, 0.08f, 0.1f);
            _tipName.fontStyle = FontStyles.Bold;

            _tipBody = UiKit.Text(_tooltip, "Body", "", 18);
            UiKit.Place(_tipBody.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -66f), new Vector2(310f, 56f));
            _tipBody.color = new Color(0.12f, 0.12f, 0.15f);
            _tipBody.textWrappingMode = TextWrappingModes.Normal;

            var costBg = UiKit.Image(_tooltip, "CostBg", new Color(0.40f, 0.46f, 0.44f, 1f));
            UiKit.Place(costBg.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(310f, 36f));
            _tipCost = UiKit.Text(costBg.transform, "Cost", "", 24);
            UiKit.Stretch(_tipCost.rectTransform);
            _tipCost.fontStyle = FontStyles.Bold;

            _tooltip.gameObject.SetActive(false);
        }

        void BuildBottomBar()
        {
            var bar = UiKit.Image(_root, "BottomBar", new Color(0.07f, 0.07f, 0.09f, 0.96f), null, true);
            var rt = bar.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = new Vector2(0f, 76f);

            _bankText = UiKit.Text(rt, "Bank", "", 24);
            UiKit.Place(_bankText.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 40f));
            _bankText.richText = true;

            var center = UiKit.Button(_root, "CenterButton", "화면 중앙으로", new Vector2(200f, 40f), 19);
            var crt = (RectTransform)center.transform;
            crt.anchorMin = crt.anchorMax = new Vector2(0f, 0f);
            crt.pivot = new Vector2(0f, 0f);
            crt.anchoredPosition = new Vector2(24f, 92f);
            center.onClick.AddListener(Recenter);

            var start = UiKit.Button(rt, "StartButton", "시작", new Vector2(220f, 52f), 26);
            var srt = (RectTransform)start.transform;
            srt.anchorMin = srt.anchorMax = new Vector2(1f, 0.5f);
            srt.pivot = new Vector2(1f, 0.5f);
            srt.anchoredPosition = new Vector2(-28f, 0f);
            start.image.color = new Color(0.95f, 0.62f, 0.28f, 1f);
            start.GetComponentInChildren<TextMeshProUGUI>().color = new Color(0.1f, 0.06f, 0.03f);
            start.onClick.AddListener(() =>
            {
                if (iris != null) iris.Play(() => { Close(); run.Restart(); });
                else { Close(); run.Restart(); }
            });
        }

        // ───────────────────────────── 상태 ─────────────────────────────

        void Refresh()
        {
            var prog = run.Progression;
            foreach (var v in _views.Values)
            {
                var n = v.Data;
                int lv = prog.Level(n.Id);
                bool unlocked = prog.IsUnlocked(n);
                bool maxed = prog.IsMaxed(n);
                bool afford = prog.CanBuy(n, run.Bank);

                if (!unlocked)
                {
                    v.Fill.color = ColFillLocked;
                    v.Ring.color = ColRingLocked;
                    v.Icon.color = new Color(1f, 1f, 1f, 0.25f);
                    v.Level.text = "";
                    v.Cost.text = "<color=#77738a>잠김</color>";
                }
                else
                {
                    v.Fill.color = maxed ? ColFillMax : lv > 0 ? ColFillBought : ColFillOpen;
                    v.Ring.color = afford ? ColRingAfford : lv > 0 ? ColRingBought : (n.Keystone ? ColKeystone : ColRingOpen);
                    v.Icon.color = lv > 0 ? new Color(0.25f, 0.13f, 0.07f) : Color.white;
                    v.Level.text = n.MaxLevel > 1 ? $"{lv}/{n.MaxLevel}" : "";
                    v.Cost.text = maxed
                        ? "<color=#ffd873>MAX</color>"
                        : $"<color={(afford ? "#8fe39a" : "#e08a8a")}>{UiKit.Label(n.Cost)} {prog.NextCost(n)}</color>";
                }

                v.Plate.gameObject.SetActive(true);

                // 들어오는 선은 부모가 찍혀 있으면 밝힌다
                bool parentOn = n.Requires.Length == 0 || prog.IsUnlocked(n);
                foreach (var e in v.InEdges) e.color = parentOn ? ColEdgeOn : ColEdgeOff;
            }

            var sb = new System.Text.StringBuilder();
            foreach (ResourceId id in Enum.GetValues(typeof(ResourceId)))
            {
                var c = ColorUtility.ToHtmlStringRGB(UiKit.ResourceColor(id));
                sb.Append($"<color=#{c}>{UiKit.Label(id)}</color> {run.Bank.Get(id)}      ");
            }
            _bankText.text = sb.ToString().TrimEnd();

            if (_hover != null) ShowTip(_hover);
        }

        void Update()
        {
            if (!IsOpen) return;
            // 살 수 있는 노드는 은은하게 숨 쉰다 — 어디를 눌러야 하는지 한눈에 보인다
            float pulse = 0.25f + 0.2f * Mathf.Sin(Time.unscaledTime * 4f);
            foreach (var v in _views.Values)
            {
                bool afford = run.Progression.CanBuy(v.Data, run.Bank);
                var g = v.Glow.color;
                g.a = afford ? pulse : 0f;
                v.Glow.color = g;
            }
        }

        void TryBuy(NodeView v)
        {
            if (!run.Progression.Buy(v.Data, run.Bank))
            {
                StartCoroutine(Shake(v.Root));
                return;
            }
            StartCoroutine(Pop(v.Root));
        }

        IEnumerator Pop(RectTransform rt)
        {
            float t = 0f;
            while (t < 0.25f)
            {
                t += Time.unscaledDeltaTime;
                float k = t / 0.25f;
                rt.localScale = Vector3.one * (1f + 0.3f * Mathf.Sin(k * Mathf.PI) * (1f - k * 0.5f));
                yield return null;
            }
            rt.localScale = Vector3.one;
        }

        IEnumerator Shake(RectTransform rt)
        {
            Vector2 p0 = rt.anchoredPosition;
            float t = 0f;
            while (t < 0.22f)
            {
                t += Time.unscaledDeltaTime;
                rt.anchoredPosition = p0 + new Vector2(Mathf.Sin(t * 70f) * 6f * (1f - t / 0.22f), 0f);
                yield return null;
            }
            rt.anchoredPosition = p0;
        }

        // ───────────────────────────── 툴팁 ─────────────────────────────

        void ShowTip(NodeView v)
        {
            _hover = v;
            var n = v.Data;
            var prog = run.Progression;
            int lv = prog.Level(n.Id);
            bool maxed = prog.IsMaxed(n);
            bool unlocked = prog.IsUnlocked(n);

            _tipName.text = n.MaxLevel > 1 ? $"{n.Name}  <size=18>Lv {lv}/{n.MaxLevel}</size>" : n.Name;

            string body = n.Effect;
            if (n.Total != null && n.MaxLevel > 1)
                body += "\n<size=16>" + (maxed ? "현재 " + n.Total(lv) : $"{n.Total(lv)} → {n.Total(lv + 1)}") + "</size>";
            if (!unlocked)
            {
                var req = new System.Text.StringBuilder();
                foreach (var r in n.Requires) req.Append(Progression.Find(r).Name).Append(' ');
                body += "\n<size=16><color=#6a2020>" + req.ToString().Trim() + " 필요</color></size>";
            }
            _tipBody.text = body;

            _tipCost.text = maxed ? "완료"
                : $"<color=#{ColorUtility.ToHtmlStringRGB(UiKit.ResourceColor(n.Cost))}>{UiKit.Label(n.Cost)}</color> {prog.NextCost(n)}";
            _tipCost.color = prog.CanBuy(n, run.Bank) || maxed ? Color.white : new Color(1f, 0.55f, 0.55f);

            // 노드 바로 위에 띄운다 (확대/이동과 무관하게 화면 좌표로)
            _tooltip.gameObject.SetActive(true);
            _tooltip.SetAsLastSibling();
            Vector3 world = v.Root.TransformPoint(new Vector3(0f, v.Root.rect.height * 0.5f + 18f, 0f));
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_root,
                RectTransformUtility.WorldToScreenPoint(null, world), null, out local);
            _tooltip.anchoredPosition = local;
        }

        void HideTip()
        {
            _hover = null;
            if (_tooltip != null) _tooltip.gameObject.SetActive(false);
        }

        // ───────────────────────────── 이동 · 확대 ─────────────────────────────

        void Recenter()
        {
            StopAllCoroutines();
            StartCoroutine(Glide(homeOffset, 1f));
        }

        IEnumerator Glide(Vector2 to, float zoom)
        {
            HideTip();
            Vector2 from = _content.anchoredPosition;
            float z0 = _zoom, t = 0f;
            while (t < 0.3f)
            {
                t += Time.unscaledDeltaTime;
                float k = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / 0.3f), 3f);
                _content.anchoredPosition = Vector2.Lerp(from, to, k);
                _zoom = Mathf.Lerp(z0, zoom, k);
                _content.localScale = Vector3.one * _zoom;
                yield return null;
            }
        }

        internal void OnDragged(Vector2 screenDelta)
        {
            HideTip();
            float s = _canvas != null ? _canvas.scaleFactor : 1f;
            _content.anchoredPosition += screenDelta / s;
        }

        internal void OnScrolled(Vector2 screenPos, float delta)
        {
            if (Mathf.Abs(delta) < 0.01f) return;
            HideTip();
            float newZoom = Mathf.Clamp(_zoom * (delta > 0 ? 1.12f : 1f / 1.12f), minZoom, maxZoom);

            // 커서 아래 지점이 그대로 있도록 확대한다
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_content, screenPos, null, out var before);
            _zoom = newZoom;
            _content.localScale = Vector3.one * _zoom;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_content, screenPos, null, out var after);
            _content.anchoredPosition += (after - before) * _zoom;
        }
    }

    /// <summary>배경을 끌어 트리를 옮기고, 휠로 확대한다.</summary>
    public class TreeDragArea : MonoBehaviour, IDragHandler, IScrollHandler
    {
        public SkillTreeView View;
        public void OnDrag(PointerEventData e) => View.OnDragged(e.delta);
        public void OnScroll(PointerEventData e) => View.OnScrolled(e.position, e.scrollDelta.y);
    }

    /// <summary>노드 위에 올렸을 때 / 뗐을 때 / 눌렀을 때.</summary>
    public class TreeNodeHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, IScrollHandler, IDragHandler
    {
        public Action Enter, Exit, Click;
        public void OnPointerEnter(PointerEventData e) => Enter?.Invoke();
        public void OnPointerExit(PointerEventData e) => Exit?.Invoke();
        public void OnPointerClick(PointerEventData e) { if (!e.dragging) Click?.Invoke(); }

        // 노드 위에서도 휠과 드래그가 먹도록 배경으로 넘긴다
        public void OnScroll(PointerEventData e) => ExecuteEvents.ExecuteHierarchy(transform.root.GetComponentInChildren<TreeDragArea>().gameObject, e, ExecuteEvents.scrollHandler);
        public void OnDrag(PointerEventData e) => ExecuteEvents.ExecuteHierarchy(transform.root.GetComponentInChildren<TreeDragArea>().gameObject, e, ExecuteEvents.dragHandler);
    }
}
