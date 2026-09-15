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

        /// <summary>이 유닛이 가진 행동의 집합. 비트 플래그라 여러 개를 동시에 가질 수 있다.</summary>
        public ActionType Actions;

        /// <summary>이 유닛이 들어갈 수 있는 지형(육지/물). Core/TerrainType.cs 참고.</summary>
        public TerrainType Domain = TerrainType.Land;

        public int MoveRange;

        public int AttackAttack;
        public int AttackRange;

        public int HealAmount;
        public int HealRange;

        /// <summary>Transport(수송) 패시브가 있을 때만 쓰이는 정원 값. 아직 태우고 내리는 시스템 자체가
        /// 없는 플레이스홀더 — Core/UnitComponents.cs의 CargoCapacity 참고.</summary>
        public int TransportCapacity;
    }
}
