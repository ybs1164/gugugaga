using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace TacticsECS
{
    /// <summary>
    /// 전투 전체를 조율하는 단일 진입점. 빈 GameObject에 이 컴포넌트 하나만 붙이면 데모가 돌아간다.
    ///
    /// 구조:
    /// - GridWorld / EntityWorld  : 실제 게임 데이터. EntityWorld는 "유닛"을 모르는 범용 엔티티-컴포넌트 저장소.
    /// - PathfindingSystem / MovementSystem / CombatSystem / AbilitySystem / TurnSystem / EnemyAI : 데이터를 읽고 쓰는 정적 "System"
    /// - GridView / TileView / UnitView : 데이터를 화면에 보여주기만 하는 얇은 "View" (로직 없음)
    /// - BattleController(이 클래스) : 입력을 받아 System을 호출하고, 결과를 View에 반영하는 조율자.
    ///   TurnState(현재 턴/차례) 같은 상태도 System이 아니라 여기(오케스트레이터)가 필드로 들고 있는다.
    ///
    /// 입력은 새 Input System 기준, 프레임당 클릭 시 1회 레이캐스트로만 처리한다
    /// (타일/유닛마다 OnMouseDown 등을 걸지 않음 -> 오브젝트 수가 늘어나도 입력 비용은 그대로).
    /// </summary>
    public class BattleController : MonoBehaviour
    {
        [Header("Grid")]
        [SerializeField] private int gridWidth = 8;
        [SerializeField] private int gridHeight = 8;
        [SerializeField] private float tileSize = 1.2f;
        [Tooltip("이 좌표들만 물(Water) 타일이 되고 나머지는 전부 육지(Land)다. 육지 유닛은 " +
            "IgnoreTerrainAction 패시브 없이는 여기 들어갈 수 없고, 물 유닛(MoveDomain=Water)은 반대로 " +
            "이 타일 밖으로 나갈 수 없다 — Core/TerrainType.cs 참고. 비워두면(기본값) 그리드 전체가 육지다.")]
        [SerializeField] private List<Vector2Int> waterTiles = new List<Vector2Int>();
        [Tooltip("육지 타일 모델(Assets/Prefabs/Tiles/Tile_Land.prefab). 비워두면 예전처럼 프리미티브 " +
            "큐브로 대체된다. TileAssetSetup.GenerateAll로 만든다.")]
        [SerializeField] private GameObject landTilePrefab;
        [Tooltip("물 타일 모델(Assets/Prefabs/Tiles/Tile_Water.prefab). 비워두면 프리미티브 큐브로 대체된다.")]
        [SerializeField] private GameObject waterTilePrefab;

        [Header("Structures (Sandbox 지형 생성 전용)")]
        [Tooltip("바이옴 앵커에 자동 배치되는 수도 모델. Assets/Prefabs/Structures/Structure_Capital.prefab. " +
            "StructureAssetSetup.GenerateAll로 만든다. 비워두면 해당 구조물은 표시되지 않는다(생성 자체는 " +
            "계속되고 GridWorld.StructureId는 채워짐 — 순수 시각 요소라 게임 로직에는 영향 없음).")]
        [SerializeField] private GameObject capitalStructurePrefab;
        [Tooltip("바이옴 CSV의 \"Ruin\" 구조물 모델. Assets/Prefabs/Structures/Structure_Ruin.prefab.")]
        [SerializeField] private GameObject ruinStructurePrefab;
        [Tooltip("바이옴 CSV의 \"Resource_Food\" 구조물 모델. Assets/Prefabs/Structures/Structure_ResourceFood.prefab.")]
        [SerializeField] private GameObject resourceFoodStructurePrefab;
        [Tooltip("바이옴 CSV의 \"Resource_Ore\" 구조물 모델. Assets/Prefabs/Structures/Structure_ResourceOre.prefab.")]
        [SerializeField] private GameObject resourceOreStructurePrefab;
        [Tooltip("바이옴 CSV의 \"Starfish\" 구조물 모델. Assets/Prefabs/Structures/Structure_Starfish.prefab.")]
        [SerializeField] private GameObject starfishStructurePrefab;
        [Tooltip("바이옴 CSV의 \"Village\" 구조물 및 외딴 섬 마을(StructureGenerationSystem.PlaceTinyIslandVillages) " +
            "모델. Assets/Prefabs/Structures/Structure_Village.prefab.")]
        [SerializeField] private GameObject villageStructurePrefab;
        [Tooltip("\"Resource_Fruit\"(과일 덤불). Assets/Prefabs/Structures/Structure_ResourceFruit.prefab — StructureAssetSetup.GenerateProceduralStructures가 생성.")]
        [SerializeField] private GameObject resourceFruitStructurePrefab;
        [Tooltip("\"Resource_Crop\"(밀밭). Assets/Prefabs/Structures/Structure_ResourceCrop.prefab.")]
        [SerializeField] private GameObject resourceCropStructurePrefab;
        [Tooltip("\"Resource_Animal\"(사슴). Assets/Prefabs/Structures/Structure_ResourceAnimal.prefab.")]
        [SerializeField] private GameObject resourceAnimalStructurePrefab;
        [Tooltip("\"Resource_Fish\"(물고기 떼). Assets/Prefabs/Structures/Structure_ResourceFish.prefab.")]
        [SerializeField] private GameObject resourceFishStructurePrefab;
        [Tooltip("맵 네 모서리에 자동 배치되는 등대. Assets/Prefabs/Structures/Structure_Lighthouse.prefab.")]
        [SerializeField] private GameObject lighthouseStructurePrefab;

        [Header("Unit Prefabs")]
        [Tooltip("근접 유닛 프리팹 (UnitView + UnitDefinition 컴포넌트를 가진 프리팹). Assets/Prefabs/Units 참고.")]
        [SerializeField] private UnitView meleePrefab;
        [Tooltip("원거리 유닛 프리팹.")]
        [SerializeField] private UnitView rangedPrefab;
        [Tooltip("방어(탱커) 유닛 프리팹.")]
        [SerializeField] private UnitView guardPrefab;

        [Header("HUD Prefabs")]
        [Tooltip("전투 HUD 프리팹(BattleHud 컴포넌트 + Canvas 이하 전체 UI). Assets/Prefabs/UI/BattleHud.prefab.")]
        [SerializeField] private BattleHud hudPrefab;
        [Tooltip("샌드박스 배치 단계 HUD 프리팹. Assets/Prefabs/UI/SandboxHud.prefab.")]
        [SerializeField] private SandboxHud sandboxHudPrefab;

        [Header("Sandbox Extra Visuals")]
        [Tooltip("샌드박스 CSV 전용 추가 BaseVisual 프리팹 — 데모 편성(SpawnDemoFormation)에는 쓰이지 않고, " +
            "CSV의 BaseVisual 값과 이름이 일치하는 것만 UnitPlacementController의 팔레트 렌더링에 쓰인다.")]
        [SerializeField] private UnitView rogueHoodedPrefab;
        [SerializeField] private UnitView magePrefab;
        [SerializeField] private UnitView skeletonWarriorPrefab;
        [SerializeField] private UnitView skeletonMagePrefab;

        [Header("Stress Test")]
        [Tooltip("데모 편성 외에 팀당 추가로 스폰할 유닛 수. 100+ 오브젝트 성능 확인용.")]
        [SerializeField] private int stressTestExtraUnitsPerTeam = 0;

        [Header("Sandbox")]
        [Tooltip("켜면 데모 편성 대신 CSV로 불러온 유닛을 그리드에 자유 배치하는 단계부터 시작한다. " +
            "배치 단계에서 SandboxHud의 \"전투 시작\" 버튼을 누르면 지금과 동일한 턴제 전투로 이어진다.")]
        [SerializeField] private bool sandboxMode;

        [Header("City Resources")]
        [Tooltip("도시 발전 자원(별/인구/별/신앙) 표시 바 프리팹. 비워두면 생성 자체를 건너뛴다 — " +
            "지금은 커스텀(샌드박스 배치) 화면에만 연결돼 있다. Assets/Prefabs/UI/CityResourceBar.prefab.")]
        [SerializeField] private CityResourceHud cityResourceHudPrefab;
        // 시작 별/신앙 최대치는 인스펙터가 아니라 Assets/Resources/GameRules.csv(Economy.*)에서 온다.
        [Tooltip("기술트리 패널 프리팹. 비워두면 생성 자체를 건너뛴다 — cityResourceHudPrefab과 같은 블록 " +
            "에서만 초기화된다(도시 별이 있어야 의미가 있으므로). Assets/Prefabs/UI/TechTreePanel.prefab.")]
        [SerializeField] private TechTreeHud techTreeHudPrefab;

        [Header("Camera (Pixel 2D)")]

        [Header("Camera Control")]
        [Tooltip("WASD/방향키로 카메라를 이동하는 속도 (월드 단위/초).")]
        [SerializeField] private float cameraPanSpeed = 10f;
        [Tooltip("마우스 휠 한 틱당 orthographicSize 변화량. 값이 클수록 휠에 민감하게 줌된다.")]
        [SerializeField] private float zoomSensitivity = 0.02f;
        [Tooltip("최대로 확대했을 때의 orthographicSize (값이 작을수록 더 확대됨).")]
        [SerializeField] private float minOrthoSize = 2f;
        [Tooltip("최대로 축소했을 때의 orthographicSize.")]
        [SerializeField] private float maxOrthoSize = 20f;

        private GridWorld _grid;
        private EntityWorld _world;
        private TurnState _turnState;
        private GridView _gridView;
        private UnitSpawner _spawner;
        private BattleHud _hud;
        private Camera _cam;
        private PixelCamera _pixelCamera;

        private SandboxHud _sandboxHud;
        private UnitPlacementController _placementController;
        private bool _placementActive;
        private List<BiomeCsvRow> _loadedBiomes;

        /// <summary>샌드박스 "표 불러오기"로 옛 단일 파일 기술트리(TechTree.csv 형식)를 읽었을 때만 채워진다. null이면 지금 표(기술 표들).
        /// 유닛/바이옴과 달리 "다시 시작"(HandleReturnToSetup)해도 유지한다 — 같은 트리로 여러 판을 시험하기 위함.</summary>

        /// <summary>샌드박스 "표 불러오기"로 고른 표 폴더(null = Assets/Resources 기본 표). 언어 버튼이 씬을 다시 열어도 유지되도록
        /// 정적으로 둔다(같은 실행 안에서만). 데모 씬은 쓰지 않는다.</summary>
        private static string s_tableFolder;

        private static readonly Dictionary<string, string> s_csvFiles = new Dictionary<string, string>();
        private static TableSource SandboxTables => new TableSource { Folder = s_tableFolder, Files = s_csvFiles, FromFolder = new List<string>() };

        /// <summary>Polytopia의 6종 맵 크기 프리셋(이름, 정사각형 한 변 길이)을 그대로 채택
        /// (docs/reference/PolytopiaMapGeneration.md 1절) — Sandbox의 "맵 크기" 버튼이 이 목록을 순환한다.</summary>
        private static readonly (string Name, int Size)[] MapSizePresets =
        {
            ("Tiny", 11), ("Small", 14), ("Normal", 16), ("Large", 18), ("Huge", 20), ("Massive", 30)
        };
        private int _selectedMapSizeIndex;

        /// <summary>Polytopia의 6종 습도(맵 타입) 프리셋 이름 + 대표 습도값(docs/reference/PolytopiaMapGeneration.md
        /// 2절 범위의 중간값). CSV에는 저장하지 않는 전역 생성 파라미터 — 맵 크기와 같은 이유로 Sandbox
        /// UI 순환 버튼으로만 선택한다. Continents(0.55)를 배율 1.0의 기준으로 삼는다
        /// (TerrainGenerationSystem.Generate의 wetnessMultiplier = 선택값 / Continents값).</summary>
        private static readonly (string Name, float Wetness)[] WetnessPresets =
        {
            ("Drylands", 0.05f), ("Lakes", 0.275f), ("Continents", 0.55f), ("Pangea", 0.50f), ("Archipelago", 0.70f), ("Waterworld", 0.95f)
        };
        private const int WetnessBaselineIndex = 2; // "Continents"
        private int _selectedWetnessIndex = WetnessBaselineIndex;

        /// <summary>습도 탭 슬라이더 — 1차 지형(아웃라인)의 목표 물 비율. 맵 타입을 고르면 그 타입의 대표값으로 돌아간다.</summary>
        private float _waterRatio = WetnessPresets[WetnessBaselineIndex].Wetness;

        /// <summary>습도 탭 바이옴 드롭다운에서 고른 바이옴(바이옴 목록 Index, 오름차순). 비어 있으면 자동(전체 바이옴, 종족이 있으면 종족 바이옴).</summary>
        private readonly SortedSet<int> _biomeSelection = new SortedSet<int>();

        /// <summary>습도 탭 종족 드롭다운(Tribes.csv Index). 없는 팀은 기본 규칙. "다시 시작"해도 유지한다.</summary>
        private readonly Dictionary<Team, int> _tribeByTeam = new Dictionary<Team, int> { [Team.Player] = -1, [Team.Enemy] = -1 };

        /// <summary>마지막 1차 지형. "지형 생성"은 설정(크기/수도 수/맵 타입/물 비율)이 같으면 이 아웃라인 위에 바이옴만 다시 채운다.</summary>
        private TerrainOutlineData? _outline;

        /// <summary>종족 모드로 생성한 지형의 팀별 수도 자리(영역 i = CitySystem.Teams[i]) — 전투 시작 때 그 팀 수도가 된다.</summary>
        private readonly Dictionary<Team, Vector2Int> _tribeCapitals = new Dictionary<Team, Vector2Int>();

        /// <summary>바이옴 CSV를 불러오지 않았을 때 쓰는 기본 바이옴 표(Resources/Tables/Biomes.csv).</summary>
        private List<BiomeCsvRow> _defaultBiomes;

        private CityResourceHud _cityResourceHud;
        private TechTreeHud _techTreeHud;
        private ActionMenuHud _actionMenu;

        /// <summary>경제(도시/영토/자원/기술) 상태. cityResourceHudPrefab이 배정된 화면(Sandbox)에서 전투가 시작될
        /// 때(BeginBattle) 만들어지고, 그 전(배치 단계)이나 경제 없는 씬(SampleScene)에서는 null이다 — null이면
        /// 모든 경제 규칙(영토 회복/점령/지형 제한/도시 패배 판정)이 꺼지고 예전 순수 전투와 똑같이 동작한다.</summary>
        private EconomyWorld _econ;
        /// <summary>배치 단계에서 불러온 유닛 CSV — 전투가 시작되면 훈련 가능한 유닛 목록(EconomyWorld.UnitRows)이 된다.</summary>
        private List<UnitCsvRow> _unitRows;
        /// <summary>지금 상황별 메뉴가 보여주고 있는 칸(타일/도시 메뉴를 행동 뒤에 다시 그릴 때 쓴다).</summary>
        private Vector2Int? _menuTile;

        // 카메라가 바라보는 지점(월드 XZ)과 고정된 isometric 회전/거리.
        // 팬(이동)은 이 focus 점만 옮기고, 매 프레임 여기서 실제 카메라 position을 재계산한다.
        private Vector3 _cameraFocus;
        private Quaternion _cameraRotation;
        private float _cameraDistance;
        private Vector3 _cameraRight;
        private Vector3 _cameraForwardFlat;

        private readonly Dictionary<int, UnitView> _viewsById = new Dictionary<int, UnitView>();

        private enum SelectState { None, UnitSelected }
        private SelectState _state = SelectState.None;

        // 마지막으로 클릭해 포커스한 칸과 그 칸에서 포커스한 대상 — 같은 칸을 다시 누르면 다음 대상으로 넘긴다.
        private enum FocusLayer { Unit, Tile }
        private Vector2Int? _focusPos;
        private FocusLayer _focusLayer;
        private int _selectedUnitId = -1;
        private HashSet<Vector2Int> _reachableTiles;
        private List<int> _attackableTargets;

        private bool _battleOver;

        private void Awake()
        {
            // 건물/타일 행동 등 게임 규칙 표(Assets/Resources/*.csv)를 Data 표에 채운다. 번역 표는 저장된 언어로.
            LanguagePreference.Apply();
            GameDataLoader.LoadAll(sandboxMode ? SandboxTables : TableSource.Resources);
            _cam = Camera.main;
            if (_cam == null)
            {
                var camGo = new GameObject("MainCamera");
                _cam = camGo.AddComponent<Camera>();
                camGo.tag = "MainCamera";
            }
        }

        private void Start()
        {
            SetupBattle();
        }

        /// <summary>waterTiles(Inspector에서 설정)에 있는 좌표만 물로 표시하고 나머지는 GridWorld의
        /// 기본값(육지)을 그대로 둔다. GridView.Build보다 먼저 호출해야 처음 만들어지는 타일 시각화가
        /// 지형을 올바르게 반영한다.</summary>
        private void ApplyWaterTiles()
        {
            foreach (var p in waterTiles)
                if (_grid.InBounds(p)) _grid.SetTerrain(p, TerrainType.Water);
        }

        private void SetupBattle()
        {
            _grid = new GridWorld(gridWidth, gridHeight, tileSize);
            _world = new EntityWorld();
            ApplyWaterTiles();

            var gridViewGo = new GameObject("GridView");
            gridViewGo.transform.SetParent(transform, false);
            _gridView = gridViewGo.AddComponent<GridView>();
            _gridView.Build(_grid, landTilePrefab, waterTilePrefab);

            _hud = Instantiate(hudPrefab, transform);
            _hud.name = "BattleHud";
            _hud.Init();
            _hud.OnDefendClicked += HandleDefendClicked;
            _hud.OnHealClicked += HandleHealClicked;
            _hud.OnSelfDestructClicked += HandleSelfDestructClicked;
            _hud.OnWaitClicked += HandleWaitClicked;
            _hud.OnDeselectClicked += ClearSelection;
            _hud.OnRestartClicked += HandleReturnToSetup;
            _hud.OnLanguageClicked += HandleLanguageClicked;

            if (cityResourceHudPrefab != null)
            {
                _cityResourceHud = Instantiate(cityResourceHudPrefab, transform);
                _cityResourceHud.name = "CityResourceHud";
                _cityResourceHud.Init();
                RefreshCityResources();

                if (techTreeHudPrefab != null)
                {
                    _techTreeHud = Instantiate(techTreeHudPrefab, transform);
                    _techTreeHud.name = "TechTreeHud";
                    _techTreeHud.Init(CurrentTechNodes());
                    _techTreeHud.OnUnlockRequested += HandleTechUnlockRequested;
                    RefreshTechTree();
                }

                var menuGo = new GameObject("ActionMenuHud");
                menuGo.transform.SetParent(transform, false);
                _actionMenu = menuGo.AddComponent<ActionMenuHud>();
                _actionMenu.Init(_hud.UiFont);
            }

            // 카메라를 유닛 스폰보다 먼저 배치한다 — UnitView가 스폰 시점에 머리 위 체력 표시를
            // Camera.main 방향으로 맞추는데(HpBillboardRotation), 그때 카메라가 아직 기본 회전값이면
            // 잘못된 각도로 굳어버린다(이후 이동/공격 전까지는 다시 계산하지 않으므로).
            PositionCamera();

            var spawnerGo = new GameObject("UnitSpawner");
            spawnerGo.transform.SetParent(transform, false);
            _spawner = spawnerGo.AddComponent<UnitSpawner>();

            if (sandboxMode)
                StartPlacementPhase();
            else
                BeginDemoBattle();
        }

        private void BeginDemoBattle()
        {
            SpawnDemoFormation();
            BeginBattle();
        }

        private void BeginBattle()
        {
            if (_cityResourceHud != null) InitEconomy();
            _hud.OnEndTurnClicked += EndTurn;
            // TurnSystem.StartTurn이 HandleTurnStart를 바로 이 호출 중에 동기로 트리거하므로,
            // 그 전에 _hud가 준비돼 있어야 한다.
            _turnState = TurnSystem.StartTurn(_world, Team.Player, 1);
            RefreshRoster();
            HandleTurnStart(_turnState.ActiveTeam, _turnState.TurnNumber);
        }

        /// <summary>승/패 화면의 "다시 시작" 버튼 핸들러. 이번 전투에 쓰인 자식 오브젝트(GridView/BattleHud/
        /// UnitSpawner와 그 아래 스폰된 유닛들, 샌드박스 모드라면 SandboxHud까지)를 전부 지우고
        /// SetupBattle을 처음부터 다시 호출한다 — 샌드박스 모드에서는 이 재시작이 곧 배치 화면(커스텀
        /// 화면)으로 되돌아가는 것이고, 데모 모드에서는 데모 편성을 다시 스폰하는 것과 같다. Camera.main
        /// (Awake에서 한 번만 찾거나 만듦)과 EventSystem(BattleHud.EnsureEventSystem, transform 밖에 생성됨)은
        /// 이 자식 파괴 대상이 아니라 그대로 재사용된다.</summary>
        private void HandleReturnToSetup()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);

            _viewsById.Clear();
            _battleOver = false;
            _state = SelectState.None;
            _selectedUnitId = -1;
            _reachableTiles = null;
            _attackableTargets = null;
            _placementActive = false;
            _sandboxHud = null;
            _placementController = null;
            _loadedBiomes = null;
            _outline = null;
            _tribeCapitals.Clear();
            _cityResourceHud = null;
            _techTreeHud = null;
            _actionMenu = null;
            _econ = null;
            _unitRows = null;
            _menuTile = null;

            SetupBattle();
        }

        // ---------- Sandbox: CSV 배치 단계 ----------

        /// <summary>SpawnDemoFormation 대신 진입하는 배치 단계. CSV를 불러와 팔레트로 삼고, 그리드를
        /// 클릭해 유닛을 자유 배치한 뒤 SandboxHud의 "전투 시작"을 누르면 BeginBattle로 넘어간다 —
        /// 카메라/그리드/턴/전투 로직은 데모 모드와 완전히 동일한 코드를 그대로 재사용한다.</summary>
        private void StartPlacementPhase()
        {
            _placementActive = true;
            _hud.SetEndTurnVisible(false);

            _placementController = new UnitPlacementController(_world, _spawner, BasePrefabsByName(), _viewsById);

            _sandboxHud = Instantiate(sandboxHudPrefab, transform);
            _sandboxHud.name = "SandboxHud";
            _sandboxHud.Init();
            _sandboxHud.OnCsvSelected += HandleSandboxCsvSelected;
            _sandboxHud.OnUnitSelected += HandleSandboxUnitSelected;
            _sandboxHud.OnTeamSelected += HandleSandboxTeamSelected;
            _sandboxHud.OnStartBattleClicked += HandleSandboxStartBattle;
            _sandboxHud.OnLoadBiomeClicked += HandleBiomeLoad;
            _sandboxHud.OnReloadTablesClicked += ReloadSandboxCsv;
            _sandboxHud.OnResetTablesClicked += () => { s_csvFiles.Clear(); ApplyTables(null); };
            _sandboxHud.OnGenerateTerrainClicked += HandleGenerateTerrain;
            _sandboxHud.OnMapSizeCycleClicked += HandleMapSizeCycle;
            _sandboxHud.OnWetnessCycleClicked += HandleWetnessCycle;
            _sandboxHud.SetSelectedTeam(Team.Player);
            _sandboxHud.SetStatus(LocalizationSystem.T("UI.Sandbox.StartHint"));

            _sandboxHud.OnMapTypeSelected += HandleMapTypeSelected;
            _sandboxHud.OnWaterRatioChanged += HandleWaterRatioChanged;
            _sandboxHud.OnGenerateOutlineClicked += HandleGenerateOutline;
            _sandboxHud.OnBiomeToggled += HandleBiomeToggled;
            _sandboxHud.OnTribeSelected += HandleTribeSelected;

            InitMapSizeSelection();
            _sandboxHud.SetMapSizeLabel(MapSizeName(_selectedMapSizeIndex), MapSizePresets[_selectedMapSizeIndex].Size);
            _sandboxHud.SetMapTypeOptions(WetnessPresets.Select(p => LocalizationSystem.F("UI.Sandbox.MapTypeOption", MapTypeName(p.Name), Mathf.RoundToInt(p.Wetness * 100f))).ToList(), _selectedWetnessIndex);
            _sandboxHud.SetWaterRatio(_waterRatio);
            RefreshWetnessLabel();
            RefreshBiomeOptions();
            _sandboxHud.SetTribeOptions(GameTables.Tribes.Select(t => t.Name).ToList(), _tribeByTeam[Team.Player], _tribeByTeam[Team.Enemy]);
            SetPaletteRows(new List<UnitCsvRow>(GameTables.Units));
            RefreshTribeInfo();
            _sandboxHud.SetCsvSources(s_csvFiles);
        }

        private void ReloadSandboxCsv()
        {
            try { ApplyTables(s_tableFolder); PixelSpriteCatalog.Validate(); }
            catch (System.Exception e)
            {
                // 선택 뒤 외부에서 카탈로그를 잘못 편집한 경우 기본 표시로 복구한다.
                s_csvFiles.Remove("Pixel2D/SpriteCatalog");
                ApplyTables(s_tableFolder);
                PixelSpriteCatalog.Validate();
                _sandboxHud.SetStatus(LocalizationSystem.F("UI.Sandbox.LoadFailed", e.Message));
            }
        }

        private void HandleSandboxCsvSelected(string resourcePath, string file)
        {
            if (!System.Array.Exists(GameDataLoader.SandboxCsvPaths, p => p == resourcePath)) return;
            bool hadPrevious = s_csvFiles.TryGetValue(resourcePath, out var previous);
            try
            {
                if (string.IsNullOrEmpty(file)) s_csvFiles.Remove(resourcePath);
                else
                {
                    System.IO.File.ReadAllText(file);
                    s_csvFiles[resourcePath] = System.IO.Path.GetFullPath(file);
                }
                ApplyTables(null);
                // SpriteCatalog은 화면을 갱신하기 전에 실제로 파싱해 오류를 확인한다.
                PixelSpriteCatalog.Validate();
                _sandboxHud.SetCsvSources(s_csvFiles);
            }
            catch (System.Exception e)
            {
                if (hadPrevious) s_csvFiles[resourcePath] = previous;
                else s_csvFiles.Remove(resourcePath);
                ApplyTables(null);
                _sandboxHud.SetCsvSources(s_csvFiles);
                _sandboxHud.SetStatus(LocalizationSystem.F("UI.Sandbox.LoadFailed", e.Message));
            }
        }

        // ---------- Sandbox: 습도 탭(1차 지형 · 바이옴 선택 · 종족) ----------

        private List<BiomeCsvRow> ActiveBiomes() => _loadedBiomes ?? (_defaultBiomes ??= GameDataLoader.LoadDefaultBiomes(SandboxTables));

        private TribeRow? TribeOf(Team team) =>
            _tribeByTeam.TryGetValue(team, out int i) && TribeSystem.IsValid(i) ? GameTables.Tribes[i] : (TribeRow?)null;

        private bool AnyTribe() => CitySystem.Teams.Any(t => TribeOf(t) != null);

        private void RefreshWetnessLabel() =>
            _sandboxHud.SetWetnessLabel(LocalizationSystem.F("UI.Sandbox.WetnessLabel", MapTypeName(WetnessPresets[_selectedWetnessIndex].Name), Mathf.RoundToInt(_waterRatio * 100f)));

        /// <summary>맵 타입 프리셋의 표시 이름(프리셋 Name은 규칙 키라 바꾸지 않는다 — StartConditionRules.MapType과 비교).</summary>
        private static string MapTypeName(string preset) => LocalizationSystem.T("UI.MapType." + preset, preset);

        private static string MapSizeName(int index) => LocalizationSystem.T("UI.MapSize." + MapSizePresets[index].Name, MapSizePresets[index].Name);

        private void RefreshBiomeOptions()
        {
            var biomes = ActiveBiomes();
            _biomeSelection.RemoveWhere(i => i >= biomes.Count);
            _sandboxHud.SetBiomeOptions(biomes.Select(b => $"{b.Name} ({b.Id})").ToList(), biomes.Select(b => b.Name).ToList(), _biomeSelection);
        }

        private void RefreshTribeInfo()
        {
            var biomes = ActiveBiomes();
            var unitRows = CurrentUnitRows();
            var lines = new List<string>();
            foreach (var team in CitySystem.Teams)
            {
                var tribe = TribeOf(team);
                if (tribe != null) lines.Add(LocalizationSystem.F("UI.Sandbox.TribeInfoLine", TeamName(team), TribeSystem.Summary(tribe.Value, biomes, unitRows)));
            }
            _sandboxHud.SetTribeInfo(lines.Count > 0 ? string.Join("\n", lines) : LocalizationSystem.T("UI.Sandbox.TribeInfoEmpty"));
        }

        /// <summary>배치 단계에서 불러온 유닛 CSV, 없으면 기본 유닛 표(Tables/Units.csv) — 종족 시작 유닛을 Id로 찾는 표.</summary>
        private List<UnitCsvRow> CurrentUnitRows() =>
            _placementController != null && _placementController.Rows.Count > 0 ? new List<UnitCsvRow>(_placementController.Rows) : LoadFallbackUnitRows();

        private void HandleMapTypeSelected(int index)
        {
            _selectedWetnessIndex = Mathf.Clamp(index, 0, WetnessPresets.Length - 1);
            _waterRatio = WetnessPresets[_selectedWetnessIndex].Wetness;
            _sandboxHud.SetWaterRatio(_waterRatio);
            RefreshWetnessLabel();
        }

        private void HandleWaterRatioChanged(float value)
        {
            _waterRatio = Mathf.Clamp01(value);
            RefreshWetnessLabel();
        }

        /// <summary>바이옴 하나를 선택에 넣거나 뺀다(-1 = 모두 빼고 자동).</summary>
        private void HandleBiomeToggled(int index)
        {
            var biomes = ActiveBiomes();
            if (index < 0) _biomeSelection.Clear();
            else if (index < biomes.Count && !_biomeSelection.Remove(index)) _biomeSelection.Add(index);
            RefreshBiomeOptions();
            var selected = SelectedBiomes();
            _sandboxHud.SetStatus(selected.Count > 0
                ? LocalizationSystem.F("UI.Sandbox.BiomeOnly", string.Join(", ", selected.Select(b => b.Name)))
                : LocalizationSystem.T("UI.Sandbox.BiomeAuto"));
        }

        /// <summary>드롭다운에서 고른 바이옴들(목록 순서). 비어 있으면 자동.</summary>
        private List<BiomeCsvRow> SelectedBiomes()
        {
            var biomes = ActiveBiomes();
            return _biomeSelection.Where(i => i < biomes.Count).Select(i => biomes[i]).ToList();
        }

        private void HandleTribeSelected(Team team, int tribeIndex)
        {
            _tribeByTeam[team] = TribeSystem.IsValid(tribeIndex) ? tribeIndex : -1;
            RefreshTribeInfo();
        }

        /// <summary>종족이 하나라도 있으면 팀마다 영역 하나(종족 바이옴 — 바이옴을 골랐으면 고른 바이옴을 팀 순서대로 돌려 씀).
        /// 종족이 없으면 고른 바이옴마다 영역 하나(하나만 골랐으면 그 바이옴 영역 2개 — 수도 2개), 자동이면 불러온 바이옴 전부(바이옴마다 수도 하나).</summary>
        private List<BiomeCsvRow> ResolveRegionBiomes(out bool perTeam)
        {
            var biomes = ActiveBiomes();
            var selected = SelectedBiomes();
            perTeam = AnyTribe();
            if (perTeam)
                return CitySystem.Teams.Select((team, i) => selected.Count > 0 ? selected[i % selected.Count]
                    : TribeOf(team) is TribeRow t ? TribeSystem.RegionBiome(t, biomes) : biomes[0]).ToList();
            if (selected.Count == 1) return new List<BiomeCsvRow> { selected[0], selected[0] };
            if (selected.Count > 1) return selected;
            return new List<BiomeCsvRow>(biomes);
        }

        private bool EnsureGridSize()
        {
            int targetSize = MapSizePresets[_selectedMapSizeIndex].Size;
            if (targetSize == _grid.Width) return true;
            if (!RebuildGridForSize(targetSize)) return false;
            _outline = null;
            return true;
        }

        /// <summary>습도 탭 "1차 지형 생성": 선택한 맵 타입 + 물 비율로 육지/물 모양과 수도/사전 마을 자리만 먼저 만든다(바이옴 없음 —
        /// 회색 육지/파란 물). "지형 생성"은 별도로 새 모양을 생성하고 바이옴을 채운다.</summary>
        private void HandleGenerateOutline()
        {
            if (!EnsureGridSize()) return;
            var regions = ResolveRegionBiomes(out _);
            var mode = ResolveShapeMode(WetnessPresets[_selectedWetnessIndex].Name);
            int seed = System.Environment.TickCount;
            var outline = TerrainGenerationSystem.PlanOutline(_grid, regions.Count, seed, mode, _waterRatio);
            _outline = outline;
            _tribeCapitals.Clear();
            TerrainGenerationSystem.ApplyOutline(_grid, outline);
            _gridView.RefreshTerrain(_grid);
            _gridView.RefreshStructures(_grid, BuildStructurePrefabsById());
            _sandboxHud.SetStatus(LocalizationSystem.F("UI.Sandbox.OutlineDone", Mathf.RoundToInt(TerrainGenerationSystem.OutlineWaterFraction(outline) * 100f),
                Mathf.RoundToInt(_waterRatio * 100f), MapTypeName(WetnessPresets[_selectedWetnessIndex].Name), outline.Anchors.Length,
                outline.Suburbs.Length + outline.PlannedVillages.Length));
        }

        /// <summary>유닛 CSV의 BaseVisual 이름 -> 외형 프리팹. 배치 단계 팔레트와 전투 중 도시 훈련이 공유한다.</summary>
        private Dictionary<string, UnitView> BasePrefabsByName() => new Dictionary<string, UnitView>
        {
            ["Melee"] = meleePrefab,
            ["Ranged"] = rangedPrefab,
            ["Guard"] = guardPrefab,
            ["RogueHooded"] = rogueHoodedPrefab,
            ["Mage"] = magePrefab,
            ["SkeletonWarrior"] = skeletonWarriorPrefab,
            ["SkeletonMage"] = skeletonMagePrefab
        };

        private void HandleWetnessCycle()
        {
            HandleMapTypeSelected((_selectedWetnessIndex + 1) % WetnessPresets.Length);
        }

        /// <summary>현재 gridWidth(인스펙터 값)와 가장 가까운 프리셋을 기본 선택값으로 삼는다.</summary>
        private void InitMapSizeSelection()
        {
            _selectedMapSizeIndex = 0;
            int bestDiff = int.MaxValue;
            for (int i = 0; i < MapSizePresets.Length; i++)
            {
                int diff = Mathf.Abs(MapSizePresets[i].Size - gridWidth);
                if (diff < bestDiff) { bestDiff = diff; _selectedMapSizeIndex = i; }
            }
        }

        private void HandleMapSizeCycle()
        {
            _selectedMapSizeIndex = (_selectedMapSizeIndex + 1) % MapSizePresets.Length;
            var preset = MapSizePresets[_selectedMapSizeIndex];
            _sandboxHud.SetMapSizeLabel(MapSizeName(_selectedMapSizeIndex), preset.Size);
        }

        /// <summary>"표 불러오기": 고른 파일이 표 이름(Units.csv, Strings.csv …)이면 그 파일이 든 표 폴더 전체를 읽고(ApplyTables),
        /// 아니면 옛 단일 파일로 보고 헤더로 종류를 가른다 — 기술트리(Branch/Tier 칸), 바이옴(Kind+Biome), 그 밖은 키형 유닛 CSV.</summary>


        /// <summary>표 폴더(null = 기본 표)를 읽어 전부 다시 올린다 — 폴더에 없는 표는 기본 표. 연결·검증 경고는 콘솔에, 개수는 상태 줄에.
        /// 팔레트(Units.csv)·종족·바이옴·기술트리 패널을 새 표로 다시 그린다. 이미 배치한 유닛은 그대로 둔다.</summary>
        private void ApplyTables(string folder)
        {
            var source = new TableSource { Folder = folder, Files = s_csvFiles, FromFolder = new List<string>() };
            if (source.Folder != null && !System.IO.Directory.Exists(source.Folder))
            {
                _sandboxHud.SetStatus(LocalizationSystem.F("UI.Sandbox.LoadFailed", source.Folder));
                return;
            }
            s_tableFolder = source.Folder;
            var errors = GameDataLoader.LoadAll(source);
            _defaultBiomes = null;
            _loadedBiomes = null;
            _biomeSelection.Clear();
            foreach (var team in CitySystem.Teams)
                if (!TribeSystem.IsValid(_tribeByTeam[team])) _tribeByTeam[team] = -1;

            _sandboxHud.RefreshLabels();
            SetPaletteRows(new List<UnitCsvRow>(GameTables.Units));
            RefreshBiomeOptions();
            _sandboxHud.SetTribeOptions(GameTables.Tribes.Select(t => t.Name).ToList(), _tribeByTeam[Team.Player], _tribeByTeam[Team.Enemy]);
            RefreshTribeInfo();
            if (_techTreeHud != null) _techTreeHud.SetNodes(CurrentTechNodes());

            string status = source.Folder == null ? LocalizationSystem.F("UI.Sandbox.CsvActive", s_csvFiles.Count)
                : LocalizationSystem.F("UI.Sandbox.TablesLoaded", source.FromFolder.Count, System.IO.Path.GetFileName(source.Folder.TrimEnd('/', '\\')));
            if (errors.Count > 0) status += " " + LocalizationSystem.F("UI.Sandbox.Warnings", errors.Count);
            _sandboxHud.SetStatus(status);
            _sandboxHud.SetCsvSources(s_csvFiles);
        }

        /// <summary>"표 내보내기": 지금 쓰는 표 전부를 Resources와 같은 구조로 그 폴더에 쓴다 — 고친 뒤 "표 불러오기"로 다시 읽는다.</summary>


        private void SetPaletteRows(List<UnitCsvRow> rows)
        {
            _placementController.SetRows(rows);
            _sandboxHud.SetPalette(rows);
            _sandboxHud.SetSelectedUnit(rows.Count > 0 ? 0 : -1);
        }

        /// <summary>옛 형식(키형) 유닛 CSV — 읽기만 한다. 팔레트만 이 파일로 바꾸고, 종족 시작 유닛은 이 표에서 Id로 찾는다.</summary>


        private List<TechNodeData> CurrentTechNodes() => GameDataLoader.LoadTechNodes(SandboxTables);

        /// <summary>옛 형식 기술트리 CSV(TechTree.csv와 같은 한 파일 형식, 읽기만)를 불러와 이후 전투의 기술트리로 쓴다. 기술트리 패널도 바로 다시 그려
        /// 배치 단계에서 모양을 확인할 수 있다. 형식 오류(TechCsvSerializer)와 참조 오류(TechTreeValidationSystem)는 콘솔에
        /// 경고로 남기고, 상태 줄에는 개수만 보인다 — 경고가 있어도 읽을 수 있는 만큼은 적용한다.</summary>


        /// <summary>바이옴 CSV를 불러오기만 한다 — 실제 지형 생성은 "지형 생성" 버튼(HandleGenerateTerrain)을
        /// 눌러야 실행된다(불러오기와 생성을 분리해, 같은 바이옴 목록으로 여러 번 재생성해볼 수 있게).</summary>
        private void HandleBiomeLoad(string path)
        {
            try
            {
                var csvText = System.IO.File.ReadAllText(path);
                var biomes = BiomeCsvSerializer.Parse(csvText);
                if (biomes.Count == 0)
                {
                    _sandboxHud.SetStatus(LocalizationSystem.T("UI.Sandbox.BiomeLoadNoRows"));
                    return;
                }
                _loadedBiomes = biomes;
                _biomeSelection.Clear();
                RefreshBiomeOptions();
                RefreshTribeInfo();
                var tribeWarnings = ArrayTableValidationSystem.ValidateLoaded(biomeCount: biomes.Count).Where(w => w.Contains("BiomeIndex")).ToList();
                foreach (var w in tribeWarnings) Debug.LogWarning("[Tribes] " + w);
                _sandboxHud.SetStatus(LocalizationSystem.F("UI.Sandbox.BiomesLoaded", biomes.Count) +
                                      (tribeWarnings.Count > 0 ? " " + LocalizationSystem.F("UI.Sandbox.BiomeTribeWarnings", tribeWarnings.Count) : string.Empty));
            }
            catch (System.Exception e)
            {
                _sandboxHud.SetStatus(LocalizationSystem.F("UI.Sandbox.BiomeLoadFailed", e.Message));
            }
        }

        /// <summary>바이옴으로 그리드를 다시 채운다. 선택된 맵 크기 프리셋이 지금 그리드와 다르면 먼저 그리드 자체를 다시 만든다
        /// (RebuildGridForSize). 이미 유닛이 놓인 칸은 생성 시스템이 건드리지 않으므로 배치 중에 눌러도 안전하다.
        /// 매번 새 시드로 수도/사전 마을 자리와 육지/물 모양을 생성한 뒤 영역별 바이옴
        /// (ResolveRegionBiomes — 자동/단일 바이옴/종족 바이옴)으로 채운다.
        /// 이어서 구조물(StructureGenerationSystem), 종족의 수도 주변 시작 조건(StartConditionSystem)까지
        /// 적용한다. 바이옴 CSV를 불러오지 않았으면 기본 바이옴 표(Resources/Tables/Biomes.csv)를 쓴다.</summary>
        private void HandleGenerateTerrain()
        {
            if (ActiveBiomes().Count == 0)
            {
                _sandboxHud.SetStatus(LocalizationSystem.T("UI.Sandbox.NoBiomes"));
                return;
            }
            if (!EnsureGridSize()) return;

            var mapTypeName = WetnessPresets[_selectedWetnessIndex].Name;
            var shapeMode = ResolveShapeMode(mapTypeName);
            var regions = ResolveRegionBiomes(out bool perTeam);
            int seed = System.Guid.NewGuid().GetHashCode();
            var outline = TerrainGenerationSystem.PlanOutline(_grid, regions.Count, seed, shapeMode, _waterRatio);
            _outline = outline;
            var anchors = TerrainGenerationSystem.GenerateFromOutline(_grid, regions, outline, seed);
            StructureGenerationSystem.Generate(_grid, regions, anchors, seed, outline.Suburbs, outline.PlannedVillages, shapeMode);

            // 종족 모드: 영역 i = CitySystem.Teams[i]. 그 팀 종족의 시작 조건을 수도 주변에 적용하고, 전투 시작 때 그 자리를 수도로 쓴다.
            _tribeCapitals.Clear();
            int startChanges = 0;
            if (perTeam)
            {
                for (int i = 0; i < CitySystem.Teams.Length && i < anchors.Length; i++)
                {
                    var team = CitySystem.Teams[i];
                    _tribeCapitals[team] = anchors[i];
                    var tribe = TribeOf(team);
                    var condition = tribe != null ? TribeSystem.StartCondition(tribe.Value) : null;
                    if (condition != null)
                        startChanges += StartConditionSystem.Apply(_grid, anchors[i], condition.Value, GameTables.StartConditionRules, mapTypeName, seed + i);
                }
                if (startChanges > 0) TerrainGenerationSystem.ClassifyWaterDepth(_grid, regions, anchors);
            }

            // 구조물 단계가 지형을 바꿀 수 있으므로(외딴 섬 마을, Lakes 육지 다리, 얕은 물/깊은 바다 재분류)
            // 지형 표시는 구조물 생성까지 끝난 뒤에 갱신한다.
            _gridView.RefreshTerrain(_grid);
            _gridView.RefreshStructures(_grid, BuildStructurePrefabsById());

            string regionText = perTeam ? LocalizationSystem.F("UI.Sandbox.RegionTribes", string.Join("/", regions.Select(b => b.Name)))
                : _biomeSelection.Count > 0 ? LocalizationSystem.F("UI.Sandbox.RegionSingle", string.Join(", ", SelectedBiomes().Select(b => b.Name))) : LocalizationSystem.F("UI.Sandbox.RegionCount", regions.Count);
            _sandboxHud.SetStatus(LocalizationSystem.F("UI.Sandbox.TerrainDone", regionText, _grid.Width, _grid.Height, MapTypeName(mapTypeName),
                Mathf.RoundToInt(outline.WaterFraction * 100f), LocalizationSystem.T("UI.Sandbox.OutlineNew"),
                startChanges > 0 ? LocalizationSystem.F("UI.Sandbox.StartChanges", startChanges) : string.Empty, seed));
        }

        /// <summary>습도 프리셋 이름 -> TerrainGenerationSystem.MapShapeMode. Drylands만 Freeform(마스크
        /// 없이 습도 배율만 적용)이고, 나머지 5종은 같은 이름의 마스크 모드로 1:1 매핑한다.</summary>
        private static TerrainGenerationSystem.MapShapeMode ResolveShapeMode(string wetnessPresetName) => wetnessPresetName switch
        {
            "Pangea" => TerrainGenerationSystem.MapShapeMode.Pangea,
            "Lakes" => TerrainGenerationSystem.MapShapeMode.Lakes,
            "Continents" => TerrainGenerationSystem.MapShapeMode.Continents,
            "Archipelago" => TerrainGenerationSystem.MapShapeMode.Archipelago,
            "Waterworld" => TerrainGenerationSystem.MapShapeMode.Waterworld,
            _ => TerrainGenerationSystem.MapShapeMode.Freeform,
        };

        private Dictionary<string, GameObject> BuildStructurePrefabsById() => new Dictionary<string, GameObject>
        {
            [StructureGenerationSystem.CapitalStructureId] = capitalStructurePrefab,
            ["Ruin"] = ruinStructurePrefab,
            ["Resource_Food"] = resourceFoodStructurePrefab,
            ["Resource_Ore"] = resourceOreStructurePrefab,
            ["Resource_Fruit"] = resourceFruitStructurePrefab,
            ["Resource_Crop"] = resourceCropStructurePrefab,
            ["Resource_Animal"] = resourceAnimalStructurePrefab,
            ["Resource_Fish"] = resourceFishStructurePrefab,
            // 광물은 기존 광물 노두 모델이 곧 광물 모델이다.
            ["Resource_Metal"] = resourceOreStructurePrefab,
            [StructureGenerationSystem.LighthouseStructureId] = lighthouseStructurePrefab,
            ["Starfish"] = starfishStructurePrefab,
            [StructureGenerationSystem.VillageStructureId] = villageStructurePrefab
        };

        /// <summary>그리드를 size x size로 다시 만든다. 이미 배치된 유닛이 있으면 좌표가 깨지므로 거부한다
        /// (맵 크기는 유닛을 배치하기 전에만 바꿀 수 있다는 안전 규칙 — TerrainGenerationSystem이 점유 칸을
        /// 건드리지 않는 것과 같은 이유). 성공하면 GridWorld/GridView를 새로 만들고 카메라를 재배치한다.</summary>
        private bool RebuildGridForSize(int size)
        {
            if (_viewsById.Count > 0)
            {
                _sandboxHud.SetStatus(LocalizationSystem.T("UI.Sandbox.ClearUnitsFirst"));
                return false;
            }

            DestroySafe(_gridView.gameObject);

            gridWidth = size;
            gridHeight = size;
            _grid = new GridWorld(gridWidth, gridHeight, tileSize);

            var gridViewGo = new GameObject("GridView");
            gridViewGo.transform.SetParent(transform, false);
            _gridView = gridViewGo.AddComponent<GridView>();
            _gridView.Build(_grid, landTilePrefab, waterTilePrefab);

            PositionCamera();
            return true;
        }

        /// <summary>Play 모드에서는 Destroy(다음 프레임 파괴), Edit 모드(배치모드 검증 스크립트 등)에서는
        /// DestroyImmediate를 써야 한다 — Edit 모드에서 Destroy를 부르면 Unity가 무시하고 에러만 남긴다.</summary>
        private static void DestroySafe(Object obj)
        {
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }

        private void HandleSandboxUnitSelected(int index)
        {
            _placementController.SelectRow(index);
            _sandboxHud.SetSelectedUnit(index);
        }

        private void HandleSandboxTeamSelected(Team team)
        {
            _placementController.SelectTeam(team);
            _sandboxHud.SetSelectedTeam(team);
        }

        private void HandleSandboxStartBattle()
        {
            // 유닛을 배치하지 않은 팀도 시작할 수 있다 — InitEconomy가 그 팀의 수도를 정하고 시작 유닛을 하나 스폰한다.
            _unitRows = new List<UnitCsvRow>(_placementController.Rows);
            _placementActive = false;
            Destroy(_sandboxHud.gameObject);
            _sandboxHud = null;
            _placementController = null;

            BeginBattle();
        }

        /// <summary>턴 종료 버튼(플레이어)과 적 턴 종료(RunEnemyTurnRoutine) 모두에서 쓰는 공통 진입점.
        /// 턴 종료 시 해당 팀의 미행동 유닛은 자동으로 대기(회복) 처리된다.</summary>
        private void EndTurn()
        {
            var hpBefore = SnapshotHp();
            var waitedIds = AbilitySystem.ApplyTurnEndWait(_grid, _world, _turnState.ActiveTeam);
            foreach (var id in waitedIds)
            {
                int healed = _world.Get<Hp>(id).Value - (hpBefore.TryGetValue(id, out var before) ? before : 0);
                _hud.AddLogEntry(FormatLogEntry(new BattleLogEntry { ActorId = id, Verb = BattleLogVerb.Wait, TargetId = BattleLogEntry.NoTarget, Amount = healed }));
                if (_viewsById.TryGetValue(id, out var view))
                    view.Refresh(_world, id);
            }
            RefreshRoster();
            if (_econ != null) TaskSystem.EndTurn(_econ, _turnState.ActiveTeam);

            _turnState = TurnSystem.EndTurn(_world, _turnState);
            HandleTurnStart(_turnState.ActiveTeam, _turnState.TurnNumber);
        }

        private void SpawnDemoFormation()
        {
            string defender = LocalizationSystem.T("UI.Demo.Defender"), warrior = LocalizationSystem.T("UI.Demo.Warrior"), archer = LocalizationSystem.T("UI.Demo.Archer");
            SpawnUnit(Team.Player, guardPrefab, new Vector2Int(1, 1), defender);
            SpawnUnit(Team.Player, meleePrefab, new Vector2Int(1, 3), warrior);
            SpawnUnit(Team.Player, rangedPrefab, new Vector2Int(0, 5), archer);
            SpawnUnit(Team.Player, meleePrefab, new Vector2Int(1, 6), warrior);

            SpawnUnit(Team.Enemy, guardPrefab, new Vector2Int(gridWidth - 2, gridHeight - 2), defender);
            SpawnUnit(Team.Enemy, meleePrefab, new Vector2Int(gridWidth - 2, gridHeight - 4), warrior);
            SpawnUnit(Team.Enemy, rangedPrefab, new Vector2Int(gridWidth - 1, gridHeight - 6), archer);
            SpawnUnit(Team.Enemy, meleePrefab, new Vector2Int(gridWidth - 2, gridHeight - 7), warrior);

            if (stressTestExtraUnitsPerTeam > 0)
                SpawnStressTestUnits(stressTestExtraUnitsPerTeam);
        }

        private void SpawnStressTestUnits(int perTeam)
        {
            var prefabs = new[] { meleePrefab, rangedPrefab, guardPrefab };
            var labels = new[] { LocalizationSystem.T("UI.Demo.Warrior"), LocalizationSystem.T("UI.Demo.Archer"), LocalizationSystem.T("UI.Demo.Defender") };
            var rng = new System.Random(12345);
            for (int i = 0; i < perTeam; i++)
            {
                int typeIndex = i % prefabs.Length;
                var prefab = prefabs[typeIndex];
                var label = labels[typeIndex];
                var p = RandomFreeTile(rng, 0, gridWidth / 2);
                if (p.HasValue) SpawnUnit(Team.Player, prefab, p.Value, label);

                var e = RandomFreeTile(rng, gridWidth / 2, gridWidth);
                if (e.HasValue) SpawnUnit(Team.Enemy, prefab, e.Value, label);
            }
        }

        private Vector2Int? RandomFreeTile(System.Random rng, int xMin, int xMax)
        {
            for (int attempt = 0; attempt < 50; attempt++)
            {
                var p = new Vector2Int(rng.Next(xMin, xMax), rng.Next(0, gridHeight));
                if (!_grid.IsOccupied(p)) return p;
            }
            return null;
        }

        private void SpawnUnit(Team team, UnitView prefab, Vector2Int pos, string label = null)
        {
            if (_grid.IsOccupied(pos)) return;
            var view = _spawner.Spawn(_grid, _world, team, prefab, pos, label);
            _viewsById[view.UnitId] = view;
        }

        /// <summary>
        /// 논리 XZ 그리드를 XY로 투영한 고정 직교 카메라. 정수 픽셀 배율은 PixelCamera가 맞춘다.
        /// </summary>
        private void PositionCamera()
        {
            var center = PixelCoordinates.FromLogical(_grid.Origin) + new Vector3((gridWidth - 1) * tileSize * .5f, (gridHeight - 1) * tileSize * .5f, 0);

            _cameraRotation = Quaternion.identity;
            _cameraDistance = 20f;
            _cameraFocus = center;
            // 카메라가 바라보는 평면(그리드 바닥) 기준 좌/우, 앞/뒤 방향. 팬 입력을 여기에 투영한다.
            _cameraRight = Vector3.right;
            _cameraForwardFlat = Vector3.up;

            _cam.orthographic = true;
            if (_pixelCamera == null) _pixelCamera = _cam.GetComponent<PixelCamera>();
            if (_pixelCamera == null) _pixelCamera = _cam.gameObject.AddComponent<PixelCamera>();
            _pixelCamera.RequestedSize = Mathf.Clamp(Mathf.Max(gridHeight * tileSize * .6f, gridWidth * tileSize * .6f / _cam.aspect), minOrthoSize, maxOrthoSize);
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(.12f, .17f, .23f);
            _cam.allowHDR = false;
            _cam.allowMSAA = false;
            _cam.transparencySortMode = TransparencySortMode.CustomAxis;
            _cam.transparencySortAxis = Vector3.up;
            var cameraData = _cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (cameraData != null) cameraData.renderPostProcessing = false;
            ApplyCameraTransform();
        }

        private void ApplyCameraTransform()
        {
            _cam.transform.rotation = _cameraRotation;
            if (_pixelCamera == null) _pixelCamera = _cam.GetComponent<PixelCamera>();
            if (_pixelCamera == null) _pixelCamera = _cam.gameObject.AddComponent<PixelCamera>();
            _pixelCamera.WorldCellSize = tileSize;
            _pixelCamera.RequestedPosition = _cameraFocus - Vector3.forward * _cameraDistance;
            _pixelCamera.Apply();
        }

        /// <summary>
        /// 카메라 focus를 그리드 영역 주변(여유 마진 포함)으로 제한해 배틀필드를 완전히 벗어나지 않게 한다.
        /// </summary>
        private void ClampCameraFocus()
        {
            float margin = Mathf.Max(gridWidth, gridHeight) * tileSize * 0.5f;
            float minX = _grid.Origin.x - margin;
            float maxX = _grid.Origin.x + (gridWidth - 1) * tileSize + margin;
            float minZ = _grid.Origin.z - margin;
            float maxZ = _grid.Origin.z + (gridHeight - 1) * tileSize + margin;
            _cameraFocus.x = Mathf.Clamp(_cameraFocus.x, minX, maxX);
            _cameraFocus.y = Mathf.Clamp(_cameraFocus.y, minZ, maxZ);
        }

        /// <summary>
        /// 키보드 팬 + 마우스 휠 줌. 턴/전투 상태와 무관하게 항상 동작한다(순수 카메라 뷰 조작이므로).
        /// </summary>
        private void HandleCameraControl()
        {
            if (Keyboard.current != null)
            {
                var move = Vector2.zero;
                if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) move.y += 1f;
                if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) move.y -= 1f;
                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) move.x += 1f;
                if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) move.x -= 1f;

                if (move != Vector2.zero)
                {
                    move.Normalize();
                    var delta = (_cameraRight * move.x + _cameraForwardFlat * move.y) * cameraPanSpeed * Time.deltaTime;
                    _cameraFocus += delta;
                    ClampCameraFocus();
                    ApplyCameraTransform();
                }
            }

            if (Mouse.current != null)
            {
                float scroll = Mouse.current.scroll.ReadValue().y;
                if (!Mathf.Approximately(scroll, 0f))
                {
                    _pixelCamera.RequestedSize = Mathf.Clamp(
                        _pixelCamera.RequestedSize - scroll * zoomSensitivity,
                        minOrthoSize, maxOrthoSize);
                    _pixelCamera.Apply();
                }
            }
        }

        // ---------- Turn flow ----------

        private void HandleTurnStart(Team team, int turnNumber)
        {
            _hud.SetTurn(team, turnNumber);
            _hud.SetEndTurnVisible(team == Team.Player && !_battleOver);
            ClearSelection();

            if (_econ != null)
            {
                _econ.Turn = turnNumber;
                // 폴리토피아처럼 첫 턴은 시작 자원만 쓰고, 2턴부터 매 자기 턴 시작마다 도시 수입이 들어온다.
                if (turnNumber > 1)
                    _econ.Resources[team] = CityResourceSystem.ApplyTurnStart(_econ.Resources[team], _grid, _world, _econ, team);
                RefreshEconomyViews(false);
            }

            if (team == Team.Enemy && !_battleOver)
                StartCoroutine(RunEnemyTurnRoutine());
        }

        /// <summary>RefreshRoster와 같은 방식 — 인구 사용량(populationUsed)은 CityResourceData가 아니라
        /// EntityWorld 쪽 값이라 매번 다시 세어서 넘긴다.</summary>
        private void RefreshCityResources()
        {
            if (_cityResourceHud == null) return;
            int populationUsed = CityResourceSystem.CountPopulation(_world, Team.Player);
            var resources = _econ != null ? _econ.Resources[Team.Player] : CityResourceData.Create(0, 0, false);
            _cityResourceHud.SetResources(resources, populationUsed);
            _cityResourceHud.SetScoreLine(_econ != null
                ? LocalizationSystem.F("UI.City.ScoreLine", ScoreSystem.Compute(_grid, _world, _econ, Team.Player), ScoreSystem.Compute(_grid, _world, _econ, Team.Enemy))
                : string.Empty);
        }

        private void RefreshTechTree()
        {
            if (_techTreeHud == null) return;
            if (_econ != null)
                _techTreeHud.SetState(_econ.Tech[Team.Player], _econ.Resources[Team.Player], CitySystem.CountCities(_econ, Team.Player));
            else
                _techTreeHud.SetState(TechTreeData.CreateEmpty(), CityResourceData.Create(0, 0, false), 1);
        }

        /// <summary>TechTreeHud.OnUnlockRequested 핸들러. 실제 해금 판정/별 소모는 TechSystem이 계산하고,
        /// 결과를 HUD/지도(숨겨진 자원 공개, 지형 이동/방어 효과)에 다시 반영한다. 배치 단계(경제 시작 전)나
        /// 적 턴에는 무시한다.</summary>
        private void HandleTechUnlockRequested(string id)
        {
            if (_econ == null || _battleOver || _turnState.ActiveTeam != Team.Player) return;
            var res = _econ.Resources[Team.Player];
            if (!TechSystem.Unlock(_econ.TechNodes, _econ.Tech[Team.Player], ref res, CitySystem.CountCities(_econ, Team.Player), id)) return;
            _econ.Resources[Team.Player] = res;
            ProcessEconomyLog(new List<EconomyLogEntry> { new EconomyLogEntry { Team = Team.Player, Kind = EconomyLogKind.Research, Subject = TechSystem.Find(_econ.TechNodes, id).Value.Name, CityIndex = -1 } });
            RefreshEconomyViews(false);
            RefreshMenu();
        }

        private IEnumerator RunEnemyTurnRoutine()
        {
            yield return new WaitForSeconds(0.3f);
            if (_econ != null)
            {
                ProcessEconomyLog(EconomyAI.RunTurn(_grid, _world, _econ, Team.Enemy));
                RefreshEconomyViews(false);
                CheckBattleEnd();
                if (_battleOver) yield break;
            }
            var entries = EnemyAI.RunTurn(_grid, _world, _econ, Team.Enemy, _econ != null ? CaptureTargets(Team.Enemy) : null);
            if (_econ != null) RefreshEconomyViews(false);
            RefreshAllViews();
            RefreshRoster();

            foreach (var entry in entries)
            {
                if (entry.Verb == BattleLogVerb.Attack)
                    _viewsById[entry.ActorId].PlayAttackTowards(PixelCoordinates.GridToWorld(_grid, _world.Get<GridPosition>(entry.TargetId).Value));
                if ((entry.Verb == BattleLogVerb.Attack || entry.Verb == BattleLogVerb.Counter) && entry.Amount > 0)
                    _viewsById[entry.TargetId].ShowDamagePopup(entry.Amount);
                _hud.AddLogEntry(FormatLogEntry(entry));
            }

            CheckBattleEnd();
            yield return new WaitForSeconds(0.3f);
            if (!_battleOver)
                EndTurn();
        }

        private void RefreshAllViews()
        {
            for (int i = 0; i < _world.EntityCount; i++)
            {
                if (_viewsById.TryGetValue(i, out var view))
                    view.Refresh(_world, i);
            }
            RefreshUnitVisibility();
        }

        /// <summary>시야: 플레이어가 탐험하지 않은 칸(구름)에 있는 다른 팀 유닛은 숨긴다(경제 없는 씬은 전부 보임).</summary>
        private void RefreshUnitVisibility()
        {
            foreach (var kv in _viewsById)
            {
                if (!UnitQueries.IsAlive(_world, kv.Key)) continue;
                bool visible = _world.Get<Team>(kv.Key) == Team.Player || VisionSystem.IsExplored(_grid, Team.Player, _world.Get<GridPosition>(kv.Key).Value);
                kv.Value.SetVisible(visible);
            }
        }

        /// <summary>좌상단 유닛 로스터를 현재 EntityWorld 상태로 다시 그린다. 체력이 바뀌거나(공격/치유/
        /// 자폭/대기) 유닛이 죽는 모든 행동 직후 호출한다.</summary>
        private void RefreshRoster() => _hud.SetRoster(_world);

        /// <summary>지금 살아있는 모든 유닛의 체력을 id별로 찍어둔다. 자폭/턴 종료 자동 대기처럼 한 번의
        /// 호출로 여러 유닛의 체력이 한꺼번에 바뀌는 행동에서, 실행 전후 차이로 데미지 팝업/회복량을
        /// 정확히 구하는 데 쓴다(단일 대상 공격은 CombatSystem.TryAttack의 out 값을 그대로 쓰므로 필요 없다).</summary>
        private Dictionary<int, int> SnapshotHp()
        {
            var snapshot = new Dictionary<int, int>();
            for (int i = 0; i < _world.EntityCount; i++)
                if (UnitQueries.IsAlive(_world, i)) snapshot[i] = _world.Get<Hp>(i).Value;
            return snapshot;
        }

        /// <summary>BattleLogEntry 값 하나를 우측 상단 행동 로그에 쓸 한 줄짜리 한글 문구로 바꾼다.
        /// 유닛 이름(UnitView.Label)과 소속 팀 강조색(BattleHud.PlayerAccent/EnemyAccent)은 View 쪽
        /// 값이라 여기(오케스트레이터)에서 조합한다 — SandboxHud 상태 문구(HandleSandboxLoad 등)와
        /// 같은 방식이다.</summary>
        private string FormatLogEntry(BattleLogEntry entry)
        {
            string Name(int id) => _viewsById.TryGetValue(id, out var view) ? view.Label : $"#{id}";
            string Colored(int id)
            {
                var accent = _world.Get<Team>(id) == Team.Player ? BattleHud.PlayerAccent : BattleHud.EnemyAccent;
                return $"<color=#{ColorUtility.ToHtmlStringRGB(accent)}>{Name(id)}</color>";
            }

            switch (entry.Verb)
            {
                case BattleLogVerb.Move: return LocalizationSystem.F("UI.Log.Move", Colored(entry.ActorId));
                case BattleLogVerb.Attack: return LocalizationSystem.F("UI.Log.Attack", Colored(entry.ActorId), Colored(entry.TargetId), entry.Amount);
                case BattleLogVerb.Counter: return LocalizationSystem.F("UI.Log.Counter", Colored(entry.ActorId), Colored(entry.TargetId), entry.Amount);
                case BattleLogVerb.Defend: return LocalizationSystem.F("UI.Log.Defend", Colored(entry.ActorId));
                case BattleLogVerb.Heal: return LocalizationSystem.F("UI.Log.Heal", Colored(entry.ActorId), entry.Amount);
                case BattleLogVerb.SelfDestruct: return LocalizationSystem.F("UI.Log.SelfDestruct", Colored(entry.ActorId));
                case BattleLogVerb.Wait: return entry.Amount > 0 ? LocalizationSystem.F("UI.Log.WaitHeal", Colored(entry.ActorId), entry.Amount) : LocalizationSystem.F("UI.Log.Wait", Colored(entry.ActorId));
                case BattleLogVerb.Defeated: return LocalizationSystem.F("UI.Log.Defeated", Colored(entry.ActorId));
                case BattleLogVerb.Promote: return LocalizationSystem.F("UI.Log.Promote", Colored(entry.ActorId));
                default: return Colored(entry.ActorId);
            }
        }

        private void CheckBattleEnd()
        {
            bool playerAlive = !HasTeamLost(Team.Player);
            bool enemyAlive = !HasTeamLost(Team.Enemy);
            if (!playerAlive || !enemyAlive)
            {
                _battleOver = true;
                ClearSelection();
                _hud.SetEndTurnVisible(false);
                _hud.ShowBattleEnd(playerWon: playerAlive);
            }
        }

        // ---------- Input ----------

        private void Update()
        {
            HandleCameraControl();

            if (Mouse.current == null) return;
            // HUD 버튼(BattleHud/SandboxHud, uGUI) 위 클릭은 그리드 클릭으로 새지 않게 막는다.
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            if (_placementActive)
            {
                UpdateStructureHover();
                return;
            }

            if (_battleOver) return;
            if (_turnState.ActiveTeam != Team.Player) return;

            if (Mouse.current.leftButton.wasPressedThisFrame)
                HandleClick();
        }

        /// <summary>Intersect the XY view plane and use one cell-boundary policy for placement, battle and hover.</summary>
        private bool TryScreenToGridPos(Vector2 screenPos, out Vector2Int gridPos)
        {
            return PixelCoordinates.TryScreenToGrid(_cam, _grid, screenPos, out gridPos);
        }

        /// <summary>전투 시작 전(배치 단계)에는 클릭이 이미 유닛 배치에 쓰여서 구조물 정보를 클릭으로
        /// 보여줄 자리가 없다 — 대신 마우스가 가리키는 칸을 매 프레임 검사해(호버) SandboxHud에 정보
        /// 패널을 띄운다. 전투 중(FocusTile)의 클릭 방식과 트리거만 다를 뿐 조회 로직은 같다.</summary>
        private void UpdateStructureHover()
        {
            if (!TryScreenToGridPos(Mouse.current.position.ReadValue(), out var pos))
            {
                _sandboxHud.HideStructurePanel();
                return;
            }

            string structureId = VisibleStructure(pos);
            if (!string.IsNullOrEmpty(structureId))
                _sandboxHud.ShowStructurePanel(structureId);
            else
                _sandboxHud.HideStructurePanel();
        }

        /// <summary>
        /// 그리드 클릭. 유닛이 선택된 상태에서 공격 대상/이동 가능 칸을 누르면 그 행동을 하고, 그 밖의 클릭은
        /// 선택 취소 없이 곧바로 클릭한 칸으로 포커스를 옮긴다. 한 칸에 유닛과 도시/건물/구조물이 겹쳐 있으면
        /// 같은 칸을 다시 누를 때마다 유닛 → 칸(도시/건물) → 유닛 … 순서로 포커스가 하나씩 넘어간다.
        /// </summary>
        private void HandleClick()
        {
            if (!TryScreenToGridPos(Mouse.current.position.ReadValue(), out var gridPos)) return;
            if (!VisionSystem.IsExplored(_grid, Team.Player, gridPos)) { ClearSelection(); return; } // 구름 칸

            int occupantId = _grid.GetOccupant(gridPos);
            // 숨은 적(위키 Cloak)은 없는 것처럼 취급한다 — 그 칸으로 이동을 시도하면 MoveAction이 드러낸다.
            bool hasUnit = occupantId != TileData.NoOccupant && UnitQueries.IsAlive(_world, occupantId) &&
                           !StealthSystem.IsHiddenFrom(_world, occupantId, Team.Player);

            if (_state == SelectState.UnitSelected)
            {
                if (hasUnit && _attackableTargets != null && _attackableTargets.Contains(occupantId))
                {
                    TryAttack(_selectedUnitId, occupantId);
                    return;
                }
                if (!hasUnit && _reachableTiles != null && _reachableTiles.Contains(gridPos))
                {
                    MoveSelectedUnit(gridPos);
                    return;
                }
            }

            // 이 칸에서 포커스할 수 있는 것들: 유닛(있으면), 칸(유닛이 없거나 칸에 보여줄 것이 있으면).
            var layers = new List<FocusLayer>();
            if (hasUnit) layers.Add(FocusLayer.Unit);
            if (!hasUnit || HasTileContent(gridPos)) layers.Add(FocusLayer.Tile);

            int next = 0;
            if (_focusPos == gridPos)
            {
                int current = layers.IndexOf(_focusLayer);
                if (current >= 0) next = (current + 1) % layers.Count;
            }

            if (layers[next] == FocusLayer.Unit) FocusUnit(occupantId);
            else FocusTile(gridPos);
        }

        /// <summary>유닛에 포커스: 이번 턴 움직일 수 있는 아군이면 선택(이동/공격 하이라이트 + 유닛 메뉴), 그 밖의
        /// 유닛(적, 행동을 마친 아군)은 정보 패널만 보여준다.</summary>
        private void FocusUnit(int unitId)
        {
            var pos = _world.Get<GridPosition>(unitId).Value;
            bool own = _world.Get<Team>(unitId) == Team.Player;
            if (own && !(_world.Get<HasMoved>(unitId).Value && _world.Get<HasActed>(unitId).Value))
            {
                SelectUnit(unitId);
                ShowUnitMenu(unitId);
            }
            else
            {
                ClearSelection();
                _hud.ShowUnitPanel(_world, unitId);
                // 뗏목 업그레이드는 행동을 쓰지 않아 승선한 그 턴에도 할 수 있다(위키 Raft).
                if (own && EmbarkSystem.NavalUnitId(_world, unitId) == NavalUnitDefinition.RaftId) ShowUnitMenu(unitId);
            }
            _focusPos = pos;
            _focusLayer = FocusLayer.Unit;
        }

        /// <summary>칸에 포커스: 유닛 선택을 풀고 구조물 정보 패널(아직 기술이 없어 숨겨진 자원은 없는 것처럼
        /// 취급)과, 경제가 켜져 있으면 그 칸의 도시/건설 메뉴를 띄운다.</summary>
        private void FocusTile(Vector2Int pos)
        {
            ClearSelection();
            string structureId = VisibleStructure(pos);
            if (!string.IsNullOrEmpty(structureId))
                _hud.ShowStructurePanel(structureId);
            ShowTileMenu(pos);
            _focusPos = pos;
            _focusLayer = FocusLayer.Tile;
        }

        /// <summary>유닛과 겹쳐 있을 때 따로 포커스할 만한 것이 칸에 있는지 — 보이는 구조물, 도시, 건물, 도로,
        /// 또는 그 칸의 채집/건설 선택지.</summary>
        private bool HasTileContent(Vector2Int pos)
        {
            if (!string.IsNullOrEmpty(VisibleStructure(pos))) return true;
            if (_econ == null) return false;
            var tile = _grid.GetTile(pos);
            if (CitySystem.FindCityAt(_econ, pos) >= 0 || !string.IsNullOrEmpty(tile.BuildingId) || tile.HasRoad) return true;
            return TileImprovementSystem.GetOptions(_grid, _econ, Team.Player, pos).Count > 0;
        }

        // ---------- Economy (도시/영토/기술/건설) ----------

        /// <summary>전투 시작 시 경제 상태를 만든다: 두 팀 시작 자원/빈 기술, 훈련 가능한 유닛 목록(배치 단계에서
        /// 불러온 유닛 CSV, 없으면 기본 유닛 표 Tables/Units.csv), 각 팀 수도(CitySystem.InitializeCapitals), 유닛 없이 시작한 팀의 시작 유닛(CitySystem.StartingUnitRequests).</summary>
        private void InitEconomy()
        {
            // 종족이 하나라도 정해졌으면 기술트리는 배열형 기술 표(Resources/Tables/Techs.csv — 기술 그룹 Index가 이 표를 가리킨다).
            bool tribeMode = AnyTribe();
            _econ = new EconomyWorld { TechNodes = tribeMode ? TechGroupSystem.BuildTechNodes() : CurrentTechNodes(), UnitRows = _unitRows ?? new List<UnitCsvRow>() };
            if (_econ.UnitRows.Count == 0) _econ.UnitRows = LoadFallbackUnitRows();
            foreach (var team in CitySystem.Teams)
            {
                var res = CityResourceData.Create(0, 0, false);
                res.Stars = GameRules.Economy.StartingStars;
                _econ.Resources[team] = res;
                _econ.Tech[team] = TechTreeData.CreateEmpty();
                // 종족: 시작 별 / 기술 그룹(연구 가능 목록) / 시작 기술 / 시작 유닛.
                if (TribeOf(team) is TribeRow tribe) TribeSystem.Apply(_econ, team, tribe, _econ.UnitRows);
            }
            if (tribeMode && _techTreeHud != null)
                _techTreeHud.SetNodes(TechGroupSystem.Filter(_econ.TechNodes, _econ.Tech[Team.Player].Allowed));
            TaskSystem.Init(_econ);
            // 시야(구름)는 경제가 켜진 전투에서만 쓴다: 수도 주변 5x5 + 유닛 주변 + 영토만 보인 채로 시작한다.
            _grid.FogEnabled = true;
            FoundTribeCapitals();
            CitySystem.InitializeCapitals(_grid, _world, _econ, includeUnitlessTeams: true);
            // 유닛 없이 시작한 팀은 수도에 기본 유닛(기술 없이 훈련 가능한 가장 싼 육지 유닛) 하나를 받는다.
            ProcessEconomyLog(CitySystem.StartingUnitRequests(_world, _econ));
            RefreshEconomyViews(false);
        }

        /// <summary>종족 모드로 생성한 지형이면 팀마다 자기 영역의 수도 자리(_tribeCapitals)를 수도로 세운다 — 시작 조건이 그 수도 주변에
        /// 적용됐으므로. 나머지 팀은 InitializeCapitals의 기본 규칙(유닛 무게중심에서 가장 가까운 수도)을 따른다.</summary>
        private void FoundTribeCapitals()
        {
            foreach (var kv in _tribeCapitals)
            {
                var pos = kv.Value;
                if (!_grid.InBounds(pos) || _grid.GetStructure(pos) != StructureGenerationSystem.CapitalStructureId) continue;
                if (CitySystem.FindCapital(_econ, kv.Key) >= 0 || CitySystem.FindCityAt(_econ, pos) >= 0) continue;
                var tribe = TribeOf(kv.Key);
                string name = tribe != null ? LocalizationSystem.F("UI.Common.TribeCapital", tribe.Value.Name)
                    : LocalizationSystem.T(kv.Key == Team.Player ? "UI.Common.AllyCapital" : "UI.Common.EnemyCapital");
                CitySystem.FoundCity(_grid, _econ, pos, kv.Key, true, name);
                VisionSystem.Reveal(_grid, kv.Key, pos, GameRules.Vision.StartRevealRadius);
            }
        }

        private static List<UnitCsvRow> LoadFallbackUnitRows() => new List<UnitCsvRow>(GameTables.Units);

        /// <summary>경제 상태가 바뀐 뒤 한 번에 다시 반영하는 곳: 생산량/유닛 수용량 재계산, 유닛 지형 제한/방어
        /// 보너스, 영토 색/건물/도시 원판, 숨겨진 자원, 자원 바/기술트리. terrainChanged면(벌목/숲 조성 등)
        /// 타일 색/숲 장식도 다시 그린다.</summary>
        private void RefreshEconomyViews(bool terrainChanged)
        {
            if (_econ == null) return;

            // 시야(유닛 주변/영토 밝히기, 등대 발견)와 과업 달성은 상태가 바뀔 때마다 다시 확인한다.
            var log = new List<EconomyLogEntry>();
            VisionSystem.Refresh(_grid, _world, _econ, log);
            TaskSystem.Refresh(_grid, _econ, log);
            if (log.Count > 0) ProcessEconomyLog(log);

            RefreshProduction();
            TechEffectSystem.RefreshUnits(_grid, _world, _econ);
            if (terrainChanged) _gridView.RefreshTerrain(_grid);
            _gridView.RefreshEconomy(_grid, _econ.Cities, BattleHud.PlayerAccent, BattleHud.EnemyAccent, _econ.Turn);
            _gridView.RefreshStructures(_grid, BuildStructurePrefabsById(), TechSystem.HiddenStructures(_econ.TechNodes, _econ.Tech[Team.Player]));
            _gridView.RefreshFog(_grid, Team.Player);
            RefreshUnitVisibility();
            RefreshCityResources();
            RefreshTechTree();
        }

        private void RefreshProduction()
        {
            if (_econ == null) return;
            foreach (var team in CitySystem.Teams)
                _econ.Resources[team] = CityResourceSystem.RefreshProduction(_econ.Resources[team], _grid, _world, _econ, team);
            RefreshCityResources();
        }

        /// <summary>플레이어에게 보이는 구조물 Id — 기술이 없어 숨겨진 자원이면 빈 문자열.</summary>
        private string VisibleStructure(Vector2Int pos)
        {
            string id = _grid.GetStructure(pos);
            if (!VisionSystem.IsExplored(_grid, Team.Player, pos)) return string.Empty;
            if (_econ != null && TechSystem.HiddenStructures(_econ.TechNodes, _econ.Tech[Team.Player]).Contains(id)) return string.Empty;
            return id;
        }

        /// <summary>도시를 가졌던 팀은 도시를 전부 잃으면, 도시가 없던 팀(또는 경제 없는 씬)은 유닛이 전멸하면 패배.</summary>
        private bool HasTeamLost(Team team)
        {
            if (_econ != null && _econ.HadCity.Contains(team)) return CitySystem.HasLost(_econ, team);
            return !UnitQueries.AnyAlive(_world, team);
        }

        /// <summary>team이 점령할 수 있는 칸(중립 마을/수도 구조물 + 다른 팀 도시) — EnemyAI 이동 목표.</summary>
        private List<Vector2Int> CaptureTargets(Team team)
        {
            var targets = new List<Vector2Int>();
            for (int y = 0; y < _grid.Height; y++)
            for (int x = 0; x < _grid.Width; x++)
            {
                var p = new Vector2Int(x, y);
                if (!CitySystem.IsSettlementTile(_grid, p)) continue;
                int city = CitySystem.FindCityAt(_econ, p);
                if (city < 0 || _econ.Cities[city].Owner != team) targets.Add(p);
            }
            return targets;
        }

        /// <summary>경제 기록을 행동 로그에 옮기고, 유닛 스폰이 필요한 기록(훈련/슈퍼 유닛/유적 유닛)은 실제로
        /// 스폰한다(Systems는 View를 몰라 스폰을 못 하므로 여기서 마무리).</summary>
        private void ProcessEconomyLog(List<EconomyLogEntry> log)
        {
            foreach (var entry in log)
            {
                if (!string.IsNullOrEmpty(entry.SpawnUnitId)) SpawnEconomyUnit(entry.Team, entry.SpawnUnitId, entry.Position, entry.CityIndex, entry.SpawnVeteran, entry.SpawnBoatId);
                _hud.AddLogEntry(FormatEconomyEntry(entry));
            }
            if (log.Count > 0) RefreshRoster();
        }

        private string FormatEconomyEntry(EconomyLogEntry e)
        {
            var accent = e.Team == Team.Player ? BattleHud.PlayerAccent : BattleHud.EnemyAccent;
            string who = $"<color=#{ColorUtility.ToHtmlStringRGB(accent)}>{TeamName(e.Team)}</color>";
            switch (e.Kind)
            {
                case EconomyLogKind.Capture: return LocalizationSystem.F("UI.Log.Capture", who, e.Subject);
                case EconomyLogKind.Research: return LocalizationSystem.F("UI.Log.Research", who, e.Subject);
                case EconomyLogKind.Build: return LocalizationSystem.F("UI.Log.Build", who, e.Subject);
                case EconomyLogKind.Train: return LocalizationSystem.F("UI.Log.Train", who, e.Subject);
                case EconomyLogKind.LevelUp:
                    return e.CityIndex >= 0 ? LocalizationSystem.F("UI.Log.LevelUpAt", who, e.Subject, _econ.Cities[e.CityIndex].Level) : LocalizationSystem.F("UI.Log.LevelUp", who, e.Subject);
                case EconomyLogKind.Reward: return LocalizationSystem.F("UI.Log.Reward", who, e.Subject);
                case EconomyLogKind.Explore: return LocalizationSystem.F("UI.Log.Explore", who, e.Subject);
                case EconomyLogKind.Disband: return LocalizationSystem.F("UI.Log.Disband", who, e.Subject);
                case EconomyLogKind.Task: return LocalizationSystem.F("UI.Log.Task", who, e.Subject);
                case EconomyLogKind.Upgrade: return LocalizationSystem.F("UI.Log.Upgrade", who, e.Subject);
                case EconomyLogKind.StartUnit: return LocalizationSystem.F("UI.Log.StartUnit", who, e.Subject);
                default: return LocalizationSystem.F("UI.Log.Other", who, e.Subject); // Action, Discover
            }
        }

        /// <summary>유닛 CSV Id로 near 칸(차 있으면 가장 가까운 빈 칸, 반경 3)에 유닛을 스폰한다. 폴리토피아처럼
        /// 새로 생긴 유닛은 그 턴에 움직이거나 행동할 수 없다.</summary>
        private bool SpawnEconomyUnit(Team team, string unitId, Vector2Int near, int homeCity, bool veteran = false, string boatId = null)
        {
            var row = CitySystem.FindUnitRow(_econ, unitId);
            if (row == null || !BasePrefabsByName().TryGetValue(row.BaseVisual, out var basePrefab) || basePrefab == null)
            {
                Debug.LogWarning($"[BattleController] 유닛 '{unitId}'를 스폰할 수 없습니다(CSV 행 또는 BaseVisual 프리팹 없음).");
                return false;
            }

            // 엔티티 생성/자리 찾기는 헤드리스 시뮬레이션과 같은 경로(UnitFactorySystem), 여기서는 View만 붙인다.
            int id = UnitFactorySystem.SpawnEconomyUnit(_grid, _world, _econ, team, unitId, near, homeCity, veteran, boatId);
            if (id < 0) return false;
            var view = _spawner.AttachCsvView(_grid, _world, id, basePrefab, row);
            _viewsById[id] = view;
            TechEffectSystem.RefreshUnits(_grid, _world, _econ);
            view.Refresh(_world, id);
            return true;
        }

        private bool CanUseEconomyMenu => _econ != null && _actionMenu != null && !_battleOver && _turnState.ActiveTeam == Team.Player;

        /// <summary>메뉴 버튼으로 무언가를 한 뒤 같은 대상의 메뉴를 새 상태로 다시 그린다.</summary>
        private void RefreshMenu()
        {
            if (!CanUseEconomyMenu) return;
            if (_state == SelectState.UnitSelected && _selectedUnitId >= 0) ShowUnitMenu(_selectedUnitId);
            else if (_menuTile.HasValue) ShowTileMenu(_menuTile.Value);
        }

        /// <summary>칸 메뉴: 우리 도시면 레벨업 보상 선택 + 유닛 훈련, 그리고 그 칸에서 할 수 있는 채집/건설 목록
        /// (TileImprovementSystem.GetOptions). 보여줄 것이 없으면 메뉴를 닫는다.</summary>
        private void ShowTileMenu(Vector2Int pos)
        {
            if (!CanUseEconomyMenu) return;
            if (!VisionSystem.IsExplored(_grid, Team.Player, pos)) { _actionMenu.Hide(); return; }
            _menuTile = pos;
            var options = new List<ActionMenuOption>();
            string title;
            var body = new System.Text.StringBuilder();
            var tile = _grid.GetTile(pos);

            int cityIndex = CitySystem.FindCityAt(_econ, pos);
            if (cityIndex >= 0)
            {
                var city = _econ.Cities[cityIndex];
                title = LocalizationSystem.F("UI.CityMenu.Title", city.Name, city.Level);
                body.Append(LocalizationSystem.F("UI.CityMenu.Stats", LocalizationSystem.T(city.Owner == Team.Player ? "UI.CityMenu.AllyCity" : "UI.CityMenu.EnemyCity"),
                    city.Population, city.Level + 1, CitySystem.SupportedUnits(_world, cityIndex), CitySystem.CityCapacity(city), CitySystem.CityStarsIncome(_grid, _world, city))).Append('\n');
                var traits = new List<string> { LocalizationSystem.F("UI.CityMenu.Border", city.BorderRadius) };
                if (city.IsCapital) traits.Add(LocalizationSystem.T("UI.CityMenu.Capital"));
                if (city.ConnectedToCapital) traits.Add(LocalizationSystem.T("UI.CityMenu.Connected"));
                if (city.HasWorkshop) traits.Add(LocalizationSystem.T("UI.CityMenu.Workshop"));
                if (city.HasWall) traits.Add(LocalizationSystem.T("UI.CityMenu.Wall"));
                if (city.ParkCount > 0) traits.Add(LocalizationSystem.F("UI.CityMenu.Parks", city.ParkCount));
                if (city.HasEmbassy) traits.Add(LocalizationSystem.F("UI.CityMenu.Embassy", string.Join(" / ",
                    _econ.Embassies.Where(e => e.CityIndex == cityIndex).Select(e =>
                        LocalizationSystem.T(e.Sender == Team.Player ? "UI.CityMenu.AllyCity" : "UI.CityMenu.EnemyCity")))));
                body.Append(string.Join(" · ", traits));

                if (city.Owner == Team.Player && city.IsCapital)
                {
                    body.Append('\n').Append(LocalizationSystem.F("UI.CityMenu.Tasks", string.Join(" · ", TaskDefinition.All
                        .Where(t => TaskSystem.IsUnlocked(_econ, Team.Player, t))
                        .Select(t => $"{t.Name} {TaskSystem.ProgressText(_grid, _econ, Team.Player, t)}"))));
                }

                if (city.Owner == Team.Player)
                {
                    if (city.PendingRewards > 0)
                    {
                        int rewardLevel = CitySystem.PendingRewardLevel(city);
                        foreach (var reward in CitySystem.RewardOptions(rewardLevel))
                        {
                            var r = reward;
                            options.Add(new ActionMenuOption
                            {
                                Label = LocalizationSystem.F("UI.CityMenu.Reward", rewardLevel, CitySystem.RewardName(r)), Detail = CitySystem.RewardDescription(r), Enabled = true,
                                OnClick = () => HandleReward(cityIndex, r)
                            });
                        }
                    }
                    foreach (var row in _econ.UnitRows)
                    {
                        if (!CitySystem.IsTrainable(row) || !TechSystem.CanTrainUnitType(_econ.TechNodes, _econ.Tech[Team.Player], row.Id)) continue;
                        bool can = CitySystem.CanTrain(_grid, _world, _econ, Team.Player, cityIndex, row, out var reason);
                        var r = row;
                        options.Add(new ActionMenuOption
                        {
                            Label = LocalizationSystem.F("UI.CityMenu.Train", row.Name, row.Cost),
                            Detail = can ? LocalizationSystem.F("UI.CityMenu.TrainDetail", row.MaxHp, row.AttackAttack, row.MoveRange) : reason,
                            Enabled = can, OnClick = () => HandleTrain(cityIndex, r)
                        });
                    }
                }
            }
            else
            {
                var cls = TileImprovementSystem.Classify(_grid, pos);
                string owner = LocalizationSystem.T(tile.OwnerTeam == (int)Team.Player ? "UI.TileMenu.AllyTerritory"
                    : tile.OwnerTeam == (int)Team.Enemy ? "UI.TileMenu.EnemyTerritory" : "UI.TileMenu.Neutral");
                title = LocalizationSystem.F("UI.TileMenu.Title", TileClassName(cls), pos.x, pos.y);
                body.Append(owner);
                var building = TileImprovementSystem.FindBuilding(tile.BuildingId);
                if (building != null)
                {
                    body.Append(" · ").Append(LocalizationSystem.F("UI.TileMenu.Building", building.Value.Name, TileImprovementSystem.BuildingPopulation(_grid, pos)));
                    if (building.Value.IsTemple)
                    {
                        int level = ScoreSystem.TempleLevel(_econ.Turn, tile.BuildingTurn);
                        body.Append(" · ").Append(LocalizationSystem.F("UI.TileMenu.Temple", level, ScoreSystem.TemplePoints(level)));
                    }
                }
                if (tile.HasRoad) body.Append(" · ").Append(LocalizationSystem.Name(ArrayTableCsvSerializer.BuildingStringTable, BuildingDefinition.Road));
            }

            foreach (var option in TileImprovementSystem.GetOptions(_grid, _econ, Team.Player, pos))
            {
                var o = option;
                options.Add(new ActionMenuOption
                {
                    Label = o.Cost > 0 ? LocalizationSystem.F("UI.TileMenu.OptionCost", o.Name, o.Cost) : o.Name, Detail = o.Detail, Enabled = o.Enabled,
                    OnClick = () => HandleTileOption(pos, o.Id)
                });
            }

            if (cityIndex < 0 && options.Count == 0 && string.IsNullOrEmpty(tile.BuildingId) && !tile.HasRoad)
            {
                _actionMenu.Hide();
                return;
            }
            _actionMenu.Show(title, body.ToString(), options);
        }

        private bool IsOwnCity(Vector2Int pos)
        {
            int city = CitySystem.FindCityAt(_econ, pos);
            return city >= 0 && _econ.Cities[city].Owner == Team.Player;
        }

        private static string TileClassName(TileClass cls)
        {
            switch (cls)
            {
                case TileClass.Forest: return LocalizationSystem.T("UI.Tile.Forest");
                case TileClass.Mountain: return LocalizationSystem.T("UI.Tile.Mountain");
                case TileClass.ShallowWater: return LocalizationSystem.T("UI.Tile.ShallowWater");
                case TileClass.Ocean: return LocalizationSystem.T("UI.Tile.Ocean");
                default: return LocalizationSystem.T("UI.Tile.Field");
            }
        }

        /// <summary>유닛 메뉴: 점령/유적 탐험/해산, 그리고 우리 도시 위라면 그 도시 메뉴로 가는 버튼.</summary>
        private void ShowUnitMenu(int unitId)
        {
            if (!CanUseEconomyMenu) return;
            var options = new List<ActionMenuOption>();
            var pos = _world.Get<GridPosition>(unitId).Value;

            if (CitySystem.CanCapture(_grid, _world, _econ, unitId))
                options.Add(new ActionMenuOption { Label = LocalizationSystem.T("UI.UnitMenu.Capture"), Detail = LocalizationSystem.T("UI.UnitMenu.CaptureDetail"), Enabled = true, OnClick = () => HandleCapture(unitId) });
            else if (CitySystem.IsSettlementTile(_grid, pos) && !IsOwnCity(pos))
                options.Add(new ActionMenuOption { Label = LocalizationSystem.T("UI.UnitMenu.Capture"), Detail = LocalizationSystem.T("UI.UnitMenu.CaptureWait"), Enabled = false });

            if (VeteranSystem.CanPromote(_world, unitId))
                options.Add(new ActionMenuOption { Label = LocalizationSystem.F("UI.UnitMenu.Promote", GameRules.Veteran.MaxHpBonus), Detail = LocalizationSystem.T("UI.UnitMenu.PromoteDetail"), Enabled = true, OnClick = () => HandlePromote(unitId) });
            if (RuinSystem.CanExplore(_grid, _world, _econ, unitId))
                options.Add(new ActionMenuOption { Label = LocalizationSystem.T("UI.UnitMenu.Explore"), Detail = LocalizationSystem.T("UI.UnitMenu.ExploreDetail"), Enabled = true, OnClick = () => HandleExplore(unitId) });
            if (RuinSystem.CanHarvestStarfish(_grid, _world, _econ, unitId))
                options.Add(new ActionMenuOption { Label = LocalizationSystem.F("UI.UnitMenu.Starfish", GameRules.Starfish.Stars), Detail = LocalizationSystem.T("UI.UnitMenu.StarfishDetail"), Enabled = true, OnClick = () => HandleStarfish(unitId) });
            if (EmbarkSystem.NavalUnitId(_world, unitId) == NavalUnitDefinition.RaftId)
            {
                foreach (var u in NavalUnitDefinition.Upgrades)
                {
                    if (!TechSystem.HasUnlock(_econ.TechNodes, _econ.Tech[Team.Player], u.UnlockKey)) continue;
                    bool can = EmbarkSystem.CanUpgrade(_grid, _world, _econ, unitId, u.Row.Id, out var reason);
                    string navalId = u.Row.Id;
                    options.Add(new ActionMenuOption
                    {
                        Label = LocalizationSystem.F("UI.UnitMenu.Upgrade", u.Row.Name, u.Row.Cost),
                        Detail = can ? LocalizationSystem.F("UI.UnitMenu.UpgradeDetail", u.Row.AttackAttack, u.Row.Defense, u.Row.MoveRange, u.Row.AttackRange) : reason,
                        Enabled = can, OnClick = () => HandleNavalUpgrade(unitId, navalId)
                    });
                }
            }

            if (RuinSystem.CanDisband(_world, _econ, unitId))
                options.Add(new ActionMenuOption { Label = LocalizationSystem.F("UI.UnitMenu.Disband", RuinSystem.DisbandRefund(_world, _econ, unitId)), Detail = LocalizationSystem.T("UI.UnitMenu.DisbandDetail"), Enabled = true, OnClick = () => HandleDisband(unitId) });

            int city = CitySystem.FindCityAt(_econ, pos);
            if (city >= 0 && _econ.Cities[city].Owner == Team.Player)
                options.Add(new ActionMenuOption { Label = LocalizationSystem.T("UI.UnitMenu.ManageCity"), Detail = _econ.Cities[city].Name, Enabled = true, OnClick = () => FocusTile(pos) });

            // 소속 도시(위키 City "Units will show which city they belong to").
            int home = CitySystem.HomeOf(_world, unitId);
            string homeText = home >= 0 && home < _econ.Cities.Count ? LocalizationSystem.F("UI.UnitMenu.Home", _econ.Cities[home].Name) : LocalizationSystem.T("UI.UnitMenu.NoHome");
            homeText += VeteranSystem.IsVeteran(_world, unitId) ? " · " + LocalizationSystem.T("UI.UnitMenu.Veteran")
                : VeteranSystem.CannotBePromoted(_world, unitId) ? string.Empty
                : " · " + LocalizationSystem.F("UI.UnitMenu.Kills", _world.GetOrDefault<Kills>(unitId).Value, GameRules.Veteran.KillsRequired);
            _actionMenu.Show(_viewsById.TryGetValue(unitId, out var view) ? view.Label : LocalizationSystem.T("UI.Common.Unit"), homeText, options);
        }

        /// <summary>다음 언어로 바꿔 저장하고 씬을 다시 연다 — 표 이름·프리팹 글자·이미 만든 도시 이름까지 한 번에 바뀌도록(진행 중 전투는 처음부터).</summary>
        private void HandleLanguageClicked()
        {
            LanguagePreference.Save(LocalizationSystem.NextLanguage(StringTable.Language));
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }

        private static string TeamName(Team team) => LocalizationSystem.T(team == Team.Player ? "UI.Common.Player" : "UI.Common.Enemy");

        private void HandleNavalUpgrade(int unitId, string navalUnitId)
        {
            if (!CanUseEconomyMenu) return;
            var log = new List<EconomyLogEntry>();
            if (!EmbarkSystem.Upgrade(_grid, _world, _econ, unitId, navalUnitId, log)) return;
            ProcessEconomyLog(log);
            _viewsById[unitId].Refresh(_world, unitId);
            RefreshEconomyViews(false);
            RecomputeHighlightsIfSelected(unitId);
            RefreshMenu();
        }

        private void RecomputeHighlightsIfSelected(int unitId)
        {
            if (_state == SelectState.UnitSelected && _selectedUnitId == unitId) RecomputeHighlights();
        }

        private void HandleTileOption(Vector2Int pos, string optionId)
        {
            if (!CanUseEconomyMenu) return;
            var before = _grid.GetTileType(pos);
            var log = new List<EconomyLogEntry>();
            if (!TileImprovementSystem.Execute(_grid, _econ, Team.Player, pos, optionId, log)) return;
            ProcessEconomyLog(log);
            RefreshEconomyViews(before != _grid.GetTileType(pos));
            RefreshMenu();
        }

        private void HandleTrain(int cityIndex, UnitCsvRow row)
        {
            if (!CanUseEconomyMenu) return;
            if (!CitySystem.PayForTraining(_grid, _world, _econ, Team.Player, cityIndex, row)) return;
            ProcessEconomyLog(new List<EconomyLogEntry>
            {
                new EconomyLogEntry { Team = Team.Player, Kind = EconomyLogKind.Train, Subject = row.Name, SpawnUnitId = row.Id, Position = _econ.Cities[cityIndex].Position, CityIndex = cityIndex }
            });
            RefreshEconomyViews(false);
            RefreshMenu();
        }

        private void HandleReward(int cityIndex, CityRewardType reward)
        {
            if (!CanUseEconomyMenu) return;
            var log = new List<EconomyLogEntry>();
            if (!CitySystem.ApplyReward(_grid, _econ, cityIndex, reward, log)) return;
            ProcessEconomyLog(log);
            RefreshEconomyViews(false);
            RefreshMenu();
        }

        private void HandleCapture(int unitId)
        {
            if (!CanUseEconomyMenu) return;
            var log = new List<EconomyLogEntry>();
            if (CitySystem.Capture(_grid, _world, _econ, unitId, log) < 0) return;
            ProcessEconomyLog(log);
            _viewsById[unitId].Refresh(_world, unitId);
            RefreshEconomyViews(false);
            CheckBattleEnd();
            ClearSelection();
        }

        private void HandleExplore(int unitId)
        {
            if (!CanUseEconomyMenu) return;
            var log = new List<EconomyLogEntry>();
            if (!RuinSystem.Explore(_grid, _world, _econ, unitId, log)) return;
            ProcessEconomyLog(log);
            _viewsById[unitId].Refresh(_world, unitId);
            RefreshEconomyViews(false);
            ClearSelection();
        }

        private void HandlePromote(int unitId)
        {
            if (!CanUseEconomyMenu || !VeteranSystem.Promote(_world, unitId)) return;
            _hud.AddLogEntry(FormatLogEntry(new BattleLogEntry { ActorId = unitId, Verb = BattleLogVerb.Promote, TargetId = BattleLogEntry.NoTarget }));
            _viewsById[unitId].Refresh(_world, unitId);
            RefreshRoster();
            RecomputeHighlightsIfSelected(unitId);
            RefreshMenu();
        }

        private void HandleStarfish(int unitId)
        {
            if (!CanUseEconomyMenu) return;
            var log = new List<EconomyLogEntry>();
            if (!RuinSystem.HarvestStarfish(_grid, _world, _econ, unitId, log)) return;
            ProcessEconomyLog(log);
            _viewsById[unitId].Refresh(_world, unitId);
            RefreshEconomyViews(false);
            ClearSelection();
        }

        private void HandleDisband(int unitId)
        {
            if (!CanUseEconomyMenu) return;
            var log = new List<EconomyLogEntry>();
            if (!RuinSystem.Disband(_grid, _world, _econ, unitId, log)) return;
            ProcessEconomyLog(log);
            _viewsById[unitId].Refresh(_world, unitId);
            RefreshEconomyViews(false);
            CheckBattleEnd();
            ClearSelection();
        }

        // ---------- Selection / actions ----------

        private void SelectUnit(int unitId)
        {
            _selectedUnitId = unitId;
            _state = SelectState.UnitSelected;
            if (_hud != null) _hud.HideStructurePanel();
            RecomputeHighlights();
        }

        private void RecomputeHighlights()
        {
            _gridView.ClearHighlights();
            int unitId = _selectedUnitId;

            var available = _world.Get<AvailableActions>(unitId).Value;
            // 대기(회복)는 이번 턴 움직이지 않은 유닛만(위키 Recover — WaitAction.CanExecute).
            if (_world.Get<HasMoved>(unitId).Value) available &= ~ActionType.Wait;

            _reachableTiles = null;
            var move = UnitActionQueries.Find<MoveAction>(_world, unitId);
            if (move != null && move.CanExecute(_world, unitId))
            {
                var selfPos = _world.Get<GridPosition>(unitId).Value;
                PathfindingSystem.GetReachable(_grid, _world, selfPos, unitId, out var reachable);
                reachable.Remove(selfPos);
                _reachableTiles = reachable;
                _gridView.HighlightMove(_reachableTiles);
            }

            _attackableTargets = new List<int>();
            var attack = UnitActionQueries.Find<AttackAction>(_world, unitId);
            if (attack != null && attack.CanExecute(_world, unitId))
            {
                for (int enemyId = 0; enemyId < _world.EntityCount; enemyId++)
                {
                    if (!UnitQueries.IsAlive(_world, enemyId) || _world.Get<Team>(enemyId) == _world.Get<Team>(unitId)) continue;
                    if (CombatSystem.IsInAttackRange(_world, unitId, enemyId))
                        _attackableTargets.Add(enemyId);
                }
                _gridView.HighlightAttack(_attackableTargets.Select(id => _world.Get<GridPosition>(id).Value));
            }

            _hud.ShowUnitPanel(_world, unitId);
            _hud.SetDeselectVisible(true);
            _hud.SetUnitActions(available, _world.Get<HasActed>(unitId).Value);
        }

        private void MoveSelectedUnit(Vector2Int pos)
        {
            var hpBefore = SnapshotHp();
            if (!MovementSystem.TryMove(_grid, _world, _selectedUnitId, pos)) return;
            foreach (var entry in hpBefore)
                if (_viewsById.TryGetValue(entry.Key, out var view) && _world.Get<Hp>(entry.Key).Value < entry.Value)
                    view.ShowDamagePopup(entry.Value - _world.Get<Hp>(entry.Key).Value);
            RefreshAllViews();
            RefreshRoster();
            CheckBattleEnd();

            _hud.AddLogEntry(FormatLogEntry(new BattleLogEntry { ActorId = _selectedUnitId, Verb = BattleLogVerb.Move, TargetId = BattleLogEntry.NoTarget }));
            _viewsById[_selectedUnitId].Refresh(_world, _selectedUnitId);
            if (_econ != null)
            {
                // 이동으로 시야(구름)/지형 방어 보너스/도시 포위(수입) 상태가 바뀔 수 있다.
                RefreshEconomyViews(false);
            }
            RecomputeHighlights();
            ShowUnitMenu(_selectedUnitId);
            _focusPos = pos;
            _focusLayer = FocusLayer.Unit;
        }

        private void TryAttack(int attackerId, int targetId)
        {
            var aliveBefore = TaskSystem.SnapshotAlive(_world);
            if (!CombatSystem.TryAttack(_grid, _world, attackerId, targetId, out int damage, out int counterDamage)) return;
            if (_econ != null)
            {
                TaskSystem.RecordAttack(_econ, Team.Player);
                TaskSystem.RecordDeaths(_econ, _world, aliveBefore);
            }

            _hud.AddLogEntry(FormatLogEntry(new BattleLogEntry { ActorId = attackerId, Verb = BattleLogVerb.Attack, TargetId = targetId, Amount = damage }));
            _viewsById[targetId].ShowDamagePopup(damage);
            if (!UnitQueries.IsAlive(_world, targetId))
                _hud.AddLogEntry(FormatLogEntry(new BattleLogEntry { ActorId = targetId, Verb = BattleLogVerb.Defeated, TargetId = BattleLogEntry.NoTarget }));

            if (counterDamage > 0)
            {
                _hud.AddLogEntry(FormatLogEntry(new BattleLogEntry { ActorId = targetId, Verb = BattleLogVerb.Counter, TargetId = attackerId, Amount = counterDamage }));
                _viewsById[attackerId].ShowDamagePopup(counterDamage);
                if (!UnitQueries.IsAlive(_world, attackerId))
                    _hud.AddLogEntry(FormatLogEntry(new BattleLogEntry { ActorId = attackerId, Verb = BattleLogVerb.Defeated, TargetId = BattleLogEntry.NoTarget }));
            }

            _viewsById[attackerId].Refresh(_world, attackerId);
            _viewsById[targetId].Refresh(_world, targetId);
            _viewsById[attackerId].PlayAttackTowards(PixelCoordinates.GridToWorld(_grid, _world.Get<GridPosition>(targetId).Value));
            RefreshRoster();
            RefreshEconomyViews(false);

            CheckBattleEnd();
            ClearSelection();
        }

        /// <summary>BattleHud의 방어 태세 버튼 클릭 이벤트 핸들러.</summary>
        private void HandleDefendClicked()
        {
            if (_state != SelectState.UnitSelected) return;
            if (!CombatSystem.TryDefend(_grid, _world, _selectedUnitId)) return;

            _hud.AddLogEntry(FormatLogEntry(new BattleLogEntry { ActorId = _selectedUnitId, Verb = BattleLogVerb.Defend, TargetId = BattleLogEntry.NoTarget }));
            _viewsById[_selectedUnitId].Refresh(_world, _selectedUnitId);
            ClearSelection();
        }

        /// <summary>BattleHud의 치유 버튼 클릭 이벤트 핸들러. 대상 선택 없이 즉시 사거리 내
        /// 모든 아군(자신 제외)을 회복시킨다.</summary>
        private void HandleHealClicked()
        {
            if (_state != SelectState.UnitSelected) return;
            if (!AbilitySystem.TryHeal(_grid, _world, _selectedUnitId, out var healedIds)) return;

            _hud.AddLogEntry(FormatLogEntry(new BattleLogEntry { ActorId = _selectedUnitId, Verb = BattleLogVerb.Heal, TargetId = BattleLogEntry.NoTarget, Amount = healedIds.Count }));
            _viewsById[_selectedUnitId].Refresh(_world, _selectedUnitId);
            foreach (var id in healedIds)
                _viewsById[id].Refresh(_world, id);
            RefreshRoster();

            ClearSelection();
        }

        /// <summary>BattleHud의 자폭 버튼 클릭 이벤트 핸들러. 대상 선택 없이 즉시 자신을 제거하고
        /// 주위 1칸의 모든 적에게 피해를 입힌다.</summary>
        private void HandleSelfDestructClicked()
        {
            if (_state != SelectState.UnitSelected) return;
            int unitId = _selectedUnitId;
            var hpBefore = SnapshotHp();
            var aliveBefore = TaskSystem.SnapshotAlive(_world);
            if (!AbilitySystem.TrySelfDestruct(_grid, _world, unitId, out var damagedIds)) return;
            if (_econ != null)
            {
                TaskSystem.RecordAttack(_econ, Team.Player);
                TaskSystem.RecordDeaths(_econ, _world, aliveBefore);
            }

            _hud.AddLogEntry(FormatLogEntry(new BattleLogEntry { ActorId = unitId, Verb = BattleLogVerb.SelfDestruct, TargetId = BattleLogEntry.NoTarget, Amount = damagedIds.Count }));
            foreach (var id in damagedIds)
            {
                int before = hpBefore.TryGetValue(id, out var v) ? v : 0;
                int after = UnitQueries.IsAlive(_world, id) ? _world.Get<Hp>(id).Value : 0;
                int taken = before - after;
                if (taken > 0) _viewsById[id].ShowDamagePopup(taken);
                if (!UnitQueries.IsAlive(_world, id))
                    _hud.AddLogEntry(FormatLogEntry(new BattleLogEntry { ActorId = id, Verb = BattleLogVerb.Defeated, TargetId = BattleLogEntry.NoTarget }));
            }
            _hud.AddLogEntry(FormatLogEntry(new BattleLogEntry { ActorId = unitId, Verb = BattleLogVerb.Defeated, TargetId = BattleLogEntry.NoTarget }));

            _viewsById[unitId].Refresh(_world, unitId);
            foreach (var id in damagedIds)
                _viewsById[id].Refresh(_world, id);
            RefreshRoster();
            // 유닛이 사라지면 점수/시야/도시 포위(수입) 상태가 바뀐다.
            RefreshEconomyViews(false);

            CheckBattleEnd();
            ClearSelection();
        }

        /// <summary>BattleHud의 대기 버튼 클릭 이벤트 핸들러. 이번 턴 행동을 종료하고 체력을 회복한다.</summary>
        private void HandleWaitClicked()
        {
            if (_state != SelectState.UnitSelected) return;
            int unitId = _selectedUnitId;
            int hpBefore = _world.Get<Hp>(unitId).Value;
            if (!AbilitySystem.TryWait(_grid, _world, unitId)) return;

            int healed = _world.Get<Hp>(unitId).Value - hpBefore;
            _hud.AddLogEntry(FormatLogEntry(new BattleLogEntry { ActorId = unitId, Verb = BattleLogVerb.Wait, TargetId = BattleLogEntry.NoTarget, Amount = healed }));
            _viewsById[unitId].Refresh(_world, unitId);
            RefreshRoster();
            ClearSelection();
        }

        private void ClearSelection()
        {
            _state = SelectState.None;
            _selectedUnitId = -1;
            _reachableTiles = null;
            _attackableTargets = null;
            if (_gridView != null) _gridView.ClearHighlights();
            if (_actionMenu != null) _actionMenu.Hide();
            _menuTile = null;
            _focusPos = null;
            if (_hud != null)
            {
                _hud.HideUnitPanel();
                _hud.HideStructurePanel();
                _hud.SetDeselectVisible(false);
                _hud.SetUnitActions(ActionType.None, hasActed: true);
            }
        }
    }
}
