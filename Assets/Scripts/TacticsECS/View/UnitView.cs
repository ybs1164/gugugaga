using System.Collections;
using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 유닛 하나의 화면 표시 전용 컴포넌트.
    /// 값을 스스로 들고 있지 않고, Init/Refresh(EntityWorld, id)로 받을 때마다 그 엔티티의 컴포넌트를
    /// 그때그때 조회해서 겉모습만 갱신한다 (전투 로직 없음).
    /// 외형(스케일)은 프리팹 자체에, 팀별 색상/MaxHp는 같은 GameObject의 UnitDefinition 컴포넌트에 있으므로
    /// 타입별 분기(switch) 없이 GetComponent로 읽어오기만 한다.
    /// 중요: Update()가 없다. 이동할 때만 짧게 코루틴을 돌리므로,
    /// 대기 중인 유닛 100~300개는 프레임당 비용이 사실상 0이다.
    /// </summary>
    public class UnitView : MonoBehaviour
    {
        public int UnitId { get; private set; }

        [SerializeField] private float moveSpeed = 6f;

        private Renderer _renderer;
        private Material _material;
        private UnitDefinition _definition;
        private TextMesh _hpText;
        private GridWorld _grid;
        private Coroutine _moveRoutine;

        private static readonly Color GuardingColor = Color.yellow;

        public void Init(EntityWorld world, int id, GridWorld grid)
        {
            UnitId = id;
            _grid = grid;
            _renderer = GetComponent<Renderer>();
            _definition = GetComponent<UnitDefinition>();

            _material = RuntimeMaterial.CreateColored(_definition.ColorFor(world.Get<Team>(id)));
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

            transform.position = _grid.GridToWorld(world.Get<GridPosition>(id).Value) + Vector3.up * 0.5f;
            Refresh(world, id);
        }

        public void Refresh(EntityWorld world, int id)
        {
            if (!UnitQueries.IsAlive(world, id))
            {
                gameObject.SetActive(false);
                return;
            }

            _hpText.text = $"{world.Get<Hp>(id).Value}/{_definition.MaxHp}";
            RuntimeMaterial.SetColor(_material, world.Get<IsGuarding>(id).Value ? GuardingColor : _definition.ColorFor(world.Get<Team>(id)));

            var targetWorldPos = _grid.GridToWorld(world.Get<GridPosition>(id).Value) + Vector3.up * 0.5f;
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
