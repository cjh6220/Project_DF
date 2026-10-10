#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;
using TMPro;

namespace Proto.EditorTools
{
    /// <summary>
    /// 버튼 아이콘 → TMP 스프라이트 묶음. 메뉴: Proto ▸ Build Button Icons
    ///
    ///   Textures/UI/ButtonIcons.png + ButtonIcons.json(조각 위치) → Resources/Sprite Assets/ButtonIcons.asset
    ///   글자 사이에 <sprite="ButtonIcons" name="ps_cross"> 로 넣는다 (InputGlyphs가 만든다).
    ///   장치 그림(Resources/UI/Device_*.png)은 UI 스프라이트로 가져온다.
    /// </summary>
    public static class ButtonIconBuilder
    {
        const string Atlas = "Assets/_Project/Textures/UI/ButtonIcons.png";
        const string Meta = "Assets/_Project/Textures/UI/ButtonIcons.json";
        const string OutDir = "Assets/_Project/Resources/Sprite Assets/";
        const string Out = OutDir + "ButtonIcons.asset";

        [System.Serializable] class Rect { public string name; public int x, y, w, h; }
        [System.Serializable] class Sheet { public int w, h; public List<Rect> sprites; }

        [MenuItem("Proto/Build Button Icons")]
        public static void Build()
        {
            var imp = (TextureImporter)AssetImporter.GetAtPath(Atlas);
            imp.textureType = TextureImporterType.Default;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.filterMode = FilterMode.Bilinear;
            imp.npotScale = TextureImporterNPOTScale.None;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Atlas);
            var sheet = JsonUtility.FromJson<Sheet>(System.IO.File.ReadAllText(Meta));

            System.IO.Directory.CreateDirectory(OutDir);
            var sa = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(Out);
            if (sa == null)
            {
                sa = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
                AssetDatabase.CreateAsset(sa, Out);
            }
            // 새로 만든 묶음은 버전이 비어 있어 TMP가 옛 형식 변환을 시도하다 터진다 — 버전을 적어 두고 옛 목록은 빈 것으로
            if (sa.spriteInfoList == null) sa.spriteInfoList = new List<TMP_Sprite>();
            var so = new SerializedObject(sa);
            var ver = so.FindProperty("m_Version");
            if (ver != null && string.IsNullOrEmpty(ver.stringValue)) { ver.stringValue = "1.1.0"; so.ApplyModifiedPropertiesWithoutUndo(); }
            sa.spriteSheet = tex;

            if (sa.material == null)
            {
                var mat = new Material(Shader.Find("TextMeshPro/Sprite")) { name = "ButtonIcons Material" };
                AssetDatabase.AddObjectToAsset(mat, sa);
                sa.material = mat;
            }
            sa.material.SetTexture(ShaderUtilities.ID_MainTex, tex);

            sa.spriteGlyphTable.Clear();
            sa.spriteCharacterTable.Clear();
            uint i = 0;
            foreach (var r in sheet.sprites)
            {
                // 글자 높이(어센트)에 맞춰 그려진다. 살짝 아래로 내려 글자 가운데에 오게
                var metrics = new GlyphMetrics(r.w, r.h, 0f, r.h * 0.86f, r.w * 1.06f);
                var glyph = new TMP_SpriteGlyph(i, metrics, new GlyphRect(r.x, r.y, r.w, r.h), 1.15f, 0);
                sa.spriteGlyphTable.Add(glyph);
                var ch = new TMP_SpriteCharacter(0xFFFE, sa, glyph) { name = r.name };
                sa.spriteCharacterTable.Add(ch);
                i++;
            }
            sa.UpdateLookupTables();
            EditorUtility.SetDirty(sa);

            foreach (var n in new[] { "Device_PS", "Device_Xbox", "Device_Handheld", "Device_Keyboard" })
            {
                var p = "Assets/_Project/Resources/UI/" + n + ".png";
                var ti = AssetImporter.GetAtPath(p) as TextureImporter;
                if (ti == null) continue;
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.alphaIsTransparency = true;
                ti.mipmapEnabled = false;
                ti.SaveAndReimport();
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[Proto] 버튼 아이콘 {sheet.sprites.Count}개 — {Out}");
        }
    }
}
#endif
