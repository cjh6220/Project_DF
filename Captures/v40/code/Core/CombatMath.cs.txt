using UnityEngine;
using Proto.Data;

namespace Proto.Core
{
    /// <summary>플레이어 공격 공용 계산 — 기본 공격과 스킬이 같이 쓴다.</summary>
    public static class CombatMath
    {
        /// <summary>치명타를 굴린다. 터지면 (1 + 치명타 피해)배.</summary>
        public static float Crit(TuningConfig cfg, float dmg, out bool crit)
        {
            crit = cfg != null && cfg.critChance > 0f && Random.value < cfg.critChance;
            return crit ? dmg * (1f + Mathf.Max(0f, cfg.critDamage)) : dmg;
        }
    }
}
