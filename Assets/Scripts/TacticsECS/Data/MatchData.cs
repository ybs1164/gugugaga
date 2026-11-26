namespace TacticsECS
{
    /// <summary>화면 없이 한 판을 만드는 설정(빠른 한 판과 같은 값). 종족·신은 표 Index, -1이면 없음.
    /// MapType은 맵 타입 이름(Drylands·Lakes·Continents·Pangea·Archipelago·Waterworld). 만드는 쪽: MatchSystem.Create.</summary>
    public struct MatchSetup
    {
        public int MapSize;
        public string MapType;
        public float WaterRatio;
        public int Seed;
        public int PlayerTribe;
        public int EnemyTribe;
        public int PlayerGod;
        public int EnemyGod;
    }

    /// <summary>맵 한 장을 만든 결과(MatchSystem.GenerateMap) — 땅 모양, 영역별 수도 자리(영역 i = CitySystem.Teams[i]), 시작 조건이 바꾼 칸 수.</summary>
    public struct GeneratedMap
    {
        public TerrainOutlineData Outline;
        public UnityEngine.Vector2Int[] Anchors;
        public int StartChanges;
    }

    /// <summary>화면 없이 진행하는 한 판의 상태 — 시뮬레이션·학습·검증이 쓴다(MatchSystem).</summary>
    public class MatchState
    {
        public MatchSetup Setup;
        public GridWorld Grid;
        public EntityWorld World;
        public EconomyWorld Econ;
        public TurnState Turn;
        public BattleOutcome Outcome;
    }
}
