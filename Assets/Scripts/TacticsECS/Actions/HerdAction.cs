namespace TacticsECS
{
    /// <summary>
    /// 무리 패시브. 순수 마커 — 자기 자신을 Execute하는 동작이 없고, PassiveAuraSystem.RefreshHerdAura가
    /// UnitActionQueries.Find&lt;HerdAction&gt;로 보유 여부를 확인해 주변 1블록 내 아군(자신 제외)에게
    /// 가속(Accelerated) 상태를 부여한다. 값(수치)은 없다 — 가속 자체의 효과(이동 거리 +1)와 해제 조건
    /// (피격 시)은 Accelerated 컴포넌트(Core/UnitComponents.cs) 쪽에 있다.
    /// </summary>
    [System.Serializable]
    public class HerdAction : IUnitAction
    {
        public ActionType GetActionType() => ActionType.Herd;

        public bool CanExecute(EntityWorld world, int unitId) => UnitQueries.IsAlive(world, unitId);
    }
}
