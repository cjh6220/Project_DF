using System.Collections.Generic;
using UnityEngine;

namespace Proto.Enemy
{
    /// <summary>
    /// 몬스터 외곽선. 색은 던전 테마(StageTheme.monsterOutline)에서 정한다 — 알파 0이면 끈다.
    ///   일반 1배 · 엘리트 1.6배 · 보스 1.3배 굵기
    /// 머티리얼은 색 · 굵기별로 하나만 만들어 같이 쓴다.
    /// 셰이더: Resources/Fx/MonsterOutline.mat (Proto/MonsterOutline) — 빌드에서 빠지지 않게 머티리얼로 묶어 둔다.
    /// </summary>
    public static class MonsterOutline
    {
        const float BaseWidth = 0.0024f;
        static Material _base;
        static readonly Dictionary<(Color, float), Material> _cache = new();

        public static void Apply(Transform root, Color color, float widthMul = 1f)
        {
            if (root == null || color.a <= 0.01f) return;
            if (_base == null) _base = Resources.Load<Material>("Fx/MonsterOutline");
            if (_base == null) return;

            var key = (color, widthMul);
            if (!_cache.TryGetValue(key, out var mat) || mat == null)
            {
                mat = new Material(_base) { name = "MonsterOutline (inst)" };
                mat.SetColor("_OutlineColor", new Color(color.r, color.g, color.b, 1f));
                mat.SetFloat("_OutlineWidth", BaseWidth * widthMul);
                _cache[key] = mat;
            }

            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!(r is MeshRenderer || r is SkinnedMeshRenderer)) continue;
                SmoothNormals(r);
                var mats = r.sharedMaterials;
                bool has = false;
                foreach (var m in mats) if (m != null && m.shader == _base.shader) has = true;
                if (has) continue;
                var list = new List<Material>(mats) { mat };
                r.sharedMaterials = list.ToArray();
            }
        }

        // ── 외곽선용 부드러운 노멀 ──
        static readonly Dictionary<Mesh, Mesh> _smooth = new();

        /// <summary>
        /// 같은 자리 정점들의 노멀을 평균 내서 접선 칸에 넣은 메시로 바꾼다 (메시마다 한 번, 복사본을 같이 쓴다).
        /// 메시 Read/Write가 꺼져 있으면 건너뛴다 — 그때는 몸 안쪽에 선이 조금 보일 수 있다.
        /// </summary>
        static void SmoothNormals(Renderer r)
        {
            Mesh src = r is SkinnedMeshRenderer s ? s.sharedMesh : r.GetComponent<MeshFilter>()?.sharedMesh;
            if (src == null) return;
            if (!_smooth.TryGetValue(src, out var m))
            {
                m = null;
                if (src.isReadable)
                {
                    // 이미 런타임 복사본(촉수 흔들기 등)이면 그대로 고치고, 에셋이면 복사해서 고친다
                    m = src.name.EndsWith("(Clone)") ? src : Object.Instantiate(src);
                    var v = m.vertices; var n = m.normals;
                    if (n == null || n.Length != v.Length) m = null;
                    else
                    {
                        var sum = new Dictionary<Vector3Int, Vector3>();
                        var keys = new Vector3Int[v.Length];
                        for (int i = 0; i < v.Length; i++)
                        {
                            var k = new Vector3Int(Mathf.RoundToInt(v[i].x * 1e4f), Mathf.RoundToInt(v[i].y * 1e4f), Mathf.RoundToInt(v[i].z * 1e4f));
                            keys[i] = k;
                            sum[k] = sum.TryGetValue(k, out var acc) ? acc + n[i] : n[i];
                        }
                        var tan = new Vector4[v.Length];
                        for (int i = 0; i < v.Length; i++) { var a = sum[keys[i]].normalized; tan[i] = new Vector4(a.x, a.y, a.z, 2f); }
                        m.tangents = tan;
                    }
                }
                _smooth[src] = m;
            }
            if (m == null || m == src) return;
            if (r is SkinnedMeshRenderer sk) sk.sharedMesh = m;
            else { var mf = r.GetComponent<MeshFilter>(); if (mf != null) mf.sharedMesh = m; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { _cache.Clear(); _smooth.Clear(); _base = null; }
    }
}
