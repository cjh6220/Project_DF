using System.Collections;
using UnityEngine;
using Proto.Data;

namespace Proto.Enemy
{
    /// <summary>몬스터 스킬 — 보스와 엘리트가 같이 쓴다. 어떤 몬스터가 무엇을 쓸지는 StageTheme에서 고른다.</summary>
    public enum MonsterSkillId { None, Swipe, Charge, Slam, Leap, Quake, Barrage, Combo }

    /// <summary>스킬을 쓰는 몸. 보스(BossController)와 엘리트(EnemyController)가 각자 채운다.</summary>
    public interface IMonsterBody
    {
        Transform Root { get; }
        Transform Player { get; }
        TuningConfig Cfg { get; }
        /// <summary>빠르기 배율 — 분노한 보스는 예고가 짧아진다.</summary>
        float Tempo { get; }
        /// <summary>범위 배율 — 보스 1, 엘리트는 덩치만큼 작게.</summary>
        float Reach { get; }
        /// <summary>피해 배율 — 보스 피해 기준.</summary>
        float Power { get; }
        /// <summary>죽었거나 맞아서 끊겼다 — 스킬이 즉시 접는다.</summary>
        bool Stopped { get; }
        Vector3 ArenaCenter { get; }
        Vector2 ArenaHalf { get; }
        float HeadHeight { get; }
        void Face(Vector3 dir);
        void Move(Vector3 delta);
        /// <summary>Idle · Walk · Run · Attack · ChestHit(포효)</summary>
        void Anim(string state, float speed);
        Telegraph Track(Telegraph t);
        void Untrack(Telegraph t);
        void HitPlayer(float damage, float shake);
        /// <summary>벽에 박았다 — 보스는 그로기(딜 타임), 엘리트는 잠깐 멈춘다.</summary>
        void Stagger(float seconds);
    }

    /// <summary>
    /// 스킬 7종. 전부 바닥 예고(Telegraph)가 먼저 깔리고, 피하는 방법이 다르다.
    ///
    ///   휘두르기  가까이 붙으면 앞쪽 직사각형                → 옆(깊이)으로 비키거나 뒤로
    ///   돌진      같은 줄로 옮겨 선 뒤 가로 끝까지 달린다     → 줄을 바꾼다. 벽에 박으면 그로기
    ///   내려찍기  주변 원                                   → 원 밖으로
    ///   도약      플레이어 자리에 원을 깔고 뛰어내린다         → 원 밖으로 (멀리 있으면 쓴다)
    ///   대지 파동 앞으로 긴 줄                               → 줄을 바꾼다 (같은 줄에 있을 때)
    ///   낙석      플레이어 둘레에 원 세 개가 차례로           → 계속 움직인다
    ///   연속 베기 짧은 직사각형 세 번, 한 걸음씩 다가온다      → 뒤로 빠지거나 옆으로
    ///
    /// 피해는 보스 수치(Tuning의 bossSwipeDamage 등)에 Power를 곱한다.
    /// </summary>
    public static class MonsterSkills
    {
        /// <summary>재사용 (초). 엘리트는 평타도 쓰므로 몸 쪽에서 더 길게 곱한다.</summary>
        public static float Cooldown(MonsterSkillId id) => id switch
        {
            MonsterSkillId.Swipe => 1.5f,   // 붙어 있으면 이것만 쓰게 되므로 짧게라도 쉰다
            MonsterSkillId.Charge => 4f,
            MonsterSkillId.Slam => 3f,
            MonsterSkillId.Leap => 5f,
            MonsterSkillId.Quake => 4f,
            MonsterSkillId.Barrage => 7f,
            MonsterSkillId.Combo => 2.5f,
            _ => 99f
        };

        /// <summary>지금 거리에서 쓸 만한가. 고를 때 가중치도 돌려준다 (0이면 못 씀).</summary>
        public static float Weight(MonsterSkillId id, IMonsterBody b)
        {
            var to = b.Player.position - b.Root.position; to.y = 0f;
            float d = to.magnitude, dz = Mathf.Abs(to.z), r = b.Reach;
            var c = b.Cfg;
            switch (id)
            {
                case MonsterSkillId.Swipe: return d <= c.bossSwipeRange * 0.85f * r ? 1.2f : 0f;
                case MonsterSkillId.Slam: return d <= c.bossSlamRadius * 0.9f * r ? 1f : 0f;
                case MonsterSkillId.Combo: return d <= 3.2f * r ? 1.1f : 0f;
                case MonsterSkillId.Charge: return d >= 2.5f ? (d > 6f ? 1.4f : 0.6f) : 0f;
                case MonsterSkillId.Leap: return d >= 3f && d <= 10f ? 1f : 0f;
                case MonsterSkillId.Quake: return dz <= 1.4f && d >= 2f ? 1f : 0f;
                case MonsterSkillId.Barrage: return d >= 2f ? 0.7f : 0f;
            }
            return 0f;
        }

        public static IEnumerator Run(MonsterSkillId id, IMonsterBody b)
        {
            switch (id)
            {
                case MonsterSkillId.Swipe: return Swipe(b);
                case MonsterSkillId.Charge: return Charge(b);
                case MonsterSkillId.Slam: return Slam(b);
                case MonsterSkillId.Leap: return Leap(b);
                case MonsterSkillId.Quake: return Quake(b);
                case MonsterSkillId.Barrage: return Barrage(b);
                case MonsterSkillId.Combo: return Combo(b);
            }
            return Nothing();
        }

        static IEnumerator Nothing() { yield break; }

        // ───────────────────────────── 스킬 ─────────────────────────────

        static IEnumerator Swipe(IMonsterBody b)
        {
            var c = b.Cfg;
            var fwd = FacePlayer(b);
            float len = c.bossSwipeRange * b.Reach, wid = c.bossSwipeWidth * b.Reach;
            float windup = c.bossSwipeWindup / b.Tempo;
            var tg = b.Track(Telegraph.Rect(b.Root.position, fwd, len, wid, windup));
            b.Anim("Attack", 0.65f / windup);   // 휘두르는 순간(약 40%)이 예고 끝에 오게
            yield return Wait(b, windup);
            b.Untrack(tg);
            if (b.Stopped) yield break;
            if (InRect(b, b.Root.position, fwd, len, wid)) b.HitPlayer(c.bossSwipeDamage * b.Power, 1f);
            Shake(b, fwd, 0.8f);
            yield return Wait(b, 0.35f);
        }

        static IEnumerator Charge(IMonsterBody b)
        {
            var c = b.Cfg;
            // 1) 줄 맞추기
            b.Anim("Walk", 1.8f);
            float t = 0f;
            while (t < 0.9f && !b.Stopped)
            {
                float dz = b.Player.position.z - b.Root.position.z;
                if (Mathf.Abs(dz) < 0.15f) break;
                b.Move(new Vector3(0f, 0f, Mathf.Sign(dz) * Mathf.Min(Mathf.Abs(dz), 8f * Time.deltaTime)));
                t += Time.deltaTime;
                yield return null;
            }
            if (b.Stopped) yield break;

            // 2) 예고 — 가로 줄
            float dir = Mathf.Sign(b.Player.position.x - b.Root.position.x);
            if (dir == 0f) dir = 1f;
            var fwd = new Vector3(dir, 0f, 0f);
            b.Face(fwd);
            float edgeX = b.ArenaCenter.x + dir * (b.ArenaHalf.x - 0.6f);
            float length = Mathf.Max(1f, Mathf.Abs(edgeX - b.Root.position.x));
            float wid = c.bossChargeWidth * Mathf.Max(0.7f, b.Reach);
            float windup = c.bossChargeWindup / b.Tempo;
            var tg = b.Track(Telegraph.Rect(b.Root.position, fwd, length, wid, windup));
            b.Anim("Idle", 2.5f);
            yield return Wait(b, windup);
            b.Untrack(tg);
            if (b.Stopped) yield break;

            // 3) 돌진
            b.Anim("Run", 1.8f);
            bool hit = false;
            float speed = c.bossChargeSpeed * b.Tempo;
            while (!b.Stopped && (edgeX - b.Root.position.x) * dir > 0.05f)
            {
                float step = Mathf.Min(speed * Time.deltaTime, Mathf.Abs(edgeX - b.Root.position.x));
                b.Move(fwd * step);
                if (!hit)
                {
                    var p = b.Player.position;
                    if (Mathf.Abs(p.z - b.Root.position.z) <= wid * 0.5f && Mathf.Abs(p.x - b.Root.position.x) <= 1.3f)
                    {
                        b.HitPlayer(c.bossChargeDamage * b.Power, 1.4f);
                        hit = true;
                    }
                }
                yield return null;
            }
            if (b.Stopped) yield break;

            // 4) 벽에 박는다
            Shake(b, fwd, 1.6f);
            b.Stagger(c.bossGroggyTime);
            yield return Wait(b, c.bossGroggyTime);   // 그로기 동안은 다음 행동을 하지 않는다
        }

        static IEnumerator Slam(IMonsterBody b)
        {
            var c = b.Cfg;
            FacePlayer(b);
            float rad = c.bossSlamRadius * b.Reach;
            float windup = c.bossSlamWindup / b.Tempo;
            var tg = b.Track(Telegraph.Circle(b.Root.position, rad, windup));
            b.Anim("ChestHit", 1.3f);
            yield return Wait(b, windup);
            b.Untrack(tg);
            if (b.Stopped) yield break;
            b.Anim("Attack", 2.2f);
            if (Flat(b.Player.position - b.Root.position) <= rad) b.HitPlayer(c.bossSlamDamage * b.Power, 1.2f);
            Burst(b.Root.position, rad, 6);
            Shake(b, Vector3.up, 1.8f);
            yield return Wait(b, 0.5f);
        }

        static IEnumerator Leap(IMonsterBody b)
        {
            var c = b.Cfg;
            FacePlayer(b);
            var target = ClampToArena(b, b.Player.position);
            float rad = 2.4f * b.Reach;
            float windup = 0.9f / b.Tempo;
            var tg = b.Track(Telegraph.Circle(target, rad, windup + 0.45f));
            b.Anim("ChestHit", 1.6f);
            yield return Wait(b, windup);
            if (b.Stopped) { b.Untrack(tg); yield break; }

            // 포물선으로 뛰어 내린다 — 0.45초
            b.Anim("Run", 1.4f);
            var from = b.Root.position;
            float dur = 0.45f, t = 0f, height = 2.2f * Mathf.Max(0.6f, b.Reach), prevY = 0f;
            while (t < dur && !b.Stopped)
            {
                t = Mathf.Min(dur, t + Time.deltaTime);
                float k = t / dur;
                float y = 4f * height * k * (1f - k);
                var flat = Vector3.Lerp(from, target, k);
                var step = flat - b.Root.position; step.y = y - prevY;
                prevY = y;
                b.Face(target - from);
                b.Move(step);
                yield return null;
            }
            b.Untrack(tg);
            if (b.Stopped) yield break;
            b.Anim("Attack", 2.4f);
            if (Flat(b.Player.position - target) <= rad) b.HitPlayer(c.bossSlamDamage * 0.9f * b.Power, 1.3f);
            Burst(target, rad, 6);
            Shake(b, Vector3.up, 1.6f);
            yield return Wait(b, 0.55f);
        }

        static IEnumerator Quake(IMonsterBody b)
        {
            var c = b.Cfg;
            float dir = Mathf.Sign(b.Player.position.x - b.Root.position.x);
            if (dir == 0f) dir = 1f;
            var fwd = new Vector3(dir, 0f, 0f);
            b.Face(fwd);
            float len = Mathf.Min(10f * b.Reach + 2f, Mathf.Abs(b.ArenaCenter.x + dir * b.ArenaHalf.x - b.Root.position.x));
            float wid = 1.6f * Mathf.Max(0.7f, b.Reach);
            float windup = 1.0f / b.Tempo;
            var tg = b.Track(Telegraph.Rect(b.Root.position, fwd, len, wid, windup));
            b.Anim("ChestHit", 1.2f);
            yield return Wait(b, windup * 0.7f);
            b.Anim("Attack", 1.8f);
            yield return Wait(b, windup * 0.3f);
            b.Untrack(tg);
            if (b.Stopped) yield break;
            if (InRect(b, b.Root.position, fwd, len, wid)) b.HitPlayer(c.bossSwipeDamage * 1.1f * b.Power, 1.2f);
            // 줄을 따라 땅이 터진다
            for (float x = 1f; x < len; x += 1.4f)
                Proto.Feel.HitFx.Impact(b.Root.position + fwd * x + Vector3.up * 0.15f, x < 3f);
            Shake(b, fwd, 1.4f);
            yield return Wait(b, 0.45f);
        }

        static IEnumerator Barrage(IMonsterBody b)
        {
            var c = b.Cfg;
            FacePlayer(b);
            b.Anim("ChestHit", 1.4f);
            float rad = 1.5f * Mathf.Max(0.7f, b.Reach);
            float windup = 1.0f / b.Tempo;
            var spots = new Vector3[3];
            var tgs = new Telegraph[3];
            for (int i = 0; i < 3; i++)
            {
                // 첫 번째는 지금 서 있는 자리, 나머지는 피할 만한 곳 근처
                var off = i == 0 ? Vector3.zero : new Vector3(Random.Range(-2.2f, 2.2f), 0f, Random.Range(-1.4f, 1.4f));
                spots[i] = ClampToArena(b, b.Player.position + off);
                tgs[i] = b.Track(Telegraph.Circle(spots[i], rad, windup));
                yield return Wait(b, 0.3f / b.Tempo);
                if (b.Stopped) { foreach (var t in tgs) if (t != null) b.Untrack(t); yield break; }
            }
            yield return Wait(b, windup - 0.6f / b.Tempo);
            for (int i = 0; i < 3; i++)
            {
                if (b.Stopped) break;
                b.Untrack(tgs[i]);
                if (Flat(b.Player.position - spots[i]) <= rad) b.HitPlayer(c.bossSwipeDamage * 0.8f * b.Power, 0.9f);
                Proto.Feel.HitFx.Impact(spots[i] + Vector3.up * 0.2f, true);
                Shake(b, Vector3.up, 0.7f);
                yield return Wait(b, 0.3f / b.Tempo);
            }
            yield return Wait(b, 0.3f);
        }

        static IEnumerator Combo(IMonsterBody b)
        {
            var c = b.Cfg;
            float len = 2.6f * b.Reach, wid = 2.4f * b.Reach;
            for (int i = 0; i < 3 && !b.Stopped; i++)
            {
                var fwd = FacePlayer(b);
                float windup = (i == 0 ? 0.6f : 0.42f) / b.Tempo;
                var tg = b.Track(Telegraph.Rect(b.Root.position, fwd, len, wid, windup));
                b.Anim("Attack", 0.65f / windup);
                yield return Wait(b, windup);
                b.Untrack(tg);
                if (b.Stopped) yield break;
                if (InRect(b, b.Root.position, fwd, len, wid)) b.HitPlayer(c.bossSwipeDamage * 0.6f * b.Power, 0.7f);
                Shake(b, fwd, 0.6f);
                // 한 걸음 다가선다
                float stepT = 0f;
                b.Anim("Walk", 1.6f);
                while (stepT < 0.15f && !b.Stopped) { b.Move(fwd * (0.9f * b.Reach / 0.15f) * Time.deltaTime); stepT += Time.deltaTime; yield return null; }
            }
            yield return Wait(b, 0.4f);
        }

        // ───────────────────────────── 도구 ─────────────────────────────

        static IEnumerator Wait(IMonsterBody b, float seconds)
        {
            float t = 0f;
            while (t < seconds && !b.Stopped) { t += Time.deltaTime; yield return null; }
        }

        static Vector3 FacePlayer(IMonsterBody b)
        {
            var to = b.Player.position - b.Root.position; to.y = 0f;
            var fwd = to.sqrMagnitude > 0.001f ? to.normalized : Vector3.right;
            b.Face(fwd);
            return fwd;
        }

        static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }

        static bool InRect(IMonsterBody b, Vector3 origin, Vector3 fwd, float length, float width)
        {
            var local = Quaternion.Inverse(Quaternion.LookRotation(fwd)) * (b.Player.position - origin);
            return local.z >= -0.5f && local.z <= length && Mathf.Abs(local.x) <= width * 0.5f;
        }

        static Vector3 ClampToArena(IMonsterBody b, Vector3 p)
        {
            p.x = Mathf.Clamp(p.x, b.ArenaCenter.x - b.ArenaHalf.x + 0.8f, b.ArenaCenter.x + b.ArenaHalf.x - 0.8f);
            p.z = Mathf.Clamp(p.z, b.ArenaCenter.z - b.ArenaHalf.y + 0.8f, b.ArenaCenter.z + b.ArenaHalf.y - 0.8f);
            p.y = b.Root.position.y;
            return p;
        }

        static void Burst(Vector3 at, float radius, int n)
        {
            var feel = Proto.Feel.Feel.I;
            if (feel == null) return;
            feel.HitStop(1.2f);
            for (int i = 0; i < n; i++)
            {
                var dir = Quaternion.Euler(0f, i * 360f / n, 0f) * Vector3.forward;
                feel.Spark(at + dir * radius * 0.6f + Vector3.up * 0.2f, dir, true);
            }
        }

        static void Shake(IMonsterBody b, Vector3 dir, float scale)
        {
            float s = scale * Mathf.Clamp(b.Reach, 0.5f, 1f);   // 엘리트는 덜 흔든다
            if (Proto.Feel.Feel.I != null) Proto.Feel.Feel.I.Shake(dir, s);
            Proto.Feel.Haptics.Pulse(0.22f * s, 0.32f * s, 0.12f + 0.06f * s);
        }
    }
}
