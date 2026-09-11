namespace TacticsECS
{
    /// <summary>
    /// UnitDefinition이 "행동의 집합"(actions 리스트)으로 들고 있는 개별 행동 하나를 정의하는 인터페이스.
    /// MoveAction/AttackAction/DefendAction/HealAction/SelfDestructAction/CounterAction(모두 이 폴더)이
    /// 각각 구현하며, 그 행동에만 필요한 값(사거리, 회복량 등)은 구현체 자신이 들고 있다.
    /// Type은 이 행동이 ActionType의 어떤 비트에 대응하는지를 나타낸다 — UnitDefinition.AvailableActions가
    /// actions 리스트를 훑어 이 값들을 모아 비트마스크로 합치고, 그 비트마스크만 Systems/BattleHud가 본다
    /// (Systems/Data 계층은 여전히 ActionType 비트플래그 기준이며 이 인터페이스를 모른다).
    /// </summary>
    public interface IUnitAction
    {
        ActionType Type { get; }
    }
}
