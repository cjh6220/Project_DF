using UnityEngine;

namespace Proto.Town
{
    /// <summary>
    /// 도리이 옆 행선지 석판. 말을 걸면 던전 선택 창이 뜨고, 고른 던전이 도리이의 행선지가 된다.
    /// 석판 위에 떠 있는 작은 룬이 지금 행선지 색으로 빛난다 — 도리이와 같은 색이라 둘이 이어져 보인다.
    /// 대화·느낌표는 VillageNpc가 맡고, 이 컴포넌트는 겉모습만 맡는다.
    /// </summary>
    public class Waystone : MonoBehaviour
    {
        [SerializeField] Renderer rune;
        [SerializeField] float spin = 30f;
        [SerializeField] float bob = 0.06f;
        [SerializeField] float brightness = 1.6f;

        static readonly int IdBase = Shader.PropertyToID("_BaseColor");
        MaterialPropertyBlock _mpb;
        Color _color = new Color(0.6f, 0.4f, 1f), _target = new Color(0.6f, 0.4f, 1f);
        Vector3 _base;
        float _flare;

        void Awake() { if (rune != null) _base = rune.transform.localPosition; }

        /// <summary>행선지 색. instant가 아니면 부드럽게 넘어간다.</summary>
        public void SetColor(Color main, Color accent, bool instant = false)
        {
            _target = Color.Lerp(main, accent, 0.4f);
            if (instant) _color = _target;
        }

        /// <summary>행선지를 바꿨다 — 한 번 번쩍.</summary>
        public void Flare() => _flare = 1f;

        void Update()
        {
            if (rune == null) return;
            float dt = Time.unscaledDeltaTime;
            _color = Color.Lerp(_color, _target, 1f - Mathf.Exp(-dt * 6f));
            _flare = Mathf.MoveTowards(_flare, 0f, dt * 1.5f);

            rune.transform.localPosition = _base + Vector3.up * (Mathf.Sin(Time.time * 1.8f) * bob);
            rune.transform.Rotate(0f, 0f, spin * Time.deltaTime, Space.Self);

            _mpb ??= new MaterialPropertyBlock();
            rune.GetPropertyBlock(_mpb);
            float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 2.4f);
            var c = _color * brightness * pulse * (1f + _flare * 2.5f);
            c.a = 1f;
            _mpb.SetColor(IdBase, c);
            rune.SetPropertyBlock(_mpb);
        }
    }
}
