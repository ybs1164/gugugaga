using System.Collections.Generic;
using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 유닛 엔티티 생성(컴포넌트 채우기 + 그리드 배치)만 하는 순수 함수형 시스템 — View 없이도 유닛을 만들 수 있게 UnitSpawner
    /// (View)에서 분리했다. 헤드리스 시뮬레이션(Editor/EconomySimulation)과 UnitSpawner가 같은 경로로 엔티티를 만든다.
    /// 자체 상태 없음.
    /// </summary>
    public static class UnitFactorySystem
    {
        /// <summary>엔티티를 만들고 유닛 컴포넌트를 전부 채운 뒤 pos에 놓는다. 행동별 값(이동/공격/치유)은 행동 목록에서
        /// 읽는다(UnitDefinition의 getter와 같은 규칙). 대기(WaitAction)는 없으면 추가한다.</summary>
        public static int Create(GridWorld grid, EntityWorld world, Team team, Vector2Int pos, int maxHp, float defense,
            IReadOnlyList<IUnitAction> actions, TerrainType domain, string unitTypeId)
        {
            var unitActions = new List<IUnitAction>();
            var available = ActionType.None;
            int moveRange = 0, attackRange = 0, healAmount = 0, healRange = 0;
            float attack = 0f;
            if (actions != null)
            {
                foreach (var a in actions)
                {
                    if (a == null) continue;
                    unitActions.Add(a);
                    available |= a.GetActionType();
                    switch (a)
                    {
                        case MoveAction m: if (moveRange == 0) moveRange = m.MoveRange; break;
                        case AttackAction at: if (attackRange == 0) { attack = at.Attack; attackRange = at.AttackRange; } break;
                        case HealAction h: if (healRange == 0) { healAmount = h.HealAmount; healRange = h.HealRange; } break;
                    }
                }
            }
            if (!unitActions.Exists(a => a is WaitAction)) unitActions.Add(new WaitAction());

            int id = world.CreateEntity();
            world.Set(id, team);
            world.Set(id, new GridPosition { Value = pos });
            world.Set(id, new Hp { Value = maxHp });
            world.Set(id, new HasMoved { Value = false });
            world.Set(id, new HasActed { Value = false });
            world.Set(id, new IsGuarding { Value = false });
            world.Set(id, new Accelerated { Value = false });
            world.Set(id, new Frozen { Value = false });

            world.Set(id, new VisionRange { Value = VisionRangeFor(unitActions) });
            world.Set(id, new Embarked { Value = false, NavalUnitId = string.Empty });
            world.Set(id, new LandForm());

            // 경제 연동 컴포넌트 기본값 — 제한/보너스 없음. 경제(기술트리)가 켜진 전투에서는 스폰 직후
            // TechEffectSystem.RefreshUnits로 팀 기술에 맞게 다시 채운다.
            world.Set(id, new UnitTypeId { Value = unitTypeId ?? string.Empty });
            world.Set(id, new HomeCity()); // 소속 없음 — 경제가 있으면 CitySystem/SpawnEconomyUnit이 배정
            world.Set(id, new Kills { Value = 0 });
            world.Set(id, new Veteran { Value = false });
            world.Set(id, new TerrainAccess { Mountain = true, Ocean = true });
            world.Set(id, new PositionalDefenseBonus { Value = 0 });

            world.Set(id, new MaxHp { Value = maxHp });
            world.Set(id, new Attack { Value = attack });
            world.Set(id, new Defense { Value = defense });
            world.Set(id, new AttackRange { Value = attackRange });
            world.Set(id, new HealAmount { Value = healAmount });
            world.Set(id, new HealRange { Value = healRange });


            world.Set(id, new AvailableActions { Value = available | ActionType.Wait });
            world.Set(id, new UnitActions { Value = unitActions });
            world.Set(id, new MoveRange { Value = moveRange });
            world.Set(id, new MoveDomain { Value = domain });

            grid.PlaceOccupant(pos, id);
            return id;
        }

        public static int CreateFromCsv(GridWorld grid, EntityWorld world, Team team, UnitCsvRow row, Vector2Int pos) =>
            Create(grid, world, team, pos, row.MaxHp, row.Defense, UnitCsvActionFactory.BuildActions(row), row.Domain, row.Id);

        /// <summary>정찰(ScoutAction) 보유 시 5x5, 아니면 3x3.</summary>
        public static int VisionRangeFor(IReadOnlyList<IUnitAction> actions)
        {
            if (actions != null)
                foreach (var a in actions)
                    if (a is ScoutAction) return GameRules.Vision.ExtendedSightRadius;
            return GameRules.Vision.BaseSightRadius;
        }

        /// <summary>near 칸(차 있으면 가장 가까운 빈 칸, 체비쇼프 반경 3까지)에서 domain 유닛이 설 수 있는 칸. 없으면 null.
        /// 폴리토피아처럼 도시에서 훈련한 유닛은 도시 칸에, 막혀 있으면 주변에 나타난다.</summary>
        public static Vector2Int? FindSpawnSpot(GridWorld grid, Vector2Int near, TerrainType domain)
        {
            for (int r = 0; r <= 3; r++)
            {
                for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                    var p = near + new Vector2Int(dx, dy);
                    if (!grid.InBounds(p) || grid.IsOccupied(p) || !grid.IsWalkable(p) || grid.GetTerrain(p) != domain) continue;
                    return p;
                }
            }
            return null;
        }

        /// <summary>경제 기록의 스폰 요청(훈련/슈퍼 유닛/유적 유닛)을 View 없이 처리한다: near 근처에 만들고, 그 턴에는
        /// 움직이거나 행동할 수 없게 한다. homeCity는 소속 도시(훈련/보상 도시) — 음수면 near에서 가장 가까운 자리 있는 자기 도시
        /// (유적 유닛). 만든 id, 실패하면 -1.</summary>
        public static int SpawnEconomyUnit(GridWorld grid, EntityWorld world, EconomyWorld econ, Team team, string unitId, Vector2Int near, int homeCity = HomeCity.None,
            bool veteran = false, string boatId = null)
        {
            var row = CitySystem.FindUnitRow(econ, unitId);
            if (row == null) return -1;
            bool onBoat = !string.IsNullOrEmpty(boatId) && EmbarkSystem.FindNavalRow(boatId) != null;
            var spot = FindSpawnSpot(grid, near, onBoat ? TerrainType.Water : row.Domain);
            if (spot == null) return -1;
            int id = CreateFromCsv(grid, world, team, row, spot.Value);
            if (veteran) VeteranSystem.MakeVeteran(world, id);
            if (onBoat) EmbarkSystem.EmbarkAs(world, id, boatId);
            world.Set(id, new HasMoved { Value = true });
            world.Set(id, new HasActed { Value = true });
            CitySystem.AssignHome(world, econ, id, homeCity >= 0 ? homeCity : CitySystem.NearestCityWithRoom(world, econ, team, near));
            return id;
        }
    }
}
