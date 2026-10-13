#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Proto.EditorTools
{
    /// <summary>
    /// 플레이어 애니메이터에 스킬 상태를 넣는다. 메뉴: Proto ▸ Build Skill Animations
    ///
    ///   상태 이름은 SkillCaster의 State와 같아야 한다 (Skill_Dash …).
    ///   전이는 없다 — SkillCaster가 코드로 바로 넘긴다 (판정 시각을 한곳에서 쥐려고).
    ///   이미 있는 상태는 클립 · 속도만 갱신한다. 여러 번 눌러도 된다.
    ///   클립: Assets/Animation/_InUse/Player/Skills (원본 팩에서 복사 — 이동은 빼고 높이 · 회전은 몸에 남김)
    /// </summary>
    public static class SkillAnimatorBuilder
    {
        const string Controller = "Assets/_Project/Animation/Player.controller";
        const string Dir = "Assets/Animation/_InUse/Player/Skills/";

        // 상태, 클립 파일, 클립 이름, 속도
        static readonly (string state, string file, string clip, float speed)[] States =
        {
            ("Skill_Dash",       "M_katana_Blade@Skill_A.FBX",                    "Skill_A",                    1.15f),
            ("Skill_Upper",      "M_katana_Blade@UpperAttack_Z0.FBX",             "UpperAttack_Z0",             1.2f),
            ("Skill_Slam",       "M_katana_Blade@Skill_D.FBX",                    "Skill_D",                    1.45f),
            ("Skill_Whirl",      "M_Big_Sword@Skill_E.FBX",                       "Skill_E",                    1.2f),
            ("Skill_Thrust",     "M_katana_Blade@Skill_B.FBX",                    "Skill_B",                    1.25f),
            ("Skill_Wave",       "M_katana_Blade@Skill_E.FBX",                    "Skill_E",                    1.2f),
            ("Skill_Iaido",      "M_Katana_Blade@Skill_I.FBX",                    "Skill_I",                    1.3f),
            ("Skill_GuardStart", "M_katana_Blade@Revenge_Guard_Start.FBX",        "Revenge_Guard_Start",        1f),
            ("Skill_GuardLoop",  "M_katana_Blade@Revenge_Guard_Loop.FBX",         "Revenge_Guard_Loop",         1f),
            ("Skill_GuardAtk",   "M_katana_Blade@Revenge_Guard_Attack_ver_A.FBX", "Revenge_Guard_Attack_ver_A", 1f),
            ("Skill_GuardEnd",   "M_katana_Blade@Revenge_Guard_End.FBX",          "Revenge_Guard_End",          1f),
            ("Skill_Gale",       "M_katana_Blade@Skill_C.FBX",                    "Skill_C",                    1.2f),
            ("Skill_Fall",       "M_katana_Blade@Skill_G.FBX",                    "Skill_G",                    1.15f),
            ("Skill_Ultimate",   "Jump_Attack_New.anim",                          "Jump_Attack_New",            1.15f),
        };

        [MenuItem("Proto/Build Skill Animations")]
        public static void Build()
        {
            var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(Controller);
            if (ac == null) { Debug.LogError("[Proto] Player.controller가 없습니다"); return; }
            var sm = ac.layers[0].stateMachine;
            int made = 0, updated = 0, row = 0;

            foreach (var e in States)
            {
                AnimationClip clip = null;
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(Dir + e.file))
                    if (o is AnimationClip c && c.name == e.clip) clip = c;
                if (clip == null) { Debug.LogWarning("[Proto] 스킬 클립 없음: " + e.file); continue; }

                AnimatorState st = null;
                foreach (var cs in sm.states) if (cs.state.name == e.state) st = cs.state;
                if (st == null)
                {
                    st = sm.AddState(e.state, new Vector3(650f, -200f + row * 55f, 0f));
                    made++;
                }
                else updated++;
                st.motion = clip;
                st.speed = e.speed;
                st.writeDefaultValues = true;
                row++;
            }
            EditorUtility.SetDirty(ac);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Proto] 스킬 상태 {made}개 추가 · {updated}개 갱신");
        }
    }
}
#endif
