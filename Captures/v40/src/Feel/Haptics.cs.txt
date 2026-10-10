using UnityEngine;
using UnityEngine.InputSystem;
using Proto.Core;

namespace Proto.Feel
{
    /// <summary>
    /// 패드 진동 — 엑스박스 · 듀얼센스/듀얼쇼크 · 스팀덱(Steam Input) 모두 Gamepad.SetMotorSpeeds 하나로 된다.
    ///
    ///   Pulse(약, 강, 시간)  타격 · 피격 · 보스 내려찍기처럼 짧게 한 번
    ///   Hold(약, 강)         매 프레임 부르는 동안 계속 — 도리이 차오르기처럼 세기가 변하는 진동
    ///
    /// 약 = 저주파 모터(묵직), 강 = 고주파 모터(날카로움). 둘 다 0~1.
    /// 여러 진동이 겹치면 센 쪽을 쓴다. 설정에서 끄거나, 키보드로 하고 있거나, 메뉴가 열려 있거나,
    /// 창이 비활성이면 멈춘다. 히트스톱(시간 정지) 중에는 멈추지 않는다 — 실제 시간으로 잰다.
    /// </summary>
    public static class Haptics
    {
        static float _pulseLow, _pulseHigh, _pulseUntil;
        static float _holdLow, _holdHigh, _holdUntil;

        public static void Pulse(float low, float high, float seconds)
        {
            HapticsRunner.Ensure();
            float now = Time.unscaledTime;
            // 진행 중인 진동보다 약하면 세기는 그대로, 시간만 늘리지 않는다
            if (now >= _pulseUntil) { _pulseLow = 0f; _pulseHigh = 0f; }
            _pulseLow = Mathf.Max(_pulseLow, Mathf.Clamp01(low));
            _pulseHigh = Mathf.Max(_pulseHigh, Mathf.Clamp01(high));
            _pulseUntil = Mathf.Max(_pulseUntil, now + seconds);
        }

        public static void Hold(float low, float high)
        {
            HapticsRunner.Ensure();
            _holdLow = Mathf.Clamp01(low);
            _holdHigh = Mathf.Clamp01(high);
            _holdUntil = Time.unscaledTime + 0.1f;   // 매 프레임 갱신하지 않으면 곧 꺼진다
        }

        public static void StopAll()
        {
            _pulseUntil = _holdUntil = 0f;
            Apply(0f, 0f);
        }

        static bool _wasRunning;

        internal static void Apply(float low, float high)
        {
            var pad = Gamepad.current;
            if (pad == null) return;
            if (low <= 0f && high <= 0f)
            {
                if (_wasRunning) { pad.SetMotorSpeeds(0f, 0f); _wasRunning = false; }
                return;
            }
            pad.SetMotorSpeeds(low, high);
            _wasRunning = true;
        }

        static bool Blocked =>
            !GameSettings.Data.vibration
            || GameInput.Scheme == InputScheme.Keyboard
            || !Application.isFocused
            || Proto.UI.SystemMenu.IsOpen || Proto.UI.SettingsView.IsOpen || Proto.UI.GiveUpMenu.IsOpen;

        internal static void Tick()
        {
            if (Blocked) { Apply(0f, 0f); return; }
            float now = Time.unscaledTime;
            float low = 0f, high = 0f;
            if (now < _pulseUntil) { low = _pulseLow; high = _pulseHigh; }
            if (now < _holdUntil) { low = Mathf.Max(low, _holdLow); high = Mathf.Max(high, _holdHigh); }
            Apply(low, high);
        }
    }
}
