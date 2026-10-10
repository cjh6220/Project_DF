using System;
using System.Collections.Generic;
using UnityEngine;
using Proto.Data;

namespace Proto.Core
{
    /// <summary>장비 부위 7칸. 순서가 곧 장비 창의 칸 순서다.</summary>
    public enum EquipSlot { Weapon, Helmet, Armor, Gloves, Boots, Earring, Necklace }

    /// <summary>등급 5단계 — 일반 · 고급 · 희귀 · 영웅 · 전설 (흰 · 파랑 · 보라 · 분홍 · 주황).</summary>
    public enum Rarity { Common, Uncommon, Rare, Epic, Legendary }

    /// <summary>장비가 올려 주는 능력치. 퍼센트 능력치는 0.05 = 5%로 담는다.</summary>
    public enum StatId { Attack, MaxHealth, Defense, CritChance, CritDamage, AttackSpeed, MoveSpeed, MaxStamina, StaminaSave }

    [Serializable]
    public class StatRoll
    {
        public StatId stat;
        public float value;
        public StatRoll() { }
        public StatRoll(StatId s, float v) { stat = s; value = v; }
    }

    /// <summary>
    /// 장비 한 개. JsonUtility로 그대로 저장된다 (필드 이름을 바꾸면 옛 저장이 깨진다).
    ///   tier  던전 위험도(1~4) — 깊은 던전일수록 수치가 크다
    ///   main  부위마다 정해진 주 능력치
    ///   subs  등급만큼 붙는 무작위 부가 능력치 (일반 0 · 고급 1 · 희귀 2 · 영웅 3 · 전설 4)
    /// </summary>
    [Serializable]
    public class Item
    {
        public string uid;
        public EquipSlot slot;
        public Rarity rarity;
        public int tier = 1;
        public string name;
        /// <summary>
        /// 장비 종류 키 — t1_weapon_base / t1_weapon_epic / t1_weapon_legend.
        /// 아이콘(Icons/item_&lt;key&gt;)과 나중에 붙일 모델이 이 키로 찾는다. 일반~희귀는 같은 base를 쓴다.
        /// </summary>
        public string key;
        public StatRoll main;
        public List<StatRoll> subs = new();
        /// <summary>얻은 순서 — 가방 정렬과 "새 장비" 표시에 쓴다.</summary>
        public long seq;
        /// <summary>장비 창에서 아직 안 본 장비.</summary>
        public bool fresh = true;

        public IEnumerable<StatRoll> AllStats()
        {
            if (main != null) yield return main;
            if (subs != null) foreach (var s in subs) yield return s;
        }

        /// <summary>대충 얼마나 좋은가 — 비교 화살표와 정렬에 쓴다.</summary>
        public float Score
        {
            get
            {
                float s = 0f;
                foreach (var r in AllStats()) s += r.value * ItemDb.Weight(r.stat);
                return s;
            }
        }
    }

    /// <summary>
    /// 장비 표 — 이름 · 능력치 범위 · 드랍 확률. 지금 값은 전부 임시다 (드랍 테이블은 나중에 따로 쓴다).
    /// 숫자를 고칠 곳은 여기 한 군데다.
    /// </summary>
    public static class ItemDb
    {
        // ───────────────────────────── 이름 · 색 ─────────────────────────────

        public static string SlotName(EquipSlot s) => s switch
        {
            EquipSlot.Weapon => "무기",
            EquipSlot.Helmet => "투구",
            EquipSlot.Armor => "갑옷",
            EquipSlot.Gloves => "장갑",
            EquipSlot.Boots => "신발",
            EquipSlot.Earring => "귀걸이",
            _ => "목걸이"
        };

        /// <summary>
        /// 장비 기본 이름 — 티어(던전) × 부위. 등급 수식어가 앞에 붙는다 ("전설의 대나무 목도").
        /// 장비 리스트 문서: claude/장비-시스템.md
        /// </summary>
        public static readonly string[][] BaseNames =
        {
            // 무기 · 투구 · 갑옷 · 장갑 · 신발 · 귀걸이 · 목걸이
            new[] { "대나무 목도", "짚 삿갓", "무명 도복", "가죽 토시", "짚신", "도토리 귀걸이", "나무 염주" },          // 1 숲
            new[] { "녹슨 카타나", "철 진가사", "사슬 갑옷", "철 토시", "광부 각반", "청동 귀걸이", "광석 염주" },        // 2 갱도
            new[] { "서리 타치", "은빛 카부토", "설원 갑주", "털가죽 코테", "설상 각반", "빙정 귀걸이", "설백 곡옥" },     // 3 설산
            new[] { "균열의 요도", "마귀 카부토", "흑요 갑주", "마수 코테", "공허 각반", "균열석 귀걸이", "마령 곡옥" },   // 4 균열
        };

        public static string BaseName(EquipSlot s, int tier) => BaseNames[Mathf.Clamp(tier, 1, 4) - 1][(int)s];

        /// <summary>영웅 등급 고유 장비 — 티어 × 부위. 이름 그대로 쓴다 (수식어 없음). 모델이 정해지면 다시 짓는다.</summary>
        public static readonly string[][] EpicNames =
        {
            new[] { "숲지기의 목검", "들개 가면", "덩굴 엮은 갑의", "곰발 토시", "바람결 짚신", "이슬방울 귀걸이", "고목 염주" },
            new[] { "흑철 카타나", "갱도 감독관 투구", "흑철 찰갑", "용광로 토시", "철징 각반", "홍옥 귀걸이", "광맥석 목걸이" },
            new[] { "빙결 타치", "설표 카부토", "백은 오요로이", "서릿발 코테", "설원 질주 각반", "빙화 귀걸이", "설녀의 곡옥" },
            new[] { "마기 서린 요도", "오니 카부토", "마장 흑갑", "심연 코테", "그림자 각반", "마안 귀걸이", "공허의 곡옥" },
        };

        /// <summary>전설 등급 고유 장비 — 티어 × 부위.</summary>
        public static readonly string[][] LegendNames =
        {
            new[] { "천년목 검 · 코다마", "산신의 탈", "녹림왕의 갑주", "백호 발톱", "천리 짚신", "정령의 방울", "세계수 염주" },
            new[] { "해골왕의 대검", "해골왕의 관", "망자의 갑주", "대장장이 신의 손", "지저 행군 각반", "용암석 귀걸이", "만년 광휘석" },
            new[] { "설월도 · 시라유키", "대천구의 탈", "만년설 갑주", "예티 왕의 손", "눈보라 걸음", "북극성 귀걸이", "영구동토의 심장" },
            new[] { "마왕검 · 아마츠", "마왕의 투구", "균열군주 갑주", "파멸의 손", "차원 보행자", "종말의 종", "균열의 핵" },
        };

        /// <summary>등급이 고르는 장비 종류 — 일반 · 고급 · 희귀는 base, 영웅 epic, 전설 legend.</summary>
        public static string Grade(Rarity r) => r == Rarity.Legendary ? "legend" : r == Rarity.Epic ? "epic" : "base";
        public static string KeyOf(EquipSlot s, int tier, Rarity r) =>
            $"t{Mathf.Clamp(tier, 1, 4)}_{s.ToString().ToLowerInvariant()}_{Grade(r)}";

        /// <summary>장비 이름 — 일반~희귀는 "수식어 + 기본 이름", 영웅 · 전설은 고유 이름.</summary>
        public static string NameOf(EquipSlot s, int tier, Rarity r)
        {
            int t = Mathf.Clamp(tier, 1, 4) - 1;
            if (r == Rarity.Legendary) return LegendNames[t][(int)s];
            if (r == Rarity.Epic) return EpicNames[t][(int)s];
            var words = RarityWord[(int)r];
            return $"{words[UnityEngine.Random.Range(0, words.Length)]} {BaseNames[t][(int)s]}";
        }

        /// <summary>나온 던전 깊이 — 설명 줄에 쓴다.</summary>
        public static readonly string[] TierName = { "숲", "갱도", "설산", "균열" };

        /// <summary>등급마다 붙는 수식어. 같은 던전 장비라도 등급이 이름에서 보이게.</summary>
        static readonly string[][] RarityWord =
        {
            new[] { "낡은", "투박한", "흔한" },
            new[] { "단단한", "손질된", "쓸만한" },
            new[] { "정교한", "빛나는", "날선" },
            new[] { "명장의", "영웅의", "피에 젖은" },
            new[] { "전설의", "신령의", "천하의" },
        };

        public static string RarityName(Rarity r) => r switch
        {
            Rarity.Common => "일반",
            Rarity.Uncommon => "고급",
            Rarity.Rare => "희귀",
            Rarity.Epic => "영웅",
            _ => "전설"
        };

        public static Color RarityColor(Rarity r) => r switch
        {
            Rarity.Common => new Color(0.86f, 0.86f, 0.86f),
            Rarity.Uncommon => new Color(0.42f, 0.70f, 1f),
            Rarity.Rare => new Color(0.74f, 0.46f, 1f),
            Rarity.Epic => new Color(1f, 0.42f, 0.74f),
            _ => new Color(1f, 0.62f, 0.16f)
        };

        public static string Hex(Rarity r) => ColorUtility.ToHtmlStringRGB(RarityColor(r));

        /// <summary>등급 색을 입힌 이름.</summary>
        public static string ColoredName(Item it) => it == null ? "" : $"<color=#{Hex(it.rarity)}>{it.name}</color>";

        public static string StatName(StatId s) => s switch
        {
            StatId.Attack => "공격력",
            StatId.MaxHealth => "최대 체력",
            StatId.Defense => "피해 감소",
            StatId.CritChance => "치명타 확률",
            StatId.CritDamage => "치명타 피해",
            StatId.AttackSpeed => "공격 속도",
            StatId.MoveSpeed => "이동 속도",
            StatId.MaxStamina => "최대 피로도",
            _ => "피로도 소모 감소"
        };

        public static bool IsPercent(StatId s) =>
            s == StatId.Defense || s == StatId.CritChance || s == StatId.CritDamage ||
            s == StatId.AttackSpeed || s == StatId.MoveSpeed || s == StatId.StaminaSave;

        public static string Format(StatId s, float v) =>
            IsPercent(s) ? $"+{v * 100f:0.#}%" : $"+{Mathf.RoundToInt(v)}";

        public static string Line(StatRoll r) => r == null ? "" : $"{StatName(r.stat)} {Format(r.stat, r.value)}";

        /// <summary>점수용 가중치 — 능력치 1단위가 대략 공격력 몇과 맞먹는가.</summary>
        public static float Weight(StatId s) => s switch
        {
            StatId.Attack => 1f,
            StatId.MaxHealth => 0.3f,
            StatId.Defense => 120f,
            StatId.CritChance => 120f,
            StatId.CritDamage => 40f,
            StatId.AttackSpeed => 110f,
            StatId.MoveSpeed => 80f,
            StatId.MaxStamina => 0.6f,
            _ => 90f
        };

        // ───────────────────────────── 능력치 범위 (임시) ─────────────────────────────

        /// <summary>부위마다 정해진 주 능력치와 1티어 일반 기준값.</summary>
        public static StatRoll MainBase(EquipSlot s) => s switch
        {
            EquipSlot.Weapon => new StatRoll(StatId.Attack, 8f),
            EquipSlot.Helmet => new StatRoll(StatId.MaxHealth, 20f),
            EquipSlot.Armor => new StatRoll(StatId.MaxHealth, 30f),
            EquipSlot.Gloves => new StatRoll(StatId.Attack, 4f),
            EquipSlot.Boots => new StatRoll(StatId.MoveSpeed, 0.03f),
            EquipSlot.Earring => new StatRoll(StatId.CritChance, 0.02f),
            _ => new StatRoll(StatId.MaxStamina, 8f)
        };

        /// <summary>부가 능력치 범위 (1티어 기준 — 티어 배율만 곱한다, 등급은 개수로 갈린다).</summary>
        public static Vector2 SubRange(StatId s) => s switch
        {
            StatId.Attack => new Vector2(2f, 4f),
            StatId.MaxHealth => new Vector2(8f, 15f),
            StatId.Defense => new Vector2(0.01f, 0.02f),
            StatId.CritChance => new Vector2(0.01f, 0.02f),
            StatId.CritDamage => new Vector2(0.04f, 0.08f),
            StatId.AttackSpeed => new Vector2(0.02f, 0.03f),
            StatId.MoveSpeed => new Vector2(0.01f, 0.02f),
            StatId.MaxStamina => new Vector2(3f, 5f),
            _ => new Vector2(0.01f, 0.02f)
        };

        /// <summary>
        /// 등급 배율 (주 능력치) — 일반 1 · 고급 1.2 · 희귀 1.45 · 영웅 1.75 · 전설 2.1
        /// 티어 배율 — 1 · 1.45 · 2.1 · 3.05 (한 티어 오를 때마다 ×1.45)
        /// 그래서 N티어 전설(2.1) ≈ N+1티어 희귀(1.45 × 1.45 = 2.1). 전설은 부가 능력치가 둘 더 붙어 살짝 앞선다.
        /// </summary>
        public static readonly float[] RarityMainMul = { 1f, 1.2f, 1.45f, 1.75f, 2.1f };
        public static readonly float[] TierMuls = { 1f, 1.45f, 2.1f, 3.05f };
        public static float TierMul(int tier) => TierMuls[Mathf.Clamp(tier, 1, 4) - 1];

        /// <summary>합계 상한 — 퍼센트 능력치가 끝없이 쌓이지 않게.</summary>
        public static float Cap(StatId s) => s switch
        {
            StatId.Defense => 0.6f,
            StatId.CritChance => 1f,
            StatId.AttackSpeed => 0.6f,
            StatId.MoveSpeed => 0.5f,
            StatId.StaminaSave => 0.5f,
            _ => float.MaxValue
        };

        // ───────────────────────────── 드랍 확률 (임시) ─────────────────────────────

        /// <summary>일반 몬스터 · 엘리트가 장비를 떨어뜨릴 확률 (성장 노드 '장비 감각'이 곱해진다).</summary>
        public const float NormalDropChance = 0.04f, EliteDropChance = 0.25f;

        /// <summary>등급 가중치 — 일반 · 고급 · 희귀 · 영웅 · 전설.</summary>
        public static readonly float[] MonsterWeights = { 60f, 28f, 10f, 1.8f, 0.2f };
        public static readonly float[] ChestWeights = { 0f, 50f, 35f, 12f, 3f };
        /// <summary>성장 노드 '보물 감정' — 상자는 희귀 이상만.</summary>
        public static readonly float[] ChestRareWeights = { 0f, 0f, 75f, 20f, 5f };
        public static readonly float[] CraftWeights = { 30f, 40f, 22f, 7f, 1f };

        public const int ChestItemCount = 3;

        // ── 분해 · 제작 ──
        static readonly int[] DustByRarity = { 2, 5, 12, 30, 80 };
        public static int DismantleDust(Item it, float bonus = 0f) =>
            it == null ? 0 : Mathf.Max(1, Mathf.RoundToInt(DustByRarity[(int)it.rarity] * TierMul(it.tier) * (1f + bonus)));

        public static int CraftDust(int tier) => Mathf.RoundToInt(40 * TierMul(tier));
        public static int CraftGold(int tier) => Mathf.RoundToInt(100 * TierMul(tier));

        // ───────────────────────────── 굴리기 ─────────────────────────────

        static long _seq;

        public static Rarity RollRarity(float[] weights)
        {
            float total = 0f;
            foreach (var w in weights) total += w;
            float r = UnityEngine.Random.value * total;
            for (int i = 0; i < weights.Length; i++)
            {
                r -= weights[i];
                if (weights[i] > 0f && r <= 0f) return (Rarity)i;
            }
            for (int i = weights.Length - 1; i >= 0; i--) if (weights[i] > 0f) return (Rarity)i;
            return Rarity.Common;
        }

        public static EquipSlot RandomSlot() => (EquipSlot)UnityEngine.Random.Range(0, 7);

        /// <summary>장비 하나를 만든다. slot이 없으면 부위도 무작위.</summary>
        public static Item Roll(int tier, Rarity rarity, EquipSlot? slot = null)
        {
            tier = Mathf.Clamp(tier, 1, 4);
            var s = slot ?? RandomSlot();
            var it = new Item
            {
                uid = Guid.NewGuid().ToString("N").Substring(0, 12),
                slot = s,
                rarity = rarity,
                tier = tier,
                seq = DateTime.UtcNow.Ticks + (++_seq),
            };
            it.name = NameOf(s, tier, rarity);
            it.key = KeyOf(s, tier, rarity);

            var mb = MainBase(s);
            float mainV = mb.value * RarityMainMul[(int)rarity] * TierMul(tier) * UnityEngine.Random.Range(0.92f, 1.08f);
            it.main = new StatRoll(mb.stat, Round(mb.stat, mainV));

            // 부가 능력치 — 서로 다르고, 주 능력치와도 겹치지 않게
            var pool = new List<StatId>((StatId[])Enum.GetValues(typeof(StatId)));
            pool.Remove(mb.stat);
            int count = (int)rarity;
            for (int i = 0; i < count && pool.Count > 0; i++)
            {
                var st = pool[UnityEngine.Random.Range(0, pool.Count)];
                pool.Remove(st);
                var range = SubRange(st);
                float v = UnityEngine.Random.Range(range.x, range.y) * TierMul(tier);
                it.subs.Add(new StatRoll(st, Round(st, v)));
            }
            return it;
        }

        static float Round(StatId s, float v) => IsPercent(s) ? Mathf.Round(v * 1000f) / 1000f : Mathf.Max(1f, Mathf.Round(v));

        /// <summary>일반 몬스터 처치 — 장비가 나오면 돌려준다, 아니면 null.</summary>
        public static Item RollMonsterDrop(int tier, bool elite, float luckMul)
        {
            float chance = (elite ? EliteDropChance : NormalDropChance) * luckMul;
            if (UnityEngine.Random.value >= chance) return null;
            return Roll(tier, RollRarity(MonsterWeights));
        }

        public static List<Item> RollChest(int tier, bool rareOnly, int count)
        {
            var list = new List<Item>();
            for (int i = 0; i < count; i++) list.Add(Roll(tier, RollRarity(rareOnly ? ChestRareWeights : ChestWeights)));
            return list;
        }
    }

    /// <summary>
    /// 가방 + 장착 칸. 장착한 장비는 가방에서 빠진다.
    /// 상태만 담는다 — 창(InventoryView · SmithView)은 보여 주고 요청만 한다.
    /// </summary>
    public class Inventory
    {
        /// <summary>처음 가방 5칸 — 성장 '큰 가방'이 5칸씩, 최대 30칸.</summary>
        public const int BaseCapacity = 5;
        public const int MaxCapacity = 30;

        readonly List<Item> _bag = new();
        readonly Item[] _equipped = new Item[7];

        /// <summary>성장 노드 '큰 가방'이 늘린다 (레벨당 5칸).</summary>
        public int BonusCapacity { get; set; }
        public int Capacity => BaseCapacity + BonusCapacity;
        public IReadOnlyList<Item> Bag => _bag;
        public bool Full => _bag.Count >= Capacity;
        public int Count => _bag.Count;

        public event Action Changed;

        public Item Equipped(EquipSlot s) => _equipped[(int)s];

        public bool Add(Item it)
        {
            if (it == null || Full) return false;
            _bag.Add(it);
            Changed?.Invoke();
            return true;
        }

        public bool Remove(Item it)
        {
            if (it == null || !_bag.Remove(it)) return false;
            Changed?.Invoke();
            return true;
        }

        /// <summary>가방의 장비를 낀다. 그 칸에 끼고 있던 것은 가방으로 돌아간다.</summary>
        public bool Equip(Item it)
        {
            if (it == null || !_bag.Contains(it)) return false;
            int i = (int)it.slot;
            var old = _equipped[i];
            _bag.Remove(it);
            _equipped[i] = it;
            it.fresh = false;
            if (old != null) _bag.Add(old);
            Changed?.Invoke();
            return true;
        }

        /// <summary>벗는다. 가방이 꽉 차 있으면 벗지 못한다.</summary>
        public bool Unequip(EquipSlot s)
        {
            var it = _equipped[(int)s];
            if (it == null || Full) return false;
            _equipped[(int)s] = null;
            _bag.Add(it);
            Changed?.Invoke();
            return true;
        }

        public void MarkSeen()
        {
            bool any = false;
            foreach (var it in _bag) if (it.fresh) { it.fresh = false; any = true; }
            if (any) Changed?.Invoke();
        }

        public int FreshCount { get { int n = 0; foreach (var it in _bag) if (it.fresh) n++; return n; } }

        /// <summary>장착한 장비 능력치의 합 (상한 적용).</summary>
        public float Total(StatId s)
        {
            float v = 0f;
            foreach (var it in _equipped)
                if (it != null) foreach (var r in it.AllStats()) if (r.stat == s) v += r.value;
            return Mathf.Min(v, ItemDb.Cap(s));
        }

        /// <summary>
        /// 장비 능력치를 런타임 설정에 더한다. RunManager.RecalcStats가 원본 값으로 되돌린 뒤 부른다.
        /// </summary>
        public void ApplyTo(TuningConfig live)
        {
            live.attackDamage += Total(StatId.Attack);
            live.maxHealth += Total(StatId.MaxHealth);
            live.damageReduction = Mathf.Clamp01(live.damageReduction + Total(StatId.Defense));
            live.critChance = Mathf.Clamp01(live.critChance + Total(StatId.CritChance));
            live.critDamage += Total(StatId.CritDamage);
            live.attackCooldown /= 1f + Total(StatId.AttackSpeed);
            float ms = 1f + Total(StatId.MoveSpeed);
            live.moveSpeed *= ms;
            live.walkSpeed *= ms;
            live.maxStamina += Total(StatId.MaxStamina);
            live.staminaDrainPerSecond *= 1f - Total(StatId.StaminaSave);
        }

        // ── 저장 ──
        public void Export(List<Item> bag, List<string> equipped)
        {
            bag.AddRange(_bag);
            foreach (var it in _equipped)
            {
                if (it != null) bag.Add(it);
                equipped.Add(it != null ? it.uid : "");
            }
        }

        public void Import(List<Item> bag, List<string> equipped)
        {
            _bag.Clear();
            for (int i = 0; i < _equipped.Length; i++) _equipped[i] = null;
            if (bag != null)
                foreach (var it in bag)
                {
                    if (it == null || string.IsNullOrEmpty(it.uid)) continue;
                    if (it.subs == null) it.subs = new List<StatRoll>();
                    int slot = equipped != null ? equipped.IndexOf(it.uid) : -1;
                    if (slot >= 0 && slot < _equipped.Length && _equipped[slot] == null && (int)it.slot == slot) _equipped[slot] = it;
                    else _bag.Add(it);
                }
            Changed?.Invoke();
        }
    }
}
