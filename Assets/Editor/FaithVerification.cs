using System.Linq;
using UnityEngine;

namespace TacticsECS.EditorTools
{
    /// <summary>
    /// 신앙 포인트(FaithSystem)를 GameObject 없이 검증하는 배치모드 스크립트. 규칙: docs/spec/game/faith.md
    /// 사용법: unity run . -- -nographics -executeMethod TacticsECS.EditorTools.FaithVerification.Run
    /// </summary>
    public static class FaithVerification
    {
        private static bool _ok;

        public static void Run()
        {
            GameDataLoader.LoadAll();
            _ok = true;
            VerifyRank();
            VerifyGain();
            VerifyCapAndClamp();
            VerifyTurnStart();
            VerifyTempleCap();
            VerifyPrayer();
            VerifyGods();
            VerifyFaithPunches();
            Debug.Log(_ok ? "[FaithVerification] ALL PASS" : "[FaithVerification] SOME CHECKS FAILED - see errors above");
        }

        private static void Check(bool condition, string message)
        {
            if (condition) return;
            _ok = false;
            Debug.LogError("[FaithVerification] FAIL: " + message);
        }

        private static void VerifyRank()
        {
            Check(FaithSystem.RankOf(new[] { 3, 1 }, 0) == 1, "유닛이 많은 팀은 1위");
            Check(FaithSystem.RankOf(new[] { 3, 1 }, 1) == 2, "유닛이 적은 팀은 2위");
            Check(FaithSystem.RankOf(new[] { 2, 2 }, 0) == 1 && FaithSystem.RankOf(new[] { 2, 2 }, 1) == 1, "동수는 공동 1위");
            Check(FaithSystem.RankOf(new[] { 3, 3, 1 }, 2) == 3, "공동 1위 다음은 3위");
        }

        private static void VerifyGain()
        {
            int first = FaithSystem.GainFor(1, 1), second = FaithSystem.GainFor(2, 1), fourth = FaithSystem.GainFor(4, 1), fifth = FaithSystem.GainFor(5, 1);
            Check(first > second && second > FaithSystem.GainFor(3, 1) && FaithSystem.GainFor(3, 1) > fourth, "순위가 높을수록 획득량이 크다");
            Check(fourth == fifth, "4위 이하는 같은 양");
            Check(fourth == GameRules.Faith.BaseGain, "4위는 기본 획득량");
            int late = 1 + GameRules.Faith.TurnsPerGrowth;
            Check(FaithSystem.GainFor(1, late) == first + 1, "TurnsPerGrowth 턴마다 획득량 +1");
        }

        private static EconomyWorld NewEconomy(GridWorld grid, out EntityWorld world)
        {
            world = new EntityWorld();
            var econ = new EconomyWorld();
            foreach (var team in CitySystem.Teams) econ.Resources[team] = new CityResourceData();
            CitySystem.FoundCity(grid, econ, new Vector2Int(1, 1), Team.Player, true, "수도");
            CitySystem.FoundCity(grid, econ, new Vector2Int(6, 6), Team.Enemy, true, "수도");
            return econ;
        }

        private static GridWorld NewGrid()
        {
            var grid = new GridWorld(8, 8);
            for (int y = 0; y < 8; y++) for (int x = 0; x < 8; x++) grid.SetTileType(new Vector2Int(x, y), "Grass");
            return grid;
        }

        private static int Unit(GridWorld grid, EntityWorld world, Team team, Vector2Int pos)
        {
            var row = new UnitCsvRow { Id = "infantry", Name = "보병", MaxHp = 10, Defense = 1, Cost = 2,
                Actions = ActionType.Move | ActionType.Attack, MoveRange = 1, AttackAttack = 4, AttackRange = 1 };
            return UnitFactorySystem.CreateFromCsv(grid, world, team, row, pos);
        }

        private static void VerifyCapAndClamp()
        {
            var grid = NewGrid();
            var econ = NewEconomy(grid, out var world);
            var r = FaithSystem.RefreshMax(new CityResourceData(), grid, world, econ, Team.Player);
            int expected = GameRules.Faith.BaseCap + econ.Cities[0].Level * GameRules.Faith.CityLevelCap;
            Check(r.MaxFaith == expected, $"최대치 = 기본 + 도시 레벨 × 레벨당 증가량 (기대 {expected}, 실제 {r.MaxFaith})");

            r = FaithSystem.Add(r, r.MaxFaith + 50);
            Check(r.Faith == r.MaxFaith, "최대치를 넘는 신앙은 버려진다");
            r = FaithSystem.Add(r, -r.MaxFaith - 5);
            Check(r.Faith == 0, "신앙은 0 아래로 내려가지 않는다");

            r = FaithSystem.Add(r, r.MaxFaith);
            var c = econ.Cities[0]; c.Level = 1; econ.Cities[0] = c;
            r = FaithSystem.RefreshMax(r, grid, world, econ, Team.Player);
            Check(r.MaxFaith == GameRules.Faith.BaseCap + GameRules.Faith.CityLevelCap, "도시 레벨이 내려가면 최대치도 내려간다");
            Check(r.Faith == r.MaxFaith, "최대치가 줄면 현재량도 새 상한까지 줄어든다");
        }

        private static void VerifyTurnStart()
        {
            var grid = NewGrid();
            var econ = NewEconomy(grid, out var world);
            Unit(grid, world, Team.Player, new Vector2Int(1, 2));
            Unit(grid, world, Team.Player, new Vector2Int(2, 2));
            Unit(grid, world, Team.Enemy, new Vector2Int(6, 5));

            var player = FaithSystem.ApplyTurnStart(econ.Resources[Team.Player], grid, world, econ, Team.Player, 1);
            var enemy = FaithSystem.ApplyTurnStart(econ.Resources[Team.Enemy], grid, world, econ, Team.Enemy, 1);
            Check(player.Faith == FaithSystem.GainFor(1, 1), $"유닛이 더 많은 팀은 1위 획득량 (실제 {player.Faith})");
            Check(enemy.Faith == FaithSystem.GainFor(2, 1), $"유닛이 적은 팀은 2위 획득량 (실제 {enemy.Faith})");

            int preview = FaithSystem.GainPreview(world, Team.Player, 1, out int rank);
            Check(rank == 1 && preview == player.Faith, "미리보기와 실제 지급이 같은 값");

            world.Set(0, new Hp { Value = 0 });
            Check(FaithSystem.CountUnits(world, Team.Player) == 1, "죽은 유닛은 순위 계산에서 빠진다");
        }
        private static void BuildTemple(GridWorld grid, Vector2Int p, string id)
        {
            var tile = grid.GetTile(p);
            tile.BuildingId = id;
            grid.SetTile(p, tile);
        }

        private static void VerifyTempleCap()
        {
            var grid = NewGrid();
            var econ = NewEconomy(grid, out var world);
            int before = FaithSystem.MaxFaith(grid, world, econ, Team.Player);
            BuildTemple(grid, new Vector2Int(2, 2), "Temple");
            int cap = TileImprovementSystem.FindBuilding("Temple").Value.FaithCap;
            Check(cap > 0, "Buildings.csv Temple has FaithCap");
            Check(FaithSystem.MaxFaith(grid, world, econ, Team.Player) == before + cap, "자기 영토 신전이 최대치를 늘린다");
            Check(FaithSystem.MaxFaith(grid, world, econ, Team.Enemy) == GameRules.Faith.BaseCap + econ.Cities[1].Level * GameRules.Faith.CityLevelCap, "남의 신전은 영향 없음");

            var r = FaithSystem.RefreshMax(new CityResourceData(), grid, world, econ, Team.Player);
            r = FaithSystem.Add(r, r.MaxFaith);
            BuildTemple(grid, new Vector2Int(2, 2), string.Empty);
            r = FaithSystem.RefreshMax(r, grid, world, econ, Team.Player);
            Check(r.MaxFaith == before && r.Faith == before, "신전이 사라지면 최대치와 현재량이 함께 줄어든다");
        }

        private static void VerifyPrayer()
        {
            var grid = NewGrid();
            var econ = NewEconomy(grid, out var world);
            BuildTemple(grid, new Vector2Int(2, 2), "Temple");
            int near = Unit(grid, world, Team.Player, new Vector2Int(1, 2));
            int far = Unit(grid, world, Team.Player, new Vector2Int(0, 0));
            int enemy = Unit(grid, world, Team.Enemy, new Vector2Int(1, 3));

            Check(PrayerSystem.CanPray(grid, world, near), "신전 8칸 안의 유닛은 기도할 수 있다");
            Check(!PrayerSystem.CanPray(grid, world, far), "신전에서 먼 유닛은 기도할 수 없다");
            Check(!PrayerSystem.CanPray(grid, world, enemy), "남의 신전에서는 기도할 수 없다");

            float defense = CombatSystem.BaseDefense(world, near);
            Check(PrayerSystem.Pray(grid, world, near), "기도 시작");
            Check(world.Get<HasActed>(near).Value && world.Get<HasMoved>(near).Value, "기도하면 행동이 끝난다");
            Check(CombatSystem.BaseDefense(world, near) == 0f && defense > 0f, "기도 중 방어력 0");
            Check(!PrayerSystem.CanPray(grid, world, near), "이미 기도 중이면 다시 못 한다");

            var res = FaithSystem.RefreshMax(new CityResourceData(), grid, world, econ, Team.Player);
            var withPrayer = FaithSystem.ApplyTurnStart(res, grid, world, econ, Team.Player, 1);
            var noPrayerWorld = FaithSystem.GainPreview(world, Team.Player, 1, out _);
            Check(withPrayer.Faith == noPrayerWorld + GameRules.Faith.PrayGain, "기도 유닛 하나당 PrayGain 추가");

            var attack = UnitActionQueries.Find<AttackAction>(world, enemy);
            Check(attack != null && attack.Execute(grid, world, enemy, near, out _), "적이 기도 중인 유닛을 공격");
            Check(!world.Get<Praying>(near).Value, "피격되면 기도가 풀린다");
            Check(CombatSystem.BaseDefense(world, near) > 0f, "풀리면 방어력 복구");

            world.Set(near, new HasActed { Value = false });
            PrayerSystem.Pray(grid, world, near);
            BuildTemple(grid, new Vector2Int(2, 2), string.Empty);
            PrayerSystem.ReleaseStale(grid, world, Team.Player);
            Check(!world.Get<Praying>(near).Value, "신전이 사라지면 기도가 풀린다");
        }

        private static int GodIndex(string id)
        {
            for (int i = 0; i < GameTables.Gods.Length; i++) if (GameTables.Gods[i].Id == id) return i;
            Check(false, "Gods.csv has " + id);
            return -1;
        }

        private static void VerifyGods()
        {
            Check(GameTables.Gods.Length >= 6, "Gods.csv has a sample god per passive kind");
            foreach (var kind in new[] { GodPassiveKind.Aura, GodPassiveKind.Buff, GodPassiveKind.Damage, GodPassiveKind.Resource, GodPassiveKind.Gold, GodPassiveKind.FaithCap })
                Check(System.Array.Exists(GameTables.Gods, g => g.Passive == kind), "a god with passive " + kind);
            Check(GameTables.Gods.All(g => !string.IsNullOrEmpty(g.Name) && !g.Name.StartsWith("God.") && !g.Description.Contains("{")), "god names and descriptions are filled");

            // 신 없음: 신전이 있어도 아무 효과 없다.
            {
                var grid = NewGrid(); var econ = NewEconomy(grid, out var world);
                BuildTemple(grid, new Vector2Int(2, 2), "Temple");
                int u = Unit(grid, world, Team.Player, new Vector2Int(1, 2));
                TemplePassiveSystem.RefreshAuras(grid, world, econ);
                Check(world.GetOrDefault<TempleAura>(u).Attack == 0f && TemplePassiveSystem.GoldIncome(grid, world, econ, Team.Player) == 0, "no god, no passive");
            }

            // 오라형 + 기도 유닛 수에 따른 강화.
            {
                var grid = NewGrid(); var econ = NewEconomy(grid, out var world);
                var god = GameTables.Gods[GodIndex("Sun")]; econ.GodIds[Team.Player] = god.Index;
                BuildTemple(grid, new Vector2Int(2, 2), "Temple");
                int near = Unit(grid, world, Team.Player, new Vector2Int(1, 2));
                int far = Unit(grid, world, Team.Player, new Vector2Int(0, 0));
                int enemy = Unit(grid, world, Team.Enemy, new Vector2Int(3, 3));
                TemplePassiveSystem.RefreshAuras(grid, world, econ);
                Check(Mathf.Approximately(world.GetOrDefault<TempleAura>(near).Attack, god.Value), "aura: adjacent ally gets Value");
                Check(world.GetOrDefault<TempleAura>(far).Attack == 0f && world.GetOrDefault<TempleAura>(enemy).Attack == 0f, "aura: far ally and enemy get nothing");
                float baseAttack = world.Get<Attack>(near).Value;
                Check(Mathf.Approximately(CombatSystem.EffectiveAttack(world, near), baseAttack + god.Value), "aura raises effective attack");
                PrayerSystem.Pray(grid, world, near);
                TemplePassiveSystem.RefreshAuras(grid, world, econ);
                Check(Mathf.Approximately(world.GetOrDefault<TempleAura>(near).Attack, god.Value + god.PerPrayer), "aura grows with praying units");
            }

            // 버프형: 방어 태세일 때만.
            {
                var grid = NewGrid(); var econ = NewEconomy(grid, out var world);
                var god = GameTables.Gods[GodIndex("Guardian")]; econ.GodIds[Team.Player] = god.Index;
                BuildTemple(grid, new Vector2Int(2, 2), "Temple");
                int u = Unit(grid, world, Team.Player, new Vector2Int(1, 2));
                TemplePassiveSystem.RefreshAuras(grid, world, econ);
                float plain = CombatSystem.BaseDefense(world, u);
                world.Set(u, new IsGuarding { Value = true });
                Check(Mathf.Approximately(CombatSystem.BaseDefense(world, u), plain + GameRules.Combat.GuardDefenseBonus + god.Value), "buff: guarding ally gains Value");
            }

            // 피해형: 인접 적, 체력 1 미만으로는 안 깎는다.
            {
                var grid = NewGrid(); var econ = NewEconomy(grid, out var world);
                var god = GameTables.Gods[GodIndex("Storm")]; econ.GodIds[Team.Player] = god.Index;
                BuildTemple(grid, new Vector2Int(2, 2), "Temple");
                int enemy = Unit(grid, world, Team.Enemy, new Vector2Int(3, 3));
                int far = Unit(grid, world, Team.Enemy, new Vector2Int(5, 5));
                int hp = world.Get<Hp>(enemy).Value;
                var hit = TemplePassiveSystem.OnTurnStart(grid, world, econ, Team.Player, 2);
                Check(hit.Contains(enemy) && world.Get<Hp>(enemy).Value == hp - Mathf.RoundToInt(god.Value), "damage: adjacent enemy loses Value hp");
                Check(!hit.Contains(far), "damage: far enemy untouched");
                world.Set(enemy, new Hp { Value = 1 });
                TemplePassiveSystem.OnTurnStart(grid, world, econ, Team.Player, 2);
                Check(world.Get<Hp>(enemy).Value == 1, "damage never kills");
            }

            // 자원형: Period 턴마다.
            {
                var grid = NewGrid(); var econ = NewEconomy(grid, out var world);
                var god = GameTables.Gods[GodIndex("Harvest")]; econ.GodIds[Team.Player] = god.Index;
                BuildTemple(grid, new Vector2Int(2, 2), "Temple");
                TemplePassiveSystem.OnTurnStart(grid, world, econ, Team.Player, god.Period + 1);
                Check(CountStructure(grid, god.Target) == 0, "resource: nothing off-period");
                TemplePassiveSystem.OnTurnStart(grid, world, econ, Team.Player, god.Period);
                Check(CountStructure(grid, god.Target) == Mathf.RoundToInt(god.Value), "resource: Value structures on the period");
            }

            // 골드형 / 신앙형.
            {
                var grid = NewGrid(); var econ = NewEconomy(grid, out var world);
                BuildTemple(grid, new Vector2Int(2, 2), "Temple");
                econ.GodIds[Team.Player] = GodIndex("Merchant");
                var plain = CityResourceSystem.RefreshProduction(new CityResourceData(), grid, world, econ, Team.Enemy).StarsProduction;
                var merchant = CityResourceSystem.RefreshProduction(new CityResourceData(), grid, world, econ, Team.Player);
                Check(TemplePassiveSystem.GoldIncome(grid, world, econ, Team.Player) == Mathf.RoundToInt(GameTables.Gods[GodIndex("Merchant")].Value), "gold: income per temple");
                Check(merchant.StarsProduction >= plain, "gold is part of star production");

                econ.GodIds[Team.Player] = -1;
                int without = FaithSystem.MaxFaith(grid, world, econ, Team.Player);
                econ.GodIds[Team.Player] = GodIndex("Sage");
                Check(FaithSystem.MaxFaith(grid, world, econ, Team.Player) == without + Mathf.RoundToInt(GameTables.Gods[GodIndex("Sage")].Value), "faith cap: bonus per temple");
            }
        }

        private static int CountStructure(GridWorld grid, string id)
        {
            int n = 0;
            for (int i = 0; i < grid.Tiles.Length; i++) if (grid.Tiles[i].StructureId == id) n++;
            return n;
        }

        private static int PunchIndex(string id)
        {
            for (int i = 0; i < GameTables.FaithPunches.Length; i++) if (GameTables.FaithPunches[i].Id == id) return i;
            Check(false, "FaithPunches.csv has " + id);
            return 0;
        }

        private static void VerifyFaithPunches()
        {
            Check(GameTables.FaithPunches.Length >= 4, "FaithPunches.csv samples");
            Check(GameTables.FaithPunches.All(p => !string.IsNullOrEmpty(p.Name) && !p.Name.StartsWith("FaithPunch.") && !p.Description.Contains("{")), "punch names and descriptions are filled");
            foreach (var god in GameTables.Gods)
                Check(god.Punches != null && god.Punches.Length > 0 && god.Punches.All(i => i >= 0 && i < GameTables.FaithPunches.Length), $"god {god.Id} has valid punches");

            // 신에 따라 쓸 수 있는 목록.
            {
                var grid = NewGrid(); var econ = NewEconomy(grid, out var world);
                Check(FaithPunchSystem.Available(econ, Team.Player).Count == 0, "no god, no punches");
                econ.GodIds[Team.Player] = GodIndex("Storm");
                Check(FaithPunchSystem.Available(econ, Team.Player).Count == GameTables.Gods[GodIndex("Storm")].Punches.Length, "god punches listed");
            }

            // 천벌: 범위 안 적에게만 피해, 신앙 소모, 모자라면 불가.
            {
                var grid = NewGrid(); var econ = NewEconomy(grid, out var world);
                var punch = GameTables.FaithPunches[PunchIndex("Smite")];
                int e1 = Unit(grid, world, Team.Enemy, new Vector2Int(5, 5));
                int e2 = Unit(grid, world, Team.Enemy, new Vector2Int(5, 6));
                int far = Unit(grid, world, Team.Enemy, new Vector2Int(7, 7));
                int ally = Unit(grid, world, Team.Player, new Vector2Int(4, 5));
                var target = new Vector2Int(5, 5);
                Check(FaithPunchSystem.Check(grid, world, econ, Team.Player, punch, target) == "UI.FaithPunch.NeedFaith", "strike needs faith");
                var res = econ.Resources[Team.Player]; res.MaxFaith = 50; res.Faith = punch.Cost + 2; econ.Resources[Team.Player] = res;
                int hp1 = world.Get<Hp>(e1).Value, hpAlly = world.Get<Hp>(ally).Value;
                Check(FaithPunchSystem.Check(grid, world, econ, Team.Player, punch, target) == null, "strike allowed");
                Check(FaithPunchSystem.AffectedCount(grid, world, Team.Player, punch, target) == 2, "strike preview counts enemies in range");
                var result = FaithPunchSystem.Apply(grid, world, econ, Team.Player, punch, target);
                Check(result.Success && result.UnitIds.Contains(e1) && result.UnitIds.Contains(e2) && !result.UnitIds.Contains(far) && !result.UnitIds.Contains(ally), "strike hits enemies in range only");
                Check(world.Get<Hp>(e1).Value == hp1 - punch.Value && world.Get<Hp>(ally).Value == hpAlly, "strike damage");
                Check(econ.Resources[Team.Player].Faith == 2, "strike spends faith");
                Check(FaithPunchSystem.Check(grid, world, econ, Team.Player, punch, new Vector2Int(0, 7)) != null, "strike needs an enemy");
            }

            // 축복: 다친 아군만 회복.
            {
                var grid = NewGrid(); var econ = NewEconomy(grid, out var world);
                var punch = GameTables.FaithPunches[PunchIndex("Blessing")];
                int hurt = Unit(grid, world, Team.Player, new Vector2Int(3, 3));
                int healthy = Unit(grid, world, Team.Player, new Vector2Int(3, 4));
                world.Set(hurt, new Hp { Value = 2 });
                var res = econ.Resources[Team.Player]; res.MaxFaith = 50; res.Faith = 50; econ.Resources[Team.Player] = res;
                var result = FaithPunchSystem.Apply(grid, world, econ, Team.Player, punch, new Vector2Int(3, 3));
                Check(result.UnitIds.Count == 1 && result.UnitIds[0] == hurt && world.Get<Hp>(hurt).Value > 2, "boost heals wounded ally");
                Check(world.Get<Hp>(hurt).Value <= world.Get<MaxHp>(hurt).Value, "heal capped at max hp");
                world.Set(hurt, new Hp { Value = world.Get<MaxHp>(hurt).Value });
                Check(FaithPunchSystem.Check(grid, world, econ, Team.Player, punch, new Vector2Int(3, 3)) == "UI.FaithPunch.NoWounded", "nobody left to heal");
            }

            // 풍년: 빈 땅에 구조물, 최대 Value개.
            {
                var grid = NewGrid(); var econ = NewEconomy(grid, out var world);
                var punch = GameTables.FaithPunches[PunchIndex("Bounty")];
                var res = econ.Resources[Team.Player]; res.MaxFaith = 50; res.Faith = 50; econ.Resources[Team.Player] = res;
                var result = FaithPunchSystem.Apply(grid, world, econ, Team.Player, punch, new Vector2Int(4, 4));
                Check(result.Success && result.Tiles.Count == punch.Value && CountStructure(grid, punch.Target) == punch.Value, "resource creates Value structures");
            }

            // 지각 변동: 땅→물→산→땅, 유닛·도시 칸은 거부.
            {
                var grid = NewGrid(); var econ = NewEconomy(grid, out var world);
                var punch = GameTables.FaithPunches[PunchIndex("Upheaval")];
                var res = econ.Resources[Team.Player]; res.MaxFaith = 100; res.Faith = 100; econ.Resources[Team.Player] = res;
                var p = new Vector2Int(5, 3);
                Check(FaithPunchSystem.Apply(grid, world, econ, Team.Player, punch, p).TerrainChanged && grid.GetTile(p).Terrain == TerrainType.Water, "land becomes water");
                FaithPunchSystem.Apply(grid, world, econ, Team.Player, punch, p);
                Check(grid.GetTile(p).Terrain == TerrainType.Land && grid.GetTile(p).TileTypeId == TerrainGenerationSystem.MountainTileId, "water becomes mountain");
                FaithPunchSystem.Apply(grid, world, econ, Team.Player, punch, p);
                Check(grid.GetTile(p).Terrain == TerrainType.Land && grid.GetTile(p).TileTypeId != TerrainGenerationSystem.MountainTileId, "mountain becomes land");
                Unit(grid, world, Team.Player, new Vector2Int(5, 4));
                Check(FaithPunchSystem.Check(grid, world, econ, Team.Player, punch, new Vector2Int(5, 4)) == "UI.FaithPunch.TileBlocked", "unit tile refused");
                Check(FaithPunchSystem.Check(grid, world, econ, Team.Player, punch, new Vector2Int(1, 1)) == "UI.FaithPunch.TileBlocked", "city tile refused");

                BuildTemple(grid, new Vector2Int(2, 2), "Temple");
                FaithPunchSystem.Apply(grid, world, econ, Team.Player, punch, new Vector2Int(2, 2));
                Check(string.IsNullOrEmpty(grid.GetTile(new Vector2Int(2, 2)).BuildingId), "terrain change removes the building");
            }
        }
    }
}
