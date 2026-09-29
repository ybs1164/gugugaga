namespace TacticsECS
{
    /// <summary>
    /// 베테랑(위키 Units "Veteran Units") — 처치 수 기록과 승급. 순수 함수형, 자체 상태 없음(값은 Kills/Veteran/MaxHp 컴포넌트).
    ///   - 공격 또는 반격으로 적을 처치하면 처치한 유닛의 Kills +1(스플래시 처치 포함 — 공격의 일부).
    ///   - Kills가 GameRules.Veteran.KillsRequired(3) 이상이면 승급할 수 있다: 최대 체력 +MaxHpBonus(5) 후 완전 회복, 한 번만.
    ///     승급 시점은 플레이어가 고른다(위키: 나중에 "공짜 회복"으로 쓰려고 미뤄도 된다). 원문에 행동 소모 여부가 없어 행동을 쓰지 않는다.
    ///   - 승급할 수 없는 유닛(위키): 배(승선 중이거나 물 유닛), 슈퍼 유닛(GameRules.City.SuperUnitId), 고정(Static) 스킬 유닛(Cloak/Dagger).
    /// </summary>
    public static class VeteranSystem
    {
        /// <summary>killerId가 적 하나를 처치했다(공격/반격/스플래시).</summary>
        public static void RecordKill(EntityWorld world, int killerId)
        {
            if (killerId < 0) return;
            world.Set(killerId, new Kills { Value = world.GetOrDefault<Kills>(killerId).Value + 1 });
        }

        public static bool IsVeteran(EntityWorld world, int unitId) => world.GetOrDefault<Veteran>(unitId).Value;

        /// <summary>승급 불가 유닛 종류인지(배/슈퍼 유닛/고정 스킬).</summary>
        public static bool CannotBePromoted(EntityWorld world, int unitId) =>
            EmbarkSystem.IsEmbarked(world, unitId) || world.Get<MoveDomain>(unitId).Value == TerrainType.Water ||
            CitySystem.IsSuperUnit(world.GetOrDefault<UnitTypeId>(unitId).Value) || UnitActionQueries.Find<StaticAction>(world, unitId) != null;

        public static bool CanPromote(EntityWorld world, int unitId) =>
            UnitQueries.IsAlive(world, unitId) && !IsVeteran(world, unitId) &&
            world.GetOrDefault<Kills>(unitId).Value >= GameRules.Veteran.KillsRequired && !CannotBePromoted(world, unitId);

        public static bool Promote(EntityWorld world, int unitId)
        {
            if (!CanPromote(world, unitId)) return false;
            MakeVeteran(world, unitId);
            return true;
        }

        /// <summary>조건 없이 베테랑으로 만든다(유적 "New Friends"처럼 베테랑으로 태어나는 유닛): 최대 체력 +5, 완전 회복.</summary>
        public static void MakeVeteran(EntityWorld world, int unitId)
        {
            int maxHp = world.Get<MaxHp>(unitId).Value + GameRules.Veteran.MaxHpBonus;
            world.Set(unitId, new MaxHp { Value = maxHp });
            world.Set(unitId, new Hp { Value = maxHp });
            world.Set(unitId, new Veteran { Value = true });
        }
    }
}
