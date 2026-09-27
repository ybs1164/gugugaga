using System.Collections.Generic;
using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// Assets/Resources의 게임 규칙 CSV를 읽어 Data 계층의 표(BuildingDefinition.All/TileActionDefinition.All ...)에
    /// 채워 넣는 진입점. 전투 화면(BattleController.Awake)과 에디터 검증/시뮬레이션이 시작할 때 한 번씩 부른다 —
    /// 다시 불러도 같은 결과(CSV를 고친 뒤 다시 부르면 새 값). 자체 상태는 없다: 결과는 Data의 정적 필드에만 남는다.
    /// 파일이 없거나 칸이 잘못되면 경고를 남기고 읽을 수 있는 만큼만 채운다(오류 목록을 돌려준다).
    /// </summary>
    public static class GameDataLoader
    {
        /// <summary>Resources.Load&lt;TextAsset&gt; 경로 — Assets/Resources/GameRules.csv(스칼라 규칙 값, Data/GameRules).</summary>
        public const string GameRulesCsvResourcePath = "GameRules";

        public static List<string> LoadAll()
        {
            var errors = new List<string>();
            BuildingDefinition.All = GameTableCsvSerializer.ParseBuildings(Read(BuildingDefinition.CsvResourcePath, errors), errors);
            TileActionDefinition.All = GameTableCsvSerializer.ParseTileActions(Read(TileActionDefinition.CsvResourcePath, errors), errors);
            CityRewardDefinition.All = GameTableCsvSerializer.ParseCityRewards(Read(CityRewardDefinition.CsvResourcePath, errors), errors);
            TaskDefinition.All = GameTableCsvSerializer.ParseTasks(Read(TaskDefinition.CsvResourcePath, errors), errors);
            GameRulesCsvSerializer.Apply(Read(GameRulesCsvResourcePath, errors), errors);
            GameTableCsvSerializer.ParseNavalUnits(Read(NavalUnitDefinition.CsvResourcePath, errors), errors, out var raft, out var upgrades);
            if (raft != null) NavalUnitDefinition.Raft = raft;
            NavalUnitDefinition.Upgrades = upgrades;
            foreach (var e in errors) Debug.LogWarning("[GameData] " + e);
            return errors;
        }

        /// <summary>Assets/Resources/TechTree.csv를 읽어 기술 정의 목록으로 만든다. 파일이 없으면 빈 목록(트리 없음).</summary>
        public static List<TechNodeData> LoadTechNodes()
        {
            var errors = new List<string>();
            var nodes = TechCsvSerializer.Parse(Read(TechTreeDefinition.CsvResourcePath, errors));
            foreach (var e in errors) Debug.LogWarning("[GameData] " + e);
            return nodes;
        }

        private static string Read(string resourcePath, List<string> errors)
        {
            var asset = Resources.Load<TextAsset>(resourcePath);
            if (asset != null) return asset.text;
            errors.Add($"Resources/{resourcePath}.csv를 찾지 못함 — 그 표는 비어 있습니다.");
            return string.Empty;
        }
    }
}
