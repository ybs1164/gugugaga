namespace TacticsECS
{
    /// <summary>
    /// 약탈 패시브: 공격 사거리 안의 적 도시를 직접 공격해 그 도시의 별 수입만큼 빼앗는다 — Systems/PlunderSystem.cs. 순수 마커 — 값(수치)은 없다.
    /// 규칙: docs/spec/game/unit.md#행동과-패시브
    /// </summary>
    [System.Serializable]
    public class PlunderAction : IUnitAction
    {
        public ActionType GetActionType() => ActionType.Plunder;

        public bool CanExecute(EntityWorld world, int unitId) => UnitQueries.IsAlive(world, unitId);
    }
}
