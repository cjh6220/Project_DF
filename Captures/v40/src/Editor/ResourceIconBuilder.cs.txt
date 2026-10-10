using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;
using TMPro;

namespace Proto.EditorTools
{
    /// <summary>
    /// 재화 아이콘 6종을 TextMeshPro 스프라이트 에셋으로 묶는다.
    /// 그러면 모든 글자 안에 &lt;sprite name="ore"&gt; 처럼 아이콘을 끼워 넣을 수 있다.
    /// 기본 스프라이트 에셋으로 등록하므로 텍스트마다 따로 설정할 필요가 없다.
    ///
    /// 원본: Assets/_Project/Resources/Icons/res_*.png (128x128)
    /// </summary>
    public static class ResourceIconBuilder
    {
        static readonly string[] Names = { "gold", "ore", "crystal", "alien", "essence", "core" };
        const string SrcDir = "Assets/_Project/Resources/Icons";
        const string AtlasPath = "Assets/_Project/Sprites/ResourceIcons.png";
        const string AssetPath = "Assets/_Project/Resources/Sprite Assets/ResourceIcons.asset";
        const int Cell = 128, Cols = 3, Rows = 2;

        [MenuItem("Proto/Build Resource Icons")]
        public static void Build()
        {
            // 1) 아틀라스 한 장으로 합친다
            var atlas = new Texture2D(Cell * Cols, Cell * Rows, TextureFormat.RGBA32, false);
            atlas.SetPixels32(new Color32[atlas.width * atlas.height]);
            for (int i = 0; i < Names.Length; i++)
            {
                var bytes = File.ReadAllBytes($"{SrcDir}/res_{Names[i]}.png");
                var src = new Texture2D(2, 2);
                src.LoadImage(bytes);
                int x = (i % Cols) * Cell, y = (Rows - 1 - i / Cols) * Cell;
                var px = src.width == Cell && src.height == Cell ? src.GetPixels() : Scale(src, Cell);
                atlas.SetPixels(x, y, Cell, Cell, px);
                Object.DestroyImmediate(src);
            }
            atlas.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(AtlasPath));
            File.WriteAllBytes(AtlasPath, atlas.EncodeToPNG());
            Object.DestroyImmediate(atlas);
            AssetDatabase.ImportAsset(AtlasPath);
            var ti = (TextureImporter)AssetImporter.GetAtPath(AtlasPath);
            ti.textureType = TextureImporterType.Default;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.isReadable = false;
            ti.npotScale = TextureImporterNPOTScale.None;   // 384x256 그대로 — 늘이면 아이콘 칸이 어긋난다
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.SaveAndReimport();
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);

            // 2) 스프라이트 에셋
            Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
            AssetDatabase.DeleteAsset(AssetPath);
            var sa = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
            sa.name = "ResourceIcons";
            // 버전이 비어 있으면 TMP가 옛 형식으로 보고 변환하려다 실패한다
            typeof(TMP_Asset).GetField("m_Version", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(sa, "1.1.0");
            sa.spriteSheet = tex;

            // 글자 줄 높이에 맞춰 아이콘이 글자 크기만큼 보이게 한다
            var face = new FaceInfo();
            face.pointSize = Cell;
            face.scale = 1f;
            face.lineHeight = Cell;
            face.ascentLine = Cell * 0.85f;
            face.descentLine = -Cell * 0.15f;
            face.baseline = 0f;
            sa.faceInfo = face;

            for (int i = 0; i < Names.Length; i++)
            {
                int x = (i % Cols) * Cell, y = (Rows - 1 - i / Cols) * Cell;
                var glyph = new TMP_SpriteGlyph((uint)i,
                    new GlyphMetrics(Cell, Cell, 0f, Cell * 0.85f, Cell * 1.05f),
                    new GlyphRect(x, y, Cell, Cell), 1f, 0);
                sa.spriteGlyphTable.Add(glyph);
                var ch = new TMP_SpriteCharacter(0xFFFE, glyph) { name = Names[i] };
                sa.spriteCharacterTable.Add(ch);
            }

            var mat = new Material(Shader.Find("TextMeshPro/Sprite")) { name = "ResourceIcons Material" };
            mat.SetTexture(ShaderUtilities.ID_MainTex, tex);
            sa.material = mat;

            AssetDatabase.CreateAsset(sa, AssetPath);
            AssetDatabase.AddObjectToAsset(mat, sa);
            sa.UpdateLookupTables();
            EditorUtility.SetDirty(sa);

            // 3) 기본 스프라이트 에셋으로 등록
            var so = new SerializedObject(TMP_Settings.instance);
            so.FindProperty("m_defaultSpriteAsset").objectReferenceValue = sa;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(TMP_Settings.instance);
            AssetDatabase.SaveAssets();
            Debug.Log("[Icons] 재화 아이콘 스프라이트 에셋 생성 완료");
        }

        static Color[] Scale(Texture2D src, int size)
        {
            var rt = RenderTexture.GetTemporary(size, size);
            Graphics.Blit(src, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            t.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            t.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            var px = t.GetPixels();
            Object.DestroyImmediate(t);
            return px;
        }
    }
}
