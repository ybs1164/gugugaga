using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 공격/방어 관련 순수 계산(사거리, 방어력, 피해량)과, 공격/방어 행동의 진입점을 담당하는 시스템.
    /// 실제 실행 가능 여부 판정/효과 적용은 AttackAction/DefendAction/CounterAction
    /// (Assets/Scripts/TacticsECS/Actions)이 직접 담당하고, 이 클래스는 UnitActionQueries로 그 행동을
    /// 찾아 위임하거나(TryAttack/TryDefend), 여러 행동이 공유하는 계산 함수(IsInAttackRange/
    /// EffectiveDefense/CalculateDamage)를 제공한다 — 이 계산 함수들은 BattleHud(UI 표시)에서도
    /// 그대로 재사용한다.
    ///
    /// 피해 공식은 위키 Combat 문서 그대로(3차 비교분석 — docs/GameDataCsv.md):
    ///   attackForce  = 공격자.공격 x (공격자 체력 / 최대 체력)
    ///   defenseForce = 대상.방어 x (대상 체력 / 최대 체력) x 방어 보너스(없음 1 / 지형·도시 1.5 / 성벽 4)
    ///   공격 피해 = round(attackForce / (attackForce + defenseForce) x 공격자.공격 x 4.5)
    ///   반격 피해 = round(defenseForce / (attackForce + defenseForce) x 대상.방어 x 4.5)  — 둘 다 공격 전 체력 기준
    ///   스플래시 = 공격 피해 / 2
    /// 계수(4.5)/배수/스플래시 나눗수는 GameRules.Combat(GameRules.csv)로 조정한다. 반올림은 .5 올림(위키 "nearest").
    /// 원문과 다른 점: 스플래시는 위키에서 반올림 없이 반으로 나눠 체력이 .5가 되는 버그가 있어, 정수 체력에 맞춰 내림한다.
    /// 방어 태세(Defend, 프로젝트 고유 행동)는 방어 스탯에 GameRules.Combat.GuardDefenseBonus를 더한다.
    /// </summary>
    public static class CombatSystem
    {
        public static bool IsInAttackRange(EntityWorld world, int attackerId, int targetId)
        {
            var attackerPos = world.Get<GridPosition>(attackerId).Value;
            var targetPos = world.Get<GridPosition>(targetId).Value;
            int dist = PathfindingSystem.Distance(attackerPos, targetPos);
            return dist >= 1 && dist <= world.Get<AttackRange>(attackerId).Value;
        }

        /// <summary>방어 스탯(방어 태세 가산 포함, 위치 보너스 배수는 제외).</summary>
        public static float BaseDefense(EntityWorld world, int unitId) =>
            world.Get<Defense>(unitId).Value + (world.Get<IsGuarding>(unitId).Value ? GameRules.Combat.GuardDefenseBonus : 0f);

        /// <summary>위치 방어 보너스 배수(PositionalDefenseBonus 등급 — TechEffectSystem이 계산): 없음 1, 표준 1.5, 성벽 4.</summary>
        public static float DefenseMultiplier(EntityWorld world, int unitId)
        {
            int level = world.Get<PositionalDefenseBonus>(unitId).Value;
            if (level >= 2) return GameRules.Combat.WallDefenseMultiplier;
            return level == 1 ? GameRules.Combat.DefenseBonusMultiplier : 1f;
        }

        /// <summary>UI 표시용 실제 방어력 = 방어 스탯 x 위치 보너스 배수.</summary>
        public static float EffectiveDefense(EntityWorld world, int unitId) => BaseDefense(world, unitId) * DefenseMultiplier(world, unitId);

        /// <summary>위키 공식으로 공격 피해와 반격 피해를 한 번에 계산한다. targetHp는 대상의 공격 전 체력(반격도 이 값 기준).</summary>
        public static void Resolve(EntityWorld world, int attackerId, int targetId, int targetHp, out int attackResult, out int defenseResult)
        {
            double atk = world.Get<Attack>(attackerId).Value;
            double def = BaseDefense(world, targetId);
            double attackForce = atk * HealthRatio(world.Get<Hp>(attackerId).Value, world.Get<MaxHp>(attackerId).Value);
            double defenseForce = def * HealthRatio(targetHp, world.Get<MaxHp>(targetId).Value) * DefenseMultiplier(world, targetId);
            double total = attackForce + defenseForce;
            if (total <= 0) { attackResult = 0; defenseResult = 0; return; }
            double k = GameRules.Combat.DamageCoefficient;
            attackResult = RoundHalfUp(attackForce / total * atk * k);
            defenseResult = RoundHalfUp(defenseForce / total * def * k);
        }

        /// <summary>지금 상태에서 공격자가 대상에게 주는 피해(0일 수 있다 — 공격 0인 사제 등).</summary>
        public static int CalculateDamage(EntityWorld world, int attackerId, int targetId)
        {
            Resolve(world, attackerId, targetId, world.Get<Hp>(targetId).Value, out int damage, out _);
            return damage;
        }

        /// <summary>대상(defenderId)이 공격자에게 되돌려주는 반격 피해 — 위키대로 대상의 "공격 전" 체력으로 계산한다.</summary>
        public static int CalculateRetaliation(EntityWorld world, int attackerId, int defenderId, int defenderHpBeforeHit)
        {
            Resolve(world, attackerId, defenderId, defenderHpBeforeHit, out _, out int retaliation);
            return retaliation;
        }

        /// <summary>위키 "Battle Preview": 지금 공격하면 되돌려받을 반격 피해 예상. 대상이 이 공격으로 죽거나, 반격 패시브가 없거나,
        /// 뻣뻣함/공격자 기습·전향, 대상 사거리 밖이면 0 — CombatSystem.TryAttack의 반격 조건과 같다. AI가 반격에 죽는 공격을 피할 때 쓴다.</summary>
        public static int PreviewRetaliation(EntityWorld world, int attackerId, int targetId)
        {
            int hp = world.Get<Hp>(targetId).Value;
            Resolve(world, attackerId, targetId, hp, out int damage, out int retaliation);
            if (damage >= hp) return 0;
            if (UnitActionQueries.Find<CounterAction>(world, targetId) == null || UnitActionQueries.Find<StiffAction>(world, targetId) != null) return 0;
            if (UnitActionQueries.Find<AmbushAction>(world, attackerId) != null || UnitActionQueries.Find<ConvertAction>(world, attackerId) != null) return 0;
            return IsInAttackRange(world, targetId, attackerId) ? retaliation : 0;
        }

        /// <summary>스플래시 피해 = 그 대상에게 계산한 공격 피해 / GameRules.Combat.SplashDivisor(내림).</summary>
        public static int CalculateSplashDamage(EntityWorld world, int attackerId, int targetId) =>
            CalculateDamage(world, attackerId, targetId) / Mathf.Max(1, GameRules.Combat.SplashDivisor);

        private static double HealthRatio(int hp, int maxHp) => maxHp <= 0 ? 1.0 : (double)Mathf.Max(0, hp) / maxHp;

        /// <summary>.5 올림 반올림(Math.Round의 은행가 반올림이면 전사 vs 전사 4.5가 4가 되어 위키 5와 달라진다).</summary>
        private static int RoundHalfUp(double x) => (int)System.Math.Floor(x + 0.5 + 1e-9);

        /// <summary>공격 행동(AttackAction)을 찾아 실행하고, 공격이 성사되어 대상이 살아남았으면 전향
        /// (ConvertAction) 여부를 먼저 반영한 뒤 대상의 반격 행동(CounterAction)을 찾아 이어서 실행한다.
        /// 공격자가 기습(AmbushAction)을 가졌거나 대상이 뻣뻣함(StiffAction)을 가졌으면 반격 자체를
        /// 건너뛴다. 공격이 실제로 성사됐는지와 별개로, 대상이 반격으로 되돌려준 피해량을
        /// counterDamageDealt로 함께 돌려준다(반격이 없었거나 발동하지 않았으면 0).</summary>
        public static bool TryAttack(GridWorld grid, EntityWorld world, int attackerId, int targetId, out int damageDealt, out int counterDamageDealt)
        {
            damageDealt = 0;
            counterDamageDealt = 0;

            var attack = UnitActionQueries.Find<AttackAction>(world, attackerId);
            // 위키 공식은 반격도 대상의 "공격 전" 체력으로 계산한다 — 공격이 체력을 깎기 전에 기억해 둔다(지역 변수, 상태 아님).
            int targetHpBeforeHit = UnitQueries.IsAlive(world, targetId) ? world.Get<Hp>(targetId).Value : 0;
            if (attack == null || !attack.Execute(grid, world, attackerId, targetId, out damageDealt)) return false;

            if (UnitQueries.IsAlive(world, targetId))
            {
                // 전향: 공격자가 가졌으면 반격 판정보다 먼저 대상의 팀을 공격자 팀으로 바꾼다 — 그래야
                // 뒤이은 반격이 (이미 팀이 같아진) CounterAction.Execute의 팀 체크로 걸러진다.
                if (UnitActionQueries.Find<ConvertAction>(world, attackerId) != null)
                    world.Set(targetId, world.Get<Team>(attackerId));

                if (UnitActionQueries.Find<AmbushAction>(world, attackerId) == null &&
                    UnitActionQueries.Find<StiffAction>(world, targetId) == null)
                {
                    var counter = UnitActionQueries.Find<CounterAction>(world, targetId);
                    counter?.ExecuteRetaliation(grid, world, targetId, attackerId, targetHpBeforeHit, out counterDamageDealt);
                }
            }

            return true;
        }

        /// <summary>방어 행동(DefendAction)을 찾아 실행한다.</summary>
        public static bool TryDefend(GridWorld grid, EntityWorld world, int unitId)
        {
            var defend = UnitActionQueries.Find<DefendAction>(world, unitId);
            return defend != null && defend.Execute(grid, world, unitId, out _);
        }
    }
}
