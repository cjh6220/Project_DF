using UnityEngine;

namespace Proto.Enemy
{
    /// <summary>
    /// 바닥 공격 예고 (던파식 붉은 장판).
    ///
    ///   옅은 영역 = 맞는 범위
    ///   진한 채움 = 남은 시간. 채움이 영역을 다 덮는 순간 때린다
    ///
    /// 예고가 보여야 "깊이 이동으로 빠져나간다"가 성립한다.
    /// 시간은 게임 시간으로 흐른다 — 히트스톱 동안에는 예고도 멈춘다.
    /// </summary>
    public class Telegraph : MonoBehaviour
    {
        static Material _base;
        static Texture2D _circle;

        static readonly Color ColArea = new Color(1f, 0.12f, 0.08f, 0.22f);
        static readonly Color ColFill = new Color(1f, 0.22f, 0.10f, 0.45f);
        static readonly Color ColFlash = new Color(1f, 0.85f, 0.6f, 0.8f);

        Transform _fill;
        Renderer _fillR, _areaR;
        float _dur, _t;
        bool _circleShape;
        float _length, _width, _radius;
        float _dieAt = -1f;

        /// <summary>RoomController가 씬 빌더에서 받은 머티리얼을 넘겨준다.</summary>
        public static void SetMaterial(Material m) => _base = m;

        /// <summary>
        /// 직사각형 — from에서 dir 방향으로 length만큼, 폭 width.
        /// 채움은 시작점에서 끝으로 자란다 (돌진이 어디로 오는지 보인다).
        /// </summary>
        public static Telegraph Rect(Vector3 from, Vector3 dir, float length, float width, float duration)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector3.right;
            var t = Create("Telegraph_Rect", from, Quaternion.LookRotation(dir.normalized), duration);
            t._length = length; t._width = width;
            t._areaR = t.Quad("Area", ColArea, null, 0.02f);
            t._areaR.transform.localScale = new Vector3(width, length, 1f);
            t._areaR.transform.localPosition = new Vector3(0f, 0.02f, length * 0.5f);
            t._fillR = t.Quad("Fill", ColFill, null, 0.03f);
            t._fill = t._fillR.transform;
            t.Tick(0f);
            return t;
        }

        /// <summary>원 — 채움이 가운데에서 바깥으로 번진다.</summary>
        public static Telegraph Circle(Vector3 center, float radius, float duration)
        {
            var t = Create("Telegraph_Circle", center, Quaternion.identity, duration);
            t._circleShape = true;
            t._radius = radius;
            t._areaR = t.Quad("Area", ColArea, CircleTex, 0.02f);
            t._areaR.transform.localScale = new Vector3(radius * 2f, radius * 2f, 1f);
            t._fillR = t.Quad("Fill", ColFill, CircleTex, 0.03f);
            t._fill = t._fillR.transform;
            t.Tick(0f);
            return t;
        }

        static Telegraph Create(string name, Vector3 pos, Quaternion rot, float duration)
        {
            var go = new GameObject(name);
            pos.y = 0f;
            go.transform.SetPositionAndRotation(pos, rot);
            var t = go.AddComponent<Telegraph>();
            t._dur = Mathf.Max(0.01f, duration);
            return t;
        }

        Renderer Quad(string name, Color c, Texture tex, float y)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = name;
            Destroy(q.GetComponent<Collider>());
            q.transform.SetParent(transform, false);
            q.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // 바닥에 눕힌다 (쿼드의 위쪽 = 앞)
            q.transform.localPosition = new Vector3(0f, y, 0f);
            var r = q.GetComponent<Renderer>();
            if (_base != null) r.sharedMaterial = _base;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            SetColor(r, c, tex);
            return r;
        }

        static void SetColor(Renderer r, Color c, Texture tex = null)
        {
            var b = new MaterialPropertyBlock();
            r.GetPropertyBlock(b);
            b.SetColor("_BaseColor", c);
            if (tex != null) b.SetTexture("_BaseMap", tex);
            r.SetPropertyBlock(b);
        }

        void Update()
        {
            if (_dieAt >= 0f)
            {
                if (Time.time >= _dieAt) Destroy(gameObject);
                return;
            }
            _t += Time.deltaTime;
            Tick(Mathf.Clamp01(_t / _dur));
            if (_t >= _dur)
            {
                // 때리는 순간 한 번 번쩍이고 사라진다
                SetColor(_fillR, ColFlash);
                _dieAt = Time.time + 0.12f;
            }
        }

        void Tick(float k)
        {
            if (_circleShape)
            {
                float d = _radius * 2f * Mathf.Max(0.02f, k);
                _fill.localScale = new Vector3(d, d, 1f);
            }
            else
            {
                float len = _length * Mathf.Max(0.02f, k);
                _fill.localScale = new Vector3(_width, len, 1f);
                _fill.localPosition = new Vector3(0f, 0.03f, len * 0.5f);
            }
        }

        /// <summary>시전자가 죽거나 끊기면 예고도 치운다.</summary>
        public void Cancel()
        {
            if (this != null) Destroy(gameObject);
        }

        static Texture2D CircleTex
        {
            get
            {
                if (_circle != null) return _circle;
                const int N = 128;
                _circle = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01((1f - d) * N * 0.5f);            // 가장자리 안티앨리어싱
                    float rim = Mathf.Clamp01(1f - Mathf.Abs(d - 0.96f) * 25f); // 테두리를 조금 진하게
                    _circle.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(a * (0.75f + rim))));
                }
                _circle.Apply();
                return _circle;
            }
        }
    }
}
