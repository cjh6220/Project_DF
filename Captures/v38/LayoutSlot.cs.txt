using UnityEngine;

namespace Proto.Dungeon
{
    public enum SlotKind { Obstacle, Clutter, Enemy, Ore }

    /// <summary>
    /// 방 배치 프리팹 안의 자리 하나. 옮기고, 크기를 바꾸고, 지우고, 복사해서 늘리면 된다.
    ///
    ///   Obstacle  큰 장애물 (던전 테마의 obstacles 중 하나 — 숲이면 나무 · 바위, 폐광이면 바위). scale = 크기 배율
    ///   Clutter   풀 · 덤불 무리의 중심. radius 안에 count개를 몰아 심는다
    ///   Enemy     몬스터가 서는 자리 (방의 마릿수만큼, 입구에서 먼 자리부터 쓴다)
    ///   Ore       광맥 자리 (방의 광맥 수만큼 앞에서부터 쓴다)
    ///
    /// 씬 화면에서 색으로 보인다 — 장애물 갈색 · 풀 초록 · 몬스터 빨강 · 광맥 하늘색.
    /// </summary>
    public class LayoutSlot : MonoBehaviour
    {
        public SlotKind kind = SlotKind.Obstacle;
        [Tooltip("켜면 큰 장애물(바위 · 비석 — 테마의 bigObstacles), 끄면 낮은 잡동사니(통나무 · 납작돌 — 테마의 obstacles)")]
        public bool big = true;
        [Tooltip("장애물 크기 배율 (테마의 기본 크기에 곱한다)")]
        [Range(0.3f, 3f)] public float scale = 1f;
        [Tooltip("풀 무리 반경 (m)")]
        [Range(0.5f, 5f)] public float radius = 2f;
        [Tooltip("풀 무리 개수")]
        [Range(1, 30)] public int count = 8;

        void OnDrawGizmos()
        {
            var p = transform.position;
            switch (kind)
            {
                case SlotKind.Obstacle:
                    Gizmos.color = big ? new Color(0.65f, 0.42f, 0.22f, 0.85f) : new Color(0.75f, 0.65f, 0.45f, 0.7f);
                    float h = big ? 1.6f : 0.4f;
                    Gizmos.DrawCube(p + Vector3.up * h * 0.5f * scale, new Vector3(2.2f, h, 2.2f) * scale);
                    break;
                case SlotKind.Clutter:
                    Gizmos.color = new Color(0.35f, 0.8f, 0.3f, 0.6f);
                    Gizmos.DrawWireSphere(p, radius);
                    Gizmos.DrawSphere(p, 0.25f);
                    break;
                case SlotKind.Enemy:
                    Gizmos.color = new Color(1f, 0.25f, 0.2f, 0.9f);
                    Gizmos.DrawSphere(p + Vector3.up * 0.5f, 0.45f);
                    Gizmos.DrawLine(p, p + Vector3.up * 1.6f);
                    break;
                case SlotKind.Ore:
                    Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.9f);
                    Gizmos.DrawWireCube(p + Vector3.up * 0.4f, new Vector3(0.9f, 0.8f, 0.9f));
                    break;
            }
        }
    }
}
