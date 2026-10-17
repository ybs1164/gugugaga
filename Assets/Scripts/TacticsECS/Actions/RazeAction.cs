namespace TacticsECS
{
    /// <summary>
    /// 파괴 패시브: 이동으로 지나간 칸(도착 칸 포함)의 건물·유적을 없앤다 — MovementSystem. 순수 마커 — 값(수치)은 없다.
    /// 규칙: docs/spec/game/unit.md#행동과-패시브
    /// </summary>
    [System.Serializable]
    public class RazeAction : IUnitAction
    {
        public ActionType GetActionType() => ActionType.Raze;

        public bool CanExecute(EntityWorld world, int unitId) => UnitQueries.IsAlive(world, unitId);
    }
}
