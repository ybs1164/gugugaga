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
    /// 머리 위 체력 표시는 숫자(현재 HP만, 최댓값은 선택 시 BattleHud 패널에서 보여준다) +
    /// 색이 상태를 말해주는 막대(HpColorScale)로 구성해, 값을 굳이 읽지 않아도 색과 길이만으로
    /// 상태가 보이게 한다.
    /// 중요: Update()가 없다. 이동할 때만 짧게 코루틴을 돌리므로,
    /// 대기 중인 유닛 100~300개는 프레임당 비용이 사실상 0이다.
    /// </summary>
    public class UnitView : MonoBehaviour
    {
        public int UnitId { get; private set; }

        [SerializeField] private float moveSpeed = 6f;
        [SerializeField] private float turnSpeedDegrees = 720f;

        [Tooltip("머리 위 체력 표시(배경/채우기 막대 + 숫자) 프리팹. Assets/Prefabs/UI/HpDisplay.prefab " +
            "(UIPrefabSetup.GenerateHpDisplay 참고) — 이 프리팹을 자식으로 인스턴스화해서 쓴다.")]
        [SerializeField] private Transform hpDisplayPrefab;

        private Renderer[] _renderers;
        private Material _material;
        private UnitDefinition _definition;
        private Transform _hpGroup;
        private TextMesh _hpText;
        private Transform _hpBarFill;
        private Material _hpFillMaterial;
        private GridWorld _grid;
        private Coroutine _moveRoutine;

        private static readonly Color GuardingColor = Color.yellow;

        /// <summary>UIPrefabSetup(에디터 전용 HpDisplay 프리팹 생성 도구, Assets/Editor)이 런타임과 같은
        /// 크기/높이 값을 쓰도록 public으로 공개한 값들. 값이 여기와 프리팹 생성 스크립트 두 곳에 따로
        /// 적히지 않게 한다. Editor 폴더는 별도 어셈블리(Assembly-CSharp-Editor)라 internal로는 보이지
        /// 않아 public이 필요하다.</summary>
        public static readonly Vector2 HpBarSize = new Vector2(0.6f, 0.08f);
        // 캐릭터 머리(약 Y 1.5 부근) 위로 확실히 뜨도록 여유를 둔 높이. 너무 낮추면 헬멧 등
        // 모델 자체 지오메트리에 가려 안 보인다(실측: 스크린샷 검증에서 1.35는 가려짐 확인).
        public const float HpBarLocalY = 1.7f;
        public const float HpNumberLocalY = 1.9f;

        /// <summary>모델 원점(발밑)이 타일 바닥면 위에 오도록 하는 높이 보정.</summary>
        private const float GroundOffset = 0.05f;

        /// <summary>
        /// KayKit 모델의 정면은 로컬 -Z를 향한다(UnitPrefabSetup이 자식을 180도 돌려 붙인 것과 같은 보정).
        /// 그래서 "이 방향을 바라보게" 회전시킬 때는 LookRotation 뒤에 이 보정을 한 번 더 곱해야 한다.
        /// </summary>
        private static readonly Quaternion ModelFacingCorrection = Quaternion.Euler(0f, 180f, 0f);

        public void Init(EntityWorld world, int id, GridWorld grid)
        {
            UnitId = id;
            _grid = grid;
            _renderers = GetComponentsInChildren<Renderer>();
            _definition = GetComponent<UnitDefinition>();

            _material = RuntimeMaterial.CreateColored(_definition.ColorFor(world.Get<Team>(id)), _definition.BodyTexture);
            foreach (var r in _renderers) r.sharedMaterial = _material;

            BuildHpDisplay();

            transform.position = _grid.GridToWorld(world.Get<GridPosition>(id).Value) + Vector3.up * GroundOffset;
            Refresh(world, id);
        }

        /// <summary>머리 위 체력 표시(숫자 + 색깔 막대)를 만든다. 구조(배경 쿼드/채우기 쿼드/숫자)는
        /// Assets/Prefabs/UI/HpDisplay.prefab에 이미 만들어져 있다(UIPrefabSetup.GenerateHpDisplay) — 여기서는
        /// 그 프리팹을 인스턴스화하고, 채우기 쿼드는 유닛마다 색이 달라져야 하므로 전용 런타임 머티리얼만
        /// 새로 만들어 씌운다(배경은 모든 유닛이 같은 색이라 프리팹의 공유 머티리얼을 그대로 쓴다).</summary>
        private void BuildHpDisplay()
        {
            if (hpDisplayPrefab == null)
            {
                Debug.LogError($"[UnitView] {name}: hpDisplayPrefab이 비어있습니다. Assets/Prefabs/UI/HpDisplay.prefab을 연결하세요.");
                return;
            }

            _hpGroup = Instantiate(hpDisplayPrefab, transform);
            _hpGroup.name = "HpDisplay";
            _hpGroup.rotation = HpBillboardRotation();

            _hpBarFill = _hpGroup.Find("Bar_Fill");
            _hpFillMaterial = RuntimeMaterial.CreateColored(HpColorScale.ForFraction(1f));
            RuntimeMaterial.SetDoubleSided(_hpFillMaterial);
            _hpBarFill.GetComponent<Renderer>().sharedMaterial = _hpFillMaterial;

            _hpText = _hpGroup.GetComponentInChildren<TextMesh>();
        }

        public void Refresh(EntityWorld world, int id)
        {
            if (!UnitQueries.IsAlive(world, id))
            {
                gameObject.SetActive(false);
                return;
            }

            int hp = world.Get<Hp>(id).Value;
            float hpFraction = _definition.MaxHp > 0 ? (float)hp / _definition.MaxHp : 0f;
            _hpText.text = hp.ToString();
            SetHpBarFraction(hpFraction);
            RuntimeMaterial.SetColor(_hpFillMaterial, HpColorScale.ForFraction(hpFraction));

            RuntimeMaterial.SetColor(_material, world.Get<IsGuarding>(id).Value ? GuardingColor : _definition.ColorFor(world.Get<Team>(id)));

            var targetWorldPos = _grid.GridToWorld(world.Get<GridPosition>(id).Value) + Vector3.up * GroundOffset;
            if ((targetWorldPos - transform.position).sqrMagnitude > 0.0001f)
            {
                if (_moveRoutine != null) StopCoroutine(_moveRoutine);
                _moveRoutine = StartCoroutine(MoveRoutine(targetWorldPos));
            }
        }

        /// <summary>채우기 쿼드는 중심 기준으로 커지므로, 왼쪽 끝이 항상 배경 왼쪽 끝에 붙어있도록
        /// 줄어든 만큼 중심을 왼쪽으로 같이 옮겨준다.</summary>
        private void SetHpBarFraction(float fraction)
        {
            float width = HpBarSize.x * Mathf.Clamp01(fraction);
            _hpBarFill.localScale = new Vector3(width, HpBarSize.y, 1f);
            _hpBarFill.localPosition = new Vector3(-HpBarSize.x / 2f + width / 2f, HpBarLocalY, -0.001f);
        }

        /// <summary>공격처럼 자리는 그대로인 채 특정 지점 쪽을 바로 바라보게 한다.</summary>
        public void FaceTowards(Vector3 worldPosition)
        {
            var rot = FacingRotation(worldPosition - transform.position);
            if (rot.HasValue) transform.rotation = rot.Value;
            _hpGroup.rotation = HpBillboardRotation(); // 몸이 도는 것과 별개로 체력 표시는 항상 카메라를 본다.
        }

        /// <summary>수평(XZ) 방향을 바라보는 회전. 방향이 거의 0이면(제자리) null.</summary>
        private static Quaternion? FacingRotation(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return null;
            return Quaternion.LookRotation(direction.normalized) * ModelFacingCorrection;
        }

        /// <summary>
        /// 체력 표시(HpDisplay)가 항상 카메라를 향하게 하는 회전. 몸통(transform)은 이동/공격 방향으로
        /// 계속 도는데, 체력 막대는 평평한 Quad라서 몸과 같이 돌면 카메라가 옆/뒤에서 보게 되는 순간
        /// 실처럼 가늘어져 안 보이게 된다(스크린샷 검증으로 실제 확인). 값을 캐싱하지 않고 매번
        /// Camera.main에서 새로 구한다 — 이동 애니메이션 중 몇 프레임, 공격 시 1회처럼 호출이 드물어
        /// (유닛이 가만히 있을 땐 전혀 호출되지 않음) 비용은 무시할 만하고, 스폰 시점이 카메라
        /// 배치보다 먼저여도(BattleController.SetupBattle 순서) 값이 굳어버리는 일이 없다.
        /// </summary>
        private static Quaternion HpBillboardRotation()
        {
            var cam = Camera.main;
            // 부호 주의: TextMesh는 로컬 -Z에서 바라볼 때 정방향으로 읽힌다. 카메라의 forward 방향을
            // (부호 반전 없이) 그대로 쓰면 카메라가 그 -Z 쪽에 오게 되어 숫자가 뒤집히지 않는다
            // (스크린샷 검증에서 부호를 반대로 했을 때 숫자가 좌우 반전되는 것을 확인).
            return cam == null ? Quaternion.identity : Quaternion.LookRotation(cam.transform.forward, Vector3.up);
        }

        private IEnumerator MoveRoutine(Vector3 worldPos)
        {
            // 이동 방향을 목적 회전으로 미리 구해두고, 이동하는 동안 위치와 함께 부드럽게 돌아간다.
            var targetRot = FacingRotation(worldPos - transform.position) ?? transform.rotation;

            while ((transform.position - worldPos).sqrMagnitude > 0.0001f)
            {
                transform.position = Vector3.MoveTowards(transform.position, worldPos, moveSpeed * Time.deltaTime);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, turnSpeedDegrees * Time.deltaTime);
                _hpGroup.rotation = HpBillboardRotation();
                yield return null;
            }
            transform.position = worldPos;
            transform.rotation = targetRot;
            _hpGroup.rotation = HpBillboardRotation();
            _moveRoutine = null;
        }
    }
}
