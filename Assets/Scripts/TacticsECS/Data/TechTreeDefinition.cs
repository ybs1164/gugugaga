namespace TacticsECS
{
    /// <summary>
    /// 기술트리 관련 고정 상수. 노드 목록 자체는 더 이상 코드 표가 아니라 Assets/Resources/TechTree.csv에 있다
    /// (기획자가 스프레드시트로 편집 — docs/TechTreeCsv.md). 런타임에는 BattleController가
    /// Resources.Load로 읽어 TechCsvSerializer.Parse로 파싱하고, EconomyWorld.TechNodes에 담아 쓴다.
    /// </summary>
    public static class TechTreeDefinition
    {
        /// <summary>Resources.Load&lt;TextAsset&gt;에 넘기는 경로(확장자 제외) — Assets/Resources/TechTree.csv.</summary>
        public const string CsvResourcePath = "TechTree";

        /// <summary>중앙 허브(시작 노드, 어떤 기술도 아님)에 쓰는 아이콘 이름.</summary>
        public const string HubIcon = "tech_hub";

        /// <summary>샌드박스 "기술 내보내기"의 기본 파일 이름(유닛 CSV의 SandboxUnits와 같은 역할).</summary>
        public const string SandboxFileName = "SandboxTechTree";

        /// <summary>다른 표(Buildings/TileActions/Tasks/NavalUnits.csv)의 Unlock 칸에서 오지 않고 코드가 직접 확인하는 해금 키.
        /// 커스텀 트리 검증(TechTreeValidationSystem)이 "아는 키"로 인정한다. Unit.* / Reveal.* 는 접두사로 따로 확인한다.</summary>
        public static readonly string[] FixedUnlockKeys =
        {
            "Literacy",                                    // TechSystem.LiteracyKey — 연구 비용 1/3 할인
            "Move.Mountain", "Move.Ocean", "Connect.Ocean", // 산 이동 / 얕은 물 이동 / 깊은 바다 이동·항구 연결
            "Defense.Forest", "Defense.Mountain", "Defense.Water",
            "Vision.Capital",                              // 수도 시야
            "Ability.Disband",                             // 해산
            "Harvest.Starfish",                            // 불가사리 인양
        };
    }
}
