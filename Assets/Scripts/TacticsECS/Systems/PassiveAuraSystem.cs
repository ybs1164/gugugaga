namespace TacticsECS
{
    /// <summary>
    /// 위치 관계로 결정되는 오라형 패시브(무리)를 갱신하는 시스템. 다른 System과 마찬가지로 자체 상태는
    /// 없다 — 호출될 때마다 현재 EntityWorld 상태만 보고 다시 계산한다.
    /// </summary>
    public static class PassiveAuraSystem
    {
        /// <summary>무리(HerdAction)를 가진 살아있는 유닛 주변 1블록 내 아군(자신 제외)에게 가속
        /// (Accelerated)을 부여한다. 이미 가속 상태인 유닛도 다시 갱신될 뿐이라 문제 없고, 범위 밖으로
        /// 벗어나도 이미 부여된 가속은 유지된다(해제는 오직 피격 시에만 — Accelerated 컴포넌트 주석 참고).
        /// 유닛이 이동하거나(MovementSystem.TryMove) 새 턴이 시작될 때(TurnSystem.StartTurn) 호출해
        /// 배치 변화를 반영한다.</summary>
        public static void RefreshHerdAura(EntityWorld world)
        {
            for (int herderId = 0; herderId < world.EntityCount; herderId++)
            {
                if (!UnitQueries.IsAlive(world, herderId)) continue;
                if (UnitActionQueries.Find<HerdAction>(world, herderId) == null) continue;

                var herderPos = world.Get<GridPosition>(herderId).Value;
                var team = world.Get<Team>(herderId);

                for (int allyId = 0; allyId < world.EntityCount; allyId++)
                {
                    if (allyId == herderId || !UnitQueries.IsAlive(world, allyId)) continue;
                    if (world.Get<Team>(allyId) != team) continue;
                    if (PathfindingSystem.Distance(herderPos, world.Get<GridPosition>(allyId).Value) > 1) continue;

                    world.Set(allyId, new Accelerated { Value = true });
                }
            }
        }
    }
}
