using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Proto.Core;

namespace Proto.UI
{
    /// <summary>
    /// 왼쪽 위 체력 · 피로도 — 로우폴리 카타나 두 자루 (체력 위, 피로도 아래). 칼날 홈에 채움이 찬다.
    ///
    ///   지연 막대   깎인 만큼 밝게 남았다가 늦게 줄어든다 — 얼마나 맞았는지 한눈에
    ///   맞을 때     칼이 짧게 흔들리고 채움이 하얗게 번쩍인다
    ///   채움 끝     뾰족한 화살촉이 홈 곡선을 따라 움직인다
    ///   경고        체력 30% · 피로도 20% 아래면 칼날 둘레가 붉게 / 노랗게 맥박친다
    ///   반짝임      회복하거나 가득 찬 채로 있으면 칼날 위로 빛이 한 번 스친다
    ///
    /// 그림: Resources/UI/KatanaBlade · KatanaGroove · KatanaHalo · KatanaFillTip · KatanaGleam + KatanaGroove.txt
    /// (Proto ▸ Build Katana HUD 로 만든다). 없으면 아무것도 하지 않고 예전 막대를 그대로 둔다.
    /// </summary>
    public class VitalsHud : MonoBehaviour
    {
        const float SwordW = 600f;                                  // 화면에서 칼 한 자루 폭
        static readonly Vector2 Origin = new Vector2(22f, -14f);    // 위 칼 왼쪽 위
        const float Spacing = 0.80f;                                // 두 칼 사이 (칼 높이 배수)

        static readonly Color ColHp = new Color(0.90f, 0.12f, 0.12f);
        static readonly Color ColHpLag = new Color(1f, 0.90f, 0.62f, 1f);
        static readonly Color ColSt = new Color(1.00f, 0.78f, 0.12f);
        static readonly Color ColStLag = new Color(1f, 1f, 1f, 0.6f);

        RunManager _run;
        Health _hp;
        Sword _hpSword, _stSword;

        // ── 홈 정보 (KatanaGroove.txt) ──
        internal static float W, H, G0, G1, Pad;
        internal static readonly List<Vector4> Samples = new();    // x · 중심 y · 반높이 · 칼날 윗변 y (원본 픽셀, 아래가 0)

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (UiKit.Ui("KatanaBlade") == null || UiKit.Ui("KatanaGroove") == null || !LoadGroove()) return;
            foreach (var t in Resources.FindObjectsOfTypeAll<RectTransform>())
            {
                if (t.name != "DungeonHud" || !t.gameObject.scene.IsValid()) continue;
                if (t.GetComponent<VitalsHud>() == null) t.gameObject.AddComponent<VitalsHud>();
                return;
            }
        }

        static bool LoadGroove()
        {
            var ta = Resources.Load<TextAsset>("UI/KatanaGroove");
            if (ta == null) return false;
            Samples.Clear();
            var lines = ta.text.Split('\n');
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            var head = lines[0].Trim().Split(' ');
            W = float.Parse(head[0], inv); H = float.Parse(head[1], inv);
            G0 = float.Parse(head[2], inv); G1 = float.Parse(head[3], inv); Pad = float.Parse(head[4], inv);
            for (int i = 1; i < lines.Length; i++)
            {
                var p = lines[i].Trim().Split(' ');
                if (p.Length < 4) continue;
                Samples.Add(new Vector4(float.Parse(p[0], inv), float.Parse(p[1], inv), float.Parse(p[2], inv), float.Parse(p[3], inv)));
            }
            return Samples.Count > 1;
        }

        /// <summary>홈 안 x(원본 픽셀)에서의 중심 y · 반높이 · 칼날 윗변 y.</summary>
        internal static Vector3 At(float x)
        {
            if (x <= Samples[0].x) return new Vector3(Samples[0].y, Samples[0].z, Samples[0].w);
            for (int i = 1; i < Samples.Count; i++)
            {
                if (x > Samples[i].x) continue;
                var a = Samples[i - 1]; var b = Samples[i];
                float t = Mathf.InverseLerp(a.x, b.x, x);
                return new Vector3(Mathf.Lerp(a.y, b.y, t), Mathf.Lerp(a.z, b.z, t), Mathf.Lerp(a.w, b.w, t));
            }
            var l = Samples[Samples.Count - 1];
            return new Vector3(l.y, l.z, l.w);
        }

        void Awake()
        {
            _run = FindFirstObjectByType<RunManager>();
            var player = FindFirstObjectByType<Proto.Player.PlayerController>();
            if (player != null) _hp = player.GetComponent<Health>();
            Build();
        }

        void Build()
        {
            foreach (var n in new[] { "StaminaBar", "HealthBar", "StaminaText", "Vitals" })
            {
                var old = transform.Find(n);
                if (old != null) old.gameObject.SetActive(false);
            }

            var root = UiKit.Rect(transform, "Katanas");
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0f, 1f);
            root.anchoredPosition = Vector2.zero;
            root.sizeDelta = Vector2.zero;
            root.SetAsFirstSibling();

            float s = SwordW / W, sh = H * s;
            _hpSword = new Sword(root, "Health", Origin, s, ColHp, ColHpLag, new Color(1f, 0.22f, 0.18f), 0.30f);
            _stSword = new Sword(root, "Stamina", Origin + new Vector2(0f, -sh * Spacing), s, ColSt, ColStLag, new Color(1f, 0.72f, 0.15f), 0.20f);
            _hpSword.Root.SetAsLastSibling();   // 위 칼의 날밑이 아래 칼 위로

            // 획득물 줄은 칼 아래로 내린다
            float bottom = Origin.y - sh * Spacing - sh - 6f;
            foreach (var n in new[] { "LootText", "LootStrip" })
            {
                var t = transform.Find(n) as RectTransform;
                if (t == null) continue;
                float top = t.anchoredPosition.y + t.sizeDelta.y * (1f - t.pivot.y);
                if (top > bottom) t.anchoredPosition += new Vector2(0f, bottom - top + (n == "LootStrip" ? 6f : 0f));
            }
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (_hp != null) _hpSword.Tick(_hp.Current, _hp.Max, dt);
            if (_run != null && _run.Stamina != null) _stSword.Tick(_run.Stamina.Current, _run.Stamina.Max, dt);
        }

        // ───────────────────────────── 칼 한 자루 ─────────────────────────────

        class Sword
        {
            public readonly RectTransform Root;
            readonly RectTransform _body;
            readonly Image _fill, _lag, _halo, _tip, _gleam;
            readonly TextMeshProUGUI _num;
            readonly Color _col, _warnCol;
            readonly float _warnAt, _s;
            readonly Vector2 _home;
            float _shown = -1f, _lagV, _hold, _last = -1f, _shake, _flash, _gleamT = 1f, _idle;
            int _lastCur = int.MinValue, _lastMax = int.MinValue;

            public Sword(RectTransform parent, string name, Vector2 topLeft, float s, Color col, Color lagCol, Color warnCol, float warnAt)
            {
                _s = s; _col = col; _warnCol = warnCol; _warnAt = warnAt;
                float sw = W * s, sh = H * s;

                Root = UiKit.Rect(parent, name);
                Root.anchorMin = Root.anchorMax = new Vector2(0f, 1f);
                Root.pivot = new Vector2(0f, 1f);
                Root.anchoredPosition = topLeft;
                Root.sizeDelta = new Vector2(sw, sh);

                // 흔들림은 몸통만 (자리 기준은 Root)
                _body = UiKit.Rect(Root, "Body");
                UiKit.Stretch(_body);
                _home = Vector2.zero;

                var haloSpr = UiKit.Ui("KatanaHalo");
                if (haloSpr != null)
                {
                    _halo = UiKit.Image(_body, "Halo", Color.clear, haloSpr);
                    UiKit.Stretch(_halo.rectTransform);
                    float p = Pad * s;
                    _halo.rectTransform.offsetMin = new Vector2(-p, -p);
                    _halo.rectTransform.offsetMax = new Vector2(p, p);
                }

                var blade = UiKit.Image(_body, "Blade", Color.white, UiKit.Ui("KatanaBlade"));
                UiKit.Stretch(blade.rectTransform);

                var groove = UiKit.Ui("KatanaGroove");
                _lag = Filled(_body, "Lag", lagCol, groove);
                _fill = Filled(_body, "Fill", col, groove);

                var tipSpr = UiKit.Ui("KatanaFillTip");
                if (tipSpr != null)
                {
                    _tip = UiKit.Image(_body, "Tip", Color.Lerp(col, Color.white, 0.25f), tipSpr);
                    var t = _tip.rectTransform;
                    t.anchorMin = t.anchorMax = Vector2.zero;
                    t.pivot = new Vector2(0.45f, 0.5f);
                }

                var gleamSpr = UiKit.Ui("KatanaGleam");
                if (gleamSpr != null)
                {
                    _gleam = UiKit.Image(_body, "Gleam", Color.clear, gleamSpr);
                    var g = _gleam.rectTransform;
                    g.anchorMin = g.anchorMax = Vector2.zero;
                    g.pivot = new Vector2(0.5f, 0.5f);
                    g.sizeDelta = new Vector2(sh * 0.62f, sh * 0.62f);
                }

                // 숫자 — 홈 왼쪽 끝 안에
                var c0 = At(G0 + 30f);
                _num = UiKit.Text(_body, "Num", "", Mathf.Clamp(c0.y * 2f * s * 0.82f, 12f, 20f), TextAlignmentOptions.Left);
                var nr = _num.rectTransform;
                nr.anchorMin = nr.anchorMax = Vector2.zero;
                nr.pivot = new Vector2(0f, 0.5f);
                nr.anchoredPosition = new Vector2((G0 + 14f) * s, c0.x * s + 0.5f);
                nr.sizeDelta = new Vector2(200f, c0.y * 2f * s + 6f);
                _num.color = Color.white;
                _num.fontStyle = FontStyles.Bold;
                _num.outlineWidth = 0.2f;
                _num.outlineColor = new Color32(0, 0, 0, 170);
            }

            static Image Filled(RectTransform p, string name, Color c, Sprite s)
            {
                var img = UiKit.Image(p, name, c, s);
                UiKit.Stretch(img.rectTransform);
                img.type = Image.Type.Filled;
                img.fillMethod = Image.FillMethod.Horizontal;
                img.fillOrigin = (int)Image.OriginHorizontal.Left;
                return img;
            }

            static float Amount(float ratio) => (G0 + ratio * (G1 - G0)) / W;

            public void Tick(float cur, float max, float dt)
            {
                float ratio = max <= 0f ? 0f : Mathf.Clamp01(cur / max);
                if (_shown < 0f) { _shown = _lagV = _last = ratio; }

                if (ratio < _last - 0.0005f) { _hold = 0.45f; _shake = 0.22f; _flash = 0.12f; }   // 맞았다
                if (ratio > _last + 0.01f) _gleamT = 0f;                                          // 찼다
                _last = ratio;

                _shown = Mathf.MoveTowards(_shown, ratio, Mathf.Max(0.02f, Mathf.Abs(ratio - _shown) * 14f) * dt);
                if (_lagV < _shown) _lagV = _shown;
                else if ((_hold -= dt) <= 0f) _lagV = Mathf.MoveTowards(_lagV, _shown, (0.25f + (_lagV - _shown) * 2.2f) * dt);

                _fill.fillAmount = _shown <= 0.0005f ? 0f : Amount(_shown);
                _lag.fillAmount = _lagV <= 0.0005f ? 0f : Amount(_lagV);

                // 맞을 때 — 흔들림 · 번쩍
                if (_shake > 0f)
                {
                    _shake -= dt;
                    float a = Mathf.Max(0f, _shake) / 0.22f * 4f;
                    _body.anchoredPosition = _home + new Vector2(Random.Range(-a, a), Random.Range(-a, a) * 0.6f);
                }
                else _body.anchoredPosition = _home;

                bool warn = ratio > 0f && ratio <= _warnAt;
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6.5f);
                var fc = warn ? Color.Lerp(_col, Color.white, 0.22f * pulse) : _col;
                if (_flash > 0f) { _flash -= dt; fc = Color.Lerp(fc, Color.white, Mathf.Clamp01(_flash / 0.12f) * 0.8f); }
                _fill.color = fc;
                if (_halo != null) _halo.color = warn ? new Color(_warnCol.r, _warnCol.g, _warnCol.b, 0.30f + 0.60f * pulse) : Color.clear;

                // 채움 끝 화살촉 — 홈 곡선을 따라 기울어진다
                float x = G0 + _shown * (G1 - G0);
                if (_tip != null)
                {
                    var c = At(x);
                    bool show = _shown > 0.01f && _shown < 0.985f;
                    _tip.enabled = show;
                    if (show)
                    {
                        var c2 = At(x + 8f);
                        float ang = Mathf.Atan2(c2.x - c.x, 8f) * Mathf.Rad2Deg;
                        float th = c.y * 2f * _s * 1.18f;
                        var t = _tip.rectTransform;
                        t.sizeDelta = new Vector2(th * 1.05f, th);
                        t.anchoredPosition = new Vector2(x * _s, c.x * _s);
                        t.localEulerAngles = new Vector3(0f, 0f, ang);
                        _tip.color = Color.Lerp(fc, Color.white, 0.3f);
                    }
                }

                // 반짝임 — 회복하면 바로, 가득 찬 채로 있으면 몇 초마다
                if (_gleam != null)
                {
                    if (ratio >= 0.999f && _gleamT >= 1f && (_idle += dt) > 4.5f) { _idle = 0f; _gleamT = 0f; }
                    if (_gleamT < 1f)
                    {
                        _gleamT += dt / 0.6f;
                        float k = Mathf.Clamp01(_gleamT);
                        float gx = Mathf.Lerp(G0, G0 + Mathf.Max(0.15f, _shown) * (G1 - G0), k);
                        var c = At(gx);
                        float ry = c.x + c.y + (c.z - c.x - c.y) * 0.5f;   // 홈 위 은색 테두리
                        var g = _gleam.rectTransform;
                        g.anchoredPosition = new Vector2(gx * _s, ry * _s);
                        g.localEulerAngles = new Vector3(0f, 0f, k * 40f);
                        _gleam.color = new Color(1f, 1f, 1f, Mathf.Sin(k * Mathf.PI));
                    }
                    else _gleam.color = Color.clear;
                }

                int ci = Mathf.CeilToInt(cur), mi = Mathf.CeilToInt(max);
                if (ci != _lastCur || mi != _lastMax)
                {
                    _lastCur = ci; _lastMax = mi;
                    _num.text = $"{ci} <color=#ffffffaa>/ {mi}</color>";
                }
            }
        }
    }
}
