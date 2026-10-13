#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Proto.EditorTools
{
    /// <summary>
    /// 스킬 테스트 씬 만들기. 메뉴: Proto ▸ Build Skill Test Scene
    /// Main 씬을 Assets/Scenes/SkillTest.unity 로 복사하고 SkillTest 오브젝트(SkillTestMode)를 붙인다.
    /// Main을 고친 뒤 테스트 씬도 맞추고 싶으면 이 메뉴를 다시 누른다 (덮어쓴다).
    /// </summary>
    public static class SkillTestSceneBuilder
    {
        const string Src = "Assets/Scenes/Main.unity";
        const string Dst = "Assets/Scenes/SkillTest.unity";

        [MenuItem("Proto/Build Skill Test Scene")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[SkillTest] 플레이 중에는 만들 수 없습니다"); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string back = EditorSceneManager.GetActiveScene().path;

            AssetDatabase.DeleteAsset(Dst);
            if (!AssetDatabase.CopyAsset(Src, Dst)) { Debug.LogWarning("[SkillTest] 복사 실패"); return; }

            var scene = EditorSceneManager.OpenScene(Dst, OpenSceneMode.Single);
            var go = new GameObject("SkillTest");
            var mode = go.AddComponent<Proto.Dev.SkillTestMode>();

            var list = new List<Proto.Data.DungeonDef>();
            foreach (var g in AssetDatabase.FindAssets("t:DungeonDef"))
            {
                var d = AssetDatabase.LoadAssetAtPath<Proto.Data.DungeonDef>(AssetDatabase.GUIDToAssetPath(g));
                if (d != null) list.Add(d);
            }
            list.Sort((a, b) => a.danger.CompareTo(b.danger));
            var so = new SerializedObject(mode);
            var arr = so.FindProperty("dungeons");
            arr.arraySize = list.Count;
            for (int i = 0; i < list.Count; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (!string.IsNullOrEmpty(back) && back != Dst) EditorSceneManager.OpenScene(back, OpenSceneMode.Single);
            Debug.Log($"[SkillTest] {Dst} 생성 — 던전 {list.Count}개. 이 씬을 열고 Play.");
        }
    }
}
#endif
