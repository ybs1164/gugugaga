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

        private Material _material;

        private static readonly Color ColorLand = new Color(0.75f, 0.75f, 0.75f);
        private static readonly Color ColorWater = new Color(0.25f, 0.45f, 0.85f);
        private static readonly Color ColorMove = new Color(0.3f, 0.65f, 1f);
        private static readonly Color ColorAttack = new Color(1f, 0.35f, 0.3f);

        /// <summary>terrain은 하이라이트가 없을 때(TileHighlight.None) 보여줄 기본 색을 고른다 —
        /// 육지는 회색, 물은 파란색(GridView.Build가 GridWorld.GetTerrain으로 조회해 넘겨준다).</summary>
        public void Init(Vector2Int gridPos, TerrainType terrain)
        {
            GridPos = gridPos;
            Terrain = terrain;
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
                _ => Terrain == TerrainType.Water ? ColorWater : ColorLand
            };
            RuntimeMaterial.SetColor(_material, c);
        }
    }
}
