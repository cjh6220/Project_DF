using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using MoreMountains.Feedbacks;
using MoreMountains.FeedbacksForThirdParty;

namespace Proto.Feel
{
    /// <summary>
    /// 화면 펀치 — 세게 맞는 순간 화면이 '움찔'한다. MMFeedbacks(More Mountains, TopDown Engine에 들어 있는 것)로 돌린다.
    ///
    ///   약하게 (Light)   색수차가 아주 잠깐 번진다
    ///   세게   (Heavy)   색수차 + 렌즈가 안쪽으로 오목하게 빨려 들었다 돌아온다
    ///
    /// 히트스톱 · 흔들림 · 진동은 원래대로 Feel이 한다. 이건 그 위에 얹는 화면 효과뿐이다.
    /// 씬의 전역 Volume에 색수차 · 렌즈 왜곡을 (없으면) 넣고, 거기에 MM 셰이커를 붙인다. 프로파일 원본은 안 건드린다(런타임 사본).
    /// 히트스톱(timeScale 0) 중에도 움직이게 실제 시간으로 돈다. 설정의 '번쩍임 줄이기'면 안 한다.
    /// </summary>
    public static class ScreenPunch
    {
        static GameObject _host;
        static MMF_Player _light, _heavy;

        public static void Light() => Play(false);
        public static void Heavy() => Play(true);

        static void Play(bool heavy)
        {
            if (Proto.Core.GameSettings.Data.reduceFlashes) return;
            if (_host == null && !Init()) return;
            var p = heavy ? _heavy : _light;
            if (p != null) p.PlayFeedbacks();
        }

        static bool Init()
        {
            Volume vol = null;
            foreach (var v in Object.FindObjectsByType<Volume>(FindObjectsSortMode.None))
                if (v.isGlobal) { vol = v; break; }
            if (vol == null) return false;

            var prof = vol.profile;   // 런타임 사본 — 에셋 원본은 그대로
            if (!prof.TryGet(out ChromaticAberration ca)) ca = prof.Add<ChromaticAberration>(true);
            ca.active = true; ca.intensity.overrideState = true; ca.intensity.value = 0f;
            if (!prof.TryGet(out LensDistortion ld)) ld = prof.Add<LensDistortion>(true);
            ld.active = true; ld.intensity.overrideState = true; ld.intensity.value = 0f;

            if (vol.GetComponent<MMChromaticAberrationShaker_URP>() == null)
            {
                var s = vol.gameObject.AddComponent<MMChromaticAberrationShaker_URP>();
                s.TimescaleMode = TimescaleModes.Unscaled;
            }
            if (vol.GetComponent<MMLensDistortionShaker_URP>() == null)
            {
                var s = vol.gameObject.AddComponent<MMLensDistortionShaker_URP>();
                s.TimescaleMode = TimescaleModes.Unscaled;
            }

            _host = new GameObject("ScreenPunch");
            _light = Build("Light", 0.14f, 0.45f, 0f, 0f);
            _heavy = Build("Heavy", 0.24f, 1f, 0.2f, -0.28f);
            return true;
        }

        static MMF_Player Build(string name, float dur, float aberration, float lensDur, float lens)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_host.transform, false);
            var p = go.AddComponent<MMF_Player>();

            var ca = new MMF_ChromaticAberration_URP();
            ca.Label = "색수차";
            ca.Duration = dur;
            ca.Intensity = Pulse();
            ca.RemapIntensityZero = 0f;
            ca.RemapIntensityOne = aberration;
            Unscaled(ca);
            p.AddFeedback(ca);

            if (lensDur > 0f)
            {
                var ld = new MMF_LensDistortion_URP();
                ld.Label = "렌즈 펀치";
                ld.Duration = lensDur;
                ld.Intensity = Pulse();
                ld.RemapIntensityZero = 0f;
                ld.RemapIntensityOne = lens;
                Unscaled(ld);
                p.AddFeedback(ld);
            }

            p.Initialization();
            return p;
        }

        /// <summary>0 → 1(아주 빨리) → 0(천천히).</summary>
        static AnimationCurve Pulse() =>
            new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.15f, 1f), new Keyframe(1f, 0f));

        static void Unscaled(MMF_Feedback f)
        {
            if (f.Timing == null) f.Timing = new MMFeedbackTiming();
            f.Timing.TimescaleMode = TimescaleModes.Unscaled;
        }
    }
}
