using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 유닛 하나의 "컴포넌트 데이터". GameObject가 아니라 UnitWorld 내부 리스트의 한 칸.
    /// 전투 로직(Systems)은 전부 이 struct를 값 복사해서 읽고, 수정한 뒤 UnitWorld.Set으로 다시 써넣는 방식으로 동작한다.
    /// 타입별 분기는 갖지 않는다 — 스폰 시 유닛 프리팹의 UnitDefinition.Stats 값을 그대로 복사해 채운다.
    /// </summary>
    [System.Serializable]
    public struct UnitData
    {
        public int Id;
        public Team Team;

        public Vector2Int GridPos;

        public int Hp;
        public int MaxHp;
        public int Attack;
        public int Defense;

        public int MoveRange;
        public int AttackRange;

        public bool CanGuard; // 방어 태세 행동이 가능한 타입인지 (UnitDefinition.Stats에서 복사됨)

        public bool HasMoved;
        public bool HasActed;
        public bool IsGuarding; // CanGuard 유닛 전용: 이번 턴 방어 태세인지

        public bool IsAlive => Hp > 0;

        public static UnitData Create(int id, Team team, UnitStats stats, Vector2Int pos)
        {
            return new UnitData
            {
                Id = id,
                Team = team,
                GridPos = pos,

                MaxHp = stats.MaxHp,
                Hp = stats.MaxHp,
                Attack = stats.Attack,
                Defense = stats.Defense,

                MoveRange = stats.MoveRange,
                AttackRange = stats.AttackRange,

                CanGuard = stats.CanGuard,

                IsGuarding = false,
                HasMoved = false,
                HasActed = false
            };
        }
    }
}
