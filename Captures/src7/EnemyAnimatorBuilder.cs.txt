#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Proto.EditorTools
{
    /// <summary>
    /// 몬스터 애니메이터. 메뉴: Proto ▸ Build Enemy Animator
    ///
    /// 좀비 세트를 쓴다. 팔을 늘어뜨리고 느리게 다가오는 실루엣이
    /// "사람이 아니다"를 멀리서도 읽히게 한다.
    /// 클립이 전부 휴머노이드라 스켈레톤·좀비 어떤 모델에도 그대로 붙는다.
    /// </summary>
    public static class EnemyAnimatorBuilder
    {
        const string People = "Assets/polyperfect/Low Poly Animated People/- Animations";
        const string OutPath = "Assets/_Project/Animation/Enemy.controller";

        [MenuItem("Proto/Build Enemy Animator")]
        public static AnimatorController Build()
        {
            System.IO.Directory.CreateDirectory("Assets/_Project/Animation");
            AssetDatabase.DeleteAsset(OutPath);
            var ac = AnimatorController.CreateAnimatorControllerAtPath(OutPath);

            ac.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ac.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            ac.AddParameter("Dead", AnimatorControllerParameterType.Trigger);

            var sm = ac.layers[0].stateMachine;

            var idle = State(sm, "Idle", Clip("In Place/Zombie/Zombie_Idle_1.anim", "Zombie_Idle_1"), 1f, new Vector3(260, 0, 0));
            var walk = State(sm, "Walk", Clip("In Place/Zombie/Zombie_Walk.fbx", "Zombie Walk"), 1.6f, new Vector3(260, 80, 0));

            // 공격 쿨이 1.4초인데 원본은 4.17초다. 배속으로 맞춘다.
            var atk = State(sm, "Attack", Clip("In Place/Zombie/Zombie_Attack.fbx", "Zombie_Attack"), 3.4f, new Vector3(560, 0, 0));
            var dead = State(sm, "Death", Clip("In Place/Deaths/Death_FallForwards.fbx", "Death"), 1.2f, new Vector3(560, 120, 0));

            sm.defaultState = idle;

            Go(idle, walk, 0.15f, AnimatorConditionMode.Greater, "Speed", 0.1f);
            Go(walk, idle, 0.15f, AnimatorConditionMode.Less, "Speed", 0.1f);

            AnyGo(sm, atk, 0.06f, AnimatorConditionMode.If, "Attack", 0f);
            Exit(atk, idle, 0.15f);

            AnyGo(sm, dead, 0.08f, AnimatorConditionMode.If, "Dead", 0f);

            EditorUtility.SetDirty(ac);
            AssetDatabase.SaveAssets();
            Debug.Log("[Proto] 몬스터 애니메이터 생성 — " + OutPath);
            return ac;
        }

        static AnimatorState State(AnimatorStateMachine sm, string name, AnimationClip clip, float speed, Vector3 pos)
        {
            var s = sm.AddState(name, pos);
            s.motion = clip;
            s.speed = speed;
            if (clip == null) Debug.LogWarning("[Proto] 클립 없음: " + name);
            return s;
        }

        static void Go(AnimatorState from, AnimatorState to, float dur, AnimatorConditionMode m, string param, float th)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = false; t.duration = dur;
            t.AddCondition(m, th, param);
        }

        static void AnyGo(AnimatorStateMachine sm, AnimatorState to, float dur, AnimatorConditionMode m, string param, float th)
        {
            var t = sm.AddAnyStateTransition(to);
            t.hasExitTime = false; t.duration = dur; t.canTransitionToSelf = true;
            t.AddCondition(m, th, param);
        }

        static void Exit(AnimatorState from, AnimatorState to, float dur)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = true; t.exitTime = 0.8f; t.duration = dur;
        }

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
