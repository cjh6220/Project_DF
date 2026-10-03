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
            ac.AddParameter("Mining", AnimatorControllerParameterType.Bool);
            ac.AddParameter("Dead", AnimatorControllerParameterType.Trigger);

            var sm = ac.layers[0].stateMachine;

            // ── 상태 ─────────────────────────────────────────────
            // 무기를 든 자세라 해적 세트가 이 게임에 가장 맞는다.
            var idle = State(sm, "Idle", Clip("In Place/Pirate/Pirate_Idle.anim", "Pirate_Idle"), 1f, new Vector3(260, 0, 0));
            // 이동 — 속도에 따라 걷기 ↔ 달리기를 섞는다 (방향키 한 번 = 걷기, 두 번 = 달리기)
            var run  = State(sm, "Run",  Clip("In Place/Pirate/Pirate_Run.fbx", "Pirate_Run"), 1f, new Vector3(260, 80, 0));
            run.motion = Locomotion(ac, Clip("In Place/Pirate/Pirate_Walk.fbx", "Pirate_Walk"), Clip("In Place/Pirate/Pirate_Run.fbx", "Pirate_Run"));

            // 기본 공격 3단 콤보 — 카타나 3콤보 (제자리 버전).
            // 상태 사이 전이는 없다. PlayerController가 판정 타이밍을 세고 PlayerAnimator가 코드로 넘긴다.
            // 배속은 PlayerController.Combo의 Speed와 반드시 같아야 한다.
            var c1 = State(sm, "Combo1", Sword("Player/BasicCombo/M_katana_Blade@Attack_3Combo_1_Inplace.FBX"), 1.35f, new Vector3(560, -120, 0));
            var c2 = State(sm, "Combo2", Sword("Player/BasicCombo/M_katana_Blade@Attack_3Combo_2_Inplace.FBX"), 1.30f, new Vector3(560, -50, 0));
            var c3 = State(sm, "Combo3", Sword("Player/BasicCombo/M_katana_Blade@Attack_3Combo_3_Inplace.FBX"), 1.25f, new Vector3(560, 20, 0));

            // 채굴 — 다리는 제자리에 서고(기본 층 Mine = 대기 자세), 상체만 곡괭이질을 한다(위 층 MineSwing).
            // 곡괭이 전용 모션이 팩에 없어서 대검 내려찍기(두 손으로 머리 위에서)를 빌린다.
            // 통째로 쓰면 깊게 내딛으며 광맥 속으로 파고든다 — 그래서 상체 마스크로 하체를 막는다.
            var mine = State(sm, "Mine", Clip("In Place/Pirate/Pirate_Idle.anim", "Pirate_Idle"), 1f, new Vector3(260, 170, 0));

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
            // 안전장치 — 코드가 돌려보내지 못해도 클립이 끝나면 대기로
            Exit(c1, idle, 0.15f);
            Exit(c2, idle, 0.15f);
            Exit(c3, idle, 0.15f);

            // ── 사망 ─────────────────────────────────────────────
            AnyGo(sm, dead, 0.08f, Cond(AnimatorConditionMode.If, "Dead", 0f));

            // ── 채굴 상체 층 ─────────────────────────────────────
            // 무게는 PlayerAnimator가 채굴 중에만 1로 올린다. 평소엔 0이라 아무 영향 없다.
            EnsureLoop(SwordPack + MineClipPath);
            ac.AddLayer(new AnimatorControllerLayer
            {
                name = "MineUpper",
                defaultWeight = 0f,
                blendingMode = AnimatorLayerBlendingMode.Override,
                avatarMask = UpperBodyMask(),
                stateMachine = new AnimatorStateMachine { name = "MineUpper", hideFlags = HideFlags.HideInHierarchy },
            });
            var layers = ac.layers;
            var upper = layers[layers.Length - 1].stateMachine;
            AssetDatabase.AddObjectToAsset(upper, ac);
            var swing = upper.AddState("MineSwing", new Vector3(260, 0, 0));
            swing.motion = Sword(MineClipPath);
            swing.speed = 1.25f;
            upper.defaultState = swing;
            ac.layers = layers;

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
            t.exitTime = 0.98f;
            t.duration = dur;
        }

        /// <summary>
        /// 게임에서 실제로 쓰는 모션만 모아 두는 곳. 팩에서 쓰기로 정한 파일은 여기로 옮긴다.
        /// (팩 폴더에 남은 것 = 아직 안 쓰는 것)
        /// </summary>
        const string SwordPack = "Assets/Animation/_InUse/";

        /// <summary>_InUse 폴더의 fbx에서 클립을 꺼낸다.</summary>
        /// <summary>상체만 — 허리 위(몸통·머리·두 팔·손가락). 다리와 루트는 기본 층을 따른다.</summary>
        static AvatarMask UpperBodyMask()
        {
            const string path = "Assets/_Project/Animation/UpperBody.mask";
            var m = AssetDatabase.LoadAssetAtPath<AvatarMask>(path);
            if (m == null) { m = new AvatarMask(); AssetDatabase.CreateAsset(m, path); }
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            {
                var part = (AvatarMaskBodyPart)i;
                bool on = part == AvatarMaskBodyPart.Body || part == AvatarMaskBodyPart.Head
                       || part == AvatarMaskBodyPart.LeftArm || part == AvatarMaskBodyPart.RightArm
                       || part == AvatarMaskBodyPart.LeftFingers || part == AvatarMaskBodyPart.RightFingers;
                m.SetHumanoidBodyPartActive(part, on);
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        const string MineClipPath = "Player/Mining/M_Big_Sword@Attack_4Combo_4_Inplace.FBX";

        /// <summary>FBX 안 클립을 반복 재생으로. 이미 켜져 있으면 건드리지 않는다.</summary>
        static void EnsureLoop(string path)
        {
            var imp = AssetImporter.GetAtPath(path) as ModelImporter;
            if (imp == null) return;
            var clips = imp.clipAnimations.Length > 0 ? imp.clipAnimations : imp.defaultClipAnimations;
            bool changed = false;
            foreach (var c in clips) if (!c.loopTime) { c.loopTime = true; changed = true; }
            if (!changed) return;
            imp.clipAnimations = clips;
            imp.SaveAndReimport();
        }

        static AnimationClip Sword(string rel)
        {
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(SwordPack + rel))
                if (o is AnimationClip c && !c.name.StartsWith("__preview")) return c;
            Debug.LogWarning("[Proto] 검 클립을 못 찾음: " + rel);
            return null;
        }

        /// <summary>우리가 손본 사본 클립.</summary>
        static AnimationClip Local(string fileName)
        {
            var c = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/_Project/Animation/Clips/" + fileName);
            if (c == null) Debug.LogWarning("[Proto] 사본 클립 없음: " + fileName);
            return c;
        }

        /// <summary>fbx 안의 서브에셋이든 .anim이든 이름으로 찾아온다.</summary>
        /// <summary>Speed(m/s)로 걷기 ↔ 달리기를 섞는 블렌드 트리. 걷기 3.2m/s, 달리기 6.5m/s에 맞춘다.</summary>
        public static BlendTree Locomotion(AnimatorController ac, AnimationClip walk, AnimationClip run)
        {
            var bt = new BlendTree { name = "Locomotion", blendType = BlendTreeType.Simple1D, blendParameter = "Speed", useAutomaticThresholds = false, hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(bt, ac);
            bt.AddChild(walk, 3.2f);
            bt.AddChild(run, 6.5f);
            return bt;
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
