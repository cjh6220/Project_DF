using UnityEngine;
using TMPro;

namespace Proto.Town
{
    /// <summary>
    /// 마을 가운데 문. 플레이어가 안에 서 있으면 소용돌이가 점점 빨라지다가, 다 차면 던전으로 들어간다.
    /// 다 차기 전에 걸어 나오면 소용돌이가 다시 느려지고 아무 일도 없다 — 마음을 바꿀 시간을 준다.
    ///
    /// 문 안쪽 소용돌이와 불빛이 천천히 숨 쉰다 — 멀리서도 "저기가 입구"로 읽히게.
    /// 포털 색은 던전마다 다르다. 행선지를 바꾸면 소용돌이·바닥 마법진·
    /// 빛 알갱이·불빛이 그 던전 색으로 부드럽게 넘어간다.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class VillageGate : MonoBehaviour
    {
        [SerializeField] VillageController village;
        [SerializeField] Transform swirl;
        [SerializeField] Light glow;
        [SerializeField] float glowBase = 2.2f;

        [Header("포털 색을 바꿀 대상")]
        [SerializeField] Renderer surface;
        [SerializeField] Renderer floorGlow;
        [SerializeField] Renderer rune;
        [SerializeField] ParticleSystem[] particles;
        [Tooltip("도리이 위 이름표 — 고른 던전 이름으로 바뀐다")]
        [SerializeField] TMP_Text label;
        [Tooltip("색이 넘어가는 시간(초)")]
        [SerializeField] float blendTime = 0.45f;

        [Header("바닥 마법진")]
        [SerializeField] float runeSpin = 4f;
        [SerializeField] float runeBase = 1.4f;

        [Header("들어가기 — 문 안에 머무는 시간")]
        [Tooltip("이만큼 문 안에 서 있으면 들어간다(초)")]
        [SerializeField] float chargeTime = 1.5f;
        [Tooltip("문을 벗어나면 이 시간에 걸쳐 원래대로 가라앉는다(초)")]
        [SerializeField] float releaseTime = 0.5f;
        [Tooltip("다 찼을 때 소용돌이가 더 도는 속도 (라디안/초)")]
        [SerializeField] float chargeSpin = 7f;

        static readonly int IdDeep = Shader.PropertyToID("_ColorDeep");
        static readonly int IdMid = Shader.PropertyToID("_ColorMid");
        static readonly int IdCore = Shader.PropertyToID("_ColorCore");
        static readonly int IdRim = Shader.PropertyToID("_RimColor");
        static readonly int IdBase = Shader.PropertyToID("_BaseColor");
        static readonly int IdPhase = Shader.PropertyToID("_PhaseOffset");
        static readonly int IdIntensity = Shader.PropertyToID("_Intensity");
        static readonly int IdTwist = Shader.PropertyToID("_Twist");

        MaterialPropertyBlock _mpb;
        Color _fromMain, _fromAccent, _toMain, _toAccent, _main, _accent;
        float _blendT = 1f;
        bool _hasColor, _painted;
        float _flare;   // 들어설 때 마법진이 번쩍인다

        // 들어가기
        float _lastInside = -10f;   // 마지막으로 트리거 안에서 확인된 시각
        bool Inside => Time.time - _lastInside < 0.12f;
        float _charge, _phase;
        bool _fired;
        float _baseIntensity = 1f, _baseTwist = 3.2f;
        Vector3 _surfaceScale;
        float[] _baseRate;
        float _moteOrbital = 1.6f, _moteRadial = -0.9f;

        /// <summary>0~1. 문 안에 서 있는 동안 차오른다.</summary>
        public float Charge => _charge;

        void Awake()
        {
            if (surface != null)
            {
                _surfaceScale = surface.transform.localScale;
                var m = surface.sharedMaterial;
                if (m != null)
                {
                    if (m.HasProperty(IdIntensity)) _baseIntensity = m.GetFloat(IdIntensity);
                    if (m.HasProperty(IdTwist)) _baseTwist = m.GetFloat(IdTwist);
                }
            }
            if (particles != null)
            {
                _baseRate = new float[particles.Length];
                for (int i = 0; i < particles.Length; i++)
                    if (particles[i] != null) _baseRate[i] = particles[i].emission.rateOverTime.constant;
                if (particles.Length > 0 && particles[0] != null)
                {
                    var v = particles[0].velocityOverLifetime;
                    _moteOrbital = v.orbitalZ.constant;
                    _moteRadial = v.radial.constant;
                }
            }
        }

        void OnTriggerEnter(Collider other) { if (other.CompareTag("Player")) _lastInside = Time.time; }
        void OnTriggerStay(Collider other) { if (other.CompareTag("Player")) _lastInside = Time.time; }
        void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            _lastInside = -10f;
            if (village != null) village.OnGateExited();
        }

        /// <summary>마을로 돌아왔을 때 — 차오른 것을 비운다.</summary>
        public void ResetCharge()
        {
            _charge = 0f; _fired = false; _lastInside = -10f;
        }

        /// <summary>포털을 이 색으로 바꾼다. instant면 바로, 아니면 부드럽게.</summary>
        public void SetPalette(Color main, Color accent, bool instant = false)
        {
            if (!_hasColor || instant)
            {
                _main = _fromMain = _toMain = main;
                _accent = _fromAccent = _toAccent = accent;
                _blendT = 1f; _hasColor = true; _painted = false;
                Paint();
                return;
            }
            if (main == _toMain && accent == _toAccent) return;
            _fromMain = _main; _fromAccent = _accent;
            _toMain = main; _toAccent = accent;
            _blendT = 0f;
        }

        /// <summary>도리이 위 이름표를 이 던전으로.</summary>
        public void SetLabel(string dungeonName, Color accent)
        {
            if (label == null) return;
            var tint = Color.Lerp(accent, Color.white, 0.25f);
            label.text = $"<size=70%><color=#{ColorUtility.ToHtmlStringRGB(tint)}>던전 입구</color></size>\n{dungeonName}";
        }

        /// <summary>던전을 골랐다 — 마법진이 크게 번쩍인다.</summary>
        public void Flare() => _flare = 1f;

        void Update()
        {
            float dt = Time.deltaTime;
            TickCharge(dt);

            if (swirl != null) swirl.Rotate(0f, 0f, -40f * dt, Space.Self);
            float breathe = 0.85f + 0.15f * Mathf.Sin(Time.time * 2.4f);
            _flare = Mathf.MoveTowards(_flare, 0f, Time.unscaledDeltaTime * 1.4f);
            float k = _charge * _charge;   // 처음엔 살살, 끝에 몰아친다
            if (glow != null) glow.intensity = glowBase * breathe * (1f + _flare * 1.5f + k * 2f);

            if (rune != null) rune.transform.Rotate(0f, (runeSpin + 90f * k) * dt, 0f, Space.World);

            if (_hasColor && _blendT < 1f)
            {
                _blendT = Mathf.Min(1f, _blendT + Time.unscaledDeltaTime / Mathf.Max(0.01f, blendTime));
                float e = 1f - (1f - _blendT) * (1f - _blendT);
                _main = Color.Lerp(_fromMain, _toMain, e);
                _accent = Color.Lerp(_fromAccent, _toAccent, e);
            }
            if (_hasColor) Paint();
            PaintCharge(k);
        }

        /// <summary>
        /// 문 안에 있으면 차오르고, 나오면 가라앉는다. 다 차면 한 번만 들어간다.
        /// 마을이 들어갈 수 없는 상태(창이 열림·막 도착함)면 차오르지 않는다.
        /// </summary>
        void TickCharge(float dt)
        {
            bool ready = village != null && village.GateReady;
            if (Inside && ready && !_fired)
            {
                _charge = Mathf.Min(1f, _charge + dt / Mathf.Max(0.05f, chargeTime));
                if (_charge >= 1f)
                {
                    _fired = true;
                    _flare = 1f;
                    village.OnGateEntered();
                }
            }
            else if (!_fired)
            {
                _charge = Mathf.Max(0f, _charge - dt / Mathf.Max(0.05f, releaseTime));
            }
        }

        /// <summary>차오르는 만큼 소용돌이가 빨라지고, 비틀리고, 밝아지고, 떤다.</summary>
        void PaintCharge(float k)
        {
            _phase += chargeSpin * k * Time.deltaTime;

            if (surface != null)
            {
                _mpb ??= new MaterialPropertyBlock();
                surface.GetPropertyBlock(_mpb);
                _mpb.SetFloat(IdPhase, _phase);
                _mpb.SetFloat(IdIntensity, _baseIntensity * (1f + 0.9f * k));
                _mpb.SetFloat(IdTwist, _baseTwist + 2.8f * k);
                surface.SetPropertyBlock(_mpb);

                // 다 찰 무렵엔 문 전체가 떨린다
                float shake = k > 0.4f ? (k - 0.4f) / 0.6f : 0f;
                float s = 1f + 0.025f * shake * Mathf.Sin(Time.time * 38f) + 0.04f * k;
                surface.transform.localScale = new Vector3(_surfaceScale.x * s, _surfaceScale.y * s, _surfaceScale.z);
            }

            if (rune != null)
            {
                _mpb ??= new MaterialPropertyBlock();
                rune.GetPropertyBlock(_mpb);
                var c = _mpb.GetColor(IdBase) * (1f + 1.8f * k);
                c.a = 1f;
                _mpb.SetColor(IdBase, c);
                rune.SetPropertyBlock(_mpb);
            }

            if (particles != null && _baseRate != null)
            {
                for (int i = 0; i < particles.Length; i++)
                {
                    var ps = particles[i];
                    if (ps == null) continue;
                    var em = ps.emission;
                    em.rateOverTime = _baseRate[i] * (1f + 3f * k);
                }
                if (particles.Length > 0 && particles[0] != null)
                {
                    var v = particles[0].velocityOverLifetime;
                    v.orbitalZ = new ParticleSystem.MinMaxCurve(_moteOrbital * (1f + 3f * k));
                    v.radial = new ParticleSystem.MinMaxCurve(_moteRadial * (1f + 2f * k));
                }
            }
        }

        /// <summary>
        /// 주 색 하나와 밝은 색 하나로 포털 전체를 칠한다.
        /// 깊은 곳은 주 색을 아주 어둡게, 테두리·중심은 밝은 색을 1 넘게 — 블룸이 걸린다.
        /// </summary>
        void Paint()
        {
            _mpb ??= new MaterialPropertyBlock();
            float breathe = 0.85f + 0.15f * Mathf.Sin(Time.time * 2.4f);

            if (surface != null)
            {
                surface.GetPropertyBlock(_mpb);
                _mpb.SetColor(IdDeep, _main * 0.07f);
                _mpb.SetColor(IdMid, _main);
                _mpb.SetColor(IdCore, _accent * 1.6f);
                _mpb.SetColor(IdRim, _accent * 1.8f + _main * 0.2f);
                surface.SetPropertyBlock(_mpb);
            }
            if (floorGlow != null)
            {
                floorGlow.GetPropertyBlock(_mpb);
                var c = Color.Lerp(_main, _accent, 0.25f); c.a = Mathf.Lerp(0.7f, 1f, _charge);
                _mpb.SetColor(IdBase, c);
                floorGlow.SetPropertyBlock(_mpb);
            }
            if (rune != null)
            {
                rune.GetPropertyBlock(_mpb);
                var c = Color.Lerp(_main, _accent, 0.45f) * runeBase * (0.8f + 0.2f * breathe) * (1f + _flare * 2.2f);
                c.a = 1f;
                _mpb.SetColor(IdBase, c);
                rune.SetPropertyBlock(_mpb);
            }
            if (particles != null && (_blendT < 1f || !_painted))
            {
                foreach (var ps in particles)
                {
                    if (ps == null) continue;
                    var m = ps.main;
                    m.startColor = new ParticleSystem.MinMaxGradient(
                        Color.Lerp(_main, Color.white, 0.25f), Color.Lerp(_accent, Color.white, 0.2f));
                }
                _painted = true;
            }
            if (glow != null) glow.color = Color.Lerp(_main, _accent, 0.35f);
        }
    }
}
