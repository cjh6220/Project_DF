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
    /// 입력은 GameInput 한곳에서 읽는다 (키보드 X 공격 · C 채굴 / 패드 □ · ×).
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

        Vector2 _moveInput;

        // ───────────── 3단 콤보 ─────────────
        //
        //   X → 1타 → (판정 뒤 X) → 2타 → (판정 뒤 X) → 3타(마무리 2연격)
        //
        // 시간은 전부 '클립 시간'(초)이다. 실제 시간 = 클립 시간 / Speed.
        // 숫자는 카타나 3콤보 클립의 손 속도를 재서 칼이 가장 빠르게 지나가는 순간에 맞췄다.
        //   Hits      판정 시각과 피해 배율
        //   BufferAt  이 시각부터 다음 X 입력을 받아 둔다 (미리 눌러도 씹히지 않게)
        //   CancelAt  이 시각부터 다음 타로 넘어갈 수 있다
        //   EndAt     모션이 멈추는 지점. 여기서 콤보가 끝나고 그때부터 이동·채굴이 된다
        //             (손 속도를 재서 클립이 자세를 다 돌아온 순간으로 잡았다)
        //   LungeFrom~To  앞으로 내딛는 구간. 거리는 Tuning.asset의 comboLungeIdle / comboLungeHeld
        struct ComboStep
        {
            public float Speed, BufferAt, CancelAt, EndAt, LungeFrom, LungeTo;
            public float[] HitAt, HitMul;
        }

        static readonly ComboStep[] Combo =
        {
            new ComboStep { Speed = 1.35f, HitAt = new[] { 0.38f }, HitMul = new[] { 1.0f },
                            BufferAt = 0.20f, CancelAt = 0.52f, EndAt = 1.20f, LungeFrom = 0.15f, LungeTo = 0.45f },
            new ComboStep { Speed = 1.30f, HitAt = new[] { 0.22f }, HitMul = new[] { 1.1f },
                            BufferAt = 0.08f, CancelAt = 0.38f, EndAt = 0.85f, LungeFrom = 0.03f, LungeTo = 0.30f },
            // 3타 — 마무리 한 방. 클립에는 앞쪽에 작은 휘두름이 하나 더 있지만 판정은 큰 것 하나만.
            new ComboStep { Speed = 1.25f, HitAt = new[] { 0.80f }, HitMul = new[] { 2.0f },
                            BufferAt = 9f, CancelAt = 1.05f, EndAt = 1.35f, LungeFrom = 0.50f, LungeTo = 0.84f },
        };

        int _step;            // 0 = 공격 중 아님, 1~3 = 몇 번째 타
        float _stepTime;      // 이 타에서 흐른 클립 시간
        int _nextHit;         // 이 타에서 아직 안 나간 판정 번호
        bool _buffered;       // 다음 타 입력을 받아 둠

        /// <summary>지금 몇 번째 타인가 (0 = 공격 중 아님). 애니메이터가 이걸 따른다.</summary>
        public int ComboStepIndex => _step;
        public bool IsAttacking => _step > 0;

        /// <summary>콤보가 끝나서 대기 자세로 돌아갈 때.</summary>
        public event Action ComboEnded;

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
        public void SetConfig(TuningConfig c)
        {
            if (_baseCfg == null) _baseCfg = cfg;   // 씬에 연결된 원본 에셋을 기억해 둔다
            cfg = c;
        }
        TuningConfig _baseCfg;

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

            GameInput.Ensure();
        }


        void Update()
        {
            if (!ControlEnabled)
            {
                // 멈춰 세운다. Move를 안 부르면 velocity가 마지막 값에 남아 제자리 달리기를 한다.
                if (_cc.enabled) _cc.SimpleMove(Vector3.zero);
                if (IsMining) CancelMining();
                if (_step > 0) EndCombo(false);
                return;
            }

            _moveInput = GameInput.MoveValue;
            if (IsMining || _step > 0) { UpdateRun(); }

            if (IsMining) { TickMining(); return; }

            if (_step > 0) { TickCombo(); return; }

            TickMove();

            if (GameInput.AttackPressed) StartStep(1);
            else if (GameInput.MinePressed) TryStartMining();
            // Z(스킬)와 커맨드는 스킬 시스템에서 붙인다
        }

        // ── 걷기 · 달리기 ──
        // 좌우 방향키를 한 번 누르면 걷고, 짧은 사이에 두 번 누르면 달린다 (손을 떼거나 반대로 누르면 다시 걷기).
        // 패드 스틱은 끝까지 빠르게 두 번 밀면 달린다.
        const float DoubleTapWindow = 0.28f;
        int _lastTapDir;          // -1 왼쪽 · +1 오른쪽
        float _lastTapAt = -10f;
        int _heldDir;
        public bool IsRunning { get; private set; }

        void UpdateRun()
        {
            int dir = _moveInput.x > 0.5f ? 1 : _moveInput.x < -0.5f ? -1 : 0;
            if (dir != 0 && _heldDir != dir)
            {
                // 새로 눌렀다 — 같은 쪽을 방금 전에도 눌렀으면 달리기
                float now = Time.unscaledTime;
                IsRunning = dir == _lastTapDir && now - _lastTapAt <= DoubleTapWindow;
                _lastTapDir = dir;
                _lastTapAt = now;
            }
            if (dir == 0) IsRunning = false;
            _heldDir = dir;
        }

        void TickMove()
        {
            UpdateRun();
            var dir = new Vector3(_moveInput.x, 0f, _moveInput.y);
            if (dir.sqrMagnitude > 1f) dir.Normalize();

            _cc.SimpleMove(dir * (IsRunning ? cfg.moveSpeed : cfg.walkSpeed));

            // 좌우 방향만 바라본다 (벨트스크롤이므로 깊이 방향은 바라보지 않는다)
            if (Mathf.Abs(_moveInput.x) > 0.01f)
                FacingRight = _moveInput.x > 0f;
        }

        // ───────────────────────────── 콤보 ─────────────────────────────

        void StartStep(int n)
        {
            // 타를 시작할 때만 방향을 바꿀 수 있다 — 휘두르는 도중에는 돌지 않는다
            if (Mathf.Abs(_moveInput.x) > 0.3f) FacingRight = _moveInput.x > 0f;

            _step = n;
            _stepTime = 0f;
            _nextHit = 0;
            _buffered = false;
            Attacked?.Invoke();
            if (Proto.Feel.Feel.I != null) Proto.Feel.Feel.I.SwingSfx();
        }

        void TickCombo()
        {
            var c = Combo[_step - 1];
            float prev = _stepTime;
            // Time.deltaTime은 히트스톱(시간 정지) 동안 거의 0 — 콤보도 같이 멈춘다
            _stepTime += Time.deltaTime * c.Speed;

            // 판정 — 칼이 몸 앞을 지나는 순간. 누르는 순간에 맞으면 '허공을 쳤는데 맞았다'가 된다.
            while (_nextHit < c.HitAt.Length && _stepTime >= c.HitAt[_nextHit])
            {
                bool finisher = _step == Combo.Length && _nextHit == c.HitAt.Length - 1;
                ResolveHit(c.HitMul[_nextHit], finisher);
                _nextHit++;
            }

            // 내딛기 — 휘두르는 구간에 맞춰 앞으로. 좌우로만 움직인다.
            // 바라보는 쪽 방향키를 누르고 있는 동안은 멀리, 떼면 그 순간부터 조금만.
            float a = Mathf.Clamp01(Mathf.InverseLerp(c.LungeFrom, c.LungeTo, prev));
            float b = Mathf.Clamp01(Mathf.InverseLerp(c.LungeFrom, c.LungeTo, _stepTime));
            var fwd = FacingRight ? Vector3.right : Vector3.left;
            bool pushing = FacingRight ? _moveInput.x > 0.3f : _moveInput.x < -0.3f;
            float dist = LungeDistance(_step - 1, pushing);
            _cc.Move(fwd * dist * (b - a) + Vector3.down * 2f * Time.deltaTime);

            // 다음 타 입력 받기 — 너무 이르면 연타가 전부 씹히고, 늦으면 끊긴다
            if (_step < Combo.Length && _stepTime >= c.BufferAt && GameInput.AttackPressed) _buffered = true;

            if (_stepTime >= c.CancelAt)
            {
                if (_buffered) { StartStep(_step + 1); return; }
                // 방향키로 후딜을 끊지 않는다 — 모션이 끝나기 전에 미끄러지듯 걸어 나가면 어색하다.
                // 이동·채굴은 EndAt(모션이 멈추는 지점) 뒤에야 된다.
                // 3타를 다 쓴 뒤에도 X를 누르면 1타부터 다시
                if (_step == Combo.Length && GameInput.AttackPressed) { StartStep(1); return; }
            }

            if (_stepTime >= c.EndAt) EndCombo(true);
        }

        /// <summary>
        /// 내딛는 거리. 원본 Tuning.asset에서 바로 읽는다 — 플레이 중에 숫자를 바꿔도 즉시 반영된다.
        /// (다른 값은 성장 노드가 반영된 런타임 사본을 쓰지만, 이 값은 성장과 무관하다.)
        /// </summary>
        float LungeDistance(int i, bool pushing)
        {
            var src = _baseCfg != null ? _baseCfg : cfg;
            var arr = pushing ? src.comboLungeHeld : src.comboLungeIdle;
            if (arr == null || arr.Length == 0) return 0f;
            return arr[Mathf.Min(i, arr.Length - 1)];
        }

        void EndCombo(bool notify)
        {
            _step = 0;
            _buffered = false;
            if (notify) ComboEnded?.Invoke();
        }

        void ResolveHit(float mul, bool finisher)
        {
            Vector3 facing = FacingRight ? Vector3.right : Vector3.left;
            var hits = Physics.OverlapSphere(transform.position, cfg.attackRange, enemyMask);
            int connected = 0;
            bool killed = false;
            var feel = Proto.Feel.Feel.I;

            // 검기 — 맞든 안 맞든 칼이 지나간 자리에 남는다. 1타 내려 베기, 2타 올려 베기, 3타 크게 + 일섬
            Vector3 swing = transform.position + facing * 0.95f + Vector3.up * 1.05f;
            if (finisher)
            {
                Proto.Feel.HitFx.Slash(swing, FacingRight, 2, 3.8f);
                Proto.Feel.HitFx.Line(swing + facing * 0.8f, FacingRight, 5.5f);
            }
            else Proto.Feel.HitFx.Slash(swing, FacingRight, (_step - 1) % 2, 3.0f);

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
                float dmg = cfg.attackDamage * mul;
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
                    Proto.Feel.HitFx.Impact(at, kill || finisher);
                    feel.Number(at + Vector3.up * 0.6f, dmg,
                        kill ? Proto.Feel.Feel.NumberKind.Kill : Proto.Feel.Feel.NumberKind.Normal);
                }
            }

            if (connected == 0 || feel == null) return;

            // 처치타·마무리타는 더 길게 멈추고 더 세게 흔든다 — 마무리가 제일 무거워야 한다
            if (killed || finisher)
            {
                feel.HitStop(cfg.killHitStopScale);
                feel.Shake(facing, 1.5f);
                feel.KillSfx();
                Proto.Feel.Haptics.Pulse(0.45f, 0.7f, 0.14f);   // 처치 · 마무리 — 묵직하게
            }
            else
            {
                // 여러 마리를 한꺼번에 치면 조금 더 묵직하게
                feel.HitStop(1f + 0.15f * Mathf.Min(connected - 1, 3));
                feel.Shake(facing);
                feel.HitSfx();
                Proto.Feel.Haptics.Pulse(0.18f, 0.35f, 0.07f);  // 타격 — 짧고 가볍게
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
            if (!GameInput.MineHeld) { CancelMining(); return; }

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
                Proto.Feel.Haptics.Pulse(0.7f, 0.45f, 0.2f);   // 피격 — 둔하고 길게
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
