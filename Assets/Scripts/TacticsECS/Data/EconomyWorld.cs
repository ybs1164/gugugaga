using System.Collections.Generic;

namespace TacticsECS
{
    /// <summary>
    /// 경제(도시/영토/자원/기술) 쪽 게임 상태 저장소 — GridWorld(타일)/EntityWorld(유닛)에 이은 세 번째
    /// 데이터베이스. 필드만 있고 메서드는 없다(CLAUDE.md 규칙 2). 읽고 쓰는 로직은 전부 CitySystem/
    /// CityResourceSystem/TechSystem/TileImprovementSystem/EconomyAI가 담당하고, 보관은 BattleController가 한다.
    /// </summary>
    public class EconomyWorld
    {
        /// <summary>모든 도시(주인이 바뀌어도 제거되지 않는다 — TileData.OwnerCity가 이 인덱스를 가리킨다).</summary>
        public readonly List<CityData> Cities = new List<CityData>();

        /// <summary>팀별 자원(골드/발전도/신앙...).</summary>
        public readonly Dictionary<Team, CityResourceData> Resources = new Dictionary<Team, CityResourceData>();

        /// <summary>팀별 해금 기술.</summary>
        public readonly Dictionary<Team, TechTreeData> Tech = new Dictionary<Team, TechTreeData>();

        /// <summary>한 번이라도 도시를 가졌던 팀 — 도시를 전부 잃으면 패배(폴리토피아 규칙). 처음부터 도시가
        /// 없던 팀(샌드박스에서 수도 없이 배치한 경우)은 예전처럼 유닛 전멸로만 판정한다.</summary>
        public readonly HashSet<Team> HadCity = new HashSet<Team>();

        /// <summary>기술트리 정의(Assets/Resources/TechTree.csv에서 파싱).</summary>
        public List<TechNodeData> TechNodes = new List<TechNodeData>();

        /// <summary>훈련 가능한 유닛 종류(샌드박스에서 불러온 유닛 CSV 행). 비어있으면 훈련 메뉴가 없다.</summary>
        public List<UnitCsvRow> UnitRows = new List<UnitCsvRow>();

        /// <summary>유적 탐험 보상 등 무작위 결과에 쓰는 시드 카운터(호출마다 CitySystem이 1씩 올린다) —
        /// System.Random 인스턴스를 들고 있지 않고 값만 저장해 재현 가능하게 한다.</summary>
        public int RandomCounter;
    }
}
