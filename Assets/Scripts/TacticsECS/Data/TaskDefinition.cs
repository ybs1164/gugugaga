namespace TacticsECS
{
    /// <summary>
    /// 과업 표 — 값은 Assets/Resources/Tasks.csv. 기준: 폴리토피아 위키 Buildings 문서 Monuments 표와 각 기념물 문서(Altar of Peace, Emperor's Tomb ...).
    /// 기념물 자체(인구 +3/점수 400)는 BuildingDefinition의 TaskId 행.
    /// Cymanti 전용 과업(Parasite)은 해당 부족이 없어 제외했다. 교역망(Network)은 위키 Technology/Roads 문서대로 도로 기술이
    /// 연다(Task.Network) — Grand Bazaar 문서의 "도시를 수도에 연결하면 개방"도 도로 없이는 사실상 불가능해 같은 결과.
    /// </summary>
    public static class TaskDefinition
    {
        public const string Pacifist = "Pacifist";
        public const string Wealth = "Wealth";
        public const string Explorer = "Explorer";
        public const string Killer = "Killer";
        public const string Network = "Network";
        public const string Metropolis = "Metropolis";
        public const string Genius = "Genius";

        /// <summary>Resources.Load&lt;TextAsset&gt; 경로 — Assets/Resources/Tasks.csv.</summary>
        public const string CsvResourcePath = "Tasks";

        /// <summary>과업 표 — Tasks.csv를 GameDataLoader.LoadAll이 채운다. 달성 판정 방식(Kind)만 코드(TaskSystem.IsMet)에 있다.</summary>
        public static TaskInfo[] All = new TaskInfo[0];
    }
}
