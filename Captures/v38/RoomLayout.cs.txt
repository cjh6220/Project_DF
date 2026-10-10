using System.Collections.Generic;
using UnityEngine;

namespace Proto.Dungeon
{
    /// <summary>
    /// 방 배치 하나 (프리팹). 위치: Resources/RoomLayouts/  — 이 폴더에 넣은 프리팹은 저절로 쓰인다.
    ///
    ///   방 종류(kind)가 같은 배치 중 하나를 (판 시드 + 방 좌표)로 고른다 — 같은 방에 다시 오면 같은 배치.
    ///   배치는 던전과 상관없이 공용이다. 같은 자리에 숲이면 나무, 폐광이면 바위가 놓인다.
    ///   한 던전 전용 배치를 만들려면 onlyDungeon에 던전 id(forest · mine · snow · rift)를 적는다.
    ///
    /// 씬 화면: 흰 사각형 = 싸우는 칸(18×10m), 노란 띠 = 출구 자리. 출구 띠 안에는 장애물을 두지 말 것.
    /// 테두리 나무 · 방 밖 장식은 지금처럼 테마가 자동으로 두른다 — 배치는 칸 안만 정한다.
    /// </summary>
    public class RoomLayout : MonoBehaviour
    {
        public RoomKind kind = RoomKind.Normal;
        [Tooltip("비워 두면 모든 던전에서 쓴다. 던전 id를 적으면 그 던전에서만")]
        public string onlyDungeon = "";
        [Tooltip("이 배치가 뽑힐 비중")]
        [Range(0.1f, 5f)] public float weight = 1f;

        public static readonly Vector2 Arena = new Vector2(18f, 10f);
        public const float ExitHalf = 3.2f;

        public List<LayoutSlot> Slots(SlotKind k)
        {
            var list = new List<LayoutSlot>();
            foreach (var s in GetComponentsInChildren<LayoutSlot>(true)) if (s.kind == k) list.Add(s);
            return list;
        }

        // ───────────────────────────── 고르기 ─────────────────────────────

        static RoomLayout[] _all;

        public static RoomLayout Pick(RoomKind kind, string dungeonId, int runSeed, Vector2Int cell)
        {
            if (_all == null) _all = Resources.LoadAll<RoomLayout>("RoomLayouts");
            var pool = new List<RoomLayout>();
            float total = 0f;
            foreach (var l in _all)
            {
                if (l == null || l.kind != kind) continue;
                if (!string.IsNullOrEmpty(l.onlyDungeon) && l.onlyDungeon != dungeonId) continue;
                pool.Add(l); total += l.weight;
            }
            if (pool.Count == 0) return null;
            var rng = new System.Random(runSeed * 92821 ^ cell.x * 68917 ^ cell.y * 31337 ^ (int)kind * 7919);
            float r = (float)rng.NextDouble() * total;
            foreach (var l in pool) { r -= l.weight; if (r <= 0f) return l; }
            return pool[pool.Count - 1];
        }

        /// <summary>에디터에서 프리팹을 고쳤을 때 다시 읽게.</summary>
        public static void Reload() => _all = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => _all = null;

        // ───────────────────────────── 씬 화면 표시 ─────────────────────────────

        void OnDrawGizmos()
        {
            var o = transform.position;
            float hx = Arena.x * 0.5f, hz = Arena.y * 0.5f;
            Gizmos.color = new Color(1f, 1f, 1f, 0.8f);
            Gizmos.DrawWireCube(o, new Vector3(Arena.x, 0.02f, Arena.y));
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.35f);
            Gizmos.DrawCube(o + new Vector3(0f, 0f, hz - 0.6f), new Vector3(ExitHalf * 2f, 0.05f, 1.2f));   // 북
            Gizmos.DrawCube(o + new Vector3(0f, 0f, -hz + 0.6f), new Vector3(ExitHalf * 2f, 0.05f, 1.2f));  // 남
            Gizmos.DrawCube(o + new Vector3(hx - 0.6f, 0f, 0f), new Vector3(1.2f, 0.05f, ExitHalf * 2f));   // 동
            Gizmos.DrawCube(o + new Vector3(-hx + 0.6f, 0f, 0f), new Vector3(1.2f, 0.05f, ExitHalf * 2f));  // 서
        }
    }
}
