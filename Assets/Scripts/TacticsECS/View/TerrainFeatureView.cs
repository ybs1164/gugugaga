using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 숲/산 타일 위 장식과 구름 덩어리 — 모양은 모델 파츠 CSV(Assets/Resources/Models/TerrainModels.csv의 "Terrain.Forest"/
    /// "Terrain.Mountain"/"Terrain.Cloud")에 있고 ModelBuilder가 조립한다(2차 모델링 비교분석 — docs/ModelingPlan.md). 칸마다 방향을
    /// 달리해(위치 기반이라 다시 만들어도 같음) 같은 모양이 격자처럼 반복돼 보이지 않게 한다. 표시 전용, 콜라이더 없음.
    /// </summary>
    public static class TerrainFeatureView
    {
        public const string CloudModel = "Terrain.Cloud";

        /// <summary>tileTypeId에 맞는 장식을 parent(타일) 아래에 만들어 반환한다. 숲/산이 아니거나 모델이 없으면 null.</summary>
        public static GameObject Create(string tileTypeId, Transform parent, Vector2Int gridPos, float baseHeight)
        {
            if (tileTypeId != TerrainGenerationSystem.ForestTileId && tileTypeId != TerrainGenerationSystem.MountainTileId) return null;
            return Place("Terrain." + tileTypeId, parent, gridPos, baseHeight);
        }

        /// <summary>구름 덩어리(탐험하지 않은 칸).</summary>
        public static GameObject CreateCloud(Transform parent, Vector2Int gridPos, float baseHeight) => Place(CloudModel, parent, gridPos, baseHeight);

        private static GameObject Place(string model, Transform parent, Vector2Int gridPos, float baseHeight)
        {
            var go = ModelBuilder.Build(model, parent, new Vector3(0f, baseHeight, 0f));
            if (go == null) return null;
            go.name = "Feature_" + model;
            go.transform.localRotation = Quaternion.Euler(0f, (gridPos.x * 73 + gridPos.y * 151) % 4 * 90f, 0f);
            return go;
        }
    }
}
