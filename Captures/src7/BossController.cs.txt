using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Proto.Core;
using Proto.Data;

namespace Proto.Enemy
{
    /// <summary>
    /// 스테이지 보스 — 1스테이지 '실버백'.
    ///
    /// 일반 몬스터와 달리 늘 슈퍼아머다. 밀리지도 끊기지도 않는다.
    /// 대신 모든 공격에 바닥 예고가 있고, 피하는 방법이 패턴마다 다르다.
    ///
    ///   휘두르기  가까이 붙으면. 앞쪽 직사각형 → 옆(깊이)으로 비키거나 뒤로 뺀다
    ///   돌진      플레이어와 같은 줄로 옮겨 선 뒤 가로로 끝까지 달린다
    ///             → 줄(깊이)을 바꿔 피한다. 벽에 박으면 그로기 — 딜 타임
    ///   내려찍기  가슴을 두드린 뒤 주변 원 → 원 밖으로 나간다
    ///
    ///   체력 50% 이하 → 포효하고 분노. 예고가 짧아지고 돌진을 두 번 한다
    ///
    /// 그로기 동안은 받는 피해가 늘어난다. "피하고 → 때린다"의 리듬이 여기서 나온다.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(Health))]
    public class BossController : MonoBehaviour
    {
        public static BossController Current { get; private set; }
        public static event Action<BossController> Spawned;

        public event Action Enraged;
        public event Action Groggy;

        public string DisplayName { get; private set; } = "보스";
        public string Title { get; private set; } = "";
        public bool Phase2 { get; private set; }
        public bool IsGroggy => Time.time < _groggyUntil;
        public Health Health => _hp;

        TuningConfig _cfg;
        Transform _player;
        Health _playerHp;
        Vector3 _center;
        Vector2 _half;
        CharacterController _cc;
        Health _hp;
        Animator _anim;
        Transform _model;
        float _groggyUntil;
        Vector3 _face = Vector3.left;
        readonly List<Telegraph> _telegraphs = new List<Telegraph>();

        float Tempo => Phase2 ? _cfg.bossPhase2Tempo : 1f;

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _hp = GetComponent<Health>();
            _hp.Damaged += OnDamaged;
            _hp.Died += OnDied;
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
            ClearTelegraphs();
        }

        public void Configure(TuningConfig cfg, Transform player, Vector3 arenaCenter, Vector2 arenaHalf,
                              GameObject modelPrefab, float modelScale, string displayName, string title)
        {
            _cfg = cfg;
            _player = player;
            _playerHp = player != null ? player.GetComponent<Health>() : null;
            _center = arenaCenter;
            _half = arenaHalf;
            DisplayName = displayName;
            Title = title;

            transform.localScale = Vector3.one * modelScale;
            _hp.Configure(cfg.bossHealth);
            SetModel(modelPrefab);

            Current = this;
            Spawned?.Invoke(this);
            StartCoroutine(Brain());
        }

        void SetModel(GameObject prefab)
        {
            if (prefab == null) return;
            var go = Instantiate(prefab, transform);
            go.name = "Model";
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            // 팩이 붙여 둔 이동 스크립트·충돌은 쓰지 않는다
            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true)) mb.enabled = false;
            foreach (var c in go.GetComponentsInChildren<CharacterController>(true)) c.enabled = false;
            foreach (var col in go.GetComponentsInChildren<Collider>(true)) Destroy(col);
            foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true)) Destroy(rb);
            _model = go.transform;
            _anim = go.GetComponentInChildren<Animator>();
            if (_anim != null)
            {
                _anim.applyRootMotion = false;
                if (_anim.GetComponent<AnimalEventSink>() == null) _anim.gameObject.AddComponent<AnimalEventSink>();
            }
        }

        // ───────────────────────────── 두뇌 ─────────────────────────────

        IEnumerator Brain()
        {
            // 등장 — 가슴을 두드리며 이름을 알린다. 이 동안은 때리지 않는다.
            FacePlayer(true);
            Play("ChestHit", 1f);
            yield return new WaitForSeconds(_cfg.bossIntro);

            int last = -1;
            while (!_hp.IsDead)
            {
                if (!Phase2 && _hp.Current <= _hp.Max * _cfg.bossPhase2At)
                {
                    yield return Enrage();
                    continue;
                }

                float d = FlatDistance();
                int pick;
                if (d <= _cfg.bossSwipeRange * 0.85f) pick = UnityEngine.Random.value < 0.6f ? 0 : 2;
                else if (d <= 6f) pick = UnityEngine.Random.value < 0.45f ? 1 : -1;
                else pick = 1;

                // 같은 패턴만 반복하지 않게
                if (pick == last && pick != -1 && UnityEngine.Random.value < 0.6f)
                    pick = pick == 1 ? -1 : 1;

                switch (pick)
                {
                    case 0: yield return Swipe(); break;
                    case 1:
                        yield return Charge();
                        if (Phase2 && !_hp.IsDead) yield return Charge();   // 분노하면 연속 돌진
                        break;
                    case 2: yield return Slam(); break;
                    default: yield return Approach(1.4f); break;
                }
                if (pick != -1) last = pick;

                if (_hp.IsDead) yield break;
                Play("Idle", 1f);
                yield return new WaitForSeconds(_cfg.bossRecover / Tempo);
            }
        }

        /// <summary>플레이어 쪽으로 걸어간다. 휘두르기 거리에 들어오면 멈춘다.</summary>
        IEnumerator Approach(float maxTime)
        {
            Play("Walk", 1.2f);
            float t = 0f;
            while (t < maxTime && !_hp.IsDead && FlatDistance() > _cfg.bossSwipeRange * 0.75f)
            {
                var to = ToPlayer();
                Move(to.normalized * _cfg.bossMoveSpeed * Tempo * Time.deltaTime);
                Face(to);
                t += Time.deltaTime;
                yield return null;
            }
        }

        /// <summary>앞쪽 직사각형. 가까이 붙은 플레이어를 벌한다.</summary>
        IEnumerator Swipe()
        {
            FacePlayer(true);
            Vector3 fwd = _face;
            float windup = _cfg.bossSwipeWindup / Tempo;
            var tg = Track(Telegraph.Rect(transform.position, fwd, _cfg.bossSwipeRange, _cfg.bossSwipeWidth, windup));
            // 휘두르는 모션의 타격 지점(약 40%)이 예고 끝에 오도록 속도를 맞춘다
            Play("Attack", 0.65f / windup);
            yield return new WaitForSeconds(windup);
            Untrack(tg);
            if (_hp.IsDead) yield break;

            if (InRect(transform.position, fwd, _cfg.bossSwipeRange, _cfg.bossSwipeWidth))
                HitPlayer(_cfg.bossSwipeDamage, 1.0f);
            Shake(fwd, 0.8f);
            yield return new WaitForSeconds(0.35f);
        }

        /// <summary>
        /// 같은 줄로 옮겨 선 뒤 가로로 끝까지 달린다.
        /// 벨트스크롤의 핵심 회피(깊이 이동)를 요구하는 패턴이다.
        /// </summary>
        IEnumerator Charge()
        {
            // 1) 줄 맞추기 — 플레이어와 같은 깊이로 빠르게 옮겨 선다
            Play("Walk", 1.8f);
            float t = 0f;
            while (t < 0.9f && !_hp.IsDead)
            {
                float dz = _player.position.z - transform.position.z;
                if (Mathf.Abs(dz) < 0.15f) break;
                Move(new Vector3(0f, 0f, Mathf.Sign(dz) * Mathf.Min(Mathf.Abs(dz), 8f * Time.deltaTime)));
                t += Time.deltaTime;
                yield return null;
            }
            if (_hp.IsDead) yield break;

            // 2) 예고 — 가로 줄 전체가 붉게 칠해진다
            float dir = Mathf.Sign(_player.position.x - transform.position.x);
            if (dir == 0f) dir = 1f;
            Vector3 fwd = new Vector3(dir, 0f, 0f);
            Face(fwd);
            float edgeX = _center.x + dir * (_half.x - 0.6f);
            float length = Mathf.Max(1f, Mathf.Abs(edgeX - transform.position.x));
            float windup = _cfg.bossChargeWindup / Tempo;
            var tg = Track(Telegraph.Rect(transform.position, fwd, length, _cfg.bossChargeWidth, windup));
            Play("Idle", 2.5f);   // 몸을 낮추고 숨을 고르는 느낌 — 빠르게 들썩인다
            yield return new WaitForSeconds(windup);
            Untrack(tg);
            if (_hp.IsDead) yield break;

            // 3) 돌진
            Play("Run", 1.8f);
            bool hit = false;
            float speed = _cfg.bossChargeSpeed * Tempo;
            while (!_hp.IsDead && (edgeX - transform.position.x) * dir > 0.05f)
            {
                float step = Mathf.Min(speed * Time.deltaTime, Mathf.Abs(edgeX - transform.position.x));
                Move(fwd * step);
                if (!hit)
                {
                    var p = _player.position;
                    if (Mathf.Abs(p.z - transform.position.z) <= _cfg.bossChargeWidth * 0.5f &&
                        Mathf.Abs(p.x - transform.position.x) <= 1.3f)
                    {
                        HitPlayer(_cfg.bossChargeDamage, 1.4f);
                        hit = true;   // 한 번만 맞는다
                    }
                }
                yield return null;
            }
            if (_hp.IsDead) yield break;

            // 4) 벽에 박는다 → 그로기. 피한 보상으로 딜 타임을 준다
            Shake(fwd, 1.6f);
            if (Proto.Feel.Feel.I != null)
            {
                Proto.Feel.Feel.I.HitStop(1.2f);
                Proto.Feel.Feel.I.Popup(transform.position + Vector3.up * (_cc.height * transform.localScale.y + 0.4f), "그로기!");
            }
            _groggyUntil = Time.time + _cfg.bossGroggyTime;
            Groggy?.Invoke();
            Play("Idle", 0.35f);
            yield return new WaitForSeconds(_cfg.bossGroggyTime);
        }

        /// <summary>가슴을 두드리고 주변을 내려찍는다.</summary>
        IEnumerator Slam()
        {
            FacePlayer(true);
            float windup = _cfg.bossSlamWindup / Tempo;
            var tg = Track(Telegraph.Circle(transform.position, _cfg.bossSlamRadius, windup));
            Play("ChestHit", 1.3f);
            yield return new WaitForSeconds(windup);
            Untrack(tg);
            if (_hp.IsDead) yield break;

            Play("Attack", 2.2f);
            if (FlatDistance() <= _cfg.bossSlamRadius) HitPlayer(_cfg.bossSlamDamage, 1.2f);
            Shake(Vector3.up, 1.8f);
            if (Proto.Feel.Feel.I != null)
            {
                Proto.Feel.Feel.I.HitStop(1.3f);
                // 땅이 터지는 느낌 — 사방으로 흙먼지
                for (int i = 0; i < 6; i++)
                {
                    var dir = Quaternion.Euler(0f, i * 60f, 0f) * Vector3.forward;
                    Proto.Feel.Feel.I.Spark(transform.position + dir * _cfg.bossSlamRadius * 0.6f + Vector3.up * 0.2f, dir, true);
                }
            }
            yield return new WaitForSeconds(0.5f);
        }

        IEnumerator Enrage()
        {
            Phase2 = true;
            Enraged?.Invoke();
            ClearTelegraphs();
            Play("ChestHit", 1.2f);
            Shake(Vector3.up, 1.4f);
            Tint(new Color(1f, 0.62f, 0.58f));
            if (Proto.Feel.Feel.I != null)
                Proto.Feel.Feel.I.Popup(transform.position + Vector3.up * (_cc.height * transform.localScale.y + 0.4f), "분노!");
            yield return new WaitForSeconds(1.4f);
        }

        // ───────────────────────────── 피격 · 사망 ─────────────────────────────

        /// <summary>그로기 동안은 더 아프게 맞는다.</summary>
        public float ModifyIncomingDamage(float raw) => IsGroggy ? raw * _cfg.bossGroggyDamageMul : raw;

        void OnDamaged(float amount, Vector3 src)
        {
            if (Proto.Feel.Feel.I == null) return;
            Proto.Feel.Feel.I.Flash(transform);
            // 슈퍼아머라 밀리지는 않는다. 그로기일 때만 크게 흔들린다.
            Proto.Feel.Feel.I.Punch(_model, IsGroggy ? 0.8f : 0.25f);
        }

        void OnDied()
        {
            StopAllCoroutines();
            ClearTelegraphs();
            _groggyUntil = 0f;
            Play("Death", 1f);
        }

        // ───────────────────────────── 도구 ─────────────────────────────

        void HitPlayer(float dmg, float shake)
        {
            if (_playerHp == null || _playerHp.IsDead) return;
            _playerHp.TakeDamage(dmg, transform.position);
            if (Proto.Feel.Feel.I != null) Proto.Feel.Feel.I.HitStop(0.8f);
        }

        void Shake(Vector3 dir, float scale)
        {
            if (Proto.Feel.Feel.I != null) Proto.Feel.Feel.I.Shake(dir, scale);
        }

        bool InRect(Vector3 origin, Vector3 fwd, float length, float width)
        {
            var local = Quaternion.Inverse(Quaternion.LookRotation(fwd)) * (_player.position - origin);
            return local.z >= -0.5f && local.z <= length && Mathf.Abs(local.x) <= width * 0.5f;
        }

        Vector3 ToPlayer()
        {
            var to = _player.position - transform.position;
            to.y = 0f;
            return to;
        }

        float FlatDistance() => ToPlayer().magnitude;

        void FacePlayer(bool snap)
        {
            var to = ToPlayer();
            if (to.sqrMagnitude < 0.001f) return;
            Face(to);
            if (snap && _model != null) _model.rotation = Quaternion.LookRotation(_face);
        }

        void Face(Vector3 dir)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f) _face = dir.normalized;
        }

        void Update()
        {
            if (_model == null || _hp.IsDead) return;
            _model.rotation = Quaternion.Slerp(_model.rotation, Quaternion.LookRotation(_face), 12f * Time.deltaTime);
        }

        /// <summary>방 밖으로 나가지 않게 가둔다.</summary>
        void Move(Vector3 delta)
        {
            _cc.Move(delta);
            var p = transform.position;
            p.x = Mathf.Clamp(p.x, _center.x - _half.x + 0.5f, _center.x + _half.x - 0.5f);
            p.z = Mathf.Clamp(p.z, _center.z - _half.y + 0.5f, _center.z + _half.y - 0.5f);
            if ((p - transform.position).sqrMagnitude > 0.0001f)
            {
                _cc.enabled = false;
                transform.position = p;
                _cc.enabled = true;
            }
        }

        /// <summary>
        /// 팩 컨트롤러는 bool 파라미터로 전이를 건다. 상태를 직접 재생하되
        /// 파라미터도 맞춰 둬야 다음 프레임에 엉뚱한 전이가 끼어들지 않는다.
        /// </summary>
        void Play(string state, float speed)
        {
            if (_anim == null) return;
            SetBool("isWalking", state == "Walk");
            SetBool("isRunning", state == "Run");
            SetBool("isAttacking", state == "Attack");
            SetBool("isChestHit", state == "ChestHit");
            SetBool("isDead", state == "Death");
            _anim.speed = speed;
            if (_anim.HasState(0, Animator.StringToHash(state)))
                _anim.CrossFadeInFixedTime(state, 0.1f, 0);
        }

        void SetBool(string name, bool v)
        {
            foreach (var p in _anim.parameters)
                if (p.name == name) { _anim.SetBool(name, v); return; }
        }

        void Tint(Color c)
        {
            var block = new MaterialPropertyBlock();
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                r.GetPropertyBlock(block);
                block.SetColor("_BaseColor", c);
                r.SetPropertyBlock(block);
            }
        }

        Telegraph Track(Telegraph t) { _telegraphs.Add(t); return t; }
        void Untrack(Telegraph t) { _telegraphs.Remove(t); }

        void ClearTelegraphs()
        {
            foreach (var t in _telegraphs) if (t != null) t.Cancel();
            _telegraphs.Clear();
        }
    }
}
