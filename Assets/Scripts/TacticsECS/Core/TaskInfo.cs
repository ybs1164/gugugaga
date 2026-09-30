namespace TacticsECS
{
    /// <summary>과업(위키 Monuments의 Task) 종류 — 달성 판정은 TaskSystem.IsMet이 종류별로 계산한다.</summary>
    public enum TaskKind
    {
        /// <summary>Threshold 턴 연속으로 공격하지 않기(평화의 제단).</summary>
        TurnsWithoutAttack,
        /// <summary>골드 Threshold 이상을 한 번에 보유(황제의 무덤).</summary>
        GoldHeld,
        /// <summary>맵의 등대 전부 발견(신의 눈).</summary>
        AllLighthouses,
        /// <summary>적 유닛 Threshold 기 처치(힘의 문).</summary>
        Kills,
        /// <summary>수도와 연결된 도시 Threshold 개(대시장).</summary>
        ConnectedCities,
        /// <summary>레벨 Threshold 이상 도시 보유(행운의 공원).</summary>
        CityLevel,
        /// <summary>기술 전부 연구(지혜의 탑).</summary>
        AllTech,
    }

    /// <summary>과업 하나의 고정 정의. 순수 데이터 — 표는 Data/TaskDefinition.cs.</summary>
    public struct TaskInfo
    {
        public string Id;
        public string Name;
        /// <summary>IconLibrary.Get에 넘기는 아이콘 이름(Assets/Art/GameIcons/Resources/Icons, CSV Icon 칸). 메뉴 버튼/기술 해금 줄에 쓴다.</summary>
        public string Icon;
        public TaskKind Kind;
        public int Threshold;

        /// <summary>비어있지 않으면 이 해금 키(TechTree.csv Unlocks)를 가진 기술이 있어야 과업이 열린다
        /// (위키 "Unlocked by" — 명상/교역/철학).</summary>
        public string UnlockKey;

        public string Description;
    }
}
