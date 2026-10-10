#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Proto.Town;

namespace Proto.EditorTools
{
    /// <summary>
    /// 장비 시스템 준비. 메뉴: Proto ▸ Items ▸ …   (여러 번 눌러도 안전하다 — 이미 있는 것은 건드리지 않는다)
    ///
    ///   Setup All          아래 셋을 한 번에
    ///   Treasure Chest     Resources/Props/TreasureChest.prefab — 보물방 상자 (polyperfect chest, 뚜껑 = chest-top)
    ///   Icons              Resources/Icons/item_*.png · res_dust.png 를 스프라이트로 + 재화 아이콘 아틀라스 다시 굽기
    ///   Blacksmith NPC     마을에 대장장이(분해 · 제작)와 모루를 세우고 VillageController.npcs에 붙인다
    /// </summary>
    public static class ItemSetupBuilder
    {
        const string ChestSrc = "Assets/polyperfect/Low Poly Ultimate Pack/T/- Prefabs_T/Fantasy_T/chest.prefab";
        const string ChestDst = "Assets/_Project/Resources/Props/TreasureChest.prefab";
        const string AnvilSrc = "Assets/polyperfect/Low Poly Ultimate Pack/T/- Prefabs_T/Tools_T/anvil.prefab";
        const string IconDir = "Assets/_Project/Resources/Icons";

        public static readonly Vector3 SmithPos = new Vector3(4.4f, 0f, -7.4f);
        const float SmithYaw = 210f;

        [MenuItem("Proto/Items/Setup All (상자 · 아이콘 · 대장장이)")]
        public static void SetupAll()
        {
            BuildChest();
            Icons();
            Blacksmith();
        }

        [MenuItem("Proto/Items/Treasure Chest")]
        public static void BuildChest()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ChestDst) != null) { Debug.Log("[Items] 상자 프리팹이 이미 있다"); return; }
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(ChestSrc);
            if (src == null) { Debug.LogWarning("[Items] 상자 원본 없음: " + ChestSrc); return; }
            ThemeOrganizer.EnsureFolder("Assets/_Project/Resources/Props");

            var root = new GameObject("TreasureChest");
            var model = (GameObject)PrefabUtility.InstantiatePrefab(src, root.transform);
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            model.name = "Model";
            model.transform.localScale = Vector3.one * 1.35f;
            foreach (var c in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (var r in model.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

            // 몸으로 막는다 — 상자를 뚫고 지나가면 물건이 아니라 그림이다
            var bc = root.AddComponent<BoxCollider>();
            bc.center = new Vector3(0f, 0.45f, 0f);
            bc.size = new Vector3(1.3f, 0.9f, 1.05f);
            root.AddComponent<Proto.Dungeon.TreasureChest>();

            PrefabUtility.SaveAsPrefabAsset(root, ChestDst);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            Debug.Log("[Items] 보물상자 프리팹 생성 " + ChestDst);
        }

        [MenuItem("Proto/Items/Icons")]
        public static void Icons()
        {
            foreach (var n in new[] { "item_weapon", "item_helmet", "item_armor", "item_gloves", "item_boots", "item_earring", "item_necklace", "res_dust" })
            {
                var path = IconDir + "/" + n + ".png";
                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null) { Debug.LogWarning("[Items] 아이콘 없음: " + path); continue; }
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.alphaIsTransparency = true;
                ti.mipmapEnabled = false;
                ti.isReadable = n == "res_dust";   // 재화 아틀라스는 파일을 직접 읽는다
                ti.SaveAndReimport();
            }
            ResourceIconBuilder.Build();
        }

        [MenuItem("Proto/Items/Blacksmith NPC")]
        public static void Blacksmith()
        {
            var village = Object.FindFirstObjectByType<VillageController>(FindObjectsInactive.Include);
            if (village == null) { Debug.LogWarning("[Items] 씬에 VillageController가 없다"); return; }
            var so = new SerializedObject(village);
            var world = so.FindProperty("world").objectReferenceValue as GameObject;
            if (world == null) { Debug.LogWarning("[Items] 마을 월드가 없다"); return; }

            var npcs = so.FindProperty("npcs");
            for (int i = 0; i < npcs.arraySize; i++)
            {
                var n = npcs.GetArrayElementAtIndex(i).objectReferenceValue as VillageNpc;
                if (n != null && n.Role == NpcRole.Smith) { Debug.Log("[Items] 대장장이가 이미 있다"); return; }
            }

            var w = world.transform;
            var npc = VillageBuilder.Npc(w, "man_carpenter", SmithPos, SmithYaw, NpcRole.Smith,
                                         "장비 분해 · 제작", "대장장이 텟사이", new Color(0.62f, 0.86f, 0.95f), null);
            var nso = new SerializedObject(npc);
            nso.FindProperty("promptVerb").stringValue = "대장간";
            nso.ApplyModifiedPropertiesWithoutUndo();

            // 모루 — 대장장이 옆
            var anvil = VillageBuilder.Place(w, AnvilSrc, SmithPos + new Vector3(-1.3f, 0f, -0.2f), -20f, 0.75f, true);
            if (anvil != null) anvil.name = "Anvil";

            npcs.arraySize++;
            npcs.GetArrayElementAtIndex(npcs.arraySize - 1).objectReferenceValue = npc;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(village);
            EditorSceneManager.MarkSceneDirty(village.gameObject.scene);
            EditorSceneManager.SaveScene(village.gameObject.scene);
            Debug.Log("[Items] 대장장이 NPC 배치 " + SmithPos);
        }
    }
}
#endif
