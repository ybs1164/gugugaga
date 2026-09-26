using System.Collections.Generic;
using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 타일 하나의 화면 표시 전용 컴포넌트. 로직/판정은 전혀 하지 않고,
    /// GridPos를 들고 있는 것과 하이라이트 색을 바꾸는 것뿐이라 100개, 1000개가 있어도 비용이 거의 없다.
    /// Update()가 없다 - 클릭 판정은 BattleController가 프레임당 한 번의 레이캐스트로 처리한다.
    /// </summary>
    public class TileView : MonoBehaviour
    {
        public enum TileHighlight { None, Move, Attack }

        public Vector2Int GridPos { get; private set; }
        public TerrainType Terrain { get; private set; }
        public string TileTypeId { get; private set; }

        private Material _material;

        /// <summary>영토 주인 팀 색(투명이면 중립) — 기본 색에 TerritoryTintStrength만큼 옅게 섞는다(진하게 섞으면 파란 팀
        /// 영토의 풀밭이 물처럼 보여서 옅게 두고, 영토 경계는 GridView가 팀 색 테두리 막대로 따로 그린다). 도로가 있으면
        /// 흙길 색을 더 섞는다. 둘 다 GridView.RefreshEconomy가 GridWorld의 OwnerTeam/HasRoad로 채운다.</summary>
        private Color _territoryTint = Color.clear;
        private bool _hasRoad;
        private const float TerritoryTintStrength = 0.15f;
        private static readonly Color RoadColor = new Color(0.55f, 0.42f, 0.28f);

        private static readonly Color ColorLand = new Color(0.75f, 0.75f, 0.75f);
        private static readonly Color ColorWater = new Color(0.25f, 0.45f, 0.85f);
        private static readonly Color ColorMove = new Color(0.3f, 0.65f, 1f);
        private static readonly Color ColorAttack = new Color(1f, 0.35f, 0.3f);

        /// <summary>바이옴 절차 생성(TerrainGenerationSystem)이 채워 넣는 TileTypeId별 기본 색. 여기 없는
        /// 키(빈 문자열 포함)는 기존 Terrain 2색 표시로 폴백한다 — SampleScene처럼 바이옴 생성을 쓰지 않는
        /// 씬은 이 테이블과 무관하게 그대로 동작한다.</summary>
        private static readonly Dictionary<string, Color> TileTypeColors = new Dictionary<string, Color>
        {
            ["Grass"] = new Color(0.42f, 0.68f, 0.35f),
            ["Forest"] = new Color(0.30f, 0.52f, 0.26f),
            ["Mountain"] = new Color(0.50f, 0.47f, 0.42f),
            ["Sand"] = new Color(0.85f, 0.75f, 0.45f),
            ["Rock"] = new Color(0.55f, 0.55f, 0.55f),
            ["Snow"] = new Color(0.92f, 0.94f, 0.96f),
            ["Water"] = ColorWater,
            [TerrainGenerationSystem.OceanTileId] = new Color(0.12f, 0.26f, 0.62f),
        };

        /// <summary>terrain/tileTypeId는 하이라이트가 없을 때(TileHighlight.None) 보여줄 기본 색을
        /// 고른다. tileTypeId가 TileTypeColors에 있으면 그 색을, 없으면(빈 문자열 등) 기존처럼 육지=회색/
        /// 물=파란색(Terrain 기준)을 쓴다 — GridView.Build/RefreshTerrain이 GridWorld.GetTerrain/
        /// GetTileType으로 조회해 넘겨준다.</summary>
        public void Init(Vector2Int gridPos, TerrainType terrain, string tileTypeId = "")
        {
            GridPos = gridPos;
            Terrain = terrain;
            TileTypeId = tileTypeId ?? string.Empty;
            // GetComponentInChildren로 찾는다 — 프리미티브 큐브는 자기 자신에, 임포트된 타일 모델
            // (GridView의 landTilePrefab/waterTilePrefab)은 자식 오브젝트에 Renderer가 있을 수 있다.
            _material = GetComponentInChildren<Renderer>().sharedMaterial;
            SetHighlight(TileHighlight.None);
        }

        public void SetHighlight(TileHighlight h)
        {
            var c = h switch
            {
                TileHighlight.Move => ColorMove,
                TileHighlight.Attack => ColorAttack,
                _ => BaseColor()
            };
            RuntimeMaterial.SetColor(_material, c);
        }

        /// <summary>영토/도로 표시만 바꾸고 하이라이트 없는 기본 색으로 다시 칠한다.</summary>
        public void SetTerritory(Color tint, bool hasRoad)
        {
            _territoryTint = tint;
            _hasRoad = hasRoad;
            SetHighlight(TileHighlight.None);
        }

        private Color BaseColor()
        {
            Color c;
            if (string.IsNullOrEmpty(TileTypeId) || !TileTypeColors.TryGetValue(TileTypeId, out c))
                c = Terrain == TerrainType.Water ? ColorWater : ColorLand;
            if (_hasRoad) c = Color.Lerp(c, RoadColor, 0.5f);
            if (_territoryTint.a > 0f) c = Color.Lerp(c, new Color(_territoryTint.r, _territoryTint.g, _territoryTint.b), TerritoryTintStrength * _territoryTint.a);
            return c;
        }
    }
}
