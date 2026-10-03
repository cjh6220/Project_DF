using UnityEngine;
using UnityEngine.InputSystem;
using Proto.Core;
using Proto.Data;
using Proto.Dungeon;
using Proto.Player;
using Proto.UI;

namespace Proto.Town
{
    /// <summary>
    /// 마을. 게임은 여기서 시작하고, 던전에서 돌아오면 여기로 온다.
    ///
    ///   왼쪽  성장 관리 NPC → 성장 트리 (노드 구매)
    ///   오른쪽 스킬 관리 NPC → 스킬 창
    ///   가운데 문 → 걸어 들어가면 정해 둔 행선지로 바로 들어간다
    ///   문 옆 행선지 석판 → 던전 선택 창. 고르면 행선지만 바뀐다 (들어가지는 않는다)
    ///
    /// 같은 던전을 반복할 때는 걷기만 하면 되고, 바꾸고 싶을 때만 석판에 들른다.
    /// 새 던전이 열리면 석판 위에 "!"가 뜬다. 행선지는 저절로 바뀌지 않는다 — 고르는 건 플레이어다.
    ///
    /// NPC 가까이에서 대화 키(키보드 Space·C / 패드 ×·A)로 대화한다. 창이 열려 있는 동안 캐릭터는 멈춘다.
    /// 마을과 던전 아레나는 같은 자리에 겹쳐 있고, 한쪽을 켜면 다른 쪽을 끈다.
    /// 카메라를 옮기지 않아도 되고, 화면 전환은 원형 와이프가 맡는다.
    /// </summary>
    public class VillageController : MonoBehaviour
    {
        [SerializeField] RunManager run;
        [SerializeField] PlayerController player;

        [Header("마을 월드")]
        [SerializeField] GameObject world;
        [SerializeField] Transform spawn;
        [SerializeField] VillageNpc[] npcs;
        [SerializeField] VillageGate gate;
        [SerializeField] Waystone waystone;

        [Header("던전")]
        [Tooltip("선택 창에 나오는 순서. 앞 던전의 보스를 잡으면 다음 던전이 열린다.")]
        [SerializeField] DungeonDef[] dungeons;
        [Tooltip("테스트용 — 켜 두면 처음부터 모든 던전이 열려 있다")]
        [SerializeField] bool unlockAllForTest = true;

        [Header("UI")]
        [SerializeField] SkillTreeView tree;
        [SerializeField] SkillWindowView skills;
        [SerializeField] DungeonSelectView select;
        [SerializeField] IrisTransition iris;
        [Tooltip("피로도·체력·미니맵 등 던전 전용 HUD. 마을에서는 숨긴다.")]
        [SerializeField] GameObject dungeonHud;
        [Tooltip("하늘색·안개를 던전과 맞춘다")]
        [SerializeField] RoomDecorator environment;

        [Header("마을 조명 — 노을")]
        [SerializeField] Color skyColor = new Color(0.98f, 0.76f, 0.62f);
        [SerializeField] Color ambientColor = new Color(0.58f, 0.46f, 0.48f);
        [SerializeField] Color sunColor = new Color(1f, 0.72f, 0.48f);
        [SerializeField] float sunIntensity = 1.15f;
        [SerializeField] Vector3 sunAngles = new Vector3(28f, 300f, 0f);
        [Tooltip("마을 원경 그림 — 노을 진 산과 성")]
        [SerializeField] Texture2D backdrop;
        [Range(0f, 1f)] [SerializeField] float backdropShift = 0.15f;
        [Range(0f, 1f)] [SerializeField] float backdropHaze = 0.15f;
        [Tooltip("그림 벽 거리 — 성(약 35m) 뒤에 세운다")]
        [SerializeField] float backdropDepth = 44f;

        // 던전으로 돌아갈 때 되돌릴 해의 원래 값
        Light _sun;
        Quaternion _sunRot;
        float _sunIntensity;
        bool _sunSaved;

        [Header("조작")]
        [SerializeField] float talkRange = 2.6f;
        [Tooltip("마을에 막 도착했을 때 문 판정을 잠깐 끈다. 문 앞에 서자마자 다시 빨려 들어가면 안 된다.")]
        [SerializeField] float gateGrace = 0.8f;

        VillageNpc _near;
        float _shownAt;
        bool _entering;
        DungeonDef _target;   // 도리이의 행선지
        // 플레이어가 선택 창에서 이미 본 던전. 열렸는데 아직 못 본 던전이 있으면 석판에 "!"
        readonly System.Collections.Generic.HashSet<string> _seen = new();

        public Vector3 SpawnPoint => spawn != null ? spawn.position : transform.position;
        bool UiOpen => (tree != null && tree.IsOpen) || (skills != null && skills.IsOpen) || (select != null && select.IsOpen);
        public DungeonDef[] Dungeons => dungeons;
        /// <summary>도리이가 차오를 수 있는 상태 — 마을에 있고, 창이 닫혀 있고, 막 도착한 게 아니다.</summary>
        public bool GateReady => run != null && run.Phase == RunPhase.Village && !_entering && !UiOpen
                                 && Time.time - _shownAt >= gateGrace;
        public bool IsCleared(int i) => dungeons != null && i >= 0 && i < dungeons.Length && run.IsCleared(dungeons[i]);
        public bool IsUnlocked(int i) =>
            unlockAllForTest || i <= 0 || (dungeons != null && i < dungeons.Length && run.IsCleared(dungeons[i - 1]));
        public bool IsTarget(int i) => dungeons != null && i >= 0 && i < dungeons.Length && dungeons[i] == Target;
        /// <summary>열렸지만 선택 창에서 아직 못 본 던전 — 카드에 "새로 열림"</summary>
        public bool IsNew(int i) => IsUnlocked(i) && dungeons[i] != null && !_seen.Contains(dungeons[i].id);

        bool AnyNew()
        {
            if (dungeons == null) return false;
            for (int i = 0; i < dungeons.Length; i++) if (IsNew(i)) return true;
            return false;
        }

        void Awake() => GameInput.Ensure();

        void Start()
        {
            if (tree != null) tree.Closed += OnUiClosed;
            if (skills != null) skills.Closed += OnUiClosed;
            // 처음 던전은 처음부터 아는 곳이다
            if (dungeons != null && dungeons.Length > 0 && dungeons[0] != null) _seen.Add(dungeons[0].id);
            if (select != null)
            {
                select.Highlighted += Preview;
                select.Chosen += OnDungeonChosen;
                select.Cancelled += OnSelectCancelled;
            }
        }

        /// <summary>도리이의 행선지 (처음이면 첫 던전).</summary>
        public DungeonDef Target =>
            _target != null ? _target : (dungeons != null && dungeons.Length > 0 ? dungeons[0] : null);

        /// <summary>도리이·석판을 이 던전 색으로. 선택 창에서 카드를 옮길 때마다 미리 보여 준다.</summary>
        void Preview(DungeonDef d) => Paint(d, false);

        void Paint(DungeonDef d, bool instant)
        {
            if (d == null) return;
            if (gate != null) { gate.SetPalette(d.portalColor, d.portalAccent, instant); gate.SetLabel(d.displayName, d.portalAccent); }
            if (waystone != null) waystone.SetColor(d.portalColor, d.portalAccent, instant);
        }

        public void Show()
        {
            if (world != null) world.SetActive(true);
            if (dungeonHud != null) dungeonHud.SetActive(false);
            if (environment != null) environment.EnsureEnvironment();
            ApplyLight();
            _shownAt = Time.time;
            // 정산 화면에서 고른 버튼이 남아 있으면 패드 ×가 그 버튼까지 눌러 버린다
            if (UnityEngine.EventSystems.EventSystem.current != null)
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
            _entering = false;
            SetNear(null);
            if (gate != null) gate.ResetCharge();
            Paint(Target, true);
        }

        public void Hide()
        {
            if (tree != null && tree.IsOpen) tree.Close();
            if (skills != null && skills.IsOpen) skills.Close();
            if (select != null && select.IsOpen) select.Hide();
            SetNear(null);
            if (world != null) world.SetActive(false);
            if (dungeonHud != null) dungeonHud.SetActive(true);
            RestoreLight();
        }

        /// <summary>마을은 노을빛. 하늘·안개·주변광·해를 마을 값으로 바꾼다.</summary>
        void ApplyLight()
        {
            _sun = RoomDecorator.FindSun();
            if (_sun != null)
            {
                if (!_sunSaved) { _sunRot = _sun.transform.rotation; _sunIntensity = _sun.intensity; _sunSaved = true; }
                _sun.color = sunColor;
                _sun.intensity = sunIntensity;
                _sun.transform.rotation = Quaternion.Euler(sunAngles);
            }
            RenderSettings.ambientLight = ambientColor;
            RenderSettings.fogColor = skyColor;
            if (Camera.main != null) Camera.main.backgroundColor = skyColor;
            Backdrop.Show(backdrop, backdropShift, backdropHaze, backdropDepth);
        }

        /// <summary>던전에 들어갈 때 — 해를 원래대로 돌리고 테마 색을 다시 칠한다.</summary>
        void RestoreLight()
        {
            if (_sun != null && _sunSaved)
            {
                _sun.transform.rotation = _sunRot;
                _sun.intensity = _sunIntensity;
            }
            if (environment != null) environment.ReapplyEnvironment();
        }

        void Update()
        {
            if (run == null || run.Phase != RunPhase.Village) return;

            // 살 수 있는 노드가 있으면 성장 관리인 머리 위에, 새 던전이 열렸으면 석판 위에 "!"
            bool affordable = AnyAffordable();
            bool fresh = AnyNew();
            foreach (var n in npcs)
                if (n != null) n.SetBadge(!UiOpen && (n.Role == NpcRole.Growth ? affordable : n.Role == NpcRole.Waystone && fresh));

            if (UiOpen || _entering) { SetNear(null); return; }

            // 가장 가까운 NPC 하나만 대화 대상
            VillageNpc best = null;
            float bestD = talkRange;
            var p = player.transform.position;
            foreach (var n in npcs)
            {
                if (n == null) continue;
                var d = n.transform.position - p; d.y = 0f;
                float m = d.magnitude;
                if (m < bestD) { bestD = m; best = n; }
            }
            SetNear(best);

            if (_near != null && GameInput.InteractPressed) Talk(_near);
        }

        void SetNear(VillageNpc n)
        {
            if (_near == n) return;
            if (_near != null) _near.SetNear(false, null);
            _near = n;
            if (_near != null) _near.SetNear(true, player.transform);
        }

        void Talk(VillageNpc n)
        {
            player.ControlEnabled = false;
            SetNear(null);
            if (n.Role == NpcRole.Growth) { if (tree != null) tree.Open(); }
            else if (n.Role == NpcRole.Waystone) OpenDestination();
            else { if (skills != null) skills.Open(); }
        }

        void OnUiClosed()
        {
            if (run.Phase == RunPhase.Village && !_entering) player.ControlEnabled = true;
        }

        /// <summary>도리이가 다 찼다 (문 안에 충분히 머물렀다). 정해 둔 행선지로 들어간다.</summary>
        public void OnGateEntered()
        {
            if (!GateReady) return;
            SetNear(null);
            Enter(Target);
        }

        public void OnGateExited() { }

        /// <summary>석판 — 행선지 선택 창.</summary>
        void OpenDestination()
        {
            if (select == null || dungeons == null || dungeons.Length == 0) { OnUiClosed(); return; }
            select.Open(this, Mathf.Max(0, System.Array.IndexOf(dungeons, Target)));
        }

        /// <summary>창을 닫으면 지금 열려 있는 던전은 모두 본 것으로 친다 — "!"가 꺼진다.</summary>
        void MarkSeen()
        {
            if (dungeons == null) return;
            for (int i = 0; i < dungeons.Length; i++)
                if (dungeons[i] != null && IsUnlocked(i)) _seen.Add(dungeons[i].id);
        }

        void OnDungeonChosen(DungeonDef d)
        {
            _target = d;
            Paint(d, false);
            if (gate != null) gate.Flare();
            if (waystone != null) waystone.Flare();
            MarkSeen();
            OnUiClosed();
        }

        void OnSelectCancelled()
        {
            // 고르다 만 색은 되돌린다
            Paint(Target, false);
            MarkSeen();
            OnUiClosed();
        }

        void Enter(DungeonDef d)
        {
            _entering = true;
            player.ControlEnabled = false;
            System.Action go = () => run.StartRun(d);
            if (iris != null) iris.Play(go); else go();
        }

        bool AnyAffordable()
        {
            foreach (var node in Progression.Nodes)
                if (run.Progression.CanBuy(node, run.Bank)) return true;
            return false;
        }
    }
}
