using System.Collections.Generic;
using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// EntityWorld에 새 엔티티를 만들고 유닛에 필요한 컴포넌트를 채운 뒤, 그에 대응하는 유닛 프리팹
    /// (UnitView + UnitDefinition)을 인스턴스화해준다. 유닛 타입별 분기는 갖지 않는다 — 어떤 프리팹을
    /// 넘기느냐로 타입이 결정된다. UnitDefinition의 각 값을 컴포넌트 하나씩으로 그대로 옮겨 담을 뿐,
    /// 값을 묶어 들고 있지 않는다.
    /// </summary>
    public class UnitSpawner : MonoBehaviour
    {
        public List<UnitView> SpawnedViews { get; } = new List<UnitView>();

        public UnitView Spawn(GridWorld grid, EntityWorld world, Team team, UnitView prefab, Vector2Int pos)
        {
            var view = Instantiate(prefab, transform);
            return FinishSpawn(grid, world, team, view, pos, prefab.name);
        }

        /// <summary>CSV로 정의한 유닛을 스폰한다. baseVisualPrefab(기존 Melee/Ranged/Guard 중 하나)을 그대로
        /// 인스턴스화한 뒤, 그 인스턴스의 UnitDefinition에만 CSV 행 값을 덮어써서(원본 프리팹은 그대로 둔 채)
        /// FinishSpawn을 공유한다 — CSV 유닛도 Spawn과 완전히 같은 방식으로 EntityWorld에 등록된다.</summary>
        public UnitView SpawnFromCsv(GridWorld grid, EntityWorld world, Team team, UnitView baseVisualPrefab, UnitCsvRow row, Vector2Int pos)
        {
            var view = Instantiate(baseVisualPrefab, transform);
            view.GetComponent<UnitDefinition>().ApplyCsvOverrides(row, UnitCsvActionFactory.BuildActions(row));
            return FinishSpawn(grid, world, team, view, pos, row.Name);
        }

        /// <summary>이미 인스턴스화된 view(정의값이 확정된 상태)를 EntityWorld 엔티티로 등록하고 그리드에
        /// 배치하는 공통 마무리 단계. Spawn/SpawnFromCsv 둘 다 이 메서드로 수렴한다.</summary>
        private UnitView FinishSpawn(GridWorld grid, EntityWorld world, Team team, UnitView view, Vector2Int pos, string label)
        {
            var definition = view.GetComponent<UnitDefinition>();

            int id = world.CreateEntity();

            world.Set(id, team);
            world.Set(id, new GridPosition { Value = pos });
            world.Set(id, new Hp { Value = definition.MaxHp });
            world.Set(id, new HasMoved { Value = false });
            world.Set(id, new HasActed { Value = false });
            world.Set(id, new IsGuarding { Value = false });
            world.Set(id, new Accelerated { Value = false });
            world.Set(id, new Frozen { Value = false });

            // 정찰 플레이스홀더: 아직 시야 시스템이 없어 기본값 0으로만 채워둔다(Core/UnitComponents.cs 참고).
            world.Set(id, new VisionRange { Value = 0 });

            world.Set(id, new MaxHp { Value = definition.MaxHp });
            world.Set(id, new Attack { Value = definition.Attack });
            world.Set(id, new Defense { Value = definition.Defense });
            world.Set(id, new AttackRange { Value = definition.AttackRange });

            world.Set(id, new HealAmount { Value = definition.HealAmount });
            world.Set(id, new HealRange { Value = definition.HealRange });

            // 수송 플레이스홀더: 아직 태우고 내리는 시스템이 없어 정원 값만 채워둔다(Core/UnitComponents.cs 참고).
            world.Set(id, new CargoCapacity { Value = definition.CargoCapacity });

            var unitActions = new List<IUnitAction>(definition.Actions);
            if (!unitActions.Exists(a => a is WaitAction))
                unitActions.Add(new WaitAction());

            world.Set(id, new AvailableActions { Value = definition.AvailableActions | ActionType.Wait });
            world.Set(id, new UnitActions { Value = unitActions });

            world.Set(id, new MoveRange { Value = definition.MoveRange });
            world.Set(id, new IgnoreTerrain { Value = definition.IgnoreTerrain });
            world.Set(id, new IgnoreUnitBlocking { Value = definition.IgnoreUnitBlocking });
            world.Set(id, new AllowDiagonal { Value = definition.AllowDiagonal });
            world.Set(id, new MoveDomain { Value = definition.Domain });

            grid.PlaceOccupant(pos, id);

            view.name = $"Unit_{team}_{label}_{id}";
            view.Init(world, id, grid);
            SpawnedViews.Add(view);
            return view;
        }
    }
}
