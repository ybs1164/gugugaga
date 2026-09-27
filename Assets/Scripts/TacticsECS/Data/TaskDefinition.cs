namespace TacticsECS
{
    /// <summary>
    /// 과업 고정 표 — 폴리토피아 위키 Buildings 문서 Monuments 표와 각 기념물 문서(Altar of Peace, Emperor's Tomb ...).
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

        public static readonly TaskInfo[] All =
        {
            new TaskInfo { Id = Pacifist, Name = "평화주의", Kind = TaskKind.TurnsWithoutAttack, Threshold = 5, UnlockKey = "Task.Pacifist",
                Description = "5턴 연속 공격하지 않기(반격은 공격이 아님)." },
            new TaskInfo { Id = Wealth, Name = "부", Kind = TaskKind.GoldHeld, Threshold = 100, UnlockKey = "Task.Wealth",
                Description = "골드 100을 한 번에 보유." },
            new TaskInfo { Id = Explorer, Name = "탐험가", Kind = TaskKind.AllLighthouses, Threshold = 0, UnlockKey = string.Empty,
                Description = "맵의 등대를 전부 발견." },
            new TaskInfo { Id = Killer, Name = "살육자", Kind = TaskKind.Kills, Threshold = 10, UnlockKey = string.Empty,
                Description = "적 유닛 10기 처치." },
            new TaskInfo { Id = Network, Name = "교역망", Kind = TaskKind.ConnectedCities, Threshold = 5, UnlockKey = "Task.Network",
                Description = "수도와 연결된 도시 5개." },
            new TaskInfo { Id = Metropolis, Name = "대도시", Kind = TaskKind.CityLevel, Threshold = 5, UnlockKey = string.Empty,
                Description = "레벨 5 이상 도시 보유." },
            new TaskInfo { Id = Genius, Name = "천재", Kind = TaskKind.AllTech, Threshold = 0, UnlockKey = "Task.Genius",
                Description = "기술 전부 연구." },
        };
    }
}
