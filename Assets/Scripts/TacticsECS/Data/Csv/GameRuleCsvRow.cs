namespace TacticsECS
{
    /// <summary>
    /// GameRules.csv 한 행을 그대로 옮겨 담는 값 타입. 파싱·적용은 Systems/Csv/GameRulesCsvSerializer.cs.
    /// </summary>
    public struct GameRuleCsvRow
    {
        public string Key;
        public string Value;
        public string Wiki;
        public string Note;
    }
}
