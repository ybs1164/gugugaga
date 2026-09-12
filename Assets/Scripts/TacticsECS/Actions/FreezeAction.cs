namespace TacticsECS
{
    /// <summary>
    /// 빙결 패시브. 순수 마커 — 자기 자신을 Execute하는 동작이 없고, AttackAction.Execute가 공격을
    /// 성사시키고 대상이 살아남았을 때 UnitActionQueries.Find&lt;FreezeAction&gt;로 공격자의 보유 여부만
    /// 확인해 대상에게 Frozen(Core/UnitComponents.cs) 상태를 건다. Frozen은 그 유닛의 다음 자기 팀 턴이
    /// 시작될 때(TurnSystem.ResetUnitStates) 그 턴의 HasMoved/HasActed를 강제로 true로 만들어 통째로
    /// 행동불능으로 소모시키고, 그 즉시 해제된다 — "1턴간 행동불능". 값(수치)은 없다.
    /// </summary>
    [System.Serializable]
    public class FreezeAction : IUnitAction
    {
        public ActionType GetActionType() => ActionType.Freeze;

        public bool CanExecute(EntityWorld world, int unitId) => UnitQueries.IsAlive(world, unitId);
    }
}
