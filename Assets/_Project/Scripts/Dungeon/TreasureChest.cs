using System;
using System.Collections;
using UnityEngine;
using TMPro;
using Proto.Core;

namespace Proto.Dungeon
{
    /// <summary>
    /// 보물방 상자. 수호자(중간보스)가 살아 있는 동안은 잠겨 있다.
    ///
    ///   잠김   붉은 빛 — 다가가면 "수호자를 쓰러뜨리세요"
    ///   풀림   금빛이 숨 쉰다 — 다가가면 [Space 열기]
    ///   열기   뚜껑이 젖혀지고 장비가 튀어나온다 (장비는 RunManager가 굴린다)
    ///
    /// 한 번 연 상자는 그 판 동안 열린 채로 남는다 (Room.ChestOpened).
    /// 모델은 Resources/Props/TreasureChest — 'chest-top' 자식이 뚜껑이다 (없으면 통째로 흔들기만).
    /// </summary>
    public class TreasureChest : MonoBehaviour
    {
        [SerializeField] float openRange = 2.3f;
        [SerializeField] float lidOpenAngle = -105f;

        public event Action Opened;

        Transform _player, _lid, _cam;
        Quaternion _lidClosed;
        Light _light;
        TextMeshPro _prompt;
        bool _locked = true, _opened, _near;
        float _promptT;

        static readonly Color ColLocked = new Color(1f, 0.25f, 0.2f);
        static readonly Color ColReady = new Color(1f, 0.78f, 0.35f);

        public bool IsOpen => _opened;

        public void Setup(Transform player, bool alreadyOpened, bool unlocked)
        {
            _player = player;
            FindParts();
            _opened = alreadyOpened;
            _locked = !unlocked && !alreadyOpened;
            if (_opened && _lid != null) _lid.localRotation = _lidClosed * Quaternion.Euler(lidOpenAngle, 0f, 0f);
            Paint();
        }

        public void Unlock()
        {
            if (!_locked) return;
            _locked = false;
            Paint();
            if (Proto.Feel.Feel.I != null) Proto.Feel.Feel.I.Popup(transform.position + Vector3.up * 1.6f, "상자가 열렸다!");
            StartCoroutine(Bounce(0.25f, 0.12f));
        }

        void FindParts()
        {
            if (_light != null) return;
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t.name.Contains("chest-top") || t.name.ToLowerInvariant().Contains("lid")) { _lid = t; break; }
            if (_lid != null) _lidClosed = _lid.localRotation;

            var lg = new GameObject("Glow");
            lg.transform.SetParent(transform, false);
            lg.transform.localPosition = new Vector3(0f, 0.9f, 0.4f);
            _light = lg.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.range = 3.2f;
            _light.shadows = LightShadows.None;

            var pg = new GameObject("Prompt");
            pg.transform.SetParent(transform, false);
            pg.transform.localPosition = new Vector3(0f, 1.75f, 0f);
            _prompt = pg.AddComponent<TextMeshPro>();
            _prompt.alignment = TextAlignmentOptions.Center;
            _prompt.fontSize = 3f;
            _prompt.textWrappingMode = TextWrappingModes.NoWrap;
            _prompt.rectTransform.sizeDelta = new Vector2(8f, 1.2f);
            _prompt.outlineWidth = 0.18f;
            _prompt.outlineColor = new Color32(0, 0, 0, 200);
            pg.SetActive(false);
        }

        void Paint()
        {
            if (_light == null) return;
            _light.color = _locked ? ColLocked : ColReady;
            _light.intensity = _opened ? 0.6f : (_locked ? 1.2f : 2.2f);
        }

        void Update()
        {
            if (_light != null && !_locked && !_opened)
                _light.intensity = 1.8f + 0.7f * Mathf.Sin(Time.time * 4f);   // 열 수 있다 — 숨 쉬는 금빛

            if (_player == null || _opened) { SetPrompt(false); return; }
            var d = _player.position - transform.position; d.y = 0f;
            bool near = d.magnitude <= openRange;
            SetPrompt(near);
            if (!near || _locked) return;

            bool uiBusy = Proto.UI.InventoryView.IsOpen || Proto.UI.UiFocus.ClosedRecently || Proto.UI.GiveUpMenu.IsOpen || Time.timeScale <= 0f;
            if (!uiBusy && GameInput.InteractPressed) Open();
        }

        void LateUpdate()
        {
            if (_prompt == null || !_prompt.gameObject.activeSelf) return;
            if (_cam == null && Camera.main != null) _cam = Camera.main.transform;
            if (_cam != null) _prompt.transform.rotation = _cam.rotation;
            if (_promptT < 1f)
            {
                _promptT = Mathf.Min(1f, _promptT + Time.deltaTime * 6f);
                _prompt.transform.localScale = Vector3.one * Mathf.Lerp(0.7f, 1f, 1f - Mathf.Pow(1f - _promptT, 3f));
            }
        }

        void SetPrompt(bool on)
        {
            if (_prompt == null) return;
            if (on)
            {
                _prompt.text = _locked
                    ? "<color=#ff8a7a>보물 수호자를 쓰러뜨리세요</color>"
                    : InputGlyphs.Of(Act.Interact) + "  <color=#ffd45a>열기</color>";
            }
            if (_near == on) return;
            _near = on;
            _prompt.gameObject.SetActive(on);
            _promptT = 0f;
        }

        void Open()
        {
            if (_opened || _locked) return;
            _opened = true;
            SetPrompt(false);
            StartCoroutine(OpenRoutine());
        }

        IEnumerator OpenRoutine()
        {
            // 잠깐 들썩이다가 뚜껑이 젖혀진다
            yield return Bounce(0.18f, 0.08f);
            float t = 0f;
            var from = _lid != null ? _lid.localRotation : Quaternion.identity;
            var to = _lidClosed * Quaternion.Euler(lidOpenAngle, 0f, 0f);
            while (t < 1f)
            {
                t = Mathf.Min(1f, t + Time.deltaTime / 0.35f);
                float k = 1f - Mathf.Pow(1f - t, 3f);
                if (_lid != null) _lid.localRotation = Quaternion.Slerp(from, to, k);
                if (_light != null) _light.intensity = Mathf.Lerp(2.5f, 5f, k);
                yield return null;
            }
            var feel = Proto.Feel.Feel.I;
            if (feel != null)
            {
                feel.Shake(Vector3.up, 0.8f);
                Proto.Feel.HitFx.Impact(transform.position + Vector3.up * 0.8f, true);
            }
            Proto.Feel.Haptics.Pulse(0.35f, 0.5f, 0.2f);
            Opened?.Invoke();

            // 빛이 잦아든다
            t = 0f;
            while (t < 1f)
            {
                t = Mathf.Min(1f, t + Time.deltaTime / 0.8f);
                if (_light != null) _light.intensity = Mathf.Lerp(5f, 0.6f, t);
                yield return null;
            }
        }

        IEnumerator Bounce(float seconds, float height)
        {
            var basePos = transform.localPosition;
            float t = 0f;
            while (t < 1f)
            {
                t = Mathf.Min(1f, t + Time.deltaTime / seconds);
                transform.localPosition = basePos + Vector3.up * (Mathf.Sin(t * Mathf.PI) * height);
                yield return null;
            }
            transform.localPosition = basePos;
        }
    }
}
