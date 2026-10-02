namespace TacticsECS
{
    // "배열형 테이블"(docs/spec/csv-common.md)의 행 타입들. 순수 값만 갖는다(CLAUDE.md 규칙 2) — 파싱/작성은
    // Systems/Csv/ArrayTableCsvSerializer, 참조 검사는 Systems/ArrayTableValidationSystem, 사용은 TechGroupSystem/
    // TribeSystem/StartConditionSystem이 한다.
    //
    // 공통 규칙: 모든 행은 Index(= 배열 위치, 0부터)를 갖고, 다른 표를 가리킬 때는 문자열 Id가 아니라 그 표의 Index(정수)를 쓴다.
    // -1은 "없음". 값이 여러 개인 속성은 번호 붙은 반복 컬럼(Unlock1..N)을 읽어 int[]/string[]로 담는다.

    /// <summary>기술을 연구하면 열리는 "해금 내역" 하나(TechUnlocks.csv). Category + "." + Target이 게임 코드가 조회하는 해금 키다
    /// (예: Build + Farm → "Build.Farm"). Target이 비면 Category 자체가 키(예: "Literacy").</summary>
    public enum TechUnlockKind { Passive, Active, UnitProduction, BuildingConstruction, Quest }

    public struct TechUnlockRow
    {
        public int Index;
        public TechUnlockKind Kind;
        public string Category;
        public string Target;
        public string Name;
        public string Description;
    }

    /// <summary>기술 하나(Techs.csv). 위치와 선행 관계는 별도 슬롯 표에 있고, 이 표에는 교체 가능한 기술 내용만 둔다.</summary>
    public struct TechRow
    {
        public int Index;
        public string Id;
        public string Name;

        public string Icon;
        public int CostBase;
        public int CostPerCity;

        /// <summary>TechUnlocks Index 목록.</summary>
        public int[] Unlocks;
        public string Description;
    }

    /// <summary>고정 트리 슬롯(TechSlots.csv). ParentIndex는 TechSlots Index이며 기술을 교체해도 변하지 않는다.</summary>
    public struct TechSlotRow
    {
        public int Index;
        public string Id;
        public int ParentIndex;
        public int Tier;
        public int Slot;
    }

    /// <summary>슬롯별 기술 배치(TechTreeLayout.csv). 각 슬롯에 기술 하나를 배치한다.</summary>
    public struct TechTreeLayoutRow
    {
        public int Index;
        public int SlotIndex;
        public int TechIndex;
    }

    /// <summary>종족 하나가 연구할 수 있는 기술 묶음(TechGroups.csv).</summary>
    public struct TechGroupRow
    {
        public int Index;
        public string Id;
        public string Name;
        public string Description;

        /// <summary>Techs Index 목록 — 여기 없는 기술은 그 종족 트리에 나타나지 않고 연구할 수도 없다.</summary>
        public int[] Techs;
    }

    /// <summary>종족 하나(Tribes.csv).</summary>
    public struct TribeRow
    {
        public int Index;
        public string Id;
        public string Name;
        public string Description;

        /// <summary>바이옴 CSV(Kind=Biome 행 순서, 기본 Resources/Tables/Biomes.csv)의 Index.</summary>
        public int BiomeIndex;

        /// <summary>TechGroups Index.</summary>
        public int TechGroupIndex;

        /// <summary>처음부터 해금된 기술(Techs Index 목록).</summary>
        public int[] StartTechs;
        public int StartStars;

        /// <summary>수도에 받는 시작 유닛 — 유닛 CSV(불러온 파일, 없으면 SandboxUnits.csv) 행 순서의 Index 목록.</summary>
        public int[] StartUnits;

        /// <summary>StartConditions Index. -1 = 규칙 없음.</summary>
        public int StartConditionIndex;
    }

    /// <summary>수도 주변 시작 조건 묶음(StartConditions.csv) — 바이옴 표와 분리된 "수도 주변 스타팅 자원/지형" 규칙 세트.</summary>
    public struct StartConditionRow
    {
        public int Index;
        public string Id;
        public string Name;
        public string Description;

        /// <summary>StartConditionRules Index 목록(적힌 순서대로 적용).</summary>
        public int[] Rules;
    }

    public enum StartRuleKind { Tile, Structure }

    /// <summary>시작 조건 규칙 하나(StartConditionRules.csv): 수도로부터 거리 MinDistance~MaxDistance(체비쇼프) 고리 안에
    /// Target이 최소 Count개 있도록 보장한다 — 모자라면 빈 칸을 골라 바꾼다.</summary>
    public struct StartConditionRuleRow
    {
        public int Index;
        public string Id;

        /// <summary>Tile = 칸의 TileTypeId를 Target으로(+ TerrainType), Structure = 칸 위에 Target 구조물.</summary>
        public StartRuleKind Kind;
        public string Target;

        /// <summary>Tile 규칙: 바뀐 칸의 이동 판정 지형. Structure 규칙: AllowedTiles가 비었을 때 놓일 수 있는 지형.</summary>
        public TerrainType TerrainType;
        public int Count;
        public int MinDistance;
        public int MaxDistance;

        /// <summary>이 맵 타입(습도 프리셋 이름, 예: "Drylands")에서만 적용. 비우면 모든 맵 타입.</summary>
        public string MapType;

        /// <summary>Tile 규칙: 바꿔도 되는 원래 TileTypeId(비우면 도시가 아닌 아무 육지). Structure 규칙: 놓일 수 있는 TileTypeId.</summary>
        public string[] AllowedTiles;
        public string Description;
    }
}
