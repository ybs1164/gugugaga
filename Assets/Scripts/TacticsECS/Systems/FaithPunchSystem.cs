using System.Collections.Generic;
using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 신앙 펀치(신앙 포인트를 쓰는 액티브 스킬). 펀치 하나는 FaithEffects 행 여럿이고, 효과마다의 대상·적용은 FaithEffectSystem이 한다.
    /// 절차: 목록(Available) → 대상 선택 → 검사(Check) → 적용(Apply, 신앙 소모 후 효과). 모든 효과의 대상은 발동 전 상태로 정한다.
    /// 자체 상태 없이 월드를 읽고 쓴다. 규칙: docs/spec/game/faith.md
    /// </summary>
    public static class FaithPunchSystem
    {
        /// <summary>팀이 고른 신이 쓸 수 있는 펀치들.</summary>
        public static List<FaithPunchRow> Available(EconomyWorld econ, Team team)
        {
            var list = new List<FaithPunchRow>();
            var god = TemplePassiveSystem.GodOf(econ, team);
            if (god == null || god.Value.Punches == null) return list;
            foreach (int i in god.Value.Punches)
                if (i >= 0 && i < GameTables.FaithPunches.Length) list.Add(GameTables.FaithPunches[i]);
            return list;
        }

        public static List<FaithEffectRow> Effects(FaithPunchRow punch) => FaithEffectSystem.Rows(punch.Effects);

        public static bool Has(FaithPunchRow punch, FaithEffectKind kind) => Effects(punch).Exists(e => e.Effect == kind);

        /// <summary>대상 칸 중심 체비쇼프 Range 안의 칸들(맵 안만).</summary>
        public static List<Vector2Int> Area(GridWorld grid, FaithPunchRow punch, Vector2Int center) => FaithEffectSystem.Area(grid, center, punch.Range);

        /// <summary>대상으로 쓸 수 없으면 이유 문구 키, 쓸 수 있으면 null. 신앙이 모자란 경우도 포함한다.
        /// 효과 중 하나라도 대상이 있으면 쓸 수 있고, 하나도 없으면 첫 효과의 이유를 돌려준다.</summary>
        public static string Check(GridWorld grid, EntityWorld world, EconomyWorld econ, Team team, FaithPunchRow punch, Vector2Int target)
        {
            if (econ.Resources[team].Faith < punch.Cost) return "UI.FaithPunch.NeedFaith";
            if (!grid.InBounds(target) || (grid.FogEnabled && !VisionSystem.IsExplored(grid, team, target))) return "UI.FaithPunch.NotExplored";
            string first = null;
            foreach (var e in Effects(punch))
            {
                string reason = Reason(grid, world, econ, team, punch, e, target);
                if (reason == null) return null;
                first = first ?? reason;
            }
            return first ?? "UI.FaithPunch.TileBlocked";
        }

        private static string Reason(GridWorld grid, EntityWorld world, EconomyWorld econ, Team team, FaithPunchRow punch, FaithEffectRow e, Vector2Int target)
        {
            switch (e.Effect)
            {
                case FaithEffectKind.Damage:
                case FaithEffectKind.Heal:
                    if (FaithEffectSystem.Units(grid, world, team, e, target, punch.Range).Count > 0) return null;
                    if (e.Effect == FaithEffectKind.Damage && e.Side == FaithSide.Enemy) return "UI.FaithPunch.NoEnemy";
                    if (e.Effect == FaithEffectKind.Heal && e.Side == FaithSide.Ally) return "UI.FaithPunch.NoWounded";
                    return "UI.FaithPunch.NoTarget";
                case FaithEffectKind.Structure:
                    return FaithEffectSystem.StructureTiles(grid, team, e, target, punch.Range).Count > 0 ? null : "UI.FaithPunch.NoSpace";
                case FaithEffectKind.Terrain:
                    return FaithEffectSystem.CanChangeTerrain(grid, econ, e, target) ? null : "UI.FaithPunch.TileBlocked";
                case FaithEffectKind.Descend:
                    return MythicSystem.CheckDescend(grid, world, econ, team, target);
                default:
                    return "UI.FaithPunch.TileBlocked";
            }
        }

        /// <summary>효과를 받을 유닛·칸 수(확정 전 미리보기). 같은 유닛·칸은 한 번만 센다.</summary>
        public static int AffectedCount(GridWorld grid, EntityWorld world, Team team, FaithPunchRow punch, Vector2Int target)
        {
            var units = new HashSet<int>();
            var tiles = new HashSet<Vector2Int>();
            foreach (var e in Effects(punch))
                switch (e.Effect)
                {
                    case FaithEffectKind.Damage:
                    case FaithEffectKind.Heal:
                        units.UnionWith(FaithEffectSystem.Units(grid, world, team, e, target, punch.Range));
                        break;
                    case FaithEffectKind.Structure:
                        var free = FaithEffectSystem.StructureTiles(grid, team, e, target, punch.Range);
                        tiles.UnionWith(free.GetRange(0, Mathf.Min(Amount(e), free.Count)));
                        break;
                    default:
                        tiles.Add(target);
                        break;
                }
            return units.Count + tiles.Count;
        }

        /// <summary>펀치 효과 하나의 크기(기도 보너스 없음).</summary>
        public static int Amount(FaithEffectRow e) => Mathf.RoundToInt(FaithEffectSystem.Strength(e, 0));

        /// <summary>신앙을 쓰고 효과를 적용한다. Check가 null이어야 한다.</summary>
        public static FaithPunchResult Apply(GridWorld grid, EntityWorld world, EconomyWorld econ, Team team, FaithPunchRow punch, Vector2Int target)
        {
            var result = new FaithPunchResult { UnitIds = new List<int>(), Tiles = new List<Vector2Int>(), SpawnedIds = new List<int>() };
            if (Check(grid, world, econ, team, punch, target) != null) return result;

            var resources = econ.Resources[team];
            resources = FaithSystem.Add(resources, -punch.Cost);
            econ.Resources[team] = resources;
            result.Spent = punch.Cost;
            result.Success = true;

            // 대상은 모두 발동 전 상태로 정한다(앞 효과가 바꾼 칸·체력을 뒤 효과가 다시 보지 않는다).
            var effects = Effects(punch);
            var units = new List<List<int>>();
            var free = new List<List<Vector2Int>>();
            var usable = new List<bool>();
            foreach (var e in effects)
            {
                units.Add(e.Effect == FaithEffectKind.Damage || e.Effect == FaithEffectKind.Heal ? FaithEffectSystem.Units(grid, world, team, e, target, punch.Range) : null);
                free.Add(e.Effect == FaithEffectKind.Structure ? FaithEffectSystem.StructureTiles(grid, team, e, target, punch.Range) : null);
                usable.Add(e.Effect == FaithEffectKind.Terrain ? FaithEffectSystem.CanChangeTerrain(grid, econ, e, target)
                         : e.Effect == FaithEffectKind.Descend ? MythicSystem.CheckDescend(grid, world, econ, team, target) == null
                         : true);
            }

            var touched = new List<int>();
            for (int i = 0; i < effects.Count; i++)
            {
                var e = effects[i];
                if (!usable[i]) continue;
                switch (e.Effect)
                {
                    case FaithEffectKind.Damage:
                        int before = touched.Count;
                        FaithEffectSystem.Damage(grid, world, econ, team, e, Amount(e), units[i], touched);
                        if (e.CountsAsAttack && touched.Count > before) result.Attacked = true;
                        break;
                    case FaithEffectKind.Heal:
                        FaithEffectSystem.Heal(world, Amount(e), units[i], touched);
                        break;
                    case FaithEffectKind.Structure:
                        FaithEffectSystem.SpawnStructures(grid, e, free[i], Amount(e), result.Tiles);
                        break;
                    case FaithEffectKind.Terrain:
                        FaithEffectSystem.ChangeTerrain(grid, econ, team, e, target);
                        result.Tiles.Add(target);
                        result.TerrainChanged = true;
                        break;
                    case FaithEffectKind.Descend:
                        result.SpawnedIds.Add(MythicSystem.Descend(grid, world, econ, team, target, out int origin));
                        touched.Add(origin);
                        break;
                }
            }
            foreach (int id in touched)
                if (!result.UnitIds.Contains(id)) result.UnitIds.Add(id);
            return result;
        }
    }
}
