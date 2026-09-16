namespace TacticsECS
{
    /// <summary>
    /// 기술(스킬트리) 노드 식별자. 각 값의 한글 이름/티어/선행 기술/비용/효과는
    /// Data/TechTreeDefinition.cs의 TechNodeData 테이블에 있다 — 이 enum 자체는 순수 식별 태그일 뿐이다.
    /// </summary>
    public enum TechId
    {
        None = 0,

        // ---- 등산 갈래 ----
        Mountaineering, // 등산
        Meditation,     // 명상
        Mining,         // 채광
        Philosophy,     // 철학
        Smithing,       // 제련

        // ---- 채집 갈래 ----
        Gathering,      // 채집
        Shields,        // 방패
        Farming,        // 농사
        Diplomacy,      // 외교
        Construction,   // 건축

        // ---- 기마 갈래 ----
        Riding,         // 기마
        FreeSpirit,     // 자유 영혼
        Roads,          // 도로
        Chivalry,       // 기사도
        Trade,          // 교역

        // ---- 사냥 갈래 ----
        Hunting,        // 사냥
        Forestry,       // 임업
        Archery,        // 궁술
        Mathematics,    // 수학
        Spiritualism,   // 강신술

        // ---- 낚시 갈래 ----
        Fishing,        // 낚시
        Ramming,        // 충파
        Sailing,        // 배 타기
        Aquatics,       // 수생학
        Navigation,     // 항해
    }
}
