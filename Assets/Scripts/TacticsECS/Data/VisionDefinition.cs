namespace TacticsECS
{
    /// <summary>
    /// 시야(구름) 규칙 상수 — 폴리토피아 위키 Terrain(Cloud)/Unit Skills(Scout)/Explorer/Lighthouse 문서. 계산은 VisionSystem.
    ///   - 유닛은 주변 3x3(반경 1), 산 위나 정찰(Scout) 스킬이면 5x5(반경 2)를 밝힌다. 한 번 밝힌 칸은 계속 보인다.
    ///   - 구름 칸에는 들어갈 수 없다.
    ///   - 도시를 얻으면 그 영토를 밝힌다. 전투 시작 시 수도 주변 5x5가 보인다.
    ///   - 탐험가(도시 2레벨 보상/유적 보상)는 12번 움직이며(2026-02 패치로 15 → 12) 가장 가까운 구름 쪽으로 간다.
    ///   - 등대를 처음 밝히면 수도 인구 +1.
    /// </summary>
    public static class VisionDefinition
    {
        public const int BaseSightRadius = 1;
        public const int ExtendedSightRadius = 2;
        public const int StartRevealRadius = 2;
        public const int ExplorerMoves = 12;
        public const int ExplorerScanRange = 4;
        public const int LighthousePopulation = 1;

        /// <summary>탐험가가 산/얕은 물/깊은 바다에 들어가려면 필요한 해금 키(위키: 등산/배 타기/항해).</summary>
        public const string ExplorerMountainKey = "Move.Mountain";
        public const string ExplorerShallowWaterKey = "Move.Ocean";
        public const string ExplorerOceanKey = "Connect.Ocean";
    }
}
