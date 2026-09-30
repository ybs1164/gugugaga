using System.Collections.Generic;
using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 게임 값 → 아이콘 이름/짧은 툴팁 문장 매핑(View 전용). Systems가 돌려주는 enum(BlockReason, EconomyLogKind, BattleLogVerb,
    /// TileClass …)을 화면에 어떤 그림으로 보여줄지를 한곳에 모아, 로직 계층에 표시 문구가 섞이지 않게 한다
    /// (docs/UxIconizationPlan.md 4절). CSV에 Icon 칸이 있는 표(건물/타일 행동/보상/과업/해금 내역)는 그 값을 쓰고, 여기에는
    /// CSV로 옮길 필요가 없는 고정 매핑만 둔다. 상태 없음.
    /// </summary>
    public static class MenuIcons
    {
        // ---------- 비활성 이유 ----------

        public static string ForBlock(BlockReason reason)
        {
            switch (reason)
            {
                case BlockReason.NotEnoughGold: return "star";
                case BlockReason.NeedTech: return "lock";
                case BlockReason.OnePerCity: return "city";
                case BlockReason.NeedAdjacent: return "build";
                case BlockReason.OutsideTerritory: return "border";
                case BlockReason.TileOccupied: return "unit";
                case BlockReason.CityFull: return "population";
                case BlockReason.NeedTurnStart: return "turn";
                case BlockReason.None: return null;
                default: return "warning";
            }
        }

        public static Color BlockTint(BlockReason reason) =>
            reason == BlockReason.NotEnoughGold ? UiKit.WarnColor : reason == BlockReason.NeedTech ? UiKit.MutedColor : UiKit.WarnColor;

        /// <summary>툴팁 한 줄. 버튼 위에는 보이지 않고 누르고 있거나(터치) 마우스를 올렸을 때만 나타난다.</summary>
        public static string BlockText(BlockReason reason)
        {
            switch (reason)
            {
                case BlockReason.NotEnoughGold: return "골드 부족";
                case BlockReason.NeedTech: return "기술 필요";
                case BlockReason.OnePerCity: return "도시당 1개";
                case BlockReason.NeedAdjacent: return "인접 건물 필요";
                case BlockReason.OutsideTerritory: return "자기 영토에서만";
                case BlockReason.TileOccupied: return "도시 칸이 막혀 있음";
                case BlockReason.CityFull: return "유닛 수용량 가득";
                case BlockReason.NotOwnCity: return "우리 도시가 아님";
                case BlockReason.NotTrainable: return "훈련 불가";
                case BlockReason.InvalidTarget: return "대상 아님";
                case BlockReason.NeedTurnStart: return "이 칸에서 턴을 시작해야 함";
                default: return string.Empty;
            }
        }

        // ---------- 지형/구조물 ----------

        public static string ForTileClass(TileClass cls)
        {
            switch (cls)
            {
                case TileClass.Forest: return "forest";
                case TileClass.Mountain: return "mountain";
                case TileClass.ShallowWater: return "shallow_water";
                case TileClass.Ocean: return "ocean";
                default: return "field";
            }
        }

        private static readonly Dictionary<string, string> StructureIcons = new Dictionary<string, string>
        {
            ["Capital"] = "crown",
            ["Village"] = "village",
            ["Ruin"] = "ruin",
            ["Resource_Fruit"] = "fruit",
            ["Resource_Crop"] = "crop",
            ["Resource_Animal"] = "animal",
            ["Resource_Metal"] = "metal",
            ["Resource_Fish"] = "fish",
            ["Resource_Food"] = "fruit",
            ["Resource_Ore"] = "metal",
            ["Lighthouse"] = "lighthouse",
            ["Starfish"] = "starfish",
        };

        public static string ForStructure(string structureId) =>
            !string.IsNullOrEmpty(structureId) && StructureIcons.TryGetValue(structureId, out var icon) ? icon : "info";

        public static string ForBuilding(string buildingId)
        {
            var b = TileImprovementSystem.FindBuilding(buildingId);
            return b != null && !string.IsNullOrEmpty(b.Value.Icon) ? b.Value.Icon : "build";
        }

        // ---------- 유닛 ----------

        /// <summary>유닛 전용 초상은 아직 없어서(계획 5절 "유닛 초상" 미구현) 역할에 맞는 기존 아이콘을 고른다.</summary>
        public static string ForUnit(UnitCsvRow row)
        {
            if (row == null) return "unit";
            if (row.Domain == TerrainType.Water) return "ship";
            if (row.HealAmount > 0) return "heal";
            if ((row.Actions & ActionType.Convert) != 0) return "convert";
            if ((row.Actions & ActionType.Splash) != 0) return "splash";
            if (row.AttackRange > 1) return "archery";
            if ((row.Actions & ActionType.Scout) != 0) return "scout";
            if ((row.Actions & ActionType.Infiltrate) != 0) return "infiltrate";
            if (row.MoveRange >= 2) return "riding";
            if (row.Defense >= 3f) return "shields";
            return "unit";
        }

        // ---------- 보상 ----------

        public static string ForReward(CityRewardType type)
        {
            var info = CitySystem.FindReward(type);
            return info != null && !string.IsNullOrEmpty(info.Value.Icon) ? info.Value.Icon : "reward";
        }

        /// <summary>보상 카드에 붙는 효과 칩 하나(아이콘 + 짧은 숫자). 효과 문장(CSV Description)은 카드 툴팁에만 쓴다.</summary>
        public static (string Icon, string Text, Color Tint) RewardEffect(CityRewardType type)
        {
            int amount = CitySystem.RewardAmount(type);
            switch (type)
            {
                case CityRewardType.Workshop:
                case CityRewardType.Park: return ("star", $"+{amount}/턴", UiKit.GoldColor);
                case CityRewardType.Resources: return ("star", $"+{amount}", UiKit.GoldColor);
                case CityRewardType.PopulationGrowth: return ("population", $"+{amount}", UiKit.PopulationColor);
                case CityRewardType.BorderGrowth: return ("border", amount.ToString(), Color.white);
                case CityRewardType.CityWall: return ("defense", $"x{GameRules.Combat.WallDefenseMultiplier:0.#}", Color.white);
                case CityRewardType.Explorer: return ("explorer", GameRules.Vision.ExplorerMoves.ToString(), Color.white);
                case CityRewardType.SuperUnit: return ("unit", "+1", Color.white);
                default: return (null, string.Empty, Color.white);
            }
        }

        // ---------- 기술 해금 ----------

        /// <summary>해금 키("Build.Farm", "Unit.archer" …) → 아이콘/이름/설명. TechUnlocks.csv의 Icon 칸을 쓰고, 표에 없는 키(샌드박스에서 불러온
        /// 커스텀 기술트리 등)는 범주별 기본 아이콘으로 대신한다.</summary>
        public static (string Icon, string Name, string Description) ForUnlock(string key)
        {
            if (GameTables.TechUnlocks != null)
                foreach (var row in GameTables.TechUnlocks)
                    if (TechGroupSystem.Key(row) == key)
                        return (string.IsNullOrEmpty(row.Icon) ? CategoryIcon(row.Category) : row.Icon, row.Name, row.Description);
            int dot = key.IndexOf('.');
            return (CategoryIcon(dot > 0 ? key.Substring(0, dot) : key), key, string.Empty);
        }

        private static string CategoryIcon(string category)
        {
            switch (category)
            {
                case "Build": return "build";
                case "Unit": return "unit";
                case "Move": return "move";
                case "Defense": return "defense";
                case "Reveal": return "explore";
                case "Harvest": return "fruit";
                case "Ability": return "info";
                case "Task": return "task";
                case "Vision": return "scout";
                case "Connect": return "port";
                case "Literacy": return "research";
                default: return "info";
            }
        }

        // ---------- 로그 ----------

        public static string ForVerb(BattleLogVerb verb)
        {
            switch (verb)
            {
                case BattleLogVerb.Move: return "move";
                case BattleLogVerb.Attack: return "attack";
                case BattleLogVerb.Counter: return "counter";
                case BattleLogVerb.Defend: return "guard";
                case BattleLogVerb.Heal: return "heal";
                case BattleLogVerb.SelfDestruct: return "selfdestruct";
                case BattleLogVerb.Wait: return "hp";
                case BattleLogVerb.Defeated: return "defeat";
                case BattleLogVerb.Promote: return "promote";
                default: return "info";
            }
        }

        public static string ForEconomy(EconomyLogKind kind)
        {
            switch (kind)
            {
                case EconomyLogKind.Capture: return "capture";
                case EconomyLogKind.Research: return "research";
                case EconomyLogKind.Build: return "build";
                case EconomyLogKind.Action: return "fruit";
                case EconomyLogKind.Train: return "unit";
                case EconomyLogKind.LevelUp: return "levelup";
                case EconomyLogKind.Reward: return "reward";
                case EconomyLogKind.Explore: return "explore";
                case EconomyLogKind.Disband: return "disband";
                case EconomyLogKind.Discover: return "explorer";
                case EconomyLogKind.Task: return "task";
                case EconomyLogKind.Upgrade: return "ship";
                case EconomyLogKind.StartUnit: return "crown";
                default: return "info";
            }
        }
    }
}
