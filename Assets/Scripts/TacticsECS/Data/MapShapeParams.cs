namespace TacticsECS
{
    /// <summary>맵 타입별 랜드마스 마스크 파라미터(값만). 전부 같은 "방사형 감쇠 + 노이즈 점수 -> 순위 컷"
    /// 마스크 하나를 재사용하고, 타입마다 파라미터만 바꾼다. 타입별 값: TerrainGenerationSystem.GetShapeParams.</summary>
    public readonly struct MapShapeParams
    {
        /// <summary>랜드마스 중심점 개수. 0=순수 노이즈(중심 없음, Lakes), 1=맵 정중앙 한 점(Pangea),
        /// 2 이상=쿼드런트로 흩어진 여러 중심점(Continents/Archipelago/Waterworld — 바이옴마다 자기
        /// 땅덩이를 갖는 효과).</summary>
        public readonly int CenterCount;
        /// <summary>방사형 감쇠와 노이즈의 혼합 비율(0~1) — 클수록 중심점 주변에 둥글게 뭉치고,
        /// 작을수록 노이즈가 지배적이라 해안선이 조각조각 들쭉날쭉해진다.</summary>
        public readonly float RadialWeight;
        /// <summary>true면 맨 바깥 테두리 칸에 큰 점수 보너스를 줘서 순위 컷에서 거의 항상 육지로
        /// 살아남게 한다(Lakes의 "가장자리 육지 다리" 규칙).</summary>
        public readonly bool ForceBorderLand;

        public MapShapeParams(int centerCount, float radialWeight, bool forceBorderLand)
        {
            CenterCount = centerCount;
            RadialWeight = radialWeight;
            ForceBorderLand = forceBorderLand;
        }
    }
}
