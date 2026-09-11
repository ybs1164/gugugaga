using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 이동 행동. 유닛이 actions 리스트에 이 항목을 가지고 있으면 ActionType.Move를 쓸 수 있고,
    /// 이동 방식에 필요한 값(사거리/지형 무시/유닛 통과/대각선)을 이 항목이 직접 들고 있다.
    /// UnitDefinition이 이 값들을 읽어 MoveRange/IgnoreTerrain/IgnoreUnitBlocking/AllowDiagonal로
    /// 노출하고, UnitSpawner가 그대로 EntityWorld 컴포넌트(Core/UnitComponents.cs)로 옮긴다.
    /// </summary>
    [System.Serializable]
    public class MoveAction : IUnitAction
    {
        [SerializeField] private int moveRange;
        [Tooltip("true면 지형(Walkable=false인 타일, 예: 벽/장애물)을 무시하고 이동할 수 있다. 비행 유닛 등에 사용.")]
        [SerializeField] private bool ignoreTerrain;
        [Tooltip("true면 다른 유닛이 있는 타일도 지나가거나 멈출 수 있다. 유령/투명체 등에 사용.")]
        [SerializeField] private bool ignoreUnitBlocking;
        [Tooltip("대각선 방향(8방향) 이동을 허용할지 여부. false면 상하좌우 4방향만 이동 가능.")]
        [SerializeField] private bool allowDiagonal;

        public ActionType Type => ActionType.Move;

        public int MoveRange => moveRange;
        public bool IgnoreTerrain => ignoreTerrain;
        public bool IgnoreUnitBlocking => ignoreUnitBlocking;
        public bool AllowDiagonal => allowDiagonal;
    }
}
