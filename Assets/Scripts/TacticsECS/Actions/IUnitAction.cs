namespace TacticsECS
{
    /// <summary>
    /// 유닛이 쓸 수 있는 행동 하나. 어떤 행동인지, 지금 쓸 수 있는지는 이 인터페이스가 노출하는
    /// 메서드(행동)로 판단한다 — ActionType 값을 비교해서 판단하지 않는다("값보다 행동으로 구분").
    ///
    /// 유닛이 실제로 갖는 행동의 집합은 UnitDefinition.actions(View, 프리팹에서 구성)이고, UnitSpawner가
    /// 그 목록을 그대로 EntityWorld의 UnitActions 컴포넌트(Core/UnitComponents.cs)로 옮긴다. Systems
    /// (MovementSystem/CombatSystem/AbilitySystem)는 UnitActionQueries.Find&lt;T&gt;로 필요한 행동을 찾아
    /// CanExecute/Execute를 호출할 뿐, 그 행동이 실제로 무엇을 하는지는 전혀 모른다 — 판단과 효과는
    /// 전부 행동 자신(MoveAction/AttackAction/DefendAction/HealAction/SelfDestructAction/CounterAction,
    /// 이 폴더)이 갖는다.
    ///
    /// 행동마다 필요한 매개변수 모양이 달라(이동은 목적지, 공격은 대상, 방어/치유/자폭은 자기 자신만)
    /// 실제 실행 메서드는 이 인터페이스가 아니라 하위 인터페이스(IMoveAction/ISelfAction/ITargetedAction)
    /// 에서 각각 정의한다.
    /// </summary>
    public interface IUnitAction
    {
        /// <summary>이 행동이 어떤 ActionType 태그에 대응하는지. 실행 가능 여부 판단에는 쓰이지 않고,
        /// CSV 내보내기/불러오기 같은 외부 데이터 연동과 View(BattleHud 아이콘 매칭) 표시 용도로만 쓰인다
        /// (ActionType 자체가 지금은 플레이스홀더 — Core/ActionType.cs 참고).</summary>
        ActionType GetActionType();

        /// <summary>지금 이 유닛이 이 행동을 쓸 수 있는 상태인지(생존 여부, 이번 턴 이미 이동/행동했는지 등
        /// 행동마다 다른 조건)를 행동 스스로 판단한다. 대상이 필요한 행동(공격 등)의 사거리/팀처럼 대상별
        /// 조건은 여기 포함되지 않고 각 하위 인터페이스의 Execute 쪽에서 판단한다.</summary>
        bool CanExecute(EntityWorld world, int unitId);
    }
}
