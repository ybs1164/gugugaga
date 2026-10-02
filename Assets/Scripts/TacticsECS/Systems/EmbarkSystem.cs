using System.Collections.Generic;
using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 항구 승선/하선과 배 업그레이드 — 폴리토피아 위키 Port/Raft/Scout/Unit Skills(Carry) 규칙. 순수 함수형, 자체 상태 없음.
    ///   - 육지 유닛이 자기 팀 항구 칸에 들어가면 뗏목이 되고 그 턴 행동이 끝난다(적 항구는 쓸 수 없다 — 경로 탐색이 막는다).
    ///   - 배가 육지 칸에 들어가면 원래 유닛으로 돌아오고 그 턴 행동이 끝난다. 업그레이드는 사라진다.
    ///   - 뗏목은 자기 영토 안에서 별을 내고 정찰선/충각선/폭격선으로 업그레이드한다(치유 없음, 행동 소모 없음).
    /// 배로 바뀌는 동안 원래 스탯은 LandForm 컴포넌트에 보관한다. 배 스탯 표는 Data/NavalUnitDefinition.cs.
    /// </summary>
    public static class EmbarkSystem
    {
        public static bool IsEmbarked(EntityWorld world, int unitId) => world.GetOrDefault<Embarked>(unitId).Value;

        public static string NavalUnitId(EntityWorld world, int unitId) => world.GetOrDefault<Embarked>(unitId).NavalUnitId;

        /// <summary>이동 직후 부른다: 항구에 선 육지 유닛은 승선, 육지에 선 배는 하선. 무언가 바뀌었으면 true.</summary>
        public static bool ApplyAfterMove(GridWorld grid, EntityWorld world, int unitId)
        {
            if (!UnitQueries.IsAlive(world, unitId)) return false;
            var pos = world.Get<GridPosition>(unitId).Value;
            var tile = grid.GetTile(pos);
            var team = world.Get<Team>(unitId);

            if (!IsEmbarked(world, unitId))
            {
                if (world.Get<MoveDomain>(unitId).Value != TerrainType.Land) return false;
                if (tile.Terrain != TerrainType.Water || tile.BuildingId != BuildingDefinition.Port || tile.OwnerTeam != (int)team) return false;
                world.Set(unitId, new LandForm
                {
                    Attack = world.Get<Attack>(unitId).Value,
                    Defense = world.Get<Defense>(unitId).Value,
                    AttackRange = world.Get<AttackRange>(unitId).Value,
                    MoveRange = world.Get<MoveRange>(unitId).Value,
                    VisionRange = world.GetOrDefault<VisionRange>(unitId).Value,
                    Domain = world.Get<MoveDomain>(unitId).Value,
                    AvailableActions = world.GetOrDefault<AvailableActions>(unitId).Value,
                    Actions = world.Get<UnitActions>(unitId).Value,
                });
                // 위키: 보통 유닛은 뗏목, Cloak은 Dinghy, Dagger는 Pirate가 된다(유닛 CSV Boat 칸 — PortBoat 컴포넌트).
                ApplyNaval(world, unitId, FindNavalRow(world.GetOrDefault<PortBoat>(unitId).NavalUnitId) ?? NavalUnitDefinition.Raft);
                EndTurn(world, unitId);
                return true;
            }

            if (tile.Terrain == TerrainType.Water) return false;
            var land = world.Get<LandForm>(unitId);
            world.Set(unitId, new Attack { Value = land.Attack });
            world.Set(unitId, new Defense { Value = land.Defense });
            world.Set(unitId, new AttackRange { Value = land.AttackRange });
            world.Set(unitId, new MoveRange { Value = land.MoveRange });
            world.Set(unitId, new VisionRange { Value = land.VisionRange });
            world.Set(unitId, new MoveDomain { Value = land.Domain });
            world.Set(unitId, new AvailableActions { Value = land.AvailableActions });
            world.Set(unitId, new UnitActions { Value = land.Actions });
            world.Set(unitId, new Embarked { Value = false, NavalUnitId = string.Empty });
            EndTurn(world, unitId);
            return true;
        }

        /// <summary>육지 유닛을 곧바로 navalUnitId 배에 태운다(물 위 유적의 "베테랑 충각선" 보상처럼 배에 탄 채 나타나는 유닛).
        /// 원래 스탯은 LandForm에 보관되어 육지에 내리면 돌아온다.</summary>
        public static void EmbarkAs(EntityWorld world, int unitId, string navalUnitId)
        {
            var row = FindNavalRow(navalUnitId);
            if (row == null || IsEmbarked(world, unitId)) return;
            world.Set(unitId, new LandForm
            {
                Attack = world.Get<Attack>(unitId).Value,
                Defense = world.Get<Defense>(unitId).Value,
                AttackRange = world.Get<AttackRange>(unitId).Value,
                MoveRange = world.Get<MoveRange>(unitId).Value,
                VisionRange = world.GetOrDefault<VisionRange>(unitId).Value,
                Domain = world.Get<MoveDomain>(unitId).Value,
                AvailableActions = world.GetOrDefault<AvailableActions>(unitId).Value,
                Actions = world.Get<UnitActions>(unitId).Value,
            });
            ApplyNaval(world, unitId, row);
        }

        private static void EndTurn(EntityWorld world, int unitId)
        {
            world.Set(unitId, new HasMoved { Value = true });
            world.Set(unitId, new HasActed { Value = true });
        }

        /// <summary>배 행 하나의 스탯/행동을 유닛에 입힌다(체력은 그대로).</summary>
        private static void ApplyNaval(EntityWorld world, int unitId, UnitCsvRow row)
        {
            var actions = UnitCsvActionFactory.BuildActions(row);
            world.Set(unitId, new Attack { Value = row.AttackAttack });
            world.Set(unitId, new Defense { Value = row.Defense });
            world.Set(unitId, new AttackRange { Value = row.AttackRange });
            world.Set(unitId, new MoveRange { Value = row.MoveRange });
            world.Set(unitId, new VisionRange { Value = UnitFactorySystem.VisionRangeFor(actions) });
            world.Set(unitId, new MoveDomain { Value = TerrainType.Water });
            world.Set(unitId, new AvailableActions { Value = row.Actions | ActionType.Wait });
            world.Set(unitId, new UnitActions { Value = actions });
            world.Set(unitId, new Embarked { Value = true, NavalUnitId = row.Id });
        }

        public static UnitCsvRow FindNavalRow(string navalUnitId)
        {
            if (string.IsNullOrEmpty(navalUnitId)) return null;
            if (navalUnitId == NavalUnitDefinition.RaftId) return NavalUnitDefinition.Raft;
            foreach (var u in NavalUnitDefinition.Upgrades)
                if (u.Row.Id == navalUnitId) return u.Row;
            foreach (var s in NavalUnitDefinition.Special)
                if (s.Id == navalUnitId) return s;
            return null;
        }

        /// <summary>뗏목을 이 배로 업그레이드할 수 있는지(뗏목 상태, 자기 영토, 기술, 별)와 못 하면 그 이유.</summary>
        public static bool CanUpgrade(GridWorld grid, EntityWorld world, EconomyWorld econ, int unitId, string navalUnitId, out string reason)
        {
            reason = string.Empty;
            if (econ == null || !UnitQueries.IsAlive(world, unitId)) { reason = "유닛 없음"; return false; }
            if (NavalUnitId(world, unitId) != NavalUnitDefinition.RaftId) { reason = "뗏목만 업그레이드"; return false; }
            var team = world.Get<Team>(unitId);
            foreach (var u in NavalUnitDefinition.Upgrades)
            {
                if (u.Row.Id != navalUnitId) continue;
                if (!TechSystem.HasUnlock(econ.TechNodes, econ.Tech[team], u.UnlockKey)) { reason = "기술 필요"; return false; }
                if (!CitySystem.IsOwnTerritory(grid, team, world.Get<GridPosition>(unitId).Value)) { reason = "자기 영토 안에서만"; return false; }
                if (econ.Resources[team].Stars < u.Row.Cost) { reason = $"별 부족 ({econ.Resources[team].Stars}/{u.Row.Cost})"; return false; }
                return true;
            }
            reason = "알 수 없는 배";
            return false;
        }

        public static bool Upgrade(GridWorld grid, EntityWorld world, EconomyWorld econ, int unitId, string navalUnitId, List<EconomyLogEntry> log)
        {
            if (!CanUpgrade(grid, world, econ, unitId, navalUnitId, out _)) return false;
            var team = world.Get<Team>(unitId);
            var row = FindNavalRow(navalUnitId);
            var res = econ.Resources[team];
            res.Stars -= row.Cost;
            econ.Resources[team] = res;

            // 이번 턴 이미 움직였거나 행동했으면 그 상태를 유지한다(업그레이드 자체는 행동을 쓰지 않는다).
            bool moved = world.Get<HasMoved>(unitId).Value, acted = world.Get<HasActed>(unitId).Value;
            ApplyNaval(world, unitId, row);
            world.Set(unitId, new HasMoved { Value = moved });
            world.Set(unitId, new HasActed { Value = acted });
            log?.Add(new EconomyLogEntry { Team = team, Kind = EconomyLogKind.Upgrade, Subject = row.Name, Position = world.Get<GridPosition>(unitId).Value, CityIndex = -1 });
            return true;
        }
    }
}
