using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using MoreMountains.Feedbacks;
using MoreMountains.FeedbacksForThirdParty;

namespace Proto.Feel
{
    /// <summary>
    /// 큰 한 방 연출 (발도술 · 대지 가르기) — MMFeedbacks로 돌린다.
    ///
    ///   Windup   쓰는 순간 화면이 살짝 당겨진다 (기를 모으는 느낌)
    ///   Finisher 맞히는 순간
    ///              · 화면이 확 당겨졌다 천천히 돌아온다 (카메라 정사영 크기)
    ///              · 흰빛 번쩍 → 흑백 · 강한 대비가 잠깐 (색 보정)
    ///              · 소리가 먹먹해졌다 돌아온다 (로우패스)
    ///              · 세상이 느려진다 (Feel.SlowMo — 히트스톱이 끝난 뒤 이어진다)
    ///
    /// 전부 실제 시간으로 돈다 (히트스톱 · 슬로모션 중에도). 설정 '번쩍임 줄이기'면 색 번쩍임은 뺀다.
    /// 카메라 · 전역 Volume · 오디오 리스너에 MM 셰이커를 처음 쓸 때 붙인다. 프로파일 원본은 안 건드린다(런타임 사본).
    /// </summary>
    public static class Cinematic
    {
        static GameObject _host;
        static MMF_Player _windup, _finisher, _finisherNoFlash;

        public static void Windup() { if (Ready()) _windup.PlayFeedbacks(); }

        public static void Finisher(float slowScale = 0.3f, float slowSeconds = 0.55f)
        {
            if (!Ready()) return;
            var p = Proto.Core.GameSettings.Data.reduceFlashes ? _finisherNoFlash : _finisher;
            p.PlayFeedbacks();
            if (Feel.I != null) Feel.I.SlowMo(slowScale, slowSeconds);
        }

        static bool Ready()
        {
            if (_host != null) return true;
            var cam = Camera.main;
            if (cam == null) return false;

            // 카메라 — 정사영 크기 셰이커
            if (cam.GetComponent<MMCameraOrthographicSizeShaker>() == null)
                cam.gameObject.AddComponent<MMCameraOrthographicSizeShaker>().TimescaleMode = TimescaleModes.Unscaled;

            // 소리 — 리스너에 로우패스 (평소엔 꺼진 것과 같은 22000Hz)
            var listener = Object.FindFirstObjectByType<AudioListener>();
            if (listener != null && listener.GetComponent<MMAudioFilterLowPassShaker>() == null)
            {
                var lp = listener.GetComponent<AudioLowPassFilter>();
                if (lp == null) lp = listener.gameObject.AddComponent<AudioLowPassFilter>();
                lp.cutoffFrequency = 22000f;
                listener.gameObject.AddComponent<MMAudioFilterLowPassShaker>().TimescaleMode = TimescaleModes.Unscaled;
            }

            // 화면 색 — 전역 Volume에 색 보정
            foreach (var v in Object.FindObjectsByType<Volume>(FindObjectsSortMode.None))
            {
                if (!v.isGlobal) continue;
                var prof = v.profile;   // 런타임 사본
                if (!prof.TryGet(out ColorAdjustments ca)) ca = prof.Add<ColorAdjustments>(true);
                ca.active = true;
                ca.saturation.overrideState = true; ca.contrast.overrideState = true; ca.postExposure.overrideState = true;
                if (v.GetComponent<MMColorAdjustmentsShaker_URP>() == null)
                    v.gameObject.AddComponent<MMColorAdjustmentsShaker_URP>().TimescaleMode = TimescaleModes.Unscaled;
                break;
            }

            _host = new GameObject("Cinematic");
            Object.DontDestroyOnLoad(_host);
            _windup = Player("Windup");
            _windup.AddFeedback(Zoom(0.45f, -0.45f, Ease(0.6f)));
            _windup.Initialization();

            _finisher = Player("Finisher");
            _finisher.AddFeedback(Zoom(1.1f, -1.5f, Punch(0.06f, 0.35f)));
            _finisher.AddFeedback(ColorFlash());
            _finisher.AddFeedback(LowPass());
            _finisher.Initialization();

            _finisherNoFlash = Player("FinisherNoFlash");
            _finisherNoFlash.AddFeedback(Zoom(1.1f, -1.5f, Punch(0.06f, 0.35f)));
            _finisherNoFlash.AddFeedback(LowPass());
            _finisherNoFlash.Initialization();
            return true;
        }

        static MMF_Player Player(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_host.transform, false);
            return go.AddComponent<MMF_Player>();
        }

        /// <summary>화면 당기기 — 정사영 크기에 amount를 더한다(음수 = 당김).</summary>
        static MMF_Feedback Zoom(float dur, float amount, AnimationCurve curve)
        {
            var f = new MMF_CameraOrthographicSize();
            f.Label = "화면 당기기";
            f.Duration = dur;
            f.RelativeOrthographicSize = true;
            f.ShakeOrthographicSize = curve;
            f.RemapOrthographicSizeZero = 0f;
            f.RemapOrthographicSizeOne = amount;
            return Unscaled(f);
        }

        /// <summary>흰빛 번쩍 → 흑백 · 강한 대비 → 원래대로.</summary>
        static MMF_Feedback ColorFlash()
        {
            var f = new MMF_ColorAdjustments_URP();
            f.Label = "흑백 번쩍";
            f.ShakeDuration = 0.5f;
            f.RelativeIntensity = true;
            f.ShakePostExposure = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.04f, 1f), new Keyframe(0.22f, 0f), new Keyframe(1f, 0f));
            f.RemapPostExposureZero = 0f; f.RemapPostExposureOne = 1.6f;
            f.ShakeSaturation = Punch(0.05f, 0.55f);
            f.RemapSaturationZero = 0f; f.RemapSaturationOne = -100f;
            f.ShakeContrast = Punch(0.05f, 0.55f);
            f.RemapContrastZero = 0f; f.RemapContrastOne = 45f;
            f.ShakeHueShift = AnimationCurve.Constant(0f, 1f, 0f);
            f.RemapHueShiftZero = 0f; f.RemapHueShiftOne = 0f;
            return Unscaled(f);
        }

        /// <summary>소리 먹먹 — 22000Hz → 700Hz → 22000Hz.</summary>
        static MMF_Feedback LowPass()
        {
            var f = new MMF_AudioFilterLowPass();
            f.Label = "소리 먹먹";
            f.Duration = 0.9f;
            f.RelativeLowPass = false;
            f.ShakeLowPass = Punch(0.05f, 0.4f);
            f.RemapLowPassZero = 22000f;
            f.RemapLowPassOne = 700f;
            return Unscaled(f);
        }

        /// <summary>0 → 1(빠르게, rise) → hold까지 유지 → 0(천천히).</summary>
        static AnimationCurve Punch(float rise, float hold) =>
            new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(rise, 1f), new Keyframe(hold, 1f), new Keyframe(1f, 0f));

        /// <summary>천천히 들어갔다(peak) 빠르게 풀린다.</summary>
        static AnimationCurve Ease(float peak) =>
            new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(peak, 1f), new Keyframe(1f, 0f));

        static MMF_Feedback Unscaled(MMF_Feedback f)
        {
            if (f.Timing == null) f.Timing = new MMFeedbackTiming();
            f.Timing.TimescaleMode = TimescaleModes.Unscaled;
            return f;
        }
    }
}
