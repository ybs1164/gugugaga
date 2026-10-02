namespace TacticsECS
{
    /// <summary>
    /// 기술트리 관련 고정 상수. 기본 트리는 Tables/TechSlots + TechTreeLayout + Techs + TechUnlocks를 조합한다.
    /// TechTree.csv는 샌드박스 단일 파일 가져오기/내보내기 호환 형식의 예제다(docs/spec/csv/tech.md).
    /// </summary>
    public static class TechTreeDefinition
    {
        /// <summary>기존 단일 파일 형식 예제 경로(확장자 제외). 기본 게임 데이터 로더는 사용하지 않는다.</summary>
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
