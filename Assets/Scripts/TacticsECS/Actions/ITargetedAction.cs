namespace TacticsECS
{
    /// <summary>다른 유닛 하나를 대상으로 하는 행동. AttackAction/CounterAction이 구현한다.
    /// amount는 대상에게 실제로 입힌 피해량.</summary>
    public interface ITargetedAction : IUnitAction
    {
        bool Execute(GridWorld grid, EntityWorld world, int actorId, int targetId, out int amount);
    }
}
