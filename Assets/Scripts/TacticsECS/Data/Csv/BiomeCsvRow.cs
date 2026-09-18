using System.Collections.Generic;

namespace TacticsECS
{
    /// <summary>
    /// CSV 한 행(= 바이옴 하나)을 그대로 옮겨 담는 값 타입. UnitCsvRow와 같은 이유로 아무 로직도 갖지
    /// 않는다 — 파싱/작성은 Systems/Csv/BiomeCsvSerializer.cs, 실제 생성은 Systems/TerrainGenerationSystem.cs가
    /// 담당한다.
    /// </summary>
    public class BiomeCsvRow
    {
        public string Id;
        public string Name;

        /// <summary>v1은 "Perlin"만 지원한다(Systems/NoiseSystem.cs). 값 자체는 그대로 보존/왕복되므로
        /// 나중에 다른 노이즈 타입을 추가해도 기존 CSV 파일들의 이 컬럼은 안 깨진다.</summary>
        public string NoiseType = "Perlin";

        public float Frequency;
        public int Octaves;

        /// <summary>같은 바이옴 안에서도 타일 엔트리마다 서로 다른 노이즈장을 쓰게 해주는 오프셋
        /// (TerrainGenerationSystem 참고) — 전역 시드와는 별개로, 바이옴 하나가 "자기 노이즈"를 갖도록
        /// CSV로 직접 지정하는 값이다.</summary>
        public int SeedOffset;

        /// <summary>이 바이옴의 앵커(쿼드런트 내 랜덤 지점, Polytopia의 "수도" 역할 —
        /// TerrainGenerationSystem.AssignBiomeRegions 참고)로부터 이 반경(체비쇼프 거리) 이내를
        /// "Inner"로, 그 밖을 "Outer"로 취급한다. BiomeTileEntry.InnerWeight/OuterWeight 참고.</summary>
        public int InnerRadius;

        public List<BiomeTileEntry> Tiles = new List<BiomeTileEntry>();

        /// <summary>타일 자체가 아니라 타일 "위에" 얹히는 구조물(자원/유적/불가사리) 생성 규칙 —
        /// docs/PolytopiaMapGeneration.md 3/9/10절. 수도(Capital)는 이 목록이 아니라 바이옴 앵커에
        /// 자동으로 배치된다(Systems/StructureGenerationSystem.cs 참고).</summary>
        public List<BiomeStructureEntry> Structures = new List<BiomeStructureEntry>();
    }

    /// <summary>바이옴 하나에 속한 타일 타입 하나의 생성 규칙. Polytopia 맵 생성 규칙(docs/
    /// PolytopiaMapGeneration.md) 중 이 프로젝트에 맞게 채택한 것들 — Inner/Outer 이중 확률, 맵 크기
    /// 비례 개수(CountPerTiles), 같은 타입끼리 최소 거리(MinDistance), 가장자리 여백(EdgeMargin),
    /// 인접 배제(ExcludeAdjacent, 1차 구현부터 유지).</summary>
    public struct BiomeTileEntry
    {
        public string TileId;

        /// <summary>이 타일이 이동 판정상 육지/물 중 무엇으로 취급되는지(Core/TerrainType.cs). 타일
        /// 타입이 늘어나도 PathfindingSystem 등 이동 로직은 이 값 하나만 보고 그대로 동작한다.</summary>
        public TerrainType TerrainType;

        /// <summary>바이옴 앵커 기준 Inner/Outer 영역(BiomeCsvRow.InnerRadius)에서 각각 쓰이는 기본
        /// 확률 계수. 노이즈로 한 번 더 보정된다(TerrainGenerationSystem.ComputeWeight).</summary>
        public float InnerWeight;
        public float OuterWeight;

        public int MinCount;

        /// <summary>0보다 크면 "이 바이옴에 배정된 영역 칸 수 / 이 값" 개수만큼(반올림) 자동으로 최소
        /// 개수를 늘린다 — 맵 전체 크기가 아니라 바이옴 영역 크기 기준이라, 바이옴이 몇 개든 밀도가
        /// 일정하게 유지된다. MinCount와 비교해 더 큰 쪽을 쓴다(TerrainGenerationSystem 참고). 0이면 비활성.</summary>
        public float CountPerTiles;

        /// <summary>같은 TileId끼리 유지해야 하는 최소 거리(체비쇼프). 0이면 제약 없음.</summary>
        public int MinDistance;

        /// <summary>맵 가장자리로부터 최소 이 거리 이상 떨어진 칸에만 배치 가능. 0이면 제약 없음.</summary>
        public int EdgeMargin;

        /// <summary>이 타일과 인접(상하좌우)할 수 없는 다른 TileId 목록. 비어있으면 제약 없음.</summary>
        public string[] ExcludeAdjacent;
    }

    /// <summary>타일 위에 얹히는 구조물(수도/유적/자원/불가사리/마을) 하나의 생성 규칙. BiomeTileEntry와
    /// 같은 제약 어휘(Weight/MinCount/CountPerTiles/MinDistance/EdgeMargin)를 재사용하되, TerrainType
    /// 대신 "어떤 TileTypeId 위에만 놓일 수 있는가"(AllowedTileTypes)를 갖는다 — 구조물은 이동 판정을
    /// 바꾸지 않는 순수 시각 요소라 TerrainType이 필요 없다. docs/PolytopiaMapGeneration.md 3/9/10/11절
    /// 갭 보강(자원-수도 근접 제약/구조물 간 인접 배제/포화 채우기/유적 물 비율 상한)으로 4개 필드 추가.</summary>
    public struct BiomeStructureEntry
    {
        public string StructureId;

        /// <summary>이 구조물이 놓일 수 있는 TileTypeId 목록(파이프 구분). 비어있으면 어떤 타일에도
        /// 놓이지 않는다(오타 방지 — "전체 허용"을 원하면 그 바이옴의 모든 TileId를 명시해야 한다).</summary>
        public string[] AllowedTileTypes;

        public float Weight;
        public int MinCount;

        /// <summary>0보다 크면 "이 바이옴 영역 안에서 AllowedTileTypes에 해당하는 칸 수 / 이 값"
        /// 개수만큼(반올림) 목표를 늘린다 — 예를 들어 Starfish는 AllowedTileTypes=Water이므로 이 값이
        /// "물 타일 몇 칸당 하나"를 뜻하게 된다(Polytopia의 "물 타일 25칸당 불가사리 1개" 규칙과 같은
        /// 기준). MinCount와 비교해 더 큰 쪽을 쓴다.</summary>
        public float CountPerTiles;

        public int MinDistance;
        public int EdgeMargin;

        /// <summary>0보다 크면 바이옴 앵커(수도 위치)로부터 이 거리(체비쇼프) 이내에만 배치 가능 —
        /// Polytopia의 "자원은 항상 도시/마을 2칸 이내에서만 스폰" 규칙(3절). 0이면 제약 없음.</summary>
        public int MaxDistanceFromAnchor;

        /// <summary>이 구조물이 물(TerrainType.Water) 타일에 배치되는 비율의 상한(0~1). 예: 0.34 ->
        /// 이 구조물 중 최대 약 1/3까지만 물 위에 배치(Polytopia의 "Lakes 맵은 유적 최대 1/3만 물 위"
        /// 규칙, 9절). null(비워두면/설정 안 하면) = 제약 없음. 일부러 float?(nullable)로 뒀다 — 0f를
        /// "제약 없음"의 기본값으로 쓰면 CSV를 거치지 않고 이 구조체를 직접 만들 때(코드/테스트) 아무
        /// 값도 안 넣은 게 곧 "물에는 무조건 0%만 허용"으로 오인되는 함정이 있었다(구조물 갭 보강 중
        /// 발견/수정 — Starfish가 항상 물 위에도 못 놓이던 버그).</summary>
        public float? MaxWaterFraction;

        /// <summary>true면 MinCount/CountPerTiles로 정한 목표 개수를 무시하고, 제약(EdgeMargin/
        /// MinDistance/MaxDistanceFromAnchor/ExcludeAdjacentStructures)을 만족하는 칸이 남지 않을
        /// 때까지 계속 배치한다 — Polytopia의 "Post-terrain 마을: 더 이상 넣을 자리가 없을 때까지 채움"
        /// 규칙(7.4절)을 일반화한 것.</summary>
        public bool FillRemaining;

        /// <summary>이 구조물과 바로 인접(상하좌우)할 수 없는 다른 StructureId 목록(파이프 구분) —
        /// 예를 들어 유적/불가사리가 수도 바로 옆에 놓이지 않게 "Capital"을 지정한다(9/10절 "다른
        /// 유적이나 마을과 바로 인접 불가"). 비어있으면 제약 없음.</summary>
        public string[] ExcludeAdjacentStructures;
    }
}
