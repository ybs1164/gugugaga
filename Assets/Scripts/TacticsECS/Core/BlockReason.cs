namespace TacticsECS
{
    /// <summary>
    /// 메뉴 선택지(건설/채집/훈련/배 업그레이드)를 지금 쓸 수 없는 이유. 순수 데이터 — Systems(CitySystem.CanTrain,
    /// TileImprovementSystem.GetOptions, EmbarkSystem.CanUpgrade)는 표시 문장 대신 이 값만 돌려주고, 어떤 아이콘/툴팁
    /// 문장으로 보여줄지는 View(MenuIcons)가 정한다 — Polytopia처럼 비활성 버튼을 "회색 + 이유 아이콘"으로 보여주기 위해
    /// 한글 문장을 로직 계층에서 뺐다(docs/UxIconizationPlan.md 4절).
    /// </summary>
    public enum BlockReason
    {
        None,
        /// <summary>골드가 비용보다 적다.</summary>
        NotEnoughGold,
        /// <summary>필요한 기술을 아직 연구하지 않았다.</summary>
        NeedTech,
        /// <summary>도시당 하나만 지을 수 있는 건물이 이미 있다(제재소/풍차/대장간).</summary>
        OnePerCity,
        /// <summary>인접 칸에 기반 건물이 없다(BuildingInfo.AdjacentBuildings).</summary>
        NeedAdjacent,
        /// <summary>자기 영토 밖이다.</summary>
        OutsideTerritory,
        /// <summary>도시 칸에 이미 유닛이 있다.</summary>
        TileOccupied,
        /// <summary>도시의 유닛 수용량이 가득 찼다.</summary>
        CityFull,
        /// <summary>우리 도시가 아니다.</summary>
        NotOwnCity,
        /// <summary>훈련으로는 만들 수 없는 유닛(보상/침투 전용)이다.</summary>
        NotTrainable,
        /// <summary>뗏목이 아니거나 알 수 없는 배 등, 대상 자체가 맞지 않는다.</summary>
        InvalidTarget,
        /// <summary>그 칸에서 턴을 시작해야 한다(점령 — 위키 Capture).</summary>
        NeedTurnStart,
    }
}
