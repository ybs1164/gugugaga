namespace TacticsECS
{
    /// <summary>
    /// 턴 순서(플레이어 -> 적 -> 플레이어 ...) 계산과, 새 턴 시작 시 해당 팀 유닛들의
    /// HasMoved/HasActed/IsGuarding 컴포넌트 초기화를 담당하는 순수 함수형 시스템. 턴 시작마다 무리
    /// (HerdAction) 오라도 함께 갱신한다(PassiveAuraSystem.RefreshHerdAura) — 턴 사이에 죽거나 배치가
    /// 바뀐 유닛을 반영하기 위함. 빙결(Frozen)된 유닛은 이 리셋에서 HasMoved/HasActed를 오히려 true로
    /// 세워 그 턴을 통째로 건너뛰게 만들고, 그 즉시 Frozen을 해제한다 — "1턴간 행동불능"이 정확히 한
    /// 턴만 적용되도록.
    /// 다른 System과 마찬가지로 자체 상태는 없다 — 현재 턴이 누구 차례인지는 항상 TurnState를
    /// 인자로 받아 다음 TurnState를 계산해 반환할 뿐이고, 실제 보관은 호출자(BattleController) 몫이다.
    /// </summary>
    public static class TurnSystem
    {
        public static TurnState StartTurn(EntityWorld world, Team team, int turnNumber)
        {
            ResetUnitStates(world, team);
            PassiveAuraSystem.RefreshHerdAura(world);
            return new TurnState { ActiveTeam = team, TurnNumber = turnNumber };
        }

        public static TurnState EndTurn(EntityWorld world, TurnState current)
        {
            var next = current.ActiveTeam == Team.Player ? Team.Enemy : Team.Player;
            int turnNumber = next == Team.Player ? current.TurnNumber + 1 : current.TurnNumber;
            return StartTurn(world, next, turnNumber);
        }

        private static void ResetUnitStates(EntityWorld world, Team team)
        {
            for (int i = 0; i < world.EntityCount; i++)
            {
                if (world.Get<Team>(i) != team) continue;

                bool frozen = world.Get<Frozen>(i).Value;
                world.Set(i, new HasMoved { Value = frozen });
                world.Set(i, new HasActed { Value = frozen });
                world.Set(i, new IsGuarding { Value = false });
                if (frozen) world.Set(i, new Frozen { Value = false });
            }
        }
    }
}
