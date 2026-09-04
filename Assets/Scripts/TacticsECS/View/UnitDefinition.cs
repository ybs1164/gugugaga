using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 유닛 프리팹에 붙는 "타입 정의" 컴포넌트. 근접/원거리/방어 같은 타입 구분은 코드의 enum 분기가
    /// 아니라, 이 컴포넌트를 가진 서로 다른 프리팹(Assets/Prefabs/Units)으로 관리한다.
    /// 값은 속성 하나당 필드 하나로 나뉘어 있으며 서로 묶여 있지 않다 — UnitSpawner가 스폰 시 이 값들을
    /// EntityWorld의 각 컴포넌트(Core/UnitComponents.cs)로 그대로 옮겨 담고, EntityWorld는 스폰 이후
    /// 이 컴포넌트를 다시 참조하지 않는다(Systems는 View/MonoBehaviour를 몰라야 하므로).
    /// </summary>
    public class UnitDefinition : MonoBehaviour
    {
        [Header("Vitals")]
        [SerializeField] private int maxHp;

        [Header("Combat")]
        [SerializeField] private int attack;
        [SerializeField] private int defense;
        [SerializeField] private int attackRange;
        [SerializeField] private bool canGuard;

        [Header("Movement")]
        [SerializeField] private int moveRange;
        [Tooltip("true면 지형(Walkable=false인 타일, 예: 벽/장애물)을 무시하고 이동할 수 있다. 비행 유닛 등에 사용.")]
        [SerializeField] private bool ignoreTerrain;
        [Tooltip("true면 다른 유닛이 있는 타일도 지나가거나 멈출 수 있다. 유령/투명체 등에 사용.")]
        [SerializeField] private bool ignoreUnitBlocking;
        [Tooltip("대각선 방향(8방향) 이동을 허용할지 여부. false면 상하좌우 4방향만 이동 가능.")]
        [SerializeField] private bool allowDiagonal;

        [Header("Appearance")]
        [SerializeField] private Color playerColor = Color.white;
        [SerializeField] private Color enemyColor = Color.white;

        public int MaxHp => maxHp;

        public int Attack => attack;
        public int Defense => defense;
        public int AttackRange => attackRange;
        public bool CanGuard => canGuard;

        public int MoveRange => moveRange;
        public bool IgnoreTerrain => ignoreTerrain;
        public bool IgnoreUnitBlocking => ignoreUnitBlocking;
        public bool AllowDiagonal => allowDiagonal;

        public Color ColorFor(Team team) => team == Team.Player ? playerColor : enemyColor;
    }
}
