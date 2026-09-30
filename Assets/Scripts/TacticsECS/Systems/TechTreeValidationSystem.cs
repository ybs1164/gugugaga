using System.Collections.Generic;

namespace TacticsECS
{
    /// <summary>
    /// 커스텀 기술트리 CSV(샌드박스 "기술 불러오기")의 참조 오류를 찾는다 — 파싱(TechCsvSerializer)은 칸 형식만 보므로,
    /// 오타 난 선행 기술/해금 키처럼 "읽히긴 하지만 게임에서 아무 일도 안 일어나는" 실수를 여기서 잡는다. 자체 상태는 없다.
    /// 경고일 뿐 불러오기를 막지는 않는다(기획자가 일부러 비워 둔 키일 수도 있다).
    ///   - Parent가 트리에 없는 Id / 자기 자신 / 순환 → 그 기술은 영영 해금할 수 없다.
    ///   - Tier가 부모 이하 → 방사형 배치가 겹친다.
    ///   - 해금 키가 어떤 표에도 없음 → 아무 효과가 없다.
    /// 참고(TechSystem 규칙): 트리에서 빠진 Unit.* / Move.* / Reveal.* 키는 "잠기지 않음"(처음부터 가능)이지만, 건물·타일 행동·과업의
    /// 해금 키는 트리에 없으면 영영 쓸 수 없다 — MissingContentKeys가 그 목록을 알려 준다.
    /// </summary>
    public static class TechTreeValidationSystem
    {
        /// <summary>unitIds가 null이면 Unit.* 키의 유닛 Id는 확인하지 않는다(샌드박스 유닛 CSV를 아직 안 불러온 경우).</summary>
        public static List<string> Validate(IReadOnlyList<TechNodeData> nodes, IEnumerable<string> unitIds)
        {
            var warnings = new List<string>();
            if (nodes == null) return warnings;
            var known = KnownKeys();
            var units = unitIds != null ? new HashSet<string>(unitIds) : null;

            foreach (var n in nodes)
            {
                if (!string.IsNullOrEmpty(n.ParentId))
                {
                    var parent = TechSystem.Find(nodes, n.ParentId);
                    if (n.ParentId == n.Id) warnings.Add($"{n.Id}: 선행 기술이 자기 자신");
                    else if (parent == null) warnings.Add($"{n.Id}: 선행 기술 '{n.ParentId}'이(가) 트리에 없음");
                    else if (parent.Value.Tier >= n.Tier) warnings.Add($"{n.Id}: Tier {n.Tier}가 선행 기술 {parent.Value.Id}(Tier {parent.Value.Tier}) 이하");
                    if (HasCycle(nodes, n)) warnings.Add($"{n.Id}: 선행 기술이 순환함");
                }

                if (n.Unlocks == null) continue;
                foreach (var key in n.Unlocks)
                {
                    if (key.StartsWith(TechSystem.UnitKeyPrefix))
                    {
                        string unitId = key.Substring(TechSystem.UnitKeyPrefix.Length);
                        if (units != null && !units.Contains(unitId) && !known.Contains(key))
                            warnings.Add($"{n.Id}: 해금 키 '{key}' — 불러온 유닛 CSV에 '{unitId}'이(가) 없음");
                    }
                    else if (key.StartsWith(TechSystem.RevealKeyPrefix))
                    {
                        string structureId = key.Substring(TechSystem.RevealKeyPrefix.Length);
                        if (System.Array.FindIndex(StructureDefinition.All, s => s.Id == structureId) < 0)
                            warnings.Add($"{n.Id}: 해금 키 '{key}' — 구조물 '{structureId}'이(가) 없음");
                    }
                    else if (!known.Contains(key)) warnings.Add($"{n.Id}: 모르는 해금 키 '{key}' (효과 없음)");
                }
            }
            return warnings;
        }

        /// <summary>건물/타일 행동/과업/배 업그레이드 중 해금 키가 트리 어디에도 없어 영영 쓸 수 없는 것들의 키.</summary>
        public static List<string> MissingContentKeys(IReadOnlyList<TechNodeData> nodes)
        {
            var missing = new List<string>();
            foreach (var key in ContentKeys())
                if (!TechSystem.IsKeyGated(nodes, key) && !missing.Contains(key)) missing.Add(key);
            return missing;
        }

        private static bool HasCycle(IReadOnlyList<TechNodeData> nodes, TechNodeData start)
        {
            var seen = new HashSet<string> { start.Id };
            string parentId = start.ParentId;
            while (!string.IsNullOrEmpty(parentId))
            {
                if (!seen.Add(parentId)) return true;
                var parent = TechSystem.Find(nodes, parentId);
                if (parent == null) return false;
                parentId = parent.Value.ParentId;
            }
            return false;
        }

        private static HashSet<string> KnownKeys()
        {
            var keys = new HashSet<string>(TechTreeDefinition.FixedUnlockKeys);
            foreach (var k in ContentKeys()) keys.Add(k);
            return keys;
        }

        /// <summary>다른 규칙 표의 Unlock 칸 값(빈 칸 제외) — Buildings/TileActions/Tasks/NavalUnits.csv.</summary>
        private static IEnumerable<string> ContentKeys()
        {
            foreach (var b in BuildingDefinition.All) if (!string.IsNullOrEmpty(b.UnlockKey)) yield return b.UnlockKey;
            foreach (var a in TileActionDefinition.All) if (!string.IsNullOrEmpty(a.UnlockKey)) yield return a.UnlockKey;
            foreach (var t in TaskDefinition.All) if (!string.IsNullOrEmpty(t.UnlockKey)) yield return t.UnlockKey;
            foreach (var u in NavalUnitDefinition.Upgrades) if (!string.IsNullOrEmpty(u.UnlockKey)) yield return u.UnlockKey;
        }
    }
}
