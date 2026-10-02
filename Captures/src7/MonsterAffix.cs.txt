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
            // 흰색 = 텍스처 그대로. 색을 넣으면 일반 몬스터까지 물들어서
            // 챔피언의 색이 '다르다'는 신호로 읽히지 않는다.
            Color = Color.white,
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
