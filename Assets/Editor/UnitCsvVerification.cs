using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TacticsECS.EditorTools
{
    /// <summary>
    /// CSV 유닛 파이프라인(파싱/왕복, 스폰 연동)을 자동으로 검증하는 배치모드 전용 스크립트. Unity
    /// CLI(-executeMethod)로 실행하고 Console 로그의 PASS/FAIL만 확인하면 된다 — 클릭으로 배치하는 실제
    /// 상호작용 UI는 여기서 다루지 않는다(에디터를 열고 Play로 직접 확인해야 함, docs/UnitCsvSandbox.md 참고).
    /// 사용법: unity -batchmode -projectPath . -executeMethod TacticsECS.EditorTools.UnitCsvVerification.Run -quit
    /// </summary>
    public static class UnitCsvVerification
    {
        private const string SampleCsvRelativePath = "docs/sample_units.csv";
        private const string SandboxCsvRelativePath = "SandboxUnits.csv";

        public static void Run()
        {
            bool ok = VerifyRoundTrip(SampleCsvRelativePath) &
                      VerifySpawnFromCsv(SampleCsvRelativePath) &
                      VerifyRoundTrip(SandboxCsvRelativePath) &
                      VerifySpawnFromCsv(SandboxCsvRelativePath);
            Debug.Log(ok ? "[UnitCsvVerification] ALL PASS" : "[UnitCsvVerification] SOME CHECKS FAILED - see errors above");
        }

        private static string ReadCsv(string relativePath) => File.ReadAllText(Path.Combine(Application.dataPath, "..", relativePath));

        /// <summary>Parse -> Write -> 재파싱해서 값이 그대로 보존되는지 확인한다.</summary>
        private static bool VerifyRoundTrip(string relativePath)
        {
            var absolutePath = Path.Combine(Application.dataPath, "..", relativePath);
            if (!File.Exists(absolutePath))
            {
                Debug.LogError($"[UnitCsvVerification] csv not found: {absolutePath}");
                return false;
            }

            var original = UnitCsvSerializer.Parse(ReadCsv(relativePath));
            var rewritten = UnitCsvSerializer.Parse(UnitCsvSerializer.Write(original));

            if (original.Count == 0)
            {
                Debug.LogError($"[UnitCsvVerification] {relativePath} parsed to 0 rows");
                return false;
            }
            if (original.Count != rewritten.Count)
            {
                Debug.LogError($"[UnitCsvVerification] round-trip row count mismatch for {relativePath}: {original.Count} vs {rewritten.Count}");
                return false;
            }

            bool ok = true;
            for (int i = 0; i < original.Count; i++)
            {
                var a = original[i];
                var b = rewritten[i];
                bool same = a.Id == b.Id && a.Name == b.Name && a.MaxHp == b.MaxHp && a.Defense == b.Defense && a.BaseVisual == b.BaseVisual &&
                    a.Actions == b.Actions && a.Domain == b.Domain && a.MoveRange == b.MoveRange &&
                    a.AttackAttack == b.AttackAttack && a.AttackRange == b.AttackRange &&
                    a.HealAmount == b.HealAmount && a.HealRange == b.HealRange &&
                    a.TransportCapacity == b.TransportCapacity;

                if (!same)
                {
                    Debug.LogError($"[UnitCsvVerification] round-trip mismatch in {relativePath} on row {i} ({a.Name})");
                    ok = false;
                }
            }

            if (ok) Debug.Log($"[UnitCsvVerification] round-trip PASS for {relativePath} ({original.Count} rows)");
            return ok;
        }

        private static bool VerifySpawnFromCsv(string relativePath)
        {
            var rows = UnitCsvSerializer.Parse(ReadCsv(relativePath));
            if (rows.Count == 0)
            {
                Debug.LogError($"[UnitCsvVerification] no rows to verify spawn against in {relativePath}");
                return false;
            }

            var basePrefabs = new Dictionary<string, UnitView>
            {
                ["Melee"] = AssetDatabase.LoadAssetAtPath<UnitView>("Assets/Prefabs/Units/Unit_Melee.prefab"),
                ["Ranged"] = AssetDatabase.LoadAssetAtPath<UnitView>("Assets/Prefabs/Units/Unit_Ranged.prefab"),
                ["Guard"] = AssetDatabase.LoadAssetAtPath<UnitView>("Assets/Prefabs/Units/Unit_Guard.prefab"),
                ["RogueHooded"] = AssetDatabase.LoadAssetAtPath<UnitView>("Assets/Prefabs/Units/Unit_RogueHooded.prefab"),
                ["Mage"] = AssetDatabase.LoadAssetAtPath<UnitView>("Assets/Prefabs/Units/Unit_Mage.prefab"),
                ["SkeletonWarrior"] = AssetDatabase.LoadAssetAtPath<UnitView>("Assets/Prefabs/Units/Unit_SkeletonWarrior.prefab"),
                ["SkeletonMage"] = AssetDatabase.LoadAssetAtPath<UnitView>("Assets/Prefabs/Units/Unit_SkeletonMage.prefab")
            };

            var grid = new GridWorld(8, 8, 1f);
            var world = new EntityWorld();
            var spawnerGo = new GameObject("UnitCsvVerification_Spawner");
            bool ok = true;

            try
            {
                var spawner = spawnerGo.AddComponent<UnitSpawner>();

                for (int i = 0; i < rows.Count; i++)
                {
                    var row = rows[i];
                    if (!basePrefabs.TryGetValue(row.BaseVisual, out var prefab) || prefab == null)
                    {
                        Debug.LogError($"[UnitCsvVerification] base prefab not found for BaseVisual '{row.BaseVisual}' ({row.Name})");
                        ok = false;
                        continue;
                    }

                    // 8x8 그리드 폭을 넘지 않도록 행을 2차원으로 접어 배치한다(CSV 행이 늘어나도 안전).
                    var pos = new Vector2Int(i % grid.Width, i / grid.Width);
                    var view = spawner.SpawnFromCsv(grid, world, Team.Player, prefab, row, pos);

                    if (world.Get<MaxHp>(view.UnitId).Value != row.MaxHp) { Debug.LogError($"[UnitCsvVerification] MaxHp mismatch for {row.Name}"); ok = false; }
                    if (world.Get<Defense>(view.UnitId).Value != row.Defense) { Debug.LogError($"[UnitCsvVerification] Defense mismatch for {row.Name}"); ok = false; }
                    if (world.Get<Attack>(view.UnitId).Value != row.AttackAttack) { Debug.LogError($"[UnitCsvVerification] Attack mismatch for {row.Name}"); ok = false; }
                    // UnitCsvActionFactory.BuildActions는 CSV의 Actions에 Wait가 없어도 항상 WaitAction을
                    // 추가로 넣어주므로(모든 유닛의 기본 보유 행동), 실제 스폰된 유닛의 AvailableActions는
                    // row.Actions에 Wait 비트가 없어도 항상 그 비트가 서 있다 — 기대값 쪽에도 같이 OR해서 비교한다.
                    if (world.Get<AvailableActions>(view.UnitId).Value != (row.Actions | ActionType.Wait)) { Debug.LogError($"[UnitCsvVerification] Actions mismatch for {row.Name}"); ok = false; }
                    if (world.Get<MoveDomain>(view.UnitId).Value != row.Domain) { Debug.LogError($"[UnitCsvVerification] Domain mismatch for {row.Name}"); ok = false; }
                    if (world.Get<CargoCapacity>(view.UnitId).Value != row.TransportCapacity) { Debug.LogError($"[UnitCsvVerification] TransportCapacity mismatch for {row.Name}"); ok = false; }
                    if (grid.GetOccupant(pos) != view.UnitId) { Debug.LogError($"[UnitCsvVerification] grid occupant mismatch for {row.Name}"); ok = false; }
                }
            }
            finally
            {
                Object.DestroyImmediate(spawnerGo);
            }

            if (ok) Debug.Log($"[UnitCsvVerification] spawn PASS ({rows.Count} rows)");
            return ok;
        }
    }
}
