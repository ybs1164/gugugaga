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
            var definition = prefab.GetComponent<UnitDefinition>();

            int id = world.CreateEntity();

            world.Set(id, team);
            world.Set(id, new GridPosition { Value = pos });
            world.Set(id, new Hp { Value = definition.MaxHp });
            world.Set(id, new HasMoved { Value = false });
            world.Set(id, new HasActed { Value = false });
            world.Set(id, new IsGuarding { Value = false });

            world.Set(id, new MaxHp { Value = definition.MaxHp });
            world.Set(id, new Attack { Value = definition.Attack });
            world.Set(id, new Defense { Value = definition.Defense });
            world.Set(id, new AttackRange { Value = definition.AttackRange });

            world.Set(id, new HealAmount { Value = definition.HealAmount });
            world.Set(id, new HealRange { Value = definition.HealRange });

            world.Set(id, new AvailableActions { Value = definition.AvailableActions });

            world.Set(id, new MoveRange { Value = definition.MoveRange });
            world.Set(id, new IgnoreTerrain { Value = definition.IgnoreTerrain });
            world.Set(id, new IgnoreUnitBlocking { Value = definition.IgnoreUnitBlocking });
            world.Set(id, new AllowDiagonal { Value = definition.AllowDiagonal });

            grid.PlaceOccupant(pos, id);

            var view = Instantiate(prefab, transform);
            view.name = $"Unit_{team}_{prefab.name}_{id}";
            view.Init(world, id, grid);
            SpawnedViews.Add(view);
            return view;
        }
    }
}
