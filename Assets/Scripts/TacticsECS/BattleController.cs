using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TacticsECS
{
    /// <summary>
    /// 전투 전체를 조율하는 단일 진입점. 빈 GameObject에 이 컴포넌트 하나만 붙이면 데모가 돌아간다.
    ///
    /// 구조:
    /// - GridWorld / UnitWorld  : 실제 게임 데이터 (야매 ECS의 "Component 배열")
    /// - PathfindingSystem / MovementSystem / CombatSystem / EnemyAI : 데이터를 읽고 쓰는 정적 "System"
    /// - GridView / TileView / UnitView : 데이터를 화면에 보여주기만 하는 얇은 "View" (로직 없음)
    /// - BattleController(이 클래스) : 입력을 받아 System을 호출하고, 결과를 View에 반영하는 조율자
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

        [Header("Stress Test")]
        [Tooltip("데모 편성 외에 팀당 추가로 스폰할 유닛 수. 100+ 오브젝트 성능 확인용.")]
        [SerializeField] private int stressTestExtraUnitsPerTeam = 0;

        private GridWorld _grid;
        private UnitWorld _units;
        private TurnManager _turnManager;
        private GridView _gridView;
        private UnitSpawner _spawner;
        private Camera _cam;

        private readonly Dictionary<int, UnitView> _viewsById = new Dictionary<int, UnitView>();

        private enum SelectState { None, UnitSelected }
        private SelectState _state = SelectState.None;
        private int _selectedUnitId = -1;
        private HashSet<Vector2Int> _reachableTiles;
        private List<int> _attackableTargets;

        private bool _battleOver;
        private string _statusMessage = "";

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
            _units = new UnitWorld();

            var gridViewGo = new GameObject("GridView");
            gridViewGo.transform.SetParent(transform, false);
            _gridView = gridViewGo.AddComponent<GridView>();
            _gridView.Build(_grid);

            var spawnerGo = new GameObject("UnitSpawner");
            spawnerGo.transform.SetParent(transform, false);
            _spawner = spawnerGo.AddComponent<UnitSpawner>();

            SpawnDemoFormation();

            var tmGo = new GameObject("TurnManager");
            tmGo.transform.SetParent(transform, false);
            _turnManager = tmGo.AddComponent<TurnManager>();
            _turnManager.OnTurnStart += HandleTurnStart;
            _turnManager.Init(_units);

            PositionCamera();
        }

        private void SpawnDemoFormation()
        {
            SpawnUnit(Team.Player, UnitType.Guard, new Vector2Int(1, 1));
            SpawnUnit(Team.Player, UnitType.Melee, new Vector2Int(1, 3));
            SpawnUnit(Team.Player, UnitType.Ranged, new Vector2Int(0, 5));
            SpawnUnit(Team.Player, UnitType.Melee, new Vector2Int(1, 6));

            SpawnUnit(Team.Enemy, UnitType.Guard, new Vector2Int(gridWidth - 2, gridHeight - 2));
            SpawnUnit(Team.Enemy, UnitType.Melee, new Vector2Int(gridWidth - 2, gridHeight - 4));
            SpawnUnit(Team.Enemy, UnitType.Ranged, new Vector2Int(gridWidth - 1, gridHeight - 6));
            SpawnUnit(Team.Enemy, UnitType.Melee, new Vector2Int(gridWidth - 2, gridHeight - 7));

            if (stressTestExtraUnitsPerTeam > 0)
                SpawnStressTestUnits(stressTestExtraUnitsPerTeam);
        }

        private void SpawnStressTestUnits(int perTeam)
        {
            var rng = new System.Random(12345);
            for (int i = 0; i < perTeam; i++)
            {
                var p = RandomFreeTile(rng, 0, gridWidth / 2);
                if (p.HasValue) SpawnUnit(Team.Player, (UnitType)(i % 3), p.Value);

                var e = RandomFreeTile(rng, gridWidth / 2, gridWidth);
                if (e.HasValue) SpawnUnit(Team.Enemy, (UnitType)(i % 3), e.Value);
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

        private void SpawnUnit(Team team, UnitType type, Vector2Int pos)
        {
            if (_grid.IsOccupied(pos)) return;
            var view = _spawner.Spawn(_grid, _units, team, type, pos);
            _viewsById[view.UnitId] = view;
        }

        private void PositionCamera()
        {
            var center = _grid.GridToWorld(new Vector2Int(gridWidth / 2, gridHeight / 2));
            float dist = Mathf.Max(gridWidth, gridHeight) * tileSize * 1.1f;
            _cam.transform.position = center + new Vector3(0, dist, -dist * 0.6f);
            _cam.transform.LookAt(center);
        }

        // ---------- Turn flow ----------

        private void HandleTurnStart(Team team, int turnNumber)
        {
            _statusMessage = $"{turnNumber}턴 - {(team == Team.Player ? "플레이어" : "적")} 턴";
            ClearSelection();

            if (team == Team.Enemy && !_battleOver)
                StartCoroutine(RunEnemyTurnRoutine());
        }

        private IEnumerator RunEnemyTurnRoutine()
        {
            yield return new WaitForSeconds(0.3f);
            EnemyAI.RunTurn(_grid, _units);
            RefreshAllViews();
            CheckBattleEnd();
            yield return new WaitForSeconds(0.3f);
            if (!_battleOver)
                _turnManager.EndTurn();
        }

        private void RefreshAllViews()
        {
            for (int i = 0; i < _units.Count; i++)
            {
                var data = _units.Get(i);
                if (_viewsById.TryGetValue(i, out var view))
                    view.Refresh(data);
            }
        }

        private void CheckBattleEnd()
        {
            bool playerAlive = _units.AnyAlive(Team.Player);
            bool enemyAlive = _units.AnyAlive(Team.Enemy);
            if (!playerAlive || !enemyAlive)
            {
                _battleOver = true;
                _statusMessage = !playerAlive ? "패배..." : "승리!";
                ClearSelection();
            }
        }

        // ---------- Input ----------

        private void Update()
        {
            if (_battleOver) return;
            if (_turnManager == null || _turnManager.ActiveTeam != Team.Player) return;
            if (Mouse.current == null) return;

            if (Mouse.current.leftButton.wasPressedThisFrame)
                HandleClick();
        }

        private void HandleClick()
        {
            var screenPos = Mouse.current.position.ReadValue();
            var ray = _cam.ScreenPointToRay(screenPos);
            if (!Physics.Raycast(ray, out var hit, 200f)) return;

            var unitView = hit.collider.GetComponentInParent<UnitView>();
            if (unitView != null)
            {
                OnUnitClicked(unitView.UnitId);
                return;
            }

            var tileView = hit.collider.GetComponentInParent<TileView>();
            if (tileView != null)
                OnTileClicked(tileView.GridPos);
        }

        private void OnUnitClicked(int unitId)
        {
            if (_state == SelectState.UnitSelected && _attackableTargets != null && _attackableTargets.Contains(unitId))
            {
                TryAttack(_selectedUnitId, unitId);
                return;
            }

            var unit = _units.Get(unitId);
            if (unit.Team != Team.Player || !unit.IsAlive || (unit.HasMoved && unit.HasActed))
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
            var unit = _units.Get(_selectedUnitId);

            _reachableTiles = null;
            if (!unit.HasMoved)
            {
                PathfindingSystem.GetReachable(_grid, unit.GridPos, unit.MoveRange, unit.Id, out var reachable);
                reachable.Remove(unit.GridPos);
                _reachableTiles = reachable;
                _gridView.HighlightMove(_reachableTiles);
            }

            _attackableTargets = new List<int>();
            if (!unit.HasActed)
            {
                foreach (var enemy in AllUnits())
                {
                    if (!enemy.IsAlive || enemy.Team == unit.Team) continue;
                    if (CombatSystem.IsInAttackRange(unit, enemy))
                        _attackableTargets.Add(enemy.Id);
                }
                _gridView.HighlightAttack(_attackableTargets.Select(id => _units.Get(id).GridPos));
            }
        }

        private IEnumerable<UnitData> AllUnits()
        {
            for (int i = 0; i < _units.Count; i++)
                yield return _units.Get(i);
        }

        private void MoveSelectedUnit(Vector2Int pos)
        {
            if (!MovementSystem.TryMove(_grid, _units, _selectedUnitId, pos)) return;
            _viewsById[_selectedUnitId].Refresh(_units.Get(_selectedUnitId));
            RecomputeHighlights();
        }

        private void TryAttack(int attackerId, int targetId)
        {
            if (!CombatSystem.TryAttack(_grid, _units, attackerId, targetId, out int dmg)) return;

            _viewsById[attackerId].Refresh(_units.Get(attackerId));
            _viewsById[targetId].Refresh(_units.Get(targetId));

            _statusMessage = $"유닛 {attackerId} -> 유닛 {targetId}: {dmg} 피해";
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
        }

        // ---------- Minimal UI (Canvas 불필요) ----------

        private void OnGUI()
        {
            GUI.Label(new Rect(10, 10, 500, 24), _statusMessage);

            if (_battleOver) return;
            if (_turnManager == null || _turnManager.ActiveTeam != Team.Player) return;

            if (GUI.Button(new Rect(10, 40, 100, 30), "턴 종료"))
                _turnManager.EndTurn();

            if (_state == SelectState.UnitSelected)
            {
                var unit = _units.Get(_selectedUnitId);
                if (unit.Type == UnitType.Guard && !unit.HasActed)
                {
                    if (GUI.Button(new Rect(120, 40, 120, 30), "방어 태세"))
                    {
                        CombatSystem.TryDefend(_units, _selectedUnitId);
                        _viewsById[_selectedUnitId].Refresh(_units.Get(_selectedUnitId));
                        ClearSelection();
                    }
                }

                if (GUI.Button(new Rect(250, 40, 100, 30), "선택 해제"))
                    ClearSelection();
            }
        }
    }
}
