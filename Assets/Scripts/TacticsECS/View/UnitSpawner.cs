using System.Collections.Generic;
using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 유닛 엔티티를 Units.csv 행으로 만들고(UnitFactorySystem — View 없는 순수 생성) 행의 BaseVisual 프리팹
    /// (UnitView + UnitDefinition)을 인스턴스화해 붙여준다. 스탯은 행에서만 온다 — 프리팹은 외형만 갖는다.
    /// </summary>
    public class UnitSpawner : MonoBehaviour
    {
        public List<UnitView> SpawnedViews { get; } = new List<UnitView>();

        /// <summary>CSV로 정의한 유닛을 스폰한다. 엔티티는 UnitFactorySystem.CreateFromCsv로 만들고, View는
        /// AttachCsvView로 붙인다 — 헤드리스 시뮬레이션과 같은 생성 경로다.</summary>
        public UnitView SpawnFromCsv(GridWorld grid, EntityWorld world, Team team, UnitView baseVisualPrefab, UnitCsvRow row, Vector2Int pos)
        {
            int id = UnitFactorySystem.CreateFromCsv(grid, world, team, row, pos);
            return AttachCsvView(grid, world, id, baseVisualPrefab, row);
        }

        /// <summary>이미 만들어진 CSV 유닛 엔티티(UnitFactorySystem.SpawnEconomyUnit 등)에 View만 붙인다. baseVisualPrefab
        /// (기존 Melee/Ranged/Guard 중 하나)을 인스턴스화한 뒤 그 인스턴스의 UnitDefinition에만 CSV 행 값을 덮어쓴다
        /// (원본 프리팹은 그대로).</summary>
        public UnitView AttachCsvView(GridWorld grid, EntityWorld world, int id, UnitView baseVisualPrefab, UnitCsvRow row)
        {
            var view = Instantiate(baseVisualPrefab, transform);
            view.GetComponent<UnitDefinition>().ApplyCsvOverrides(row, UnitCsvActionFactory.BuildActions(row));
            return FinishView(grid, world, world.Get<Team>(id), view, id, row.Name);
        }

        private UnitView FinishView(GridWorld grid, EntityWorld world, Team team, UnitView view, int id, string label)
        {
            view.name = $"Unit_{team}_{label}_{id}";
            view.Init(world, id, grid, label);
            SpawnedViews.Add(view);
            return view;
        }
    }
}
