using System.Collections.Generic;

namespace TacticsECS
{
    /// <summary>
    /// 모든 유닛의 데이터를 두 개의 나란한(parallel) 리스트에 나눠 보관한다.
    /// - _units: 전투 중 바뀌는 런타임 값(UnitData - Hp/GridPos/행동 여부 등)
    /// - _stats: 스폰 시 정해지고 이후 바뀌지 않는 고정 스탯(UnitStats - 공격력/방어력/이동 방식 등)
    /// 같은 id는 두 리스트에서 항상 같은 인덱스를 가리킨다 (유닛은 제거되지 않고 죽으면 Hp만 0이 된다).
    /// 유닛 GameObject(UnitView)는 이 데이터의 "표시"일 뿐이고, 전투 판정은 전부 이 배열 위에서 동작한다.
    /// 유닛 수가 수백으로 늘어나도 늘어나는 건 MonoBehaviour 개수가 아니라 struct 배열 크기뿐이라
    /// 순회/조회 비용이 매우 저렴하게 유지된다.
    /// </summary>
    public class UnitWorld
    {
        private readonly List<UnitData> _units = new List<UnitData>();
        private readonly List<UnitStats> _stats = new List<UnitStats>();

        public int Count => _units.Count;

        public IReadOnlyList<UnitData> All => _units;

        public int Spawn(UnitData data, UnitStats stats)
        {
            data.Id = _units.Count;
            _units.Add(data);
            _stats.Add(stats);
            return data.Id;
        }

        public UnitData Get(int id) => _units[id];

        public void Set(int id, UnitData data) => _units[id] = data;

        public UnitStats GetStats(int id) => _stats[id];

        public bool AnyAlive(Team team)
        {
            for (int i = 0; i < _units.Count; i++)
                if (_units[i].IsAlive && _units[i].Team == team) return true;
            return false;
        }
    }
}
