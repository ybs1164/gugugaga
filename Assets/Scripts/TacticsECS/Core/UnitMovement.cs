using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 유닛의 이동 방식을 세부적으로 지정하는 고정 값. PathfindingSystem(도달 가능 범위 계산)과
    /// MovementSystem(실제 이동 판정)이 이 값을 읽어 어떤 타일/유닛을 지나갈 수 있는지 판정한다.
    /// UnitDefinition 프리팹 컴포넌트에서 인스펙터로 채워지며, 순수 데이터이므로 로직을 갖지 않는다.
    /// </summary>
    [System.Serializable]
    public struct UnitMovement
    {
        public int MoveRange;

        [Tooltip("true면 지형(Walkable=false인 타일, 예: 벽/장애물)을 무시하고 이동할 수 있다. 비행 유닛 등에 사용.")]
        public bool IgnoreTerrain;

        [Tooltip("true면 다른 유닛이 있는 타일도 지나가거나 멈출 수 있다. 유령/투명체 등에 사용.")]
        public bool IgnoreUnitBlocking;

        [Tooltip("대각선 방향(8방향) 이동을 허용할지 여부. false면 상하좌우 4방향만 이동 가능.")]
        public bool AllowDiagonal;
    }
}
