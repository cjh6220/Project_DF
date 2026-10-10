#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Proto.EditorTools
{
    /// <summary>
    /// 장비 빛기둥 그림 → 가산 합성 머티리얼. 메뉴: Proto ▸ Build Loot Beam FX
    ///
    ///   Assets/Texture/Effect/loot_beam.png         세로 빛기둥 (아래가 밝고 위로 사라짐)
    ///   Assets/Texture/Effect/loot_beam_streak.png  기둥 안쪽의 가는 빛줄기
    ///   Assets/Texture/Effect/loot_glow.png         둥근 빛
    ///   Assets/Texture/Effect/loot_sparkle.png      네 갈래 반짝이
    ///   Assets/Texture/Effect/loot_ring.png         바닥 마법진 (위에서 본 모양)
    ///   Assets/Texture/Effect/loot_flare.png        가로 섬광
    ///
    /// 전부 검은 바탕에 흰 그림 — 검정이 투명이 되고, 색은 등급 색으로 입힌다.
    /// 그림을 바꾸면 같은 이름으로 덮어쓰고 이 메뉴를 한 번 더 누른다.
    /// </summary>
    public static class LootFxBuilder
    {
        const string Dir = "Assets/_Project/Resources/Fx/Loot/";      // 머티리얼 (런타임이 Resources로 찾는다)
        const string TexDir = "Assets/Texture/Effect/";               // 그림 (GPT로 뽑은 원본)

        [MenuItem("Proto/Build Loot Beam FX")]
        public static void Build()
        {
            Mat("Mat_Beam", "loot_beam", TextureWrapMode.Clamp);
            Mat("Mat_Streak", "loot_beam_streak", TextureWrapMode.Clamp);
            Mat("Mat_Glow", "loot_glow", TextureWrapMode.Clamp);
            Mat("Mat_Sparkle", "loot_sparkle", TextureWrapMode.Clamp);
            Mat("Mat_Ring", "loot_ring", TextureWrapMode.Clamp);
            Mat("Mat_Flare", "loot_flare", TextureWrapMode.Clamp);
            // 아주 밝은 판 — 기둥 심지 · 솟구침 · 바닥 불꽃. 블룸 문턱(1)을 크게 넘어 번진다
            Mat("Mat_BeamHot", "loot_beam", TextureWrapMode.Clamp, 6f);
            Mat("Mat_GlowHot", "loot_glow", TextureWrapMode.Clamp, 5f);
            AssetDatabase.SaveAssets();
            Debug.Log("[Proto] 장비 빛기둥 머티리얼 생성 완료");
        }

        static void Mat(string matName, string texName, TextureWrapMode wrap, float bright = 2.6f)
        {
            string texPath = TexDir + texName + ".png";
            var imp = AssetImporter.GetAtPath(texPath) as TextureImporter;
            if (imp == null) { Debug.LogWarning("[Proto] 빛기둥 그림 없음: " + texPath); return; }
            imp.textureType = TextureImporterType.Default;
            imp.alphaSource = TextureImporterAlphaSource.FromGrayScale;   // 검은 바탕 = 투명
            imp.alphaIsTransparency = true;
            imp.wrapMode = wrap;
            imp.mipmapEnabled = true;
            imp.SaveAndReimport();
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);

            string path = Dir + matName + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", new Color(bright, bright, bright, 1f));   // 가산 합성 — 밝은 낮 숲에서도 보이게
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 2f);   // Additive
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Cull", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 20;
            m.SetOverrideTag("RenderType", "Transparent");
            EditorUtility.SetDirty(m);
        }
    }
}
#endif
