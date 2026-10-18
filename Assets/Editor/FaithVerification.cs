using System.Collections.Generic;
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
            VerifyMythic();
            VerifyGreatTemple();
            VerifyFaithAI();
            VerifyFaithResearch();
            VerifyGreatTempleAccess();
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
            foreach (var team in CitySystem.Teams) { econ.Resources[team] = new CityResourceData(); econ.Tech[team] = TechTreeData.CreateEmpty(); }
            TaskSystem.Init(econ);
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
            Check(Mathf.Approximately(CombatSystem.BaseDefense(world, near), GameRules.Faith.PrayDefense) && defense > GameRules.Faith.PrayDefense, "기도 중 방어 스탯 = Faith.PrayDefense");
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

        private static FaithEffectRow EffectOf(string id)
        {
            int i = System.Array.FindIndex(GameTables.FaithEffects, e => e.Id == id);
            Check(i >= 0, "FaithEffects.csv has " + id);
            return i >= 0 ? GameTables.FaithEffects[i] : default;
        }

        /// <summary>신의 첫 패시브의 첫 효과(샘플 신은 효과 하나씩).</summary>
        private static FaithEffectRow PassiveEffect(GodRow god) => FaithEffectSystem.Rows(TemplePassiveSystem.Passives(god)[0].Effects)[0];

        private static void VerifyGods()
        {
            Check(GameTables.Gods.Length >= 6, "Gods.csv has a sample god per passive kind");
            Check(GameTables.Gods.All(g => g.Passives != null && g.Passives.Length > 0 && TemplePassiveSystem.Passives(g).Count == g.Passives.Length), "every sample god has valid passives");
            foreach (var kind in new[] { FaithEffectKind.Stat, FaithEffectKind.GuardStat, FaithEffectKind.Damage, FaithEffectKind.Structure, FaithEffectKind.Stars, FaithEffectKind.FaithCap })
                Check(GameTables.Gods.Any(g => TemplePassiveSystem.Passives(g).Any(p => FaithEffectSystem.Rows(p.Effects).Any(e => e.Effect == kind))), "a god with passive effect " + kind);
            Check(GameTables.Gods.All(g => !string.IsNullOrEmpty(g.Name) && !g.Name.StartsWith("God.") && !g.Description.Contains("{")), "god names and descriptions are filled");
            Check(ArrayTableValidationSystem.ValidateFaith(GameTables.FaithEffects, GameTables.TemplePassives, GameTables.FaithPunches, GameTables.MythicUnits, GameTables.Gods).Count == 0, "faith tables cross-reference cleanly");
            {
                var badPunch = GameTables.FaithPunches[0];
                badPunch.Effects = new[] { System.Array.FindIndex(GameTables.FaithEffects, e => e.Effect == FaithEffectKind.Stat) };
                Check(ArrayTableValidationSystem.ValidateFaith(GameTables.FaithEffects, GameTables.TemplePassives, new[] { badPunch }, new MythicRow[0], new GodRow[0]).Count == 1, "a passive-only effect in a punch is reported");
            }

            // 신 없음: 신전이 있어도 아무 효과 없다.
            {
                var grid = NewGrid(); var econ = NewEconomy(grid, out var world);
                BuildTemple(grid, new Vector2Int(2, 2), "Temple");
                int u = Unit(grid, world, Team.Player, new Vector2Int(1, 2));
                TemplePassiveSystem.RefreshAuras(grid, world, econ);
                Check(world.GetOrDefault<TempleAura>(u).Attack == 0f && TemplePassiveSystem.GoldIncome(grid, world, econ, Team.Player) == 0, "no god, no passive");
            }

            // 능력치형 + 기도 유닛 수에 따른 강화.
            {
                var grid = NewGrid(); var econ = NewEconomy(grid, out var world);
                var god = GameTables.Gods[GodIndex("Sun")]; econ.GodIds[Team.Player] = god.Index;
                var e = PassiveEffect(god);
                BuildTemple(grid, new Vector2Int(2, 2), "Temple");
                int near = Unit(grid, world, Team.Player, new Vector2Int(1, 2));
                int far = Unit(grid, world, Team.Player, new Vector2Int(0, 0));
                int enemy = Unit(grid, world, Team.Enemy, new Vector2Int(3, 3));
                TemplePassiveSystem.RefreshAuras(grid, world, econ);
                Check(Mathf.Approximately(world.GetOrDefault<TempleAura>(near).Attack, e.Amount), "aura: adjacent ally gets Amount");
                Check(world.GetOrDefault<TempleAura>(far).Attack == 0f && world.GetOrDefault<TempleAura>(enemy).Attack == 0f, "aura: far ally and enemy get nothing");
                float baseAttack = world.Get<Attack>(near).Value;
                Check(Mathf.Approximately(CombatSystem.EffectiveAttack(world, near), baseAttack + e.Amount), "aura raises effective attack");
                PrayerSystem.Pray(grid, world, near);
                TemplePassiveSystem.RefreshAuras(grid, world, econ);
                Check(Mathf.Approximately(world.GetOrDefault<TempleAura>(near).Attack, e.Amount + e.PerPrayer), "aura grows with praying units");
            }

            // 방어 태세형: 방어 태세일 때만.
            {
                var grid = NewGrid(); var econ = NewEconomy(grid, out var world);
                var god = GameTables.Gods[GodIndex("Guardian")]; econ.GodIds[Team.Player] = god.Index;
                BuildTemple(grid, new Vector2Int(2, 2), "Temple");
                int u = Unit(grid, world, Team.Player, new Vector2Int(1, 2));
                TemplePassiveSystem.RefreshAuras(grid, world, econ);
                float plain = CombatSystem.BaseDefense(world, u);
                world.Set(u, new IsGuarding { Value = true });
                Check(Mathf.Approximately(CombatSystem.BaseDefense(world, u), plain + GameRules.Combat.GuardDefenseBonus + PassiveEffect(god).Amount), "buff: guarding ally gains Amount");
            }

            // 피해형: 인접 적, 체력 MinHp 미만으로는 안 깎는다.
            {
                var grid = NewGrid(); var econ = NewEconomy(grid, out var world);
                var god = GameTables.Gods[GodIndex("Storm")]; econ.GodIds[Team.Player] = god.Index;
                var e = PassiveEffect(god);
                BuildTemple(grid, new Vector2Int(2, 2), "Temple");
                int enemy = Unit(grid, world, Team.Enemy, new Vector2Int(3, 3));
                int far = Unit(grid, world, Team.Enemy, new Vector2Int(5, 5));
                int hp = world.Get<Hp>(enemy).Value;
                var hit = TemplePassiveSystem.OnTurnStart(grid, world, econ, Team.Player, 2);
                Check(hit.Contains(enemy) && world.Get<Hp>(enemy).Value == hp - Mathf.RoundToInt(e.Amount), "damage: adjacent enemy loses Amount hp");
                Check(!hit.Contains(far), "damage: far enemy untouched");
                world.Set(enemy, new Hp { Value = e.MinHp });
                TemplePassiveSystem.OnTurnStart(grid, world, econ, Team.Player, 2);
                Check(e.MinHp >= 1 && world.Get<Hp>(enemy).Value == e.MinHp, "damage stops at MinHp");
            }

            // 구조물형: Period 턴마다.
            {
                var grid = NewGrid(); var econ = NewEconomy(grid, out var world);
                var god = GameTables.Gods[GodIndex("Harvest")]; econ.GodIds[Team.Player] = god.Index;
                var passive = TemplePassiveSystem.Passives(god)[0];
                var e = PassiveEffect(god);
                BuildTemple(grid, new Vector2Int(2, 2), "Temple");
                TemplePassiveSystem.OnTurnStart(grid, world, econ, Team.Player, passive.Period + 1);
                Check(CountStructure(grid, e.TargetId) == 0, "resource: nothing off-period");
                TemplePassiveSystem.OnTurnStart(grid, world, econ, Team.Player, passive.Period);
                Check(CountStructure(grid, e.TargetId) == Mathf.RoundToInt(e.Amount), "resource: Amount structures on the period");
            }

            // 별형 / 신앙 최대치형.
            {
                var grid = NewGrid(); var econ = NewEconomy(grid, out var world);
                BuildTemple(grid, new Vector2Int(2, 2), "Temple");
                econ.GodIds[Team.Player] = GodIndex("Merchant");
                var plain = CityResourceSystem.RefreshProduction(new CityResourceData(), grid, world, econ, Team.Enemy).StarsProduction;
                var merchant = CityResourceSystem.RefreshProduction(new CityResourceData(), grid, world, econ, Team.Player);
                Check(TemplePassiveSystem.GoldIncome(grid, world, econ, Team.Player) == Mathf.RoundToInt(PassiveEffect(GameTables.Gods[GodIndex("Merchant")]).Amount), "gold: income per temple");
                Check(merchant.StarsProduction >= plain, "gold is part of star production");

                econ.GodIds[Team.Player] = -1;
                int without = FaithSystem.MaxFaith(grid, world, econ, Team.Player);
                econ.GodIds[Team.Player] = GodIndex("Sage");
                Check(FaithSystem.MaxFaith(grid, world, econ, Team.Player) == without + Mathf.RoundToInt(PassiveEffect(GameTables.Gods[GodIndex("Sage")]).Amount), "faith cap: bonus per temple");
            }

            // 패시브 여러 개: 한 신이 두 패시브를 가지면 둘 다 작용한다.
            {
                var grid = NewGrid(); var econ = NewEconomy(grid, out var world);
                int sun = GodIndex("Sun");
                var saved = GameTables.Gods[sun];
                var both = saved;
                both.Passives = new[] { saved.Passives[0], TemplePassiveSystem.Passives(GameTables.Gods[GodIndex("Merchant")])[0].Index };
                GameTables.Gods[sun] = both;
                try
                {
                    econ.GodIds[Team.Player] = sun;
                    BuildTemple(grid, new Vector2Int(2, 2), "Temple");
                    int near = Unit(grid, world, Team.Player, new Vector2Int(1, 2));
                    TemplePassiveSystem.RefreshAuras(grid, world, econ);
                    Check(world.GetOrDefault<TempleAura>(near).Attack > 0f && TemplePassiveSystem.GoldIncome(grid, world, econ, Team.Player) > 0, "two passives both apply");
                }
                finally { GameTables.Gods[sun] = saved; }
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
                Check(world.Get<Hp>(e1).Value == hp1 - FaithPunchSystem.Amount(EffectOf("Smite")) && world.Get<Hp>(ally).Value == hpAlly, "strike damage");
                Check(result.Attacked, "strike counts as an attack");
                Check(econ.Resources[Team.Player].Faith == 2, "strike spends faith");
                world.Set(e2, new Hp { Value = 1 });
                var res2 = econ.Resources[Team.Player]; res2.Faith = punch.Cost; econ.Resources[Team.Player] = res2;
                FaithPunchSystem.Apply(grid, world, econ, Team.Player, punch, target);
                Check(!UnitQueries.IsAlive(world, e2) && grid.GetOccupant(new Vector2Int(5, 6)) == TileData.NoOccupant, "a unit killed by a strike frees its tile");
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
                var bounty = EffectOf("Bounty");
                Check(result.Success && result.Tiles.Count == FaithPunchSystem.Amount(bounty) && CountStructure(grid, bounty.TargetId) == FaithPunchSystem.Amount(bounty), "resource creates Amount structures");
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

        private static void VerifyMythic()
        {
            Check(GameTables.MythicUnits.Length >= 2, "MythicUnits.csv samples");
            foreach (var m in GameTables.MythicUnits)
                Check(System.Array.Exists(GameTables.Units, u => u.Id == m.Id) && m.Upkeep > 0, $"mythic {m.Id} has a Units.csv row and upkeep");

            var grid = NewGrid(); var econ = NewEconomy(grid, out var world);
            econ.GodIds[Team.Player] = GodIndex("Sun");
            var mythicRow = MythicSystem.MythicOf(econ, Team.Player).Value;
            var descent = GameTables.FaithPunches[PunchIndex("Descent")];
            var res = econ.Resources[Team.Player]; res.MaxFaith = 100; res.Faith = 100; econ.Resources[Team.Player] = res;

            var pos = new Vector2Int(3, 3);
            Check(FaithPunchSystem.Check(grid, world, econ, Team.Player, descent, pos) == "UI.FaithPunch.NoDescendTarget", "descend needs an ally");
            int origin = Unit(grid, world, Team.Player, pos);
            world.Set(origin, new Hp { Value = 7 });
            int enemy = Unit(grid, world, Team.Enemy, new Vector2Int(5, 3));
            int farEnemy = Unit(grid, world, Team.Enemy, new Vector2Int(7, 3));
            int unitsBefore = FaithSystem.CountUnits(world, Team.Player);

            var result = FaithPunchSystem.Apply(grid, world, econ, Team.Player, descent, pos);
            Check(result.Success && result.SpawnedIds.Count == 1, "descend succeeds");
            int mythic = result.SpawnedIds[0];
            Check(!UnitQueries.IsAlive(world, origin) && grid.GetOccupant(pos) == mythic, "original is preserved off the board, mythic takes the tile");
            Check(world.Get<Hp>(mythic).Value == world.Get<MaxHp>(mythic).Value && world.Get<Hp>(mythic).Value != 7, "original stats are not carried over");
            Check(world.Get<Mythic>(mythic).TurnsLeft == mythicRow.Upkeep && MythicSystem.FindMythic(world, Team.Player) == mythic, "upkeep granted, one mythic per team");
            Check(FaithSystem.CountUnits(world, Team.Player) == unitsBefore, "swap does not change unit count");
            Check(FaithPunchSystem.Check(grid, world, econ, Team.Player, descent, pos) == "UI.FaithPunch.MythicExists", "only one mythic at a time");

            // 액티브: 사거리·비용·유지 턴.
            Check(MythicSystem.CanUseActive(world, mythic), "mythic can use its active");
            Check(MythicSystem.CheckActive(grid, world, econ, mythic, new Vector2Int(7, 3)) == "UI.Mythic.Reason.Reach", "active out of reach refused");
            int faith = econ.Resources[Team.Player].Faith, hp = world.Get<Hp>(enemy).Value;
            var used = MythicSystem.UseActive(grid, world, econ, mythic, new Vector2Int(5, 3));
            var active = MythicSystem.ActivePunchRow(mythicRow);
            Check(used.Success && world.Get<Hp>(enemy).Value == hp - FaithPunchSystem.Amount(FaithPunchSystem.Effects(active)[0]) && world.Get<Hp>(farEnemy).Value == world.Get<MaxHp>(farEnemy).Value, "active hits with its own punch row");
            Check(econ.Resources[Team.Player].Faith == faith - active.Cost, "active costs its punch cost");
            Check(world.Get<Mythic>(mythic).TurnsLeft == mythicRow.Upkeep - mythicRow.ActiveUpkeepCost && world.Get<HasActed>(mythic).Value, "active costs upkeep and ends the action");
            Check(!MythicSystem.CanUseActive(world, mythic), "one active per turn");

            // 유지 턴이 끝나면 신화 유닛이 원본과 함께 소멸한다(기획서).
            int turns = world.Get<Mythic>(mythic).TurnsLeft;
            for (int i = 0; i < turns - 1; i++) MythicSystem.OnTurnStart(grid, world, Team.Player);
            Check(MythicSystem.IsMythic(world, mythic), "mythic stays while upkeep remains");
            MythicSystem.OnTurnStart(grid, world, Team.Player);
            Check(!UnitQueries.IsAlive(world, mythic) && !UnitQueries.IsAlive(world, origin) && grid.GetOccupant(pos) == TileData.NoOccupant, "mythic and original vanish when upkeep runs out");

            // 신화 유닛이 죽으면 원본도 함께 제거된다.
            origin = UnitFactorySystem.CreateFromCsv(grid, world, Team.Player, GameTables.Units.First(u => u.Id == "infantry"), pos);
            var again = FaithPunchSystem.Apply(grid, world, econ, Team.Player, descent, pos);
            int mythic2 = again.SpawnedIds[0];
            world.Set(mythic2, new Hp { Value = 0 });
            MythicSystem.Sweep(grid, world);
            Check(!UnitQueries.IsAlive(world, origin) && !UnitQueries.IsAlive(world, mythic2), "mythic death removes the original too");
            Check(grid.GetOccupant(pos) == TileData.NoOccupant && MythicSystem.FindMythic(world, Team.Player) < 0, "tile freed, no mythic left");
        }

        private static void VerifyGreatTemple()
        {
            var info = GreatTempleSystem.Info();
            Check(info != null && info.Value.IsGreatTemple, "GreatTemple building row");
            var task = TaskSystem.Find(GreatTempleSystem.TaskId);
            Check(task != null && task.Value.Kind == TaskKind.FaithHeld, "GreatTemple task is FaithHeld");

            var grid = NewGrid(); var econ = NewEconomy(grid, out var world);
            TaskSystem.Init(econ);
            var res = econ.Resources[Team.Player]; res.Stars = 50; res.MaxFaith = 100; res.Faith = 0; econ.Resources[Team.Player] = res;
            var origin = new Vector2Int(2, 2);

            Check(GreatTempleSystem.CanBuild(grid, world, econ, Team.Player, origin) == "UI.GreatTemple.NeedTask", "needs the task");
            Check(!TaskSystem.IsMet(grid, econ, Team.Player, task.Value), "task not met without faith");
            res.Faith = task.Value.Threshold; econ.Resources[Team.Player] = res;
            Check(TaskSystem.IsMet(grid, econ, Team.Player, task.Value), "task met when holding enough faith");
            econ.Tasks[Team.Player].Completed.Add(GreatTempleSystem.TaskId);

            Check(GreatTempleSystem.CanBuild(grid, world, econ, Team.Player, new Vector2Int(6, 6)) == "UI.GreatTemple.NoSite", "needs own territory");
            Check(GreatTempleSystem.CanBuild(grid, world, econ, Team.Player, origin) == null, "buildable on connected empty plains");
            Check(GreatTempleSystem.FindSite(grid, world, econ, Team.Player, origin).Count == GameRules.Faith.GreatTempleMinTiles, "site has the required tile count");

            Check(GreatTempleSystem.Build(grid, world, econ, Team.Player, origin, 1), "build");
            var temple = econ.GreatTemples[0];
            Check(temple.Tiles.All(t => grid.GetTile(t).BuildingId == GreatTempleSystem.BuildingId), "every tile carries the building");
            Check(temple.Tiles.All(t => VisionSystem.IsExplored(grid, Team.Enemy, t)), "location revealed to every team");
            Check(econ.Resources[Team.Player].Stars == 50 - info.Value.Cost, "build costs stars");
            Check(GreatTempleSystem.CanBuild(grid, world, econ, Team.Player, origin) == "UI.GreatTemple.Exists", "one per team");

            // 보호: 인접 적이 있으면 처음부터 다시 센다.
            int protect = GameRules.Faith.GreatTempleProtectTurns;
            GreatTempleSystem.OnTurnStart(grid, world, econ, Team.Player, 1 + protect - 1);
            Check(econ.GreatTemples[0].State == GreatTempleState.Building, "still guarded before the protection time");
            int intruder = Unit(grid, world, Team.Enemy, new Vector2Int(3, 3));
            GreatTempleSystem.Refresh(grid, world, econ, 1 + protect - 1);
            Check(econ.GreatTemples[0].StartTurn == protect, "intruder restarts the protection count");
            GreatTempleSystem.OnTurnStart(grid, world, econ, Team.Player, 1 + protect);
            Check(econ.GreatTemples[0].State == GreatTempleState.Building, "protection does not finish while the intruder stays");
            world.Set(intruder, new Hp { Value = 0 });
            grid.RemoveOccupant(new Vector2Int(3, 3));
            int done = 1 + protect + protect;
            GreatTempleSystem.OnTurnStart(grid, world, econ, Team.Player, done);
            Check(econ.GreatTemples[0].State == GreatTempleState.Waiting && econ.FaithWinner < 0, "protection completes into the tiebreak wait");

            // 타이브레이크: 살아 있는 상대의 대신전이 있으면 보류, 상대가 전멸하면 승리.
            Unit(grid, world, Team.Enemy, new Vector2Int(6, 6));
            econ.GreatTemples.Add(new GreatTempleData { Owner = Team.Enemy, Tiles = new[] { new Vector2Int(6, 5) }, State = GreatTempleState.Building, StartTurn = done, CompleteTurn = -1 });
            GreatTempleSystem.OnTurnStart(grid, world, econ, Team.Player, done + GameRules.Faith.GreatTempleTieBreakTurns);
            Check(econ.FaithWinner < 0, "rival great temple suspends the win");
            Check(GreatTempleSystem.StatusLine(world, econ, Team.Player, done).Contains("\n") || GreatTempleSystem.StatusLine(world, econ, Team.Player, done).Length > 0, "status line shows progress");
            for (int i = 0; i < world.EntityCount; i++) if (world.Get<Team>(i) == Team.Enemy) world.Set(i, new Hp { Value = 0 });
            GreatTempleSystem.OnTurnStart(grid, world, econ, Team.Player, done + GameRules.Faith.GreatTempleTieBreakTurns);
            Check(econ.FaithWinner == (int)Team.Player, "sole great temple wins");
            Check(ScoreSystem.ComputeBreakdown(grid, world, econ, Team.Player).FaithVictory == GameRules.Score.FaithVictory, "victory bonus in the breakdown");

            // 일반 타일 메뉴에는 나오지 않는다.
            econ.Tech[Team.Player] = TechTreeData.CreateEmpty();
            Check(!TileImprovementSystem.GetOptions(grid, econ, Team.Player, new Vector2Int(0, 0)).Any(o => o.Id == GreatTempleSystem.BuildingId), "not a regular tile option");
        }

        private static void SetFaithOf(EconomyWorld econ, Team team, int faith, int max)
        {
            var r = econ.Resources[team]; r.MaxFaith = max; r.Faith = faith; econ.Resources[team] = r;
        }

        private static void VerifyFaithAI()
        {
            var ai = Team.Enemy;
            int storm = GodIndex("Storm");

            // 신이 없으면 신앙 AI는 아무것도 하지 않는다.
            {
                var grid = NewGrid(); var econ = NewEconomy(grid, out var world);
                BuildTemple(grid, new Vector2Int(5, 5), "Temple");
                int u = Unit(grid, world, ai, new Vector2Int(5, 6));
                FaithAI.RunTurn(grid, world, econ, ai, new List<EconomyLogEntry>());
                Check(!world.GetOrDefault<Praying>(u).Value, "AI without a god does nothing");
            }

            // 기도: 신전 곁 + 적이 멀 때만.
            {
                var grid = NewGrid(); var econ = NewEconomy(grid, out var world);
                econ.GodIds[ai] = storm;
                BuildTemple(grid, new Vector2Int(5, 5), "Temple");
                int safe = Unit(grid, world, ai, new Vector2Int(5, 6));
                int far = Unit(grid, world, ai, new Vector2Int(7, 7));
                Unit(grid, world, Team.Player, new Vector2Int(0, 0));
                FaithAI.RunTurn(grid, world, econ, ai, new List<EconomyLogEntry>());
                Check(world.GetOrDefault<Praying>(safe).Value && world.Get<HasActed>(safe).Value, "AI prays next to its temple when no enemy is near");
                Check(!world.GetOrDefault<Praying>(far).Value, "AI units away from the temple do not pray");

                var grid2 = NewGrid(); var econ2 = NewEconomy(grid2, out var world2);
                econ2.GodIds[ai] = storm;
                BuildTemple(grid2, new Vector2Int(5, 5), "Temple");
                int busy = Unit(grid2, world2, ai, new Vector2Int(5, 6));
                Unit(grid2, world2, Team.Player, new Vector2Int(5, 3));
                FaithAI.RunTurn(grid2, world2, econ2, ai, new List<EconomyLogEntry>());
                Check(!world2.GetOrDefault<Praying>(busy).Value, "AI does not pray with an enemy close by");
            }

            // 신전 건설.
            {
                var grid = NewGrid(); var econ = NewEconomy(grid, out var world);
                econ.TechNodes = GameDataLoader.LoadTechNodes();
                econ.Tech[ai] = TechTreeData.CreateEmpty();
                foreach (var n in econ.TechNodes) econ.Tech[ai].Unlocked.Add(n.Id);
                econ.GodIds[ai] = storm;
                var res = econ.Resources[ai]; res.Stars = 100; econ.Resources[ai] = res;
                var log = new List<EconomyLogEntry>();
                FaithAI.RunTurn(grid, world, econ, ai, log);
                Check(TemplePassiveSystem.Temples(grid, ai).Count >= 1, "AI builds a temple near its capital");
                Check(econ.Resources[ai].Stars < 100, "temple costs stars");
            }

            // 천벌: 신앙이 가득 차면(낭비 방지) 가장 이득인 칸에 쓴다.
            {
                var grid = NewGrid(); var econ = NewEconomy(grid, out var world);
                econ.GodIds[ai] = storm;
                SetFaithOf(econ, ai, 20, 20);
                int target = Unit(grid, world, Team.Player, new Vector2Int(3, 3));
                int hp = world.Get<Hp>(target).Value;
                var log = new List<EconomyLogEntry>();
                FaithAI.RunTurn(grid, world, econ, ai, log);
                Check(world.Get<Hp>(target).Value <= hp - FaithPunchSystem.Amount(EffectOf("Smite")), "AI strikes a visible enemy");
                Check(econ.Resources[ai].Faith < 20 && log.Count > 0, "AI spends faith and logs it");

                // 신앙이 낮고 위협도 없으면 아끼고 쓰지 않는다.
                var grid2 = NewGrid(); var econ2 = NewEconomy(grid2, out var world2);
                econ2.GodIds[ai] = storm;
                SetFaithOf(econ2, ai, 8, 20);
                int safeTarget = Unit(grid2, world2, Team.Player, new Vector2Int(1, 3));
                int hp2 = world2.Get<Hp>(safeTarget).Value;
                FaithAI.RunTurn(grid2, world2, econ2, ai, new List<EconomyLogEntry>());
                Check(world2.Get<Hp>(safeTarget).Value == hp2 && econ2.Resources[ai].Faith == 8, "AI saves faith when not threatened and not capped");
            }

            // 강림 + 신화 유닛 액티브.
            {
                int idx = PunchIndex("Smite");
                var saved = GameTables.FaithPunches[idx];
                var blocked = saved; blocked.Cost = 99; // 천벌을 막아 강림이 고르게 한다
                GameTables.FaithPunches[idx] = blocked;
                try
                {
                    var grid = NewGrid(); var econ = NewEconomy(grid, out var world);
                    econ.GodIds[ai] = GodIndex("Storm");
                    SetFaithOf(econ, ai, 20, 20);
                    int own = Unit(grid, world, ai, new Vector2Int(5, 5));
                    int victim = Unit(grid, world, Team.Player, new Vector2Int(3, 5));
                    int hp = world.Get<Hp>(victim).Value;
                    FaithAI.RunTurn(grid, world, econ, ai, new List<EconomyLogEntry>());
                    int mythic = MythicSystem.FindMythic(world, ai);
                    Check(mythic >= 0 && !UnitQueries.IsAlive(world, own), "AI descends its unit when an enemy is near");

                    // 같은 턴에 신화 유닛 액티브(약해진 천벌)도 사거리 안의 적에게 쓴다. 원본 천벌은 막혀 있어도 액티브는 자기 비용을 쓴다.
                    var row = GameTables.MythicUnits[world.Get<Mythic>(mythic).MythicIndex];
                    Check(row.ActiveReach >= 2 && world.Get<HasActed>(mythic).Value && world.Get<Hp>(victim).Value < hp, "AI uses the mythic active on an enemy in reach");
                }
                finally { GameTables.FaithPunches[idx] = saved; }
            }

            // 대신전.
            {
                var grid = NewGrid(); var econ = NewEconomy(grid, out var world);
                TaskSystem.Init(econ);
                econ.GodIds[ai] = storm;
                var res = econ.Resources[ai]; res.Stars = 50; econ.Resources[ai] = res;
                FaithAI.RunTurn(grid, world, econ, ai, new List<EconomyLogEntry>());
                Check(GreatTempleSystem.IndexOf(econ, ai) < 0, "AI waits for the great temple task");
                econ.Tasks[ai].Completed.Add(GreatTempleSystem.TaskId);
                FaithAI.RunEarly(grid, world, econ, ai, new List<EconomyLogEntry>());
                Check(GreatTempleSystem.IndexOf(econ, ai) >= 0, "AI builds the great temple once the task is done");
            }
        }

        private static void VerifyFaithResearch()
        {
            var ai = Team.Enemy;
            var grid = NewGrid(); var econ = NewEconomy(grid, out var world);
            econ.TechNodes = GameDataLoader.LoadTechNodes();
            Check(FaithAI.PreferredTech(econ, ai) == null, "no god, no preferred tech");
            econ.GodIds[ai] = GodIndex("Storm");

            var templeKeys = BuildingDefinition.All.Where(b => b.FaithCap > 0 && !string.IsNullOrEmpty(b.UnlockKey)).Select(b => b.UnlockKey).ToList();
            var taskKey = TaskSystem.Find(GreatTempleSystem.TaskId).Value.UnlockKey;
            bool temple = false, task = false;
            for (int step = 0; step < 16; step++)
            {
                string id = FaithAI.PreferredTech(econ, ai);
                if (id == null) break;
                var res = econ.Resources[ai]; res.Stars = 1000; econ.Resources[ai] = res;
                Check(TechSystem.CanUnlock(econ.TechNodes, econ.Tech[ai], econ.Resources[ai], 1, id), $"preferred tech {id} is researchable now");
                if (!TechSystem.Unlock(econ.TechNodes, econ.Tech[ai], ref res, 1, id)) break;
                econ.Resources[ai] = res;
                temple |= templeKeys.Any(k => TechSystem.HasUnlock(econ.TechNodes, econ.Tech[ai], k));
                task |= TechSystem.HasUnlock(econ.TechNodes, econ.Tech[ai], taskKey);
            }
            Check(temple, "following the preference unlocks a temple");
            Check(task, "following the preference unlocks the great temple task");
            Check(FaithAI.PreferredTech(econ, ai) == null, "nothing left to prefer");
        }

        private static void VerifyGreatTempleAccess()
        {
            var ai = Team.Enemy;

            // 숲과 자원 칸도 대신전 자리로 쓸 수 있고, 지으면 치워진다. 유적 같은 구조물은 막는다.
            {
                var grid = NewGrid(); var econ = NewEconomy(grid, out var world);
                foreach (var p in new[] { new Vector2Int(2, 2), new Vector2Int(1, 2), new Vector2Int(2, 1), new Vector2Int(0, 2) })
                {
                    var t = grid.GetTile(p); t.TileTypeId = TerrainGenerationSystem.ForestTileId; grid.SetTile(p, t);
                }
                var res0 = grid.GetTile(new Vector2Int(2, 1)); res0.StructureId = "Resource_Fruit"; grid.SetTile(new Vector2Int(2, 1), res0);
                econ.Tasks[Team.Player].Completed.Add(GreatTempleSystem.TaskId);
                var r = econ.Resources[Team.Player]; r.Stars = 50; econ.Resources[Team.Player] = r;
                Check(GreatTempleSystem.CanBuild(grid, world, econ, Team.Player, new Vector2Int(2, 2)) == null, "forest and resource tiles can host the great temple");
                GreatTempleSystem.Build(grid, world, econ, Team.Player, new Vector2Int(2, 2), 1);
                var cleared = econ.GreatTemples[0].Tiles.All(t => grid.GetTile(t).TileTypeId != TerrainGenerationSystem.ForestTileId && string.IsNullOrEmpty(grid.GetTile(t).StructureId) && grid.GetTile(t).BuildingId == GreatTempleSystem.BuildingId);
                Check(cleared, "building clears forest and resources on its tiles");

                var grid2 = NewGrid(); var econ2 = NewEconomy(grid2, out var world2);
                foreach (var p in new[] { new Vector2Int(2, 2), new Vector2Int(1, 2), new Vector2Int(2, 1), new Vector2Int(0, 2), new Vector2Int(0, 1), new Vector2Int(2, 0), new Vector2Int(1, 0), new Vector2Int(0, 0) })
                {
                    var t = grid2.GetTile(p); t.StructureId = "Ruin"; grid2.SetTile(p, t);
                }
                econ2.Tasks[Team.Player].Completed.Add(GreatTempleSystem.TaskId);
                var r2 = econ2.Resources[Team.Player]; r2.Stars = 50; econ2.Resources[Team.Player] = r2;
                Check(GreatTempleSystem.CanBuild(grid2, world2, econ2, Team.Player, new Vector2Int(2, 2)) == "UI.GreatTemple.NoSite", "ruins block the great temple");
            }

            // 별 비축: 과업을 채우고 자리가 있는데 별이 모자라면 AI는 그 비용을 남겨 둔다(연구·건설·훈련을 미룬다).
            {
                var grid = NewGrid(); var econ = NewEconomy(grid, out var world);
                econ.TechNodes = GameDataLoader.LoadTechNodes();
                econ.UnitRows.Add(new UnitCsvRow { Id = "infantry", Name = "보병", MaxHp = 10, Defense = 1, Cost = 2, BaseVisual = "Melee",
                    Actions = ActionType.Move | ActionType.Attack, MoveRange = 1, AttackAttack = 4, AttackRange = 1 });
                econ.GodIds[ai] = GodIndex("Storm");
                econ.Tasks[ai].Completed.Add(GreatTempleSystem.TaskId);
                int cost = GreatTempleSystem.Info().Value.Cost;
                var res = econ.Resources[ai]; res.Stars = cost - 1; econ.Resources[ai] = res;
                Check(FaithAI.StarReserve(grid, world, econ, ai) == cost, "reserve equals the great temple cost while it is unaffordable");
                EconomyAI.RunTurn(grid, world, econ, ai);
                Check(econ.Resources[ai].Stars == cost - 1 && GreatTempleSystem.IndexOf(econ, ai) < 0, "AI saves stars instead of spending them");

                res = econ.Resources[ai]; res.Stars = cost; econ.Resources[ai] = res;
                EconomyAI.RunTurn(grid, world, econ, ai);
                Check(GreatTempleSystem.IndexOf(econ, ai) >= 0, "AI builds the great temple as soon as it can pay");
                Check(FaithAI.StarReserve(grid, world, econ, ai) == 0, "no reserve once built");
            }
        }
    }
}
