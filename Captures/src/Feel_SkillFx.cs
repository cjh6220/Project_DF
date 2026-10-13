using UnityEngine;

namespace Proto.Feel
{
    /// <summary>
    /// 스킬 전용 이펙트 — 파티클 시스템 프리팹을 꺼내 재생한다. 모양 · 색 · 속도는 전부 프리팹에 있다
    /// (Resources/Fx/Skill/PS_*.prefab — 메뉴 Proto ▸ Build Skill FX가 만든다. 인스펙터에서 바로 고쳐도 된다).
    ///
    ///   찌르기   Thrust    PS_Thrust 를 칼끝에 놓고 찌르는 쪽을 보게 돌린다. 빛창 길이 = 찌르는 길이, Tip은 끝 근처로
    ///   회전     Whirl     PS_Whirl 을 플레이어에 붙였다가 스킬 시간이 끝나면 멈춘다. 반대로 돌면 X를 뒤집는다
    ///   검기     WaveTrail PS_Crescent 를 날아가는 검기에 붙인다. 검기가 사라지면 Release로 떼어 낸다
    /// 프리팹이 없으면 조용히 건너뛴다.
    /// </summary>
    public static class SkillFx
    {
        static GameObject _thrust, _whirl, _crescent;
        static bool _loaded;

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _thrust = Resources.Load<GameObject>("Fx/Skill/PS_Thrust");
            _whirl = Resources.Load<GameObject>("Fx/Skill/PS_Whirl");
            _crescent = Resources.Load<GameObject>("Fx/Skill/PS_Crescent");
        }

        /// <summary>찌르기 한 번 — from에서 dir 쪽으로 length만큼.</summary>
        public static void Thrust(Vector3 from, Vector3 dir, float length)
        {
            Load();
            if (_thrust == null) return;
            var go = Object.Instantiate(_thrust, from, Quaternion.LookRotation(dir.normalized, Vector3.up));
            var tip = go.transform.Find("Tip");
            if (tip != null) tip.localPosition = new Vector3(0f, 0f, length * 0.8f);
            foreach (var n in new[] { "Spear", "SpearGlow" })
            {
                var t = go.transform.Find(n);
                if (t == null) continue;
                var main = t.GetComponent<ParticleSystem>().main;
                main.startSizeZMultiplier = length * (n == "Spear" ? 1f : 1.04f);   // 창 메시는 길이 1
            }
            Object.Destroy(go, 0.8f);
        }

        /// <summary>회전 — follow에 붙어 duration 동안 돈다. radius는 판정 반경 (프리팹은 2.6m 기준).</summary>
        public static void Whirl(Transform follow, float duration, float radius, bool clockwise)
        {
            Load();
            if (_whirl == null || follow == null) return;
            var go = Object.Instantiate(_whirl, follow);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            float k = radius / 2.6f;
            go.transform.localScale = new Vector3(clockwise ? -k : k, k, k);   // 반대로 돌 땐 X를 뒤집는다 — 호가 쓸고 가는 쪽 · 회오리 흐름이 같이 뒤집힌다
            go.AddComponent<StopAfter>().Seconds = duration;
        }

        /// <summary>검기 꼬리 — follow(날아가는 검기)에 붙인다. 볼록한 쪽이 날아가는 쪽.</summary>
        public static GameObject WaveTrail(Transform follow, bool right)
        {
            Load();
            if (_crescent == null || follow == null) return null;
            var go = Object.Instantiate(_crescent, follow);
            go.transform.localPosition = Vector3.zero;
            go.transform.rotation = Quaternion.Euler(0f, right ? 0f : 180f, 0f);   // +X가 날아가는 쪽
            return go;
        }

        /// <summary>붙여 둔 이펙트를 떼어 내 — 새로 뿜지는 않고, 남은 잔상이 사라지면 지운다.</summary>
        public static void Release(GameObject fx, float linger = 0.6f)
        {
            if (fx == null) return;
            fx.transform.SetParent(null, true);
            foreach (var ps in fx.GetComponentsInChildren<ParticleSystem>()) ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            Object.Destroy(fx, linger);
        }

        /// <summary>정한 시간이 지나면 뿜기를 멈추고, 남은 입자가 사라진 뒤 지운다.</summary>
        class StopAfter : MonoBehaviour
        {
            public float Seconds;
            bool _stopped;

            void Update()
            {
                if (_stopped) return;
                Seconds -= Time.deltaTime;
                if (Seconds > 0f) return;
                _stopped = true;
                foreach (var ps in GetComponentsInChildren<ParticleSystem>()) ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
                transform.SetParent(null, true);   // 남은 흙먼지가 플레이어를 따라가지 않게
                Destroy(gameObject, 1f);
            }
        }
    }
}
