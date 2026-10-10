using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Proto.UI
{
    /// <summary>
    /// 마우스 없이 모든 창을 쓰게 하는 공용 규칙 — 키보드 · 패드 포커스.
    ///
    ///   확인 키 하나   UI 확인 = Enter · 숫자패드 Enter · Space · 패드 아래 버튼 (게임의 Interact와 같은 키)
    ///   포커스 유지    버튼 창(메뉴 · 포기 · 정산)은 Keep()으로 — 선택이 사라지거나 창 밖으로 나가면 기본 버튼을 다시 잡는다
    ///                 (빈 곳을 클릭해도 방향키가 계속 먹는다)
    ///   자체 조작 창   설정 · 스킬 · 성장 · 던전 선택처럼 스스로 방향키를 읽는 창은 Block()/Unblock()으로 —
    ///                 그동안 Unity 선택을 비워 둔다. 뒤 창의 버튼이 같은 Enter를 받아 '게임 종료'가 눌리는 일을 막는다.
    ///                 닫으면 열기 전 선택(예: 메뉴의 [설정])으로 돌아간다
    ///   닫힌 프레임    창을 닫은 바로 그 키로 마을 NPC가 다시 열리지 않게 ClosedRecently
    ///   포커스 표시    UiKit.Button에는 FocusMarker가 붙는다 — 고른 버튼에 금색 테 + 살짝 커짐
    /// </summary>
    public static class UiFocus
    {
        static readonly Dictionary<object, System.Func<bool>> _blockers = new();
        static GameObject _saved;
        static int _closedFrame = -10;

        public static bool Blocked => _blockers.Count > 0;

        /// <summary>창을 막 닫았다 — 이번 · 다음 프레임에는 같은 키로 다른 것을 열지 않는다.</summary>
        public static bool ClosedRecently => Time.frameCount - _closedFrame <= 1;
        public static void MarkClosed() => _closedFrame = Time.frameCount;

        static EventSystem Es => EventSystem.current;

        /// <param name="stillOpen">창이 다른 길로(SetActive 등) 닫혀도 막힘이 남지 않게 매 프레임 확인한다.</param>
        public static void Block(object owner, System.Func<bool> stillOpen = null)
        {
            if (owner == null || _blockers.ContainsKey(owner)) return;
            _blockers[owner] = stillOpen;
            if (_blockers.Count == 1 && Es != null)
            {
                _saved = Es.currentSelectedGameObject;
                Es.SetSelectedGameObject(null);
                Es.sendNavigationEvents = false;
            }
        }

        public static void Unblock(object owner)
        {
            if (owner == null || !_blockers.Remove(owner)) return;
            Release();
        }

        static void Release()
        {
            MarkClosed();
            if (_blockers.Count > 0 || Es == null) return;
            Es.sendNavigationEvents = true;
            if (_saved != null && _saved.activeInHierarchy) Es.SetSelectedGameObject(_saved);
            _saved = null;
        }

        /// <summary>버튼 창이 매 프레임 부른다 — 선택이 없거나 panel 밖이면 fallback을 다시 고른다.</summary>
        public static void Keep(Transform panel, Selectable fallback)
        {
            if (Blocked || Es == null || fallback == null || !fallback.gameObject.activeInHierarchy) return;
            var cur = Es.currentSelectedGameObject;
            if (cur != null && cur.activeInHierarchy && cur.transform.IsChildOf(panel)) return;
            Es.SetSelectedGameObject(fallback.gameObject);
        }

        public static void Select(GameObject go)
        {
            if (Es != null) Es.SetSelectedGameObject(go);
        }

        /// <summary>방향키로 이 버튼에 오지 못하게 (HUD의 [메뉴] · [포기] 같은 바깥 버튼).</summary>
        public static void NoNav(Selectable s)
        {
            if (s == null) return;
            var n = s.navigation; n.mode = Navigation.Mode.None; s.navigation = n;
        }

        // ───────────────────────────── 부팅 ─────────────────────────────

        static InputActionAsset _uiActions;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _blockers.Clear(); _saved = null; _closedFrame = -10; _uiActions = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var go = new GameObject("UiFocus");
            Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideInHierarchy;
            go.AddComponent<Runner>();
        }

        /// <summary>
        /// UI 확인 키에 Space · 숫자패드 Enter를 더한다 (기본 Submit은 Enter · 패드 아래 버튼뿐).
        /// 패키지의 기본 액션 파일은 건드리지 않고 복사본을 만들어 모듈에 끼운다.
        /// </summary>
        static void PatchSubmit()
        {
            var m = Es != null ? Es.GetComponent<InputSystemUIInputModule>() : null;
            if (m == null || m.actionsAsset == null || m.actionsAsset == _uiActions) return;
            var copy = Object.Instantiate(m.actionsAsset);
            copy.name = m.actionsAsset.name + " (UiFocus)";
            var submit = copy.FindAction("UI/Submit");
            if (submit != null)
            {
                submit.AddBinding("<Keyboard>/space");
                submit.AddBinding("<Keyboard>/numpadEnter");
            }
            _uiActions = copy;
            m.actionsAsset = copy;   // 모듈이 이름으로 액션을 다시 찾아 붙인다
        }

        class Runner : MonoBehaviour
        {
            void Update()
            {
                PatchSubmit();
                // 막은 창이 Unblock 없이 사라졌으면 풀어 준다
                if (_blockers.Count > 0)
                {
                    object dead = null;
                    foreach (var kv in _blockers)
                    {
                        bool gone = (kv.Key is Object uo && uo == null) || (kv.Value != null && !kv.Value());
                        if (gone) { dead = kv.Key; break; }
                    }
                    if (dead != null) Unblock(dead);
                }
                // 자체 조작 창이 열려 있는 동안 마우스로 버튼을 눌러도 선택이 남지 않게 — 남으면 Enter가 그 버튼까지 누른다
                if (Blocked && Es != null && Es.currentSelectedGameObject != null) Es.SetSelectedGameObject(null);
            }
        }
    }

    /// <summary>
    /// 키보드 · 패드로 고른 버튼 표시 — 금색 테가 숨 쉬고 버튼이 살짝 커진다.
    /// Unity 선택(EventSystem)으로 고른 버튼이면 저절로 켜지고,
    /// 스스로 방향키를 읽는 창(설정 등)은 Light(button, true)로 직접 켠다.
    /// </summary>
    public class FocusMarker : MonoBehaviour
    {
        Image _ring;
        float _k, _applied = -1f;
        public bool Lit;

        public static void Light(Component c, bool on)
        {
            if (c == null) return;
            var m = c.GetComponent<FocusMarker>();
            if (m == null) { if (!on) return; m = c.gameObject.AddComponent<FocusMarker>(); }
            m.Lit = on;
        }

        void Awake()
        {
            _ring = UiKit.Image(transform, "Focus", new Color(1f, 0.80f, 0.42f, 0f), UiArt.RoundRectOutline);
            _ring.type = Image.Type.Sliced;
            _ring.raycastTarget = false;
            var rt = _ring.rectTransform;
            UiKit.Stretch(rt);
            rt.offsetMin = new Vector2(-5f, -5f);
            rt.offsetMax = new Vector2(5f, 5f);
        }

        void Update()
        {
            var es = EventSystem.current;
            bool on = Lit || (es != null && es.currentSelectedGameObject == gameObject);
            _k = Mathf.MoveTowards(_k, on ? 1f : 0f, Time.unscaledDeltaTime * 8f);
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 5f);
            _ring.color = new Color(1f, 0.80f, 0.42f, _k * pulse);
            // 크기는 바뀔 때만 건드린다 — 다른 연출이 버튼 크기를 움직이는 걸 막지 않게
            if (!Mathf.Approximately(_k, _applied))
            {
                transform.localScale = Vector3.one * (1f + 0.04f * _k);
                _applied = _k;
            }
        }

        void OnDisable()
        {
            _k = 0f;
            if (_applied > 0f) transform.localScale = Vector3.one;
            _applied = 0f;
            if (_ring != null) _ring.color = Color.clear;
        }
    }
}
