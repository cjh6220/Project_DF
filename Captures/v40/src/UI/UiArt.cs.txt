using UnityEngine;

namespace Proto.UI
{
    /// <summary>
    /// 코드로 그리는 UI 그림들 — 금속 링, 자물쇠, 조준점, 말풍선 꼬리, 그라데이션 버튼.
    /// 이미지 파일 없이 스킬트리 화면을 꾸미는 데 쓴다. 한 번 만들면 재사용한다.
    /// </summary>
    public static class UiArt
    {
        static Sprite _ring, _lock, _cross, _tri, _btnGrad, _roundRect, _roundOutline, _disc;

        /// <summary>금속 느낌 원판 — 위가 밝고 아래가 어두운 세로 그라데이션 + 바깥 테두리. 색을 곱해서 쓴다.</summary>
        public static Sprite MetalDisc
        {
            get
            {
                if (_ring != null) return _ring;
                const int N = 256;
                var t = NewTex(N, N);
                float px = 2f / N;
                for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01((1f - d) / px);
                    float v = Mathf.Lerp(0.55f, 1f, dy * 0.5f + 0.5f);              // 위가 밝다
                    v *= Mathf.Lerp(0.55f, 1f, Mathf.Clamp01((1f - d) / 0.035f));   // 바깥 가장자리 어둡게
                    float spec = Mathf.Clamp01(1f - Mathf.Abs(d - 0.93f) * 30f) * Mathf.Clamp01(dy + 0.3f);
                    v = Mathf.Clamp01(v + spec * 0.35f);                          // 위쪽 반사광
                    t.SetPixel(x, y, new Color(v, v, v, a));
                }
                t.Apply();
                return _ring = Sprite.Create(t, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
            }
        }

        /// <summary>매끈한 원판 (마스크·안쪽 채움용).</summary>
        public static Sprite Disc
        {
            get
            {
                if (_disc != null) return _disc;
                const int N = 256;
                var t = NewTex(N, N);
                float px = 2f / N;
                for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01((1f - d) / px)));
                }
                t.Apply();
                return _disc = Sprite.Create(t, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
            }
        }

        /// <summary>자물쇠 모양 (흰색).</summary>
        public static Sprite Lock
        {
            get
            {
                if (_lock != null) return _lock;
                const int N = 64;
                var t = NewTex(N, N);
                for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float fx = x + 0.5f, fy = y + 0.5f;
                    // 몸통 (둥근 사각형)
                    float bx = Mathf.Max(Mathf.Abs(fx - 32f) - 13f, 0f), by = Mathf.Max(Mathf.Abs(fy - 22f) - 11f, 0f);
                    float body = Mathf.Clamp01(4f - Mathf.Sqrt(bx * bx + by * by) + 0.5f) > 0f && Mathf.Abs(fx - 32f) <= 17f && Mathf.Abs(fy - 22f) <= 15f ? 1f : 0f;
                    // 고리 (위쪽 반원 + 다리)
                    float r = Mathf.Sqrt((fx - 32f) * (fx - 32f) + (fy - 38f) * (fy - 38f));
                    float ring = (r >= 7.5f && r <= 12.5f && fy >= 38f) ? 1f : 0f;
                    float legs = (Mathf.Abs(Mathf.Abs(fx - 32f) - 10f) <= 2.5f && fy >= 33f && fy < 38f) ? 1f : 0f;
                    // 열쇠 구멍
                    float hole = ((fx - 32f) * (fx - 32f) + (fy - 24f) * (fy - 24f) <= 10f || (Mathf.Abs(fx - 32f) <= 1.5f && fy >= 15f && fy <= 24f)) ? 1f : 0f;
                    float a = Mathf.Max(Mathf.Max(body * (1f - hole), ring), legs);
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
                t.Apply();
                return _lock = Sprite.Create(t, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
            }
        }

        /// <summary>조준점 (중앙 정렬 버튼).</summary>
        public static Sprite Crosshair
        {
            get
            {
                if (_cross != null) return _cross;
                const int N = 64;
                var t = NewTex(N, N);
                for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float fx = x + 0.5f - 32f, fy = y + 0.5f - 32f;
                    float r = Mathf.Sqrt(fx * fx + fy * fy);
                    float ring = Mathf.Clamp01(2f - Mathf.Abs(r - 18f));
                    float dot = Mathf.Clamp01(4.5f - r);
                    float tick = ((Mathf.Abs(fx) <= 1.6f && Mathf.Abs(fy) >= 20f && Mathf.Abs(fy) <= 29f) ||
                                  (Mathf.Abs(fy) <= 1.6f && Mathf.Abs(fx) >= 20f && Mathf.Abs(fx) <= 29f)) ? 1f : 0f;
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Max(ring, Mathf.Max(dot, tick))));
                }
                t.Apply();
                return _cross = Sprite.Create(t, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
            }
        }

        /// <summary>왼쪽을 가리키는 삼각형 (툴팁 꼬리).</summary>
        public static Sprite TriangleLeft
        {
            get
            {
                if (_tri != null) return _tri;
                const int N = 32;
                var t = NewTex(N, N);
                for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float fx = x + 0.5f, fy = Mathf.Abs(y + 0.5f - 16f);
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(fx * 0.5f - fy + 0.5f)));
                }
                t.Apply();
                return _tri = Sprite.Create(t, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
            }
        }

        /// <summary>둥근 사각형, 위가 밝은 세로 그라데이션 (금색 버튼). 9분할로 늘린다.</summary>
        public static Sprite GradientButton => _btnGrad != null ? _btnGrad : (_btnGrad = RoundRect(64, 12f, true, 0f));

        /// <summary>둥근 사각형 채움 (9분할).</summary>
        public static Sprite RoundRectFill => _roundRect != null ? _roundRect : (_roundRect = RoundRect(64, 10f, false, 0f));

        /// <summary>둥근 사각형 테두리만 (9분할).</summary>
        public static Sprite RoundRectOutline => _roundOutline != null ? _roundOutline : (_roundOutline = RoundRect(64, 10f, false, 2f));

        static Sprite RoundRect(int n, float radius, bool gradient, float outline)
        {
            var t = NewTex(n, n);
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float fx = x + 0.5f, fy = y + 0.5f;
                float cx = Mathf.Clamp(fx, radius, n - radius), cy = Mathf.Clamp(fy, radius, n - radius);
                float d = Mathf.Sqrt((fx - cx) * (fx - cx) + (fy - cy) * (fy - cy));
                float a = Mathf.Clamp01(radius - d + 0.5f);
                if (outline > 0f)
                {
                    // 안쪽 사각형까지의 거리로 테두리만 남긴다
                    float r2 = radius - outline;
                    float cx2 = Mathf.Clamp(fx, radius, n - radius), cy2 = Mathf.Clamp(fy, radius, n - radius);
                    float d2 = Mathf.Sqrt((fx - cx2) * (fx - cx2) + (fy - cy2) * (fy - cy2));
                    float inner = Mathf.Clamp01(r2 - d2 + 0.5f);
                    a = Mathf.Clamp01(a - inner);
                }
                float v = gradient ? Mathf.Lerp(0.72f, 1f, (float)y / (n - 1)) : 1f;
                t.SetPixel(x, y, new Color(v, v, v, a));
            }
            t.Apply();
            float b = radius + 2f;
            return Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

        static Texture2D NewTex(int w, int h) =>
            new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
    }
}
