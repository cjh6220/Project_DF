using UnityEngine;

namespace Proto.Feel
{
    /// <summary>
    /// 색을 런타임에 입힌다.
    ///
    /// 에디터에서 Renderer.SetPropertyBlock으로 칠한 색은 직렬화되지 않아
    /// 플레이 모드에 들어가는 순간 사라진다. 그래서 색을 컴포넌트에 들고 있다가
    /// Awake에서 다시 칠한다.
    ///
    /// 프로토타입에서는 머티리얼 에셋을 만들지 않고 이 방식으로 때운다.
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class Tint : MonoBehaviour
    {
        [SerializeField] Color color = Color.white;
        [SerializeField] float emission = 0f;

        Renderer _r;

        public void Set(Color c, float emissionScale = 0f)
        {
            color = c;
            emission = emissionScale;
            Apply();
        }

        void Awake() => Apply();
        void OnValidate() => Apply();

        void Apply()
        {
            if (_r == null) _r = GetComponent<Renderer>();
            if (_r == null) return;

            var block = new MaterialPropertyBlock();
            _r.GetPropertyBlock(block);
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            if (emission > 0f) block.SetColor("_EmissionColor", color * emission);
            _r.SetPropertyBlock(block);
        }
    }
}
