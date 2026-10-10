using UnityEngine;
using TMPro;

namespace Proto.Feel
{
    /// <summary>튀어 올랐다가 떨어지며 사라지는 숫자. Feel이 풀로 돌려 쓴다.</summary>
    public class DamageNumber : MonoBehaviour
    {
        TextMeshPro _tmp;
        float _age = -1f;
        float _life;
        Vector3 _start;
        Vector3 _vel;
        float _baseSize;

        public static DamageNumber Create(Transform parent, TMP_FontAsset font)
        {
            var go = new GameObject("DmgNumber");
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshPro>();
            if (font != null) tmp.font = font;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
            tmp.outlineWidth = 0.25f;
            tmp.outlineColor = new Color32(20, 12, 8, 255);
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.rectTransform.sizeDelta = new Vector2(4f, 1.5f);
            tmp.sortingOrder = 100;
            var n = go.AddComponent<DamageNumber>();
            n._tmp = tmp;
            go.SetActive(false);
            return n;
        }

        public void Show(Vector3 pos, string text, Feel.NumberKind kind, Quaternion faceRot)
        {
            gameObject.SetActive(true);
            _tmp.text = text;
            switch (kind)
            {
                case Feel.NumberKind.Kill:
                    _tmp.color = new Color(1f, 0.82f, 0.2f);
                    _baseSize = 7.5f; _life = 0.8f; break;
                case Feel.NumberKind.Info:
                    _tmp.color = new Color(1f, 0.72f, 0.25f);
                    _baseSize = 8f; _life = 1.1f; break;
                case Feel.NumberKind.Player:
                    _tmp.color = new Color(1f, 0.3f, 0.25f);
                    _baseSize = 5f; _life = 0.7f; break;
                default:
                    _tmp.color = Color.white;
                    _baseSize = 5.5f; _life = 0.6f; break;
            }
            transform.rotation = faceRot;
            _start = pos;
            _vel = new Vector3(Random.Range(-0.6f, 0.6f), 4.2f, 0f);
            _age = 0f;
            Tick(0f);
        }

        void Update()
        {
            if (_age < 0f) return;
            _age += Time.unscaledDeltaTime;
            if (_age >= _life) { _age = -1f; gameObject.SetActive(false); return; }
            Tick(_age);
        }

        void Tick(float a)
        {
            // 위로 톡 튀어 오르고 중력으로 떨어진다
            transform.position = _start + _vel * a + 0.5f * new Vector3(0f, -11f, 0f) * a * a;

            // 등장할 때 크게 → 제자리 크기. 마지막 30%에 페이드
            float pop = a < 0.08f ? Mathf.Lerp(1.7f, 1f, a / 0.08f) : 1f;
            _tmp.fontSize = _baseSize * pop;
            float k = a / _life;
            var c = _tmp.color;
            c.a = k < 0.7f ? 1f : Mathf.Lerp(1f, 0f, (k - 0.7f) / 0.3f);
            _tmp.color = c;
        }
    }
}
