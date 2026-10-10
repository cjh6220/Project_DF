#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Proto.Data;
using Proto.Dungeon;

namespace Proto.EditorTools
{
    /// <summary>
    /// 테마별 폴더 정리. 메뉴: Proto ▸ Themes ▸ …
    ///
    ///   방 배치   Resources/RoomLayouts/&lt;테마&gt;/      — 테마마다 따로 고칠 수 있다 (onlyDungeon이 그 던전 id로 채워진다)
    ///   장식 모델  Assets/_Project/Decor/&lt;테마&gt;/&lt;종류&gt;/ — 에셋 팩 원본의 프리팹 변형(Variant).
    ///             크기 · 색 · 콜라이더를 여기서 고치면 그 테마에만 들어가고, 원본 팩은 그대로다.
    ///
    ///   종류 폴더 = StageTheme 칸 이름
    ///     BorderTrees 뒷벽 큰 나무 · SideTrees 동서 변 · BorderFill 발치 덤불 · LowBorder 카메라 쪽 낮은 것
    ///     Obstacles 낮은 지형물 · BigObstacles 큰 장애물 · Clutter 잡초 · Door 출구 문
    ///
    /// 장식을 더하려면: 모델을 그 테마의 종류 폴더에 넣고 StageTheme 칸에 끌어 넣는다.
    /// 테마 빌더(Build Stage … Theme)를 다시 돌리면 칸이 팩 원본으로 돌아가니, 그 뒤에 Split Decor를 한 번 더 누른다.
    /// 두 메뉴 모두 여러 번 눌러도 안전하다 — 이미 나뉜 것은 건드리지 않는다.
    /// </summary>
    public static class ThemeOrganizer
    {
        public const string LayoutRoot = "Assets/_Project/Resources/RoomLayouts";
        public const string DecorRoot = "Assets/_Project/Decor";

        /// <summary>던전 id → 폴더 이름. RoomLayoutBuilder도 이 표를 쓴다.</summary>
        public static readonly (string id, string folder)[] Themes =
        {
            ("forest", "Forest"), ("mine", "Mine"), ("snow", "SnowTemple"), ("rift", "Rift"),
        };

        static readonly string[] DecorFields =
        {
            "borderTrees", "sideTrees", "borderFill", "lowBorder", "obstacles", "bigObstacles", "clutter",
        };

        [MenuItem("Proto/Themes/Split All (방 배치 + 장식)")]
        public static void SplitAll()
        {
            SplitLayouts();
            SplitDecor();
        }

        // ───────────────────────────── 방 배치 ─────────────────────────────

        /// <summary>RoomLayouts 바로 아래의 공용 배치를 테마 폴더 넷으로 복사하고 원본을 지운다.</summary>
        [MenuItem("Proto/Themes/Split Room Layouts")]
        public static void SplitLayouts()
        {
            int copied = 0, removed = 0;
            foreach (var t in Themes) EnsureFolder(LayoutRoot + "/" + t.folder);

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { LayoutRoot }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetDirectoryName(path).Replace('\\', '/') != LayoutRoot) continue;   // 이미 테마 폴더 안
                var src = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var layout = src != null ? src.GetComponent<RoomLayout>() : null;
                if (layout == null) continue;

                foreach (var t in Themes)
                {
                    // 한 던전 전용으로 만든 배치는 그 던전 폴더로만 간다
                    if (!string.IsNullOrEmpty(layout.onlyDungeon) && layout.onlyDungeon != t.id) continue;
                    string dst = LayoutRoot + "/" + t.folder + "/" + System.IO.Path.GetFileName(path);
                    if (AssetDatabase.LoadAssetAtPath<GameObject>(dst) != null) continue;
                    if (!AssetDatabase.CopyAsset(path, dst)) { Debug.LogWarning("[Proto] 복사 실패 " + dst); continue; }
                    var root = PrefabUtility.LoadPrefabContents(dst);
                    root.GetComponent<RoomLayout>().onlyDungeon = t.id;
                    PrefabUtility.SaveAsPrefabAsset(root, dst);
                    PrefabUtility.UnloadPrefabContents(root);
                    copied++;
                }
                AssetDatabase.DeleteAsset(path);
                removed++;
            }
            AssetDatabase.SaveAssets();
            RoomLayout.Reload();
            Debug.Log($"[Proto] 방 배치 테마별 정리 — 복사 {copied} · 공용 원본 정리 {removed}");
        }

        // ───────────────────────────── 장식 모델 ─────────────────────────────

        [MenuItem("Proto/Themes/Split Decor Models")]
        public static void SplitDecor()
        {
            var report = new System.Text.StringBuilder("[Proto] 장식 모델 테마별 정리\n");
            foreach (var guid in AssetDatabase.FindAssets("t:DungeonDef"))
            {
                var def = AssetDatabase.LoadAssetAtPath<DungeonDef>(AssetDatabase.GUIDToAssetPath(guid));
                if (def == null || def.theme == null) continue;
                string folder = FolderOf(def.id);
                if (folder == null) continue;
                var theme = def.theme;
                int made = 0, kept = 0;

                foreach (var field in DecorFields)
                {
                    var fi = typeof(StageTheme).GetField(field);
                    var arr = fi.GetValue(theme) as GameObject[];
                    if (arr == null || arr.Length == 0) continue;
                    string dir = DecorRoot + "/" + folder + "/" + Cap(field);
                    for (int i = 0; i < arr.Length; i++)
                    {
                        var r = Variant(arr[i], dir, ref made, ref kept);
                        if (r != null) arr[i] = r;
                    }
                    fi.SetValue(theme, arr);
                }
                if (theme.doorModel != null)
                {
                    var r = Variant(theme.doorModel, DecorRoot + "/" + folder + "/Door", ref made, ref kept);
                    if (r != null) theme.doorModel = r;
                }
                EditorUtility.SetDirty(theme);
                report.AppendLine($"  {folder}: 새 변형 {made} · 이미 정리됨 {kept}");
            }
            AssetDatabase.SaveAssets();
            Debug.Log(report.ToString());
        }

        /// <summary>원본의 프리팹 변형을 dir에 만든다. 이미 Decor 안의 것이면 그대로 돌려준다.</summary>
        static GameObject Variant(GameObject src, string dir, ref int made, ref int kept)
        {
            if (src == null) return null;
            string srcPath = AssetDatabase.GetAssetPath(src);
            if (srcPath.StartsWith(DecorRoot + "/")) { kept++; return src; }

            EnsureFolder(dir);
            string dst = dir + "/" + src.name + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(dst);
            if (existing != null) { kept++; return existing; }   // 같은 칸에 같은 모델이 두 번 — 하나만 만든다

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(src);
            if (inst == null) inst = Object.Instantiate(src);
            inst.name = src.name;
            var saved = PrefabUtility.SaveAsPrefabAsset(inst, dst);
            Object.DestroyImmediate(inst);
            if (saved != null) made++;
            return saved;
        }

        public static string FolderOf(string id)
        {
            foreach (var t in Themes) if (t.id == id) return t.folder;
            return null;
        }

        static string Cap(string s) => char.ToUpperInvariant(s[0]) + s.Substring(1);

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
#endif
