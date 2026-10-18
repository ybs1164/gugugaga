namespace TacticsECS
{
    /// <summary>
    /// 승패 판정. 규칙: docs/spec/game/turn.md#승패, docs/spec/game/faith.md#신앙-승리
    /// econ이 null이면(경제 없는 데모 씬) 유닛 전멸만 본다.
    /// </summary>
    public static class VictorySystem
    {
        /// <summary>도시를 가졌던 팀은 도시를 전부 잃으면, 도시가 없던 팀(또는 경제 없는 씬)은 유닛이 전멸하면 패배.</summary>
        public static bool HasTeamLost(EntityWorld world, EconomyWorld econ, Team team)
        {
            if (econ != null && econ.HadCity.Contains(team)) return CitySystem.HasLost(econ, team);
            return !UnitQueries.AnyAlive(world, team);
        }

        /// <summary>신앙 승리가 먼저, 그다음 섬멸. 두 팀이 함께 졌으면 플레이어 패배로 본다.</summary>
        public static BattleOutcome Evaluate(EntityWorld world, EconomyWorld econ)
        {
            if (econ != null && econ.FaithWinner >= 0)
                return new BattleOutcome { Reason = BattleEndReason.FaithVictory, Winner = (Team)econ.FaithWinner };

            bool playerLost = HasTeamLost(world, econ, Team.Player);
            bool enemyLost = HasTeamLost(world, econ, Team.Enemy);
            if (!playerLost && !enemyLost) return new BattleOutcome { Reason = BattleEndReason.None };
            return new BattleOutcome { Reason = BattleEndReason.Conquest, Winner = playerLost ? Team.Enemy : Team.Player };
        }
    }
}
