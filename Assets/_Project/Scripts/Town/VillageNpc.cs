using UnityEngine;
using TMPro;
using Proto.Core;

namespace Proto.Town
{
    public enum NpcRole { Growth, Skill, Waystone }

    /// <summary>
    /// 마을 NPC 하나. 머리 위 이름표, 가까이 가면 뜨는 [Space 대화], 할 일이 있으면 "!".
    /// 가까이 오면 플레이어 쪽으로 몸을 돌린다 — 말을 걸 수 있다는 걸 몸으로 알려 준다.
    /// </summary>
    public class VillageNpc : MonoBehaviour
    {
        [SerializeField] NpcRole role;
        [SerializeField] Transform model;
        [SerializeField] GameObject prompt;
        [SerializeField] GameObject badge;
        [Tooltip("평소에 바라보는 Y 회전")]
        [SerializeField] float restYaw = 180f;
        [SerializeField] float turnSpeed = 6f;
        [Tooltip("말 걸기 안내 문구 — [Space  대화]의 '대화' 자리")]
        [SerializeField] string promptVerb = "대화";

        public NpcRole Role => role;

        Transform _look;
        Vector3 _badgeBase, _promptBase;
        float _promptT;

        TMP_Text _promptText;
        Animator _anim;
        static readonly int NearHash = Animator.StringToHash("Near");

        void OnEnable() => GameInput.SchemeChanged += RefreshPrompt;
        void OnDisable() => GameInput.SchemeChanged -= RefreshPrompt;

        /// <summary>[Space 대화] / [× 대화] — 지금 쓰는 장치의 버튼으로.</summary>
        void RefreshPrompt()
        {
            if (_promptText != null) Proto.UI.InputFx.Set(_promptText, InputGlyphs.Of(Act.Interact) + "  " + promptVerb);
        }

        void Awake()
        {
            if (prompt != null) _promptText = prompt.GetComponent<TMP_Text>();
            if (model != null) _anim = model.GetComponent<Animator>();
            RefreshPrompt();
            if (badge != null) { _badgeBase = badge.transform.localPosition; badge.SetActive(false); }
            if (prompt != null) { _promptBase = prompt.transform.localPosition; prompt.SetActive(false); }
        }

        public void SetNear(bool near, Transform player)
        {
            _look = near ? player : null;
            // 가까이 오면 몸짓이 바뀐다 — 장로는 말을 걸고, 검객은 칼을 고쳐 쥔다
            if (_anim != null && _anim.runtimeAnimatorController != null) _anim.SetBool(NearHash, near);
            if (prompt != null)
            {
                if (_promptText != null) _promptText.text = InputGlyphs.Of(Act.Interact) + "  " + promptVerb;
                prompt.SetActive(near);
                _promptT = 0f;
            }
        }

        public void SetBadge(bool on)
        {
            if (badge != null && badge.activeSelf != on) badge.SetActive(on);
        }

        void Update()
        {
            if (model != null)
            {
                float yaw = restYaw;
                if (_look != null)
                {
                    var d = _look.position - transform.position; d.y = 0f;
                    if (d.sqrMagnitude > 0.01f) yaw = Quaternion.LookRotation(d).eulerAngles.y;
                }
                model.localRotation = Quaternion.Slerp(model.localRotation, Quaternion.Euler(0f, yaw, 0f), turnSpeed * Time.deltaTime);
            }

            if (badge != null && badge.activeSelf)
                badge.transform.localPosition = _badgeBase + Vector3.up * (Mathf.Sin(Time.time * 3.2f) * 0.08f);

            // 뜰 때만 크기를 만진다 — 그 뒤에는 버튼 표기 뒤집기 연출이 크기를 쓴다
            if (prompt != null && prompt.activeSelf && _promptT < 1f)
            {
                // 뜰 때 살짝 튀어 오른다
                _promptT = Mathf.Min(1f, _promptT + Time.deltaTime * 6f);
                float k = 1f - Mathf.Pow(1f - _promptT, 3f);
                prompt.transform.localPosition = _promptBase + Vector3.down * (0.25f * (1f - k));
                prompt.transform.localScale = Vector3.one * Mathf.Lerp(0.7f, 1f, k);
            }
        }
    }
}
