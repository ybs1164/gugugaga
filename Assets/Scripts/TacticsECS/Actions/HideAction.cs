namespace TacticsECS
{
    /// <summary>
    /// 은신 패시브(위키 Unit Skills "Hide" — "Allows a unit to hide itself and become invisible to enemies when it moves. Such units also
    /// ignore zone of control and can move through enemy units."). 순수 마커 — 이동 후 Hidden을 세우는 곳은 MovementSystem.TryMove,
    /// 보이기/드러나기 판정은 StealthSystem, ZoC 무시/적 유닛 통과는 PathfindingSystem이 보유 여부만 확인한다. 값(수치)은 없다.
    /// </summary>
    [System.Serializable]
    public class HideAction : IUnitAction
    {
        public ActionType GetActionType() => ActionType.Hide;

        public bool CanExecute(EntityWorld world, int unitId) => UnitQueries.IsAlive(world, unitId);
    }
}
