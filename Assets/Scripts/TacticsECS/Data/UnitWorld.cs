using System.Collections.Generic;

namespace TacticsECS
{
    /// <summary>
    /// 모든 유닛의 데이터를 하나의 연속된 리스트에 보관한다.
    /// 유닛 GameObject(UnitView)는 이 데이터의 "표시"일 뿐이고, 전투 판정은 전부 이 배열 위에서 동작한다.
    /// 유닛 수가 수백으로 늘어나도 늘어나는 건 MonoBehaviour 개수가 아니라 struct 배열 크기뿐이라
    /// 순회/조회 비용이 매우 저렴하게 유지된다.
    /// </summary>
    public class UnitWorld
    {
        private readonly List<UnitData> _units = new List<UnitData>();

        public int Count => _units.Count;

        public IReadOnlyList<UnitData> All => _units;

        public int Spawn(UnitData data)
        {
            data.Id = _units.Count;
            _units.Add(data);
            return data.Id;
        }

        public UnitData Get(int id) => _units[id];

        public void Set(int id, UnitData data) => _units[id] = data;

        public bool AnyAlive(Team team)
        {
            for (int i = 0; i < _units.Count; i++)
                if (_units[i].IsAlive && _units[i].Team == team) return true;
            return false;
        }
    }
}
