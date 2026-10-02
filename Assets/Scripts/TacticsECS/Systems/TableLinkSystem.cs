using System.Collections.Generic;

namespace TacticsECS
{
    /// <summary>
    /// 배열형 표 사이의 Index 연결을 런타임 값(Id/해금 키)으로 푼다 — 표 관계는 docs/spec/csv/tech.md.
    ///   - TechUnlocks의 BuildingIndex/UnitIndex/BoatIndex → 그 행의 Target(= 가리킨 행의 Id)
    ///   - 그 결과로 건물의 UnlockKey(BuildingInfo.UnlockKey)
    ///   - Boats의 Kind → NavalUnitDefinition의 뗏목/업그레이드/특수 배
    /// 범위 밖 Index는 건너뛴다(경고는 ArrayTableValidationSystem). 자체 상태는 없다.
    /// </summary>
    public static class TableLinkSystem
    {
        /// <summary>Index로 대상을 가리키는 해금 내역의 Target을 그 대상의 Id로 채운다(배열을 그 자리에서 고친다).</summary>
        public static void ResolveUnlockTargets(TechUnlockRow[] unlocks, IReadOnlyList<BuildingInfo> buildings,
            IReadOnlyList<UnitCsvRow> units, IReadOnlyList<BoatRow> boats)
        {
            for (int i = 0; i < unlocks.Length; i++)
            {
                var u = unlocks[i];
                if (InRange(u.BuildingIndex, buildings)) u.Target = buildings[u.BuildingIndex].Id;
                else if (InRange(u.UnitIndex, units)) u.Target = units[u.UnitIndex].Id;
                else if (InRange(u.BoatIndex, boats)) u.Target = boats[u.BoatIndex].Unit.Id;
                unlocks[i] = u;
            }
        }

        /// <summary>BuildingIndex로 건물을 가리키는 해금 내역의 키를 그 건물의 UnlockKey로. 가리키는 내역이 없는 건물은 기술 없이 지을 수 있다.</summary>
        public static void ApplyBuildingUnlocks(BuildingInfo[] buildings, IReadOnlyList<TechUnlockRow> unlocks)
        {
            for (int b = 0; b < buildings.Length; b++) buildings[b].UnlockKey = string.Empty;
            foreach (var u in unlocks)
                if (InRange(u.BuildingIndex, buildings) && string.IsNullOrEmpty(buildings[u.BuildingIndex].UnlockKey))
                    buildings[u.BuildingIndex].UnlockKey = TechGroupSystem.Key(u);
        }

        /// <summary>배 표를 종류별로 나눈다. 업그레이드 배의 해금 키는 "Unit.&lt;Id&gt;"(TechUnlocks의 BoatIndex 행이 같은 키를 만든다).
        /// Raft 행이 없으면 raft는 null.</summary>
        public static void SplitBoats(IReadOnlyList<BoatRow> boats, out UnitCsvRow raft, out (UnitCsvRow Row, string UnlockKey)[] upgrades,
            out UnitCsvRow[] special)
        {
            raft = null;
            var up = new List<(UnitCsvRow, string)>();
            var sp = new List<UnitCsvRow>();
            foreach (var b in boats)
            {
                if (b.Kind == BoatKind.Raft) { if (raft == null) raft = b.Unit; }
                else if (b.Kind == BoatKind.Upgrade) up.Add((b.Unit, TechSystem.UnitKeyPrefix + b.Unit.Id));
                else sp.Add(b.Unit);
            }
            upgrades = up.ToArray();
            special = sp.ToArray();
        }

        private static bool InRange<T>(int i, IReadOnlyList<T> list) => list != null && i >= 0 && i < list.Count;
    }
}
