using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using Proto.Core;

namespace Proto.UI
{
    /// <summary>
    /// 대장장이 창 — 마을 대장장이 NPC가 연다. 탭 두 개 (Q · E / L1 · R1로 넘긴다).
    ///
    ///   분해  가방의 장비를 골라(확인 키로 표시) 장비 가루로 바꾼다.
    ///         R = 등급 일괄 표시 (일반 이하 → 고급 이하 → 희귀 이하 → 해제), Tab = 분해 실행
    ///   제작  부위를 고르고 가루 + 골드를 내면 그 부위 장비가 무작위 등급 · 옵션으로 나온다.
    ///         제작 단계 = 보스를 잡은 던전 중 가장 깊은 곳 (아직 없으면 1단계)
    ///
    /// 장착 중인 장비는 가방에 없으니 분해 목록에 나오지 않는다 — 실수로 끼고 있는 걸 갈아 버리지 않게.
    /// </summary>
    public class SmithView : MonoBehaviour
    {
        public static SmithView Instance { get; private set; }
        public bool IsOpen => _root != null && _root.gameObject.activeSelf;
        public event System.Action Closed;

        const float W = 1300f, H = 860f;
        const float Cell = 80f, Gap = 8f;
        const int Cols = 8, VisibleRows = 7;

        RunManager _run;
        Proto.Town.VillageController _village;
        RectTransform _root, _panel, _win, _content, _dismantlePage, _craftPage;
        TextMeshProUGUI _money, _hint, _tabA, _tabB;
        Image _tabAImg, _tabBImg;

        // 분해
        readonly List<ItemCell> _cells = new();
        readonly List<Item> _view = new();
        readonly HashSet<Item> _marked = new();
        TextMeshProUGUI _dDetail, _dSum, _dBag, _dBtnLabel;
        Button _dBtn;
        int _sel, _scroll, _bulk;

        // 제작
        readonly List<RectTransform> _slotRows = new();
        readonly List<Image> _slotRowBg = new();
        TextMeshProUGUI _cInfo, _cOdds, _cResult, _cBtnLabel;
        ItemCell _cResultCell;
        Button _cBtn;
        int _craftSel;

        int _tab;   // 0 분해 · 1 제작
        float _openedAt;
        static readonly string[] BulkNames = { "일반 이하", "고급 이하", "희귀 이하", "표시 해제" };
        static readonly Color ColMark = new Color(1f, 0.38f, 0.30f, 0.95f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Instance != null) return;
            var host = WinKit.Host();
            if (host == null) return;
            var go = new GameObject("SmithView", typeof(RectTransform));
            go.transform.SetParent(host, false);
            UiKit.Stretch((RectTransform)go.transform);
            go.AddComponent<SmithView>();
        }

        void Awake()
        {
            Instance = this;
            _run = FindFirstObjectByType<RunManager>();
            Build();
            _root.gameObject.SetActive(false);
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void Start()
        {
            if (_run != null) _run.Inventory.Changed += () => { if (IsOpen) Refresh(); };
        }

        void OnEnable() => GameInput.SchemeChanged += OnScheme;
        void OnDisable() => GameInput.SchemeChanged -= OnScheme;
        void OnScheme() { if (IsOpen) Refresh(); }

        public void Open(Proto.Town.VillageController village)
        {
            _village = village;
            _openedAt = Time.unscaledTime;
            UiFocus.Block(this, () => this != null && IsOpen);
            _root.gameObject.SetActive(true);
            transform.SetAsLastSibling();
            _marked.Clear();
            _sel = 0; _scroll = 0; _bulk = 0;
            _cResult.text = "";
            _cResultCell.Root.gameObject.SetActive(false);
            Refresh();
            StartCoroutine(WinKit.PopIn(_win));
        }

        public void Close()
        {
            bool was = IsOpen;
            _root.gameObject.SetActive(false);
            UiFocus.Unblock(this);
            if (was) { _run.Inventory.MarkSeen(); Closed?.Invoke(); }
        }

        void Update()
        {
            if (!IsOpen) return;
            if (GameInput.CancelPressed) { Close(); return; }
            if (Time.unscaledTime - _openedAt < 0.15f) return;

            int page = GameInput.PageStep;
            if (page != 0) { _tab = Mathf.Clamp(_tab + page, 0, 1); Refresh(); }

            var d = GameInput.Navigate();
            if (_tab == 0) UpdateDismantle(d); else UpdateCraft(d);
        }

        // ───────────────────────────── 분해 ─────────────────────────────

        void UpdateDismantle(Vector2Int d)
        {
            if (d != Vector2Int.zero && _view.Count > 0)
            {
                int col = _sel % Cols, row = _sel / Cols, rows = (_view.Count + Cols - 1) / Cols;
                if (d.x != 0) col = Mathf.Clamp(col + d.x, 0, Cols - 1);
                if (d.y != 0) row = Mathf.Clamp(row - d.y, 0, rows - 1);
                _sel = Mathf.Min(_view.Count - 1, row * Cols + col);
                EnsureVisible();
                PaintDismantle();
            }
            if (GameInput.InteractPressed) ToggleMark(_sel);
            if (GameInput.AltAPressed) BulkMark();
            if (GameInput.AltBPressed) DoDismantle();

            var m = Mouse.current;
            if (m != null)
            {
                float wheel = m.scroll.ReadValue().y;
                if (Mathf.Abs(wheel) > 0.01f) { _scroll = Mathf.Clamp(_scroll - (int)Mathf.Sign(wheel), 0, MaxScroll()); LayoutCells(); }
            }

            float pulse = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 5f);
            for (int i = 0; i < _cells.Count; i++)
            {
                _cells[i].SetFocus(i == _sel && i < _view.Count, pulse);
                _cells[i].SetMark(i < _view.Count && _marked.Contains(_view[i]), ColMark);
            }
        }

        void ToggleMark(int i)
        {
            if (i < 0 || i >= _view.Count) return;
            var it = _view[i];
            if (!_marked.Add(it)) _marked.Remove(it);
            PaintDismantle();
        }

        void BulkMark()
        {
            _marked.Clear();
            if (_bulk < 3)
                foreach (var it in _view) if ((int)it.rarity <= _bulk) _marked.Add(it);
            _bulk = (_bulk + 1) % BulkNames.Length;
            PaintDismantle();
        }

        void DoDismantle()
        {
            if (_marked.Count == 0) return;
            int dust = 0;
            foreach (var it in _marked) dust += ItemDb.DismantleDust(it, _run.SalvageBonus);
            var list = new List<Item>(_marked);
            _marked.Clear();
            foreach (var it in list) _run.Inventory.Remove(it);
            _run.Bank.Add(ResourceId.Dust, dust);
            _run.RequestSave();
            Refresh();
            _dSum.text = $"<color=#9be27a>{list.Count}개 분해 — {UiKit.Icon(ResourceId.Dust)} +{dust}</color>";
            StartCoroutine(Pop(_money.rectTransform));
        }

        void OnClickCell(int i)
        {
            if (_tab != 0 || i >= _view.Count) return;
            if (_sel == i) ToggleMark(i);
            else { _sel = i; PaintDismantle(); }
        }

        int MaxScroll()
        {
            int rows = Mathf.Max((_view.Count + Cols - 1) / Cols, VisibleRows);
            return Mathf.Max(0, rows - VisibleRows);
        }

        void EnsureVisible()
        {
            int row = _sel / Cols;
            if (row < _scroll) _scroll = row;
            if (row >= _scroll + VisibleRows) _scroll = row - VisibleRows + 1;
            _scroll = Mathf.Clamp(_scroll, 0, MaxScroll());
            LayoutCells();
        }

        void LayoutCells()
        {
            int need = Mathf.Max(_view.Count, _run.Inventory.Capacity);
            for (int i = 0; i < _cells.Count; i++)
            {
                int col = i % Cols, row = i / Cols - _scroll;
                _cells[i].Root.anchoredPosition = new Vector2(col * (Cell + Gap), -row * (Cell + Gap));
                _cells[i].Root.gameObject.SetActive(i < need && row >= 0 && row < VisibleRows);
            }
        }

        void PaintDismantle()
        {
            var it = _sel >= 0 && _sel < _view.Count ? _view[_sel] : null;
            _dDetail.text = it != null
                ? ItemUi.Describe(it) + $"\n<color=#a8a4b8>분해하면</color> {UiKit.Icon(ResourceId.Dust)} <color=#9fdcf2>{ItemDb.DismantleDust(it, _run.SalvageBonus)}</color>"
                  + (_marked.Contains(it) ? "\n<color=#ff8a7a>분해할 장비로 표시됨</color>" : "")
                : "<color=#7d7a8c>분해할 장비가 없습니다\n\n장착 중인 장비는 여기에 나오지 않는다</color>";
            int dust = 0;
            foreach (var m in _marked) dust += ItemDb.DismantleDust(m, _run.SalvageBonus);
            _dSum.text = _marked.Count > 0
                ? $"표시한 장비 <color=#ff8a7a>{_marked.Count}</color>개  →  {UiKit.Icon(ResourceId.Dust)} <color=#9fdcf2>+{dust}</color>"
                : "<color=#7d7a8c>확인 키로 분해할 장비를 표시하세요</color>";
            _dBtn.interactable = _marked.Count > 0;
            _dBtnLabel.text = $"분해  <size=80%>{InputGlyphs.Of(Act.AltB)}</size>";
        }

        // ───────────────────────────── 제작 ─────────────────────────────

        int CraftTier()
        {
            int best = 1;
            if (_village != null && _village.Dungeons != null)
                for (int i = 0; i < _village.Dungeons.Length; i++)
                {
                    var dd = _village.Dungeons[i];
                    if (dd != null && _run.IsCleared(dd)) best = Mathf.Max(best, dd.danger);
                }
            return Mathf.Clamp(best, 1, 4);
        }

        void UpdateCraft(Vector2Int d)
        {
            if (d.y != 0) { _craftSel = Mathf.Clamp(_craftSel - d.y, 0, 6); PaintCraft(); }
            if (GameInput.InteractPressed) DoCraft();
        }

        void DoCraft()
        {
            int tier = CraftTier();
            int dust = ItemDb.CraftDust(tier), gold = ItemDb.CraftGold(tier);
            if (_run.Inventory.Full) { _cResult.text = "<color=#ff7a6a>가방이 가득 찼습니다</color>"; return; }
            if (_run.Bank.Get(ResourceId.Dust) < dust || _run.Bank.Get(ResourceId.Gold) < gold)
            {
                _cResult.text = "<color=#ff7a6a>재료가 모자랍니다</color>";
                return;
            }
            _run.Bank.Add(ResourceId.Dust, -dust);
            _run.Bank.Add(ResourceId.Gold, -gold);
            var it = ItemDb.Roll(tier, ItemDb.RollRarity(ItemDb.CraftWeights), (EquipSlot)_craftSel);
            _run.Inventory.Add(it);
            _run.RequestSave();
            _cResultCell.Root.gameObject.SetActive(true);
            _cResultCell.Set(it, "");
            _cResult.text = ItemUi.Describe(it);
            StartCoroutine(Pop(_cResultCell.Root));
            if (it.rarity >= Rarity.Epic && Proto.Feel.Feel.I != null) Proto.Feel.Haptics.Pulse(0.4f, 0.6f, 0.2f);
            RefreshMoney();
            PaintCraft();
        }

        void PaintCraft()
        {
            for (int i = 0; i < _slotRowBg.Count; i++)
                _slotRowBg[i].color = i == _craftSel ? new Color(0.22f, 0.18f, 0.12f, 1f) : new Color(0.12f, 0.10f, 0.16f, 1f);
            int tier = CraftTier();
            int dust = ItemDb.CraftDust(tier), gold = ItemDb.CraftGold(tier);
            bool okD = _run.Bank.Get(ResourceId.Dust) >= dust, okG = _run.Bank.Get(ResourceId.Gold) >= gold;
            var slot = (EquipSlot)_craftSel;
            _cInfo.text = $"<size=120%><b>{ItemDb.SlotName(slot)} 제작</b></size>\n"
                        + $"<color=#a8a4b8>제작 단계</color>  {tier}단계  <size=80%><color=#7d7a8c>(보스를 잡은 가장 깊은 던전)</color></size>\n\n"
                        + $"<color=#a8a4b8>비용</color>   {UiKit.Icon(ResourceId.Dust)} <color=#{(okD ? "9fdcf2" : "ff7a6a")}>{dust}</color>    {UiKit.Icon(ResourceId.Gold)} <color=#{(okG ? "ffd45a" : "ff7a6a")}>{gold}</color>";
            var sb = new System.Text.StringBuilder("<color=#a8a4b8>나올 등급</color>\n");
            float total = 0f;
            foreach (var w in ItemDb.CraftWeights) total += w;
            for (int i = 0; i < ItemDb.CraftWeights.Length; i++)
            {
                var r = (Rarity)i;
                sb.Append($"<color=#{ItemDb.Hex(r)}>{ItemDb.RarityName(r)}</color> {ItemDb.CraftWeights[i] / total * 100f:0.#}%   ");
                if (i == 2) sb.Append('\n');
            }
            _cOdds.text = sb.ToString();
            _cBtn.interactable = okD && okG && !_run.Inventory.Full;
            _cBtnLabel.text = $"제작  <size=80%>{InputGlyphs.Of(Act.Interact)}</size>";
        }

        // ───────────────────────────── 공용 ─────────────────────────────

        void Refresh()
        {
            _view.Clear();
            _view.AddRange(_run.Inventory.Bag);
            _view.Sort((a, b) => a.rarity != b.rarity ? a.rarity.CompareTo(b.rarity) : a.slot.CompareTo(b.slot));   // 낮은 등급부터 — 갈아 버릴 것이 앞에
            _marked.RemoveWhere(it => !_view.Contains(it));
            int need = Mathf.Max(_view.Count, _run.Inventory.Capacity);
            while (_cells.Count < need)
            {
                int idx = _cells.Count;
                var c = ItemCell.Create(_content, "Cell" + idx, Vector2.zero, Cell);
                c.Button.onClick.AddListener(() => OnClickCell(idx));
                _cells.Add(c);
            }
            for (int i = 0; i < _cells.Count; i++) _cells[i].Set(i < _view.Count ? _view[i] : null, "");
            _sel = Mathf.Clamp(_sel, 0, Mathf.Max(0, _view.Count - 1));
            EnsureVisible();
            _dBag.text = $"가방  {_run.Inventory.Count} / {_run.Inventory.Capacity}    <color=#a8a4b8>{InputGlyphs.Of(Act.AltA)} 일괄 표시: {BulkNames[_bulk]}</color>";

            _dismantlePage.gameObject.SetActive(_tab == 0);
            _craftPage.gameObject.SetActive(_tab == 1);
            _tabAImg.color = _tab == 0 ? new Color(0.30f, 0.24f, 0.14f, 1f) : new Color(0.12f, 0.10f, 0.16f, 1f);
            _tabBImg.color = _tab == 1 ? new Color(0.30f, 0.24f, 0.14f, 1f) : new Color(0.12f, 0.10f, 0.16f, 1f);
            _tabA.color = _tab == 0 ? WinKit.ColGold : WinKit.ColMuted;
            _tabB.color = _tab == 1 ? WinKit.ColGold : WinKit.ColMuted;
            _tabA.text = $"{InputGlyphs.Of(Act.PagePrev)}  분해";
            _tabB.text = $"제작  {InputGlyphs.Of(Act.PageNext)}";

            RefreshMoney();
            PaintDismantle();
            PaintCraft();
            _hint.text = _tab == 0
                ? $"{InputGlyphs.Of(Act.Navigate)} 이동   ·   {InputGlyphs.Of(Act.Interact)} 표시   ·   {InputGlyphs.Of(Act.AltA)} 일괄 표시   ·   {InputGlyphs.Of(Act.AltB)} 분해   ·   {InputGlyphs.Of(Act.Cancel)} 닫기"
                : $"{InputGlyphs.Of(Act.Navigate)} 부위 고르기   ·   {InputGlyphs.Of(Act.Interact)} 제작   ·   {InputGlyphs.Of(Act.Cancel)} 닫기";
        }

        void RefreshMoney()
        {
            _money.text = $"{UiKit.Icon(ResourceId.Dust)} {_run.Bank.Get(ResourceId.Dust)}    {UiKit.Icon(ResourceId.Gold)} {_run.Bank.Get(ResourceId.Gold)}";
        }

        System.Collections.IEnumerator Pop(RectTransform rt)
        {
            float t = 0f;
            while (t < 0.25f)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / 0.25f);
                rt.localScale = Vector3.one * (1f + 0.15f * Mathf.Sin(k * Mathf.PI));
                yield return null;
            }
            rt.localScale = Vector3.one;
        }

        // ───────────────────────────── 조립 ─────────────────────────────

        void Build()
        {
            _panel = WinKit.Build(transform, "SmithWindow", W, H, "대장장이", "쓰지 않는 장비를 갈아 가루로 — 가루로 원하는 부위의 새 장비를", Close, out _root);
            _win = (RectTransform)_panel.parent;

            _money = UiKit.Text(_panel, "Money", "", 24, TextAlignmentOptions.Right);
            WinKit.Corner(_money.rectTransform, new Vector2(1f, 1f), new Vector2(-100f, -30f), new Vector2(420f, 44f));

            // 탭
            _tabAImg = TabButton("TabDismantle", new Vector2(330f, -30f), out _tabA, () => { _tab = 0; Refresh(); });
            _tabBImg = TabButton("TabCraft", new Vector2(500f, -30f), out _tabB, () => { _tab = 1; Refresh(); });

            // ── 분해 페이지 ──
            _dismantlePage = UiKit.Stretch(UiKit.Rect(_panel, "Dismantle"));
            const float bx = 40f, by = -168f;
            float gridW = Cols * Cell + (Cols - 1) * Gap;
            _dBag = UiKit.Text(_dismantlePage, "Bag", "", 20, TextAlignmentOptions.Left);
            WinKit.Corner(_dBag.rectTransform, new Vector2(0f, 1f), new Vector2(bx, -132f), new Vector2(gridW, 28f));
            _dBag.richText = true;
            var vp = UiKit.Rect(_dismantlePage, "Viewport");
            WinKit.Corner(vp, new Vector2(0f, 1f), new Vector2(bx - 8f, by + 8f), new Vector2(gridW + 16f, VisibleRows * (Cell + Gap) + 8f));
            vp.gameObject.AddComponent<RectMask2D>();
            _content = UiKit.Rect(vp, "Content");
            _content.anchorMin = _content.anchorMax = new Vector2(0f, 1f);
            _content.pivot = new Vector2(0f, 1f);
            _content.anchoredPosition = new Vector2(8f, -8f);
            _content.sizeDelta = new Vector2(gridW, 10f);

            float dx = bx + gridW + 40f;
            var box = WinKit.Box(_dismantlePage, "Detail", new Vector2(dx, -150f), new Vector2(W - dx - 30f, 610f));
            _dDetail = UiKit.Text(box, "Desc", "", 21, TextAlignmentOptions.TopLeft);
            WinKit.Corner(_dDetail.rectTransform, new Vector2(0f, 1f), new Vector2(18f, -16f), new Vector2(W - dx - 66f, 360f));
            _dDetail.textWrappingMode = TextWrappingModes.Normal;
            _dDetail.richText = true;
            _dSum = UiKit.Text(box, "Sum", "", 20, TextAlignmentOptions.TopLeft);
            WinKit.Corner(_dSum.rectTransform, new Vector2(0f, 1f), new Vector2(18f, -400f), new Vector2(W - dx - 66f, 80f));
            _dSum.textWrappingMode = TextWrappingModes.Normal;
            _dSum.richText = true;
            _dBtn = UiKit.Button(box, "DismantleBtn", "분해", new Vector2(260f, 54f), 22);
            UiKit.Style(_dBtn, UiKit.ButtonKind.Danger);
            UiFocus.NoNav(_dBtn);
            var brt = (RectTransform)_dBtn.transform;
            brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0f);
            brt.anchoredPosition = new Vector2(0f, 44f);
            _dBtnLabel = _dBtn.GetComponentInChildren<TextMeshProUGUI>();
            _dBtnLabel.richText = true;
            _dBtn.onClick.AddListener(DoDismantle);

            // ── 제작 페이지 ──
            _craftPage = UiKit.Stretch(UiKit.Rect(_panel, "Craft"));
            for (int i = 0; i < 7; i++)
            {
                int idx = i;
                var s = (EquipSlot)i;
                var bg = UiKit.Image(_craftPage, "Row_" + s, new Color(0.12f, 0.10f, 0.16f, 1f), UiArt.RoundRectFill, true);
                bg.type = Image.Type.Sliced;
                WinKit.Corner(bg.rectTransform, new Vector2(0f, 1f), new Vector2(40f, -150f - i * 84f), new Vector2(360f, 74f));
                var ol = UiKit.Image(bg.transform, "Outline", WinKit.ColEdge, UiArt.RoundRectOutline);
                ol.type = Image.Type.Sliced;
                UiKit.Stretch(ol.rectTransform);
                var icon = UiKit.Image(bg.transform, "Icon", Color.white, ItemUi.Icon(s));
                WinKit.Corner(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(56f, 56f));
                icon.rectTransform.pivot = new Vector2(0f, 0.5f);
                icon.enabled = icon.sprite != null;
                icon.preserveAspect = true;
                var t = UiKit.Text(bg.transform, "Name", ItemDb.SlotName(s), 26, TextAlignmentOptions.Left);
                WinKit.Corner(t.rectTransform, new Vector2(0f, 0.5f), new Vector2(84f, 0f), new Vector2(260f, 40f));
                t.rectTransform.pivot = new Vector2(0f, 0.5f);
                var b = bg.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                UiFocus.NoNav(b);
                b.onClick.AddListener(() => { if (_craftSel == idx) DoCraft(); else { _craftSel = idx; PaintCraft(); } });
                _slotRows.Add(bg.rectTransform);
                _slotRowBg.Add(bg);
            }

            var cbox = WinKit.Box(_craftPage, "Info", new Vector2(430f, -150f), new Vector2(W - 460f, 610f));
            _cInfo = UiKit.Text(cbox, "Info", "", 22, TextAlignmentOptions.TopLeft);
            WinKit.Corner(_cInfo.rectTransform, new Vector2(0f, 1f), new Vector2(22f, -18f), new Vector2(W - 510f, 150f));
            _cInfo.textWrappingMode = TextWrappingModes.Normal;
            _cInfo.richText = true;
            _cOdds = UiKit.Text(cbox, "Odds", "", 19, TextAlignmentOptions.TopLeft);
            WinKit.Corner(_cOdds.rectTransform, new Vector2(0f, 1f), new Vector2(22f, -176f), new Vector2(W - 510f, 90f));
            _cOdds.richText = true;
            _cResultCell = ItemCell.Create(cbox, "Result", new Vector2(22f, -290f), 120f);
            _cResult = UiKit.Text(cbox, "ResultText", "", 20, TextAlignmentOptions.TopLeft);
            WinKit.Corner(_cResult.rectTransform, new Vector2(0f, 1f), new Vector2(166f, -290f), new Vector2(W - 660f, 220f));
            _cResult.textWrappingMode = TextWrappingModes.Normal;
            _cResult.richText = true;
            _cBtn = UiKit.Button(cbox, "CraftBtn", "제작", new Vector2(260f, 54f), 22);
            UiKit.Style(_cBtn, UiKit.ButtonKind.Primary);
            UiFocus.NoNav(_cBtn);
            var crt = (RectTransform)_cBtn.transform;
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0f);
            crt.anchoredPosition = new Vector2(0f, 44f);
            _cBtnLabel = _cBtn.GetComponentInChildren<TextMeshProUGUI>();
            _cBtnLabel.richText = true;
            _cBtn.onClick.AddListener(DoCraft);

            _hint = UiKit.Text(_panel, "Hint", "", 18);
            _hint.rectTransform.anchorMin = new Vector2(0f, 0f); _hint.rectTransform.anchorMax = new Vector2(1f, 0f);
            _hint.rectTransform.pivot = new Vector2(0.5f, 0f);
            _hint.rectTransform.anchoredPosition = new Vector2(0f, 22f);
            _hint.rectTransform.sizeDelta = new Vector2(-60f, 30f);
            _hint.color = WinKit.ColMuted;
            _hint.richText = true;
        }

        Image TabButton(string name, Vector2 pos, out TextMeshProUGUI label, System.Action onClick)
        {
            var bg = UiKit.Image(_panel, name, new Color(0.12f, 0.10f, 0.16f, 1f), UiArt.RoundRectFill, true);
            bg.type = Image.Type.Sliced;
            WinKit.Corner(bg.rectTransform, new Vector2(0f, 1f), pos, new Vector2(160f, 46f));
            var ol = UiKit.Image(bg.transform, "Outline", WinKit.ColEdge, UiArt.RoundRectOutline);
            ol.type = Image.Type.Sliced;
            UiKit.Stretch(ol.rectTransform);
            label = UiKit.Text(bg.transform, "Label", "", 22);
            UiKit.Stretch(label.rectTransform);
            label.richText = true;
            var b = bg.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            UiFocus.NoNav(b);
            b.onClick.AddListener(() => onClick());
            return bg;
        }
    }
}
