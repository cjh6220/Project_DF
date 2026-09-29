using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using Proto.Core;

namespace Proto.UI
{
    /// <summary>
    /// 귀환 연출과 정산 화면.
    ///
    ///   1) 화면이 어두워지고 가운데 띠에 사유 문구 ("너무 지쳐서 더 나아갈 수 없습니다")
    ///   2) 문구가 빠지고 정산 — 제목, 탐험한 방, 획득 자원이 한 줄씩 올라가며 숫자가 차오른다
    ///   3) [스킬트리] [다시 들어가기]
    ///       스킬트리 → 원형 전환 → 노드 트리 화면
    ///       다시 들어가기 → 원형 전환 → 바로 다음 판
    ///
    /// 연출 중에 클릭하거나 키를 누르면 끝까지 건너뛴다. 반복 플레이에서 기다리게 하면 안 된다.
    /// "한 번 더" 버튼을 누르게 만드는 지점이므로 3D 마을보다 여기에 공을 들인다.
    /// </summary>
    public class ResultView : MonoBehaviour
    {
        [SerializeField] RunManager run;
        [SerializeField] IrisTransition iris;
        [SerializeField] SkillTreeView tree;

        [Header("연출 시간 (실제 시간, 초)")]
        [SerializeField] float dimTime = 0.35f;
        [SerializeField] float bannerHold = 1.3f;
        [SerializeField] float countTime = 0.55f;
        [SerializeField] float rowStagger = 0.12f;

        RectTransform _root, _banner, _panel, _lootList;
        Image _dim;
        CanvasGroup _bannerCg, _panelCg, _buttonsCg;
        TextMeshProUGUI _bannerText, _title, _rooms;
        Image _badge;
        Button _treeBtn, _againBtn;

        Coroutine _seq;
        bool _skip;
        int _bestRooms;

        void Awake()
        {
            Build();
            _root.gameObject.SetActive(false);
        }

        void Start()
        {
            run.RunEnded += Show;
            run.RunStarted += Hide;

            _treeBtn.onClick.AddListener(() =>
            {
                if (tree == null) return;
                Go(() => { Hide(); tree.Open(); });
            });
            _againBtn.onClick.AddListener(() => Go(() => { Hide(); run.Restart(); }));
        }

        void Go(System.Action swap)
        {
            if (iris != null) iris.Play(swap); else swap();
        }

        void Hide()
        {
            if (_seq != null) { StopCoroutine(_seq); _seq = null; }
            _root.gameObject.SetActive(false);
        }

        // ───────────────────────────── 조립 ─────────────────────────────

        void Build()
        {
            _root = UiKit.Stretch(UiKit.Rect(transform, "ResultScreen"));

            _dim = UiKit.Image(_root, "Dim", new Color(0f, 0f, 0f, 0f), null, true);
            UiKit.Stretch(_dim.rectTransform);

            // 1단계: 가운데 띠
            var band = UiKit.Image(_root, "Banner", new Color(0f, 0f, 0f, 0.6f));
            _banner = band.rectTransform;
            _banner.anchorMin = new Vector2(0f, 0.5f); _banner.anchorMax = new Vector2(1f, 0.5f);
            _banner.pivot = new Vector2(0.5f, 0.5f);
            _banner.sizeDelta = new Vector2(0f, 96f);
            _banner.anchoredPosition = Vector2.zero;
            _bannerCg = band.gameObject.AddComponent<CanvasGroup>();
            _bannerText = UiKit.Text(_banner, "Text", "", 38);
            UiKit.Stretch(_bannerText.rectTransform);

            // 2단계: 정산
            _panel = UiKit.Place(UiKit.Rect(_root, "Panel"), new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(640f, 640f));
            _panelCg = _panel.gameObject.AddComponent<CanvasGroup>();

            _title = UiKit.Text(_panel, "Title", "", 52);
            UiKit.Place(_title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(640f, 64f));
            _title.fontStyle = FontStyles.Bold;

            var line = UiKit.Image(_panel, "Underline", new Color(1f, 1f, 1f, 0.85f));
            UiKit.Place(line.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(360f, 3f));

            _rooms = UiKit.Text(_panel, "Rooms", "", 26);
            UiKit.Place(_rooms.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -118f), new Vector2(600f, 36f));

            _lootList = UiKit.Place(UiKit.Rect(_panel, "Loot"), new Vector2(0.5f, 1f), new Vector2(0f, -160f), new Vector2(400f, 300f));
            _lootList.pivot = new Vector2(0.5f, 1f);

            var buttons = UiKit.Place(UiKit.Rect(_panel, "Buttons"), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(600f, 60f));
            _buttonsCg = buttons.gameObject.AddComponent<CanvasGroup>();

            _treeBtn = UiKit.Button(buttons, "TreeButton", "스킬트리", new Vector2(230f, 54f), 24);
            ((RectTransform)_treeBtn.transform).anchoredPosition = new Vector2(-125f, 0f);
            _againBtn = UiKit.Button(buttons, "AgainButton", "다시 들어가기", new Vector2(230f, 54f), 24);
            ((RectTransform)_againBtn.transform).anchoredPosition = new Vector2(125f, 0f);

            // 살 수 있는 노드가 있으면 스킬트리 버튼에 빨간 점
            _badge = UiKit.Image(_treeBtn.transform, "Badge", new Color(0.92f, 0.22f, 0.22f), UiKit.Circle);
            UiKit.Place(_badge.rectTransform, new Vector2(1f, 1f), new Vector2(-4f, -4f), new Vector2(20f, 20f));
            var bang = UiKit.Text(_badge.transform, "Mark", "!", 16);
            UiKit.Stretch(bang.rectTransform);
            bang.fontStyle = FontStyles.Bold;
        }

        // ───────────────────────────── 연출 ─────────────────────────────

        void Show(string reason)
        {
            _root.gameObject.SetActive(true);
            transform.SetAsLastSibling();
            if (_seq != null) StopCoroutine(_seq);
            _seq = StartCoroutine(Sequence(reason));
        }

        void Update()
        {
            if (_seq == null) return;
            // 클릭이나 Space/Enter/Esc로 끝까지 건너뛴다.
            // 공격·이동 키는 받지 않는다 — 죽는 순간 연타하던 키가 연출을 날려 버리면 안 된다.
            var kb = Keyboard.current;
            bool pressed = (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                        || (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame));
            if (pressed) _skip = true;
        }

        IEnumerator Sequence(string reason)
        {
            _skip = false;
            bool bossReached = run.Map.CurrentCell == run.Map.BossCell;
            bossRoomDeath = bossReached;
            int rooms = CountVisited();
            bool newBest = rooms > _bestRooms;
            _bestRooms = Mathf.Max(_bestRooms, rooms);

            // 초기 상태
            _dim.color = new Color(0f, 0f, 0f, 0f);
            _bannerCg.alpha = 0f;
            _panelCg.alpha = 0f;
            _panelCg.interactable = _panelCg.blocksRaycasts = false;
            _buttonsCg.alpha = 0f;
            _buttonsCg.interactable = false;
            foreach (Transform c in _lootList) Destroy(c.gameObject);

            _bannerText.text = BannerFor(reason);
            _title.text = TitleFor(reason);
            _rooms.text = "";

            // 1) 어두워지며 띠 문구
            yield return Tween(dimTime, k =>
            {
                _dim.color = new Color(0f, 0f, 0f, 0.6f * k);
                _bannerCg.alpha = k;
            });
            yield return Wait(bannerHold);
            yield return Tween(0.25f, k => _bannerCg.alpha = 1f - k);

            // 2) 정산 패널
            yield return Tween(0.3f, k =>
            {
                // 숲 배경이 밝아서 정산 글씨가 묻히지 않도록 충분히 어둡게
                _dim.color = new Color(0f, 0f, 0f, 0.6f + 0.28f * k);
                _panelCg.alpha = k;
                _panel.localScale = Vector3.one * Mathf.Lerp(0.96f, 1f, k);
            });

            // 탐험한 방 수 카운트
            yield return Tween(countTime, k =>
            {
                int shown = Mathf.RoundToInt(rooms * k);
                _rooms.text = $"탐험한 방  {shown}개   <size=20><color=#aaaaaa>최고 {_bestRooms}개</color></size>"
                              + (newBest && k >= 1f ? "   <size=20><color=#ffd45a>신기록!</color></size>" : "");
            });

            // 획득 자원 — 한 줄씩 올라오며 숫자가 차오른다
            var loot = new List<KeyValuePair<ResourceId, int>>(run.RunLoot.All);
            if (loot.Count == 0)
            {
                var none = UiKit.Text(_lootList, "None", "획득물 없음", 24);
                UiKit.Place(none.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(400f, 36f));
                none.color = new Color(1f, 1f, 1f, 0.5f);
            }
            for (int i = 0; i < loot.Count; i++)
            {
                var kv = loot[i];
                var row = UiKit.Text(_lootList, "Row_" + kv.Key, "", 28);
                UiKit.Place(row.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -24f - i * 42f), new Vector2(400f, 40f));
                row.richText = true;
                string label = $"<color=#{ColorUtility.ToHtmlStringRGB(UiKit.ResourceColor(kv.Key))}>{UiKit.Label(kv.Key)}</color>";
                int amount = kv.Value;
                var rrt = row.rectTransform;
                Vector2 basePos = rrt.anchoredPosition;

                StartCoroutine(Tween(countTime, k =>
                {
                    row.text = $"{label}   +{Mathf.RoundToInt(amount * k)}";
                    row.alpha = Mathf.Clamp01(k * 3f);
                    rrt.anchoredPosition = basePos + new Vector2(0f, -14f * (1f - Mathf.Clamp01(k * 3f)));
                }));
                yield return Wait(rowStagger);
            }
            yield return Wait(countTime);

            // 3) 버튼
            _badge.gameObject.SetActive(AnyAffordable());
            _panelCg.interactable = _panelCg.blocksRaycasts = true;
            yield return Tween(0.2f, k => _buttonsCg.alpha = k);
            _buttonsCg.interactable = true;
            _seq = null;
        }

        /// <summary>건너뛰기를 누르면 즉시 끝 값으로 간다.</summary>
        IEnumerator Tween(float dur, System.Action<float> apply)
        {
            float t = 0f;
            while (t < dur && !_skip)
            {
                t += Time.unscaledDeltaTime;
                apply(Mathf.Clamp01(t / dur));
                yield return null;
            }
            apply(1f);
        }

        IEnumerator Wait(float dur)
        {
            float t = 0f;
            while (t < dur && !_skip) { t += Time.unscaledDeltaTime; yield return null; }
        }

        bool bossRoomDeath;

        string BannerFor(string reason) => reason switch
        {
            "탈진" => "너무 지쳐서 더 나아갈 수 없습니다",
            "쓰러짐" => bossRoomDeath ? "보스 앞에서 쓰러졌습니다" : "더 이상 버틸 수 없습니다",
            "보스 처치" => "숲의 주인을 쓰러뜨렸습니다",
            _ => reason
        };

        static string TitleFor(string reason) => reason switch
        {
            "탈진" => "탈진",
            "쓰러짐" => "쓰러짐",
            "보스 처치" => "보스 처치!",
            _ => reason
        };

        bool AnyAffordable()
        {
            foreach (var n in Progression.Nodes)
                if (run.Progression.CanBuy(n, run.Bank)) return true;
            return false;
        }

        int CountVisited()
        {
            int n = 0;
            var map = run.Map;
            for (int x = 0; x < map.Width; x++)
            for (int y = 0; y < map.Height; y++)
            {
                var r = map.At(new Vector2Int(x, y));
                if (r != null && r.Visited) n++;
            }
            return n;
        }
    }
}
