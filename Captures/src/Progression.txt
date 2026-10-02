using System;
using System.Collections.Generic;
using UnityEngine;
using Proto.Data;

namespace Proto.Core
{
    public enum NodeId { Stamina, Step, Pickaxe, Power, BossSense }

    /// <summary>
    /// 스킬트리 노드 하나. 지금은 5개뿐이지만 구조는 114개까지 그대로 간다.
    /// </summary>
    public class UpgradeNode
    {
        public NodeId Id;
        public string Name;
        public string Effect;        // 한 줄 설명. 레벨마다 붙는 효과
        public ResourceId Cost;      // 어떤 자원을 먹는가
        public int BaseCost;
        public int CostStep;         // 레벨당 비용 증가
        public int MaxLevel;

        // ── 트리 배치 ──
        /// <summary>트리 화면에서의 위치 (중앙 시작점 = 0,0 / 위가 +y).</summary>
        public Vector2 Pos;
        /// <summary>선행 노드. 비어 있으면 중앙 시작점에 이어진다. 선행 노드를 1레벨 이상 찍어야 열린다.</summary>
        public NodeId[] Requires = new NodeId[0];
        /// <summary>노드 원 안에 쓰는 한 글자. 아이콘 그림이 없을 때만 보인다.</summary>
        public string Icon;
        /// <summary>Resources/Icons 안의 아이콘 파일 이름 (확장자 없이).</summary>
        public string IconKey;
        /// <summary>게임의 성격을 바꾸는 노드. 크게, 다른 색으로 그린다.</summary>
        public bool Keystone;
        /// <summary>레벨 → 누적 효과 설명. 툴팁의 '현재 → 다음'에 쓴다.</summary>
        public Func<int, string> Total;

        public int CostAt(int level) => BaseCost + CostStep * level;
    }

    /// <summary>
    /// 마을 성장. 던전에서 캐온 자원을 여기에 쓰고, 다음 판이 더 멀리 간다.
    ///
    /// 앞의 네 노드는 수치일 뿐이다. 다섯 번째 "보스 감지"가 이 게임의
    /// 성격을 바꾸는 노드다 — 채집 게임이 돌파 게임이 되는 지점.
    /// 수치 노드로는 만들 수 없는 체감이며, 트리를 114개로 늘려도
    /// 실제로 기억에 남는 건 이런 종류의 노드다.
    ///
    /// 자원 계열 분리를 지킨다.
    ///   채굴 자원(원석·결정) → 더 오래 버틴다
    ///   전투 자원(정수·핵)   → 더 강해진다 / 정보를 연다
    /// </summary>
    public class Progression
    {
        /// <summary>
        /// 트리 배치 — 중앙에서 갈래가 뻗는다.
        ///   위   탐험 (지구력 → 발걸음 → 보스 감지)
        ///   오른쪽 채굴 (곡괭이)
        ///   왼쪽  전투 (완력)
        /// 노드가 늘어나면 각 갈래 끝에 이어 붙인다.
        /// </summary>
        public static readonly UpgradeNode[] Nodes =
        {
            new UpgradeNode {
                Id = NodeId.Stamina, Name = "지구력", Effect = "최대 피로도 +20",
                Cost = ResourceId.Ore, BaseCost = 8, CostStep = 6, MaxLevel = 5,
                Pos = new Vector2(0f, 150f), Icon = "지", IconKey = "explore_endurance",
                Total = lv => "최대 피로도 +" + (20 * lv)
            },
            new UpgradeNode {
                Id = NodeId.Step, Name = "발걸음", Effect = "새 방 진입 비용 -2",
                Cost = ResourceId.Ore, BaseCost = 12, CostStep = 10, MaxLevel = 4,
                Pos = new Vector2(0f, 300f), Icon = "발", IconKey = "explore_footsteps", Requires = new[] { NodeId.Stamina },
                Total = lv => "새 방 진입 비용 -" + (2 * lv)
            },
            new UpgradeNode {
                Id = NodeId.BossSense, Name = "보스 감지", Effect = "미니맵에 보스방 위치가 표시된다",
                Cost = ResourceId.Core, BaseCost = 3, CostStep = 0, MaxLevel = 1,
                Pos = new Vector2(0f, 470f), Icon = "보", IconKey = "explore_boss_sense", Requires = new[] { NodeId.Step }, Keystone = true,
                Total = lv => lv > 0 ? "보스방 위치 표시" : "보스방 위치 모름"
            },
            new UpgradeNode {
                Id = NodeId.Pickaxe, Name = "곡괭이", Effect = "채굴 시간 -0.5초",
                Cost = ResourceId.Crystal, BaseCost = 6, CostStep = 5, MaxLevel = 3,
                Pos = new Vector2(170f, 0f), Icon = "곡", IconKey = "mine_pickaxe",
                Total = lv => "채굴 시간 -" + (0.5f * lv).ToString("0.0") + "초"
            },
            new UpgradeNode {
                Id = NodeId.Power, Name = "완력", Effect = "공격력 +5",
                Cost = ResourceId.Essence, BaseCost = 15, CostStep = 12, MaxLevel = 5,
                Pos = new Vector2(-170f, 0f), Icon = "완", IconKey = "combat_might",
                Total = lv => "공격력 +" + (5 * lv)
            }
        };

        readonly Dictionary<NodeId, int> _levels = new();

        /// <summary>노드를 찍었을 때. UI 갱신용.</summary>
        public event Action Changed;

        public int Level(NodeId id) => _levels.TryGetValue(id, out var v) ? v : 0;
        public bool IsMaxed(UpgradeNode n) => Level(n.Id) >= n.MaxLevel;
        public int NextCost(UpgradeNode n) => n.CostAt(Level(n.Id));

        /// <summary>선행 노드를 모두 1레벨 이상 찍었는가.</summary>
        public bool IsUnlocked(UpgradeNode n)
        {
            foreach (var r in n.Requires) if (Level(r) <= 0) return false;
            return true;
        }

        public bool CanBuy(UpgradeNode n, ResourceWallet bank) =>
            IsUnlocked(n) && !IsMaxed(n) && bank.Get(n.Cost) >= NextCost(n);

        public bool Buy(UpgradeNode n, ResourceWallet bank)
        {
            if (!CanBuy(n, bank)) return false;
            bank.Add(n.Cost, -NextCost(n));
            _levels[n.Id] = Level(n.Id) + 1;
            Changed?.Invoke();
            return true;
        }

        public static UpgradeNode Find(NodeId id) => Array.Find(Nodes, n => n.Id == id);

        /// <summary>
        /// 노드 효과를 런타임 설정에 반영한다.
        /// 반드시 원본 에셋 값에서 다시 계산한다 — 누적하면 판을 거듭할수록 터진다.
        /// </summary>
        public void ApplyTo(TuningConfig baseCfg, TuningConfig live)
        {
            live.maxStamina       = baseCfg.maxStamina + 20f * Level(NodeId.Stamina);
            live.staminaOnNewRoom = Mathf.Max(0f, baseCfg.staminaOnNewRoom - 2f * Level(NodeId.Step));
            live.mineDuration     = Mathf.Max(0.3f, baseCfg.mineDuration - 0.5f * Level(NodeId.Pickaxe));
            live.attackDamage     = baseCfg.attackDamage + 5f * Level(NodeId.Power);
        }
    }
}
