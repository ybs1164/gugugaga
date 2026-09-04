using System;
using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 턴 순서(플레이어 -> 적 -> 플레이어 ...)를 관리하고,
    /// 새 턴 시작 시 해당 팀 유닛들의 HasMoved/HasActed/IsGuarding 컴포넌트를 초기화한다.
    /// </summary>
    public class TurnManager : MonoBehaviour
    {
        public Team ActiveTeam { get; private set; } = Team.Player;
        public int TurnNumber { get; private set; } = 1;

        public event Action<Team, int> OnTurnStart;

        private EntityWorld _world;

        public void Init(EntityWorld world)
        {
            _world = world;
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
            for (int i = 0; i < _world.EntityCount; i++)
            {
                if (_world.Get<Team>(i) != team) continue;
                _world.Set(i, new HasMoved { Value = false });
                _world.Set(i, new HasActed { Value = false });
                _world.Set(i, new IsGuarding { Value = false });
            }
        }
    }
}
