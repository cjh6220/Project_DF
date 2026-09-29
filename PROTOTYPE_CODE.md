# 프로토타입 코드

Unity 6 / URP / new Input System. `Assets/_Project/Scripts/` 전체 소스.

씬은 `Proto ▸ Build Prototype Scene` 메뉴가 프리미티브만으로 통째로 만든다. 외부 에셋 없음.
수치는 전부 `Assets/_Project/Data/Tuning.asset` (ScriptableObject)에서 플레이 중에도 조정 가능.

## 파일 목록

- `Assets/_Project/Scripts/Core/Health.cs`
- `Assets/_Project/Scripts/Core/Resources.cs`
- `Assets/_Project/Scripts/Core/RunManager.cs`
- `Assets/_Project/Scripts/Core/Stamina.cs`
- `Assets/_Project/Scripts/Data/TuningConfig.cs`
- `Assets/_Project/Scripts/Dungeon/DungeonGenerator.cs`
- `Assets/_Project/Scripts/Dungeon/DungeonMap.cs`
- `Assets/_Project/Scripts/Dungeon/ExitGate.cs`
- `Assets/_Project/Scripts/Dungeon/RoomController.cs`
- `Assets/_Project/Scripts/Editor/ProtoSceneBuilder.cs`
- `Assets/_Project/Scripts/Enemy/EnemyController.cs`
- `Assets/_Project/Scripts/Enemy/MonsterAffix.cs`
- `Assets/_Project/Scripts/Feel/Feel.cs`
- `Assets/_Project/Scripts/Feel/Tint.cs`
- `Assets/_Project/Scripts/Mining/OreNode.cs`
- `Assets/_Project/Scripts/Player/PlayerController.cs`
- `Assets/_Project/Scripts/UI/HudView.cs`
- `Assets/_Project/Scripts/UI/MinimapView.cs`
- `Assets/_Project/Scripts/UI/ResultView.cs`

---

## Assets/_Project/Scripts/Core/Health.cs

```csharp
using System;
using UnityEngine;

namespace Proto.Core
{
    /// <summary>플레이어와 적이 공유하는 체력. 피격 시 이벤트로 타격감 연출을 건다.</summary>
    public class Health : MonoBehaviour
    {
        [SerializeField] float max = 100f;
        public float Max => max;
        public float Current { get; private set; }
        public bool IsDead => Current <= 0f;

        public event Action<float, float> Changed;   // (current, max)
        public event Action<float, Vector3> Damaged; // (amount, sourcePos)
        public event Action Died;

        void Awake() => Current = max;

        public void Configure(float newMax)
        {
            max = newMax;
            Current = newMax;
            Changed?.Invoke(Current, Max);
        }

        public void TakeDamage(float amount, Vector3 sourcePos)
        {
            if (IsDead || amount <= 0f) return;
            Current = Mathf.Max(0f, Current - amount);
            Changed?.Invoke(Current, Max);
            Damaged?.Invoke(amount, sourcePos);
            if (Current <= 0f) Died?.Invoke();
        }

        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f) return;
            Current = Mathf.Min(Max, Current + amount);
            Changed?.Invoke(Current, Max);
        }
    }
}
```

## Assets/_Project/Scripts/Core/Resources.cs

```csharp
using System;
using System.Collections.Generic;

namespace Proto.Core
{
    /// <summary>
    /// 자원 6종. 크게 2계열로 나뉘며 각각 다른 성장을 먹인다.
    ///   채굴 계열 → 더 오래 버틴다 (탐험/채굴/고용)
    ///   전투 계열 → 더 강해진다 (스킬)
    /// 이 분리가 게임의 뼈대다. 흐리지 말 것.
    /// </summary>
    public enum ResourceId
    {
        Gold,      // 공통
        Ore,       // 채굴 — 얕은 방
        Crystal,   // 채굴 — 깊은 방
        Alien,     // 채굴 — 희귀
        Essence,   // 전투 — 일반 몬스터
        Core       // 전투 — 엘리트/보스
    }

    [Serializable]
    public class ResourceWallet
    {
        readonly Dictionary<ResourceId, int> _amounts = new();

        public int Get(ResourceId id) => _amounts.TryGetValue(id, out var v) ? v : 0;

        public void Add(ResourceId id, int amount)
        {
            if (amount == 0) return;
            _amounts[id] = Get(id) + amount;
            Changed?.Invoke(id, _amounts[id]);
        }

        public void Clear()
        {
            _amounts.Clear();
            Cleared?.Invoke();
        }

        public void MergeInto(ResourceWallet target)
        {
            foreach (var kv in _amounts) target.Add(kv.Key, kv.Value);
        }

        public IEnumerable<KeyValuePair<ResourceId, int>> All => _amounts;

        public event Action<ResourceId, int> Changed;
        public event Action Cleared;
    }
}
```

## Assets/_Project/Scripts/Core/RunManager.cs

```csharp
using System;
using UnityEngine;
using Proto.Data;
using Proto.Dungeon;
using Proto.Mining;
using Proto.Player;

namespace Proto.Core
{
    public enum RunPhase { Idle, InDungeon, Result }

    /// <summary>
    /// 한 번의 던전 입장을 관리한다.
    ///
    ///   입장 → 피로도 가득 → 방 이동/전투/채굴 → 피로도 0 또는 HP 0 → 귀환
    ///   페널티 없음. 획득물 전부 유지. 그래서 "한 번 더"가 쉽게 나온다.
    /// </summary>
    public class RunManager : MonoBehaviour
    {
        [SerializeField] TuningConfig cfg;
        [SerializeField] RoomController room;
        [SerializeField] PlayerController player;
        [SerializeField] Transform playerSpawn;

        public TuningConfig Config => cfg;
        public DungeonMap Map { get; private set; }
        public Stamina Stamina { get; private set; }
        public ResourceWallet RunLoot { get; } = new();
        public ResourceWallet Bank { get; } = new();
        public RunPhase Phase { get; private set; } = RunPhase.Idle;

        public event Action RunStarted;
        public event Action<string> RunEnded;      // 종료 사유
        public event Action MapChanged;

        Health _playerHealth;

        void Awake()
        {
            Stamina = new Stamina(cfg);
            Stamina.Emptied += () => EndRun("탈진");

            _playerHealth = player.GetComponent<Health>();
            _playerHealth.Died += () => EndRun("쓰러짐");

            room.ExitUsed += OnExitUsed;
            room.OreMined += OnOreMined;
            room.EnemyKilled += RewardEnemyKill;
        }

        void Start() => StartRun();

        public void StartRun()
        {
            Map = DungeonGenerator.Generate(cfg, UnityEngine.Random.Range(0, int.MaxValue));
            RunLoot.Clear();
            Stamina.Refill();
            Stamina.Draining = true;

            _playerHealth.Configure(cfg.maxHealth);
            player.ControlEnabled = true;

            Phase = RunPhase.InDungeon;
            EnterCurrentRoom();
            RunStarted?.Invoke();
            MapChanged?.Invoke();
        }

        void Update()
        {
            if (Phase != RunPhase.InDungeon) return;
            // 던전에 있는 동안 시간에 따라 계속 소모된다
            Stamina.Tick(Time.deltaTime);
        }

        void EnterCurrentRoom()
        {
            var r = Map.Current;
            r.Visited = true;
            room.Build(r, player.transform);

            if (playerSpawn != null)
            {
                var cc = player.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                player.transform.position = playerSpawn.position;
                if (cc != null) cc.enabled = true;
            }
            MapChanged?.Invoke();
        }

        void OnExitUsed(Dir d)
        {
            if (Phase != RunPhase.InDungeon) return;

            var next = Map.CurrentCell + DungeonMap.Step(d);
            if (!Map.InBounds(next) || Map.At(next) == null) return;

            // 방 이동 고정 비용 (0이면 순수 시간제)
            if (cfg.staminaPerRoomMove > 0f) Stamina.Spend(cfg.staminaPerRoomMove);
            if (Stamina.Depleted) return;

            Map.MoveTo(next);
            EnterCurrentRoom();
        }

        void OnOreMined(OreNode ore)
        {
            RunLoot.Add(ore.ResourceId, ore.Yield);
        }

        public void RewardEnemyKill(bool elite)
        {
            RunLoot.Add(ResourceId.Essence, elite ? 3 : 1);
            if (elite) RunLoot.Add(ResourceId.Core, 1);
            RunLoot.Add(ResourceId.Gold, UnityEngine.Random.Range(2, 6));
        }

        void EndRun(string reason)
        {
            if (Phase != RunPhase.InDungeon) return;

            Phase = RunPhase.Result;
            Stamina.Draining = false;
            player.ControlEnabled = false;

            // 페널티 없음 — 획득물은 전부 유지한다
            RunLoot.MergeInto(Bank);
            RunEnded?.Invoke(reason);
        }

        /// <summary>정산 화면에서 "다시 들어가기"를 누르면 호출한다.</summary>
        public void Restart() => StartRun();
    }
}
```

## Assets/_Project/Scripts/Core/Stamina.cs

```csharp
using System;
using UnityEngine;
using Proto.Data;

namespace Proto.Core
{
    /// <summary>
    /// 피로도. 던전에 있는 동안 시간에 따라 계속 소모된다.
    /// 이동도, 전투도, 채굴도 전부 시간을 쓰므로
    /// 자원 하나가 게임 안의 모든 결정을 관통한다.
    /// </summary>
    public class Stamina
    {
        readonly TuningConfig _cfg;

        public float Current { get; private set; }
        public float Max => _cfg.maxStamina;
        public float Ratio => Max <= 0f ? 0f : Mathf.Clamp01(Current / Max);
        public bool Depleted => Current <= 0f;
        public bool Draining { get; set; }

        public event Action<float, float> Changed;   // (current, max)
        public event Action Emptied;

        public Stamina(TuningConfig cfg)
        {
            _cfg = cfg;
            Refill();
        }

        public void Refill()
        {
            Current = _cfg.maxStamina;
            Changed?.Invoke(Current, Max);
        }

        public void Tick(float deltaTime)
        {
            if (!Draining || Depleted) return;
            Spend(_cfg.staminaDrainPerSecond * deltaTime);
        }

        public void Spend(float amount)
        {
            if (amount <= 0f || Depleted) return;
            Current = Mathf.Max(0f, Current - amount);
            Changed?.Invoke(Current, Max);
            if (Current <= 0f)
            {
                Draining = false;
                Emptied?.Invoke();
            }
        }

        public void Restore(float amount)
        {
            if (amount <= 0f) return;
            Current = Mathf.Min(Max, Current + amount);
            Changed?.Invoke(Current, Max);
        }
    }
}
```

## Assets/_Project/Scripts/Data/TuningConfig.cs

```csharp
using UnityEngine;

namespace Proto.Data
{
    /// <summary>
    /// 모든 튜닝 수치를 한 곳에 모은다.
    /// 피로도 소모 속도가 이 게임에서 가장 중요한 숫자이므로,
    /// 플레이 중에도 인스펙터에서 바로 만질 수 있어야 한다.
    /// Assets/_Project/Data/Tuning.asset 으로 생성해서 쓴다.
    /// </summary>
    [CreateAssetMenu(fileName = "Tuning", menuName = "Proto/Tuning Config")]
    public class TuningConfig : ScriptableObject
    {
        [Header("── 피로도 ──")]
        [Tooltip("던전 입장 시 최대 피로도")]
        public float maxStamina = 100f;

        [Tooltip("초당 소모량. 이 게임에서 가장 중요한 숫자.")]
        public float staminaDrainPerSecond = 1.0f;

        [Tooltip("방을 옮길 때 추가로 드는 고정 비용 (0이면 순수 시간제)")]
        public float staminaPerRoomMove = 0f;

        [Header("── 맵 ──")]
        public int gridWidth = 7;
        public int gridHeight = 7;

        [Tooltip("입구에서 보스방까지 최소 거리(칸)")]
        public int minBossDistance = 6;

        [Tooltip("본 경로에서 뻗어나갈 가지의 개수")]
        public int branchCount = 5;

        [Tooltip("가지 하나의 최대 길이")]
        public int branchMaxLength = 3;

        [Header("── 방 내용물 ──")]
        public Vector2Int enemiesPerRoom = new Vector2Int(2, 4);

        [Tooltip("본 경로 방의 광맥 수 (적게)")]
        public Vector2Int oresOnMainPath = new Vector2Int(0, 1);

        [Tooltip("가지 방의 광맥 수 (많이) — 우회로에 보상을 몰아준다")]
        public Vector2Int oresOnBranch = new Vector2Int(2, 3);

        [Header("── 채굴 ──")]
        [Tooltip("광맥 1회 채굴에 걸리는 시간")]
        public float mineDuration = 2.5f;

        [Tooltip("피격 시 채굴 진행도를 얼마나 유지하는가 (0=전부 잃음, 1=유지)")]
        [Range(0f, 1f)] public float mineProgressKeptOnHit = 0f;

        public float mineRange = 2.2f;

        [Header("── 플레이어 ──")]
        public float moveSpeed = 6.5f;
        public float maxHealth = 100f;
        public float attackDamage = 25f;
        public float attackCooldown = 0.38f;
        public float attackRange = 2.0f;
        public float attackArcDegrees = 110f;

        [Header("── 적 ──")]
        public float enemyHealth = 40f;
        public float enemyMoveSpeed = 3.0f;
        public float enemyDamage = 8f;
        public float enemyAttackCooldown = 1.4f;
        public float enemyAttackRange = 1.8f;

                [Header("── 챔피언 (접두사 몬스터) ──")]
        [Tooltip("방에 챔피언이 나올 확률. 방마다 몇 마리가 아니라 확률로 굴린다.\n" +
                 "매 방에 나오면 특별하지 않다 — 한 판에 2~3마리 만나는 정도가 적당하다.")]
        [Range(0f, 1f)] public float championChancePerRoom = 0.35f;

        [Tooltip("챔피언이 나왔을 때, 한 마리 더 붙을 확률")]
        [Range(0f, 1f)] public float championSecondChance = 0.18f;

        [Header("── 접두사 효과 ──")]
        [Tooltip("'폭발하는' 접두사의 사망 폭발 반경과 피해")]
        public float explodeRadius = 3.2f;
        public float explodeDamage = 18f;

        [Tooltip("'울부짖는' 접두사가 죽을 때 남은 적에게 거는 가속")]
        public float howlHasteMultiplier = 1.5f;
        public float howlHasteDuration = 6f;

        [Header("── 피격 반응 ──")]
        [Tooltip("일반 몬스터가 맞고 멈칫하는 시간. 엘리트는 슈퍼아머라 무시한다.")]
        public float enemyHitStun = 0.22f;

        [Tooltip("일반 몬스터가 맞고 밀려나는 거리")]
        public float enemyKnockback = 0.4f;

[Header("── 타격감 ──")]
        [Tooltip("타격 시 화면 정지 시간. 손맛의 절반이 여기서 나온다.")]
        public float hitStopDuration = 0.06f;
        public float shakeDuration = 0.12f;
        public float shakeAmplitude = 0.22f;
        public float hitFlashDuration = 0.08f;
    }
}
```

## Assets/_Project/Scripts/Dungeon/DungeonGenerator.cs

```csharp
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
```

## Assets/_Project/Scripts/Dungeon/DungeonMap.cs

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using Proto.Data;

namespace Proto.Dungeon
{
    public enum RoomKind { Normal, Entry, Boss, Elite, Treasure }

    [Flags]
    public enum Dir { None = 0, N = 1, E = 2, S = 4, W = 8 }

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
```

## Assets/_Project/Scripts/Dungeon/ExitGate.cs

```csharp
using System;
using UnityEngine;

namespace Proto.Dungeon
{
    /// <summary>방 출구. 몬스터가 남아 있으면 닫혀 있다.</summary>
    [RequireComponent(typeof(Collider))]
    public class ExitGate : MonoBehaviour
    {
        Dir _dir;
        Action<Dir> _onUse;
        bool _open;
        Renderer _renderer;

        static readonly Color Closed = new Color(0.55f, 0.16f, 0.16f);
        static readonly Color Open = new Color(0.30f, 0.90f, 0.55f);

        public void Setup(Dir dir, Action<Dir> onUse)
        {
            _dir = dir;
            _onUse = onUse;
            var col = GetComponent<Collider>();
            col.isTrigger = true;
            _renderer = GetComponentInChildren<Renderer>();
            SetOpen(false);
        }

        public void SetOpen(bool open)
        {
            _open = open;
            if (_renderer == null) return;
            var block = new MaterialPropertyBlock();
            _renderer.GetPropertyBlock(block);
            var c = open ? Open : Closed;
            block.SetColor("_BaseColor", c);
            block.SetColor("_EmissionColor", c * (open ? 1.8f : 0.4f));
            _renderer.SetPropertyBlock(block);
        }

        void OnTriggerEnter(Collider other)
        {
            if (!_open) return;
            if (!other.CompareTag("Player")) return;
            _onUse?.Invoke(_dir);
        }
    }
}
```

## Assets/_Project/Scripts/Dungeon/RoomController.cs

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using Proto.Core;
using Proto.Data;
using Proto.Enemy;
using Proto.Mining;

namespace Proto.Dungeon
{
    /// <summary>
    /// 씬에 실제로 존재하는 방 하나. 프로토타입에서는 아레나 하나를 재활용하고
    /// 내용물(적·광맥·출구)만 갈아끼운다.
    ///
    /// 방 클리어 조건: 몬스터 전멸. 전투는 선택이 아니라 통행료다.
    /// </summary>
    public class RoomController : MonoBehaviour
    {
        [SerializeField] TuningConfig cfg;
        [SerializeField] Transform contentRoot;
        [SerializeField] GameObject enemyPrefab;
        [SerializeField] GameObject orePrefab;
        [SerializeField] GameObject exitPrefab;
        [SerializeField] Vector2 arenaSize = new Vector2(18f, 10f);

        readonly List<GameObject> _spawned = new();
        readonly List<Health> _enemies = new();
        readonly Dictionary<Dir, GameObject> _exits = new();

        public bool Cleared { get; private set; }

        public event Action Clearedchanged;
        public event Action<bool> EnemyKilled;   // (elite)
        public event Action<Dir> ExitUsed;
        public event Action<OreNode> OreMined;

        public void Build(Room room, Transform player)
        {
            ClearContents();
            Cleared = room.EnemyCount == 0;

            SpawnEnemies(room, player);
            SpawnOres(room);
            SpawnExits(room);
            RefreshExits();
        }

        void SpawnEnemies(Room room, Transform player)
        {
            // 일반 몬스터 — 넓백과 경직이 통한다
            for (int i = 0; i < room.EnemyCount; i++)
                SpawnOne(player, Proto.Enemy.Affix.None);

            // 챔피언 — 접두사가 붙은 강화 몬스터
            foreach (var affix in room.Champions)
                SpawnOne(player, affix);
        }

        void SpawnOne(Transform player, Proto.Enemy.Affix affix)
        {
            var pos = RandomPoint(0.55f);
            var go = Instantiate(enemyPrefab, pos, Quaternion.identity, contentRoot);
            go.SetActive(true);
            _spawned.Add(go);

            var ec = go.GetComponent<EnemyController>();
            if (ec != null) ec.Configure(cfg, player, affix);

            var hp = go.GetComponent<Health>();
            if (hp != null)
            {
                _enemies.Add(hp);
                bool champion = affix != Proto.Enemy.Affix.None;
                hp.Died += () => OnEnemyDied(hp, go, champion);
            }
        }



        void SpawnOres(Room room)
        {
            for (int i = 0; i < room.OreCount; i++)
            {
                var pos = RandomPoint(0.8f);
                var go = Instantiate(orePrefab, pos, Quaternion.identity, contentRoot);
                go.SetActive(true);
                _spawned.Add(go);

                var ore = go.GetComponent<OreNode>();
                if (ore == null) continue;

                // 깊은 방일수록 상급 광맥 확률이 오른다
                int depth = room.Cell.y;
                float roll = UnityEngine.Random.value;
                OreGrade grade =
                    roll < 0.08f + depth * 0.02f ? OreGrade.Rare :
                    roll < 0.35f + depth * 0.05f ? OreGrade.Deep :
                    OreGrade.Common;

                int amount = grade == OreGrade.Rare ? 1 : UnityEngine.Random.Range(1, 3);
                ore.Configure(grade, amount);
                ore.Mined += o => OreMined?.Invoke(o);
            }
        }

        void SpawnExits(Room room)
        {
            _exits.Clear();
            foreach (var d in DungeonMap.AllDirs)
            {
                if ((room.Exits & d) == 0) continue;

                Vector3 pos = d switch
                {
                    Dir.N => new Vector3(0f, 0f, arenaSize.y * 0.5f),
                    Dir.S => new Vector3(0f, 0f, -arenaSize.y * 0.5f),
                    Dir.E => new Vector3(arenaSize.x * 0.5f, 0f, 0f),
                    _ => new Vector3(-arenaSize.x * 0.5f, 0f, 0f)
                };

                var go = Instantiate(exitPrefab, contentRoot.position + pos,
                                     Quaternion.identity, contentRoot);
                go.SetActive(true);
                _spawned.Add(go);
                _exits[d] = go;

                var gate = go.GetComponent<ExitGate>();
                if (gate == null) gate = go.AddComponent<ExitGate>();
                gate.Setup(d, dir => ExitUsed?.Invoke(dir));
            }
        }

        void OnEnemyDied(Health hp, GameObject go, bool elite)
        {
            _enemies.Remove(hp);
            go.SetActive(false);
            EnemyKilled?.Invoke(elite);

            if (_enemies.Count == 0 && !Cleared)
            {
                Cleared = true;
                RefreshExits();
                Clearedchanged?.Invoke();
            }
        }

        /// <summary>몬스터가 남아 있으면 출구가 잠긴다.</summary>
        void RefreshExits()
        {
            foreach (var kv in _exits)
            {
                var gate = kv.Value.GetComponent<ExitGate>();
                if (gate != null) gate.SetOpen(Cleared);
            }
        }

        Vector3 RandomPoint(float spread)
        {
            return contentRoot.position + new Vector3(
                UnityEngine.Random.Range(-arenaSize.x, arenaSize.x) * 0.5f * spread,
                0f,
                UnityEngine.Random.Range(-arenaSize.y, arenaSize.y) * 0.5f * spread);
        }

        void ClearContents()
        {
            foreach (var go in _spawned) if (go != null) Destroy(go);
            _spawned.Clear();
            _enemies.Clear();
            _exits.Clear();
        }
    }
}
```

## Assets/_Project/Scripts/Editor/ProtoSceneBuilder.cs

```csharp
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Proto.Core;
using Proto.Data;
using Proto.Dungeon;
using Proto.Enemy;
using Proto.Mining;
using Proto.Player;
using Proto.UI;

namespace Proto.EditorTools
{
    /// <summary>
    /// 1주차 프로토타입 씬을 통째로 만들어 준다.
    /// 메뉴: Proto ▸ Build Prototype Scene
    ///
    /// 에셋은 하나도 쓰지 않는다. 전부 캡슐과 큐브다.
    /// 재미가 확인되기 전에 에셋을 사면 그 에셋에 맞춰 설계가 끌려간다.
    /// </summary>
    public static class ProtoSceneBuilder
    {
        const string TuningPath = "Assets/_Project/Data/Tuning.asset";

        [MenuItem("Proto/Build Prototype Scene")]
        public static void Build()
        {
            var cfg = LoadOrCreateTuning();

            var root = new GameObject("== PROTOTYPE ==");

            BuildLighting(root.transform);
            var arena = BuildArena(root.transform);
            var player = BuildPlayer(root.transform, cfg);
            var cam = BuildCamera(root.transform, player.transform);
            BuildFeel(root.transform, cfg, cam.transform.parent);

            var enemyPrefab = BuildEnemyTemplate(root.transform);
            var orePrefab = BuildOreTemplate(root.transform);
            var exitPrefab = BuildExitTemplate(root.transform);

            var roomGo = new GameObject("Room");
            roomGo.transform.SetParent(root.transform);
            var room = roomGo.AddComponent<RoomController>();
            Set(room, "cfg", cfg);
            Set(room, "contentRoot", arena.transform);
            Set(room, "enemyPrefab", enemyPrefab);
            Set(room, "orePrefab", orePrefab);
            Set(room, "exitPrefab", exitPrefab);

            var spawn = new GameObject("PlayerSpawn");
            spawn.transform.SetParent(arena.transform);
            spawn.transform.localPosition = Vector3.zero;

            var runGo = new GameObject("RunManager");
            runGo.transform.SetParent(root.transform);
            var run = runGo.AddComponent<RunManager>();
            Set(run, "cfg", cfg);
            Set(run, "room", room);
            Set(run, "player", player.GetComponent<PlayerController>());
            Set(run, "playerSpawn", spawn.transform);

            BuildUI(root.transform, run, player.GetComponent<PlayerController>());

            enemyPrefab.SetActive(false);
            orePrefab.SetActive(false);
            exitPrefab.SetActive(false);

            Selection.activeGameObject = root;
            Debug.Log("[Proto] 프로토타입 씬 생성 완료. Play를 누르세요.\n" +
                      "이동 WASD · 공격 J · 채굴 K(누르고 있기)\n" +
                      "가장 먼저 만질 숫자: Tuning.asset 의 staminaDrainPerSecond");
        }

        static TuningConfig LoadOrCreateTuning()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<TuningConfig>(TuningPath);
            if (cfg != null) return cfg;

            System.IO.Directory.CreateDirectory("Assets/_Project/Data");
            AssetDatabase.Refresh();
            cfg = ScriptableObject.CreateInstance<TuningConfig>();
            AssetDatabase.CreateAsset(cfg, TuningPath);
            AssetDatabase.SaveAssets();
            return cfg;
        }

        static void BuildLighting(Transform parent)
        {
            var go = new GameObject("Sun");
            go.transform.SetParent(parent);
            go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var l = go.AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = 1.1f;
            l.color = new Color(1f, 0.96f, 0.9f);
        }

        static GameObject BuildArena(Transform parent)
        {
            var arena = new GameObject("Arena");
            arena.transform.SetParent(parent);

            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.SetParent(arena.transform);
            floor.transform.localScale = new Vector3(20f, 0.5f, 12f);
            floor.transform.localPosition = new Vector3(0f, -0.25f, 0f);
            Tint(floor, new Color(0.20f, 0.22f, 0.28f));

            CreateWall(arena.transform, new Vector3(0f, 1f, 6.2f), new Vector3(20f, 2f, 0.4f));
            CreateWall(arena.transform, new Vector3(0f, 1f, -6.2f), new Vector3(20f, 2f, 0.4f));
            CreateWall(arena.transform, new Vector3(10.2f, 1f, 0f), new Vector3(0.4f, 2f, 12f));
            CreateWall(arena.transform, new Vector3(-10.2f, 1f, 0f), new Vector3(0.4f, 2f, 12f));
            return arena;
        }

        static void CreateWall(Transform parent, Vector3 pos, Vector3 scale)
        {
            var w = GameObject.CreatePrimitive(PrimitiveType.Cube);
            w.name = "Wall";
            w.transform.SetParent(parent);
            w.transform.localPosition = pos;
            w.transform.localScale = scale;
            Tint(w, new Color(0.12f, 0.13f, 0.18f));
        }

        static GameObject BuildPlayer(Transform parent, TuningConfig cfg)
        {
            var go = new GameObject("Player");
            go.transform.SetParent(parent);
            go.tag = "Player";

            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.8f; cc.radius = 0.4f; cc.center = new Vector3(0f, 0.9f, 0f);

            var visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform);
            visual.transform.localPosition = Vector3.zero;

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(visual.transform);
            body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            body.transform.localScale = new Vector3(0.8f, 0.9f, 0.8f);
            Object.DestroyImmediate(body.GetComponent<Collider>());
            Tint(body, new Color(0.35f, 0.85f, 0.95f));

            var nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nose.name = "Facing";
            nose.transform.SetParent(visual.transform);
            nose.transform.localPosition = new Vector3(0.55f, 0.95f, 0f);
            nose.transform.localScale = new Vector3(0.45f, 0.2f, 0.2f);
            Object.DestroyImmediate(nose.GetComponent<Collider>());
            Tint(nose, new Color(1f, 0.85f, 0.3f));

            go.AddComponent<Health>();
            var pc = go.AddComponent<PlayerController>();
            Set(pc, "cfg", cfg);
            Set(pc, "visual", visual.transform);
            SetLayerMask(pc, "enemyMask", ~0);
            return go;
        }

        static Camera BuildCamera(Transform parent, Transform target)
        {
            var rig = new GameObject("CameraRig");
            rig.transform.SetParent(parent);
            rig.transform.position = Vector3.zero;

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(rig.transform);

            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 6.5f;
            camGo.transform.position = new Vector3(0f, 9f, -18f);
            camGo.transform.rotation = Quaternion.Euler(20f, 0f, 0f);
            cam.backgroundColor = new Color(0.05f, 0.06f, 0.09f);
            cam.clearFlags = CameraClearFlags.SolidColor;

            camGo.AddComponent<AudioListener>();
            return cam;
        }

        static void BuildFeel(Transform parent, TuningConfig cfg, Transform camRoot)
        {
            var go = new GameObject("Feel");
            go.transform.SetParent(parent);
            var f = go.AddComponent<Proto.Feel.Feel>();
            Set(f, "cfg", cfg);
            Set(f, "cameraRoot", camRoot);
        }

        static GameObject BuildEnemyTemplate(Transform parent)
        {
            var go = new GameObject("EnemyTemplate");
            go.transform.SetParent(parent);

            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.6f; cc.radius = 0.4f; cc.center = new Vector3(0f, 0.8f, 0f);

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.transform.SetParent(go.transform);
            body.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            body.transform.localScale = new Vector3(0.7f, 0.8f, 0.7f);
            Object.DestroyImmediate(body.GetComponent<Collider>());
            Tint(body, new Color(0.85f, 0.35f, 0.35f));

            var hit = go.AddComponent<SphereCollider>();
            hit.radius = 0.6f;
            hit.center = new Vector3(0f, 0.8f, 0f);
            hit.isTrigger = true;

            go.AddComponent<Health>();
            go.AddComponent<EnemyController>();
            return go;
        }

        static GameObject BuildOreTemplate(Transform parent)
        {
            var go = new GameObject("OreTemplate");
            go.transform.SetParent(parent);

            var crystal = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crystal.transform.SetParent(go.transform);
            crystal.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            crystal.transform.localScale = new Vector3(0.6f, 1.0f, 0.6f);
            crystal.transform.localRotation = Quaternion.Euler(0f, 45f, 12f);
            Object.DestroyImmediate(crystal.GetComponent<Collider>());

            var ore = go.AddComponent<OreNode>();
            Set(ore, "visual", crystal.GetComponent<Renderer>());
            return go;
        }

        static GameObject BuildExitTemplate(Transform parent)
        {
            var go = new GameObject("ExitTemplate");
            go.transform.SetParent(parent);

            var gate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gate.transform.SetParent(go.transform);
            gate.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            gate.transform.localScale = new Vector3(1.8f, 1.6f, 0.3f);
            Object.DestroyImmediate(gate.GetComponent<Collider>());

            var trigger = go.AddComponent<BoxCollider>();
            trigger.size = new Vector3(2.2f, 2f, 1.2f);
            trigger.center = new Vector3(0f, 1f, 0f);
            trigger.isTrigger = true;

            go.AddComponent<ExitGate>();
            return go;
        }

        static void BuildUI(Transform parent, RunManager run, PlayerController player)
        {
            var canvasGo = new GameObject("Canvas", typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(parent);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var es = new GameObject("EventSystem",
                    typeof(UnityEngine.EventSystems.EventSystem),
                    typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
                es.transform.SetParent(parent);
            }

            var staminaFill = Bar(canvasGo.transform, "StaminaBar",
                new Vector2(40f, -40f), new Vector2(520f, 26f),
                             new Color(1.00f, 0.84f, 0.10f));   // 피로도 = 노랑
            var staminaText = Text(canvasGo.transform, "StaminaText",
                new Vector2(50f, -44f), 18, TextAlignmentOptions.Left);

            var healthFill = Bar(canvasGo.transform, "HealthBar",
                new Vector2(40f, -76f), new Vector2(380f, 16f),
                            new Color(0.90f, 0.13f, 0.13f));   // 체력 = 빨강

            var lootText = Text(canvasGo.transform, "LootText",
                new Vector2(40f, -112f), 20, TextAlignmentOptions.Left);
            var bossText = Text(canvasGo.transform, "BossText",
                new Vector2(40f, -146f), 20, TextAlignmentOptions.Left);

            var mineRoot = new GameObject("MineBar", typeof(RectTransform));
            mineRoot.transform.SetParent(canvasGo.transform, false);
            var mrt = (RectTransform)mineRoot.transform;
            Anchor(mrt, new Vector2(0.5f, 0.25f), Vector2.zero, new Vector2(320f, 18f));
            var mineFill = Bar(mineRoot.transform, "MineFill", Vector2.zero,
                new Vector2(320f, 18f), new Color(0.45f, 0.85f, 1f), true);
            mineRoot.SetActive(false);

            var mapRoot = new GameObject("Minimap", typeof(RectTransform));
            mapRoot.transform.SetParent(canvasGo.transform, false);
            var maprt = (RectTransform)mapRoot.transform;
            maprt.anchorMin = maprt.anchorMax = new Vector2(1f, 1f);
            maprt.pivot = new Vector2(1f, 1f);
            maprt.anchoredPosition = new Vector2(-40f, -40f);
            maprt.sizeDelta = new Vector2(200f, 200f);

            var cellPrefab = new GameObject("Cell", typeof(RectTransform), typeof(Image));
            cellPrefab.transform.SetParent(parent, false);
            var cprt = (RectTransform)cellPrefab.transform;
            cprt.pivot = Vector2.zero;
            cprt.anchorMin = Vector2.zero;
            cprt.anchorMax = Vector2.zero;
            cellPrefab.SetActive(false);

            var hud = canvasGo.AddComponent<HudView>();
            Set(hud, "run", run);
            Set(hud, "player", player);
            Set(hud, "staminaFill", staminaFill);
            Set(hud, "healthFill", healthFill);
            Set(hud, "mineBarRoot", mineRoot);
            Set(hud, "mineFill", mineFill);
            Set(hud, "staminaText", staminaText);
            Set(hud, "lootText", lootText);
            Set(hud, "bossDistanceText", bossText);

            var mini = canvasGo.AddComponent<MinimapView>();
            Set(mini, "run", run);
            Set(mini, "grid", maprt);
            Set(mini, "cellPrefab", cellPrefab);

            Set(hud, "minimap", mini);

            var resultRoot = new GameObject("Result", typeof(RectTransform), typeof(Image));
            resultRoot.transform.SetParent(canvasGo.transform, false);
            var rrt = (RectTransform)resultRoot.transform;
            rrt.anchorMin = Vector2.zero; rrt.anchorMax = Vector2.one;
            rrt.offsetMin = rrt.offsetMax = Vector2.zero;
            resultRoot.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.82f);

            var title = Text(resultRoot.transform, "Title", new Vector2(0f, 180f), 56,
                TextAlignmentOptions.Center, true);
            var body = Text(resultRoot.transform, "Body", new Vector2(0f, -20f), 26,
                TextAlignmentOptions.Center, true);

            var btnGo = new GameObject("AgainButton", typeof(RectTransform),
                typeof(Image), typeof(Button));
            btnGo.transform.SetParent(resultRoot.transform, false);
            var brt = (RectTransform)btnGo.transform;
            Anchor(brt, new Vector2(0.5f, 0.5f), new Vector2(0f, -240f), new Vector2(280f, 64f));
            btnGo.GetComponent<Image>().color = new Color(0.95f, 0.80f, 0.35f);
            var btnLabel = Text(btnGo.transform, "Label", Vector2.zero, 26,
                TextAlignmentOptions.Center, true);
            btnLabel.text = "다시 들어가기";
            btnLabel.color = Color.black;

            var result = canvasGo.AddComponent<ResultView>();
            Set(result, "run", run);
            Set(result, "root", resultRoot);
            Set(result, "titleText", title);
            Set(result, "bodyText", body);
            Set(result, "againButton", btnGo.GetComponent<Button>());
        }

        static Image Bar(Transform parent, string name, Vector2 pos, Vector2 size,
                         Color color, bool center = false)
        {
            var bg = new GameObject(name, typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(parent, false);
            var rt = (RectTransform)bg.transform;
            Anchor(rt, center ? new Vector2(0.5f, 0.5f) : new Vector2(0f, 1f), pos, size);
            bg.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(bg.transform, false);
            var frt = (RectTransform)fillGo.transform;
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
            frt.offsetMin = new Vector2(2f, 2f); frt.offsetMax = new Vector2(-2f, -2f);

            var img = fillGo.GetComponent<Image>();
            img.color = color;
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            img.fillAmount = 1f;
            return img;
        }

        static TMP_Text Text(Transform parent, string name, Vector2 pos, float size,
                             TextAlignmentOptions align, bool center = false)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            Anchor(rt, center ? new Vector2(0.5f, 0.5f) : new Vector2(0f, 1f),
                   pos, new Vector2(900f, 40f));

            var t = go.AddComponent<TextMeshProUGUI>();
            t.fontSize = size;
            t.alignment = align;
            t.color = Color.white;
            t.text = "";
            return t;
        }

        static void Anchor(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        static void Tint(GameObject go, Color c)
        {
            var r = go.GetComponent<Renderer>();
            if (r == null) return;
            // 에디터에서 칠한 MaterialPropertyBlock은 플레이 진입 시 사라진다.
            // 색을 컴포넌트에 들려 보내서 런타임에 다시 칠하게 한다.
            var t = go.GetComponent<Proto.Feel.Tint>();
            if (t == null) t = go.AddComponent<Proto.Feel.Tint>();
            t.Set(c);
        }

        /// <summary>private [SerializeField] 필드를 에디터에서 채운다.</summary>
        static void Set(Object target, string field, object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogWarning("[Proto] 필드 없음: " + target.GetType().Name + "." + field);
                return;
            }

            if (value is Object o) prop.objectReferenceValue = o;
            else if (value is int i) prop.intValue = i;
            else if (value is float f) prop.floatValue = f;
            else if (value is bool b) prop.boolValue = b;

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetLayerMask(Object target, string field, int maskValue)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null) return;
            prop.intValue = maskValue;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
```

## Assets/_Project/Scripts/Enemy/EnemyController.cs

```csharp
using UnityEngine;
using Proto.Core;
using Proto.Data;

namespace Proto.Enemy
{
    /// <summary>
    /// 프로토타입용 최소 AI — 플레이어를 향해 걸어와 근접 공격.
    ///
    /// 피격 반응의 비대칭이 이 게임의 전투를 규정한다.
    ///   플레이어     경직 없음. 맞아도 행동이 끊기지 않는다 (채굴만 중단)
    ///   일반 몬스터  넉백 + 경직. 때리면 밀리고 멈칫한다 → 공간이 생긴다
    ///   강건한 챔피언 슈퍼아머. 넉백도 경직도 통하지 않는다 → 거리를 둬야 한다
    ///
    /// 챔피언은 별도 몬스터가 아니라 접두사가 붙은 일반 몬스터다 (MonsterAffix 참조).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(Health))]
    public class EnemyController : MonoBehaviour
    {
        [SerializeField] TuningConfig cfg;
        [SerializeField] Renderer visual;

        CharacterController _cc;
        Health _health;
        Transform _player;

        float _attackReadyAt;
        float _stunUntil;
        float _hasteUntil;

        Affix _affix = Affix.None;
        AffixDef _def = Affixes.Normal;

        /// <summary>접두사가 붙은 챔피언인가. 처치 보상이 이 값으로 갈린다.</summary>
        public bool IsChampion => _affix != Affix.None;
        public Affix Affix => _affix;
        public string DisplayName => IsChampion ? _def.Name : "";

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _health = GetComponent<Health>();
            if (visual == null) visual = GetComponentInChildren<Renderer>();
            _health.Damaged += OnDamaged;
            _health.Died += OnDied;
        }

        public void Configure(TuningConfig config, Transform player, Affix affix)
        {
            cfg = config;
            _player = player;
            _affix = affix;
            _def = Affixes.Get(affix);

            _health.Configure(cfg.enemyHealth * _def.HealthMul);
            transform.localScale = Vector3.one * _def.ScaleMul;
            SetColor(_def.Color);
        }

        void Update()
        {
            if (_health.IsDead || _player == null || cfg == null) return;

            // 피격 경직 중에는 움직이지도 때리지도 못한다.
            // 슈퍼아머 접두사는 애초에 스턴이 걸리지 않으므로 여기에 안 걸린다.
            if (Time.time < _stunUntil) return;

            var to = _player.position - transform.position;
            to.y = 0f;
            float dist = to.magnitude;

            float speed = cfg.enemyMoveSpeed * _def.SpeedMul;
            if (Time.time < _hasteUntil) speed *= cfg.howlHasteMultiplier;

            if (dist > cfg.enemyAttackRange)
            {
                _cc.SimpleMove(to.normalized * speed);
            }
            else if (Time.time >= _attackReadyAt)
            {
                _attackReadyAt = Time.time + cfg.enemyAttackCooldown;
                var hp = _player.GetComponent<Health>();
                if (hp != null) hp.TakeDamage(cfg.enemyDamage * _def.DamageMul, transform.position);
            }
        }

        /// <summary>"단단한" 접두사는 받는 피해를 줄인다. 그만큼 전투가 길어지고 피로도를 먹는다.</summary>
        public float ModifyIncomingDamage(float raw) => raw * _def.DamageTakenMul;

        void OnDamaged(float amount, Vector3 src)
        {
            if (Proto.Feel.Feel.I != null) Proto.Feel.Feel.I.Flash(visual);

            // 슈퍼아머 — 넉백도 경직도 없다. 맞아도 계속 걸어온다.
            if (_def.SuperArmor) return;

            _stunUntil = Time.time + cfg.enemyHitStun;

            var push = transform.position - src;
            push.y = 0f;
            if (push.sqrMagnitude > 0.001f)
                _cc.Move(push.normalized * cfg.enemyKnockback);
        }

        void OnDied()
        {
            if (_def.ExplodeOnDeath) Explode();
            if (_def.HasteAlliesOnDeath) HasteAllies();
        }

        /// <summary>"폭발하는" — 빨리 죽이면 손해다. 거리를 두고 처리해야 한다.</summary>
        void Explode()
        {
            if (_player == null) return;
            float d = Vector3.Distance(transform.position, _player.position);
            if (d > cfg.explodeRadius) return;

            var hp = _player.GetComponent<Health>();
            if (hp != null) hp.TakeDamage(cfg.explodeDamage, transform.position);

            if (Proto.Feel.Feel.I != null)
            {
                Proto.Feel.Feel.I.HitStop(1.5f);
                Proto.Feel.Feel.I.Shake((_player.position - transform.position).normalized, 1.6f);
            }
        }

        /// <summary>"울부짖는" — 죽으면 남은 적들이 빨라진다. 처치 순서가 중요해진다.</summary>
        void HasteAllies()
        {
            foreach (var e in FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
            {
                if (e == this) continue;
                e.ApplyHaste(cfg.howlHasteDuration);
            }
        }

        public void ApplyHaste(float duration)
        {
            _hasteUntil = Mathf.Max(_hasteUntil, Time.time + duration);
        }

        void SetColor(Color c)
        {
            var t = GetComponentInChildren<Proto.Feel.Tint>();
            if (t != null) { t.Set(c); return; }

            if (visual == null) return;
            var block = new MaterialPropertyBlock();
            visual.GetPropertyBlock(block);
            block.SetColor("_BaseColor", c);
            block.SetColor("_Color", c);
            visual.SetPropertyBlock(block);
        }
    }
}
```

## Assets/_Project/Scripts/Enemy/MonsterAffix.cs

```csharp
using UnityEngine;

namespace Proto.Enemy
{
    /// <summary>
    /// 챔피언 접두사. 던전앤파이터의 "챔피언" 등급을 벤치마킹한 구조다.
    ///
    /// 핵심: 챔피언은 별도 몬스터가 아니라 일반 몬스터에 붙는 강화 옵션이다.
    /// 몬스터 7종 × 접두사 6종이면 42가지 조합인데 만드는 건 13개뿐이다.
    ///
    /// 그리고 이 게임에서는 각 접두사가 "전투 시간"에 다르게 영향을 준다.
    /// 시간이 곧 피로도이므로, 접두사가 곧 탐험 거리에 대한 압박이 된다.
    ///   단단한   → 오래 걸린다
    ///   재빠른   → 쫓아다니느라 오래 걸린다
    ///   폭발하는 → 빨리 죽이면 손해다 (거리를 둬야 한다)
    /// </summary>
    public enum Affix
    {
        None,
        Stalwart,   // 강건한   — 슈퍼아머
        Swift,      // 재빠른   — 빠름
        Giant,      // 거대한   — 크고 강하고 느림
        Armored,    // 단단한   — 받는 피해 감소
        Volatile,   // 폭발하는 — 죽을 때 폭발
        Howling     // 울부짖는 — 죽을 때 남은 적 가속
    }

    public struct AffixDef
    {
        public string Name;
        public Color Color;

        public float HealthMul;
        public float SpeedMul;
        public float DamageMul;
        public float ScaleMul;
        public float DamageTakenMul;

        public bool SuperArmor;        // 넉백·경직 면역
        public bool ExplodeOnDeath;    // 죽을 때 주변에 피해
        public bool HasteAlliesOnDeath;// 죽을 때 남은 적 가속
    }

    public static class Affixes
    {
        /// <summary>접두사 없는 일반 몬스터.</summary>
        public static readonly AffixDef Normal = new AffixDef
        {
            Name = "",
            Color = new Color(0.85f, 0.35f, 0.35f),
            HealthMul = 1f, SpeedMul = 1f, DamageMul = 1f,
            ScaleMul = 1f, DamageTakenMul = 1f
        };

        public static AffixDef Get(Affix a)
        {
            switch (a)
            {
                case Affix.Stalwart:
                    return new AffixDef
                    {
                        Name = "강건한",
                        Color = new Color(0.78f, 0.36f, 0.95f),
                        HealthMul = 1.6f, SpeedMul = 0.9f, DamageMul = 1.2f,
                        ScaleMul = 1.15f, DamageTakenMul = 1f,
                        SuperArmor = true
                    };

                case Affix.Swift:
                    return new AffixDef
                    {
                        Name = "재빠른",
                        Color = new Color(0.30f, 0.92f, 0.82f),
                        HealthMul = 0.8f, SpeedMul = 1.7f, DamageMul = 0.9f,
                        ScaleMul = 0.9f, DamageTakenMul = 1f
                    };

                case Affix.Giant:
                    return new AffixDef
                    {
                        Name = "거대한",
                        Color = new Color(1.00f, 0.55f, 0.20f),
                        HealthMul = 3f, SpeedMul = 0.65f, DamageMul = 1.6f,
                        ScaleMul = 1.5f, DamageTakenMul = 1f
                    };

                case Affix.Armored:
                    return new AffixDef
                    {
                        Name = "단단한",
                        Color = new Color(0.60f, 0.72f, 0.88f),
                        HealthMul = 1.2f, SpeedMul = 0.85f, DamageMul = 1f,
                        ScaleMul = 1.1f, DamageTakenMul = 0.55f
                    };

                case Affix.Volatile:
                    return new AffixDef
                    {
                        Name = "폭발하는",
                        Color = new Color(1.00f, 0.30f, 0.18f),
                        HealthMul = 0.7f, SpeedMul = 1.15f, DamageMul = 0.8f,
                        ScaleMul = 1f, DamageTakenMul = 1f,
                        ExplodeOnDeath = true
                    };

                case Affix.Howling:
                    return new AffixDef
                    {
                        Name = "울부짖는",
                        Color = new Color(1.00f, 0.86f, 0.30f),
                        HealthMul = 1.1f, SpeedMul = 1f, DamageMul = 1f,
                        ScaleMul = 1.05f, DamageTakenMul = 1f,
                        HasteAlliesOnDeath = true
                    };

                default:
                    return Normal;
            }
        }

        static readonly Affix[] Pool =
        {
            Affix.Stalwart, Affix.Swift, Affix.Giant,
            Affix.Armored, Affix.Volatile, Affix.Howling
        };

        public static Affix Roll(System.Random rng) => Pool[rng.Next(Pool.Length)];
        public static Affix Roll() => Pool[Random.Range(0, Pool.Length)];
    }
}
```

## Assets/_Project/Scripts/Feel/Feel.cs

```csharp
using System.Collections;
using UnityEngine;
using Proto.Data;

namespace Proto.Feel
{
    /// <summary>
    /// 타격감은 에셋이 아니라 코드다. 모델 퀄리티는 거의 영향이 없다.
    ///   히트스톱(절반은 이것) · 히트 플래시 · 방향성 화면 흔들림
    /// 씬에 하나만 두고 전역으로 쓴다.
    /// </summary>
    public class Feel : MonoBehaviour
    {
        public static Feel I { get; private set; }

        [SerializeField] TuningConfig cfg;
        [SerializeField] Transform cameraRoot;

        Coroutine _shake;
        float _stopUntilRealtime;

        void Awake()
        {
            I = this;
            if (cameraRoot == null && Camera.main != null)
                cameraRoot = Camera.main.transform;
        }

        /// <summary>맞는 순간 아주 짧게 시간을 멈춘다. 이게 손맛의 절반이다.</summary>
        public void HitStop(float scale = 1f)
        {
            if (cfg == null) return;
            float dur = cfg.hitStopDuration * scale;
            float until = Time.realtimeSinceStartup + dur;
            if (until <= _stopUntilRealtime) return;   // 더 긴 정지가 진행 중이면 무시
            _stopUntilRealtime = until;
            StopCoroutine(nameof(HitStopRoutine));
            StartCoroutine(nameof(HitStopRoutine));
        }

        IEnumerator HitStopRoutine()
        {
            Time.timeScale = 0f;
            while (Time.realtimeSinceStartup < _stopUntilRealtime) yield return null;
            Time.timeScale = 1f;
        }

        /// <summary>방향성 있게, 짧고 세게.</summary>
        public void Shake(Vector3 direction, float scale = 1f)
        {
            if (cfg == null || cameraRoot == null) return;
            if (_shake != null) StopCoroutine(_shake);
            _shake = StartCoroutine(ShakeRoutine(direction.normalized, scale));
        }

        IEnumerator ShakeRoutine(Vector3 dir, float scale)
        {
            Vector3 origin = cameraRoot.localPosition;
            float t = 0f;
            float dur = cfg.shakeDuration;
            float amp = cfg.shakeAmplitude * scale;

            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float falloff = 1f - (t / dur);
                float wave = Mathf.Sin(t * 90f) * falloff * amp;
                cameraRoot.localPosition = origin + dir * wave;
                yield return null;
            }
            cameraRoot.localPosition = origin;
            _shake = null;
        }

        /// <summary>피격 프레임에 흰색으로 덮는다.</summary>
        public void Flash(Renderer r)
        {
            if (r == null || cfg == null) return;
            StartCoroutine(FlashRoutine(r));
        }

        IEnumerator FlashRoutine(Renderer r)
        {
            var block = new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            Color original = block.GetColor("_BaseColor");
            if (original == default) original = Color.white;

            block.SetColor("_BaseColor", Color.white * 4f);
            r.SetPropertyBlock(block);

            yield return new WaitForSecondsRealtime(cfg.hitFlashDuration);

            block.SetColor("_BaseColor", original);
            r.SetPropertyBlock(block);
        }
    }
}
```

## Assets/_Project/Scripts/Feel/Tint.cs

```csharp
using UnityEngine;

namespace Proto.Feel
{
    /// <summary>
    /// 색을 런타임에 입힌다.
    ///
    /// 에디터에서 Renderer.SetPropertyBlock으로 칠한 색은 직렬화되지 않아
    /// 플레이 모드에 들어가는 순간 사라진다. 그래서 색을 컴포넌트에 들고 있다가
    /// Awake에서 다시 칠한다.
    ///
    /// 프로토타입에서는 머티리얼 에셋을 만들지 않고 이 방식으로 때운다.
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class Tint : MonoBehaviour
    {
        [SerializeField] Color color = Color.white;
        [SerializeField] float emission = 0f;

        Renderer _r;

        public void Set(Color c, float emissionScale = 0f)
        {
            color = c;
            emission = emissionScale;
            Apply();
        }

        void Awake() => Apply();
        void OnValidate() => Apply();

        void Apply()
        {
            if (_r == null) _r = GetComponent<Renderer>();
            if (_r == null) return;

            var block = new MaterialPropertyBlock();
            _r.GetPropertyBlock(block);
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            if (emission > 0f) block.SetColor("_EmissionColor", color * emission);
            _r.SetPropertyBlock(block);
        }
    }
}
```

## Assets/_Project/Scripts/Mining/OreNode.cs

```csharp
using System;
using UnityEngine;
using Proto.Core;

namespace Proto.Mining
{
    public enum OreGrade { Common, Deep, Rare }

    /// <summary>
    /// 광맥. 캐는 데 2~3초가 걸리고 그동안 이동·공격이 불가능하며 피격 시 중단된다.
    /// 캐는 시간이 곧 피로도이므로 채굴에는 이미 비용이 붙어 있다.
    /// 별도의 페널티 장치를 얹지 않는다.
    /// </summary>
    public class OreNode : MonoBehaviour
    {
        [SerializeField] OreGrade grade = OreGrade.Common;
        [SerializeField] int yieldAmount = 1;
        [SerializeField] Renderer visual;

        public OreGrade Grade => grade;
        public bool Depleted { get; private set; }

        /// <summary>등급은 색과 발광으로 구분한다. 텍스트 없이 방에 들어가자마자 읽혀야 한다.</summary>
        static readonly Color[] GradeColors =
        {
            new Color(0.45f, 0.80f, 1.00f),  // Common — 옅은 청
            new Color(0.55f, 0.45f, 1.00f),  // Deep   — 보라
            new Color(1.00f, 0.78f, 0.30f)   // Rare   — 금
        };

        public event Action<OreNode> Mined;

        public void Configure(OreGrade g, int amount)
        {
            grade = g;
            yieldAmount = amount;
            ApplyColor();
        }

        void Awake() => ApplyColor();

        void ApplyColor()
        {
            if (visual == null) visual = GetComponentInChildren<Renderer>();
            if (visual == null) return;
            var block = new MaterialPropertyBlock();
            visual.GetPropertyBlock(block);
            var c = GradeColors[(int)grade];
            block.SetColor("_BaseColor", c);
            block.SetColor("_EmissionColor", c * 2.2f);
            visual.SetPropertyBlock(block);
        }

        public ResourceId ResourceId => grade switch
        {
            OreGrade.Common => Proto.Core.ResourceId.Ore,
            OreGrade.Deep => Proto.Core.ResourceId.Crystal,
            _ => Proto.Core.ResourceId.Alien
        };

        public int Yield => yieldAmount;

        public void Deplete()
        {
            if (Depleted) return;
            Depleted = true;
            Mined?.Invoke(this);
            gameObject.SetActive(false);
        }
    }
}
```

## Assets/_Project/Scripts/Player/PlayerController.cs

```csharp
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Proto.Core;
using Proto.Data;
using Proto.Mining;

namespace Proto.Player
{
    /// <summary>
    /// 던전앤파이터식 벨트스크롤 조작.
    ///   X = 좌우, Z = 깊이(위아래). 깊이 이동으로 공격을 피한다.
    ///   공격은 전방 부채꼴 판정. 히트스톱으로 손맛을 만든다.
    ///   채굴 중에는 이동·공격 불가, 피격 시 중단.
    ///
    /// 입력은 Input System을 코드로 직접 구성한다 (.inputactions 에셋 불필요).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(Health))]
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] TuningConfig cfg;
        [SerializeField] Transform visual;
        [SerializeField] LayerMask enemyMask;

        CharacterController _cc;
        Health _health;

        InputAction _move, _attack, _mine;
        Vector2 _moveInput;
        float _attackReadyAt;

        // 채굴 상태
        OreNode _target;
        float _mineProgress;
        public bool IsMining => _target != null;

        public event Action<float> MineProgressChanged;  // 0~1, 채굴 중이 아니면 -1
        public event Action<OreNode> OreMined;
        public event Action Attacked;

        public bool ControlEnabled { get; set; } = true;

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _health = GetComponent<Health>();
            if (cfg != null) _health.Configure(cfg.maxHealth);
            _health.Damaged += OnDamaged;

            BuildInput();
        }

        void BuildInput()
        {
            _move = new InputAction("Move", InputActionType.Value);
            _move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            _move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            _move.AddBinding("<Gamepad>/leftStick");

            _attack = new InputAction("Attack", InputActionType.Button);
            _attack.AddBinding("<Keyboard>/j");
            _attack.AddBinding("<Mouse>/leftButton");
            _attack.AddBinding("<Gamepad>/buttonWest");

            _mine = new InputAction("Mine", InputActionType.Button);
            _mine.AddBinding("<Keyboard>/k");
            _mine.AddBinding("<Gamepad>/buttonSouth");
        }

        void OnEnable() { _move.Enable(); _attack.Enable(); _mine.Enable(); }
        void OnDisable() { _move.Disable(); _attack.Disable(); _mine.Disable(); }

        void Update()
        {
            if (!ControlEnabled) return;

            _moveInput = _move.ReadValue<Vector2>();

            if (IsMining) { TickMining(); return; }

            TickMove();

            if (_attack.WasPressedThisFrame() && Time.time >= _attackReadyAt) DoAttack();
            if (_mine.WasPressedThisFrame()) TryStartMining();
        }

        void TickMove()
        {
            var dir = new Vector3(_moveInput.x, 0f, _moveInput.y);
            if (dir.sqrMagnitude > 1f) dir.Normalize();

            _cc.SimpleMove(dir * cfg.moveSpeed);

            // 좌우 방향만 바라본다 (벨트스크롤이므로 깊이 방향은 바라보지 않는다)
            if (visual != null && Mathf.Abs(_moveInput.x) > 0.01f)
                visual.localScale = new Vector3(Mathf.Sign(_moveInput.x), 1f, 1f);
        }

        void DoAttack()
        {
            _attackReadyAt = Time.time + cfg.attackCooldown;
            Attacked?.Invoke();

            Vector3 facing = visual != null && visual.localScale.x < 0f ? Vector3.left : Vector3.right;
            var hits = Physics.OverlapSphere(transform.position, cfg.attackRange, enemyMask);
            bool connected = false;

            foreach (var h in hits)
            {
                var to = h.transform.position - transform.position;
                to.y = 0f;
                if (Vector3.Angle(facing, to) > cfg.attackArcDegrees * 0.5f) continue;

                var hp = h.GetComponentInParent<Health>();
                if (hp == null || hp.IsDead) continue;
                if (hp.gameObject == gameObject) continue;

                // "단단한" 같은 접두사는 받는 피해를 줄인다.
                // 그만큼 전투가 길어지고, 길어진 만큼 피로도를 먹는다.
                float dmg = cfg.attackDamage;
                var ec = h.GetComponentInParent<Proto.Enemy.EnemyController>();
                if (ec != null) dmg = ec.ModifyIncomingDamage(dmg);

                hp.TakeDamage(dmg, transform.position);
                connected = true;
            }

            if (connected && Proto.Feel.Feel.I != null)
            {
                Proto.Feel.Feel.I.HitStop();
                Proto.Feel.Feel.I.Shake(facing);
            }
        }

        void TryStartMining()
        {
            OreNode best = null;
            float bestDist = float.MaxValue;

            foreach (var ore in FindObjectsByType<OreNode>(FindObjectsSortMode.None))
            {
                if (ore.Depleted) continue;
                float d = Vector3.Distance(transform.position, ore.transform.position);
                if (d < cfg.mineRange && d < bestDist) { best = ore; bestDist = d; }
            }

            if (best == null) return;
            _target = best;
            _mineProgress = 0f;
            MineProgressChanged?.Invoke(0f);
        }

        void TickMining()
        {
            // 캐는 도중에 키를 떼면 중단
            if (!_mine.IsPressed()) { CancelMining(); return; }

            _mineProgress += Time.deltaTime / Mathf.Max(0.01f, cfg.mineDuration);
            MineProgressChanged?.Invoke(Mathf.Clamp01(_mineProgress));

            if (_mineProgress >= 1f)
            {
                var ore = _target;
                _target = null;
                MineProgressChanged?.Invoke(-1f);
                ore.Deplete();
                OreMined?.Invoke(ore);
            }
        }

        void CancelMining()
        {
            _target = null;
            _mineProgress = 0f;
            MineProgressChanged?.Invoke(-1f);
        }

        void OnDamaged(float amount, Vector3 src)
        {
            // 플레이어는 피격 경직이 없다. 맞아도 이동·공격이 끊기지 않고
            // 넓백도 받지 않는다. 피드백은 화면 흔들림과 HP로만 준다.
            //
            // 대신 채굴은 중단된다 — 캐는 동안 무방비라는 것이
            // 이 게임에서 유일하게 피격이 플레이어를 멈추는 지점이다.
            if (IsMining)
            {
                _mineProgress *= cfg.mineProgressKeptOnHit;
                if (cfg.mineProgressKeptOnHit <= 0f) CancelMining();
                else MineProgressChanged?.Invoke(Mathf.Clamp01(_mineProgress));
            }

            if (Proto.Feel.Feel.I != null)
            {
                Proto.Feel.Feel.I.Shake((transform.position - src).normalized, 0.7f);
                Proto.Feel.Feel.I.Flash(visual != null ? visual.GetComponentInChildren<Renderer>() : null);
            }
        }

        void OnDrawGizmosSelected()
        {
            if (cfg == null) return;
            Gizmos.color = new Color(1f, 0.4f, 0.3f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, cfg.attackRange);
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, cfg.mineRange);
        }
    }
}
```

## Assets/_Project/Scripts/UI/HudView.cs

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Proto.Core;

namespace Proto.UI
{
    /// <summary>피로도 바, HP, 이번 판 획득물, 채굴 진행도.</summary>
    public class HudView : MonoBehaviour
    {
        [SerializeField] RunManager run;
        [SerializeField] Proto.Player.PlayerController player;

        [Header("Bars")]
        [SerializeField] Image staminaFill;
        [SerializeField] Image healthFill;
        [SerializeField] GameObject mineBarRoot;
        [SerializeField] Image mineFill;

        [Header("Text")]
        [SerializeField] TMP_Text staminaText;
        [SerializeField] TMP_Text lootText;
        [SerializeField] TMP_Text bossDistanceText;
        [SerializeField] MinimapView minimap;   // 보스방을 아는지 여기서 확인한다

        Health _hp;

        void Start()
        {
            _hp = player.GetComponent<Health>();

            run.Stamina.Changed += (cur, max) =>
            {
                if (staminaFill != null) staminaFill.fillAmount = max <= 0 ? 0 : cur / max;
                if (staminaText != null) staminaText.text = Mathf.CeilToInt(cur) + " / " + Mathf.CeilToInt(max);
            };

            _hp.Changed += (cur, max) =>
            {
                if (healthFill != null) healthFill.fillAmount = max <= 0 ? 0 : cur / max;
            };

            run.RunLoot.Changed += (a, b) => RefreshLoot();
            run.RunLoot.Cleared += RefreshLoot;
            run.MapChanged += RefreshBossDistance;

            player.MineProgressChanged += p =>
            {
                bool mining = p >= 0f;
                if (mineBarRoot != null) mineBarRoot.SetActive(mining);
                if (mining && mineFill != null) mineFill.fillAmount = p;
            };

            RefreshLoot();
            RefreshBossDistance();
        }

        void RefreshLoot()
        {
            if (lootText == null) return;
            var sb = new System.Text.StringBuilder();
            foreach (var kv in run.RunLoot.All)
                sb.Append(Label(kv.Key)).Append(' ').Append(kv.Value).Append("   ");
            lootText.text = sb.Length == 0 ? "획득물 없음" : sb.ToString();
        }

        void RefreshBossDistance()
        {
            if (bossDistanceText == null || run.Map == null) return;

            // 보스방 위치를 모르면 거리도 모른다.
            // 초반의 목표는 보스가 아니라 "캐서 무사히 나오기"다.
            bool known = minimap != null && minimap.BossRoomKnown;
            bossDistanceText.text = known
                ? "보스방까지 " + run.Map.ManhattanToBoss + "칸"
                : "보스방 위치 미확인";
        }

        static string Label(ResourceId id) => id switch
        {
            ResourceId.Gold => "골드",
            ResourceId.Ore => "원석",
            ResourceId.Crystal => "결정",
            ResourceId.Alien => "이질체",
            ResourceId.Essence => "정수",
            _ => "핵"
        };
    }
}
```

## Assets/_Project/Scripts/UI/MinimapView.cs

```csharp
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

        /// <summary>보스방 위치를 아는가. HUD의 거리 표시도 이 값을 따른다.</summary>
        public bool BossRoomKnown => showBossRoom;

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

            bool bossVisible = showBossRoom || _bossDiscovered;

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
```

## Assets/_Project/Scripts/UI/ResultView.cs

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Proto.Core;

namespace Proto.UI
{
    /// <summary>
    /// 귀환 직후 정산 화면. "한 번 더" 버튼을 누르게 만드는 지점이므로
    /// 3D 마을보다 여기에 공을 들이는 편이 낫다.
    /// </summary>
    public class ResultView : MonoBehaviour
    {
        [SerializeField] RunManager run;
        [SerializeField] GameObject root;
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_Text bodyText;
        [SerializeField] Button againButton;

        int _bestDepth;

        void Start()
        {
            root.SetActive(false);
            run.RunEnded += Show;
            run.RunStarted += () => root.SetActive(false);
            againButton.onClick.AddListener(() => run.Restart());
        }

        void Show(string reason)
        {
            root.SetActive(true);

            int reached = CountVisited();
            bool bossReached = run.Map.CurrentCell == run.Map.BossCell;

            // 보스방 위치를 모르면 거리도 알려주지 않는다.
            // 정산 화면으로 정보가 샐는 걸 막는다.
            var minimap = FindFirstObjectByType<MinimapView>();
            bool bossKnown = (minimap != null && minimap.BossRoomKnown) || bossReached;

            _bestDepth = Mathf.Max(_bestDepth, reached);

            titleText.text = bossReached ? "보스방 도달" : reason;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("탐험한 방   " + reached + "개   (최고 " + _bestDepth + "개)");
            if (!bossReached)
                sb.AppendLine(bossKnown
                    ? "보스방까지  " + run.Map.ManhattanToBoss + "칸 남음"
                    : "보스방은 찾지 못했다");
            sb.AppendLine();
            foreach (var kv in run.RunLoot.All)
                sb.AppendLine(Label(kv.Key) + "  +" + kv.Value);
            bodyText.text = sb.ToString();
        }

        int CountVisited()
        {
            int n = 0;
            var map = run.Map;
            for (int x = 0; x < map.Width; x++)
            for (int y = 0; y < map.Height; y++)
            {
                var r = map.At(new Vector2Int(x, y));
                if (r != null && r.Visited) n++;
            }
            return n;
        }

        static string Label(ResourceId id) => id switch
        {
            ResourceId.Gold => "골드",
            ResourceId.Ore => "원석",
            ResourceId.Crystal => "결정",
            ResourceId.Alien => "이질체",
            ResourceId.Essence => "정수",
            _ => "핵"
        };
    }
}
```

