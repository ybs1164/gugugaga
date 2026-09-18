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
}
