namespace TacticsECS
{
    /// <summary>
    /// 배 유닛 표 — 폴리토피아 위키 Raft/Scout/Rammer/Bomber 문서. 값은 Assets/Resources/NavalUnits.csv(유닛 CSV와 같은 컬럼 +
    /// 업그레이드에 필요한 해금 키 Unlock)이고 GameDataLoader.LoadAll이 채운다. 배는 도시에서 훈련하지 않는다: 육지 유닛이 자기
    /// 항구에 들어가면 뗏목(Raft)이 되고(EmbarkSystem), 뗏목은 자기 영토 안에서 골드를 내고 정찰선/충각선/폭격선으로
    /// 업그레이드한다. 배의 체력은 태운 유닛의 체력 그대로이고, 육지에 내리면 원래 유닛으로 돌아온다(업그레이드는 사라짐).
    ///
    /// 3차(위키 전투 공식 도입) 이후 공격력은 위키 원값 그대로다(예전엔 뺄셈 공식 척도에 맞춰 x2). 행동 태그는 위키 스킬과
    /// 대응: Dash → Charge, Scout → Scout, Splash/Stiff 그대로, 뻣뻣함이 없는 배는 반격(Counter).
    /// </summary>
    public static class NavalUnitDefinition
    {
        public const string RaftId = "raft";

        /// <summary>Resources.Load&lt;TextAsset&gt; 경로 — Assets/Resources/NavalUnits.csv.</summary>
        public const string CsvResourcePath = "NavalUnits";

        /// <summary>CSV의 Id가 RaftId인 행.</summary>
        public static UnitCsvRow Raft = new UnitCsvRow { Id = RaftId, Name = "뗏목", Domain = TerrainType.Water, Actions = ActionType.Move | ActionType.Stiff, Defense = 1, MoveRange = 2, Cost = 0 };

        /// <summary>뗏목에서 업그레이드할 수 있는 배(UnlockKey = 필요 해금 키 — TechTree.csv의 Unit.* 키).</summary>
        public static (UnitCsvRow Row, string UnlockKey)[] Upgrades = new (UnitCsvRow, string)[0];
    }
}
