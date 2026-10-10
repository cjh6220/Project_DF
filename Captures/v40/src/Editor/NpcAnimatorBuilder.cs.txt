#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Proto.Town;

namespace Proto.EditorTools
{
    /// <summary>
    /// 마을 NPC 애니메이터. 메뉴: Proto ▸ Build NPC Animators
    ///
    ///   평소  Idle  — 장로는 마법사 대기, 검객은 카타나 대기 자세
    ///   가까이 오면 Near — 장로는 이야기하는 몸짓, 검객은 칼을 고쳐 쥐는 자세
    ///
    /// 플레이어 애니메이터와 달리 에셋을 지우고 새로 만들지 않는다.
    /// 지우면 파일 ID가 바뀌어 씬에 놓인 NPC의 연결이 끊기고 T자세가 된다.
    /// 같은 파일 안에서 상태만 비우고 다시 채운다.
    /// </summary>
    public static class NpcAnimatorBuilder
    {
        const string Dir = "Assets/_Project/Animation/";
        const string People = "Assets/polyperfect/Low Poly Animated People/- Animations/";
        const string InUse = "Assets/Animation/_InUse/Npc/";

        [MenuItem("Proto/Build NPC Animators")]
        public static void BuildAll()
        {
            For(NpcRole.Growth);
            For(NpcRole.Skill);
            AssetDatabase.SaveAssets();
        }

        public static RuntimeAnimatorController For(NpcRole role)
        {
            if (role == NpcRole.Growth)
                return Build("Npc_Elder",
                    Clip(People + "In Place/Wizard/Wizard_Idle.anim", "Wizard_Idle"),
                    Clip(People + "Common_Animations/Common_Animation_Set.fbx", "Standing_Talking"));
            return Build("Npc_Swordsman",
                Clip(InUse + "Swordsman/M_katana_Blade@Idle_ver_A.FBX", "Idle_ver_A"),
                Clip(InUse + "Swordsman/M_katana_Blade@Idle_ver_B.FBX", "Idle_ver_B"));
        }

        static AnimatorController Build(string name, AnimationClip idle, AnimationClip near)
        {
            string path = Dir + name + ".controller";
            var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (ac == null) ac = AnimatorController.CreateAnimatorControllerAtPath(path);

            // 같은 파일을 비우고 다시 채운다 (ID 유지)
            var sm = ac.layers[0].stateMachine;
            foreach (var s in sm.states) sm.RemoveState(s.state);
            foreach (var p in ac.parameters) ac.RemoveParameter(p);
            ac.AddParameter("Near", AnimatorControllerParameterType.Bool);

            var a = sm.AddState("Idle", new Vector3(260, 0, 0));
            a.motion = idle;
            var b = sm.AddState("Near", new Vector3(260, 90, 0));
            b.motion = near;
            sm.defaultState = a;

            var t1 = a.AddTransition(b); t1.hasExitTime = false; t1.duration = 0.35f; t1.AddCondition(AnimatorConditionMode.If, 0, "Near");
            var t2 = b.AddTransition(a); t2.hasExitTime = false; t2.duration = 0.45f; t2.AddCondition(AnimatorConditionMode.IfNot, 0, "Near");

            if (idle == null || near == null) Debug.LogWarning("[Proto] NPC 클립 없음: " + name);
            EditorUtility.SetDirty(ac);
            return ac;
        }

        static AnimationClip Clip(string path, string clipName)
        {
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                if (o is AnimationClip c && !c.name.StartsWith("__preview") && c.name == clipName) return c;
            Debug.LogWarning("[Proto] 클립을 못 찾음: " + clipName + " in " + path);
            return null;
        }
    }
}
#endif
