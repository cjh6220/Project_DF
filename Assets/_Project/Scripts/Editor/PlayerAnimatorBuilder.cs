#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Proto.EditorTools
{
    /// <summary>
    /// 플레이어 애니메이터 컨트롤러를 코드로 만든다.
    /// 메뉴: Proto ▸ Build Player Animator
    ///
    /// 손으로 만들면 상태·전이가 20개쯤 되고, 클립을 바꿀 때마다 다시 이어야 한다.
    /// 스킬이 12종으로 늘어나면 이 파일에 줄만 추가하면 된다.
    ///
    /// 클립은 전부 휴머노이드라 어떤 모델에도 리타깃된다.
    /// </summary>
    public static class PlayerAnimatorBuilder
    {
        const string People = "Assets/polyperfect/Low Poly Animated People/- Animations";
        const string OutPath = "Assets/_Project/Animation/Player.controller";

        [MenuItem("Proto/Build Player Animator")]
        public static AnimatorController Build()
        {
            System.IO.Directory.CreateDirectory("Assets/_Project/Animation");
            AssetDatabase.DeleteAsset(OutPath);
            var ac = AnimatorController.CreateAnimatorControllerAtPath(OutPath);

            ac.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ac.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            ac.AddParameter("AttackIndex", AnimatorControllerParameterType.Int);
            ac.AddParameter("Mining", AnimatorControllerParameterType.Bool);
            ac.AddParameter("Dead", AnimatorControllerParameterType.Trigger);

            var sm = ac.layers[0].stateMachine;

            // ── 상태 ─────────────────────────────────────────────
            // 무기를 든 자세라 해적 세트가 이 게임에 가장 맞는다.
            var idle = State(sm, "Idle", Clip("In Place/Pirate/Pirate_Idle.anim", "Pirate_Idle"), 1f, new Vector3(260, 0, 0));
            var run  = State(sm, "Run",  Clip("In Place/Pirate/Pirate_Run.fbx", "Pirate_Run"), 1f, new Vector3(260, 80, 0));

            // 공격 쿨이 0.38초인데 원본 클립은 1.2~1.5초다. 배속으로 맞춘다.
            // 원본 공격 클립은 앞으로 크게 전진한다(최대 1.08m). 그대로 쓰면
            // 때릴 때마다 캐릭터가 미끄러져서 공격 판정 위치와 눈에 보이는 위치가 어긋난다.
            // Bake Into Pose(XZ)를 끈 사본을 쓴다 — 수평 이동이 루트 모션으로 빠지고,
            // applyRootMotion이 꺼져 있으므로 버려진다.
            var atk1 = State(sm, "Attack1", Local("Pirate Slash (InPlace).anim"), 3.2f, new Vector3(560, -60, 0));
            var atk2 = State(sm, "Attack2", Local("Pirate_Stab (InPlace).anim"), 2.8f, new Vector3(560, 20, 0));

            // 곡괭이 애니메이션이 없어서 쓸기 동작을 빌려 쓴다. 내려치는 것처럼 보인다.
            var mine = State(sm, "Mine", Clip("Common_Animations/Common_Animation_Set.fbx", "Cleaning_Sweeping"), 1.4f, new Vector3(260, 170, 0));

            var dead = State(sm, "Death", Clip("In Place/Deaths/Death_Slashed.fbx", "Death_Slashed"), 1f, new Vector3(560, 160, 0));

            sm.defaultState = idle;

            // ── 이동 ─────────────────────────────────────────────
            Go(idle, run, 0.12f, Cond(AnimatorConditionMode.Greater, "Speed", 0.1f));
            Go(run, idle, 0.12f, Cond(AnimatorConditionMode.Less, "Speed", 0.1f));

            // ── 채굴 ─────────────────────────────────────────────
            Go(idle, mine, 0.10f, Cond(AnimatorConditionMode.If, "Mining", 0f));
            Go(run,  mine, 0.10f, Cond(AnimatorConditionMode.If, "Mining", 0f));
            Go(mine, idle, 0.12f, Cond(AnimatorConditionMode.IfNot, "Mining", 0f));

            // ── 공격 ─────────────────────────────────────────────
            // AnyState에서 들어가므로 이동 중에도, 공격 중에도 바로 끊고 나간다.
            AnyGo(sm, atk1, 0.05f,
                  Cond(AnimatorConditionMode.If, "Attack", 0f),
                  Cond(AnimatorConditionMode.Equals, "AttackIndex", 0f));
            AnyGo(sm, atk2, 0.05f,
                  Cond(AnimatorConditionMode.If, "Attack", 0f),
                  Cond(AnimatorConditionMode.Equals, "AttackIndex", 1f));

            Exit(atk1, idle, 0.12f);
            Exit(atk2, idle, 0.12f);

            // ── 사망 ─────────────────────────────────────────────
            AnyGo(sm, dead, 0.08f, Cond(AnimatorConditionMode.If, "Dead", 0f));

            EditorUtility.SetDirty(ac);
            AssetDatabase.SaveAssets();

            Debug.Log("[Proto] 플레이어 애니메이터 생성 — " + OutPath);
            return ac;
        }

        // ──────────────────────────────────────────────────────────

        static AnimatorState State(AnimatorStateMachine sm, string name, AnimationClip clip, float speed, Vector3 pos)
        {
            var s = sm.AddState(name, pos);
            s.motion = clip;
            s.speed = speed;
            if (clip == null) Debug.LogWarning("[Proto] 클립 없음: " + name);
            return s;
        }

        static AnimatorCondition Cond(AnimatorConditionMode mode, string param, float threshold)
            => new AnimatorCondition { mode = mode, parameter = param, threshold = threshold };

        static void Go(AnimatorState from, AnimatorState to, float dur, params AnimatorCondition[] conds)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = false;
            t.duration = dur;
            foreach (var c in conds) t.AddCondition(c.mode, c.threshold, c.parameter);
        }

        static void AnyGo(AnimatorStateMachine sm, AnimatorState to, float dur, params AnimatorCondition[] conds)
        {
            var t = sm.AddAnyStateTransition(to);
            t.hasExitTime = false;
            t.duration = dur;
            t.canTransitionToSelf = true;
            foreach (var c in conds) t.AddCondition(c.mode, c.threshold, c.parameter);
        }

        /// <summary>클립이 끝나면 돌아온다.</summary>
        static void Exit(AnimatorState from, AnimatorState to, float dur)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = true;
            t.exitTime = 0.85f;
            t.duration = dur;
        }

        /// <summary>우리가 손본 사본 클립.</summary>
        static AnimationClip Local(string fileName)
        {
            var c = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/_Project/Animation/Clips/" + fileName);
            if (c == null) Debug.LogWarning("[Proto] 사본 클립 없음: " + fileName);
            return c;
        }

        /// <summary>fbx 안의 서브에셋이든 .anim이든 이름으로 찾아온다.</summary>
        static AnimationClip Clip(string relPath, string clipName)
        {
            string path = People + "/" + relPath;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                var c = o as AnimationClip;
                if (c == null || c.name.StartsWith("__preview")) continue;
                if (c.name.Trim() == clipName.Trim()) return c;
            }
            Debug.LogWarning("[Proto] 클립을 못 찾음: " + clipName + " in " + path);
            return null;
        }
    }
}
#endif
