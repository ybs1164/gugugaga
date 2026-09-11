using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// CSV 한 행(= 유닛 타입 하나)을 그대로 옮겨 담는 값 타입. 파일 텍스트나 UnitDefinition/IUnitAction과
    /// 무관하게 이 행 자체는 아무 로직도 갖지 않는다 — 파싱(UnitCsvSerializer)과 실제 행동 객체 변환
    /// (UnitCsvActionFactory)은 전부 Systems 계층이 담당한다.
    /// Actions는 이 유닛이 가진 행동의 종류만 나타내는 태그이고(ActionType 재사용 — 원래도 "CSV 연동용"
    /// 플레이스홀더로 문서화되어 있던 값이다, Core/ActionType.cs 참고), 그 태그에 대응하는 파라미터 값은
    /// 아래 각 액션별 필드에 별도로 담긴다(해당 태그가 없으면 값은 쓰이지 않는다).
    /// </summary>
    public class UnitCsvRow
    {
        public string Name;
        public int MaxHp;
        public int Defense;

        /// <summary>외형(모델/머티리얼 슬롯)을 빌려올 기존 프리팹 이름표. Assets/Prefabs/Units의
        /// Unit_&lt;BaseVisual&gt; 프리팹을 찾는 키로 쓰인다(UnitCsvRow 자신은 그 매칭 방법을 모른다).</summary>
        public string BaseVisual;

        public Color PlayerColor = Color.white;
        public Color EnemyColor = Color.white;

        /// <summary>이 유닛이 가진 행동의 집합. 비트 플래그라 여러 개를 동시에 가질 수 있다.</summary>
        public ActionType Actions;

        public int MoveRange;
        public bool MoveIgnoreTerrain;
        public bool MoveIgnoreUnitBlocking;
        public bool MoveAllowDiagonal;

        public int AttackAttack;
        public int AttackRange;

        public int HealAmount;
        public int HealRange;
    }
}
