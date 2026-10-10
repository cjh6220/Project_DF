using UnityEngine;

namespace Proto.Core
{
    /// <summary>
    /// 자동 저장 담당 — RunManager에 붙는다 (SaveSystem.LoadInto가 붙여 준다).
    /// 바뀐 게 생기면 표시만 해 두고 0.5초 모았다가 한 번 쓴다. 구매를 연달아 눌러도 파일은 한 번만 쓴다.
    /// </summary>
    public class SaveAgent : MonoBehaviour
    {
        RunManager _run;
        Proto.Town.VillageController _village;
        bool _dirty;
        float _dirtyAt;
        const float Delay = 0.5f;

        public void Bind(RunManager run, Proto.Town.VillageController village)
        {
            Unbind();
            _run = run; _village = village;
            _run.SaveRequested += MarkDirty;
            _run.Progression.Changed += MarkDirty;
            _run.Skills.Changed += MarkDirty;
            _run.Bank.Changed += OnBank;
        }

        void Unbind()
        {
            if (_run == null) return;
            _run.SaveRequested -= MarkDirty;
            _run.Progression.Changed -= MarkDirty;
            _run.Skills.Changed -= MarkDirty;
            _run.Bank.Changed -= OnBank;
        }

        void OnDestroy() => Unbind();

        void OnBank(ResourceId id, int amount) => MarkDirty();

        void MarkDirty()
        {
            if (!_dirty) _dirtyAt = Time.unscaledTime;
            _dirty = true;
        }

        void LateUpdate()
        {
            if (_dirty && Time.unscaledTime - _dirtyAt >= Delay) SaveNow();
        }

        public void SaveNow()
        {
            _dirty = false;
            SaveSystem.Save(_run, _village);
        }

        void OnApplicationQuit() => SaveNow();
        void OnApplicationPause(bool paused) { if (paused) SaveNow(); }
    }
}
