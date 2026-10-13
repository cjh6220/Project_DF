#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Proto.Feel;

namespace Proto.EditorTools
{
    /// <summary>
    /// SkillVfx 배정표에 쓰인 프리팹 이름을 이펙트 팩 폴더에서 찾아 Resources/Fx/SkillVfxLibrary.asset 에 모은다.
    /// 스크립트를 다시 읽을 때마다 자동으로 (표를 고치면 저절로 반영). 메뉴로도: Proto ▸ Build Skill VFX Library
    /// </summary>
    public static class SkillVfxLibraryBuilder
    {
        const string Path = "Assets/_Project/Resources/Fx/SkillVfxLibrary.asset";
        static readonly string[] Roots = { "Assets/Hovl Studio", "Assets/ARPG Effects" };

        [InitializeOnLoadMethod]
        static void Auto() => EditorApplication.delayCall += () => Build(false);

        [MenuItem("Proto/Build Skill VFX Library")]
        static void Menu() => Build(true);

        static void Build(bool log)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var lib = AssetDatabase.LoadAssetAtPath<SkillVfxLibrary>(Path);
            if (lib == null)
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
                lib = ScriptableObject.CreateInstance<SkillVfxLibrary>();
                AssetDatabase.CreateAsset(lib, Path);
            }
            var names = new List<string>(); var prefabs = new List<GameObject>(); var missing = new List<string>();
            foreach (var n in SkillVfx.AllPrefabNames())
            {
                if (names.Contains(n)) continue;
                GameObject found = null;
                foreach (var g in AssetDatabase.FindAssets("t:Prefab", Roots))
                {
                    var p = AssetDatabase.GUIDToAssetPath(g);
                    if (System.IO.Path.GetFileNameWithoutExtension(p) == n) { found = AssetDatabase.LoadAssetAtPath<GameObject>(p); break; }
                }
                if (found == null) { missing.Add(n); continue; }
                names.Add(n); prefabs.Add(found);
            }
            bool changed = names.Count != lib.Names.Count;
            for (int i = 0; !changed && i < names.Count; i++) changed = names[i] != lib.Names[i] || prefabs[i] != lib.Prefabs[i];
            if (changed)
            {
                lib.Names = names; lib.Prefabs = prefabs;
                EditorUtility.SetDirty(lib);
                AssetDatabase.SaveAssets();
            }
            if (missing.Count > 0) Debug.LogWarning("[Proto] 스킬 이펙트 프리팹을 못 찾음: " + string.Join(", ", missing));
            if (log) Debug.Log("[Proto] 스킬 이펙트 라이브러리 " + names.Count + "개");
        }
    }
}
#endif
