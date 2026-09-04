using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 유닛 프리팹에 붙는 "타입 정의" 컴포넌트. 근접/원거리/방어 같은 타입 구분은 더 이상 코드의
    /// enum 분기가 아니라, 이 컴포넌트를 가진 서로 다른 프리팹(Assets/Prefabs/Units)으로 관리한다.
    /// UnitSpawner/UnitView는 이 값을 그대로 읽어 쓸 뿐 타입별 switch를 갖지 않는다.
    /// </summary>
    public class UnitDefinition : MonoBehaviour
    {
        [SerializeField] private UnitStats stats;
        [SerializeField] private Color playerColor = Color.white;
        [SerializeField] private Color enemyColor = Color.white;

        public UnitStats Stats => stats;

        public Color ColorFor(Team team) => team == Team.Player ? playerColor : enemyColor;
    }
}
