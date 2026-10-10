using UnityEngine;

namespace Proto.Enemy
{
    /// <summary>
    /// 리깅 없는 모델의 아래쪽(촉수)을 코드로 흐느적거리게 한다 — 공허의 눈.
    ///
    ///   몸 아래 cutoff 비율 아래에 있는 정점만, 아래로 갈수록 크게 흔든다 (뿌리는 고정, 끝이 크게 휜다)
    ///   정점이 몸 중심에서 어느 방향에 있는지로 박자를 다르게 줘서 촉수마다 따로 논다
    ///   Excite() — 공격할 때 잠깐 크게 · 빠르게 / Die() — 서서히 멈추며 축 늘어진다
    ///
    /// 메시를 복사해서 쓴다 (원본 에셋은 건드리지 않는다). MeshFilter 하나짜리 정적 메시 기준.
    /// </summary>
    public class TentacleSway : MonoBehaviour
    {
        [Tooltip("몸 높이 중 아래에서 이 비율까지를 촉수로 본다")]
        [Range(0.1f, 0.8f)] public float cutoff = 0.45f;
        [Tooltip("흔들림 크기 (몸 높이 대비)")]
        public float amplitude = 0.07f;
        public float speed = 2.6f;

        MeshFilter _mf;
        Mesh _mesh;
        Vector3[] _base, _work;
        float[] _weight, _phase;
        float _height, _excite, _alive = 1f, _droop;
        int _frame;

        void Awake()
        {
            _mf = GetComponentInChildren<MeshFilter>();
            if (_mf == null || _mf.sharedMesh == null) { enabled = false; return; }
            _mesh = Instantiate(_mf.sharedMesh);
            _mesh.MarkDynamic();
            _mf.mesh = _mesh;
            _base = _mesh.vertices;
            _work = new Vector3[_base.Length];
            _weight = new float[_base.Length];
            _phase = new float[_base.Length];

            var b = _mesh.bounds;
            _height = b.size.y;
            float minY = b.min.y, cut = b.min.y + b.size.y * cutoff;
            for (int i = 0; i < _base.Length; i++)
            {
                var v = _base[i];
                float w = Mathf.Clamp01((cut - v.y) / Mathf.Max(0.0001f, cut - minY));
                _weight[i] = w * w;   // 뿌리 근처는 거의 안 움직인다
                float ang = Mathf.Atan2(v.z - b.center.z, v.x - b.center.x);
                _phase[i] = ang * 2f;  // 둘레 방향마다 박자가 다르다 → 촉수마다 따로
            }
        }

        /// <summary>공격 — 잠깐 크게 휘젓는다.</summary>
        public void Excite() => _excite = 1f;

        /// <summary>죽음 — 흔들림이 잦아들고 촉수가 아래로 처진다.</summary>
        public void Die() => _alive = 0f;

        void Update()
        {
            if (_mesh == null) return;
            float dt = Time.deltaTime;
            _excite = Mathf.MoveTowards(_excite, 0f, dt * 1.8f);
            _droop = Mathf.MoveTowards(_droop, _alive > 0f ? 0f : 1f, dt * 0.8f);
            float live = 1f - _droop;
            float amp = _height * amplitude * (1f + _excite * 1.6f) * live;
            float t = Time.time * speed * (1f + _excite);
            float sag = _height * 0.12f * _droop;

            for (int i = 0; i < _base.Length; i++)
            {
                float w = _weight[i];
                var v = _base[i];
                if (w > 0f)
                {
                    float p = _phase[i] + w * 2.4f;   // 끝으로 갈수록 늦게 따라온다 → 물결
                    v.x += Mathf.Sin(t + p) * amp * w;
                    v.z += Mathf.Cos(t * 0.8f + p) * amp * 0.6f * w;
                    v.y -= sag * w;
                }
                _work[i] = v;
            }
            _mesh.vertices = _work;
            if ((++_frame & 7) == 0) _mesh.RecalculateBounds();
        }

        void OnDestroy() { if (_mesh != null) Destroy(_mesh); }
    }
}
