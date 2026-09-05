using System.Collections;
using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 유닛 하나의 화면 표시 전용 컴포넌트.
    /// 값을 스스로 들고 있지 않고, Init/Refresh(EntityWorld, id)로 받을 때마다 그 엔티티의 컴포넌트를
    /// 그때그때 조회해서 겉모습만 갱신한다 (전투 로직 없음).
    /// 외형(모델/스케일)은 프리팹 자체에, 팀별 색상/텍스처/MaxHp는 같은 GameObject의 UnitDefinition
    /// 컴포넌트에 있으므로 타입별 분기(switch) 없이 GetComponent로 읽어오기만 한다.
    /// 유닛 모델(자식의 KayKit 캐릭터)은 몸통/팔/다리/무기 등 여러 개의 Renderer로 나뉘어 있어,
    /// 팀 색 틴트는 그 전부에 같은 런타임 머티리얼 하나를 공유시켜 적용한다.
    /// 중요: Update()가 없다. 이동할 때만 짧게 코루틴을 돌리므로,
    /// 대기 중인 유닛 100~300개는 프레임당 비용이 사실상 0이다.
    /// </summary>
    public class UnitView : MonoBehaviour
    {
        public int UnitId { get; private set; }

        [SerializeField] private float moveSpeed = 6f;

        private Renderer[] _renderers;
        private Material _material;
        private UnitDefinition _definition;
        private TextMesh _hpText;
        private GridWorld _grid;
        private Coroutine _moveRoutine;

        private static readonly Color GuardingColor = Color.yellow;

        /// <summary>모델 원점(발밑)이 타일 바닥면 위에 오도록 하는 높이 보정.</summary>
        private const float GroundOffset = 0.05f;

        public void Init(EntityWorld world, int id, GridWorld grid)
        {
            UnitId = id;
            _grid = grid;
            _renderers = GetComponentsInChildren<Renderer>();
            _definition = GetComponent<UnitDefinition>();

            _material = RuntimeMaterial.CreateColored(_definition.ColorFor(world.Get<Team>(id)), _definition.BodyTexture);
            foreach (var r in _renderers) r.sharedMaterial = _material;

            var textGo = new GameObject("HP");
            textGo.transform.SetParent(transform, false);
            textGo.transform.localPosition = new Vector3(0, 1.5f, 0);
            textGo.transform.localScale = Vector3.one * 0.3f;
            _hpText = textGo.AddComponent<TextMesh>();
            _hpText.alignment = TextAlignment.Center;
            _hpText.anchor = TextAnchor.MiddleCenter;
            _hpText.fontSize = 48;
            _hpText.color = Color.white;

            transform.position = _grid.GridToWorld(world.Get<GridPosition>(id).Value) + Vector3.up * GroundOffset;
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

            var targetWorldPos = _grid.GridToWorld(world.Get<GridPosition>(id).Value) + Vector3.up * GroundOffset;
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
