using UnityEngine;

namespace TacticsECS.EditorTools
{
    /// <summary>
    /// 헤드리스 검증 스크립트를 한 번의 Unity 실행으로 모두 돌리는 진입점(에디터 기동 비용을 한 번만 낸다). 각 스크립트는 예전처럼
    /// "[이름] ALL PASS" / "SOME CHECKS FAILED"를 로그에 남기므로 로그 파일에서 그 줄만 보면 된다. 한 스크립트가 예외를 던져도
    /// 나머지는 계속 돈다. 시간이 오래 걸리는 EconomySimulation은 빼 두었다(따로 실행).
    /// 사용법: unity run . -- -nographics -logFile Logs/verify_all.log -executeMethod TacticsECS.EditorTools.VerificationSuite.Run
    /// </summary>
    public static class VerificationSuite
    {
        public static void Run()
        {
            Step("GameDataCsvVerification", GameDataCsvVerification.Run);
            Step("EconomyVerification", EconomyVerification.Run);
            Step("BuildingFeatureVerification", BuildingFeatureVerification.Run);
            Step("UnitCsvVerification", UnitCsvVerification.Run);
            Step("TerrainGenerationVerification", TerrainGenerationVerification.Run);
            Step("StructureGenerationVerification", StructureGenerationVerification.Run);
            Step("UIVerification", UIVerification.Run);
            Step("ArrayTableVerification", ArrayTableVerification.Run);
            Step("WikiParityVerification", WikiParityVerification.Run);
            Debug.Log("[VerificationSuite] DONE");
        }

        private static void Step(string name, System.Action run)
        {
            try { run(); }
            catch (System.Exception e) { Debug.LogError($"[VerificationSuite] {name} threw: {e}"); }
        }
    }
}
