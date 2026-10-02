using UnityEngine;

namespace Proto.Data
{
    /// <summary>
    /// 모든 튜닝 수치를 한 곳에 모은다.
    /// 피로도 소모 속도가 이 게임에서 가장 중요한 숫자이므로,
    /// 플레이 중에도 인스펙터에서 바로 만질 수 있어야 한다.
    /// Assets/_Project/Data/Tuning.asset 으로 생성해서 쓴다.
    /// </summary>
    [CreateAssetMenu(fileName = "Tuning", menuName = "Proto/Tuning Config")]
    public class TuningConfig : ScriptableObject
    {
        [Header("── 피로도 ──")]
        [Tooltip("던전 입장 시 최대 피로도")]
        public float maxStamina = 100f;

        [Tooltip("초당 소모량. 이 게임에서 가장 중요한 숫자.")]
        public float staminaDrainPerSecond = 1.0f;

        [Tooltip("처음 들어가는 방에 진입할 때 드는 비용.\n" +
                 "이미 지나온 방으로 되돌아갈 때는 들지 않는다 — 후퇴가 벌이 되면 안 된다.\n" +
                 "깊이 들어갈수록 비싸지고, 왔던 길로 나오는 건 시간만 든다.")]
        public float staminaOnNewRoom = 10f;

        [Header("── 맵 ──")]
        public int gridWidth = 7;
        public int gridHeight = 7;

        [Tooltip("입구에서 보스방까지 최소 거리(칸)")]
        public int minBossDistance = 6;

        [Tooltip("본 경로에서 뻗어나갈 가지의 개수")]
        public int branchCount = 5;

        [Tooltip("가지 하나의 최대 길이")]
        public int branchMaxLength = 3;

        [Header("── 방 내용물 ──")]
        public Vector2Int enemiesPerRoom = new Vector2Int(2, 4);

        [Tooltip("플레이어가 들어온 벽에서 얼마나 안쪽에 서는가")]
        public float roomEntryInset = 2.0f;

        [Tooltip("몬스터가 입장 지점에서 최소한 떨어져 있어야 하는 거리.\n" +
                 "들어가자마자 맞으면 그건 난이도가 아니라 사고다.")]
        public float enemySpawnSafeRadius = 5.0f;

        [Tooltip("몬스터끼리의 최소 간격. 뭉쳐 있으면 한 방에 다 맞는다.")]
        public float enemySpawnSpacing = 3.0f;

        [Tooltip("본 경로 방의 광맥 수 (적게)")]
        public Vector2Int oresOnMainPath = new Vector2Int(0, 1);

        [Tooltip("가지 방의 광맥 수 (많이) — 우회로에 보상을 몰아준다")]
        public Vector2Int oresOnBranch = new Vector2Int(2, 3);

        [Header("── 채굴 ──")]
        [Tooltip("광맥 1회 채굴에 걸리는 시간")]
        public float mineDuration = 2.5f;

        [Tooltip("피격 시 채굴 진행도를 얼마나 유지하는가 (0=전부 잃음, 1=유지)")]
        [Range(0f, 1f)] public float mineProgressKeptOnHit = 0f;

        public float mineRange = 2.2f;

        [Header("── 플레이어 ──")]
        public float moveSpeed = 6.5f;
        public float maxHealth = 100f;
        public float attackDamage = 25f;
        public float attackCooldown = 0.38f;
        public float attackRange = 2.0f;
        public float attackArcDegrees = 110f;

        [Tooltip("버튼을 누르고 판정이 들어가기까지. 무기가 몸 앞을 지나는 순간에 맞춘다.\n" +
                 "Attack1(베기) 모션 기준. 애니메이션 속도를 바꾸면 같이 바꿔야 한다.")]
        public float attackHitDelay1 = 0.17f;
        [Tooltip("Attack2(찌르기) 모션 기준")]
        public float attackHitDelay2 = 0.16f;

        [Header("── 적 ──")]
        public float enemyHealth = 40f;
        public float enemyMoveSpeed = 3.0f;
        public float enemyDamage = 10f;
        public float enemyAttackCooldown = 1.4f;

        [Tooltip("공격 예고 시간. 때리기 전에 이만큼 자세를 잡고 멈춰 선다.\n" +
                 "이 구간이 있어야 '먼저 때려서 끊는다'가 성립한다.\n" +
                 "0이면 예고 없이 즉발이라 맞기 전엔 알 수가 없다.")]
        public float enemyWindup = 0.45f;
        public float enemyAttackRange = 1.8f;

        [Header("── 챔피언 (접두사 몬스터) ──")]
        [Tooltip("방에 챔피언이 나올 확률. 방마다 몇 마리가 아니라 확률로 굴린다.\n" +
                 "매 방에 나오면 특별하지 않다 — 한 판에 2~3마리 만나는 정도가 적당하다.")]
        [Range(0f, 1f)] public float championChancePerRoom = 0.35f;

        [Tooltip("챔피언이 나왔을 때, 한 마리 더 붙을 확률")]
        [Range(0f, 1f)] public float championSecondChance = 0.18f;

        [Header("── 접두사 효과 ──")]
        [Tooltip("'폭발하는' 접두사의 사망 폭발 반경과 피해")]
        public float explodeRadius = 3.2f;
        public float explodeDamage = 18f;

        [Tooltip("'울부짖는' 접두사가 죽을 때 남은 적에게 거는 가속")]
        public float howlHasteMultiplier = 1.5f;
        public float howlHasteDuration = 6f;

        [Header("── 피격 반응 ──")]
        [Tooltip("일반 몬스터가 맞고 멈칫하는 시간. '강건한' 챔피언은 슈퍼아머라 무시한다.")]
        public float enemyHitStun = 0.22f;

        [Tooltip("일반 몬스터가 맞고 밀려나는 거리.\n" +
                 "경직 시간에 걸쳐 밀려난다 — 순간이동이 아니라 미끄러져야 보인다.")]
        public float enemyKnockback = 0.4f;

        [Tooltip("죽은 몬스터를 몇 초 남겨 둘지.\n" +
                 "0이면 죽는 모션이 한 프레임도 안 보이고 그냥 사라진다.")]
        public float corpseLinger = 2.0f;

        [Header("── 보스 ──")]
        public float bossHealth = 700f;
        public float bossMoveSpeed = 1.6f;
        [Tooltip("등장 연출 시간. 이 동안은 때리지 않는다.")]
        public float bossIntro = 2.0f;
        [Tooltip("패턴 사이 쉬는 시간. 이 틈에 때린다.")]
        public float bossRecover = 0.8f;

        [Tooltip("휘두르기 — 앞쪽 직사각형")]
        public float bossSwipeRange = 3.4f;
        public float bossSwipeWidth = 2.6f;
        public float bossSwipeWindup = 0.65f;
        public float bossSwipeDamage = 15f;

        [Tooltip("돌진 — 같은 줄로 옮겨 선 뒤 가로로 끝까지. 깊이 이동으로 피한다.")]
        public float bossChargeWindup = 0.9f;
        public float bossChargeSpeed = 14f;
        public float bossChargeWidth = 1.8f;
        public float bossChargeDamage = 22f;

        [Tooltip("내려찍기 — 주변 원")]
        public float bossSlamWindup = 1.1f;
        public float bossSlamRadius = 3.6f;
        public float bossSlamDamage = 20f;

        [Tooltip("돌진이 벽에 박힌 뒤 멈춰 있는 시간과, 그동안 받는 피해 배율")]
        public float bossGroggyTime = 1.8f;
        public float bossGroggyDamageMul = 1.5f;

        [Tooltip("이 비율 이하로 체력이 떨어지면 분노. 분노하면 예고가 이 배율만큼 빨라진다.")]
        [Range(0f, 1f)] public float bossPhase2At = 0.5f;
        public float bossPhase2Tempo = 1.3f;

        [Tooltip("처치 보상")]
        public int bossRewardCore = 3;
        public int bossRewardEssence = 15;
        public int bossRewardGold = 40;

        [Header("── 타격감 ──")]
        [Tooltip("타격 시 화면 정지 시간. 손맛의 절반이 여기서 나온다.")]
        public float hitStopDuration = 0.06f;
        public float shakeDuration = 0.12f;
        public float shakeAmplitude = 0.22f;
        public float hitFlashDuration = 0.08f;

        [Tooltip("처치타의 히트스톱 배율. 마무리가 제일 무거워야 한다.")]
        public float killHitStopScale = 2.2f;

        [Tooltip("맞은 몸이 찌그러지는 정도와 시간")]
        public float hitPunch = 0.18f;
        public float hitPunchDuration = 0.16f;
    }
}
