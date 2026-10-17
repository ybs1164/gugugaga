namespace TacticsECS
{
    /// <summary>
    /// 폭우 패시브(위키 Unit Skills "Drench"): 적이 없는 칸도 공격 사거리 안이면 공격할 수 있고, 공격한 칸을 침수시킨다 — Systems/TerrainChangeSystem.cs. 순수 마커 — 값(수치)은 없다.
    /// 규칙: docs/spec/game/unit.md#행동과-패시브
    /// </summary>
    [System.Serializable]
    public class DrenchAction : IUnitAction
    {
        public ActionType GetActionType() => ActionType.Drench;

        public bool CanExecute(EntityWorld world, int unitId) => UnitQueries.IsAlive(world, unitId);
    }
}
