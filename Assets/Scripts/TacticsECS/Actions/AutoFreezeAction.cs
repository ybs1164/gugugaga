namespace TacticsECS
{
    /// <summary>
    /// 자동 빙결 패시브(위키 Unit Skills "Auto Freeze"): 이동할 때마다 주변 8칸의 물을 얼음으로 만들고 그 칸들의 적을 빙결시킨다 — Systems/TerrainChangeSystem.cs. 순수 마커 — 값(수치)은 없다.
    /// 규칙: docs/spec/game/unit.md#행동과-패시브
    /// </summary>
    [System.Serializable]
    public class AutoFreezeAction : IUnitAction
    {
        public ActionType GetActionType() => ActionType.AutoFreeze;

        public bool CanExecute(EntityWorld world, int unitId) => UnitQueries.IsAlive(world, unitId);
    }
}
