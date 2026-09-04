using System.Collections.Generic;
using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 모든 유닛 데이터를 "속성 하나당 리스트 하나"로 나눠 보관한다 (Structure of Arrays).
    /// 유닛을 표현하는 struct를 따로 두지 않고, 같은 id가 모든 리스트에서 같은 인덱스를 가리키는 방식으로
    /// 하나의 엔티티를 구성한다. 유닛은 죽어도 리스트에서 제거되지 않고 Hp만 0이 되므로,
    /// 모든 리스트의 길이는 항상 서로 같다.
    /// 유닛 GameObject(UnitView)는 이 데이터의 "표시"일 뿐이고, 전투 판정은 전부 이 배열들 위에서 동작한다.
    /// 유닛 수가 수백으로 늘어나도 늘어나는 건 MonoBehaviour 개수가 아니라 리스트 길이뿐이라
    /// 순회/조회 비용이 매우 저렴하게 유지된다.
    /// </summary>
    public class UnitWorld
    {
        // ---- 런타임 값 (전투 중 바뀜) ----
        private readonly List<Team> _team = new List<Team>();
        private readonly List<Vector2Int> _gridPos = new List<Vector2Int>();
        private readonly List<int> _hp = new List<int>();
        private readonly List<bool> _hasMoved = new List<bool>();
        private readonly List<bool> _hasActed = new List<bool>();
        private readonly List<bool> _isGuarding = new List<bool>();

        // ---- 고정 스탯 (스폰 후 바뀌지 않음) ----
        private readonly List<int> _maxHp = new List<int>();
        private readonly List<int> _attack = new List<int>();
        private readonly List<int> _defense = new List<int>();
        private readonly List<int> _attackRange = new List<int>();
        private readonly List<bool> _canGuard = new List<bool>();

        // ---- 이동 방식 (스폰 후 바뀌지 않음) ----
        private readonly List<int> _moveRange = new List<int>();
        private readonly List<bool> _ignoreTerrain = new List<bool>();
        private readonly List<bool> _ignoreUnitBlocking = new List<bool>();
        private readonly List<bool> _allowDiagonal = new List<bool>();

        public int Count => _team.Count;

        public int Spawn(
            Team team, Vector2Int pos,
            int maxHp, int attack, int defense, int attackRange, bool canGuard,
            int moveRange, bool ignoreTerrain, bool ignoreUnitBlocking, bool allowDiagonal)
        {
            int id = _team.Count;

            _team.Add(team);
            _gridPos.Add(pos);
            _hp.Add(maxHp);
            _hasMoved.Add(false);
            _hasActed.Add(false);
            _isGuarding.Add(false);

            _maxHp.Add(maxHp);
            _attack.Add(attack);
            _defense.Add(defense);
            _attackRange.Add(attackRange);
            _canGuard.Add(canGuard);

            _moveRange.Add(moveRange);
            _ignoreTerrain.Add(ignoreTerrain);
            _ignoreUnitBlocking.Add(ignoreUnitBlocking);
            _allowDiagonal.Add(allowDiagonal);

            return id;
        }

        public bool IsAlive(int id) => _hp[id] > 0;

        public Team GetTeam(int id) => _team[id];

        public Vector2Int GetGridPos(int id) => _gridPos[id];
        public void SetGridPos(int id, Vector2Int value) => _gridPos[id] = value;

        public int GetHp(int id) => _hp[id];
        public void SetHp(int id, int value) => _hp[id] = value;

        public bool GetHasMoved(int id) => _hasMoved[id];
        public void SetHasMoved(int id, bool value) => _hasMoved[id] = value;

        public bool GetHasActed(int id) => _hasActed[id];
        public void SetHasActed(int id, bool value) => _hasActed[id] = value;

        public bool GetIsGuarding(int id) => _isGuarding[id];
        public void SetIsGuarding(int id, bool value) => _isGuarding[id] = value;

        public int GetMaxHp(int id) => _maxHp[id];
        public int GetAttack(int id) => _attack[id];
        public int GetDefense(int id) => _defense[id];
        public int GetAttackRange(int id) => _attackRange[id];
        public bool GetCanGuard(int id) => _canGuard[id];

        public int GetMoveRange(int id) => _moveRange[id];
        public bool GetIgnoreTerrain(int id) => _ignoreTerrain[id];
        public bool GetIgnoreUnitBlocking(int id) => _ignoreUnitBlocking[id];
        public bool GetAllowDiagonal(int id) => _allowDiagonal[id];

        public bool AnyAlive(Team team)
        {
            for (int i = 0; i < _team.Count; i++)
                if (IsAlive(i) && _team[i] == team) return true;
            return false;
        }
    }
}
