namespace TacticsECS
{
    [System.Serializable]
    public class StompAction : IUnitAction
    {
        public ActionType GetActionType() => ActionType.Stomp;
        public bool CanExecute(EntityWorld world, int unitId) => UnitQueries.IsAlive(world, unitId);
    }
}
