using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 체스판 같은 타일 그리드의 데이터 저장소.
    /// 타일이 GameObject가 아니라 TileData[] 배열의 한 칸이라는 점이 핵심 (야매 ECS).
    /// Width*Height가 커져도 순회/조회 비용은 배열 인덱싱 수준으로 저렴하다.
    /// 조회·수정 로직은 Systems/GridQueries(확장 메서드)에 있다.
    /// </summary>
    public class GridWorld
    {
        public readonly int Width;
        public readonly int Height;
        public readonly float TileSize;
        public readonly Vector3 Origin;

        /// <summary>시야(구름) 규칙을 켤지. 경제가 켜진 전투(샌드박스)에서만 true — false면 모든 칸이 모든 팀에게
        /// 탐험된 것으로 취급된다(TileData.ExploredMask 무시, VisionSystem.IsExplored 참고).</summary>
        public bool FogEnabled;
        public EconomyWorld Economy;

        /// <summary>칸 데이터. 인덱스 = y * Width + x (GridQueries.Index).</summary>
        public readonly TileData[] Tiles;

        public static readonly Vector2Int[] Dir4 =
        {
            new Vector2Int(1, 0), new Vector2Int(-1, 0),
            new Vector2Int(0, 1), new Vector2Int(0, -1)
        };

        public static readonly Vector2Int[] Dir8 =
        {
            new Vector2Int(1, 0), new Vector2Int(-1, 0),
            new Vector2Int(0, 1), new Vector2Int(0, -1),
            new Vector2Int(1, 1), new Vector2Int(1, -1),
            new Vector2Int(-1, 1), new Vector2Int(-1, -1)
        };

        public GridWorld(int width, int height, float tileSize = 1f, Vector3 origin = default)
        {
            Width = width;
            Height = height;
            TileSize = tileSize;
            Origin = origin;

            Tiles = new TileData[width * height];
            for (int i = 0; i < Tiles.Length; i++)
                Tiles[i] = TileData.Default;
        }
    }
}
