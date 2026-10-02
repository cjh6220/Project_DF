using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Proto.Data;

namespace Proto.Dungeon
{
    /// <summary>
    /// 격자 던전 생성기.
    ///
    /// 설계 원칙: "보스로 가는 최단 경로는 보상이 적고, 우회로에 광맥이 많다."
    /// 잘못 들어간 방이 비어 있으면 그건 벌점일 뿐이다. 막다른 길에는 반드시 보상이 있어야
    /// "속공이냐 채집이냐"가 맵 구조 자체로 표현된다.
    ///
    /// 생성 순서
    ///   1) 입구를 아래쪽 가운데에 둔다
    ///   2) 보스방을 minBossDistance 이상 떨어진 칸에 둔다
    ///   3) 입구 → 보스 본 경로를 굽이지게 판다 (직선이면 긴장이 없다)
    ///   4) 본 경로에서 가지를 뻗는다 → 여기에 광맥을 몰아준다
    /// </summary>
    public static class DungeonGenerator
    {
        public static DungeonMap Generate(TuningConfig cfg, int seed)
        {
            var rng = new System.Random(seed);
            var map = new DungeonMap(cfg.gridWidth, cfg.gridHeight);

            var entry = new Vector2Int(cfg.gridWidth / 2, 0);
            var boss = PickBossCell(cfg, rng, entry);

            var main = CarveMainPath(map, rng, entry, boss, cfg);
            foreach (var c in main)
            {
                var r = EnsureRoom(map, c);
                r.OnMainPath = true;
            }

            CarveBranches(map, rng, main, cfg);

            map.At(entry).Kind = RoomKind.Entry;
            map.At(boss).Kind = RoomKind.Boss;
            map.SetEntry(entry);
            map.SetBoss(boss);
            map.At(entry).Visited = true;
            map.At(entry).Cleared = true;

            PopulateRooms(map, rng, cfg);
            return map;
        }

        static Vector2Int PickBossCell(TuningConfig cfg, System.Random rng, Vector2Int entry)
        {
            var candidates = new List<Vector2Int>();
            for (int x = 0; x < cfg.gridWidth; x++)
            for (int y = 0; y < cfg.gridHeight; y++)
            {
                var c = new Vector2Int(x, y);
                int dist = Mathf.Abs(c.x - entry.x) + Mathf.Abs(c.y - entry.y);
                if (dist >= cfg.minBossDistance) candidates.Add(c);
            }
            if (candidates.Count == 0)
                return new Vector2Int(cfg.gridWidth / 2, cfg.gridHeight - 1);
            return candidates[rng.Next(candidates.Count)];
        }

        /// <summary>
        /// 보스 쪽으로 편향된 랜덤워크. 가끔 옆으로 새게 해서 길이 굽이지게 만든다.
        /// 직선거리 5칸인데 실제로는 9칸 걸리는 상황 — 그것만으로 긴장이 된다.
        /// </summary>
        static List<Vector2Int> CarveMainPath(DungeonMap map, System.Random rng,
                                              Vector2Int from, Vector2Int to, TuningConfig cfg)
        {
            var path = new List<Vector2Int> { from };
            var cur = from;
            var visited = new HashSet<Vector2Int> { from };
            int guard = cfg.gridWidth * cfg.gridHeight * 6;

            while (cur != to && guard-- > 0)
            {
                var options = new List<Dir>();
                foreach (var d in DungeonMap.AllDirs)
                {
                    var n = cur + DungeonMap.Step(d);
                    if (!map.InBounds(n) || visited.Contains(n)) continue;

                    int before = Mathf.Abs(cur.x - to.x) + Mathf.Abs(cur.y - to.y);
                    int after = Mathf.Abs(n.x - to.x) + Mathf.Abs(n.y - to.y);

                    // 보스에 가까워지는 방향은 가중치 3, 옆으로 새는 방향은 1
                    int weight = after < before ? 3 : 1;
                    for (int i = 0; i < weight; i++) options.Add(d);
                }

                if (options.Count == 0)
                {
                    // 막혔으면 한 칸 되돌아간다
                    if (path.Count <= 1) break;
                    path.RemoveAt(path.Count - 1);
                    cur = path[path.Count - 1];
                    continue;
                }

                var pick = options[rng.Next(options.Count)];
                var next = cur + DungeonMap.Step(pick);
                Connect(map, cur, pick);
                visited.Add(next);
                path.Add(next);
                cur = next;
            }
            return path;
        }

        static void CarveBranches(DungeonMap map, System.Random rng,
                                  List<Vector2Int> main, TuningConfig cfg)
        {
            // 입구와 보스방 바로 옆은 가지의 시작점으로 쓰지 않는다
            var anchors = main.Skip(1).Take(Mathf.Max(0, main.Count - 2)).ToList();
            if (anchors.Count == 0) return;

            for (int b = 0; b < cfg.branchCount; b++)
            {
                var start = anchors[rng.Next(anchors.Count)];
                var cur = start;
                int len = 1 + rng.Next(Mathf.Max(1, cfg.branchMaxLength));

                for (int i = 0; i < len; i++)
                {
                    var free = DungeonMap.AllDirs
                        .Where(d =>
                        {
                            var n = cur + DungeonMap.Step(d);
                            return map.InBounds(n) && map.At(n) == null;
                        }).ToList();

                    if (free.Count == 0) break;
                    var pick = free[rng.Next(free.Count)];
                    Connect(map, cur, pick);
                    cur += DungeonMap.Step(pick);
                }
            }
        }

        static void PopulateRooms(DungeonMap map, System.Random rng, TuningConfig cfg)
        {
            for (int x = 0; x < map.Width; x++)
            for (int y = 0; y < map.Height; y++)
            {
                var r = map.At(new Vector2Int(x, y));
                if (r == null) continue;

                r.Champions.Clear();

                if (r.Kind == RoomKind.Entry)
                {
                    r.EnemyCount = 0; r.OreCount = 0;
                    continue;
                }

                r.EnemyCount = rng.Next(cfg.enemiesPerRoom.x, cfg.enemiesPerRoom.y + 1);

                // 챔피언은 "방당 몇 마리"가 아니라 확률로 굴린다.
                // 매 방에 나오면 특별하지 않기 때문이다.
                if (rng.NextDouble() < cfg.championChancePerRoom)
                {
                    r.Champions.Add(Proto.Enemy.Affixes.Roll(rng));
                    if (rng.NextDouble() < cfg.championSecondChance)
                        r.Champions.Add(Proto.Enemy.Affixes.Roll(rng));
                }

                // 여기가 핵심 — 우회로(가지)에 광맥을 몰아준다
                var range = r.OnMainPath ? cfg.oresOnMainPath : cfg.oresOnBranch;
                r.OreCount = rng.Next(range.x, range.y + 1);

                bool deadEnd = CountExits(r) == 1 && !r.OnMainPath;

                // 막다른 길이면 보물방으로 승격 (탐색에 확실한 보상을 준다)
                if (deadEnd && r.Kind == RoomKind.Normal && rng.NextDouble() < 0.4)
                {
                    r.Kind = RoomKind.Treasure;
                    r.OreCount += 1;
                }

                // 챔피언이 둘 이상이면 엘리트방으로 표시한다
                if (r.Champions.Count >= 2 && r.Kind == RoomKind.Normal)
                    r.Kind = RoomKind.Elite;
            }
        }

        static int CountExits(Room r)
        {
            int n = 0;
            foreach (var d in DungeonMap.AllDirs) if ((r.Exits & d) != 0) n++;
            return n;
        }

        static Room EnsureRoom(DungeonMap map, Vector2Int c)
        {
            var r = map.At(c);
            if (r == null) { r = new Room { Cell = c }; map.Put(r); }
            return r;
        }

        static void Connect(DungeonMap map, Vector2Int from, Dir d)
        {
            var to = from + DungeonMap.Step(d);
            var a = EnsureRoom(map, from);
            var b = EnsureRoom(map, to);
            a.Exits |= d;
            b.Exits |= DungeonMap.Opposite(d);
        }
    }
}
