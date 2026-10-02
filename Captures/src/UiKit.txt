using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Proto.Core;

namespace Proto.UI
{
    /// <summary>
    /// 메타 화면(정산·스킬트리)을 코드로 조립할 때 쓰는 도구 모음.
    /// 씬 빌더에 UI 배치를 하드코딩하지 않는다 — 화면 구성은 각 View가 스스로 만든다.
    /// </summary>
    public static class UiKit
    {
        static Sprite _white, _circle, _ring, _soft;

        public static Sprite White
        {
            get
            {
                if (_white == null)
                    _white = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);
                return _white;
            }
        }

        /// <summary>안티앨리어싱된 원.</summary>
        public static Sprite Circle => _circle != null ? _circle : (_circle = MakeCircle(128, 0f));

        static Sprite _rsq;

        /// <summary>
        /// 모서리가 살짝 둥근 사각형. 스킬트리 노드 틀.
        /// 아이콘 그림이 사각형이라 원으로 자르면 가장자리가 많이 잘린다.
        /// </summary>
        public static Sprite RoundedSquare
        {
            get
            {
                if (_rsq != null) return _rsq;
                const int N = 128;
                const float R = 14f;   // 모서리 반지름 (픽셀)
                var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f;
                    // 모서리 원의 중심까지 거리 — 가장자리 1픽셀은 부드럽게
                    float cx = Mathf.Clamp(px, R, N - R), cy = Mathf.Clamp(py, R, N - R);
                    float d = Mathf.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
                    float a = Mathf.Clamp01(R - d + 0.5f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
                tex.Apply();
                _rsq = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
                return _rsq;
            }
        }

        /// <summary>테두리 링 (두께 약 10%).</summary>
        public static Sprite Ring => _ring != null ? _ring : (_ring = MakeCircle(128, 0.80f));

        /// <summary>가운데가 밝고 가장자리가 사라지는 원. 글로우용.</summary>
        public static Sprite Soft
        {
            get
            {
                if (_soft != null) return _soft;
                const int N = 128;
                var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - d);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
                tex.Apply();
                _soft = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
                return _soft;
            }
        }

        static Sprite MakeCircle(int n, float inner)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float px = 2f / n;   // 한 픽셀 폭 (정규화 좌표)
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01((1f - d) / px);
                if (inner > 0f) a *= Mathf.Clamp01((d - inner) / px);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        }

        // ── 조립 ──

        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static Image Image(Transform parent, string name, Color c, Sprite s = null, bool raycast = false)
        {
            var rt = Rect(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = s != null ? s : White;
            img.color = c;
            img.raycastTarget = raycast;
            return img;
        }

        public static TextMeshProUGUI Text(Transform parent, string name, string text, float size,
                                           TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var rt = Rect(parent, name);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            t.color = Color.white;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            return t;
        }

        /// <summary>납작한 버튼. 영상 레퍼런스처럼 회색 판에 흰 글씨.</summary>
        public static Button Button(Transform parent, string name, string label, Vector2 size, float fontSize = 22f)
        {
            var img = Image(parent, name, new Color(0.30f, 0.32f, 0.34f, 0.95f), null, true);
            img.rectTransform.sizeDelta = size;
            var b = img.gameObject.AddComponent<Button>();
            var colors = b.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            colors.colorMultiplier = 1.4f;
            b.colors = colors;

            var edge = Image(img.transform, "Edge", new Color(0f, 0f, 0f, 0.5f));
            Stretch(edge.rectTransform);
            edge.rectTransform.offsetMin = new Vector2(0f, 0f);
            edge.rectTransform.offsetMax = new Vector2(0f, -size.y + 3f);   // 아래쪽 그림자 줄

            var t = Text(img.transform, "Label", label, fontSize);
            Stretch(t.rectTransform);
            return b;
        }

        /// <summary>글자 안에 끼워 넣는 재화 아이콘 (TMP 기본 스프라이트 에셋 "ResourceIcons").</summary>
        public static string Icon(ResourceId id) => "<sprite name=\"" + id.ToString().ToLowerInvariant() + "\">";

        /// <summary>아이콘 + 이름. 재화를 글자로 보여 주는 곳은 전부 이걸 쓴다.</summary>
        public static string Label(ResourceId id) => Icon(id) + " " + Name(id);

        public static string Name(ResourceId id) => id switch
        {
            ResourceId.Gold => "골드",
            ResourceId.Ore => "원석",
            ResourceId.Crystal => "결정",
            ResourceId.Alien => "이질체",
            ResourceId.Essence => "정수",
            _ => "핵"
        };

        public static Color ResourceColor(ResourceId id) => id switch
        {
            ResourceId.Gold => new Color(1f, 0.85f, 0.35f),
            ResourceId.Ore => new Color(0.80f, 0.72f, 0.62f),
            ResourceId.Crystal => new Color(0.55f, 0.85f, 1f),
            ResourceId.Alien => new Color(0.75f, 1f, 0.55f),
            ResourceId.Essence => new Color(1f, 0.55f, 0.55f),
            _ => new Color(1f, 0.6f, 1f)
        };
    }
}
