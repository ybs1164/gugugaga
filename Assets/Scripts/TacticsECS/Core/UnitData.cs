using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 유닛 하나의 "컴포넌트 데이터". GameObject가 아니라 UnitWorld 내부 리스트의 한 칸.
    /// 전투 로직(Systems)은 전부 이 struct를 값 복사해서 읽고, 수정한 뒤 UnitWorld.Set으로 다시 써넣는 방식으로 동작한다.
    /// </summary>
    [System.Serializable]
    public struct UnitData
    {
        public int Id;
        public Team Team;
        public UnitType Type;

        public Vector2Int GridPos;

        public int Hp;
        public int MaxHp;
        public int Attack;
        public int Defense;

        public int MoveRange;
        public int AttackRange;

        public bool HasMoved;
        public bool HasActed;
        public bool IsGuarding; // Guard 타입 전용: 이번 턴 방어 태세인지

        public bool IsAlive => Hp > 0;

        public static UnitData Create(int id, Team team, UnitType type, Vector2Int pos)
        {
            var d = new UnitData
            {
                Id = id,
                Team = team,
                Type = type,
                GridPos = pos,
                IsGuarding = false,
                HasMoved = false,
                HasActed = false
            };

            switch (type)
            {
                case UnitType.Melee:
                    d.MaxHp = 12; d.Attack = 5; d.Defense = 1; d.MoveRange = 3; d.AttackRange = 1;
                    break;
                case UnitType.Ranged:
                    d.MaxHp = 8; d.Attack = 4; d.Defense = 0; d.MoveRange = 2; d.AttackRange = 3;
                    break;
                case UnitType.Guard:
                    d.MaxHp = 18; d.Attack = 3; d.Defense = 3; d.MoveRange = 2; d.AttackRange = 1;
                    break;
            }

            d.Hp = d.MaxHp;
            return d;
        }
    }
}
