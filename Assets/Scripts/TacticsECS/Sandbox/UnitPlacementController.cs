using System.Collections.Generic;
using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 샌드박스 배치 단계 전용 로직. CSV로 불러온 유닛 팔레트에서 하나를 "브러시"로 고르고, 그리드를
    /// 클릭해 놓거나(빈 칸) 치운다(이미 유닛이 있는 칸). BattleController(SandboxHud 이벤트를 받는 쪽)가
    /// 이 컨트롤러의 메서드를 그대로 호출해주기만 하면 되고, 배치 세부사항(어떤 프리팹을 베이스로 쓸지,
    /// 치울 때 그리드/EntityWorld를 어떻게 정리할지)은 전부 여기 안에 있다.
    /// MonoBehaviour가 아닌 평범한 C# 클래스다 — BattleController가 이미 갖고 있는 GridWorld/EntityWorld/
    /// UnitSpawner를 생성자로 받아 재사용할 뿐, 자신만의 씬 오브젝트를 갖지 않는다.
    /// </summary>
    public class UnitPlacementController
    {
        private readonly GridWorld _grid;
        private readonly EntityWorld _world;
        private readonly UnitSpawner _spawner;
        private readonly IReadOnlyDictionary<string, UnitView> _basePrefabsByName;
        private readonly Dictionary<int, UnitView> _viewsById;

        private List<UnitCsvRow> _rows = new List<UnitCsvRow>();
        private int _selectedRowIndex = -1;
        private Team _selectedTeam = Team.Player;

        public IReadOnlyList<UnitCsvRow> Rows => _rows;
        public Team SelectedTeam => _selectedTeam;

        public UnitPlacementController(GridWorld grid, EntityWorld world, UnitSpawner spawner,
            IReadOnlyDictionary<string, UnitView> basePrefabsByName, Dictionary<int, UnitView> viewsById)
        {
            _grid = grid;
            _world = world;
            _spawner = spawner;
            _basePrefabsByName = basePrefabsByName;
            _viewsById = viewsById;
        }

        /// <summary>새로 불러온 CSV 행 목록으로 팔레트를 교체한다. 첫 번째 행을 기본 브러시로 선택한다.</summary>
        public void SetRows(List<UnitCsvRow> rows)
        {
            _rows = rows ?? new List<UnitCsvRow>();
            _selectedRowIndex = _rows.Count > 0 ? 0 : -1;
        }

        public void SelectRow(int index)
        {
            if (index >= 0 && index < _rows.Count) _selectedRowIndex = index;
        }

        public void SelectTeam(Team team) => _selectedTeam = team;

        /// <summary>빈 칸이면 현재 브러시(선택된 CSV 행 + 팀)로 스폰하고, 이미 유닛이 있는 칸이면 치운다.</summary>
        public void HandleGridClick(Vector2Int pos)
        {
            int occupant = _grid.GetOccupant(pos);
            if (occupant != TileData.NoOccupant)
            {
                RemoveUnit(occupant, pos);
                return;
            }

            if (_selectedRowIndex < 0 || _selectedRowIndex >= _rows.Count) return;
            var row = _rows[_selectedRowIndex];

            if (!_basePrefabsByName.TryGetValue(row.BaseVisual, out var basePrefab) || basePrefab == null)
            {
                Debug.LogWarning($"[UnitPlacementController] BaseVisual '{row.BaseVisual}'에 대응하는 프리팹을 찾지 못했다 (유닛: {row.Name}). Melee/Ranged/Guard 중 하나로 CSV를 수정하세요.");
                return;
            }

            var view = _spawner.SpawnFromCsv(_grid, _world, _selectedTeam, basePrefab, row, pos);
            _viewsById[view.UnitId] = view;
        }

        /// <summary>이미 죽인 유닛과 똑같은 방식(Hp=0)으로 취급해 치운다 — EntityWorld는 엔티티를 삭제하는
        /// 개념이 없고, 모든 System이 이미 "Hp&lt;=0이면 죽은 유닛"으로 걸러내고 있으므로(UnitQueries.IsAlive)
        /// 배치 단계에서 치우는 것도 같은 규칙을 그대로 재사용하면 충분하다.</summary>
        private void RemoveUnit(int unitId, Vector2Int pos)
        {
            _grid.RemoveOccupant(pos);
            _world.Set(unitId, new Hp { Value = 0 });

            if (_viewsById.TryGetValue(unitId, out var view))
            {
                Object.Destroy(view.gameObject);
                _viewsById.Remove(unitId);
            }
        }
    }
}
