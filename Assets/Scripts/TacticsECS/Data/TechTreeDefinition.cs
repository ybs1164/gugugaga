namespace TacticsECS
{
    /// <summary>
    /// 기술트리 25개 노드(5갈래 x 1+2+2티어)의 고정 정의 테이블. 폴리토피아 기반 초안 — Effect 텍스트는
    /// 기획 문서를 그대로 옮긴 요약이고, 실제 게임플레이 효과 대부분은 그 대상 시스템(건물/유닛 훈련/
    /// 지형 보너스) 자체가 아직 없어 구현되지 않은 플레이스홀더다(Systems/TechSystem.cs 참고). Cost는
    /// 정식 밸런싱 전의 임시값(티어가 오를수록 비쌈)이다.
    /// 표 순서가 UIPrefabSetup.GenerateTechTreePanel의 배치 순서를 겸하므로, 갈래 안에서는 1티어 ->
    /// 2티어(Slot 0, 1) -> 3티어(Slot 0, 1) 순서를 지킨다.
    /// </summary>
    public static class TechTreeDefinition
    {
        public const int Tier1Cost = 3;
        public const int Tier2Cost = 5;
        public const int Tier3Cost = 8;

        public static readonly TechNodeData[] Nodes =
        {
            // ---- 등산 ----
            new TechNodeData { Id = TechId.Mountaineering, Branch = TechId.Mountaineering, ParentId = TechId.None, Tier = 1, Slot = 0, Name = "등산", Cost = Tier1Cost, Effect = "산 타일 진입/이동 가능. 숨겨진 광맥 발견. 산 타일 유닛 방어력 증가." },
            new TechNodeData { Id = TechId.Meditation, Branch = TechId.Mountaineering, ParentId = TechId.Mountaineering, Tier = 2, Slot = 0, Name = "명상", Cost = Tier2Cost, Effect = "산악 신전 건설 가능." },
            new TechNodeData { Id = TechId.Mining, Branch = TechId.Mountaineering, ParentId = TechId.Mountaineering, Tier = 2, Slot = 1, Name = "채광", Cost = Tier2Cost, Effect = "광산 건설 가능." },
            new TechNodeData { Id = TechId.Philosophy, Branch = TechId.Mountaineering, ParentId = TechId.Meditation, Tier = 3, Slot = 0, Name = "철학", Cost = Tier3Cost, Effect = "모든 기술 개발 비용 30% 감소. 현자 훈련 가능." },
            new TechNodeData { Id = TechId.Smithing, Branch = TechId.Mountaineering, ParentId = TechId.Mining, Tier = 3, Slot = 1, Name = "제련", Cost = Tier3Cost, Effect = "대장간 건설 가능. 검사 훈련 가능." },

            // ---- 채집 ----
            new TechNodeData { Id = TechId.Gathering, Branch = TechId.Gathering, ParentId = TechId.None, Tier = 1, Slot = 0, Name = "채집", Cost = Tier1Cost, Effect = "과일 채집 가능. 숨겨진 농작물 발견." },
            new TechNodeData { Id = TechId.Shields, Branch = TechId.Gathering, ParentId = TechId.Gathering, Tier = 2, Slot = 0, Name = "방패", Cost = Tier2Cost, Effect = "방패병 훈련 가능." },
            new TechNodeData { Id = TechId.Farming, Branch = TechId.Gathering, ParentId = TechId.Gathering, Tier = 2, Slot = 1, Name = "농사", Cost = Tier2Cost, Effect = "농장 건설 가능." },
            new TechNodeData { Id = TechId.Diplomacy, Branch = TechId.Gathering, ParentId = TechId.Shields, Tier = 3, Slot = 0, Name = "외교", Cost = Tier3Cost, Effect = "망토 훈련 가능." },
            new TechNodeData { Id = TechId.Construction, Branch = TechId.Gathering, ParentId = TechId.Farming, Tier = 3, Slot = 1, Name = "건축", Cost = Tier3Cost, Effect = "풍차 건설 가능. 자기 건물 파괴 가능." },

            // ---- 기마 ----
            new TechNodeData { Id = TechId.Riding, Branch = TechId.Riding, ParentId = TechId.None, Tier = 1, Slot = 0, Name = "기마", Cost = Tier1Cost, Effect = "기마병 훈련 가능." },
            new TechNodeData { Id = TechId.FreeSpirit, Branch = TechId.Riding, ParentId = TechId.Riding, Tier = 2, Slot = 0, Name = "자유 영혼", Cost = Tier2Cost, Effect = "신전 건설 가능. 자기 유닛 해산 가능." },
            new TechNodeData { Id = TechId.Roads, Branch = TechId.Riding, ParentId = TechId.Riding, Tier = 2, Slot = 1, Name = "도로", Cost = Tier2Cost, Effect = "도로/다리 건설 가능." },
            new TechNodeData { Id = TechId.Chivalry, Branch = TechId.Riding, ParentId = TechId.FreeSpirit, Tier = 3, Slot = 0, Name = "기사도", Cost = Tier3Cost, Effect = "기사 훈련 가능. 자기 숲 화전 가능." },
            new TechNodeData { Id = TechId.Trade, Branch = TechId.Riding, ParentId = TechId.Roads, Tier = 3, Slot = 1, Name = "교역", Cost = Tier3Cost, Effect = "시장 건설 가능." },

            // ---- 사냥 ----
            new TechNodeData { Id = TechId.Hunting, Branch = TechId.Hunting, ParentId = TechId.None, Tier = 1, Slot = 0, Name = "사냥", Cost = Tier1Cost, Effect = "동물 채집 가능. 숨겨진 동물 발견." },
            new TechNodeData { Id = TechId.Forestry, Branch = TechId.Hunting, ParentId = TechId.Hunting, Tier = 2, Slot = 0, Name = "임업", Cost = Tier2Cost, Effect = "벌목장 건설 가능. 자기 숲 벌목 가능." },
            new TechNodeData { Id = TechId.Archery, Branch = TechId.Hunting, ParentId = TechId.Hunting, Tier = 2, Slot = 1, Name = "궁술", Cost = Tier2Cost, Effect = "궁수 훈련 가능. 숲 타일 유닛 방어력 증가." },
            new TechNodeData { Id = TechId.Mathematics, Branch = TechId.Hunting, ParentId = TechId.Forestry, Tier = 3, Slot = 0, Name = "수학", Cost = Tier3Cost, Effect = "제재소 건설 가능. 투석기 훈련 가능." },
            new TechNodeData { Id = TechId.Spiritualism, Branch = TechId.Hunting, ParentId = TechId.Archery, Tier = 3, Slot = 1, Name = "강신술", Cost = Tier3Cost, Effect = "숲 신전 건설 가능. 자기 평지에 숲 생성 가능." },

            // ---- 낚시 ----
            new TechNodeData { Id = TechId.Fishing, Branch = TechId.Fishing, ParentId = TechId.None, Tier = 1, Slot = 0, Name = "낚시", Cost = Tier1Cost, Effect = "항구 건설 가능. 물고기 채집 가능. 숨겨진 물고기 발견." },
            new TechNodeData { Id = TechId.Ramming, Branch = TechId.Fishing, ParentId = TechId.Fishing, Tier = 2, Slot = 0, Name = "충파", Cost = Tier2Cost, Effect = "충각선 업그레이드 가능." },
            new TechNodeData { Id = TechId.Sailing, Branch = TechId.Fishing, ParentId = TechId.Fishing, Tier = 2, Slot = 1, Name = "배 타기", Cost = Tier2Cost, Effect = "정찰선 업그레이드 가능. 원양 타일 진입/이동 가능." },
            new TechNodeData { Id = TechId.Aquatics, Branch = TechId.Fishing, ParentId = TechId.Ramming, Tier = 3, Slot = 0, Name = "수생학", Cost = Tier3Cost, Effect = "해양 신전 건설 가능. 근해/원양 타일 유닛 방어력 증가." },
            new TechNodeData { Id = TechId.Navigation, Branch = TechId.Fishing, ParentId = TechId.Sailing, Tier = 3, Slot = 1, Name = "항해", Cost = Tier3Cost, Effect = "전투함 업그레이드 가능. 유물 인양 가능. 숨겨진 유물 발견." },
        };

        /// <summary>표시 순서(=위 배열에서 각 갈래가 처음 등장하는 순서)와 같은 갈래(1티어 루트) 목록.
        /// UIPrefabSetup.GenerateTechTreePanel이 열(가로) 순서를 정할 때 쓴다.</summary>
        public static readonly TechId[] BranchOrder =
        {
            TechId.Mountaineering, TechId.Gathering, TechId.Riding, TechId.Hunting, TechId.Fishing,
        };
    }
}
