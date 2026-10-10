using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using TMPro;

namespace Proto.EditorTools
{
    /// <summary>
    /// 타격감에 쓰는 에셋을 코드로 만든다.
    ///   스파크 텍스처·머티리얼, 휘두르기/타격/처치 효과음(WAV)
    ///
    /// 효과음은 합성한 임시 소리다. 나중에 같은 이름의 파일로 덮어쓰면 그대로 바뀐다.
    /// </summary>
    public static class FeelAssetBuilder
    {
        const string Dir = "Assets/_Project/Feel";
        public const string SparkTexPath = Dir + "/SparkDot.png";
        public const string SparkMatPath = Dir + "/HitSpark.mat";
        public const string SwingPath = Dir + "/sfx_swing.wav";
        public const string HitPath = Dir + "/sfx_hit.wav";
        public const string KillPath = Dir + "/sfx_kill.wav";
        public const string FontPath = "Assets/_Project/Fonts/Malgun SDF 2.asset";

        const int Rate = 44100;

        [MenuItem("Proto/Build Feel Assets")]
        public static void Build()
        {
            Directory.CreateDirectory(Dir);

            BuildSparkTexture();
            WriteWav(SwingPath, Swing());
            WriteWav(HitPath, Hit(0.16f, 150f, 55f, 1f));
            WriteWav(KillPath, Hit(0.34f, 120f, 38f, 1.35f));
            AssetDatabase.Refresh();

            BuildSparkMaterial();
            AssetDatabase.SaveAssets();

            // 열려 있는 씬의 Feel에도 바로 물린다
            foreach (var f in UnityEngine.Object.FindObjectsByType<Proto.Feel.Feel>(FindObjectsSortMode.None))
            {
                Assign(f);
                EditorUtility.SetDirty(f);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(f.gameObject.scene);
            }
            Debug.Log("[Feel] 타격감 에셋 생성 완료");
        }

        /// <summary>씬 빌더도 이걸 부른다.</summary>
        public static void Assign(Proto.Feel.Feel f)
        {
            var so = new SerializedObject(f);
            so.FindProperty("sparkMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(SparkMatPath);
            so.FindProperty("damageFont").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            so.FindProperty("swingClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(SwingPath);
            so.FindProperty("hitClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(HitPath);
            so.FindProperty("killClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(KillPath);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ── 스파크 ──

        static void BuildSparkTexture()
        {
            const int N = 64;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                // 밝은 심 + 부드러운 번짐
                float a = Mathf.Clamp01(1f - d);
                a = Mathf.Pow(a, 1.8f) + Mathf.Pow(Mathf.Clamp01(1f - d * 2.2f), 2f) * 0.6f;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(a)));
            }
            File.WriteAllBytes(SparkTexPath, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(SparkTexPath);
            var ti = (TextureImporter)AssetImporter.GetAtPath(SparkTexPath);
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.SaveAndReimport();
        }

        static void BuildSparkMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(SparkMatPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, SparkMatPath);
            }
            mat.shader = shader;
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(SparkTexPath));
            mat.SetColor("_BaseColor", Color.white);

            // 가산 블렌딩 투명 — 겹칠수록 밝아진다
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 2f);   // Additive
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_ColorMode", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            mat.SetOverrideTag("RenderType", "Transparent");
            EditorUtility.SetDirty(mat);
        }

        // ── 효과음 합성 ──

        /// <summary>휘두르는 바람 소리 — 필터 컷오프가 올라갔다 내려가는 노이즈.</summary>
        static float[] Swing()
        {
            float dur = 0.2f;
            int n = (int)(dur * Rate);
            var s = new float[n];
            var rng = new System.Random(7);
            float lp = 0f, lp2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float noise = (float)(rng.NextDouble() * 2 - 1);
                float cutoff = Mathf.Lerp(0.03f, 0.28f, Mathf.Sin(t * Mathf.PI));   // 휙 — 가운데서 가장 밝다
                lp += (noise - lp) * cutoff;
                lp2 += (lp - lp2) * cutoff;
                float band = lp - lp2 * 0.6f;
                float env = Mathf.Pow(Mathf.Sin(t * Mathf.PI), 1.6f) * (1f - t * 0.4f);
                s[i] = band * env * 2.4f;
            }
            return Normalize(s, 0.7f);
        }

        /// <summary>
        /// 둔탁한 타격음 — 아래로 떨어지는 사인(몸통) + 짧은 노이즈 클릭(접촉).
        /// 처치음은 같은 구조에 더 낮고 길게.
        /// </summary>
        static float[] Hit(float dur, float f0, float f1, float weight)
        {
            int n = (int)(dur * Rate);
            var s = new float[n];
            var rng = new System.Random(11);
            double phase = 0;
            float hp = 0f, prev = 0f;
            for (int i = 0; i < n; i++)
            {
                float time = (float)i / Rate;
                float t = (float)i / n;

                float freq = Mathf.Lerp(f1, f0, Mathf.Exp(-time * 28f));
                phase += 2 * Math.PI * freq / Rate;
                float body = (float)Math.Sin(phase) * Mathf.Exp(-time * (18f / weight));

                float noise = (float)(rng.NextDouble() * 2 - 1);
                hp = noise - prev; prev = noise;                             // 고역 통과 = 날카로운 접촉음
                float click = hp * Mathf.Exp(-time * 120f) * 0.9f;
                float crunch = noise * Mathf.Exp(-time * 35f) * 0.35f * weight;

                float v = body * 1.1f + click + crunch;
                v = (float)Math.Tanh(v * 1.8f);                              // 살짝 찌그러뜨려 단단하게
                s[i] = v * (1f - Mathf.Pow(t, 4f));
            }
            return Normalize(s, 0.9f);
        }

        static float[] Normalize(float[] s, float peak)
        {
            float m = 0f;
            foreach (var v in s) m = Mathf.Max(m, Mathf.Abs(v));
            if (m > 0f) for (int i = 0; i < s.Length; i++) s[i] = s[i] / m * peak;
            return s;
        }

        static void WriteWav(string path, float[] samples)
        {
            using (var fs = new FileStream(path, FileMode.Create))
            using (var w = new BinaryWriter(fs))
            {
                int bytes = samples.Length * 2;
                w.Write(new[] { 'R', 'I', 'F', 'F' });
                w.Write(36 + bytes);
                w.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
                w.Write(16); w.Write((short)1); w.Write((short)1);
                w.Write(Rate); w.Write(Rate * 2); w.Write((short)2); w.Write((short)16);
                w.Write(new[] { 'd', 'a', 't', 'a' });
                w.Write(bytes);
                foreach (var v in samples) w.Write((short)(Mathf.Clamp(v, -1f, 1f) * 32767));
            }
            AssetDatabase.ImportAsset(path);
        }
    }
}
