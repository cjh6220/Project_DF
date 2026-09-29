using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Proto.Core;
using Proto.Enemy;

namespace Proto.UI
{
    /// <summary>
    /// 보스 등장 연출 + 화면 위쪽 보스 체력바 (던파식).
    ///
    ///   등장  화면 가운데에 이름과 칭호가 크게 떴다가 사라진다
    ///   전투  위쪽에 긴 체력바. 빨강은 즉시, 흰 잔상은 따라 내려온다.
    ///         50% 지점에 눈금 — 저기를 넘기면 분노한다는 걸 미리 보여 준다
    ///   분노  바 테두리가 붉어진다 / 그로기 동안에는 바 옆에 표시
    /// </summary>
    public class BossHud : MonoBehaviour
    {
        [SerializeField] RunManager run;

        RectTransform _root, _bar;
        CanvasGroup _barCg, _introCg;
        TextMeshProUGUI _name, _introName, _introTitle, _state;
        Image _fill, _trail, _frame;
        BossController _boss;
        float _shown = 1f, _trailValue = 1f, _holdUntil;
        Coroutine _intro;

        const float BarWidth = 800f;   // 왼쪽 위 피로도 바와 겹치지 않게

        void Awake()
        {
            Build();
            _root.gameObject.SetActive(false);
            BossController.Spawned += OnSpawned;
        }

        void Start()
        {
            if (run != null) run.RunStarted += Hide;
        }

        void OnDestroy() => BossController.Spawned -= OnSpawned;

        void Build()
        {
            _root = UiKit.Stretch(UiKit.Rect(transform, "BossHud"));

            // 위쪽 체력바
            _bar = UiKit.Place(UiKit.Rect(_root, "Bar"), new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(BarWidth, 26f));
            _barCg = _bar.gameObject.AddComponent<CanvasGroup>();
            _frame = UiKit.Image(_bar, "Frame", new Color(0.05f, 0.04f, 0.04f, 0.9f));
            UiKit.Stretch(_frame.rectTransform);
            _frame.rectTransform.offsetMin = new Vector2(-4f, -4f);
            _frame.rectTransform.offsetMax = new Vector2(4f, 4f);
            var back = UiKit.Image(_bar, "Back", new Color(0.18f, 0.08f, 0.08f, 1f));
            UiKit.Stretch(back.rectTransform);
            _trail = UiKit.Image(_bar, "Trail", new Color(1f, 0.92f, 0.75f, 1f));
            LeftAnchored(_trail.rectTransform);
            _fill = UiKit.Image(_bar, "Fill", new Color(0.88f, 0.14f, 0.12f, 1f));
            LeftAnchored(_fill.rectTransform);

            var tick = UiKit.Image(_bar, "PhaseTick", new Color(1f, 1f, 1f, 0.8f));
            UiKit.Place(tick.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(3f, 34f));

            _name = UiKit.Text(_bar, "Name", "", 24, TextAlignmentOptions.BottomLeft);
            _name.rectTransform.anchorMin = _name.rectTransform.anchorMax = new Vector2(0f, 1f);
            _name.rectTransform.pivot = new Vector2(0f, 0f);
            _name.rectTransform.anchoredPosition = new Vector2(0f, 8f);
            _name.rectTransform.sizeDelta = new Vector2(600f, 32f);
            _name.fontStyle = FontStyles.Bold;

            _state = UiKit.Text(_bar, "State", "", 22, TextAlignmentOptions.BottomRight);
            _state.rectTransform.anchorMin = _state.rectTransform.anchorMax = new Vector2(1f, 1f);
            _state.rectTransform.pivot = new Vector2(1f, 0f);
            _state.rectTransform.anchoredPosition = new Vector2(0f, 8f);
            _state.rectTransform.sizeDelta = new Vector2(300f, 30f);

            // 등장 연출
            var intro = UiKit.Place(UiKit.Rect(_root, "Intro"), new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(900f, 200f));
            _introCg = intro.gameObject.AddComponent<CanvasGroup>();
            var band = UiKit.Image(intro, "Band", new Color(0f, 0f, 0f, 0.5f));
            UiKit.Place(band.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2400f, 150f));
            _introTitle = UiKit.Text(intro, "Title", "", 26);
            UiKit.Place(_introTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(900f, 36f));
            _introTitle.color = new Color(1f, 0.75f, 0.55f);
            _introName = UiKit.Text(intro, "Name", "", 72);
            UiKit.Place(_introName.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -18f), new Vector2(900f, 90f));
            _introName.fontStyle = FontStyles.Bold;
        }

        static void LeftAnchored(RectTransform rt)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(BarWidth, 0f);
        }

        void OnSpawned(BossController b)
        {
            _boss = b;
            _shown = _trailValue = 1f;
            _name.text = b.DisplayName;
            _state.text = "";
            _frame.color = new Color(0.05f, 0.04f, 0.04f, 0.9f);
            SetWidth(_fill, 1f);
            SetWidth(_trail, 1f);

            b.Health.Changed += OnHealth;
            b.Health.Died += OnDied;
            b.Enraged += () =>
            {
                _frame.color = new Color(0.75f, 0.1f, 0.08f, 1f);
                _state.text = "<color=#ff6a5a>분노</color>";
            };

            _root.gameObject.SetActive(true);
            transform.SetAsLastSibling();
            if (_intro != null) StopCoroutine(_intro);
            _intro = StartCoroutine(Intro(b));
        }

        IEnumerator Intro(BossController b)
        {
            _introName.text = b.DisplayName;
            _introTitle.text = b.Title;
            _barCg.alpha = 0f;
            _introCg.alpha = 0f;

            yield return Fade(_introCg, 0f, 1f, 0.35f);
            yield return new WaitForSecondsRealtime(1.1f);
            yield return Fade(_introCg, 1f, 0f, 0.35f);
            yield return Fade(_barCg, 0f, 1f, 0.3f);
            _intro = null;
        }

        IEnumerator Fade(CanvasGroup cg, float a, float b, float dur)
        {
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                cg.alpha = Mathf.Lerp(a, b, t / dur);
                yield return null;
            }
            cg.alpha = b;
        }

        void OnHealth(float cur, float max)
        {
            float r = max <= 0f ? 0f : cur / max;
            if (r < _shown) _holdUntil = Time.time + 0.4f;
            _shown = r;
            SetWidth(_fill, r);
        }

        void OnDied()
        {
            _state.text = "";
            StartCoroutine(HideAfter(1.5f));
        }

        IEnumerator HideAfter(float s)
        {
            yield return new WaitForSecondsRealtime(s);
            yield return Fade(_barCg, 1f, 0f, 0.4f);
            Hide();
        }

        void Hide()
        {
            if (_boss != null)
            {
                _boss.Health.Changed -= OnHealth;
                _boss.Health.Died -= OnDied;
            }
            _boss = null;
            _root.gameObject.SetActive(false);
        }

        void Update()
        {
            if (_boss == null) return;

            if (_trailValue > _shown && Time.time >= _holdUntil)
            {
                _trailValue = Mathf.MoveTowards(_trailValue, _shown, 0.8f * Time.deltaTime);
                SetWidth(_trail, _trailValue);
            }

            if (!_boss.Health.IsDead && !_boss.Phase2)
                _state.text = _boss.IsGroggy ? "<color=#ffd45a>그로기</color>" : "";
            else if (!_boss.Health.IsDead && _boss.Phase2)
                _state.text = _boss.IsGroggy ? "<color=#ffd45a>그로기</color>  <color=#ff6a5a>분노</color>" : "<color=#ff6a5a>분노</color>";
        }

        static void SetWidth(Image img, float r)
        {
            var rt = img.rectTransform;
            rt.sizeDelta = new Vector2(BarWidth * Mathf.Clamp01(r), 0f);
        }
    }
}
