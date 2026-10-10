using UnityEngine;

namespace Proto.Dungeon
{
    /// <summary>
    /// 출구에 세우는 문 — 겉모습만 담당한다 (지나가는 판정은 ExitGate).
    ///
    ///   모델     던전 테마의 doorModel (숲 아치 · 폐광 나무 문 · 설산 얼음 아치 · 마계 포털). 없으면 마을 도리이
    ///   막 · 바닥  문 안을 채우는 빛 막 — 잠김 붉게 / 열림 은은한 초록. 남쪽(카메라 쪽) 문은 모델 없이 바닥 빛만
    ///   보스 문   보스방으로 이어지는 문은 모델을 검붉게 칠하고 붉은 빛이 숨 쉬듯 깜빡인다
    /// </summary>
    public class ExitDoor : MonoBehaviour
    {
        Renderer[] _model;
        Renderer _veil, _floor;
        Light _light;
        bool _boss, _open;
        MaterialPropertyBlock _mpb;
        float _phase;

        static Material _veilMat, _floorMat;
        static readonly int IdBase = Shader.PropertyToID("_BaseColor");

        static readonly Color ClosedVeil = new Color(1.6f, 0.25f, 0.2f, 0.55f);
        static readonly Color OpenVeil = new Color(0.4f, 1.4f, 0.7f, 0.18f);
        static readonly Color BossVeil = new Color(2.2f, 0.15f, 0.1f, 0.7f);
        static readonly Color BossTint = new Color(0.62f, 0.22f, 0.2f, 1f);

        public static ExitDoor Build(Transform parent, Dir dir, GameObject model, float height, float width, bool boss)
        {
            var root = new GameObject("Door_" + dir);
            root.transform.SetParent(parent, false);
            var d = root.AddComponent<ExitDoor>();
            d._boss = boss;
            d._mpb = new MaterialPropertyBlock();

            // 남쪽 문은 카메라와 플레이어 사이라 모델을 세우면 가린다 — 바닥 빛만
            bool south = dir == Dir.S;
            // 옆 문을 90°로 세우면 카메라에서 옆면만 보여 기둥처럼 읽힌다 — 카메라 쪽으로 틀어 아치가 보이게
            float yaw = dir == Dir.N ? 0f : dir == Dir.E ? 50f : dir == Dir.W ? -50f : 180f;
            root.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            if (!south && model != null)
            {
                var m = Instantiate(model, root.transform);
                m.name = "Model";
                m.transform.localPosition = Vector3.zero;
                m.transform.localRotation = Quaternion.identity;
                foreach (var c in m.GetComponentsInChildren<Collider>()) Destroy(c);
                // 모델 크기가 제각각이라 키를 맞춘다
                Bounds b = new Bounds(); bool any = false;
                foreach (var r in m.GetComponentsInChildren<Renderer>()) { if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
                if (any && b.size.y > 0.01f)
                {
                    float k = height / b.size.y;
                    m.transform.localScale *= k;
                    // 바닥에 딱 붙인다
                    float bottom = (b.min.y - root.transform.position.y) * k;
                    m.transform.localPosition = new Vector3(0f, -bottom, 0f);
                }
                d._model = m.GetComponentsInChildren<Renderer>();
                if (boss) foreach (var r in d._model) { r.GetPropertyBlock(d._mpb); d._mpb.SetColor(IdBase, BossTint); r.SetPropertyBlock(d._mpb); }
            }

            // 문 안 빛 막 (세운 문만)
            if (!south)
            {
                d._veil = Quad(root.transform, "Veil", new Vector3(0f, height * 0.42f, 0f), Quaternion.identity, new Vector3(width * 0.62f, height * 0.78f, 1f));
            }
            // 바닥 빛
            d._floor = Quad(root.transform, "Floor", new Vector3(0f, 0.03f, south ? 0.4f : -0.4f), Quaternion.Euler(90f, 0f, 0f), new Vector3(width * 1.1f, 2.6f, 1f));

            if (boss)
            {
                var lgo = new GameObject("BossLight");
                lgo.transform.SetParent(root.transform, false);
                lgo.transform.localPosition = new Vector3(0f, 1.2f, -1.2f);
                d._light = lgo.AddComponent<Light>();
                d._light.type = LightType.Point;
                d._light.color = new Color(1f, 0.2f, 0.12f);
                d._light.range = 6f;
                d._light.intensity = 2f;
                d._light.shadows = LightShadows.None;
            }
            d.SetOpen(false);
            return d;
        }

        static Renderer Quad(Transform parent, string name, Vector3 pos, Quaternion rot, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = name == "Floor" ? FloorMaterial() : VeilMaterial();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }

        /// <summary>더하기 혼합 빛 재질 — 타격 이펙트와 같은 셰이더라 빌드에서 빠지지 않는다.</summary>
        static Material MakeMat(string name, Texture2D tex)
        {
            var src = Resources.Load<Material>("Fx/FxImpact");
            var m = src != null ? new Material(src) : new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            m.name = name;
            m.SetTexture("_BaseMap", tex);
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 5;
            return m;
        }

        static Material VeilMaterial()
        {
            if (_veilMat == null) _veilMat = MakeMat("DoorVeil", Gradient(64, 64, false));
            return _veilMat;
        }

        static Material FloorMaterial()
        {
            if (_floorMat == null) _floorMat = MakeMat("DoorFloor", Gradient(64, 64, true));
            return _floorMat;
        }

        /// <summary>
        /// 부드러운 빛 텍스처. radial = 가운데가 밝은 원 (바닥), 아니면 양옆이 흐리고 위로 갈수록 옅어지는 막.
        /// 흰 텍스처를 쓰면 네모 판이 그대로 보인다.
        /// </summary>
        static Texture2D Gradient(int w, int h, bool radial)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w * 2f - 1f, v = (y + 0.5f) / h;
                float k;
                if (radial)
                {
                    float d = Mathf.Sqrt(u * u + (v * 2f - 1f) * (v * 2f - 1f));
                    k = Mathf.Clamp01(1f - d); k *= k;
                }
                else
                {
                    float side = Mathf.Clamp01(1f - Mathf.Abs(u)); side = Mathf.SmoothStep(0f, 1f, side * 1.6f);
                    float up = Mathf.Clamp01(1f - v); up = 0.25f + 0.75f * up * up;
                    float bottom = Mathf.SmoothStep(0f, 1f, v * 8f);
                    k = side * up * bottom;
                }
                px[y * w + x] = new Color(k, k, k, k);
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        public void SetOpen(bool open)
        {
            _open = open;
            Paint(1f);
        }

        void Paint(float pulse)
        {
            var c = _boss ? BossVeil : (_open ? OpenVeil : ClosedVeil);
            if (_boss && _open) c.a *= 0.6f;
            c.a *= pulse;
            foreach (var r in new[] { _veil, _floor })
            {
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                var cc = r == _floor ? new Color(c.r, c.g, c.b, c.a * 0.8f) : c;
                _mpb.SetColor(IdBase, cc);
                r.SetPropertyBlock(_mpb);
            }
            if (_veil != null) _veil.enabled = !_open || _boss;   // 열린 일반 문은 막을 걷는다
        }

        void Update()
        {
            if (!_boss) return;
            // 보스 문 — 붉은 빛이 천천히 숨 쉰다
            _phase += Time.deltaTime * 2.2f;
            float k = 0.65f + 0.35f * Mathf.Sin(_phase);
            Paint(k);
            if (_light != null) _light.intensity = 1.2f + 1.6f * k;
        }
    }
}
