using System;
using System.Collections.Generic;
using UnityEngine;
using Proto.Data;

namespace Proto.Dungeon
{
    public enum RoomKind { Normal, Entry, Boss, Elite, Treasure }

    [Flags]
    public enum Dir { None = 0, N = 1, E = 2, S = 4, W = 8 }

    /// <summary>
    /// 방 안 광맥 하나의 상태. 위치까지 기억해 두는 이유는 두 가지다.
    ///   1. 캐고 나간 광맥이 돌아왔을 때 되살아나면 무한 파밍이 된다
    ///   2. 안 캔 광맥이 다른 자리로 옮겨가면 "저기 결정 있었지"가 성립하지 않는다
    /// </summary>
    public class OreSpot
    {
        public Vector3 LocalPos;
        public Proto.Mining.OreGrade Grade;
        public int Amount;
        public bool Mined;
    }

    public class Room
    {
        public Vector2Int Cell;
        public RoomKind Kind = RoomKind.Normal;
        public Dir Exits = Dir.None;

        /// <summary>본 경로(입구→보스) 위에 있는 방인가. 가지 방에 광맥을 몰아주기 위해 쓴다.</summary>
        public bool OnMainPath;

        public bool Visited;
        public bool Cleared;
        public int OreCount;
        public int EnemyCount;
        /// <summary>
        /// 이 방에 붙을 챔피언 접두사 목록. 비어 있으면 일반 몬스터만 나온다.
        /// 챔피언은 EnemyCount에 포함되지 않는 추가 몬스터다.
        /// </summary>
        public System.Collections.Generic.List<Proto.Enemy.Affix> Champions = new();

        /// <summary>
        /// 광맥 배치. null이면 아직 이 방에 처음 들어온 것이고, 그때 한 번 만든다.
        /// </summary>
        public System.Collections.Generic.List<OreSpot> Ores;

        /// <summary>아직 캘 게 남았는가.</summary>
        public bool HasOreLeft
        {
            get
            {
                if (Ores == null) return OreCount > 0;
                foreach (var o in Ores) if (!o.Mined) return true;
                return false;
            }
        }
    }

    /// <summary>
    /// 격자 맵. 노드 그래프가 아니라 격자를 쓰는 이유는 거리가 칸으로 읽히기 때문이다.
    /// "보스방까지 7칸 → 내 피로도로 갈 수 있나"가 즉시 계산된다.
    ///
    /// 정보 공개 규칙:
    ///   보스방 위치      → 기본은 모른다. 탐험 트리 "보스 감지"로 해금
    ///   지나온 방과 연결  → 보인다
    ///   안 간 방의 연결   → 그 방에 들어가야 드러난다
    /// </summary>
    public class DungeonMap
    {
        public readonly int Width, Height;
        readonly Room[,] _rooms;

        public Vector2Int EntryCell { get; private set; }
        public Vector2Int BossCell { get; private set; }
        public Vector2Int CurrentCell { get; private set; }

        public DungeonMap(int w, int h)
        {
            Width = w; Height = h;
            _rooms = new Room[w, h];
        }

        public bool InBounds(Vector2Int c) =>
            c.x >= 0 && c.x < Width && c.y >= 0 && c.y < Height;

        public Room At(Vector2Int c) => InBounds(c) ? _rooms[c.x, c.y] : null;
        public Room Current => At(CurrentCell);

        public void Put(Room r) => _rooms[r.Cell.x, r.Cell.y] = r;

        public void SetEntry(Vector2Int c) { EntryCell = c; CurrentCell = c; }
        public void SetBoss(Vector2Int c) { BossCell = c; }

        public void MoveTo(Vector2Int c)
        {
            if (!InBounds(c) || At(c) == null) return;
            CurrentCell = c;
            At(c).Visited = true;
        }

        public static Vector2Int Step(Dir d) => d switch
        {
            Dir.N => Vector2Int.up,
            Dir.S => Vector2Int.down,
            Dir.E => Vector2Int.right,
            Dir.W => Vector2Int.left,
            _ => Vector2Int.zero
        };

        public static Dir Opposite(Dir d) => d switch
        {
            Dir.N => Dir.S, Dir.S => Dir.N,
            Dir.E => Dir.W, Dir.W => Dir.E,
            _ => Dir.None
        };

        public static readonly Dir[] AllDirs = { Dir.N, Dir.E, Dir.S, Dir.W };

        public IEnumerable<Dir> ExitsOf(Vector2Int c)
        {
            var r = At(c);
            if (r == null) yield break;
            foreach (var d in AllDirs)
                if ((r.Exits & d) != 0) yield return d;
        }

        /// <summary>보스방까지 남은 칸 수 (직선거리).</summary>
        public int ManhattanToBoss =>
            Mathf.Abs(CurrentCell.x - BossCell.x) + Mathf.Abs(CurrentCell.y - BossCell.y);

        /// <summary>실제 연결을 따라간 최단 경로. "경로 예측" 노드로 해금되는 정보.</summary>
        public List<Vector2Int> ShortestPathToBoss()
        {
            var prev = new Dictionary<Vector2Int, Vector2Int>();
            var seen = new HashSet<Vector2Int> { CurrentCell };
            var q = new Queue<Vector2Int>();
            q.Enqueue(CurrentCell);

            while (q.Count > 0)
            {
                var cur = q.Dequeue();
                if (cur == BossCell) break;
                foreach (var d in ExitsOf(cur))
                {
                    var nxt = cur + Step(d);
                    if (!InBounds(nxt) || At(nxt) == null || !seen.Add(nxt)) continue;
                    prev[nxt] = cur;
                    q.Enqueue(nxt);
                }
            }

            var path = new List<Vector2Int>();
            if (!seen.Contains(BossCell)) return path;
            var node = BossCell;
            while (node != CurrentCell) { path.Add(node); node = prev[node]; }
            path.Reverse();
            return path;
        }
    }
}
