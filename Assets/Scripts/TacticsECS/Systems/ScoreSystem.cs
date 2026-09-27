using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 점수 계산 — 폴리토피아 위키 Score 문서(상수와 원문과 다른 점은 Data/ScoreDefinition.cs). 점수는 따로 저장하지 않고
    /// 지금 상태(유닛/영토/탐험/도시/공원/기념물/신전/기술)에서 매번 계산한다 — 유닛이 죽거나 건물이 부서지거나 도시를 잃으면
    /// 자연히 줄어든다(위키: "Points are lost when..."). 순수 함수형, 자체 상태 없음.
    /// </summary>
    public static class ScoreSystem
    {
        /// <summary>신전 레벨(1~5): 건설한 턴 포함 3턴마다 1레벨(위키 Temple 표: 0-2턴 Lv1, 3-5턴 Lv2 ... 12턴+ Lv5).</summary>
        public static int TempleLevel(int currentTurn, int builtTurn) =>
            Mathf.Clamp(1 + Mathf.Max(0, currentTurn - builtTurn) / ScoreDefinition.TempleTurnsPerLevel, 1, ScoreDefinition.TempleMaxLevel);

        public static int TemplePoints(int level) => ScoreDefinition.TempleBase + ScoreDefinition.PerTempleLevelAbove1 * (level - 1);

        public static int Compute(GridWorld grid, EntityWorld world, EconomyWorld econ, Team team) => Total(ComputeBreakdown(grid, world, econ, team));

        public static int Total(ScoreBreakdown b) => b.Units + b.Territory + b.Exploration + b.Cities + b.Parks + b.Monuments + b.Temples + b.Tech;

        public static ScoreBreakdown ComputeBreakdown(GridWorld grid, EntityWorld world, EconomyWorld econ, Team team)
        {
            var b = new ScoreBreakdown();
            if (econ == null) return b;

            for (int id = 0; id < world.EntityCount; id++)
            {
                if (!UnitQueries.IsAlive(world, id) || world.Get<Team>(id) != team) continue;
                var row = CitySystem.FindUnitRow(econ, world.GetOrDefault<UnitTypeId>(id).Value ?? string.Empty);
                if (row != null) b.Units += ScoreDefinition.PerUnitCostStar * row.Cost;
            }

            for (int y = 0; y < grid.Height; y++)
            for (int x = 0; x < grid.Width; x++)
            {
                var p = new Vector2Int(x, y);
                var t = grid.GetTile(p);
                if (grid.FogEnabled && VisionSystem.IsExplored(grid, team, p)) b.Exploration += ScoreDefinition.PerExploredTile;
                if (t.OwnerTeam != (int)team) continue;
                b.Territory += ScoreDefinition.PerTerritoryTile;
                var info = TileImprovementSystem.FindBuilding(t.BuildingId);
                if (info == null) continue;
                if (!string.IsNullOrEmpty(info.Value.TaskId)) b.Monuments += ScoreDefinition.Monument;
                else if (info.Value.IsTemple) b.Temples += TemplePoints(TempleLevel(econ.Turn, t.BuildingTurn));
            }

            foreach (var c in econ.Cities)
            {
                if (c.Owner != team) continue;
                b.Cities += ScoreDefinition.CityBase + ScoreDefinition.PerCityLevelAbove1 * (c.Level - 1) + ScoreDefinition.PerPopulation * Mathf.Max(0, c.Population);
                b.Parks += ScoreDefinition.Park * c.ParkCount;
            }

            foreach (var n in econ.TechNodes)
                if (TechSystem.IsUnlocked(econ.Tech[team], n.Id)) b.Tech += ScoreDefinition.PerTechTier * n.Tier;
            return b;
        }
    }
}
