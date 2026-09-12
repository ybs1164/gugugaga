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

        [Header("Unit Prefabs")]
        [Tooltip("근접 유닛 프리팹 (UnitView + UnitDefinition 컴포넌트를 가진 프리팹). Assets/Prefabs/Units 참고.")]
        [SerializeField] private UnitView meleePrefab;
        [Tooltip("원거리 유닛 프리팹.")]
        [SerializeField] private UnitView rangedPrefab;
        [Tooltip("방어(탱커) 유닛 프리팹.")]
        [SerializeField] private UnitView guardPrefab;

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

        private void SetupBattle()
        {
            _grid = new GridWorld(gridWidth, gridHeight, tileSize);
            _world = new EntityWorld();

            var gridViewGo = new GameObject("GridView");
            gridViewGo.transform.SetParent(transform, false);
            _gridView = gridViewGo.AddComponent<GridView>();
            _gridView.Build(_grid);

            var hudGo = new GameObject("BattleHud");
            hudGo.transform.SetParent(transform, false);
            _hud = hudGo.AddComponent<BattleHud>();
            _hud.Init();
            _hud.OnDefendClicked += HandleDefendClicked;
            _hud.OnHealClicked += HandleHealClicked;
            _hud.OnSelfDestructClicked += HandleSelfDestructClicked;
            _hud.OnDeselectClicked += ClearSelection;
            _hud.OnRestartClicked += HandleReturnToSetup;

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

            var sandboxHudGo = new GameObject("SandboxHud");
            sandboxHudGo.transform.SetParent(transform, false);
            _sandboxHud = sandboxHudGo.AddComponent<SandboxHud>();
            _sandboxHud.Init();
            _sandboxHud.OnLoadClicked += HandleSandboxLoad;
            _sandboxHud.OnExportClicked += HandleSandboxExport;
            _sandboxHud.OnUnitSelected += HandleSandboxUnitSelected;
            _sandboxHud.OnTeamSelected += HandleSandboxTeamSelected;
            _sandboxHud.OnStartBattleClicked += HandleSandboxStartBattle;
            _sandboxHud.SetSelectedTeam(Team.Player);
            _sandboxHud.SetStatus("\"불러오기\"로 CSV 파일을 선택해 배치를 시작하세요.");
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

        /// <summary>턴 종료 버튼(플레이어)과 적 턴 종료(RunEnemyTurnRoutine) 모두에서 쓰는 공통 진입점.</summary>
        private void EndTurn()
        {
            _turnState = TurnSystem.EndTurn(_world, _turnState);
            HandleTurnStart(_turnState.ActiveTeam, _turnState.TurnNumber);
        }

        private void SpawnDemoFormation()
        {
            SpawnUnit(Team.Player, guardPrefab, new Vector2Int(1, 1));
            SpawnUnit(Team.Player, meleePrefab, new Vector2Int(1, 3));
            SpawnUnit(Team.Player, rangedPrefab, new Vector2Int(0, 5));
            SpawnUnit(Team.Player, meleePrefab, new Vector2Int(1, 6));

            SpawnUnit(Team.Enemy, guardPrefab, new Vector2Int(gridWidth - 2, gridHeight - 2));
            SpawnUnit(Team.Enemy, meleePrefab, new Vector2Int(gridWidth - 2, gridHeight - 4));
            SpawnUnit(Team.Enemy, rangedPrefab, new Vector2Int(gridWidth - 1, gridHeight - 6));
            SpawnUnit(Team.Enemy, meleePrefab, new Vector2Int(gridWidth - 2, gridHeight - 7));

            if (stressTestExtraUnitsPerTeam > 0)
                SpawnStressTestUnits(stressTestExtraUnitsPerTeam);
        }

        private void SpawnStressTestUnits(int perTeam)
        {
            var prefabs = new[] { meleePrefab, rangedPrefab, guardPrefab };
            var rng = new System.Random(12345);
            for (int i = 0; i < perTeam; i++)
            {
                var prefab = prefabs[i % prefabs.Length];
                var p = RandomFreeTile(rng, 0, gridWidth / 2);
                if (p.HasValue) SpawnUnit(Team.Player, prefab, p.Value);

                var e = RandomFreeTile(rng, gridWidth / 2, gridWidth);
                if (e.HasValue) SpawnUnit(Team.Enemy, prefab, e.Value);
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

        private void SpawnUnit(Team team, UnitView prefab, Vector2Int pos)
        {
            if (_grid.IsOccupied(pos)) return;
            var view = _spawner.Spawn(_grid, _world, team, prefab, pos);
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

            if (team == Team.Enemy && !_battleOver)
                StartCoroutine(RunEnemyTurnRoutine());
        }

        private IEnumerator RunEnemyTurnRoutine()
        {
            yield return new WaitForSeconds(0.3f);
            var attacks = EnemyAI.RunTurn(_grid, _world);
            RefreshAllViews();
            foreach (var (attackerId, targetId) in attacks)
                _viewsById[attackerId].FaceTowards(_grid.GridToWorld(_world.Get<GridPosition>(targetId).Value));
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
            if (_state != SelectState.UnitSelected) return;
            if (_reachableTiles != null && _reachableTiles.Contains(pos))
                MoveSelectedUnit(pos);
        }

        // ---------- Selection / actions ----------

        private void SelectUnit(int unitId)
        {
            _selectedUnitId = unitId;
            _state = SelectState.UnitSelected;
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
            _viewsById[_selectedUnitId].Refresh(_world, _selectedUnitId);
            RecomputeHighlights();
        }

        private void TryAttack(int attackerId, int targetId)
        {
            if (!CombatSystem.TryAttack(_grid, _world, attackerId, targetId, out _, out _)) return;

            _viewsById[attackerId].Refresh(_world, attackerId);
            _viewsById[targetId].Refresh(_world, targetId);
            _viewsById[attackerId].FaceTowards(_grid.GridToWorld(_world.Get<GridPosition>(targetId).Value));

            CheckBattleEnd();
            ClearSelection();
        }

        /// <summary>BattleHud의 방어 태세 버튼 클릭 이벤트 핸들러.</summary>
        private void HandleDefendClicked()
        {
            if (_state != SelectState.UnitSelected) return;
            if (!CombatSystem.TryDefend(_grid, _world, _selectedUnitId)) return;

            _viewsById[_selectedUnitId].Refresh(_world, _selectedUnitId);
            ClearSelection();
        }

        /// <summary>BattleHud의 치유 버튼 클릭 이벤트 핸들러. 대상 선택 없이 즉시 사거리 내
        /// 모든 아군(자신 제외)을 회복시킨다.</summary>
        private void HandleHealClicked()
        {
            if (_state != SelectState.UnitSelected) return;
            if (!AbilitySystem.TryHeal(_grid, _world, _selectedUnitId, out var healedIds)) return;

            _viewsById[_selectedUnitId].Refresh(_world, _selectedUnitId);
            foreach (var id in healedIds)
                _viewsById[id].Refresh(_world, id);

            ClearSelection();
        }

        /// <summary>BattleHud의 자폭 버튼 클릭 이벤트 핸들러. 대상 선택 없이 즉시 자신을 제거하고
        /// 주위 1칸의 모든 적에게 피해를 입힌다.</summary>
        private void HandleSelfDestructClicked()
        {
            if (_state != SelectState.UnitSelected) return;
            int unitId = _selectedUnitId;
            if (!AbilitySystem.TrySelfDestruct(_grid, _world, unitId, out var damagedIds)) return;

            _viewsById[unitId].Refresh(_world, unitId);
            foreach (var id in damagedIds)
                _viewsById[id].Refresh(_world, id);

            CheckBattleEnd();
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
                _hud.SetDeselectVisible(false);
                _hud.SetUnitActions(ActionType.None, hasActed: true);
            }
        }
    }
}
