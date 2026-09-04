using System.Collections;
using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 유닛 하나의 화면 표시 전용 컴포넌트.
    /// UnitData를 스스로 들고 있지 않고, Refresh(UnitData)로 받아서 겉모습만 갱신한다 (전투 로직 없음).
    /// 중요: Update()가 없다. 이동할 때만 짧게 코루틴을 돌리므로,
    /// 대기 중인 유닛 100~300개는 프레임당 비용이 사실상 0이다.
    /// </summary>
    public class UnitView : MonoBehaviour
    {
        public int UnitId { get; private set; }

        [SerializeField] private float moveSpeed = 6f;

        private Renderer _renderer;
        private Material _material;
        private TextMesh _hpText;
        private GridWorld _grid;
        private Coroutine _moveRoutine;

        private static readonly Color PlayerMelee = new Color(0.2f, 0.5f, 1f);
        private static readonly Color PlayerRanged = new Color(0.2f, 0.8f, 0.5f);
        private static readonly Color PlayerGuard = new Color(0.1f, 0.3f, 0.7f);
        private static readonly Color EnemyMelee = new Color(1f, 0.4f, 0.3f);
        private static readonly Color EnemyRanged = new Color(1f, 0.7f, 0.2f);
        private static readonly Color EnemyGuard = new Color(0.6f, 0.1f, 0.1f);
        private static readonly Color GuardingColor = Color.yellow;

        public void Init(UnitData data, GridWorld grid)
        {
            UnitId = data.Id;
            _grid = grid;
            _renderer = GetComponent<Renderer>();

            transform.localScale = data.Type == UnitType.Guard
                ? new Vector3(0.7f, 0.6f, 0.7f)
                : new Vector3(0.5f, 0.5f, 0.5f);

            _material = RuntimeMaterial.CreateColored(ColorFor(data));
            _renderer.sharedMaterial = _material;

            var textGo = new GameObject("HP");
            textGo.transform.SetParent(transform, false);
            textGo.transform.localPosition = new Vector3(0, 1.2f, 0);
            textGo.transform.localScale = Vector3.one * 0.3f;
            _hpText = textGo.AddComponent<TextMesh>();
            _hpText.alignment = TextAlignment.Center;
            _hpText.anchor = TextAnchor.MiddleCenter;
            _hpText.fontSize = 48;
            _hpText.color = Color.white;

            transform.position = _grid.GridToWorld(data.GridPos) + Vector3.up * 0.5f;
            Refresh(data);
        }

        private static Color ColorFor(UnitData d)
        {
            if (d.Team == Team.Player)
                return d.Type switch
                {
                    UnitType.Melee => PlayerMelee,
                    UnitType.Ranged => PlayerRanged,
                    _ => PlayerGuard
                };

            return d.Type switch
            {
                UnitType.Melee => EnemyMelee,
                UnitType.Ranged => EnemyRanged,
                _ => EnemyGuard
            };
        }

        public void Refresh(UnitData data)
        {
            if (!data.IsAlive)
            {
                gameObject.SetActive(false);
                return;
            }

            _hpText.text = $"{data.Hp}/{data.MaxHp}";
            RuntimeMaterial.SetColor(_material, data.IsGuarding ? GuardingColor : ColorFor(data));

            var targetWorldPos = _grid.GridToWorld(data.GridPos) + Vector3.up * 0.5f;
            if ((targetWorldPos - transform.position).sqrMagnitude > 0.0001f)
            {
                if (_moveRoutine != null) StopCoroutine(_moveRoutine);
                _moveRoutine = StartCoroutine(MoveRoutine(targetWorldPos));
            }
        }

        private IEnumerator MoveRoutine(Vector3 worldPos)
        {
            while ((transform.position - worldPos).sqrMagnitude > 0.0001f)
            {
                transform.position = Vector3.MoveTowards(transform.position, worldPos, moveSpeed * Time.deltaTime);
                yield return null;
            }
            transform.position = worldPos;
            _moveRoutine = null;
        }
    }
}
