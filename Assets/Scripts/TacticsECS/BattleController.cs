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
        [Tooltip("도시 발전 자원(발전도/인구/골드/신앙) 표시 바 프리팹. 비워두면 생성 자체를 건너뛴다 — " +
            "지금은 커스텀(샌드박스 배치) 화면에만 연결돼 있다. Assets/Prefabs/UI/CityResourceBar.prefab.")]
        [SerializeField] private CityResourceHud cityResourceHudPrefab;
        [Tooltip("이 도시가 보유할 수 있는 유닛 총 수량(인구 상한).")]
        [SerializeField] private int cityPopulationCap = 10;
        [Tooltip("매 턴 시작 시 자동 생산되는 골드량(수도 보너스는 별도). 실제로는 도시 타일/건물 생산량의 " +
            "합이어야 하지만 타일 시스템이 없어 고정값 플레이스홀더로 둔다.")]
        [SerializeField] private int cityGoldProduction = 1;
        [Tooltip("매 턴 시작 시 자동 생산되는 도시 발전도(기술트리 해금에 쓰인다). 실제로는 수도와 연결된 " +
            "도시 수에 비례해야 하지만 영토/도로 시스템이 없어 고정값 플레이스홀더로 둔다 " +
            "(CityResourceSystem.IsConnectedToCapital 참고).")]
        [SerializeField] private int cityDevelopmentProduction = 1;
        [Tooltip("신앙 최대 보유량.")]
        [SerializeField] private int cityMaxFaith = 10;
        [Tooltip("최초 시작 도시(수도) 여부 — 켜면 골드 생산 +1 보너스가 붙는다(피점령 시 일반 도시 취급하는 " +
            "규칙은 점령 개념이 없어 아직 반영하지 않는다).")]
        [SerializeField] private bool cityIsCapital = true;
        [Tooltip("기술트리 패널 프리팹. 비워두면 생성 자체를 건너뛴다 — cityResourceHudPrefab과 같은 블록 " +
            "에서만 초기화된다(도시 발전도가 있어야 의미가 있으므로). Assets/Prefabs/UI/TechTreePanel.prefab.")]
        [SerializeField] private TechTreeHud techTreeHudPrefab;

        [Header("Camera (Isometric)")]
        [Tooltip("Y축(수평) 회전. 45도면 그리드 대각선 방향에서 바라보는 전형적인 isometric 구도.")]
        [SerializeField] private float isoYawDegrees = 45f;
        [Tooltip("X축(피치) 회전. 35.264도가 수학적으로 정확한 isometric 각도(atan(1/sqrt(2))).")]
        [SerializeField] private float isoPitchDegrees = 35.264f;
        [Tooltip("그리드를 화면에 얼마나 꽉 채울지. 값이 작을수록 확대된다.")]
        [SerializeField] private float isoZoom = 0.62f;

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

        private SandboxHud _sandboxHud;
        private UnitPlacementController _placementController;
        private bool _placementActive;
        private List<BiomeCsvRow> _loadedBiomes;

        /// <summary>Polytopia의 6종 맵 크기 프리셋(이름, 정사각형 한 변 길이)을 그대로 채택
        /// (docs/PolytopiaMapGeneration.md 1절) — Sandbox의 "맵 크기" 버튼이 이 목록을 순환한다.</summary>
        private static readonly (string Name, int Size)[] MapSizePresets =
        {
            ("Tiny", 11), ("Small", 14), ("Normal", 16), ("Large", 18), ("Huge", 20), ("Massive", 30)
        };
        private int _selectedMapSizeIndex;

        /// <summary>Polytopia의 6종 습도(맵 타입) 프리셋 이름 + 대표 습도값(docs/PolytopiaMapGeneration.md
        /// 2절 범위의 중간값). CSV에는 저장하지 않는 전역 생성 파라미터 — 맵 크기와 같은 이유로 Sandbox
        /// UI 순환 버튼으로만 선택한다. Continents(0.55)를 배율 1.0의 기준으로 삼는다
        /// (TerrainGenerationSystem.Generate의 wetnessMultiplier = 선택값 / Continents값).</summary>
        private static readonly (string Name, float Wetness)[] WetnessPresets =
        {
            ("Drylands", 0.05f), ("Lakes", 0.275f), ("Continents", 0.55f), ("Pangea", 0.50f), ("Archipelago", 0.70f), ("Waterworld", 0.95f)
        };
        private const int WetnessBaselineIndex = 2; // "Continents"
        private int _selectedWetnessIndex = WetnessBaselineIndex;

        private CityResourceHud _cityResourceHud;
        private CityResourceData _playerCity;

        private TechTreeHud _techTreeHud;
        private TechTreeData _playerTech;

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
        private int _selectedUnitId = -1;
        private HashSet<Vector2Int> _reachableTiles;
        private List<int> _attackableTargets;

        private bool _battleOver;

        private void Awake()
        {
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

            if (cityResourceHudPrefab != null)
            {
                _cityResourceHud = Instantiate(cityResourceHudPrefab, transform);
                _cityResourceHud.name = "CityResourceHud";
                _cityResourceHud.Init();
                _playerCity = CityResourceData.Create(cityPopulationCap, cityGoldProduction, cityDevelopmentProduction, cityMaxFaith, cityIsCapital);
                RefreshCityResources();

                if (techTreeHudPrefab != null)
                {
                    _techTreeHud = Instantiate(techTreeHudPrefab, transform);
                    _techTreeHud.name = "TechTreeHud";
                    _techTreeHud.Init();
                    _techTreeHud.OnUnlockRequested += HandleTechUnlockRequested;
                    _playerTech = TechTreeData.CreateEmpty();
                    RefreshTechTree();
                }
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
            _cityResourceHud = null;
            _techTreeHud = null;

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

            var basePrefabsByName = new Dictionary<string, UnitView>
            {
                ["Melee"] = meleePrefab,
                ["Ranged"] = rangedPrefab,
                ["Guard"] = guardPrefab,
                ["RogueHooded"] = rogueHoodedPrefab,
                ["Mage"] = magePrefab,
                ["SkeletonWarrior"] = skeletonWarriorPrefab,
                ["SkeletonMage"] = skeletonMagePrefab
            };
            _placementController = new UnitPlacementController(_grid, _world, _spawner, basePrefabsByName, _viewsById);

            _sandboxHud = Instantiate(sandboxHudPrefab, transform);
            _sandboxHud.name = "SandboxHud";
            _sandboxHud.Init();
            _sandboxHud.OnLoadClicked += HandleSandboxLoad;
            _sandboxHud.OnExportClicked += HandleSandboxExport;
            _sandboxHud.OnUnitSelected += HandleSandboxUnitSelected;
            _sandboxHud.OnTeamSelected += HandleSandboxTeamSelected;
            _sandboxHud.OnStartBattleClicked += HandleSandboxStartBattle;
            _sandboxHud.OnLoadBiomeClicked += HandleBiomeLoad;
            _sandboxHud.OnGenerateTerrainClicked += HandleGenerateTerrain;
            _sandboxHud.OnMapSizeCycleClicked += HandleMapSizeCycle;
            _sandboxHud.OnWetnessCycleClicked += HandleWetnessCycle;
            _sandboxHud.SetSelectedTeam(Team.Player);
            _sandboxHud.SetStatus("\"불러오기\"로 CSV 파일을 선택해 배치를 시작하세요.");

            InitMapSizeSelection();
            _sandboxHud.SetMapSizeLabel(MapSizePresets[_selectedMapSizeIndex].Name, MapSizePresets[_selectedMapSizeIndex].Size);
            _sandboxHud.SetWetnessLabel(WetnessPresets[_selectedWetnessIndex].Name);
        }

        private void HandleWetnessCycle()
        {
            _selectedWetnessIndex = (_selectedWetnessIndex + 1) % WetnessPresets.Length;
            _sandboxHud.SetWetnessLabel(WetnessPresets[_selectedWetnessIndex].Name);
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
            _sandboxHud.SetMapSizeLabel(preset.Name, preset.Size);
        }

        private void HandleSandboxLoad(string path)
        {
            try
            {
                var csvText = System.IO.File.ReadAllText(path);
                var rows = UnitCsvSerializer.Parse(csvText);
                _placementController.SetRows(rows);
                _sandboxHud.SetPalette(rows);
                _sandboxHud.SetSelectedUnit(rows.Count > 0 ? 0 : -1);
                _sandboxHud.SetStatus($"{rows.Count}개 유닛을 불러왔습니다. 팔레트에서 골라 빈 칸을 클릭하세요.");
            }
            catch (System.Exception e)
            {
                _sandboxHud.SetStatus($"불러오기 실패: {e.Message}");
            }
        }

        private void HandleSandboxExport(string path)
        {
            try
            {
                var csvText = UnitCsvSerializer.Write(_placementController.Rows);
                System.IO.File.WriteAllText(path, csvText);
                _sandboxHud.SetStatus($"{_placementController.Rows.Count}개 유닛을 {path}에 내보냈습니다.");
            }
            catch (System.Exception e)
            {
                _sandboxHud.SetStatus($"내보내기 실패: {e.Message}");
            }
        }

        /// <summary>바이옴 CSV를 불러오기만 한다 — 실제 지형 생성은 "지형 생성" 버튼(HandleGenerateTerrain)을
        /// 눌러야 실행된다(불러오기와 생성을 분리해, 같은 바이옴 목록으로 여러 번 재생성해볼 수 있게).</summary>
        private void HandleBiomeLoad(string path)
        {
            try
            {
                var csvText = System.IO.File.ReadAllText(path);
                _loadedBiomes = BiomeCsvSerializer.Parse(csvText);
                _sandboxHud.SetStatus($"{_loadedBiomes.Count}개 바이옴을 불러왔습니다. \"지형 생성\"을 눌러 지형을 만드세요.");
            }
            catch (System.Exception e)
            {
                _sandboxHud.SetStatus($"바이옴 불러오기 실패: {e.Message}");
            }
        }

        /// <summary>불러온 바이옴 목록으로 그리드를 다시 채운다. 선택된 맵 크기 프리셋이 지금 그리드와
        /// 다르면 먼저 그리드 자체를 다시 만든다(RebuildGridForSize). 이미 유닛이 놓인 칸은
        /// TerrainGenerationSystem이 알아서 건드리지 않으므로(크기가 그대로라면) 배치 중에 눌러도
        /// 안전하다. 매번 새 시드를 뽑아서, 같은 바이옴 CSV로도 누를 때마다 다른 결과가 나오게 한다.
        /// 지형 생성 직후 반환되는 바이옴 앵커로 StructureGenerationSystem(수도/유적/자원/불가사리)까지
        /// 이어서 실행한다. 습도 프리셋 이름이 Pangea/Lakes/Continents/Archipelago/Waterworld 중 하나면
        /// TerrainGenerationSystem이 완전히 다른 경로(랜드마스 마스크로 모양 자체를 확정)를 타도록
        /// ResolveShapeMode로 매핑한 MapShapeMode를 함께 넘긴다. Drylands는 물이 거의 없어(목표 0~10%)
        /// 마스크 없이도 기존 방식으로 충분해 Freeform(습도 배율)만 쓴다.</summary>
        private void HandleGenerateTerrain()
        {
            if (_loadedBiomes == null || _loadedBiomes.Count == 0)
            {
                _sandboxHud.SetStatus("먼저 \"바이옴 불러오기\"로 바이옴 CSV를 불러오세요.");
                return;
            }

            int targetSize = MapSizePresets[_selectedMapSizeIndex].Size;
            if (targetSize != _grid.Width && !RebuildGridForSize(targetSize))
                return;

            var selectedWetness = WetnessPresets[_selectedWetnessIndex];
            var shapeMode = ResolveShapeMode(selectedWetness.Name);
            float wetnessMultiplier = selectedWetness.Wetness / WetnessPresets[WetnessBaselineIndex].Wetness;
            int seed = System.Environment.TickCount;
            var anchors = TerrainGenerationSystem.Generate(_grid, _loadedBiomes, seed, wetnessMultiplier, shapeMode, selectedWetness.Wetness);
            _gridView.RefreshTerrain(_grid);

            StructureGenerationSystem.Generate(_grid, _loadedBiomes, anchors, seed);
            _gridView.RefreshStructures(_grid, BuildStructurePrefabsById());

            _sandboxHud.SetStatus($"지형을 새로 생성했습니다 (바이옴 {_loadedBiomes.Count}개, {_grid.Width}x{_grid.Height}, " +
                $"습도={WetnessPresets[_selectedWetnessIndex].Name}, seed={seed}).");
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
                _sandboxHud.SetStatus("맵 크기를 바꾸려면 먼저 배치된 유닛을 모두 치우세요.");
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

            _turnState = TurnSystem.EndTurn(_world, _turnState);
            HandleTurnStart(_turnState.ActiveTeam, _turnState.TurnNumber);
        }

        private void SpawnDemoFormation()
        {
            SpawnUnit(Team.Player, guardPrefab, new Vector2Int(1, 1), "방패병");
            SpawnUnit(Team.Player, meleePrefab, new Vector2Int(1, 3), "근접 전사");
            SpawnUnit(Team.Player, rangedPrefab, new Vector2Int(0, 5), "궁수");
            SpawnUnit(Team.Player, meleePrefab, new Vector2Int(1, 6), "근접 전사");

            SpawnUnit(Team.Enemy, guardPrefab, new Vector2Int(gridWidth - 2, gridHeight - 2), "방패병");
            SpawnUnit(Team.Enemy, meleePrefab, new Vector2Int(gridWidth - 2, gridHeight - 4), "근접 전사");
            SpawnUnit(Team.Enemy, rangedPrefab, new Vector2Int(gridWidth - 1, gridHeight - 6), "궁수");
            SpawnUnit(Team.Enemy, meleePrefab, new Vector2Int(gridWidth - 2, gridHeight - 7), "근접 전사");

            if (stressTestExtraUnitsPerTeam > 0)
                SpawnStressTestUnits(stressTestExtraUnitsPerTeam);
        }

        private void SpawnStressTestUnits(int perTeam)
        {
            var prefabs = new[] { meleePrefab, rangedPrefab, guardPrefab };
            var labels = new[] { "근접 전사", "궁수", "방패병" };
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
        /// 카메라를 정통 isometric 구도(대각선 45도 + 피치 35.264도)로 배치한다.
        /// 원근 대신 orthographic을 사용해 거리에 따른 크기 왜곡이 없도록 한다.
        /// </summary>
        private void PositionCamera()
        {
            var center = _grid.GridToWorld(new Vector2Int(gridWidth / 2, gridHeight / 2));
            float span = Mathf.Max(gridWidth, gridHeight) * tileSize;

            _cameraRotation = Quaternion.Euler(isoPitchDegrees, isoYawDegrees, 0f);
            _cameraDistance = span * 1.5f;
            _cameraFocus = center;
            // 카메라가 바라보는 평면(그리드 바닥) 기준 좌/우, 앞/뒤 방향. 팬 입력을 여기에 투영한다.
            _cameraRight = Vector3.ProjectOnPlane(_cameraRotation * Vector3.right, Vector3.up).normalized;
            _cameraForwardFlat = Vector3.ProjectOnPlane(_cameraRotation * Vector3.forward, Vector3.up).normalized;

            _cam.orthographic = true;
            _cam.orthographicSize = Mathf.Clamp(span * isoZoom, minOrthoSize, maxOrthoSize);
            ApplyCameraTransform();
        }

        private void ApplyCameraTransform()
        {
            _cam.transform.rotation = _cameraRotation;
            _cam.transform.position = _cameraFocus - _cameraRotation * Vector3.forward * _cameraDistance;
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
            _cameraFocus.z = Mathf.Clamp(_cameraFocus.z, minZ, maxZ);
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
                    _cam.orthographicSize = Mathf.Clamp(
                        _cam.orthographicSize - scroll * zoomSensitivity,
                        minOrthoSize, maxOrthoSize);
                }
            }
        }

        // ---------- Turn flow ----------

        private void HandleTurnStart(Team team, int turnNumber)
        {
            _hud.SetTurn(team, turnNumber);
            _hud.SetEndTurnVisible(team == Team.Player && !_battleOver);
            ClearSelection();

            if (team == Team.Player && _cityResourceHud != null)
            {
                _playerCity = CityResourceSystem.ApplyTurnStart(_playerCity, _world, Team.Player);
                RefreshCityResources();
                // 발전도가 늘어나 해금 가능 여부/버튼 활성화가 바뀔 수 있으므로 같이 갱신한다.
                if (_techTreeHud != null) RefreshTechTree();
            }

            if (team == Team.Enemy && !_battleOver)
                StartCoroutine(RunEnemyTurnRoutine());
        }

        /// <summary>RefreshRoster와 같은 방식 — 인구 사용량(populationUsed)은 CityResourceData가 아니라
        /// EntityWorld 쪽 값이라 매번 다시 세어서 넘긴다.</summary>
        private void RefreshCityResources()
        {
            int populationUsed = CityResourceSystem.CountPopulation(_world, Team.Player);
            _cityResourceHud.SetResources(_playerCity, populationUsed);
        }

        private void RefreshTechTree()
        {
            _techTreeHud.SetState(_playerTech, _playerCity);
        }

        /// <summary>TechTreeHud.OnUnlockRequested 핸들러. 실제 해금 판정/발전도 소모는 TechSystem이 계산하고,
        /// 결과를 두 HUD 모두에 다시 반영한다(발전도가 줄어드므로 CityResourceHud도 함께).</summary>
        private void HandleTechUnlockRequested(TechId id)
        {
            _playerTech = TechSystem.Unlock(_playerTech, _playerCity, id, out _playerCity);
            RefreshTechTree();
            RefreshCityResources();
        }

        private IEnumerator RunEnemyTurnRoutine()
        {
            yield return new WaitForSeconds(0.3f);
            var entries = EnemyAI.RunTurn(_grid, _world);
            RefreshAllViews();
            RefreshRoster();

            foreach (var entry in entries)
            {
                if (entry.Verb == BattleLogVerb.Attack)
                    _viewsById[entry.ActorId].FaceTowards(_grid.GridToWorld(_world.Get<GridPosition>(entry.TargetId).Value));
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
                case BattleLogVerb.Move: return $"{Colored(entry.ActorId)} 이동";
                case BattleLogVerb.Attack: return $"{Colored(entry.ActorId)} 공격 → {Colored(entry.TargetId)} ({entry.Amount})";
                case BattleLogVerb.Counter: return $"{Colored(entry.ActorId)} 반격 → {Colored(entry.TargetId)} ({entry.Amount})";
                case BattleLogVerb.Defend: return $"{Colored(entry.ActorId)} 방어 태세";
                case BattleLogVerb.Heal: return $"{Colored(entry.ActorId)} 치유 ({entry.Amount}명)";
                case BattleLogVerb.SelfDestruct: return $"{Colored(entry.ActorId)} 자폭";
                case BattleLogVerb.Wait: return entry.Amount > 0 ? $"{Colored(entry.ActorId)} 대기 (+{entry.Amount})" : $"{Colored(entry.ActorId)} 대기";
                case BattleLogVerb.Defeated: return $"{Colored(entry.ActorId)} 쓰러짐";
                default: return Colored(entry.ActorId);
            }
        }

        private void CheckBattleEnd()
        {
            bool playerAlive = UnitQueries.AnyAlive(_world, Team.Player);
            bool enemyAlive = UnitQueries.AnyAlive(_world, Team.Enemy);
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
                if (Mouse.current.leftButton.wasPressedThisFrame && TryScreenToGridPos(Mouse.current.position.ReadValue(), out var placePos))
                    _placementController.HandleGridClick(placePos);
                return;
            }

            if (_battleOver) return;
            if (_turnState.ActiveTeam != Team.Player) return;

            if (Mouse.current.leftButton.wasPressedThisFrame)
                HandleClick();
        }

        /// <summary>
        /// 화면 좌표를 그리드 좌표로 변환한다. 3D 콜라이더 레이캐스트 대신 그리드 바닥 평면과의 교차점으로
        /// 계산하는 이유: 유닛(Capsule)은 타일 위로 솟아 있어서, isometric 각도에서 콜라이더 레이캐스트를
        /// 쓰면 카메라에 더 가까운(앞쪽) 칸의 유닛이 그 뒤 칸으로 가는 레이를 가로막아 클릭이 씹히는 문제가
        /// 있었다. 평면 교차 -> 그리드 좌표 역산 -> GridWorld 조회 방식은 화면에 보이는 칸과 항상 일치하고
        /// 유닛 높이에 의한 가림 문제가 애초에 발생하지 않는다. 배치 단계(TryScreenToGridPos)와 전투 단계
        /// (HandleClick) 둘 다 이 변환을 그대로 재사용한다.
        /// </summary>
        private bool TryScreenToGridPos(Vector2 screenPos, out Vector2Int gridPos)
        {
            gridPos = default;
            var ray = _cam.ScreenPointToRay(screenPos);

            var groundPlane = new Plane(Vector3.up, _grid.Origin);
            if (!groundPlane.Raycast(ray, out float enter)) return false;

            var worldPoint = ray.GetPoint(enter);
            gridPos = new Vector2Int(
                Mathf.RoundToInt((worldPoint.x - _grid.Origin.x) / tileSize),
                Mathf.RoundToInt((worldPoint.z - _grid.Origin.z) / tileSize));

            return _grid.InBounds(gridPos);
        }

        private void HandleClick()
        {
            if (!TryScreenToGridPos(Mouse.current.position.ReadValue(), out var gridPos)) return;

            int occupantId = _grid.GetOccupant(gridPos);
            if (occupantId != TileData.NoOccupant)
            {
                OnUnitClicked(occupantId);
                return;
            }

            OnTileClicked(gridPos);
        }

        private void OnUnitClicked(int unitId)
        {
            if (_state == SelectState.UnitSelected && _attackableTargets != null && _attackableTargets.Contains(unitId))
            {
                TryAttack(_selectedUnitId, unitId);
                return;
            }

            if (_world.Get<Team>(unitId) != Team.Player || !UnitQueries.IsAlive(_world, unitId) ||
                (_world.Get<HasMoved>(unitId).Value && _world.Get<HasActed>(unitId).Value))
            {
                ClearSelection();
                return;
            }

            SelectUnit(unitId);
        }

        private void OnTileClicked(Vector2Int pos)
        {
            if (_state == SelectState.UnitSelected)
            {
                if (_reachableTiles != null && _reachableTiles.Contains(pos))
                    MoveSelectedUnit(pos);
                return;
            }

            // 유닛이 선택되지 않은 상태에서 빈 칸을 클릭하면(구조물이든 아니든) 구조물 정보 패널을
            // 켜거나 끈다 — 순수 표시용 상호작용이라 TileData.StructureId 조회 외에 다른 판정은 없다.
            string structureId = _grid.GetStructure(pos);
            if (!string.IsNullOrEmpty(structureId))
                _hud.ShowStructurePanel(structureId);
            else
                _hud.HideStructurePanel();
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
            if (!MovementSystem.TryMove(_grid, _world, _selectedUnitId, pos)) return;

            _hud.AddLogEntry(FormatLogEntry(new BattleLogEntry { ActorId = _selectedUnitId, Verb = BattleLogVerb.Move, TargetId = BattleLogEntry.NoTarget }));
            _viewsById[_selectedUnitId].Refresh(_world, _selectedUnitId);
            RecomputeHighlights();
        }

        private void TryAttack(int attackerId, int targetId)
        {
            if (!CombatSystem.TryAttack(_grid, _world, attackerId, targetId, out int damage, out int counterDamage)) return;

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
            _viewsById[attackerId].FaceTowards(_grid.GridToWorld(_world.Get<GridPosition>(targetId).Value));
            RefreshRoster();

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
            if (!AbilitySystem.TrySelfDestruct(_grid, _world, unitId, out var damagedIds)) return;

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
