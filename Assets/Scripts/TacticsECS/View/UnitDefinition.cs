using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 유닛 프리팹(Assets/Prefabs/Units, Units.csv의 BaseVisual)에 붙는 컴포넌트. 프리팹에는 외형(modelId)만 저장한다 —
    /// 스탯·행동·지형(maxHp/defense/actions/domain)은 직렬화하지 않고, 스폰 때 UnitSpawner가 인스턴스에
    /// ApplyCsvOverrides로 Units.csv 행 값을 채운다(수치는 CSV에만 — docs/spec/README.md).
    /// AvailableActions는 이 리스트를 ActionType 비트마스크로 합친 값인데, 지금은 실행 판정에 쓰이지
    /// 않는 플레이스홀더다 — CSV 내보내기/불러오기 같은 외부 데이터 연동과 View(BattleHud 아이콘 매칭)
    /// 표시 용도로만 남아있다(Core/ActionType.cs 참고).
    /// </summary>
    public class UnitDefinition : MonoBehaviour
    {
        // 스폰 때 ApplyCsvOverrides가 채우는 런타임 값 — 직렬화하지 않는다.
        private int maxHp;
        private float defense;
        private List<IUnitAction> actions = new List<IUnitAction>();
        private TerrainType domain = TerrainType.Land;

        [Header("Appearance")]
        [Tooltip("유닛 모델의 겉감 텍스처. 표시 색(팀별 고정색)은 UnitDefinition이 아니라 UnitView가 " +
            "Team 값으로 정한다 — 유닛 타입마다 다른 색을 가질 수 없다.")]
        [SerializeField] private Texture2D bodyTexture;

        [Tooltip("Pixel2D/SpriteCatalog.csv의 표시 키(예: Unit.infantry). 등록된 CSV ID는 전용 픽셀 스프라이트를 사용하고, " +
            "사용자 정의 ID는 선택한 BaseVisual 프리팹의 픽셀 외형을 사용한다.")]
        [SerializeField] private string modelId;

        public int MaxHp => maxHp;
        public float Defense => defense;

        /// <summary>이 유닛이 가진 행동들 그대로(읽기 전용). 개별 값이 아니라 행동 자체가 필요한
        /// 곳(예: 커스텀 에디터)에서 쓴다.</summary>
        public IReadOnlyList<IUnitAction> Actions => actions;

        /// <summary>actions 리스트를 ActionType 비트마스크로 합친 값. 실행 판정에는 쓰이지 않는
        /// 플레이스홀더 — CSV 연동/BattleHud 아이콘 표시 용도로만 쓰인다.</summary>
        public ActionType AvailableActions
        {
            get
            {
                var result = ActionType.None;
                foreach (var action in actions)
                    if (action != null) result |= action.GetActionType();
                return result;
            }
        }

        private T FindAction<T>() where T : class, IUnitAction => actions.OfType<T>().FirstOrDefault();

        public int MoveRange => FindAction<MoveAction>()?.MoveRange ?? 0;

        public float Attack => FindAction<AttackAction>()?.Attack ?? 0f;
        public int AttackRange => FindAction<AttackAction>()?.AttackRange ?? 0;

        public int HealAmount => FindAction<HealAction>()?.HealAmount ?? 0;
        public int HealRange => FindAction<HealAction>()?.HealRange ?? 0;

        public TerrainType Domain => domain;

        public Texture2D BodyTexture => bodyTexture;

        public string ModelId => modelId;

        /// <summary>CSV 행(UnitCsvRow) 값으로 이 컴포넌트의 필드를 전부 덮어쓴다. 외형(bodyTexture)은
        /// 건드리지 않는다 — CSV 유닛은 BaseVisual 프리팹의 모델/텍스처를 그대로 빌려 쓰고 스탯/행동만
        /// 갈아끼운다(색은 UnitDefinition이 아니라 UnitView가 Team으로 정하므로 여기서 다룰 값이 아니다).
        /// 반드시 프리팹 에셋이 아니라 Instantiate로 만든 인스턴스에만 호출해야 원본 프리팹이
        /// 오염되지 않는다(UnitSpawner.SpawnFromCsv 참고).</summary>
        public void ApplyCsvOverrides(UnitCsvRow row, List<IUnitAction> csvActions)
        {
            maxHp = row.MaxHp;
            defense = row.Defense;
            actions = csvActions ?? new List<IUnitAction>();
            if (!actions.Any(a => a is WaitAction))
                actions.Add(new WaitAction());
            domain = row.Domain;
            if (PixelSpriteCatalog.Has("Unit." + row.Id)) modelId = "Unit." + row.Id;
        }
    }
}
