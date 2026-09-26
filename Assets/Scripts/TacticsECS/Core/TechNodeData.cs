namespace TacticsECS
{
    /// <summary>
    /// 기술 노드 하나의 고정 정의. 순수 데이터이며, 값은 Assets/Resources/TechTree.csv(기획자가 직접 편집하는
    /// 표)에서 Systems/Csv/TechCsvSerializer가 읽어 채운다 — 해금 판정/비용 계산은 Systems/TechSystem.cs가 맡는다.
    /// Id는 예전 TechId enum 대신 문자열이다: CSV에 행을 추가하는 것만으로 새 기술이 생겨야 하므로 코드에
    /// 기술 이름이 박혀 있으면 안 된다. 기술이 실제로 무엇을 여는지는 Unlocks의 "해금 키"(예: "Build.Farm",
    /// "Unit.shield", "Move.Mountain")로만 표현되고, 각 시스템은 기술 이름이 아니라 그 키를 조회한다
    /// (TechSystem.HasUnlock).
    /// </summary>
    public struct TechNodeData
    {
        public string Id;

        /// <summary>이 노드가 속한 기술 갈래의 1티어 루트 Id. 루트 자신은 자기 자신을 가리킨다.</summary>
        public string Branch;

        /// <summary>선행 기술 Id. 1티어 루트는 빈 문자열(선행 기술 없음).</summary>
        public string ParentId;

        /// <summary>1~3.</summary>
        public int Tier;

        /// <summary>같은 갈래의 2/3티어가 두 갈래로 나뉠 때 어느 쪽인지(0 또는 1). 3티어 노드는 자신이
        /// 속한 2티어 노드와 같은 값을 가져(트리 UI에서 그 바로 바깥에 놓인다), 1티어 루트는 의미 없다(0).</summary>
        public int Slot;

        public string Name;

        /// <summary>IconLibrary.Get에 그대로 넘기는 아이콘 이름(Assets/Art/GameIcons/Resources/Icons).</summary>
        public string Icon;

        /// <summary>폴리토피아 공식 "(티어) x (도시 수) + 4"의 +4 부분. 비용 = CostBase + CostPerCity x 도시 수
        /// (TechSystem.Cost). CSV에서 비워두면 4.</summary>
        public int CostBase;

        /// <summary>도시 하나당 늘어나는 비용. CSV에서 비워두면 Tier 값(폴리토피아 공식 그대로).</summary>
        public int CostPerCity;

        /// <summary>이 기술이 여는 해금 키 목록(CSV에서는 세미콜론 구분). 키 목록과 의미는
        /// docs/TechTreeCsv.md 참고.</summary>
        public string[] Unlocks;

        /// <summary>해금 시 열리는 효과 요약(표시용 텍스트).</summary>
        public string Effect;
    }
}
