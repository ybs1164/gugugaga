namespace TacticsECS
{
    /// <summary>
    /// 스칼라 게임 규칙 값 — Assets/Resources/GameRules.csv의 "Key,Value" 행이 <c>도메인.이름</c>으로 여기 필드에 대응한다
    /// (예: <c>City.MarketStarsCap</c> -&gt; <see cref="City.MarketStarsCap"/>). GameDataLoader.LoadAll이 CSV 값을 채우고,
    /// 필드 초기값은 CSV를 읽기 전/행이 빠졌을 때 쓰는 위키 기본값이다(행이 빠지면 경고). 순수 데이터(CLAUDE.md 규칙 2).
    ///
    /// 2차 비교분석(docs/history/GameDataCsv.md) 결과 키-값 표를 택했다: 한 줄 = 규칙 하나 + 위키 값(Wiki) + 설명 + 원문과 다른 이유(Note).
    /// 새 규칙은 여기 필드 하나와 CSV 행 하나를 추가하면 된다(로더는 리플렉션으로 필드를 찾으므로 로더 수정 불필요).
    /// </summary>
    public static class GameRules
    {
        public static class Economy
        {
            public static int StartingStars = 5;
        }

        public static class City
        {
            public static int StarsPerLevel = 1;
            public static int CapitalStars = 1;
            public static int DefaultBorderRadius = 1;
            public static int UnitCapacityBase = 1;
            public static int MarketStarsPerLevel = 1;
            public static int MarketStarsCap = 8;
            public static int MaxPortWaterGap = 5;
            public static string SuperUnitId = "giant";
        }

        public static class Heal
        {
            public static int OwnTerritory = 4;
            public static int Other = 2;
        }

        public static class Unit
        {
            public static int DisbandRefundDivisor = 2;
        }

        public static class Ruin
        {
            public static int Stars = 10;
            public static int Population = 3;
            public static string NewFriendsUnitId = "gladiator";
            public static string SeaUnitId = "infantry";
            public static string SeaBoatId = "rammer";
        }

        /// <summary>위키 Cloak "Infiltrate".</summary>
        public static class Infiltration
        {
            public static int MaxDaggers = 5;
            public static string DaggerUnitId = "dagger";
        }

        /// <summary>위키 Units "Veteran Units".</summary>
        public static class Veteran
        {
            public static int KillsRequired = 3;
            public static int MaxHpBonus = 5;
        }

        /// <summary>위키 Starfish: 배가 불가사리 칸에서 턴을 시작하면 인양(턴 소모)해 별을 받는다.</summary>
        public static class Starfish
        {
            public static int Stars = 8;
        }

        public static class Tech
        {
            public static int DefaultCostBase = 4;
            public static int LiteracyDivisor = 3;
        }

        /// <summary>위키 Combat 공식의 상수. 계산은 CombatSystem.Resolve.</summary>
        public static class Combat
        {
            public static float DamageCoefficient = 4.5f;
            public static float DefenseBonusMultiplier = 1.5f;
            public static float WallDefenseMultiplier = 4f;
            public static int SplashDivisor = 2;
            public static bool MeleeAdvanceOnKill = true;
            public static float GuardDefenseBonus = 1f;
        }

        /// <summary>위키 Score 문서. 계산은 ScoreSystem.</summary>
        public static class Score
        {
            public static int PerUnitCostStar = 5;
            public static int SuperUnit = 50;
            public static int PerTerritoryTile = 20;
            public static int PerExploredTile = 5;
            public static int CityBase = 100;
            public static int PerCityLevelAbove1 = 50;
            public static int PerPopulation = 5;
            public static int Park = 250;
            public static int Monument = 400;
            public static int TempleBase = 100;
            public static int PerTempleLevelAbove1 = 100;
            public static int TempleMaxLevel = 5;
            public static int TempleTurnsPerLevel = 3;
            public static int PerTechTier = 100;
        }

        /// <summary>위키 Terrain(Cloud)/Unit Skills(Scout)/Explorer/Lighthouse. 계산은 VisionSystem, 해금 키는 VisionDefinition.</summary>
        public static class Vision
        {
            public static int BaseSightRadius = 1;
            public static int ExtendedSightRadius = 2;
            public static int StartRevealRadius = 2;
            public static int ExplorerMoves = 12;
            public static int ExplorerScanRange = 4;
            public static int LighthousePopulation = 1;
        }

        /// <summary>AI 튜닝 값(위키 무관).</summary>
        public static class AI
        {
            public static int MaxImprovementsPerTurn = 12;
            public static int ThreatRadius = 3;
            public static int MaxConnectionBudget = 18;
            public static int DefendRadius = 2;
        }
    }
}
