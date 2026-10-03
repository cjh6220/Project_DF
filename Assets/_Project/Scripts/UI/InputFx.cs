using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Proto.Core;

namespace Proto.UI
{
    /// <summary>
    /// 입력 장치가 바뀌는 순간의 연출.
    ///
    ///   1) 버튼 표기 글자가 카드 뒤집히듯 한 번 접혔다 펴지며 새 표기로 바뀐다 (왼쪽부터 물결처럼)
    ///   2) 오른쪽 아래에 "듀얼센스" 알림이 미끄러져 들어오고, 빛줄기가 한 번 훑고 지나간다
    ///
    /// 씬에 따로 놓지 않는다. 게임이 시작되면 스스로 생긴다.
    /// 버튼 표기를 바꾸는 쪽은 text = ... 대신 InputFx.Set(text, ...)을 부르면 된다.
    /// </summary>
    public class InputFx : MonoBehaviour
    {
        static InputFx _i;

        RectTransform _toast, _shine;
        CanvasGroup _toastCg;
        TextMeshProUGUI _toastGlyph, _toastName, _toastSub;
        Image _toastDevice;
        Coroutine _toastCo;

        readonly Dictionary<TMP_Text, Coroutine> _running = new Dictionary<TMP_Text, Coroutine>();
        readonly Dictionary<TMP_Text, Color> _baseColor = new Dictionary<TMP_Text, Color>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (_i != null) return;
            var go = new GameObject("[InputFx]");
            DontDestroyOnLoad(go);
            _i = go.AddComponent<InputFx>();
        }

        void Awake()
        {
            GameInput.Ensure();
            // 알림 글자를 미리 글꼴 아틀라스에 올려 둔다. 안 하면 처음 뜰 때 한 프레임 네모로 깨진다.
            var font = TMP_Settings.defaultFontAsset;
            if (font != null) font.TryAddCharacters("듀얼센스쇼크4Xbox패드키보드버튼표시를바꿨습니다□△○×ABXYZC RBLT/OPTIONSMenuEsc대화닫기마을로포기구매선택이동확대축소");
            BuildToast();
            GameInput.SchemeChanged += OnSchemeChanged;
            _lastScheme = GameInput.Scheme;
        }

        void OnDestroy() => GameInput.SchemeChanged -= OnSchemeChanged;

        // ───────────────────────────── 글자 뒤집기 ─────────────────────────────

        /// <summary>
        /// 버튼 표기를 바꾼다. 글자가 달라졌고 화면에 보이는 중이면 뒤집기 연출을 한다.
        /// delay로 여러 글자를 차례로 뒤집으면 물결처럼 훑고 지나간다.
        /// </summary>
        public static void Set(TMP_Text t, string text, float delay = 0f)
        {
            if (t == null || t.text == text) return;
            if (_i == null || !t.isActiveAndEnabled) { t.text = text; return; }
            _i.Begin(t, text, delay);
        }

        void Begin(TMP_Text t, string text, float delay)
        {
            if (_running.TryGetValue(t, out var co) && co != null) StopCoroutine(co);
            if (!_baseColor.ContainsKey(t)) _baseColor[t] = t.color;
            _running[t] = StartCoroutine(Flip(t, text, delay));
        }

        IEnumerator Flip(TMP_Text t, string text, float delay)
        {
            var tr = t.transform;
            var baseColor = _baseColor[t];
            var flash = new Color(1f, 0.93f, 0.62f, baseColor.a);

            float w = 0f;
            while (w < delay) { w += Time.unscaledDeltaTime; yield return null; }

            // 접기 — 가로로 납작해진다
            const float fold = 0.07f, open = 0.2f;
            float e = 0f;
            while (e < fold && t != null)
            {
                e += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(e / fold);
                tr.localScale = new Vector3(1f - k * k, 1f + 0.1f * k, 1f);
                yield return null;
            }
            if (t == null) yield break;

            t.text = text;

            // 펴기 — 살짝 넘치게 펴지며 금빛이 번졌다 빠진다
            e = 0f;
            while (e < open && t != null)
            {
                e += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(e / open);
                float sx = BackOut(k);
                tr.localScale = new Vector3(sx, 1f + 0.1f * (1f - k), 1f);
                t.color = Color.Lerp(flash, baseColor, k * k);
                yield return null;
            }
            if (t == null) yield break;
            tr.localScale = Vector3.one;
            t.color = baseColor;
            _running.Remove(t);
        }

        static float BackOut(float k)
        {
            const float c = 2.2f;
            float x = k - 1f;
            return 1f + (c + 1f) * x * x * x + c * x * x;
        }

        // ───────────────────────────── 장치 알림 ─────────────────────────────

        void BuildToast()
        {
            var canvasGo = new GameObject("InputFxCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;   // 전환막보다도 위
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            // 알림 띠 — 어두운 유리 + 왼쪽 금색 막대 (Resources/UI/Toast)
            var toastSpr = UiKit.Ui("Toast");
            var border = UiKit.Image(canvasGo.transform, "Toast", Color.white, toastSpr != null ? toastSpr : UiArt.RoundRectFill);
            border.type = Image.Type.Sliced;
            _toast = border.rectTransform;
            _toast.anchorMin = _toast.anchorMax = new Vector2(1f, 0f);
            _toast.pivot = new Vector2(1f, 0f);
            _toast.sizeDelta = new Vector2(420f, 92f);
            _toastCg = border.gameObject.AddComponent<CanvasGroup>();
            _toastCg.blocksRaycasts = false;

            var panel = UiKit.Image(_toast, "Panel", toastSpr != null ? Color.clear : new Color(0.09f, 0.075f, 0.12f, 0.97f), UiArt.RoundRectFill);
            panel.type = Image.Type.Sliced;
            UiKit.Stretch(panel.rectTransform);
            panel.rectTransform.offsetMin = new Vector2(2f, 2f);
            panel.rectTransform.offsetMax = new Vector2(-2f, -2f);
            panel.gameObject.AddComponent<RectMask2D>();

            // 훑고 지나가는 빛줄기
            var shine = UiKit.Image(panel.transform, "Shine", new Color(1f, 0.92f, 0.7f, 0.18f), UiKit.Soft);
            _shine = shine.rectTransform;
            _shine.anchorMin = _shine.anchorMax = new Vector2(0f, 0.5f);
            _shine.sizeDelta = new Vector2(90f, 220f);
            _shine.localRotation = Quaternion.Euler(0f, 0f, -20f);

            // 왼쪽: 장치 그림 (패드 · 키보드 · 휴대용)
            _toastDevice = UiKit.Image(panel.transform, "Device", Color.white, UiKit.Ui("Device_Keyboard"));
            _toastDevice.preserveAspect = true;
            var dv = _toastDevice.rectTransform;
            dv.anchorMin = dv.anchorMax = new Vector2(0f, 0.5f);
            dv.pivot = new Vector2(0f, 0.5f);
            dv.anchoredPosition = new Vector2(26f, 0f);
            dv.sizeDelta = new Vector2(96f, 66f);

            // 아래 줄: 얼굴 버튼 아이콘 + 안내
            _toastGlyph = UiKit.Text(panel.transform, "Glyph", "", 22, TextAlignmentOptions.Left);
            var g = _toastGlyph.rectTransform;
            g.anchorMin = g.anchorMax = new Vector2(0f, 0.5f);
            g.pivot = new Vector2(0f, 0.5f);
            g.anchoredPosition = new Vector2(140f, -17f);
            g.sizeDelta = new Vector2(260f, 30f);

            _toastName = UiKit.Text(panel.transform, "Name", "", 26, TextAlignmentOptions.Left);
            var n = _toastName.rectTransform;
            n.anchorMin = n.anchorMax = new Vector2(0f, 0.5f);
            n.pivot = new Vector2(0f, 0.5f);
            n.anchoredPosition = new Vector2(140f, 15f);
            n.sizeDelta = new Vector2(260f, 34f);
            _toastName.fontStyle = FontStyles.Bold;

            _toastSub = UiKit.Text(panel.transform, "Sub", "", 17, TextAlignmentOptions.Right);
            var s = _toastSub.rectTransform;
            s.anchorMin = s.anchorMax = new Vector2(0f, 0.5f);
            s.pivot = new Vector2(0f, 0.5f);
            s.anchoredPosition = new Vector2(140f, -17f);
            s.sizeDelta = new Vector2(256f, 24f);
            _toastSub.color = new Color(0.66f, 0.63f, 0.74f, 1f);

            border.gameObject.SetActive(false);
        }

        InputScheme _lastScheme;

        void OnSchemeChanged()
        {
            // 키 재설정 · 설정 변경으로도 신호가 오지만, 알림은 장치가 실제로 바뀌었을 때만 띄운다
            if (GameInput.Scheme == _lastScheme) return;
            _lastScheme = GameInput.Scheme;
            // 장치 그림 — 휴대용(리눅스에서 패드 = 스팀덱으로 본다)
            string dev = GameInput.Scheme == InputScheme.PlayStation ? "Device_PS"
                       : GameInput.Scheme == InputScheme.Xbox ? (Application.platform == RuntimePlatform.LinuxPlayer ? "Device_Handheld" : "Device_Xbox")
                       : "Device_Keyboard";
            var spr = UiKit.Ui(dev);
            if (spr != null) _toastDevice.sprite = spr;
            _toastGlyph.text = InputGlyphs.FaceButtons();
            _toastName.text = GameInput.DeviceName;
            _toastSub.text = "버튼 표시 바뀜";
            if (_toastCo != null) StopCoroutine(_toastCo);
            _toastCo = StartCoroutine(ShowToast());
        }

        IEnumerator ShowToast()
        {
            _toast.gameObject.SetActive(true);
            Vector2 shown = new Vector2(-36f, 36f), hidden = new Vector2(420f, 36f);

            // 들어오기 — 빠르게 미끄러져 들어와 살짝 넘쳤다 선다
            float e = 0f;
            while (e < 0.28f)
            {
                e += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(e / 0.28f);
                _toast.anchoredPosition = Vector2.LerpUnclamped(hidden, shown, BackOut(k));
                _toastCg.alpha = Mathf.Clamp01(k * 2f);
                _shine.anchoredPosition = new Vector2(Mathf.Lerp(-80f, 440f, k), 0f);
                yield return null;
            }
            _toast.anchoredPosition = shown;

            // 빛줄기 한 번 더
            e = 0f;
            while (e < 0.35f)
            {
                e += Time.unscaledDeltaTime;
                _shine.anchoredPosition = new Vector2(Mathf.Lerp(-80f, 440f, e / 0.35f), 0f);
                yield return null;
            }

            float hold = 0f;
            while (hold < 1.2f) { hold += Time.unscaledDeltaTime; yield return null; }

            // 나가기
            e = 0f;
            while (e < 0.22f)
            {
                e += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(e / 0.22f);
                _toast.anchoredPosition = Vector2.Lerp(shown, hidden, k * k);
                _toastCg.alpha = 1f - k;
                yield return null;
            }
            _toast.gameObject.SetActive(false);
            _toastCo = null;
        }
    }
}
