namespace TacticsECS
{
    /// <summary>
    /// 기술/도시가 유닛에 주는 지속 효과를 유닛 컴포넌트로 옮겨 적는 순수 함수형 시스템(자체 상태 없음).
    ///   - TerrainAccess: "Move.Mountain"(등산) 없이는 산, "Move.Ocean"(배 타기) 없이는 깊은 바다 진입 불가 —
    ///     PathfindingSystem/MoveAction이 이 컴포넌트만 보고 판정한다(경로 탐색 시그니처에 기술 정보를 넘기지
    ///     않기 위함).
    ///   - PositionalDefenseBonus: 서 있는 칸이 산/숲/물이고 팀이 "Defense.Mountain/Forest/Water"를 가졌으면 +1
    ///     (폴리토피아의 방어 x1.5를 정수 방어력 체계에 맞춘 값), 요새화(FortifyAction) 유닛이 자기 도시 칸에
    ///     있으면 +1(성벽이면 +3). CombatSystem.EffectiveDefense가 더한다.
    /// 위치/기술/도시가 바뀌는 모든 지점(이동, 턴 시작, 연구, 점령, 훈련, 적 턴 종료) 뒤에 BattleController가
    /// 다시 부른다. econ이 null(경제 없는 씬)이면 제한도 보너스도 없는 기본값으로 채운다.
    /// </summary>
    public static class TechEffectSystem
    {
        public const int TerrainDefenseBonus = 1;

        public static void RefreshUnits(GridWorld grid, EntityWorld world, EconomyWorld econ)
        {
            for (int id = 0; id < world.EntityCount; id++)
            {
                if (!UnitQueries.IsAlive(world, id)) continue;

                if (econ == null)
                {
                    world.Set(id, new TerrainAccess { Mountain = true, Ocean = true });
                    world.Set(id, new PositionalDefenseBonus { Value = 0 });
                    continue;
                }

                var team = world.Get<Team>(id);
                var tech = econ.Tech[team];
                var nodes = econ.TechNodes;
                world.Set(id, new TerrainAccess
                {
                    Mountain = !TechSystem.IsKeyGated(nodes, "Move.Mountain") || TechSystem.HasUnlock(nodes, tech, "Move.Mountain"),
                    Ocean = !TechSystem.IsKeyGated(nodes, "Move.Ocean") || TechSystem.HasUnlock(nodes, tech, "Move.Ocean"),
                });

                var pos = world.Get<GridPosition>(id).Value;
                int bonus = 0;
                var cls = TileImprovementSystem.Classify(grid, pos);
                if ((cls == TileClass.Mountain && TechSystem.HasUnlock(nodes, tech, "Defense.Mountain")) ||
                    (cls == TileClass.Forest && TechSystem.HasUnlock(nodes, tech, "Defense.Forest")) ||
                    ((cls == TileClass.ShallowWater || cls == TileClass.Ocean) && TechSystem.HasUnlock(nodes, tech, "Defense.Water")))
                    bonus += TerrainDefenseBonus;

                int city = CitySystem.FindCityAt(econ, pos);
                if (city >= 0 && econ.Cities[city].Owner == team && UnitActionQueries.Find<FortifyAction>(world, id) != null)
                    bonus += econ.Cities[city].HasWall ? CitySystem.WallDefenseBonus : CitySystem.CityDefenseBonus;

                world.Set(id, new PositionalDefenseBonus { Value = bonus });
            }
        }

        /// <summary>tile이 특수 지형(산/깊은 바다)이라 unitId의 TerrainAccess로 들어갈 수 없는지.</summary>
        public static bool IsTerrainLocked(GridWorld grid, EntityWorld world, int unitId, UnityEngine.Vector2Int tile)
        {
            var access = world.Get<TerrainAccess>(unitId);
            var type = grid.GetTileType(tile);
            if (type == TerrainGenerationSystem.MountainTileId && !access.Mountain) return true;
            if (type == TerrainGenerationSystem.OceanTileId && !access.Ocean) return true;
            return false;
        }
    }
}
