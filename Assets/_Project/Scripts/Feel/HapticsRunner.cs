using UnityEngine;

namespace Proto.Feel
{
    /// <summary>매 프레임 패드 모터 세기를 갱신한다 — Haptics를 처음 부를 때 저절로 생긴다.</summary>
    public class HapticsRunner : MonoBehaviour
    {
        static HapticsRunner _i;

        public static void Ensure()
        {
            if (_i != null) return;
            var go = new GameObject("[Haptics]");
            DontDestroyOnLoad(go);
            _i = go.AddComponent<HapticsRunner>();
        }

        void LateUpdate() => Haptics.Tick();
        void OnApplicationFocus(bool f) { if (!f) Haptics.Apply(0f, 0f); }
        void OnApplicationQuit() => Haptics.StopAll();
        void OnDisable() => Haptics.StopAll();
    }
}
