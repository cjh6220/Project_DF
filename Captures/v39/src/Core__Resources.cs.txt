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
