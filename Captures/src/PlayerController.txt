using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Proto.Core;
using Proto.Data;
using Proto.Mining;

namespace Proto.Player
{
    /// <summary>
    /// 던전앤파이터식 벨트스크롤 조작.
    ///   X = 좌우, Z = 깊이(위아래). 깊이 이동으로 공격을 피한다.
    ///   공격은 전방 부채꼴 판정. 히트스톱으로 손맛을 만든다.
    ///   채굴 중에는 이동·공격 불가, 피격 시 중단.
    ///
    /// 입력은 Input System을 코드로 직접 구성한다 (.inputactions 에셋 불필요).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(Health))]
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] TuningConfig cfg;
        [SerializeField] Transform visual;
        [SerializeField] LayerMask enemyMask;

        CharacterController _cc;
        Health _health;

        InputAction _move, _attack, _mine;
        Vector2 _moveInput;
        float _attackReadyAt;

        // 휘두른 뒤 판정이 들어갈 시각 (-1이면 대기 중인 타격 없음)
        float _pendingHitAt = -1f;
        Vector3 _pendingFacing = Vector3.right;

        /// <summary>지금 휘두르는 모션 번호 (0/1 번갈아). 애니메이터가 이걸 따른다.</summary>
        public int AttackIndex { get; private set; } = 1;

        // 채굴 상태
        OreNode _target;
        float _mineProgress;
        public bool IsMining => _target != null;

        public event Action<float> MineProgressChanged;  // 0~1, 채굴 중이 아니면 -1
        public event Action<OreNode> OreMined;
        public event Action Attacked;

        public bool ControlEnabled { get; set; } = true;

        /// <summary>바라보는 쪽. 공격 판정과 모델 회전이 이 값을 따른다.</summary>
        public bool FacingRight { get; private set; } = true;

        /// <summary>RunManager가 런타임 사본을 물려준다 (성장 노드 반영본).</summary>
        public void SetConfig(TuningConfig c) => cfg = c;

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _health = GetComponent<Health>();
            if (cfg != null) _health.Configure(cfg.maxHealth);
            _health.Damaged += OnDamaged;

            // 몬스터에 막혀 못 움직이는 일은 없어야 한다. 벨트스크롤에서 몸끼리 막히면
            // 몰렸을 때 빠져나갈 길이 없다. 공격 판정은 OverlapSphere라 충돌과 무관하다.
            // 프로젝트 충돌 매트릭스에도 꺼 두었지만, 설정이 날아가도 보장되게 여기서 한 번 더 끈다.
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            if (enemyLayer >= 0) Physics.IgnoreLayerCollision(gameObject.layer, enemyLayer, true);

            BuildInput();
        }

        void BuildInput()
        {
            _move = new InputAction("Move", InputActionType.Value);
            _move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            _move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            _move.AddBinding("<Gamepad>/leftStick");

            _attack = new InputAction("Attack", InputActionType.Button);
            _attack.AddBinding("<Keyboard>/j");
            _attack.AddBinding("<Mouse>/leftButton");
            _attack.AddBinding("<Gamepad>/buttonWest");

            _mine = new InputAction("Mine", InputActionType.Button);
            _mine.AddBinding("<Keyboard>/k");
            _mine.AddBinding("<Gamepad>/buttonSouth");
        }

        void OnEnable() { _move.Enable(); _attack.Enable(); _mine.Enable(); }
        void OnDisable() { _move.Disable(); _attack.Disable(); _mine.Disable(); }

        void Update()
        {
            if (!ControlEnabled) return;

            _moveInput = _move.ReadValue<Vector2>();

            if (_pendingHitAt >= 0f && Time.time >= _pendingHitAt) ResolveHit();

            if (IsMining) { TickMining(); return; }

            TickMove();

            if (_attack.WasPressedThisFrame() && Time.time >= _attackReadyAt) DoAttack();
            if (_mine.WasPressedThisFrame() && _pendingHitAt < 0f) TryStartMining();
        }

        void TickMove()
        {
            var dir = new Vector3(_moveInput.x, 0f, _moveInput.y);
            if (dir.sqrMagnitude > 1f) dir.Normalize();

            _cc.SimpleMove(dir * cfg.moveSpeed);

            // 좌우 방향만 바라본다 (벨트스크롤이므로 깊이 방향은 바라보지 않는다)
            if (Mathf.Abs(_moveInput.x) > 0.01f)
                FacingRight = _moveInput.x > 0f;
        }

        void DoAttack()
        {
            _attackReadyAt = Time.time + cfg.attackCooldown;

            // 두 가지 모션을 번갈아 쓴다. 판정 타이밍이 모션마다 다르므로 여기서 정한다.
            AttackIndex = 1 - AttackIndex;
            Attacked?.Invoke();
            if (Proto.Feel.Feel.I != null) Proto.Feel.Feel.I.SwingSfx();

            // 누르는 순간이 아니라 무기가 몸 앞을 지나는 순간에 맞는다.
            // 판정이 모션보다 먼저 오면 '허공을 쳤는데 맞았다'가 되어 손맛이 죽는다.
            _pendingFacing = FacingRight ? Vector3.right : Vector3.left;
            _pendingHitAt = Time.time + (AttackIndex == 0 ? cfg.attackHitDelay1 : cfg.attackHitDelay2);
        }

        void ResolveHit()
        {
            _pendingHitAt = -1f;
            Vector3 facing = _pendingFacing;
            var hits = Physics.OverlapSphere(transform.position, cfg.attackRange, enemyMask);
            int connected = 0;
            bool killed = false;
            var feel = Proto.Feel.Feel.I;

            // 한 몸에 콜라이더가 여럿이면(이동용 CharacterController + 피격용) 두 번 맞는다.
            // 대상은 Health 단위로 한 번만 친다.
            var struck = new System.Collections.Generic.HashSet<Health>();

            foreach (var h in hits)
            {
                var to = h.transform.position - transform.position;
                to.y = 0f;
                if (Vector3.Angle(facing, to) > cfg.attackArcDegrees * 0.5f) continue;

                var hp = h.GetComponentInParent<Health>();
                if (hp == null || hp.IsDead) continue;
                if (hp.gameObject == gameObject) continue;
                if (!struck.Add(hp)) continue;

                // "단단한" 같은 접두사는 받는 피해를 줄인다.
                // 그만큼 전투가 길어지고, 길어진 만큼 피로도를 먹는다.
                float dmg = cfg.attackDamage;
                var ec = h.GetComponentInParent<Proto.Enemy.EnemyController>();
                if (ec != null) dmg = ec.ModifyIncomingDamage(dmg);
                var boss = h.GetComponentInParent<Proto.Enemy.BossController>();
                if (boss != null) dmg = boss.ModifyIncomingDamage(dmg);   // 그로기 동안 더 아프다

                hp.TakeDamage(dmg, transform.position);
                connected++;
                bool kill = hp.IsDead;
                killed |= kill;

                if (feel != null)
                {
                    Vector3 at = HitPoint(hp.transform, facing);
                    feel.Spark(at, facing, kill);
                    feel.Number(at + Vector3.up * 0.6f, dmg,
                        kill ? Proto.Feel.Feel.NumberKind.Kill : Proto.Feel.Feel.NumberKind.Normal);
                }
            }

            if (connected == 0 || feel == null) return;

            // 처치타는 더 길게 멈추고 더 세게 흔든다 — 마무리가 제일 무거워야 한다
            if (killed)
            {
                feel.HitStop(cfg.killHitStopScale);
                feel.Shake(facing, 1.5f);
                feel.KillSfx();
            }
            else
            {
                // 여러 마리를 한꺼번에 치면 조금 더 묵직하게
                feel.HitStop(1f + 0.15f * Mathf.Min(connected - 1, 3));
                feel.Shake(facing);
                feel.HitSfx();
            }
        }

        /// <summary>맞은 쪽 몸통, 때린 사람 쪽 표면 즈음.</summary>
        static Vector3 HitPoint(Transform target, Vector3 facing)
        {
            var cc = target.GetComponent<CharacterController>();
            float s = target.lossyScale.y;
            float h = cc != null ? (cc.center.y + cc.height * 0.1f) * s : 0.9f;
            float r = cc != null ? cc.radius * s : 0.4f;
            return target.position + Vector3.up * h - facing * r * 0.6f;
        }

        void TryStartMining()
        {
            OreNode best = null;
            float bestDist = float.MaxValue;

            foreach (var ore in FindObjectsByType<OreNode>(FindObjectsSortMode.None))
            {
                if (ore.Depleted) continue;
                float d = Vector3.Distance(transform.position, ore.transform.position);
                if (d < cfg.mineRange && d < bestDist) { best = ore; bestDist = d; }
            }

            if (best == null) return;
            _target = best;
            _mineProgress = 0f;
            MineProgressChanged?.Invoke(0f);
        }

        void TickMining()
        {
            // 캐는 도중에 키를 떼면 중단
            if (!_mine.IsPressed()) { CancelMining(); return; }

            _mineProgress += Time.deltaTime / Mathf.Max(0.01f, cfg.mineDuration);
            MineProgressChanged?.Invoke(Mathf.Clamp01(_mineProgress));

            if (_mineProgress >= 1f)
            {
                var ore = _target;
                _target = null;
                MineProgressChanged?.Invoke(-1f);
                ore.Deplete();
                OreMined?.Invoke(ore);
            }
        }

        void CancelMining()
        {
            _target = null;
            _mineProgress = 0f;
            MineProgressChanged?.Invoke(-1f);
        }

        void OnDamaged(float amount, Vector3 src)
        {
            // 플레이어는 피격 경직이 없다. 맞아도 이동·공격이 끊기지 않고
            // 넉백도 받지 않는다. 피드백은 화면 흔들림과 HP로만 준다.
            //
            // 대신 채굴은 중단된다 — 캐는 동안 무방비라는 것이
            // 이 게임에서 유일하게 피격이 플레이어를 멈추는 지점이다.
            if (IsMining)
            {
                _mineProgress *= cfg.mineProgressKeptOnHit;
                if (cfg.mineProgressKeptOnHit <= 0f) CancelMining();
                else MineProgressChanged?.Invoke(Mathf.Clamp01(_mineProgress));
            }

            if (Proto.Feel.Feel.I != null)
            {
                Proto.Feel.Feel.I.Shake((transform.position - src).normalized, 0.7f);
                Proto.Feel.Feel.I.Flash(visual != null ? visual : transform);
                Proto.Feel.Feel.I.Number(transform.position + Vector3.up * 2.1f, amount, Proto.Feel.Feel.NumberKind.Player);
            }
        }

        void OnDrawGizmosSelected()
        {
            if (cfg == null) return;
            Gizmos.color = new Color(1f, 0.4f, 0.3f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, cfg.attackRange);
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, cfg.mineRange);
        }
    }
}
