#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Proto.Dungeon;

namespace Proto.EditorTools
{
    /// <summary>
    /// 방 배치 프리팹 12종을 만든다. 메뉴: Proto ▸ Room Layouts ▸ Create Missing
    ///
    ///   입구 1 · 일반 6 · 엘리트 2 · 보물 2 · 보스 1 → Assets/_Project/Resources/RoomLayouts/
    ///   이미 있는 프리팹은 건드리지 않는다 — 고친 배치가 지워지지 않게. 처음 상태로 되돌리려면 그 프리팹을 지우고 다시 실행.
    ///
    /// 좌표: 싸우는 칸 18×10m, 가운데 (0,0). x 왼쪽 -9 ~ 오른쪽 +9 · z 아래(카메라 쪽) -5 ~ 위 +5.
    /// 출구 자리(각 변 가운데 폭 6.4m)는 비워 둔다.
    /// </summary>
    public static class RoomLayoutBuilder
    {
        const string Dir = "Assets/_Project/Resources/RoomLayouts/";

        [MenuItem("Proto/Room Layouts/Create Missing")]
        public static void CreateMissing() => Build(false);

        [MenuItem("Proto/Room Layouts/Recreate All (지금 배치를 덮어씀)")]
        static void RecreateAll()
        {
            if (EditorUtility.DisplayDialog("방 배치 다시 만들기", "고친 배치가 모두 처음 상태로 돌아갑니다.", "다시 만들기", "취소"))
                Build(true);
        }

        static GameObject _root;

        static void Build(bool overwrite)
        {
            System.IO.Directory.CreateDirectory(Dir);
            int made = 0;

            if (Begin("Entry_01", RoomKind.Entry, overwrite))
            {
                O(-6f, 3f, 1.1f); O(6.5f, 2.6f, 0.8f);
                C(-5f, -2f, 2.2f, 8); C(5f, -1.6f, 1.8f, 6); C(0f, 1.6f, 1.5f, 4);
                R(-6.6f, -0.6f);
                made += End();
            }
            if (Begin("Normal_01_OpenField", RoomKind.Normal, overwrite))   // 열린 들판 — 장애물 둘
            {
                O(-5f, 2.6f, 1.2f); O(5.5f, -2f, 1.0f);
                C(-2f, -2.2f, 2f, 8); C(3f, 2.6f, 2f, 7);
                E(-4f, 0f); E(-1f, 2f); E(2f, -1f); E(5f, 1.2f); E(0f, -3f); E(6.2f, 3.2f);
                R(-6.6f, -3.2f); R(6.6f, 3.6f); R(-1.5f, 3.6f);
                made += End();
            }
            if (Begin("Normal_02_FourPillars", RoomKind.Normal, overwrite))  // 기둥 넷 — 사이로 돌며 싸운다
            {
                O(-4.5f, 2.2f, 0.9f); O(4.5f, 2.2f, 0.9f); O(-4.5f, -2.2f, 0.9f); O(4.5f, -2.2f, 0.9f);
                C(0f, 0f, 1.5f, 5); C(-7f, 3.6f, 1.5f, 5); C(7f, -3.6f, 1.5f, 5);
                E(0f, 2.5f); E(0f, -2.5f); E(-6.2f, 0f); E(6.2f, 0f); E(-2.4f, 0f); E(2.4f, 0f);
                R(-7f, -3.6f); R(7f, 3.6f); R(0f, 0f);
                made += End();
            }
            if (Begin("Normal_03_CenterRock", RoomKind.Normal, overwrite))   // 가운데 큰 바위 — 돌아서 간다
            {
                O(0f, 0.5f, 1.8f); O(-6.6f, -3.2f, 0.7f); O(6.6f, 3.4f, 0.7f);
                C(-3.6f, 2.6f, 1.8f, 7); C(4f, -2.6f, 2f, 7);
                E(-5f, 1f); E(5f, -1f); E(-3f, -2.6f); E(3f, 2.6f); E(-6.6f, 2.6f); E(7f, 0f);
                R(-1.8f, -2.4f); R(2.2f, 2.9f);
                made += End();
            }
            if (Begin("Normal_04_Diagonal", RoomKind.Normal, overwrite))     // 대각선 바위줄
            {
                O(-6f, 3f, 1.0f); O(-3f, 1.2f, 0.8f); O(2.6f, -1.5f, 0.9f); O(6f, -3f, 1.1f);
                C(-5f, -2.6f, 2f, 8); C(5f, 2.6f, 2f, 8);
                E(-6.6f, -0.6f); E(-1f, -2.6f); E(1f, 2.6f); E(6.6f, 0.6f); E(-3.6f, -3.2f); E(3.6f, 3.2f);
                R(0f, 0f); R(-7.2f, -3.6f);
                made += End();
            }
            if (Begin("Normal_05_NorthRidge", RoomKind.Normal, overwrite))   // 북쪽 바위줄 — 아래가 넓다
            {
                O(-7.5f, 3.5f, 1.1f); O(-5f, 3.8f, 0.9f); O(5f, 3.8f, 0.9f); O(7.5f, 3.5f, 1.1f);
                C(-3f, -2f, 2.2f, 9); C(4f, -2.6f, 1.8f, 6);
                E(-5f, 0.6f); E(-2f, 1.6f); E(2f, 1f); E(5f, 0f); E(0f, -2f); E(-6f, -2.6f);
                R(-7f, 1f); R(7f, 1f);
                made += End();
            }
            if (Begin("Normal_06_Scattered", RoomKind.Normal, overwrite))    // 잔돌 흩뿌림
            {
                O(-6f, 2f, 0.55f); O(-3f, -2.6f, 0.55f); O(-1.4f, 2.8f, 0.5f); O(1.6f, -1f, 0.55f);
                O(4f, 2.6f, 0.55f); O(6.6f, -2.6f, 0.55f); O(-6.6f, -1f, 0.45f);
                C(0f, 0f, 3f, 10);
                E(-4.5f, 0.5f); E(4.5f, -0.5f); E(-2f, 0f); E(2.5f, 1.5f); E(0f, -3f); E(6f, 1f);
                R(-4f, 3.4f); R(4f, -3.4f);
                made += End();
            }
            if (Begin("Elite_01_Ring", RoomKind.Elite, overwrite))           // 원형 투기장 — 가운데가 넓다
            {
                O(-6.8f, 3.4f, 1.0f); O(6.8f, 3.4f, 1.0f); O(-6.8f, -3.4f, 1.0f); O(6.8f, -3.4f, 1.0f);
                C(-4f, 0f, 1.2f, 4); C(4f, 0f, 1.2f, 4);
                E(0f, 2.5f); E(2.6f, 1.4f); E(3f, -1.5f); E(0f, -2.5f); E(-3f, -1.5f); E(-2.6f, 1.4f);
                R(-5f, -3.6f); R(5f, 3.6f);
                made += End();
            }
            if (Begin("Elite_02_TwinWalls", RoomKind.Elite, overwrite))      // 양쪽 바위벽 — 가운데 통로 싸움
            {
                O(-4f, 2.8f, 1.0f); O(-4f, -2.6f, 1.0f); O(4f, 2.8f, 1.0f); O(4f, -2.6f, 1.0f);
                C(0f, 0f, 2f, 8); C(-7f, 3.6f, 1.2f, 4); C(7f, -3.6f, 1.2f, 4);
                E(-1.5f, 1.5f); E(1.5f, -1.5f); E(0f, 0f); E(-6.4f, 0f); E(6.4f, 0f); E(-2f, -2.6f); E(2f, 2.6f);
                R(-6.6f, -3.6f); R(6.6f, 3.6f);
                made += End();
            }
            if (Begin("Treasure_01_OreGarden", RoomKind.Treasure, overwrite)) // 광맥 밭 — 가운데 몰려 있다
            {
                O(-6f, -3f, 0.9f); O(6f, -3f, 0.9f); O(-6.5f, 3f, 0.9f); O(6.5f, 3f, 0.9f);
                C(0f, 0f, 3.5f, 12);
                E(-5f, 0f); E(5f, 0f); E(0f, 2.8f);
                R(-1.5f, 0.8f); R(1.5f, 0.8f); R(0f, -1f); R(-3f, 2f); R(3f, 2f);
                made += End();
            }
            if (Begin("Treasure_02_RockNook", RoomKind.Treasure, overwrite))  // 바위 사이 광맥
            {
                O(-2.6f, 1.5f, 1.2f); O(2.6f, 1.5f, 1.2f); O(-2.6f, -1.8f, 0.8f); O(2.6f, -1.8f, 0.8f);
                C(-6f, 0f, 2f, 8); C(6f, 0f, 2f, 8);
                E(-6f, 2f); E(6f, -2f); E(-6f, -2.6f);
                R(0f, 0f); R(0f, 1.6f); R(0f, -1.6f);
                made += End();
            }
            if (Begin("Boss_01_Arena", RoomKind.Boss, overwrite))            // 보스방 — 넓게 비운다
            {
                O(-7.3f, 3.6f, 1.3f); O(7.3f, 3.6f, 1.3f); O(-7.5f, -3.6f, 0.8f); O(7.5f, -3.6f, 0.8f);
                C(-5f, 3.6f, 1.5f, 5); C(5f, 3.6f, 1.5f, 5);
                made += End();
            }

            AssetDatabase.SaveAssets();
            RoomLayout.Reload();
            Debug.Log($"[Proto] 방 배치 {made}개 만듦 — {Dir}");
        }

        static string _path;

        static bool Begin(string name, RoomKind kind, bool overwrite)
        {
            _path = Dir + name + ".prefab";
            if (!overwrite && AssetDatabase.LoadAssetAtPath<GameObject>(_path) != null) return false;
            _root = new GameObject(name);
            _root.AddComponent<RoomLayout>().kind = kind;
            return true;
        }

        static int End()
        {
            PrefabUtility.SaveAsPrefabAsset(_root, _path);
            Object.DestroyImmediate(_root);
            return 1;
        }

        static LayoutSlot Slot(string name, SlotKind k, float x, float z)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform, false);
            go.transform.localPosition = new Vector3(x, 0f, z);
            var s = go.AddComponent<LayoutSlot>();
            s.kind = k;
            return s;
        }

        static int _n;
        static void O(float x, float z, float scale) { var s = Slot("Obstacle_" + (++_n), SlotKind.Obstacle, x, z); s.scale = scale; s.big = scale >= 0.7f; }
        static void C(float x, float z, float r, int count) { var s = Slot("Clutter_" + (++_n), SlotKind.Clutter, x, z); s.radius = r; s.count = count; }
        static void E(float x, float z) => Slot("Enemy_" + (++_n), SlotKind.Enemy, x, z);
        static void R(float x, float z) => Slot("Ore_" + (++_n), SlotKind.Ore, x, z);
    }
}
#endif
