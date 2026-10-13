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

        /// <summary>받는 피해 배율 — 플레이어 장비의 '피해 감소'. 1 = 그대로.</summary>
        public float DamageTakenMul = 1f;

        /// <summary>피해를 받기 직전에 묻는다. true를 돌려주면 막은 것 (반격 자세).</summary>
        public Func<float, Vector3, bool> Intercept;

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
            if (Intercept != null && Intercept(amount, sourcePos)) return;
            amount *= DamageTakenMul;
            Current = Mathf.Max(0f, Current - amount);
            Changed?.Invoke(Current, Max);
            Damaged?.Invoke(amount, sourcePos);
            if (Current <= 0f) Died?.Invoke();
        }

        /// <summary>최대 체력만 바꾼다 — 던전 안에서 장비를 바꿨을 때. 남은 비율은 그대로 둔다.</summary>
        public void SetMax(float newMax)
        {
            if (newMax <= 0f || Mathf.Approximately(newMax, max)) return;
            float ratio = max > 0f ? Current / max : 1f;
            max = newMax;
            if (!IsDead) Current = Mathf.Max(1f, max * ratio);
            Changed?.Invoke(Current, Max);
        }

        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f) return;
            Current = Mathf.Min(Max, Current + amount);
            Changed?.Invoke(Current, Max);
        }
    }
}
