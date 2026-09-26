using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 도시 하나(수도 또는 점령한 마을)의 값. 순수 데이터이며, 레벨업/보상/수입/영토 계산은 전부
    /// Systems/CitySystem.cs가 맡는다. EconomyWorld.Cities 리스트의 한 칸이고, 타일은 그 리스트 인덱스를
    /// TileData.OwnerCity로 가리킨다(도시는 사라지지 않고 주인만 바뀌므로 인덱스가 안정적이다).
    /// </summary>
    public struct CityData
    {
        public string Name;
        public Vector2Int Position;
        public Team Owner;
        public bool IsCapital;

        /// <summary>1부터 시작. 레벨 L에서 L+1로 오르려면 인구 L+1이 필요하다(폴리토피아 규칙).</summary>
        public int Level;

        /// <summary>현재 레벨에서 모은 인구(레벨업 시 필요량만큼 차감되고 남은 값은 이월). 음수일 수 있다
        /// (건물 파괴/수도 연결 해제) — 음수만큼 그 도시의 골드 수입이 줄어든다.</summary>
        public int Population;

        /// <summary>영토 반경(체비쇼프 거리). 기본 1(3x3), 4레벨 보상 "국경 확장"으로 2(5x5).</summary>
        public int BorderRadius;

        public bool HasWorkshop;
        public bool HasWall;
        public int ParkCount;

        /// <summary>아직 고르지 않은 레벨업 보상 개수. 가장 낮은 미선택 레벨의 선택지부터 차례로 고른다
        /// (CitySystem.PendingRewardLevel).</summary>
        public int PendingRewards;

        /// <summary>수도와 도로/항구로 연결되어 있는지(수도 자신은 false). CitySystem.RefreshConnections가
        /// 갱신하며, 연결될 때 이 도시와 수도에 인구 +1씩, 끊기면 -1씩 반영된다.</summary>
        public bool ConnectedToCapital;
    }
}
