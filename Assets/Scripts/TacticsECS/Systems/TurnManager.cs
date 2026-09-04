using System;
using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 턴 순서(플레이어 -> 적 -> 플레이어 ...)를 관리하고,
    /// 새 턴 시작 시 해당 팀 유닛들의 HasMoved/HasActed/IsGuarding을 초기화한다.
    /// </summary>
    public class TurnManager : MonoBehaviour
    {
        public Team ActiveTeam { get; private set; } = Team.Player;
        public int TurnNumber { get; private set; } = 1;

        public event Action<Team, int> OnTurnStart;

        private UnitWorld _units;

        public void Init(UnitWorld units)
        {
            _units = units;
            StartTurn(Team.Player);
        }

        public void EndTurn()
        {
            var next = ActiveTeam == Team.Player ? Team.Enemy : Team.Player;
            if (next == Team.Player) TurnNumber++;
            StartTurn(next);
        }

        private void StartTurn(Team team)
        {
            ActiveTeam = team;
            ResetUnitStates(team);
            OnTurnStart?.Invoke(team, TurnNumber);
        }

        private void ResetUnitStates(Team team)
        {
            for (int i = 0; i < _units.Count; i++)
            {
                var u = _units.Get(i);
                if (u.Team != team) continue;
                u.HasMoved = false;
                u.HasActed = false;
                u.IsGuarding = false;
                _units.Set(i, u);
            }
        }
    }
}
