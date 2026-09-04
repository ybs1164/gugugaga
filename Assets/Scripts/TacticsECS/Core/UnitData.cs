using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 유닛 하나의 "런타임" 컴포넌트 데이터 — 전투 중 실제로 바뀌는 값만 담는다.
    /// GameObject가 아니라 UnitWorld 내부 리스트의 한 칸이며, 전투 로직(Systems)은 전부 이 struct를
    /// 값 복사해서 읽고 수정한 뒤 UnitWorld.Set으로 다시 써넣는 방식으로 동작한다.
    /// 전투 중 바뀌지 않는 고정 스탯(공격력/방어력/이동 방식 등)은 여기 두지 않고
    /// UnitStats에 별도로 저장한다 (UnitWorld.GetStats(id)로 조회).
    /// </summary>
    [System.Serializable]
    public struct UnitData
    {
        public int Id;
        public Team Team;

        public Vector2Int GridPos;

        public int Hp;

        public UnitTurnState TurnState;

        public bool IsAlive => Hp > 0;

        public static UnitData Create(int id, Team team, Vector2Int pos, int maxHp)
        {
            return new UnitData
            {
                Id = id,
                Team = team,
                GridPos = pos,
                Hp = maxHp,
                TurnState = default
            };
        }
    }
}
