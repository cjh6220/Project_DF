using UnityEngine;
using Proto.Core;
using Proto.Data;

namespace Proto.Enemy
{
    /// <summary>
    /// 프로토타입용 최소 AI — 플레이어를 향해 걸어와 근접 공격.
    ///
    /// 피격 반응의 비대칭이 이 게임의 전투를 규정한다.
    ///   플레이어     경직 없음. 맞아도 행동이 끊기지 않는다 (채굴만 중단)
    ///   일반 몬스터  넉백 + 경직. 때리면 밀리고 멈칫한다 → 공간이 생긴다
    ///   강건한 챔피언 슈퍼아머. 넉백도 경직도 통하지 않는다 → 거리를 둬야 한다
    ///
    /// 챔피언은 별도 몬스터가 아니라 접두사가 붙은 일반 몬스터다 (MonsterAffix 참조).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(Health))]
    public class EnemyController : MonoBehaviour
    {
        [SerializeField] TuningConfig cfg;
        [SerializeField] Renderer visual;

        CharacterController _cc;
        Health _health;
        Transform _player;

        float _attackReadyAt;
        float _stunUntil;
        float _hasteUntil;

        /// <summary>공격 예고가 끝나는 시각. 0이면 준비 중이 아니다.</summary>
        float _windupUntil;

        /// <summary>넉백 속도. 경직 시간 동안 이 속도로 밀려난다.</summary>
        Vector3 _knockVel;

        Affix _affix = Affix.None;
        AffixDef _def = Affixes.Normal;

        /// <summary>접두사가 붙은 챔피언인가. 처치 보상이 이 값으로 갈린다.</summary>
        public bool IsChampion => _affix != Affix.None;
        public Affix Affix => _affix;
        public string DisplayName => IsChampion ? _def.Name : "";

        /// <summary>겉모습이 쓴다. 이 프레임에 몸이 향해야 할 방향.</summary>
        public Vector3 LookDirection { get; private set; } = Vector3.right;

        /// <summary>공격을 시작하는 순간(예고 시작). 애니메이션이 여기에 붙는다.</summary>
        public event System.Action Attacked;

        /// <summary>준비하던 공격이 맞아서 끊긴 순간.</summary>
        public event System.Action AttackCanceled;

        /// <summary>공격 준비 중인가. 이때 맞으면 끊긴다.</summary>
        public bool WindingUp => _windupUntil > 0f;

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _health = GetComponent<Health>();
            if (visual == null) visual = GetComponentInChildren<Renderer>();
            _health.Damaged += OnDamaged;
            _health.Died += OnDied;
        }

        public void Configure(TuningConfig config, Transform player, Affix affix)
        {
            cfg = config;
            _player = player;
            _affix = affix;
            _def = Affixes.Get(affix);

            _health.Configure(cfg.enemyHealth * _def.HealthMul);
            transform.localScale = Vector3.one * _def.ScaleMul;
            SetColor(_def.Color);
        }

        void Update()
        {
            if (_health.IsDead || _player == null || cfg == null) return;

            // 넉백 — 밀려나는 동안은 아무것도 못 한다.
            // 순간이동이 아니라 경직 시간에 걸쳐 미끄러져야 눈에 보인다.
            // 슈퍼아머 접두사는 애초에 스턴이 안 걸리므로 여기에 오지 않는다.
            if (Time.time < _stunUntil)
            {
                if (_knockVel.sqrMagnitude > 0.0001f) _cc.Move(_knockVel * Time.deltaTime);
                return;
            }

            var to = _player.position - transform.position;
            to.y = 0f;
            float dist = to.magnitude;
            if (dist > 0.01f) LookDirection = to / dist;

            // 공격 예고 중 — 발을 멈추고 기다린다. 이 구간에 맞으면 취소된다.
            if (_windupUntil > 0f)
            {
                if (Time.time < _windupUntil) return;

                _windupUntil = 0f;
                _attackReadyAt = Time.time + cfg.enemyAttackCooldown;

                // 준비하는 동안 플레이어가 빠져나갔으면 헛스윙이다.
                // 이게 있어야 '예고를 보고 피한다'가 실제로 통한다.
                if (dist <= cfg.enemyAttackRange * 1.3f)
                {
                    var hp = _player.GetComponent<Health>();
                    if (hp != null) hp.TakeDamage(cfg.enemyDamage * _def.DamageMul, transform.position);
                }
                return;
            }

            float speed = cfg.enemyMoveSpeed * _def.SpeedMul;
            if (Time.time < _hasteUntil) speed *= cfg.howlHasteMultiplier;

            if (dist > cfg.enemyAttackRange)
            {
                _cc.SimpleMove(to.normalized * speed);
            }
            else if (Time.time >= _attackReadyAt)
            {
                _windupUntil = Time.time + cfg.enemyWindup;
                Attacked?.Invoke();
            }
        }

        /// <summary>"단단한" 접두사는 받는 피해를 줄인다. 그만큼 전투가 길어지고 피로도를 먹는다.</summary>
        public float ModifyIncomingDamage(float raw) => raw * _def.DamageTakenMul;

        void OnDamaged(float amount, Vector3 src)
        {
            // 모델은 스폰할 때 갈아끼우므로 직렬화된 참조가 죽어 있을 수 있다
            if (visual == null) visual = GetComponentInChildren<Renderer>();
            if (Proto.Feel.Feel.I != null)
            {
                // 몸 전체가 번쩍이고, 맞은 쪽으로 찌그러진다.
                // 슈퍼아머도 번쩍이긴 한다 — 맞았다는 건 알려줘야 한다. 대신 덜 찌그러진다.
                Proto.Feel.Feel.I.Flash(transform);
                Proto.Feel.Feel.I.Punch(Body, _def.SuperArmor ? 0.4f : 1f);
            }

            // 슈퍼아머 — 넉백도 경직도 없고 공격도 안 끊긴다.
            // '강건한' 접두사만 여기 해당한다. 다른 챔피언은 일반과 똑같이 밀린다.
            if (_def.SuperArmor) return;

            // 준비하던 공격은 끊긴다. 먼저 때리면 안 맞는다.
            if (_windupUntil > 0f)
            {
                _windupUntil = 0f;
                AttackCanceled?.Invoke();
            }

            _stunUntil = Time.time + cfg.enemyHitStun;

            var push = transform.position - src;
            push.y = 0f;
            _knockVel = push.sqrMagnitude > 0.001f
                ? push.normalized * (cfg.enemyKnockback / Mathf.Max(0.01f, cfg.enemyHitStun))
                : Vector3.zero;
        }

        Transform _body;

        /// <summary>찌그러뜨릴 모델. 루트를 건드리면 접두사 크기와 충돌한다.</summary>
        Transform Body
        {
            get
            {
                if (_body == null)
                {
                    var an = GetComponentInChildren<Animator>();
                    _body = an != null && an.transform != transform ? an.transform
                          : (visual != null ? visual.transform : null);
                }
                return _body;
            }
        }

        void OnDied()
        {
            _windupUntil = 0f;
            _knockVel = Vector3.zero;
            if (_def.ExplodeOnDeath) Explode();
            if (_def.HasteAlliesOnDeath) HasteAllies();
        }

        /// <summary>"폭발하는" — 빨리 죽이면 손해다. 거리를 두고 처리해야 한다.</summary>
        void Explode()
        {
            if (_player == null) return;
            float d = Vector3.Distance(transform.position, _player.position);
            if (d > cfg.explodeRadius) return;

            var hp = _player.GetComponent<Health>();
            if (hp != null) hp.TakeDamage(cfg.explodeDamage, transform.position);

            if (Proto.Feel.Feel.I != null)
            {
                Proto.Feel.Feel.I.HitStop(1.5f);
                Proto.Feel.Feel.I.Shake((_player.position - transform.position).normalized, 1.6f);
                Proto.Feel.Haptics.Pulse(0.5f, 0.6f, 0.18f);
            }
        }

        /// <summary>"울부짖는" — 죽으면 남은 적들이 빨라진다. 처치 순서가 중요해진다.</summary>
        void HasteAllies()
        {
            foreach (var e in FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
            {
                if (e == this) continue;
                e.ApplyHaste(cfg.howlHasteDuration);
            }
        }

        public void ApplyHaste(float duration)
        {
            _hasteUntil = Mathf.Max(_hasteUntil, Time.time + duration);
        }

        void SetColor(Color c)
        {
            var t = GetComponentInChildren<Proto.Feel.Tint>();
            if (t != null) { t.Set(c); return; }

            // 모델이 여러 렌더러(스킨드 메시 포함)로 나뉘어 있을 수 있다
            var block = new MaterialPropertyBlock();
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                r.GetPropertyBlock(block);
                block.SetColor("_BaseColor", c);
                block.SetColor("_Color", c);
                r.SetPropertyBlock(block);
            }
        }
    }
}
