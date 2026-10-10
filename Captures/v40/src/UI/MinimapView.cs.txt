using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Proto.Core;
using Proto.Dungeon;

namespace Proto.UI
{
    /// <summary>
    /// 던파식 격자 미니맵.
    ///
    /// 정보 공개는 4단계로 판다. 각 단계가 게임의 성격을 바꾼다.
    ///
    ///   기본        보스방 위치 모름        → 더듬어 캐고 나오는 "채집 게임"
    ///   보스 감지   위치가 보인다           → "저기까지 갈 수 있나"가 시작되는 "돌파 게임"
    ///   경로 예측   최단 경로가 보인다      → 계산이 가능해진다
    ///   지도        전부 보인다             → 최적화 게임이 된다
    ///
    /// 보스 감지 이전에 우연히 보스방을 발견하는 순간이 생기는데,
    /// 맵이 매번 랜덤이라 다음 런엔 또 없다. 그 답답함이 노드를 찍고 싶게 만든다.
    /// </summary>
    public class MinimapView : MonoBehaviour
    {
        [SerializeField] RunManager run;
        [SerializeField] RectTransform grid;
        [SerializeField] GameObject cellPrefab;
        [SerializeField] float cellSize = 22f;
        [SerializeField] float cellGap = 3f;

        [Header("해금 정보 — 탐험 트리 노드로 켜진다")]
        [SerializeField] bool showBossRoom;       // "보스 감지" 노드 — 이게 게임의 성격을 바꾼다
        [SerializeField] bool showPathToBoss;     // "경로 예측" 노드
        [SerializeField] bool revealAll;          // "지도" 노드

        /// <summary>보스방 위치를 아는가 ("보스 감지" 노드 또는 디버그 토글).</summary>
        public bool BossRoomKnown =>
            showBossRoom || run.Progression.Level(NodeId.BossSense) > 0;

        /// <summary>아직 해금은 안 했지만 우연히 도달해 본 적이 있는가.</summary>
        bool _bossDiscovered;

        static readonly Color ColUnknown = new Color(1f, 1f, 1f, 0.05f);
        static readonly Color ColKnown = new Color(0.62f, 0.68f, 0.80f, 0.45f);
        static readonly Color ColVisited = new Color(0.90f, 0.82f, 0.45f, 0.95f);
        static readonly Color ColCurrent = new Color(0.30f, 0.95f, 0.80f, 1f);
        static readonly Color ColBoss = new Color(0.95f, 0.30f, 0.30f, 1f);
        static readonly Color ColPath = new Color(0.45f, 0.75f, 1f, 0.85f);

        readonly Dictionary<Vector2Int, Image> _cells = new();

        void Start()
        {
            run.MapChanged += Rebuild;
            run.RunStarted += BuildGrid;
            BuildGrid();
        }

        void BuildGrid()
        {
            // 맵이 매번 랜덤이므로 "우연히 발견한 보스방"은 판이 바뀌면 사라진다
            _bossDiscovered = false;

            foreach (Transform t in grid) Destroy(t.gameObject);
            _cells.Clear();

            var map = run.Map;
            if (map == null) return;

            for (int x = 0; x < map.Width; x++)
            for (int y = 0; y < map.Height; y++)
            {
                var go = Instantiate(cellPrefab, grid);
                go.SetActive(true);
                var rt = (RectTransform)go.transform;
                rt.sizeDelta = new Vector2(cellSize, cellSize);
                rt.anchoredPosition = new Vector2(
                    x * (cellSize + cellGap),
                    y * (cellSize + cellGap));
                _cells[new Vector2Int(x, y)] = go.GetComponent<Image>();
            }
            Rebuild();
        }

        void Rebuild()
        {
            var map = run.Map;
            if (map == null) return;

            // 우연히 보스방에 도달했다면 그 판 동안은 계속 보인다
            if (map.At(map.BossCell) != null && map.At(map.BossCell).Visited)
                _bossDiscovered = true;

            // '보스 감지' 노드를 찍었으면 판이 시작하자마자 미니맵에 보스방이 빨갛게 찍힌다.
            // 거리 숫자가 아니라 위치 자체를 보여준다 — 길은 플레이어가 미니맵을 보고 판단한다.
            bool bossVisible = BossRoomKnown || _bossDiscovered;

            HashSet<Vector2Int> path = null;
            if (showPathToBoss && bossVisible)
            {
                path = new HashSet<Vector2Int>(map.ShortestPathToBoss());
            }

            foreach (var kv in _cells)
            {
                var cell = kv.Key;
                var img = kv.Value;
                var roomData = map.At(cell);

                if (roomData == null) { img.color = ColUnknown; continue; }

                if (cell == map.CurrentCell) { img.color = ColCurrent; continue; }

                // 보스방은 "보스 감지"를 찍었거나 직접 가 봤을 때만 드러난다
                if (cell == map.BossCell && bossVisible) { img.color = ColBoss; continue; }

                if (roomData.Visited) { img.color = ColVisited; continue; }

                if (path != null && path.Contains(cell)) { img.color = ColPath; continue; }

                // 방문한 방과 이어져 있으면 윤곽만 드러난다
                bool adjacentToVisited = false;
                foreach (var d in DungeonMap.AllDirs)
                {
                    if ((roomData.Exits & d) == 0) continue;
                    var n = map.At(cell + DungeonMap.Step(d));
                    if (n != null && n.Visited) { adjacentToVisited = true; break; }
                }

                img.color = (revealAll || adjacentToVisited) ? ColKnown : ColUnknown;
            }
        }

        // 탐험 트리 노드가 해금되면 호출
        public void UnlockBossDetection() { showBossRoom = true; Rebuild(); }
        public void UnlockPathPrediction() { showPathToBoss = true; Rebuild(); }
        public void UnlockFullMap() { revealAll = true; Rebuild(); }

        public void ResetForNewRun() { _bossDiscovered = false; }
    }
}
