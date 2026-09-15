namespace TacticsECS
{
    /// <summary>
    /// 장애물 통과 패시브. Charge/Retreat/Infiltrate와 같은 성격 — 자기 자신을 Execute하는 동작이 없고,
    /// MoveAction.Execute와 PathfindingSystem.GetReachable이 지형 판정을 할 때
    /// UnitActionQueries.Find&lt;IgnoreTerrainAction&gt;로 보유 여부만 확인해 "Walkable=false인 타일(벽/장애물)
    /// 및 MoveDomain과 다른 지형(예: 육지 유닛의 물 타일)을 무시하고 이동 가능"으로 판단을 바꿔준다.
    /// 값(수치)은 없다 — 순수 마커. 대부분의 유닛에는 해당 없는 드문 케이스라 예전 MoveAction 자신의
    /// bool 필드(CSV의 Move.IgnoreTerrain 컬럼)에서 이 패시브로 옮겨왔다.
    /// </summary>
    [System.Serializable]
    public class IgnoreTerrainAction : IUnitAction
    {
        public ActionType GetActionType() => ActionType.IgnoreTerrain;

        public bool CanExecute(EntityWorld world, int unitId) => UnitQueries.IsAlive(world, unitId);
    }
}
