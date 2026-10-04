using UnityEngine;

namespace TacticsECS
{
    // 규칙: docs/spec/game/building.md#대사관
    public static class DiplomacySystem
    {
        private static (Team, Team) Pair(Team a, Team b) => (int)a < (int)b ? (a, b) : (b, a);
        public static bool AtWar(EconomyWorld econ, Team a, Team b) => econ != null && econ.Wars.Contains(Pair(a, b));
        public static bool HasPeaceTreaty(EconomyWorld econ, Team a, Team b) => econ != null && econ.PeaceTreaties.Contains(Pair(a, b));

        public static void DeclareWar(EconomyWorld econ, Team a, Team b)
        {
            if (econ == null || a == b) return;
            econ.Wars.Add(Pair(a, b));
            econ.PeaceTreaties.Remove(Pair(a, b));
            econ.Embassies.RemoveAll(e =>
                e.CityIndex < 0 || e.CityIndex >= econ.Cities.Count ||
                (e.Sender == a && econ.Cities[e.CityIndex].Owner == b) ||
                (e.Sender == b && econ.Cities[e.CityIndex].Owner == a));
            RefreshDisplays(econ);
        }

        // 호출자는 양쪽의 조약 동의를 확인한 뒤 적용한다.
        public static void SetPeaceTreaty(EconomyWorld econ, Team a, Team b, bool active)
        {
            if (econ == null || a == b) return;
            if (active) { econ.Wars.Remove(Pair(a, b)); econ.PeaceTreaties.Add(Pair(a, b)); }
            else econ.PeaceTreaties.Remove(Pair(a, b));
        }

        public static bool HasEmbassy(EconomyWorld econ, Team sender, int cityIndex) =>
            econ.Embassies.Exists(e => e.Sender == sender && e.CityIndex == cityIndex);

        public static bool CanBuild(EconomyWorld econ, Team sender, int cityIndex)
        {
            if (econ == null || cityIndex < 0 || cityIndex >= econ.Cities.Count) return false;
            var city = econ.Cities[cityIndex];
            return city.IsCapital && city.Owner == city.OriginalOwner && city.Owner != sender &&
                !AtWar(econ, sender, city.Owner) && !HasEmbassy(econ, sender, cityIndex);
        }

        public static void Build(GridWorld grid, EconomyWorld econ, Team sender, int cityIndex)
        {
            if (!CanBuild(econ, sender, cityIndex)) return;
            econ.Embassies.Add(new EmbassyData { Sender = sender, CityIndex = cityIndex });
            RefreshDisplays(econ);
            VisionSystem.Reveal(grid, sender, econ.Cities[cityIndex].Position, GameRules.Diplomacy.EmbassySightRadius);
        }

        public static int Income(EconomyWorld econ, Team team)
        {
            int total = 0;
            foreach (var embassy in econ.Embassies)
            {
                if (embassy.CityIndex < 0 || embassy.CityIndex >= econ.Cities.Count) continue;
                var city = econ.Cities[embassy.CityIndex];
                if (!city.IsCapital || city.Owner != city.OriginalOwner || AtWar(econ, embassy.Sender, city.Owner)) continue;
                if (team != embassy.Sender && team != city.Owner) continue;
                total += GameRules.Diplomacy.EmbassyIncome *
                    (HasPeaceTreaty(econ, embassy.Sender, city.Owner) ? GameRules.Diplomacy.PeaceTreatyIncomeMultiplier : 1);
            }
            return total;
        }

        public static void RefreshDisplays(EconomyWorld econ)
        {
            for (int i = 0; i < econ.Cities.Count; i++)
            {
                var city = econ.Cities[i];
                city.HasEmbassy = econ.Embassies.Exists(e => e.CityIndex == i);
                econ.Cities[i] = city;
            }
        }
    }
}
