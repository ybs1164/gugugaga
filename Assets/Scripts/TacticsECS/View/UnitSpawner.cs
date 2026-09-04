using System.Collections.Generic;
using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// UnitWorld에 유닛 데이터를 추가하고, 그에 대응하는 최소 사양의 GameObject(UnitView)를 하나 만들어준다.
    /// </summary>
    public class UnitSpawner : MonoBehaviour
    {
        public List<UnitView> SpawnedViews { get; } = new List<UnitView>();

        public UnitView Spawn(GridWorld grid, UnitWorld units, Team team, UnitType type, Vector2Int pos)
        {
            var data = UnitData.Create(0, team, type, pos);
            int id = units.Spawn(data);
            data = units.Get(id);

            grid.PlaceOccupant(pos, id);

            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = $"Unit_{team}_{type}_{id}";
            go.transform.SetParent(transform, false);

            var view = go.AddComponent<UnitView>();
            view.Init(data, grid);
            SpawnedViews.Add(view);
            return view;
        }
    }
}
