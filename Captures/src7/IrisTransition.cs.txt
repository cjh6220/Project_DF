using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Proto.UI
{
    /// <summary>
    /// 원형으로 닫혔다가 열리는 화면 전환 (아이리스 와이프).
    ///
    ///   닫힘  화면 가장자리부터 검게 조여 들어가 가운데 한 점으로 사라진다
    ///   교체  완전히 검은 동안 화면을 갈아끼운다
    ///   열림  가운데에서 원이 퍼지며 새 화면이 드러난다
    ///
    /// 씬을 실제로 넘기지 않는다. 같은 씬 안에서 패널을 바꾸는데,
    /// 이 연출이 있으면 플레이어는 '다른 곳으로 넘어갔다'고 느낀다.
    /// </summary>
    public class IrisTransition : MonoBehaviour
    {
        [SerializeField] Material irisMaterial;
        [SerializeField] float closeTime = 0.45f;
        [SerializeField] float holdTime = 0.12f;
        [SerializeField] float openTime = 0.5f;

        RawImage _img;
        Material _mat;
        bool _busy;

        public bool Busy => _busy;

        static readonly int Radius = Shader.PropertyToID("_Radius");
        static readonly int Aspect = Shader.PropertyToID("_Aspect");

        // 화면 모서리까지 덮는 반지름. 화면 비율 16:9 기준 모서리 거리 ≈ 1.02
        const float OpenRadius = 1.25f;

        void Awake()
        {
            var rt = UiKit.Stretch(UiKit.Rect(transform, "Iris"));
            _img = rt.gameObject.AddComponent<RawImage>();
            _img.texture = Texture2D.whiteTexture;
            _img.color = Color.white;
            _img.raycastTarget = true;   // 전환 중에는 입력을 막는다
            if (irisMaterial != null)
            {
                _mat = new Material(irisMaterial);
                _img.material = _mat;
            }
            rt.gameObject.SetActive(false);
        }

        /// <summary>닫고 → 화면을 바꾸고 → 연다.</summary>
        public void Play(Action swap)
        {
            if (_busy) return;
            StartCoroutine(Run(swap));
        }

        IEnumerator Run(Action swap)
        {
            _busy = true;
            transform.SetAsLastSibling();              // 항상 맨 위에 그린다
            _img.gameObject.SetActive(true);

            yield return Animate(OpenRadius, 0f, closeTime, EaseIn);
            swap?.Invoke();
            transform.SetAsLastSibling();              // 교체된 화면이 자기를 맨 위로 올려도 전환막이 덮는다
            yield return new WaitForSecondsRealtime(holdTime);
            yield return Animate(0f, OpenRadius, openTime, EaseOut);

            _img.gameObject.SetActive(false);
            _busy = false;
        }

        IEnumerator Animate(float from, float to, float dur, Func<float, float> ease)
        {
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                Set(Mathf.Lerp(from, to, ease(Mathf.Clamp01(t / dur))));
                yield return null;
            }
            Set(to);
        }

        void Set(float r)
        {
            if (_mat == null) { _img.color = new Color(0f, 0f, 0f, r < 0.5f ? 1f : 0f); return; }
            _mat.SetFloat(Radius, r);
            _mat.SetFloat(Aspect, Screen.height > 0 ? (float)Screen.width / Screen.height : 1.777f);
        }

        static float EaseIn(float x) => x * x;
        static float EaseOut(float x) => 1f - (1f - x) * (1f - x);
    }
}
