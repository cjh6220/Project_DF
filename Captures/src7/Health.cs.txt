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
