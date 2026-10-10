using System;
using UnityEngine;

namespace Proto.Dungeon
{
    /// <summary>방 출구. 몬스터가 남아 있으면 닫혀 있다.</summary>
    [RequireComponent(typeof(Collider))]
    public class ExitGate : MonoBehaviour
    {
        Dir _dir;
        Action<Dir> _onUse;
        bool _open;
        Renderer _renderer;

        static readonly Color Closed = new Color(0.55f, 0.16f, 0.16f);
        static readonly Color Open = new Color(0.30f, 0.90f, 0.55f);

        public void Setup(Dir dir, Action<Dir> onUse)
        {
            _dir = dir;
            _onUse = onUse;
            var col = GetComponent<Collider>();
            col.isTrigger = true;
            _renderer = GetComponentInChildren<Renderer>();
            SetOpen(false);
        }

        public void SetOpen(bool open)
        {
            _open = open;
            if (_renderer == null) return;
            var block = new MaterialPropertyBlock();
            _renderer.GetPropertyBlock(block);
            var c = open ? Open : Closed;
            block.SetColor("_BaseColor", c);
            block.SetColor("_EmissionColor", c * (open ? 1.8f : 0.4f));
            _renderer.SetPropertyBlock(block);
        }

        void OnTriggerEnter(Collider other)
        {
            if (!_open) return;
            if (!other.CompareTag("Player")) return;
            _onUse?.Invoke(_dir);
        }
    }
}
