using UnityEngine;
using Proto.Core;

namespace Proto.Enemy
{
    /// <summary>
    /// 몬스터의 겉모습 — 모델 장착, 애니메이션, 바라보는 방향, 접두사 색.
    ///
    /// 모델을 스폰할 때 붙인다. 스테이지 테마가 어떤 동물을 쓸지 정하므로
    /// 프리팹을 종류별로 만들 필요가 없다.
    ///
    /// 팩마다 애니메이터 파라미터 이름이 제각각이다.
    ///   늑대·멧돼지·곰·호랑이  isWalking / isRunning / isAttacking / isDead
    ///   거미                  isDead 대신 isDead1/2/3, isRunning 없음
    ///   여우                  죽는 애니메이션 자체가 없음
    /// 그래서 이름을 강제하지 않고, 있는 파라미터만 골라서 건드린다.
    /// </summary>
    [RequireComponent(typeof(EnemyController))]
    public class EnemyVisual : MonoBehaviour
    {
        [SerializeField] Animator animator;
        [SerializeField] Transform model;
        [SerializeField] float turnSpeed = 10f;

        [Tooltip("이 속도를 넘으면 걷기가 아니라 달리기로 친다")]
        [SerializeField] float runThreshold = 2.6f;

        [Tooltip("공격 모션을 켜 두는 시간. 볼 파라미터라 직접 꺼 줘야 한다.\n" +
                 "Tuning의 enemyWindup(예고 시간)보다 조금 길어야 때리는 순간까지 이어진다.")]
        [SerializeField] float attackHold = 0.7f;

        EnemyController _enemy;
        CharacterController _cc;
        Health _health;

        // 이 애니메이터가 실제로 가진 파라미터만 건드린다
        bool _hasWalking, _hasRunning, _hasAttacking, _hasDead, _hasDead1, _hasStanding;
        bool _hasSpeed, _hasAttackTrigger, _hasDeadTrigger;

        // 팩 컨트롤러는 공격·사망 전이를 Idle에서만 걸어 뒀다.
        // 걷거나 때리는 중에 죽으면 죽는 모션에 영영 도달하지 못한다.
        // 그래서 파라미터만 믿지 않고 상태를 직접 재생한다.
        static readonly string[] AttackStateNames = { "Attack", "Attack 1", "Attack1" };
        static readonly string[] DeathStateNames  = { "Death", "Death 1", "Dead" };
        static readonly string[] IdleStateNames    = { "Idle", "Idle 0" };
        string _attackState, _deathState, _idleState;

        float _attackUntil;
        bool _dead;

        void Awake()
        {
            _enemy = GetComponent<EnemyController>();
            _cc = GetComponent<CharacterController>();
            _health = GetComponent<Health>();

            _enemy.Attacked += OnAttacked;
            _enemy.AttackCanceled += OnAttackCanceled;
            if (_health != null) _health.Died += OnDied;

            if (animator == null) animator = GetComponentInChildren<Animator>();
            CacheParameters();
        }

        void OnDestroy()
        {
            if (_enemy != null) { _enemy.Attacked -= OnAttacked; _enemy.AttackCanceled -= OnAttackCanceled; }
            if (_health != null) _health.Died -= OnDied;
        }

        /// <summary>
        /// 스폰 시점에 모델을 붙인다.
        /// 팩이 붙여 둔 이동 스크립트는 끄기만 한다 — 런타임 Destroy는
        /// [RequireComponent] 의존성 때문에 순서를 타서 지저분해진다.
        /// </summary>
        public void SetModel(GameObject prefab)
        {
            if (prefab == null) return;
            if (model != null) Destroy(model.gameObject);

            var go = Instantiate(prefab, transform);
            go.name = "Model";
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;

            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true)) mb.enabled = false;
            foreach (var cc in go.GetComponentsInChildren<CharacterController>(true)) cc.enabled = false;
            foreach (var col in go.GetComponentsInChildren<Collider>(true)) Destroy(col);
            foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true)) Destroy(rb);

            model = go.transform;
            animator = go.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                animator.applyRootMotion = false;
                // 클립에 박힌 AnimationEvent를 받아 줄 것이 필요하다 (없으면 경고 폭탄)
                if (animator.GetComponent<AnimalEventSink>() == null)
                    animator.gameObject.AddComponent<AnimalEventSink>();
            }

            _dead = false;
            _attackUntil = 0f;
            CacheParameters();
        }

        void CacheParameters()
        {
            _hasWalking = _hasRunning = _hasAttacking = _hasDead = _hasDead1 = _hasStanding = false;
            _hasSpeed = _hasAttackTrigger = _hasDeadTrigger = false;
            if (animator == null || animator.runtimeAnimatorController == null) return;

            _attackState = FindState(AttackStateNames);
            _deathState = FindState(DeathStateNames);
            _idleState = FindState(IdleStateNames);

            foreach (var p in animator.parameters)
            {
                switch (p.name)
                {
                    case "isWalking":   _hasWalking = true; break;
                    case "isRunning":   _hasRunning = true; break;
                    case "isAttacking": _hasAttacking = true; break;
                    case "isDead":      _hasDead = true; break;
                    case "isDead1":     _hasDead1 = true; break;
                    case "isStanding":  _hasStanding = true; break;
                    case "Speed":       _hasSpeed = true; break;
                    case "Attack":      _hasAttackTrigger = true; break;
                    case "Dead":        _hasDeadTrigger = true; break;
                }
            }
        }

        string FindState(string[] candidates)
        {
            foreach (var n in candidates)
                if (animator.HasState(0, Animator.StringToHash(n))) return n;
            return null;
        }

        void Update()
        {
            if (animator == null) return;

            if (!_dead)
            {
                var v = _cc != null ? _cc.velocity : Vector3.zero;
                v.y = 0f;
                float speed = v.magnitude;

                if (_hasSpeed) animator.SetFloat("Speed", speed);

                bool moving = speed > 0.1f;
                bool running = speed > runThreshold;
                if (_hasWalking) animator.SetBool("isWalking", moving && !running);
                if (_hasRunning) animator.SetBool("isRunning", running);

                if (Time.time > _attackUntil)
                {
                    if (_hasAttacking) animator.SetBool("isAttacking", false);
                    if (_hasStanding) animator.SetBool("isStanding", false);
                }
            }

            if (model == null) return;

            var look = _enemy.LookDirection;
            look.y = 0f;
            if (look.sqrMagnitude < 0.0001f) return;

            model.rotation = Quaternion.Slerp(
                model.rotation, Quaternion.LookRotation(look.normalized), turnSpeed * Time.deltaTime);
        }

        void OnAttacked()
        {
            if (animator == null || _dead) return;
            if (_hasAttackTrigger) animator.SetTrigger("Attack");
            if (_attackState != null) animator.CrossFadeInFixedTime(_attackState, 0.06f, 0);
            if (_hasAttacking)
            {
                animator.SetBool("isAttacking", true);
                // 곰은 일어서야 때린다. 그런 모델을 위해 같이 켜 준다.
                if (_hasStanding) animator.SetBool("isStanding", true);
                _attackUntil = Time.time + attackHold;
            }
        }

        /// <summary>맞아서 공격이 끊겼다. 휘두르던 모션을 즉시 접는다.</summary>
        void OnAttackCanceled()
        {
            if (animator == null || _dead) return;
            _attackUntil = 0f;
            if (_hasAttacking) animator.SetBool("isAttacking", false);
            if (_hasStanding) animator.SetBool("isStanding", false);
            if (_idleState != null) animator.CrossFadeInFixedTime(_idleState, 0.08f, 0);
        }

        void OnDied()
        {
            if (animator == null) return;
            _dead = true;

            if (_hasWalking) animator.SetBool("isWalking", false);
            if (_hasRunning) animator.SetBool("isRunning", false);
            if (_hasAttacking) animator.SetBool("isAttacking", false);
            if (_hasStanding) animator.SetBool("isStanding", false);

            if (_hasDeadTrigger) animator.SetTrigger("Dead");
            if (_hasDead) animator.SetBool("isDead", true);
            else if (_hasDead1) animator.SetBool("isDead1", true);

            // 파라미터로 못 가는 경우가 대부분이라 직접 재생한다
            if (_deathState != null) animator.CrossFadeInFixedTime(_deathState, 0.12f, 0);
            // 여우처럼 죽는 모션이 아예 없는 모델도 있다. 그냥 사라진다.
        }
    }
}
