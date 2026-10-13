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
