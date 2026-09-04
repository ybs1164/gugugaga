using System.Collections.Generic;
using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// UnitWorld에 유닛 데이터를 추가하고, 그에 대응하는 유닛 프리팹(UnitView + UnitDefinition)을
    /// 인스턴스화해준다. 유닛 타입별 분기는 갖지 않는다 — 어떤 프리팹을 넘기느냐로 타입이 결정된다.
    /// UnitDefinition의 각 속성 값을 UnitWorld.Spawn에 하나씩 그대로 전달할 뿐, 값을 묶어 들고 있지 않는다.
    /// </summary>
    public class UnitSpawner : MonoBehaviour
    {
        public List<UnitView> SpawnedViews { get; } = new List<UnitView>();

        public UnitView Spawn(GridWorld grid, UnitWorld units, Team team, UnitView prefab, Vector2Int pos)
        {
            var definition = prefab.GetComponent<UnitDefinition>();

            int id = units.Spawn(
                team, pos,
                definition.MaxHp, definition.Attack, definition.Defense, definition.AttackRange, definition.CanGuard,
                definition.MoveRange, definition.IgnoreTerrain, definition.IgnoreUnitBlocking, definition.AllowDiagonal);

            grid.PlaceOccupant(pos, id);

            var view = Instantiate(prefab, transform);
            view.name = $"Unit_{team}_{prefab.name}_{id}";
            view.Init(units, id, grid);
            SpawnedViews.Add(view);
            return view;
        }
    }
}
