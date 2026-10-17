namespace TacticsECS
{
    /// <summary>
    /// 은신 패시브: 한 바퀴 동안 피해를 받지 않으면 자기 턴 시작에 숨는다(기획서 — 위키 Hide는 이동하면 숨음). 순수 마커 — 숨기기/드러내기는
    /// StealthSystem이 보유 여부만 확인한다. 영향권 무시는 은신이 아니라 SneakAction. 규칙: docs/spec/game/vision.md#은신
    /// </summary>
    [System.Serializable]
    public class HideAction : IUnitAction
    {
        public ActionType GetActionType() => ActionType.Hide;

        public bool CanExecute(EntityWorld world, int unitId) => UnitQueries.IsAlive(world, unitId);
    }
}
