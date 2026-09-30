using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 1차 지형(아웃라인) — 바이옴을 채우기 전에 확정되는 육지/물 모양과 수도·사전 마을 위치. 샌드박스 "습도 탭"에서 물 비율을 조절해
    /// 먼저 만들고(TerrainGenerationSystem.PlanOutline), 같은 아웃라인 위에 바이옴을 바꿔 가며 여러 번 채울 수 있다
    /// (TerrainGenerationSystem.GenerateFromOutline). 값만 있다.
    /// </summary>
    public struct TerrainOutlineData
    {
        public int Seed;
        public int Width;
        public int Height;
        public TerrainGenerationSystem.MapShapeMode ShapeMode;

        /// <summary>목표 물 비율(0~1) — 습도 탭 슬라이더 값.</summary>
        public float WaterFraction;

        /// <summary>grid.Index 순서, true = 육지.</summary>
        public bool[] LandMask;

        /// <summary>수도 위치(영역 순서 — 영역 i의 바이옴이 i번째 수도 주변을 채운다).</summary>
        public Vector2Int[] Anchors;
        public Vector2Int[] Suburbs;
        public Vector2Int[] PlannedVillages;
    }
}
