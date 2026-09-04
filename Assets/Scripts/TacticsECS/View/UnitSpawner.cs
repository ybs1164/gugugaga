using System.Collections.Generic;
using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// UnitWorld에 유닛 데이터를 추가하고, 그에 대응하는 유닛 프리팹(UnitView + UnitDefinition)을
    /// 인스턴스화해준다. 유닛 타입별 분기는 갖지 않는다 — 어떤 프리팹을 넘기느냐로 타입이 결정된다.
    /// </summary>
    public class UnitSpawner : MonoBehaviour
    {
        public List<UnitView> SpawnedViews { get; } = new List<UnitView>();

        public UnitView Spawn(GridWorld grid, UnitWorld units, Team team, UnitView prefab, Vector2Int pos)
        {
            var definition = prefab.GetComponent<UnitDefinition>();
            var stats = definition.Stats;
            var data = UnitData.Create(0, team, pos, stats.MaxHp);
            int id = units.Spawn(data, stats);
            data = units.Get(id);

            grid.PlaceOccupant(pos, id);

            var view = Instantiate(prefab, transform);
            view.name = $"Unit_{team}_{prefab.name}_{id}";
            view.Init(data, grid);
            SpawnedViews.Add(view);
            return view;
        }
    }
}
