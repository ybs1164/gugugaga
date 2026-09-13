using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 수송 패시브. 순수 마커 — 자기 자신을 Execute하는 동작이 없다. ScoutAction/VisionRange와 같은 성격의
    /// 플레이스홀더다: "몇 명을 태울 수 있는가"(capacity, CSV의 Transport.Capacity 컬럼)는 값으로 갖고
    /// 있지만, 실제로 유닛을 태우고 내리는 시스템 자체가 아직 없어 지금은 아무 게임플레이 효과가 없다
    /// (SandboxUnits.csv 예시의 함선류에 쓰임). 육지/물 이동 제한(MoveDomain, Core/UnitComponents.cs)은
    /// 이 패시브와 무관하게 이미 별도로 동작한다. 나중에 수송 시스템이 생기면
    /// UnitActionQueries.Find&lt;TransportAction&gt;로 보유 여부를 확인하고 Capacity를 정원으로 쓰면 된다.
    /// </summary>
    [System.Serializable]
    public class TransportAction : IUnitAction
    {
        [SerializeField] private int capacity;

        public ActionType GetActionType() => ActionType.Transport;

        /// <summary>CSV 행(UnitCsvRow)의 Transport.Capacity 값으로 인스턴스를 만든다.</summary>
        public static TransportAction FromCsv(int capacity) => new TransportAction { capacity = capacity };

        public int Capacity => capacity;

        public bool CanExecute(EntityWorld world, int unitId) => UnitQueries.IsAlive(world, unitId);
    }
}
