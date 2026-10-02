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
