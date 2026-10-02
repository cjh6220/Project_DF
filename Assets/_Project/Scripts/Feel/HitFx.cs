using System.Collections.Generic;
using UnityEngine;

namespace Proto.Feel
{
    /// <summary>
    /// 칼질 이펙트 — 그림 한 장짜리 빛을 카메라 쪽으로 세워 잠깐 보여 준다.
    ///
    ///   검기   Slash  휘두를 때마다 몸 앞에 초승달 궤적. 1타는 내려 베기, 2타는 올려 베기(위아래 뒤집기)
    ///   타격   Impact 맞은 자리에 터지는 별빛
    ///   일섬   Line   가로로 그어지는 한 줄 빛 — 3타 마무리, 나중에 발도술
    ///
    /// 그림: Resources/Fx/FxSlashArc · FxImpact · FxIaidoLine (재질, 더하기 혼합)
    /// 히트스톱 중에도 아주 천천히 진행된다 — 멈춘 화면에 검기가 걸려 있는 게 손맛이다.
    /// </summary>
    public class HitFx : MonoBehaviour
    {
        enum Kind { Arc, Impact, Line }

        class Fx
        {
            public Transform T;
            public Renderer R;
            public Kind Kind;
            public float Age, Life, Size, Roll, Spin, Length;
            public Vector3 Pos;
            public bool FlipX, FlipY;
            public Color Color;
        }

        static HitFx _i;
        readonly List<Fx> _live = new(), _pool = new();
        Material _arc, _impact, _line;
        MaterialPropertyBlock _mpb;
        Camera _cam;
        static readonly int IdBase = Shader.PropertyToID("_BaseColor");

        static HitFx I
        {
            get
            {
                if (_i == null)
                {
                    var go = new GameObject("[HitFx]");
                    _i = go.AddComponent<HitFx>();
                }
                return _i;
            }
        }

        void Awake()
        {
            _arc = Resources.Load<Material>("Fx/FxSlashArc");
            _impact = Resources.Load<Material>("Fx/FxImpact");
            _line = Resources.Load<Material>("Fx/FxIaidoLine");
            _mpb = new MaterialPropertyBlock();
        }

        // ───────────────────────────── 부르기 ─────────────────────────────

        /// <summary>검기. variant 0 내려 베기, 1 올려 베기, 2 마무리(크고 비스듬히).</summary>
        public static void Slash(Vector3 center, bool right, int variant, float size)
        {
            var f = I.Spawn(Kind.Arc, I._arc);
            if (f == null) return;
            f.Pos = center; f.Size = size; f.Life = variant == 2 ? 0.26f : 0.2f;
            f.FlipX = !right; f.FlipY = variant == 1;
            f.Roll = variant == 2 ? -18f : Random.Range(-8f, 8f);
            f.Spin = (variant == 1 ? 1f : -1f) * 22f;   // 휘두르는 쪽으로 살짝 쓸려 간다
            f.Color = variant == 2 ? new Color(2.2f, 2.0f, 1.6f) : new Color(1.5f, 1.7f, 2.1f);
        }

        /// <summary>맞은 자리 별빛. heavy면 크고 오래.</summary>
        public static void Impact(Vector3 at, bool heavy)
        {
            var f = I.Spawn(Kind.Impact, I._impact);
            if (f == null) return;
            f.Pos = at; f.Size = heavy ? 2.1f : 1.4f; f.Life = heavy ? 0.2f : 0.14f;
            f.Roll = Random.Range(0f, 360f); f.Spin = Random.Range(-60f, 60f);
            f.Color = heavy ? new Color(2.4f, 2.0f, 1.4f) : new Color(2.0f, 1.9f, 1.7f);
        }

        /// <summary>가로 한 줄 빛 — 순식간에 그어졌다가 가늘어지며 사라진다.</summary>
        public static void Line(Vector3 center, bool right, float length)
        {
            var f = I.Spawn(Kind.Line, I._line);
            if (f == null) return;
            f.Pos = center; f.Length = length; f.Size = length * 0.1f; f.Life = 0.34f;
            f.FlipX = !right; f.Roll = Random.Range(-4f, 4f);
            f.Color = new Color(2.6f, 2.6f, 2.8f);
        }

        // ───────────────────────────── 속 ─────────────────────────────

        Fx Spawn(Kind kind, Material mat)
        {
            if (mat == null) return null;
            Fx f;
            if (_pool.Count > 0) { f = _pool[_pool.Count - 1]; _pool.RemoveAt(_pool.Count - 1); }
            else
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                go.name = "Fx";
                Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(transform, false);
                f = new Fx { T = go.transform, R = go.GetComponent<Renderer>() };
                f.R.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                f.R.receiveShadows = false;
            }
            f.Kind = kind; f.Age = 0f; f.FlipX = f.FlipY = false; f.Spin = 0f;
            f.R.sharedMaterial = mat;
            f.T.gameObject.SetActive(true);
            _live.Add(f);
            return f;
        }

        void LateUpdate()
        {
            if (_live.Count == 0) return;
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;

            // 히트스톱(시간 거의 정지) 동안에도 아주 조금씩은 흐른다
            float dt = Mathf.Max(Time.deltaTime, Time.unscaledDeltaTime * 0.15f);
            var camRot = _cam.transform.rotation;
            var toCam = -_cam.transform.forward;

            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var f = _live[i];
                f.Age += dt;
                float k = Mathf.Clamp01(f.Age / f.Life);
                if (k >= 1f)
                {
                    f.T.gameObject.SetActive(false);
                    _live.RemoveAt(i);
                    _pool.Add(f);
                    continue;
                }

                float sx, sy, alpha;
                switch (f.Kind)
                {
                    case Kind.Arc:
                    {
                        float grow = 1f - Mathf.Pow(1f - k, 3f);
                        float s = f.Size * Mathf.Lerp(0.72f, 1.06f, grow);
                        sx = sy = s;
                        alpha = k < 0.35f ? 1f : 1f - (k - 0.35f) / 0.65f;
                        break;
                    }
                    case Kind.Impact:
                    {
                        float s = f.Size * Mathf.Lerp(0.45f, 1.25f, 1f - (1f - k) * (1f - k));
                        sx = sy = s;
                        alpha = 1f - k * k;
                        break;
                    }
                    default:   // Line
                    {
                        float draw = Mathf.Clamp01(k / 0.15f);           // 처음 15%에 끝까지 그어진다
                        float thin = k < 0.15f ? 1f : Mathf.Lerp(1f, 0.15f, (k - 0.15f) / 0.85f);
                        sx = f.Length * (1f - Mathf.Pow(1f - draw, 3f));
                        sy = f.Size * thin;
                        alpha = k < 0.4f ? 1f : 1f - (k - 0.4f) / 0.6f;
                        break;
                    }
                }

                float roll = f.Roll + f.Spin * k;
                // 몸·적보다 카메라 쪽으로 조금 당겨서 그려야 가려지지 않는다
                f.T.position = f.Pos + toCam * 0.6f;
                f.T.rotation = camRot * Quaternion.Euler(0f, 0f, f.FlipX ? -roll : roll);
                f.T.localScale = new Vector3(f.FlipX ? -sx : sx, f.FlipY ? -sy : sy, 1f);

                var c = f.Color; c.a = alpha;
                f.R.GetPropertyBlock(_mpb);
                _mpb.SetColor(IdBase, c);
                f.R.SetPropertyBlock(_mpb);
            }
        }
    }
}
