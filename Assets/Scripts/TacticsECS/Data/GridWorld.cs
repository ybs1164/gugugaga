using System.Collections.Generic;
using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 체스판 같은 타일 그리드의 데이터 저장소.
    /// 타일이 GameObject가 아니라 TileData[] 배열의 한 칸이라는 점이 핵심 (야매 ECS).
    /// Width*Height가 커져도 순회/조회 비용은 배열 인덱싱 수준으로 저렴하다.
    /// </summary>
    public class GridWorld
    {
        public readonly int Width;
        public readonly int Height;
        public readonly float TileSize;
        public readonly Vector3 Origin;

        private readonly TileData[] _tiles;

        private static readonly Vector2Int[] Dir4 =
        {
            new Vector2Int(1, 0), new Vector2Int(-1, 0),
            new Vector2Int(0, 1), new Vector2Int(0, -1)
        };

        private static readonly Vector2Int[] Dir8 =
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

            _tiles = new TileData[width * height];
            for (int i = 0; i < _tiles.Length; i++)
                _tiles[i] = TileData.Default;
        }

        public bool InBounds(Vector2Int p) => p.x >= 0 && p.y >= 0 && p.x < Width && p.y < Height;

        public int Index(Vector2Int p) => p.y * Width + p.x;

        public TileData GetTile(Vector2Int p) => _tiles[Index(p)];

        public void SetTile(Vector2Int p, TileData data) => _tiles[Index(p)] = data;

        public Vector3 GridToWorld(Vector2Int p) => Origin + new Vector3(p.x * TileSize, 0f, p.y * TileSize);

        public bool IsWalkable(Vector2Int p) => InBounds(p) && GetTile(p).Walkable;

        public bool IsOccupied(Vector2Int p) => InBounds(p) && GetTile(p).OccupantId != TileData.NoOccupant;

        public int GetOccupant(Vector2Int p) => InBounds(p) ? GetTile(p).OccupantId : TileData.NoOccupant;

        public void PlaceOccupant(Vector2Int p, int unitId)
        {
            var t = GetTile(p);
            t.OccupantId = unitId;
            SetTile(p, t);
        }

        public void RemoveOccupant(Vector2Int p)
        {
            var t = GetTile(p);
            t.OccupantId = TileData.NoOccupant;
            SetTile(p, t);
        }

        /// <summary>allowDiagonal이 true면 8방향, false면 상하좌우 4방향 이웃 타일을 반환한다.</summary>
        public IEnumerable<Vector2Int> GetNeighbors(Vector2Int p, bool allowDiagonal)
        {
            foreach (var d in allowDiagonal ? Dir8 : Dir4)
            {
                var n = p + d;
                if (InBounds(n)) yield return n;
            }
        }
    }
}
