using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 신앙 포인트 계산. 자체 상태 없이 CityResourceData를 받아 새 값을 돌려준다.
    /// 규칙: docs/spec/game/faith.md
    /// </summary>
    public static class FaithSystem
    {
        /// <summary>팀의 신앙 최대 보유량 = 기본 + 도시 레벨 합 × 레벨당 증가량. 세부: 신전·기술 등은 이후 단계에서 여기에 더한다.</summary>
        public static int MaxFaith(GridWorld grid, EconomyWorld econ, Team team)
        {
            int max = GameRules.Faith.BaseCap;
            for (int i = 0; i < econ.Cities.Count; i++)
            {
                if (econ.Cities[i].Owner != team) continue;
                max += econ.Cities[i].Level * GameRules.Faith.CityLevelCap;
            }
            return Mathf.Max(0, max);
        }

        /// <summary>상한을 다시 계산해 적고, 현재량이 새 상한을 넘으면 상한까지 줄인다.</summary>
        public static CityResourceData RefreshMax(CityResourceData resources, GridWorld grid, EconomyWorld econ, Team team)
        {
            resources.MaxFaith = MaxFaith(grid, econ, team);
            resources.Faith = Mathf.Clamp(resources.Faith, 0, resources.MaxFaith);
            return resources;
        }

        /// <summary>신앙을 더한다(음수면 뺀다). 상한을 넘는 부분은 버려지고 0 아래로는 내려가지 않는다.</summary>
        public static CityResourceData Add(CityResourceData resources, int amount)
        {
            resources.Faith = Mathf.Clamp(resources.Faith + amount, 0, resources.MaxFaith);
            return resources;
        }

        /// <summary>자기 팀의 살아있는 유닛 수.</summary>
        public static int CountUnits(EntityWorld world, Team team)
        {
            int count = 0;
            for (int i = 0; i < world.EntityCount; i++)
                if (world.Get<Team>(i) == team && UnitQueries.IsAlive(world, i)) count++;
            return count;
        }

        /// <summary>공동 순위: 나보다 유닛이 많은 팀 수 + 1. 같은 수면 같은 순위다.</summary>
        public static int RankOf(int[] unitCounts, int index)
        {
            int rank = 1;
            for (int i = 0; i < unitCounts.Length; i++)
                if (unitCounts[i] > unitCounts[index]) rank++;
            return rank;
        }

        /// <summary>순위와 턴 번호로 정해지는 턴 시작 획득량. 4위 이하는 순위 보너스가 없다.</summary>
        public static int GainFor(int rank, int turnNumber)
        {
            int bonus = rank == 1 ? GameRules.Faith.RankBonus1
                      : rank == 2 ? GameRules.Faith.RankBonus2
                      : rank == 3 ? GameRules.Faith.RankBonus3 : 0;
            int growth = GameRules.Faith.TurnsPerGrowth > 0 ? Mathf.Max(0, turnNumber - 1) / GameRules.Faith.TurnsPerGrowth : 0;
            return GameRules.Faith.BaseGain + bonus + growth;
        }

        /// <summary>이 팀의 순위와 그 순위의 획득량(HUD 미리보기와 턴 시작 지급이 같은 값을 쓴다).</summary>
        public static int GainPreview(EntityWorld world, Team team, int turnNumber, out int rank)
        {
            var teams = CitySystem.Teams;
            var counts = new int[teams.Length];
            int self = 0;
            for (int i = 0; i < teams.Length; i++)
            {
                counts[i] = CountUnits(world, teams[i]);
                if (teams[i] == team) self = i;
            }
            rank = RankOf(counts, self);
            return GainFor(rank, turnNumber);
        }

        /// <summary>턴 시작 지급.</summary>
        public static CityResourceData ApplyTurnStart(CityResourceData resources, GridWorld grid, EntityWorld world, EconomyWorld econ, Team team, int turnNumber)
        {
            resources = RefreshMax(resources, grid, econ, team);
            return Add(resources, GainPreview(world, team, turnNumber, out _));
        }
    }
}
