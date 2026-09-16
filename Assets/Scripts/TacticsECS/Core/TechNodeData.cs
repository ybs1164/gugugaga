namespace TacticsECS
{
    /// <summary>
    /// 기술 노드 하나의 고정 정의(이름/티어/선행 기술/비용/효과 설명). 순수 데이터이며, 이 값들을 채우는
    /// 테이블은 Data/TechTreeDefinition.cs, 해금 판정/소모 로직은 Systems/TechSystem.cs가 맡는다.
    /// </summary>
    public struct TechNodeData
    {
        public TechId Id;

        /// <summary>이 노드가 속한 기술 갈래의 1티어 루트 Id. 루트 자신은 자기 자신을 가리킨다.</summary>
        public TechId Branch;

        /// <summary>선행 기술 Id. 1티어 루트는 TechId.None(선행 기술 없음).</summary>
        public TechId ParentId;

        /// <summary>1~3.</summary>
        public int Tier;

        /// <summary>같은 갈래의 2/3티어가 두 갈래로 나뉠 때 어느 쪽인지(0 또는 1). 3티어 노드는 자신이
        /// 속한 2티어 노드와 같은 값을 가져(트리 UI에서 그 바로 아래에 놓인다), 1티어 루트는 의미 없다(0).</summary>
        public int Slot;

        public string Name;

        /// <summary>해금에 필요한 도시 발전도(CityResourceData.Development). TechTreeDefinition의
        /// Tier1Cost/Tier2Cost/Tier3Cost 참고 — 아직 정식 밸런싱 전의 임시값이다.</summary>
        public int Cost;

        /// <summary>해금 시 열리는 효과 요약(표시용 텍스트). 실제 게임플레이 효과(건물 건설, 유닛 훈련,
        /// 지형 방어 보너스 등)는 대부분 그 대상 시스템 자체가 아직 없어 구현되지 않았다 — TechSystem의
        /// 문서 주석 참고.</summary>
        public string Effect;
    }
}
