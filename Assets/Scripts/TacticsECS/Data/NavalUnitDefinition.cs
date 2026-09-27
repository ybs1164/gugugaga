namespace TacticsECS
{
    /// <summary>
    /// 배 유닛 고정 표 — 폴리토피아 위키 Raft/Scout/Rammer/Bomber 문서. 배는 도시에서 훈련하지 않는다: 육지 유닛이 자기
    /// 항구에 들어가면 뗏목(Raft)이 되고(EmbarkSystem), 뗏목은 자기 영토 안에서 골드를 내고 정찰선/충각선/폭격선으로
    /// 업그레이드한다. 배의 체력은 태운 유닛의 체력 그대로이고, 육지에 내리면 원래 유닛으로 돌아온다(업그레이드는 사라짐).
    ///
    /// 원문과 다른 점: 공격력은 이 프로젝트 유닛 CSV의 척도(보병 공격 4 = 위키 전사 공격 2)에 맞춰 위키 값의 2배로 적었다.
    /// 방어/이동/사거리는 위키 값 그대로. 행동 태그는 위키 스킬과 대응: Dash → Charge, Scout → Scout, Splash/Stiff 그대로.
    /// UnlockKey는 TechTree.csv의 Unit.* 키(충파=rammer, 배 타기=scout, 항해=bomber)를 그대로 쓴다.
    /// </summary>
    public static class NavalUnitDefinition
    {
        public const string RaftId = "raft";

        public static readonly UnitCsvRow Raft = new UnitCsvRow
        {
            Id = RaftId, Name = "뗏목", Defense = 1, BaseVisual = string.Empty, Domain = TerrainType.Water,
            Actions = ActionType.Move | ActionType.Stiff, MoveRange = 2, Cost = 0,
        };

        /// <summary>뗏목에서 업그레이드할 수 있는 배(UnitKey = 필요 해금 키).</summary>
        public static readonly (UnitCsvRow Row, string UnlockKey)[] Upgrades =
        {
            (new UnitCsvRow { Id = "scout", Name = "정찰선", Defense = 1, BaseVisual = string.Empty, Domain = TerrainType.Water,
                Actions = ActionType.Move | ActionType.Attack | ActionType.Charge | ActionType.Scout, MoveRange = 3, AttackAttack = 4, AttackRange = 2, Cost = 5 },
             "Unit.scout"),
            (new UnitCsvRow { Id = "rammer", Name = "충각선", Defense = 3, BaseVisual = string.Empty, Domain = TerrainType.Water,
                Actions = ActionType.Move | ActionType.Attack | ActionType.Charge, MoveRange = 3, AttackAttack = 6, AttackRange = 1, Cost = 5 },
             "Unit.rammer"),
            (new UnitCsvRow { Id = "bomber", Name = "폭격선", Defense = 2, BaseVisual = string.Empty, Domain = TerrainType.Water,
                Actions = ActionType.Move | ActionType.Attack | ActionType.Splash | ActionType.Stiff, MoveRange = 2, AttackAttack = 6, AttackRange = 3, Cost = 15 },
             "Unit.bomber"),
        };
    }
}
