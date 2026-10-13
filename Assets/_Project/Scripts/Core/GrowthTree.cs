using System.Collections.Generic;
using UnityEngine;

namespace Proto.Core
{
    public enum GNodeKind { Rep, Unlock, Star, Skill, Bridge }

    /// <summary>성장 트리 노드 하나 (배치용). 실제로 살 수 있는 노드는 Real에 Progression의 NodeId가 들어 있다.</summary>
    public class GNode
    {
        public string Key, Name, Effect, IconKey, Branch;
        public GNodeKind Kind;
        public NodeId? Real;
        public readonly List<GNode> Parents = new();
        public readonly List<GNode> Children = new();
        public Vector2 Pos;
        /// <summary>몇 번째 고리 (0 = 안쪽). 바깥 고리는 누적 레벨이 차야 열린다.</summary>
        public int Ring;
        public bool Planned => Real == null;
    }

    /// <summary>
    /// 성장 트리 배치 — 코어(던전 입구) + 네 갈래 + 고리. 기획: 성장트리-배치안.md
    ///
    ///   방향이 곧 갈래 — 전투 왼쪽 위 · 탐험 오른쪽 위 · 고용 오른쪽 아래 · 장비 왼쪽 아래
    ///   줄기(해금 노드)가 바깥으로 뻗고, 반복 노드는 줄기 양옆에 가지로 붙는다 (일자로 늘어서지 않게)
    ///   전투 갈래 옆에 검술 부채꼴 — 스킬 해금 3단
    ///   고리 3개 — 2고리는 누적 20레벨, 3고리는 누적 60레벨에 열린다
    ///   맨 바깥 다리 노드 4개 — 이웃 갈래를 잇는다
    ///
    /// 위치는 아래 수치로 계산한다 (손으로 좌표를 찍지 않는다). 노드를 더하려면 Build()의 목록에 한 줄.
    /// 아직 게임에 없는 노드는 Real 없이 넣는다 — 화면에 '준비 중'으로 보이고 살 수 없다.
    /// </summary>
    public static class GrowthTree
    {
        public const float Ring1 = 560f, Ring2 = 900f, BridgeRadius = 1120f;
        public static readonly int[] RingNeed = { 0, 20, 60 };

        public static readonly List<GNode> All = new();
        static readonly Dictionary<string, GNode> _byKey = new();
        static readonly Dictionary<NodeId, GNode> _byReal = new();

        public static GNode Get(string key) => _byKey.TryGetValue(key, out var n) ? n : null;
        public static GNode Of(NodeId id) => _byReal.TryGetValue(id, out var n) ? n : null;

        /// <summary>갈래 방향 (도, 화면 위 = 90).</summary>
        public static float Angle(string branch) => branch switch
        {
            "combat" => 135f, "explore" => 45f, "hire" => -45f, "gear" => -135f, _ => 90f
        };

        public static string BranchName(string branch) => branch switch
        {
            "combat" => "전투", "explore" => "탐험", "hire" => "고용", "gear" => "장비", "bridge" => "다리", _ => ""
        };

        public static Color BranchColor(string branch) => branch switch
        {
            "combat" => new Color(0.95f, 0.42f, 0.36f),
            "explore" => new Color(0.36f, 0.78f, 0.80f),
            "hire" => new Color(0.66f, 0.48f, 0.92f),
            "gear" => new Color(0.95f, 0.70f, 0.30f),
            _ => new Color(1f, 0.80f, 0.42f)
        };

        static GrowthTree() => Build();

        // ───────────────────────────── 목록 ─────────────────────────────
        // 줄기 S(...) — 바깥으로 차례로. 각 줄기 노드 뒤의 가지 C(...)는 그 노드 옆에 붙는다.

        static void Build()
        {
            Branch("explore",
                S("explore_endurance", "지구력", "최대 피로도 +20", GNodeKind.Rep, NodeId.Stamina,
                    C("explore_thrift", "절약", "피로도 소모 속도 −5% (I~V)")),
                S("explore_footsteps", "발걸음", "새 방 진입 비용 −2", GNodeKind.Rep, NodeId.Step,
                    C("explore_quickness", "순발력", "이동 반응 속도 증가 (I~III)")),
                S("explore_boss_sense", "보스 감지", "미니맵에 보스방 위치가 표시된다", GNodeKind.Star, NodeId.BossSense,
                    C("explore_premonition", "예감", "인접 방의 유형 표시", GNodeKind.Unlock)),
                S("explore_path_foresight", "경로 예측", "보스방까지의 최단 경로를 미니맵에 표시", GNodeKind.Unlock, null,
                    C("explore_camp", "야영", "던전당 1회, 피로도 30% 회복", GNodeKind.Unlock),
                    C("explore_return_stone", "귀환석", "언제든 즉시 귀환, 획득물 확정", GNodeKind.Unlock)),
                S("explore_full_map", "지도", "방 배치 전체를 입장 시부터 공개", GNodeKind.Unlock, null,
                    C("explore_open_doors", "문 열림", "봉인된 방 진입 비용 감소")),
                S("explore_shortcut", "지름길", "클리어한 층의 중간 지점부터 시작 가능", GNodeKind.Star, null,
                    C("explore_second_wind", "두 번째 바람", "피로도 20% 이하에서 이속 +30%, 소모 −20%", GNodeKind.Unlock),
                    C("explore_deep_adaptation", "심층 적응", "깊은 방일수록 피로도 소모 감소", GNodeKind.Unlock)));

            Branch("gear",
                S("gear_bag_space", "큰 가방", "가방 +5칸 (처음 5칸 → 최대 30칸)", GNodeKind.Rep, NodeId.BagSpace,
                    C("gear_salvage", "분해 숙련", "분해 시 장비 가루 +20%", GNodeKind.Rep, NodeId.Salvage)),
                S("gear_loot_luck", "장비 감각", "몬스터 장비 드랍률 +25%", GNodeKind.Rep, NodeId.LootLuck,
                    C("gear_bulk_dismantle", "일괄 분해", "등급을 골라 한 번에 분해", GNodeKind.Unlock)),
                S("gear_chest_rare", "보물 감정", "보물상자에서 희귀 등급 이상만 나온다", GNodeKind.Star, NodeId.ChestRare,
                    C("gear_chest_extra", "넘치는 상자", "보물상자 장비 +1개", GNodeKind.Unlock, NodeId.ChestExtra)),
                S("gear_reforge", "재련", "장비 부가 능력치 1개를 다시 굴린다", GNodeKind.Unlock, null,
                    C("gear_lock_option", "옵션 고정", "제작 시 원하는 부가 능력치 1개 고정", GNodeKind.Unlock)),
                S("gear_set_bonus", "세트 장비", "같은 던전 장비 3 · 5부위 세트 효과", GNodeKind.Unlock, null,
                    C("gear_elite_drop", "엘리트 전리품", "엘리트 처치 시 장비 확정", GNodeKind.Unlock),
                    C("gear_legend_sense", "전설의 기운", "전설 등급 확률 2배", GNodeKind.Unlock)),
                S("gear_mythic", "신화", "전설 위의 여섯 번째 등급이 열린다", GNodeKind.Star, null));

            Branch("combat",
                S("combat_might", "완력", "공격력 +5", GNodeKind.Rep, NodeId.Power,
                    C("combat_critical", "치명", "치명타 확률 +3% (I~V)")),
                S("combat_training", "수련", "스킬 포인트 +1 (I~XX)", GNodeKind.Rep, null,
                    C("combat_toughness", "강인함", "최대 HP +10% (I~III)"),
                    C("combat_swiftness", "속공", "공격 속도 +6% (I~III)")),
                S("combat_cancel", "캔슬", "공격 후딜을 끊을 수 있음", GNodeKind.Unlock, null,
                    C("combat_input_buffer", "선입력", "입력 버퍼 증가 — 커맨드가 덜 씹힘", GNodeKind.Unlock),
                    C("combat_evasion", "회피", "무적 프레임 +2 (I~III)")),
                S("combat_execute", "처형", "HP 20% 이하 적 즉사", GNodeKind.Unlock, null,
                    C("combat_chain_strike", "연쇄", "처치 시 주변 적에게 피해", GNodeKind.Unlock),
                    C("combat_life_drain", "흡혈", "처치 시 HP 소량 회복", GNodeKind.Unlock)),
                S("combat_air_combo", "공중 콤보", "띄운 적을 공중에서 이어칠 수 있음", GNodeKind.Unlock, null,
                    C("combat_spoils", "전리품", "몬스터 자원 획득량 +20%"),
                    C("combat_elite_hunter", "엘리트 사냥", "엘리트 처치 시 핵 확정", GNodeKind.Unlock)),
                S("combat_trance", "무아지경", "노히트 클리어 시 피로도 소모 5초 정지", GNodeKind.Star, null,
                    C("combat_super_armor", "슈퍼아머", "강공격 중 경직 면역", GNodeKind.Unlock)));

            Branch("hire",
                S("hire_fighter_helper", "전투 알바", "따라다니며 몬스터와 싸운다", GNodeKind.Unlock, null,
                    C("hire_headcount", "고용 인원", "동행 인원 +1 (I~V)"),
                    C("hire_expertise", "숙련", "알바 공격 속도 +15% (I~V)"),
                    C("hire_helper_vitality", "알바 체력", "알바 HP +25% (I~III)")),
                S("hire_porter_helper", "운반 알바", "바닥의 자원을 자동으로 주워 온다", GNodeKind.Unlock, null,
                    C("hire_wage_deal", "급여 협상", "알바 유지 골드 −20% (I~III)"),
                    C("hire_orders", "지시", "알바에게 우선 작업을 지정", GNodeKind.Unlock)),
                S("hire_scout_helper", "정찰 알바", "인접 방의 내용을 미리 가져온다", GNodeKind.Unlock, null,
                    C("hire_shift_swap", "교대", "쓰러진 알바가 일정 시간 후 복귀", GNodeKind.Unlock),
                    C("hire_medic_helper", "위생병 알바", "플레이어 HP를 천천히 회복", GNodeKind.Unlock)),
                S("hire_teamwork", "팀워크", "알바가 처치한 적의 자원 +15%", GNodeKind.Unlock, null,
                    C("hire_escort_formation", "호위 대형", "알바가 피해 일부를 대신 받음", GNodeKind.Unlock),
                    C("hire_work_supervisor", "작업 감독", "플레이어 전투 중 알바 공격 속도 +30%", GNodeKind.Unlock)),
                S("hire_two_shifts", "2교대", "다음 방으로 가도 알바가 이전 방에서 계속 싸운다", GNodeKind.Star, null,
                    C("hire_auto_roster", "자동 편성", "입장 시 최적 알바 조합 자동 선택", GNodeKind.Unlock),
                    C("hire_guild", "길드", "알바 인원 상한 +3")));

            SkillFan();

            // 다리 — 이웃 갈래 끝을 잇는다 (둘 중 하나만 찍어도 열린다)
            Bridge("bridge_annihilation", "섬멸", "방의 마지막 적 처치 시 피로도 소량 회복", "combat_annihilation", 90f, "combat_trance", "explore_shortcut");
            Bridge("bridge_auto_salvage", "자동 분해", "고른 등급 이하 장비는 줍는 즉시 가루가 된다", "hire_porter_helper", -90f, "hire_two_shifts", "gear_mythic");
            Bridge("bridge_weapon_master", "무기 숙련", "무기 주 능력치 +20%", "combat_might", 180f, "gear_mythic", "combat_trance");
            Bridge("bridge_scout_link", "정찰 연계", "정찰 알바가 가져온 방 정보가 미니맵에 남는다", "hire_scout_helper", 0f, "explore_shortcut", "hire_two_shifts");

            foreach (var n in All)
            {
                float r = n.Pos.magnitude;
                n.Ring = r <= Ring1 ? 0 : r <= Ring2 ? 1 : 2;
            }
        }

        // ───────────────────────────── 배치 계산 ─────────────────────────────

        struct ChildDef { public string Key, Name, Effect; public GNodeKind Kind; public NodeId? Real; }
        struct SpineDef { public string Key, Name, Effect; public GNodeKind Kind; public NodeId? Real; public ChildDef[] Children; }

        static ChildDef C(string key, string name, string effect, GNodeKind kind = GNodeKind.Rep, NodeId? real = null) =>
            new ChildDef { Key = key, Name = name, Effect = effect, Kind = kind, Real = real };

        static SpineDef S(string key, string name, string effect, GNodeKind kind, NodeId? real, params ChildDef[] children) =>
            new SpineDef { Key = key, Name = name, Effect = effect, Kind = kind, Real = real, Children = children };

        const float SpineStart = 210f, SpineStep = 160f, Zig = 24f, Side0 = 118f, SideStep = 104f, Out0 = 20f, OutStep = 30f;

        static void Branch(string branch, params SpineDef[] spine)
        {
            float a = Angle(branch) * Mathf.Deg2Rad;
            var u = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            var v = new Vector2(-u.y, u.x);
            GNode prev = null;
            for (int i = 0; i < spine.Length; i++)
            {
                var s = spine[i];
                float side = i % 2 == 0 ? 1f : -1f;   // 가지는 줄기 양옆으로 번갈아
                var pos = u * (SpineStart + i * SpineStep) + v * (-side * Zig);
                var node = Add(s.Key, s.Name, s.Effect, s.Kind, branch, s.Real, pos, prev);
                GNode last = node;
                for (int j = 0; j < s.Children.Length; j++)
                {
                    var c = s.Children[j];
                    var cp = pos + v * side * (Side0 + SideStep * j) + u * (Out0 + OutStep * j);
                    last = Add(c.Key, c.Name, c.Effect, c.Kind, branch, c.Real, cp, last);
                }
                prev = node;
            }
        }

        /// <summary>검술 — 전투 갈래 옆의 부채꼴. 수련에서 뻗는다. 1단 3개 → 2단 4개 → 궁극기.</summary>
        static void SkillFan()
        {
            const float FanShift = 0f;   // 전투 갈래 방향이 바뀌면 부채꼴도 같이 돌린다
            var root = Get("combat_training");
            var t1 = new[] { ("skill_ground_slam", "내려찍기"), ("skill_whirlwind", "회전베기"), ("skill_rapid_thrusts", "연속 찌르기"), ("skill_gale_slash", "질풍참") };
            var t2 = new[] { ("skill_sword_wave", "검기 날리기"), ("skill_iaido", "발도술"), ("skill_counter_stance", "반격 자세"), ("skill_falling_blossom", "낙화참") };
            var row1 = new List<GNode>();
            for (int i = 0; i < t1.Length; i++)
                row1.Add(Add(t1[i].Item1, t1[i].Item2, "스킬 해금 (핵 2) — 1단", GNodeKind.Skill, "combat", null, Polar(FanShift + 167f + i * 12f, 640f), root));
            var row2 = new List<GNode>();
            for (int i = 0; i < t2.Length; i++)
            {
                float ang = FanShift + 162f + i * 11f;
                var parent = row1[Mathf.Clamp(Mathf.RoundToInt(i * (t1.Length - 1) / (float)(t2.Length - 1)), 0, row1.Count - 1)];
                row2.Add(Add(t2[i].Item1, t2[i].Item2, "스킬 해금 (핵 4) — 2단, 1단 하나 이상", GNodeKind.Skill, "combat", null, Polar(ang, 790f), parent));
            }
            var ult = Add("skill_earth_splitter", "대지 가르기", "궁극기 해금 (핵 8) — 2단 둘 이상", GNodeKind.Star, "combat", null, Polar(FanShift + 178f, 935f), row2[1]);
            Link(row2[2], ult);
        }

        static void Bridge(string key, string name, string effect, string icon, float angle, string a, string b)
        {
            var n = Add(key, name, effect, GNodeKind.Bridge, "bridge", null, Polar(angle, BridgeRadius), Get(a));
            n.IconKey = icon;
            Link(Get(b), n);
        }

        static Vector2 Polar(float deg, float r) => new Vector2(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad)) * r;

        static GNode Add(string key, string name, string effect, GNodeKind kind, string branch, NodeId? real, Vector2 pos, GNode parent)
        {
            var n = new GNode { Key = key, Name = name, Effect = effect, Kind = kind, Branch = branch, Real = real, Pos = pos, IconKey = key };
            All.Add(n);
            _byKey[key] = n;
            if (real.HasValue) _byReal[real.Value] = n;
            if (parent != null) Link(parent, n);
            return n;
        }

        static void Link(GNode parent, GNode child)
        {
            if (parent == null || child == null || child.Parents.Contains(parent)) return;
            child.Parents.Add(parent);
            parent.Children.Add(child);
        }
    }
}
