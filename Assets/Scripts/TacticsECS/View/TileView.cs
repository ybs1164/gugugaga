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

        private Material _material;

        private static readonly Color ColorDefault = new Color(0.75f, 0.75f, 0.75f);
        private static readonly Color ColorMove = new Color(0.3f, 0.65f, 1f);
        private static readonly Color ColorAttack = new Color(1f, 0.35f, 0.3f);

        public void Init(Vector2Int gridPos)
        {
            GridPos = gridPos;
            _material = GetComponent<Renderer>().sharedMaterial;
            SetHighlight(TileHighlight.None);
        }

        public void SetHighlight(TileHighlight h)
        {
            var c = h switch
            {
                TileHighlight.Move => ColorMove,
                TileHighlight.Attack => ColorAttack,
                _ => ColorDefault
            };
            RuntimeMaterial.SetColor(_material, c);
        }
    }
}
