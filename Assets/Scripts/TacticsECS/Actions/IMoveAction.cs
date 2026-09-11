using UnityEngine;

namespace TacticsECS
{
    /// <summary>목적지 한 칸이 필요한 행동. MoveAction만 구현한다.</summary>
    public interface IMoveAction : IUnitAction
    {
        /// <summary>destination으로 실제 이동을 시도한다. 실행 가능 여부(CanExecute)뿐 아니라
        /// 목적지 자체의 유효성(그리드 범위/지형/다른 유닛 차단)까지 이 메서드가 판정한다.</summary>
        bool Execute(GridWorld grid, EntityWorld world, int unitId, Vector2Int destination);
    }
}
