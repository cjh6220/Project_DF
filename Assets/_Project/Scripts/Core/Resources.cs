using System;
using System.Collections.Generic;

namespace Proto.Core
{
    /// <summary>
    /// 자원 3종. 골드는 공통, 정수·핵은 전투에서 얻는다.
    ///   정수 → 일반 몬스터, 탐험 성장
    ///   핵   → 엘리트/보스, 스킬과 큰 성장
    ///   가루 → 장비를 분해해서 얻는다. 대장장이가 새 장비를 만든다
    /// </summary>
    public enum ResourceId
    {
        Gold,      // 공통
        Essence,   // 전투 — 일반 몬스터
        Core,      // 전투 — 엘리트/보스
        Dust       // 장비 분해 — 제작 재료
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
