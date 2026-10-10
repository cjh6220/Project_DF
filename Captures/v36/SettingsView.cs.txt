using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using Proto.Core;

namespace Proto.UI
{
    /// <summary>
    /// 설정 창 — 마을 메뉴의 [설정], 던전 포기 창의 [설정]에서 연다.
    ///
    ///   탭    화면 · 사운드 · 조작 · 게임
    ///   조작  ↑↓ 줄 고르기 · ←→ 값 바꾸기 · 결정키 = 키 바꾸기 / 다음 값 · Esc 닫기 (마우스는 ◀ ▶ 클릭)
    ///
    /// 바꾸는 즉시 적용 · 저장한다 (GameSettings.Commit). 따로 "적용" 버튼이 없다.
    /// </summary>
    public class SettingsView : MonoBehaviour
    {
        public static bool IsOpen { get; private set; }
        /// <summary>닫힌 프레임 — 같은 Esc로 뒤의 메뉴까지 닫히지 않게 다른 창이 본다.</summary>
        public static int ClosedFrame { get; private set; } = -1;

        static readonly string[] TabNames = { "화면", "사운드", "조작", "게임" };
        const float RowH = 32f, RowW = 820f;

        class Row
        {
            public string Label;
            public Func<string> Value;
            public Action<int> Step;      // ←→ (-1 / +1)
            public Action Activate;       // 결정키 (없으면 Step(+1))
            public Func<bool> Enabled;
            public RectTransform Rt;
            public Image Bg;
            public TextMeshProUGUI LabelText, ValueText;
            public GameObject Arrows;
        }

        RectTransform _root, _panel, _list;
        readonly List<Button> _tabs = new();
        readonly List<Row> _rows = new();
        TextMeshProUGUI _hint;
        int _tab, _focus;                 // _focus -1 = 탭 줄
        bool _rebinding;
        int _rebindEndFrame = -10;
        float _prevTimeScale = 1f;

        void Awake()
        {
            IsOpen = false;
            Build();
            _root.gameObject.SetActive(false);
        }

        void OnDestroy() { if (IsOpen) IsOpen = false; }

        // ───────────────────────────── 열고 닫기 ─────────────────────────────

        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            _root.gameObject.SetActive(true);
            transform.SetAsLastSibling();
            _root.SetAsLastSibling();
            _prevTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            ShowTab(_tab);
            _focus = 0;
            Refresh();
            GameInput.SchemeChanged += Refresh;
        }

        public void Close()
        {
            if (!IsOpen || _rebinding) return;
            IsOpen = false;
            ClosedFrame = Time.frameCount;
            _root.gameObject.SetActive(false);
            Time.timeScale = _prevTimeScale;
            GameInput.SchemeChanged -= Refresh;
        }

        // ───────────────────────────── 만들기 ─────────────────────────────

        void Build()
        {
            _root = UiKit.Stretch(UiKit.Rect(transform, "Settings"));
            var dim = UiKit.Image(_root, "Dim", new Color(0f, 0f, 0f, 0.55f), null, true);
            UiKit.Stretch(dim.rectTransform);

            var panel = UiKit.Image(_root, "Panel", new Color(0.05f, 0.06f, 0.09f, 0.985f));
            _panel = panel.rectTransform;
            UiKit.Place(_panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(960f, 800f));
            UiKit.Frame(_panel, 0.6f, 0f, UiKit.FrameKind.Window);
            panel.color = new Color(0.05f, 0.06f, 0.09f, 0.985f);   // 뒤의 메뉴가 비치지 않게 거의 불투명 (Frame이 유리색으로 바꾼 것을 덮는다)

            var title = UiKit.Text(_panel, "Title", "설정", 38);
            UiKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -46f), new Vector2(600f, 48f));
            title.fontStyle = FontStyles.Bold;

            for (int i = 0; i < TabNames.Length; i++)
            {
                int idx = i;
                var b = UiKit.Button(_panel, "Tab_" + TabNames[i], TabNames[i], new Vector2(180f, 48f), 22);
                var rt = (RectTransform)b.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(-285f + i * 190f, -112f);
                b.onClick.AddListener(() => { _focus = -1; ShowTab(idx); });
                _tabs.Add(b);
            }

            _list = UiKit.Rect(_panel, "List");
            _list.anchorMin = _list.anchorMax = new Vector2(0.5f, 1f);
            _list.pivot = new Vector2(0.5f, 1f);
            _list.anchoredPosition = new Vector2(0f, -160f);
            _list.sizeDelta = new Vector2(RowW, 560f);

            var reset = UiKit.Button(_panel, "Defaults", "기본값으로", new Vector2(180f, 44f), 19);
            var rr = (RectTransform)reset.transform;
            rr.anchorMin = rr.anchorMax = new Vector2(0f, 0f);
            rr.pivot = new Vector2(0f, 0f);
            rr.anchoredPosition = new Vector2(70f, 34f);
            reset.onClick.AddListener(() => { GameSettings.ResetDefaults(); Refresh(); });

            var close = UiKit.Button(_panel, "Close", "닫기", new Vector2(160f, 44f), 19);
            UiKit.Style(close, UiKit.ButtonKind.Primary);
            var cr = (RectTransform)close.transform;
            cr.anchorMin = cr.anchorMax = new Vector2(1f, 0f);
            cr.pivot = new Vector2(1f, 0f);
            cr.anchoredPosition = new Vector2(-70f, 34f);
            close.onClick.AddListener(Close);

            _hint = UiKit.Text(_panel, "Hint", "", 17);
            UiKit.Place(_hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 56f), new Vector2(460f, 26f));
            _hint.color = new Color(1f, 1f, 1f, 0.5f);
        }

        void ShowTab(int t)
        {
            _tab = (t + TabNames.Length) % TabNames.Length;
            for (int i = 0; i < _tabs.Count; i++)
                UiKit.Style(_tabs[i], i == _tab ? UiKit.ButtonKind.Primary : UiKit.ButtonKind.Secondary);

            foreach (var r in _rows) if (r.Rt != null) Destroy(r.Rt.gameObject);
            _rows.Clear();
            switch (_tab)
            {
                case 0: BuildDisplay(); break;
                case 1: BuildSound(); break;
                case 2: BuildControls(); break;
                default: BuildGame(); break;
            }
            for (int i = 0; i < _rows.Count; i++) MakeRow(_rows[i], i);
            if (_focus >= _rows.Count) _focus = _rows.Count - 1;
            Refresh();
        }

        // ───────────────────────────── 탭 내용 ─────────────────────────────

        static SettingsData D => GameSettings.Data;
        static string OnOff(bool b) => b ? "켜짐" : "꺼짐";
        static string Pct(float v) => Mathf.RoundToInt(v * 100f) + "%";
        static int Wrap(int v, int n) => ((v % n) + n) % n;

        void Add(string label, Func<string> value, Action<int> step, Action activate = null, Func<bool> enabled = null)
            => _rows.Add(new Row { Label = label, Value = value, Step = step, Activate = activate, Enabled = enabled });

        void Toggle(string label, Func<bool> get, Action<bool> set, bool display = false)
            => Add(label, () => OnOff(get()), _ => { set(!get()); GameSettings.Commit(display); });

        void Volume(string label, Func<float> get, Action<float> set)
            => Add(label, () => Pct(get()), d => { set(Mathf.Clamp01(Mathf.Round((get() + d * 0.1f) * 10f) / 10f)); GameSettings.Commit(); });

        void BuildDisplay()
        {
            string[] modes = { "전체화면", "테두리 없는 창", "창모드" };
            Add("화면 모드", () => modes[Mathf.Clamp(D.displayMode, 0, 2)],
                d => { D.displayMode = Wrap(D.displayMode + d, 3); GameSettings.Commit(true); });

            var res = GameSettings.Resolutions();
            Add("해상도", () =>
                {
                    var cur = CurrentRes(res);
                    return cur.x + " × " + cur.y;
                },
                d =>
                {
                    int i = res.IndexOf(CurrentRes(res));
                    var v = res[Wrap(i + d, res.Count)];
                    D.width = v.x; D.height = v.y;
                    GameSettings.Commit(true);
                });

            Toggle("수직 동기화", () => D.vsync, v => D.vsync = v, true);

            Add("프레임 제한", () => D.vsync ? "수직 동기화 사용 중" : D.fpsLimit <= 0 ? "제한 없음" : D.fpsLimit + " FPS",
                d =>
                {
                    int i = Array.IndexOf(GameSettings.FpsChoices, D.fpsLimit);
                    D.fpsLimit = GameSettings.FpsChoices[Wrap((i < 0 ? 0 : i) + d, GameSettings.FpsChoices.Length)];
                    GameSettings.Commit(true);
                }, null, () => !D.vsync);
        }

        static Vector2Int CurrentRes(List<Vector2Int> res)
        {
            var want = D.width > 0 ? new Vector2Int(D.width, D.height) : new Vector2Int(Screen.width, Screen.height);
            if (res.Contains(want)) return want;
            // 목록에 없으면 가장 가까운 것
            Vector2Int best = res[res.Count - 1]; int bd = int.MaxValue;
            foreach (var r in res) { int dd = Mathf.Abs(r.x - want.x) + Mathf.Abs(r.y - want.y); if (dd < bd) { bd = dd; best = r; } }
            return best;
        }

        void BuildSound()
        {
            Volume("전체 볼륨", () => D.master, v => D.master = v);
            Volume("배경음", () => D.music, v => D.music = v);
            Volume("효과음", () => D.sfx, v => D.sfx = v);
            Toggle("창이 비활성이면 음소거", () => D.muteUnfocused, v => D.muteUnfocused = v);
        }

        void BuildControls()
        {
            string[] glyphs = { "자동", "키보드", "플레이스테이션", "Xbox" };
            Add("버튼 표시", () => glyphs[Mathf.Clamp(D.glyphMode, 0, 3)],
                d => { D.glyphMode = Wrap(D.glyphMode + d, 4); GameSettings.Commit(); });
            Toggle("패드 진동", () => D.vibration, v => D.vibration = v);

            // 키 재설정은 넣지 않는다 — 스킬 커맨드(방향 + Z/X)가 키 배치에 묶여 있다
        }

        void BuildGame()
        {
            Add("화면 흔들림", () => Pct(D.shake),
                d => { D.shake = Mathf.Clamp01(Mathf.Round((D.shake + d * 0.25f) * 4f) / 4f); GameSettings.Commit(); });
            Toggle("번쩍임 · 멈춤 줄이기", () => D.reduceFlashes, v => D.reduceFlashes = v);
            Toggle("피해 숫자 표시", () => D.damageNumbers, v => D.damageNumbers = v);
            Add("UI 크기", () => Pct(D.uiScale), d =>
            {
                int i = Array.FindIndex(GameSettings.UiScales, s => Mathf.Approximately(s, D.uiScale));
                D.uiScale = GameSettings.UiScales[Mathf.Clamp((i < 0 ? 1 : i) + d, 0, GameSettings.UiScales.Length - 1)];
                GameSettings.Commit();
            });
            string[] cw = { "짧게", "보통", "넉넉하게", "아주 넉넉하게" };
            Add("커맨드 입력 여유", () =>
            {
                int i = Array.FindIndex(GameSettings.CommandWindows, s => Mathf.Approximately(s, D.commandWindow));
                return cw[i < 0 ? 1 : i];
            }, d =>
            {
                int i = Array.FindIndex(GameSettings.CommandWindows, s => Mathf.Approximately(s, D.commandWindow));
                D.commandWindow = GameSettings.CommandWindows[Mathf.Clamp((i < 0 ? 1 : i) + d, 0, GameSettings.CommandWindows.Length - 1)];
                GameSettings.Commit();
            });
            Toggle("창이 비활성이면 일시정지", () => D.autoPause, v => D.autoPause = v);
        }

        // ───────────────────────────── 줄 하나 ─────────────────────────────

        void MakeRow(Row r, int i)
        {
            var bg = UiKit.Image(_list, "Row_" + i, new Color(1f, 1f, 1f, 0f), null, true);
            r.Bg = bg;
            r.Rt = bg.rectTransform;
            r.Rt.anchorMin = r.Rt.anchorMax = new Vector2(0.5f, 1f);
            r.Rt.pivot = new Vector2(0.5f, 1f);
            r.Rt.anchoredPosition = new Vector2(0f, -i * (RowH + 3f));
            r.Rt.sizeDelta = new Vector2(RowW, RowH);

            // 마우스를 올리면 그 줄로 초점
            var trig = bg.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            var enter = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter };
            int idx = i;
            enter.callback.AddListener(_ => { if (!_rebinding) { _focus = idx; Refresh(); } });
            trig.triggers.Add(enter);

            r.LabelText = UiKit.Text(r.Rt, "Label", r.Label, 20, TextAlignmentOptions.Left);
            var lr = r.LabelText.rectTransform;
            lr.anchorMin = new Vector2(0f, 0f); lr.anchorMax = new Vector2(0.5f, 1f);
            lr.offsetMin = new Vector2(24f, 0f); lr.offsetMax = Vector2.zero;

            r.ValueText = UiKit.Text(r.Rt, "Value", "", 20);
            var vr = r.ValueText.rectTransform;
            vr.anchorMin = new Vector2(0.5f, 0f); vr.anchorMax = new Vector2(1f, 1f);
            vr.offsetMin = new Vector2(50f, 0f); vr.offsetMax = new Vector2(-50f, 0f);

            if (r.Step != null)
            {
                var arrows = UiKit.Rect(r.Rt, "Arrows");
                UiKit.Stretch(arrows);
                r.Arrows = arrows.gameObject;
                ArrowButton(arrows, "◀", new Vector2(0.5f, 0.5f), new Vector2(22f, 0f), () => DoStep(idx, -1));
                ArrowButton(arrows, "▶", new Vector2(1f, 0.5f), new Vector2(-22f, 0f), () => DoStep(idx, +1));
            }
            else
            {
                // 키 줄 — 값 자리 전체가 버튼
                var b = r.ValueText.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                r.ValueText.raycastTarget = true;
                b.onClick.AddListener(() => { _focus = idx; DoActivate(idx); });
            }
        }

        void ArrowButton(RectTransform parent, string glyph, Vector2 anchor, Vector2 pos, Action onClick)
        {
            var t = UiKit.Text(parent, glyph, glyph, 20);
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(40f, RowH);
            t.raycastTarget = true;
            t.color = new Color(1f, 0.85f, 0.5f, 1f);
            var b = t.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => onClick());
        }

        void DoStep(int i, int d)
        {
            if (i < 0 || i >= _rows.Count || _rebinding) return;
            var r = _rows[i];
            if (r.Enabled != null && !r.Enabled()) return;
            r.Step?.Invoke(d);
            Refresh();
        }

        void DoActivate(int i)
        {
            if (i < 0 || i >= _rows.Count || _rebinding) return;
            var r = _rows[i];
            if (r.Enabled != null && !r.Enabled()) return;
            if (r.Activate != null) r.Activate(); else r.Step?.Invoke(+1);
            Refresh();
        }

        void Refresh()
        {
            if (!IsOpen) return;
            for (int i = 0; i < _rows.Count; i++)
            {
                var r = _rows[i];
                bool on = r.Enabled == null || r.Enabled();
                bool focus = i == _focus;
                r.Bg.color = focus ? new Color(1f, 0.8f, 0.4f, 0.12f) : new Color(1f, 1f, 1f, i % 2 == 0 ? 0.03f : 0f);
                r.LabelText.color = !on ? new Color(1f, 1f, 1f, 0.35f) : focus ? new Color(1f, 0.85f, 0.5f) : Color.white;
                r.ValueText.text = r.Value();
                r.ValueText.color = on ? Color.white : new Color(1f, 1f, 1f, 0.35f);
                if (r.Arrows != null) r.Arrows.SetActive(on);
            }
            string nav = InputGlyphs.Of(Act.Navigate), ok = InputGlyphs.Of(Act.Interact), back = InputGlyphs.Of(Act.Cancel);
            _hint.text = _rebinding ? "바꿀 키를 누르세요  ·  Esc 취소" : $"{nav} 고르기 · 바꾸기   {ok} 결정   {back} 닫기";
        }

        // ───────────────────────────── 키 바꾸기 ─────────────────────────────

        IEnumerator Rebind(GameInput.Rebindable r)
        {
            if (_rebinding) yield break;
            _rebinding = true;
            var row = _rows[_focus];
            row.ValueText.text = "<color=#ffd45a>키를 누르세요…</color>";
            Refresh();
            row.ValueText.text = "<color=#ffd45a>키를 누르세요…</color>";

            // 방금 누른 결정키가 새 키로 잡히지 않게 손을 뗄 때까지 기다린다
            while (Keyboard.current != null && Keyboard.current.anyKey.isPressed) yield return null;

            bool wasEnabled = r.Action.enabled;
            r.Action.Disable();
            bool done = false;
            var op = r.Action.PerformInteractiveRebinding(r.Index)
                .WithControlsHavingToMatchPath("<Keyboard>")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnMatchWaitForAnother(0.05f)
                .OnComplete(o => done = true)
                .OnCancel(o => done = true)
                .Start();
            while (!done) yield return null;
            op.Dispose();
            if (wasEnabled) r.Action.Enable();

            GameSettings.StoreOverrides();
            GameSettings.Commit();
            GameInput.NotifyBindingsChanged();
            _rebinding = false;
            _rebindEndFrame = Time.frameCount;
            Refresh();
        }

        // ───────────────────────────── 입력 ─────────────────────────────

        void Update()
        {
            if (!IsOpen || _rebinding) return;
            if (Time.frameCount <= _rebindEndFrame + 1) return;   // 키 바꾸기를 끝낸 Esc · 키가 여기로 새지 않게

            if (GameInput.CancelPressed || GameInput.MenuPressed) { Close(); return; }

            var d = GameInput.Navigate();
            if (d.y != 0)
            {
                _focus = Mathf.Clamp(_focus - d.y, -1, _rows.Count - 1);
                Refresh();
            }
            else if (d.x != 0)
            {
                if (_focus < 0) ShowTab(_tab + d.x);
                else DoStep(_focus, d.x);
            }
            else if (GameInput.InteractPressed)
            {
                if (_focus < 0) ShowTab(_tab + 1);
                else DoActivate(_focus);
            }

            for (int i = 0; i < _tabs.Count; i++)
            {
                var img = _tabs[i].image;
                img.color = (_focus < 0 && i == _tab) ? new Color(1f, 1f, 1f, 1f) : new Color(1f, 1f, 1f, i == _tab ? 1f : 0.75f);
            }
        }
    }
}
