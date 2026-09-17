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

        public List<BiomeTileEntry> Tiles = new List<BiomeTileEntry>();
    }

    /// <summary>바이옴 하나에 속한 타일 타입 하나의 생성 규칙. Weight(확률 계수)/MinCount(최소 개수)/
    /// ExcludeAdjacent(인접 배제 규칙)는 전부 사용자가 대화로 확정한 3가지 요구사항 그대로다.</summary>
    public struct BiomeTileEntry
    {
        public string TileId;

        /// <summary>이 타일이 이동 판정상 육지/물 중 무엇으로 취급되는지(Core/TerrainType.cs). 타일
        /// 타입이 늘어나도 PathfindingSystem 등 이동 로직은 이 값 하나만 보고 그대로 동작한다.</summary>
        public TerrainType TerrainType;

        public float Weight;
        public int MinCount;

        /// <summary>이 타일과 인접(상하좌우)할 수 없는 다른 TileId 목록. 비어있으면 제약 없음.</summary>
        public string[] ExcludeAdjacent;
    }
}
