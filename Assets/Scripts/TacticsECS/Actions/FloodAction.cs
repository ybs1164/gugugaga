namespace TacticsECS
{
    /// <summary>
    /// 침수 패시브: 서 있는 평지·숲 칸을 침수 지형으로 바꾸는 행동을 쓸 수 있다 — Systems/TerrainChangeSystem.cs. 순수 마커 — 값(수치)은 없다.
    /// 규칙: docs/spec/game/unit.md#행동과-패시브
    /// </summary>
    [System.Serializable]
    public class FloodAction : IUnitAction
    {
        public ActionType GetActionType() => ActionType.Flood;

        public bool CanExecute(EntityWorld world, int unitId) => UnitQueries.IsAlive(world, unitId);
    }
}
