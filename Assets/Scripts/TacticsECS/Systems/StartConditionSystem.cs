using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

namespace TacticsECS
{
    /// <summary>
    /// 수도 주변 시작 조건(StartConditions.csv + StartConditionRules.csv)을 이미 생성된 지형 위에 적용한다 — 바이옴 표가 영역 전체의
    /// 타일/구조물 비율을 정한다면, 이 표는 "수도 몇 칸 안에 무엇이 최소 몇 개" 같은 개별 보장만 따로 맡는다(바이옴 표와 분리).
    /// 규칙마다: 수도로부터 체비쇼프 거리 MinDistance~MaxDistance 고리에서 Target이 이미 Count개 이상이면 그대로, 모자라면 조건에 맞는
    /// 칸을 무작위로 골라 채운다. 수도/마을 칸과 유닛이 있는 칸은 건드리지 않는다. Tile 규칙이 물을 만들 수 있으므로 호출자는 적용 뒤
    /// TerrainGenerationSystem.ClassifyWaterDepth와 화면 갱신을 한다. 자체 상태는 없다.
    /// </summary>
    public static class StartConditionSystem
    {
        /// <summary>조건 하나를 수도 한 곳에 적용하고 바꾼 칸 수를 돌려준다. mapType은 습도 프리셋 이름(규칙의 MapType 필터용).</summary>
        public static int Apply(GridWorld grid, Vector2Int capital, StartConditionRow condition, StartConditionRuleRow[] rules, string mapType, int seed)
        {
            if (grid == null || condition.Rules == null) return 0;
            var rng = new Random(seed);
            int changed = 0;
            foreach (var ri in condition.Rules)
            {
                if (ri < 0 || ri >= rules.Length) continue;
                changed += ApplyRule(grid, capital, rules[ri], mapType, rng);
            }
            return changed;
        }

        public static bool RuleApplies(StartConditionRuleRow rule, string mapType) =>
            string.IsNullOrEmpty(rule.MapType) || string.Equals(rule.MapType, mapType, System.StringComparison.OrdinalIgnoreCase);

        /// <summary>규칙 하나: 고리 안의 Target 개수를 세고 모자란 만큼 채운다.</summary>
        public static int ApplyRule(GridWorld grid, Vector2Int capital, StartConditionRuleRow rule, string mapType, Random rng)
        {
            if (!RuleApplies(rule, mapType) || string.IsNullOrEmpty(rule.Target)) return 0;
            var ring = Ring(grid, capital, rule.MinDistance, rule.MaxDistance);
            int have = 0;
            foreach (var p in ring) if (Matches(grid, p, rule)) have++;

            var candidates = new List<Vector2Int>();
            foreach (var p in ring) if (!Matches(grid, p, rule) && CanChange(grid, p, rule)) candidates.Add(p);
            ProceduralGenerationUtil.Shuffle(candidates, rng);

            int changed = 0;
            for (int i = 0; i < candidates.Count && have < rule.Count; i++, have++, changed++)
            {
                var p = candidates[i];
                if (rule.Kind == StartRuleKind.Tile)
                {
                    grid.SetTerrain(p, rule.TerrainType);
                    grid.SetTileType(p, rule.Target);
                    if (!string.IsNullOrEmpty(grid.GetStructure(p))) grid.SetStructure(p, string.Empty); // 바뀐 지형에 안 맞는 자원 제거
                }
                else grid.SetStructure(p, rule.Target);
            }
            return changed;
        }

        /// <summary>수도에서 체비쇼프 거리 min~max인 맵 안 칸들.</summary>
        public static List<Vector2Int> Ring(GridWorld grid, Vector2Int center, int min, int max)
        {
            var cells = new List<Vector2Int>();
            for (int dy = -max; dy <= max; dy++)
                for (int dx = -max; dx <= max; dx++)
                {
                    int d = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
                    var p = new Vector2Int(center.x + dx, center.y + dy);
                    if (d >= min && d <= max && grid.InBounds(p)) cells.Add(p);
                }
            return cells;
        }

        /// <summary>규칙의 Target이 이미 이 칸에 있는지.</summary>
        public static bool Matches(GridWorld grid, Vector2Int p, StartConditionRuleRow rule) =>
            rule.Kind == StartRuleKind.Tile ? grid.GetTileType(p) == rule.Target : grid.GetStructure(p) == rule.Target;

        private static bool CanChange(GridWorld grid, Vector2Int p, StartConditionRuleRow rule)
        {
            if (grid.IsOccupied(p)) return false;
            string structure = grid.GetStructure(p);
            if (structure == StructureGenerationSystem.CapitalStructureId || structure == StructureGenerationSystem.VillageStructureId) return false;
            string tile = grid.GetTileType(p);
            bool listed = rule.AllowedTiles != null && rule.AllowedTiles.Length > 0;
            if (rule.Kind == StartRuleKind.Tile)
                return listed ? System.Array.IndexOf(rule.AllowedTiles, tile) >= 0 : grid.GetTerrain(p) == TerrainType.Land;
            if (!string.IsNullOrEmpty(structure)) return false; // 구조물은 빈 칸에만
            return listed ? System.Array.IndexOf(rule.AllowedTiles, tile) >= 0 : grid.GetTerrain(p) == rule.TerrainType;
        }
    }
}
