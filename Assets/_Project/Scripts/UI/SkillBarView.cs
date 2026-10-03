using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Proto.Core;

namespace Proto.UI
{
    /// <summary>
    /// 던전 화면 아래 가운데 스킬창 — 퀵슬롯 4칸 + 커맨드 입력창.
    ///
    ///   칸       SkillSlot (빈 칸은 SkillSlotEmpty). 왼쪽 위에 칸 키 (A S D F / 패드 버튼)
    ///   재사용   SkillSlotCooldown이 시계 방향으로 걷히고 가운데에 남은 초
    ///   준비 끝  재사용이 끝나는 순간 SkillSlotReady로 잠깐 번쩍 (살짝 커졌다 돌아온다)
    ///   누름     SkillSlotFlash 빛 테가 퍼졌다 사라진다
    ///   입력창   방향 · 공격 · 스킬 입력이 차례로 쌓인다. 커맨드 여유 시간(설정)이 지나면 새로 시작, 가만히 있으면 사라진다
    ///
    /// 스킬 발동은 아직 없다 — 발동 코드가 생기면 SkillBarView.Instance.Use(칸, 재사용 초)만 부르면 된다.
    /// 씬에 따로 둘 필요 없다. 게임이 시작되면 DungeonHud 아래에 스스로 붙는다.
    /// </summary>
    public class SkillBarView : MonoBehaviour
    {
        public static SkillBarView Instance { get; private set; }

        const float SlotSize = 100f, SlotGap = 112f, IconSize = 62f;
        static readonly Vector2 BarSize = new Vector2(660f, 104f);
        static readonly Vector2 StripSize = new Vector2(380f, 72f);

        class Slot
        {
            public RectTransform Root;
            public Image Base, Icon, Cool, Ready, Flash;
            public TextMeshProUGUI Key, Secs;
            public int Skill = -2;
            public float CoolLeft, CoolTotal, ReadyT, FlashT;
        }

        RunManager _run;
        readonly List<Slot> _slots = new();
        Sprite _sprSlot, _sprEmpty;
        RectTransform _strip;
        CanvasGroup _stripGroup;
        TextMeshProUGUI _stripText;
        readonly List<string> _tokens = new();
        float _lastInput = -99f;
        Vector2Int _lastDir;
        InputScheme _scheme;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Instance != null) return;
            var hud = FindFirstObjectByType<HudView>(FindObjectsInactive.Include);
            if (hud == null) return;
            var root = hud.transform.Find("DungeonHud");
            if (root == null) return;
            root.gameObject.AddComponent<SkillBarView>();
        }

        void Awake()
        {
            Instance = this;
            _run = FindFirstObjectByType<RunManager>();
            Build();
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void OnEnable()
        {
            if (_run != null) _run.Skills.Changed += Refresh;
            GameInput.SchemeChanged += RefreshKeys;
            Refresh();
            RefreshKeys();
        }

        void OnDisable()
        {
            if (_run != null) _run.Skills.Changed -= Refresh;
            GameInput.SchemeChanged -= RefreshKeys;
            _tokens.Clear();
        }

        // ───────────────────────────── 조립 ─────────────────────────────

        void Build()
        {
            _sprSlot = UiKit.Ui("SkillSlot");
            _sprEmpty = UiKit.Ui("SkillSlotEmpty");

            var bar = UiKit.Rect(transform, "SkillBar");
            UiKit.Place(bar, new Vector2(0.5f, 0f), new Vector2(0f, 74f), BarSize);
            var bg = UiKit.Image(bar, "Back", Color.white, UiKit.Ui("SkillBar"));
            UiKit.Stretch(bg.rectTransform);
            bg.preserveAspect = true;

            for (int i = 0; i < SkillBook.SlotCount; i++)
            {
                var s = new Slot();
                s.Root = UiKit.Rect(bar, "Slot" + i);
                UiKit.Place(s.Root, new Vector2(0.5f, 0.5f), new Vector2((i - 1.5f) * SlotGap, 4f), new Vector2(SlotSize, SlotSize));

                s.Base = Fill(s.Root, "Base", _sprSlot, 1f);
                s.Ready = Fill(s.Root, "Ready", UiKit.Ui("SkillSlotReady"), 1f);   // 아이콘 아래 — 금빛 칸 위에 아이콘이 보이게
                s.Ready.enabled = false;
                s.Icon = UiKit.Image(s.Root, "Icon", Color.white, null);
                UiKit.Place(s.Icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), new Vector2(IconSize, IconSize));
                s.Icon.preserveAspect = true;

                s.Cool = Fill(s.Root, "Cooldown", UiKit.Ui("SkillSlotCooldown"), 1f);
                s.Cool.color = new Color(0f, 0f, 0f, 1f);   // 그림 자체가 반투명 회색이라 검게 곱하면 알맞게 어두워진다
                s.Cool.type = Image.Type.Filled;
                s.Cool.fillMethod = Image.FillMethod.Radial360;
                s.Cool.fillOrigin = (int)Image.Origin360.Top;
                s.Cool.fillClockwise = false;   // 남은 부분이 줄어들며 시계 방향으로 걷힌다
                s.Cool.enabled = false;

                s.Flash = Fill(s.Root, "Flash", UiKit.Ui("SkillSlotFlash"), 1.06f);
                s.Flash.enabled = false;

                s.Secs = UiKit.Text(s.Root, "Secs", "", 30);
                UiKit.Stretch(s.Secs.rectTransform);
                s.Secs.fontStyle = FontStyles.Bold;
                s.Secs.color = new Color(1f, 0.93f, 0.75f);
                s.Secs.outlineWidth = 0.2f; s.Secs.outlineColor = new Color32(0, 0, 0, 220);

                s.Key = UiKit.Text(s.Root, "Key", "", 20, TextAlignmentOptions.TopLeft);
                UiKit.Place(s.Key.rectTransform, new Vector2(0f, 1f), new Vector2(26f, -22f), new Vector2(44f, 30f));
                s.Key.outlineWidth = 0.25f; s.Key.outlineColor = new Color32(0, 0, 0, 230);

                _slots.Add(s);
            }

            // 커맨드 입력창 — 스킬창 위
            _strip = UiKit.Rect(transform, "CommandStrip");
            UiKit.Place(_strip, new Vector2(0.5f, 0f), new Vector2(0f, 74f + BarSize.y * 0.5f + StripSize.y * 0.5f + 4f), StripSize);
            _stripGroup = _strip.gameObject.AddComponent<CanvasGroup>();
            _stripGroup.alpha = 0f;
            var sbg = UiKit.Image(_strip, "Back", Color.white, UiKit.Ui("CommandStrip"));
            UiKit.Stretch(sbg.rectTransform);
            sbg.preserveAspect = true;
            _stripText = UiKit.Text(_strip, "Text", "", 28);
            UiKit.Stretch(_stripText.rectTransform);
            _stripText.characterSpacing = 6f;
            _stripText.color = new Color(1f, 0.9f, 0.68f);
        }

        /// <summary>칸 전체를 덮는 그림. 슬롯 그림들은 같은 틀로 잘라 두어서 겹치면 테두리가 맞는다.</summary>
        static Image Fill(RectTransform parent, string name, Sprite spr, float scale)
        {
            var img = UiKit.Image(parent, name, Color.white, spr);
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(SlotSize, SlotSize) * scale;
            return img;
        }

        // ───────────────────────────── 갱신 ─────────────────────────────

        void Refresh()
        {
            if (_run == null) return;
            for (int i = 0; i < _slots.Count; i++)
            {
                var s = _slots[i];
                int k = _run.Skills.Slot(i);
                if (k == s.Skill) continue;
                s.Skill = k;
                bool has = k >= 0;
                s.Base.sprite = has || _sprEmpty == null ? _sprSlot : _sprEmpty;
                s.Icon.enabled = has;
                if (has) s.Icon.sprite = Resources.Load<Sprite>("Icons/" + SkillBook.All[k].Icon);
                if (!has) { s.CoolLeft = 0f; s.Cool.enabled = false; s.Secs.text = ""; }
            }
        }

        void RefreshKeys()
        {
            _scheme = GameInput.Scheme;
            for (int i = 0; i < _slots.Count; i++) _slots[i].Key.text = InputGlyphs.Slot(i);
            _tokens.Clear();   // 장치가 바뀌면 이미 찍힌 버튼 그림이 안 맞는다
            _stripText.text = "";
        }

        /// <summary>칸을 썼다 — 빛 테를 번쩍이고, 재사용 시간이 있으면 돌린다.</summary>
        public void Use(int slot, float cooldown = 0f)
        {
            if (slot < 0 || slot >= _slots.Count) return;
            var s = _slots[slot];
            s.FlashT = 1f;
            if (cooldown > 0f) { s.CoolTotal = s.CoolLeft = cooldown; }
        }

        /// <summary>재사용 중인가 (발동 코드가 물어본다).</summary>
        public bool CoolingDown(int slot) => slot >= 0 && slot < _slots.Count && _slots[slot].CoolLeft > 0f;

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (GameInput.Scheme != _scheme) RefreshKeys();

            bool live = _run == null || _run.Phase == RunPhase.InDungeon;
            if (live && Time.timeScale > 0f)
            {
                int pressed = GameInput.SlotPressed();
                if (pressed >= 0 && _slots[pressed].Skill >= 0) Use(pressed);
                TrackCommand();
            }

            foreach (var s in _slots) Animate(s, dt);
            AnimateStrip();
        }

        void Animate(Slot s, float dt)
        {
            // 재사용 — 게임 시간으로 흐른다 (일시정지 중엔 멈춤)
            if (s.CoolLeft > 0f)
            {
                s.CoolLeft = Mathf.Max(0f, s.CoolLeft - Time.deltaTime);
                s.Cool.enabled = true;
                s.Cool.fillAmount = s.CoolTotal > 0f ? s.CoolLeft / s.CoolTotal : 0f;
                s.Secs.text = s.CoolLeft >= 1f ? Mathf.CeilToInt(s.CoolLeft).ToString() : s.CoolLeft > 0f ? s.CoolLeft.ToString("0.0") : "";
                s.Icon.color = new Color(0.8f, 0.8f, 0.85f, 1f);
                if (s.CoolLeft <= 0f) { s.Cool.enabled = false; s.Secs.text = ""; s.Icon.color = Color.white; s.ReadyT = 1f; }
            }

            // 준비 끝 — 금빛 칸으로 잠깐 바뀌며 살짝 커졌다 돌아온다
            if (s.ReadyT > 0f)
            {
                s.ReadyT = Mathf.Max(0f, s.ReadyT - dt / 0.45f);
                s.Ready.enabled = s.ReadyT > 0f;
                s.Ready.color = new Color(1f, 1f, 1f, Mathf.Clamp01(s.ReadyT * 1.6f));
                float pop = 1f + 0.12f * Mathf.Sin(s.ReadyT * Mathf.PI);
                s.Root.localScale = Vector3.one * pop;
            }
            else if (s.FlashT <= 0f) s.Root.localScale = Vector3.one;

            // 누름 — 빛 테가 퍼지며 사라진다, 칸은 살짝 눌렸다 돌아온다
            if (s.FlashT > 0f)
            {
                s.FlashT = Mathf.Max(0f, s.FlashT - dt / 0.3f);
                s.Flash.enabled = s.FlashT > 0f;
                s.Flash.color = new Color(1f, 1f, 1f, s.FlashT);
                s.Flash.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.18f, 0.96f, s.FlashT);
                if (s.ReadyT <= 0f) s.Root.localScale = Vector3.one * (1f - 0.08f * s.FlashT);
            }
        }

        // ───────────────────────────── 커맨드 입력창 ─────────────────────────────

        void TrackCommand()
        {
            var v = GameInput.MoveValue;
            Vector2Int d = Vector2Int.zero;
            if (v.sqrMagnitude > 0.35f)
                d = Mathf.Abs(v.x) >= Mathf.Abs(v.y) ? new Vector2Int(v.x > 0 ? 1 : -1, 0) : new Vector2Int(0, v.y > 0 ? 1 : -1);
            if (d != _lastDir && d != Vector2Int.zero)
                Push(d.x > 0 ? "→" : d.x < 0 ? "←" : d.y > 0 ? "↑" : "↓");
            _lastDir = d;

            if (GameInput.AttackPressed) Push(InputGlyphs.Of(Act.Attack));
            if (GameInput.SkillPressed) Push(InputGlyphs.Of(Act.Skill));
        }

        void Push(string token)
        {
            float window = Mathf.Max(0.25f, GameSettings.Data.commandWindow);
            if (Time.unscaledTime - _lastInput > window * 2f) _tokens.Clear();   // 여유 시간이 지나면 새 커맨드
            _lastInput = Time.unscaledTime;
            _tokens.Add(token);
            while (_tokens.Count > 7) _tokens.RemoveAt(0);
            _stripText.text = string.Join(" ", _tokens);
        }

        void AnimateStrip()
        {
            float idle = Time.unscaledTime - _lastInput;
            float target = _tokens.Count > 0 && idle < 1.1f ? 1f : 0f;
            _stripGroup.alpha = Mathf.MoveTowards(_stripGroup.alpha, target, Time.unscaledDeltaTime * (target > 0f ? 10f : 3f));
            if (_stripGroup.alpha <= 0f && _tokens.Count > 0 && idle >= 1.1f) { _tokens.Clear(); _stripText.text = ""; }
        }
    }
}
