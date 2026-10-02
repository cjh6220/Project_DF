using UnityEngine;
using Proto.Core;

namespace Proto.Enemy
{
    /// <summary>
    /// 몬스터 머리 위 체력바.
    ///
    ///   빨강  현재 체력 — 맞는 순간 즉시 줄어든다
    ///   흰색  잔상 — 잠깐 머물렀다가 따라 내려온다. 방금 얼마나 깎였는지가 눈에 남는다
    ///
    /// 카메라를 향해 돌고, 접두사로 몸집이 커져도 바 크기는 그대로다.
    /// 챔피언은 바가 더 길다. 죽으면 사라진다.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class EnemyHealthBar : MonoBehaviour
    {
        [SerializeField] Sprite sprite;
        [SerializeField] Material material;

        [SerializeField] float width = 1.0f;
        [SerializeField] float championWidth = 1.4f;
        [SerializeField] float height = 0.11f;
        [SerializeField] float headMargin = 0.35f;

        [Tooltip("잔상이 따라 내려오기 전 머무는 시간")]
        [SerializeField] float trailDelay = 0.35f;
        [SerializeField] float trailSpeed = 1.6f;   // 초당 비율

        static readonly Color ColBack = new Color(0.08f, 0.06f, 0.06f, 0.85f);
        static readonly Color ColTrail = new Color(1f, 0.92f, 0.75f, 1f);
        static readonly Color ColFill = new Color(0.90f, 0.13f, 0.13f, 1f);
        static readonly Color ColFillChampion = new Color(1f, 0.55f, 0.1f, 1f);

        Health _hp;
        EnemyController _ec;
        Transform _root;
        Transform _fill, _trail;
        SpriteRenderer _fillR;
        float _shown = 1f;        // 빨강 비율
        float _trailValue = 1f;   // 흰색 비율
        float _trailHoldUntil;
        float _top = -1f;         // 발밑 기준 머리 높이 (모델이 정해진 뒤 한 번 잰다)
        float _w;
        int _bornFrame;

        void Awake()
        {
            _bornFrame = Time.frameCount;
            _hp = GetComponent<Health>();
            _ec = GetComponent<EnemyController>();
            _hp.Changed += OnChanged;
            _hp.Died += () => { if (_root != null) _root.gameObject.SetActive(false); };
        }

        void OnDestroy()
        {
            if (_hp != null) _hp.Changed -= OnChanged;
        }

        void OnEnable()
        {
            if (_root != null) _root.gameObject.SetActive(!_hp.IsDead);
        }

        void Build()
        {
            _w = (_ec != null && _ec.IsChampion) ? championWidth : width;

            _root = new GameObject("HPBar").transform;
            _root.SetParent(transform, false);

            var back = Make("Back", ColBack, 0, 0f);
            // 테두리처럼 보이게 배경을 살짝 크게
            SetBar(back, 1f, _w + 0.04f, height + 0.04f);
            _trail = Make("Trail", ColTrail, 1, -0.002f);
            _fill = Make("Fill", (_ec != null && _ec.IsChampion) ? ColFillChampion : ColFill, 2, -0.004f);
            _fillR = _fill.GetComponent<SpriteRenderer>();

            _shown = _trailValue = Ratio();
            SetBar(_fill, _shown, _w, height);
            SetBar(_trail, _trailValue, _w, height);
        }

        Transform Make(string name, Color c, int order, float z)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            go.transform.localPosition = new Vector3(0f, 0f, z);   // 카메라 쪽(-z)으로 살짝씩 띄워 겹침 순서를 고정
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            if (material != null) r.sharedMaterial = material;
            r.color = c;
            r.sortingOrder = 100 + order;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go.transform;
        }

        /// <summary>왼쪽 끝을 고정한 채 비율만큼 늘린다.</summary>
        void SetBar(Transform t, float ratio, float w, float h)
        {
            if (t == null || sprite == null) return;
            Vector2 size = sprite.bounds.size;
            float len = w * Mathf.Clamp01(ratio);
            t.localScale = new Vector3(len / size.x, h / size.y, 1f);
            var p = t.localPosition;
            p.x = -w * 0.5f + len * 0.5f;
            t.localPosition = p;
        }

        float Ratio() => _hp.Max <= 0f ? 0f : _hp.Current / _hp.Max;

        void OnChanged(float cur, float max)
        {
            if (_root == null) return;
            float r = max <= 0f ? 0f : cur / max;

            // 회복(재설정)이면 잔상 없이 바로 채운다
            if (r >= _shown) { _shown = _trailValue = r; SetBar(_trail, r, _w, height); }
            else { _shown = r; _trailHoldUntil = Time.time + trailDelay; }

            SetBar(_fill, _shown, _w, height);
        }

        void LateUpdate()
        {
            if (_hp.IsDead) return;

            // 모델은 스폰 직후에 갈아끼워지고, 원래 모델은 프레임 끝에야 지워진다.
            // 두 프레임 기다렸다가 머리 높이를 재야 헌 모델 크기에 속지 않는다.
            if (_top < 0f)
            {
                if (Time.frameCount < _bornFrame + 2) return;
                _top = MeasureTop();
            }
            if (_root == null) Build();

            float s = transform.lossyScale.y;
            _root.position = transform.position + Vector3.up * (_top + headMargin);
            _root.localScale = Vector3.one / Mathf.Max(0.01f, s);   // 몸집이 커져도 바 크기는 그대로

            var cam = Camera.main;
            if (cam != null) _root.rotation = cam.transform.rotation;

            // 잔상 — 잠깐 머물렀다가 따라 내려온다
            if (_trailValue > _shown && Time.time >= _trailHoldUntil)
            {
                _trailValue = Mathf.MoveTowards(_trailValue, _shown, trailSpeed * Time.deltaTime);
                SetBar(_trail, _trailValue, _w, height);
            }
        }

        float MeasureTop()
        {
            float top = 0f;
            bool any = false;
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                if (r is SpriteRenderer || r is ParticleSystemRenderer) continue;
                top = Mathf.Max(top, r.bounds.max.y - transform.position.y);
                any = true;
            }
            if (!any)
            {
                var cc = GetComponent<CharacterController>();
                top = cc != null ? (cc.center.y + cc.height * 0.5f) * transform.lossyScale.y : 1.6f;
            }
            return top;
        }
    }
}
