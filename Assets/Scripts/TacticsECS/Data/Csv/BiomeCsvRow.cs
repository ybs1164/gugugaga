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

        /// <summary>가장 가까운 도시(수도 + 지형 전에 위치가 정해진 마을)로부터 이 반경(체비쇼프 거리) 이내를
        /// "Inner"로, 그 밖을 "Outer"로 취급한다(원문 3절의 Inner City = 도시에 인접 = 1). 타일 가중치
        /// BiomeTileEntry.InnerWeight/OuterWeight에만 쓰인다 — 자원의 Inner/Outer는 항상 거리 1/2 고정.</summary>
        public int InnerRadius;

        /// <summary>산 스폰 배수(Polytopia 종족 배수와 같은 의미, docs/PolytopiaMapGeneration.md 4/12.1절).
        /// 1.0이면 기준값(육지의 14%가 산), 0이면(비워두면) 산 없음 — 기존 CSV와 호환되도록 0이 기본값이다.</summary>
        public float MountainRate;

        /// <summary>숲 스폰 배수. 1.0이면 기준값(육지의 38%가 숲, 산 배수 적용 후 비례 보정), 0이면 숲 레이어
        /// 없음(CSV 타일 목록에 직접 넣은 "Forest" 엔트리는 이와 무관하게 그대로 동작).</summary>
        public float ForestRate;

        public List<BiomeTileEntry> Tiles = new List<BiomeTileEntry>();

        /// <summary>타일 자체가 아니라 타일 "위에" 얹히는 구조물(Post-terrain 마을/자원/유적/불가사리) 생성 규칙 —
        /// docs/PolytopiaMapGeneration.md 3/7/9/10절. 수도(Capital)와 등대(Lighthouse)는 이 목록이 아니라
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

    /// <summary>타일 위에 얹히는 구조물(마을/자원/유적/불가사리) 하나의 생성 규칙. BiomeTileEntry와
    /// 같은 제약 어휘(Weight/MinCount/CountPerTiles/MinDistance/EdgeMargin)를 재사용하되, TerrainType
    /// 대신 "어떤 TileTypeId 위에만 놓일 수 있는가"(AllowedTileTypes)를 갖는다. 엔트리는 성격에 따라
    /// StructureGenerationSystem의 서로 다른 단계에서 처리된다(docs/PolytopiaMapGeneration.md 5절 순서):
    /// (1) 도시(Village) + FillRemaining -> 마을 단계(포화 채우기), (2) InnerRate/OuterRate가 있는 엔트리 ->
    /// 자원 단계(모든 도시 2칸 이내, 비율 쿼터), (3) 나머지 -> 유적/불가사리 단계(맵 전체 목표 개수).</summary>
    public struct BiomeStructureEntry
    {
        public string StructureId;

        /// <summary>이 구조물이 놓일 수 있는 TileTypeId 목록(파이프 구분). 비어있으면 어떤 타일에도
        /// 놓이지 않는다(오타 방지 — "전체 허용"을 원하면 그 바이옴의 모든 TileId를 명시해야 한다).
        /// 깊은 바다는 "Ocean"(TerrainGenerationSystem.OceanTileId), 얕은 물은 바이옴의 물 타일 Id다.</summary>
        public string[] AllowedTileTypes;

        /// <summary>같은 칸을 두고 여러 개수 기반 엔트리가 경쟁할 때의 가중치(자원 단계에서는 무시).</summary>
        public float Weight;
        public int MinCount;

        /// <summary>0보다 크면 "이 바이옴 영역 안에서 AllowedTileTypes에 해당하는 칸 수 / 이 값"
        /// 개수만큼(반올림) 목표를 늘린다 — 예를 들어 Starfish는 AllowedTileTypes=Water|Ocean이므로 이 값이
        /// "물 타일 몇 칸당 하나"를 뜻하게 된다(Polytopia의 "물 타일 25칸당 불가사리 1개" 규칙). 바이옴별
        /// 목표를 맵 전체로 합산해 한 번에 배치한다. MinCount와 비교해 더 큰 쪽을 쓴다. 단 "Ruin"은
        /// 원문의 맵 크기별 고정 개수표를 쓴다(StructureGenerationSystem.RuinCounts).</summary>
        public float CountPerTiles;

        /// <summary>같은 StructureId끼리 유지해야 하는 최소 거리(체비쇼프, 맵 전체 기준). 0이면 제약 없음.</summary>
        public int MinDistance;
        public int EdgeMargin;

        /// <summary>0보다 크면 가장 가까운 도시(수도/마을)로부터 이 거리(체비쇼프) 이내에만 배치 가능.
        /// 0이면 제약 없음. 자원(InnerRate/OuterRate) 엔트리는 이 값과 무관하게 항상 원문대로 도시 2칸 이내다.</summary>
        public int MaxDistanceFromCity;

        /// <summary>Lakes 맵에서만 적용되는, 이 구조물이 물(TerrainType.Water) 위에 놓이는 비율의 상한(0~1).
        /// 예: 0.34 -> 최대 약 1/3까지만 물 위(원문 9절 "Lakes 맵은 유적 최대 1/3만 물 위" — 다른 맵 타입은
        /// 제한 없음). null(비워두면) = 제약 없음. 0f를 "제약 없음"으로 쓰면 코드로 직접 만든 엔트리에서
        /// "물에는 0%만 허용"으로 오인되는 함정이 있어 nullable로 둔다.</summary>
        public float? MaxWaterFractionOnLakes;

        /// <summary>true면 MinCount/CountPerTiles로 정한 목표 개수를 무시하고, 제약을 만족하는 칸이 남지
        /// 않을 때까지 계속 배치한다 — Polytopia의 "Post-terrain 마을: 더 이상 넣을 자리가 없을 때까지 채움"
        /// 규칙(7.4절)을 일반화한 것.</summary>
        public bool FillRemaining;

        /// <summary>이 구조물과 바로 인접(8방향, 대각선 포함)할 수 없는 다른 StructureId 목록(파이프 구분) —
        /// 예: 유적은 "Capital|Village"(원문 9절 "마을과 바로 인접 불가"), 불가사리는
        /// "Capital|Village|Lighthouse"(10절). 비어있으면 제약 없음.</summary>
        public string[] ExcludeAdjacentStructures;

        /// <summary>자원 엔트리 전용(둘 중 하나라도 0보다 크면 자원으로 취급) — 도시(수도/마을)에 바로 인접한
        /// 칸(Inner, 거리 1)과 거리 2인 칸(Outer) 중, AllowedTileTypes에 해당하는 칸의 몇 비율(0~1)을 이
        /// 자원으로 채울지. 원문 3절 표를 지형별 조건부 비율로 바꾼 값이다(예: 과일 = 평지 Inner 18%/48% =
        /// 0.375, Outer 6%/48% = 0.125). 같은 타일 타입을 공유하는 자원이 여럿이면 CSV 순서대로 쿼터를 떼어 간다.</summary>
        public float InnerRate;
        public float OuterRate;
    }
}
