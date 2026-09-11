using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 유닛 프리팹에 붙는 "타입 정의" 컴포넌트. 근접/원거리/방어 같은 타입 구분은 코드의 enum 분기가
    /// 아니라, 이 컴포넌트를 가진 서로 다른 프리팹(Assets/Prefabs/Units)으로 관리한다.
    ///
    /// 이 유닛이 쓸 수 있는 행동은 더 이상 플래그 하나로 직접 지정하지 않는다 — IUnitAction을 구현한
    /// 개별 스크립트(MoveAction/AttackAction/DefendAction/HealAction/SelfDestructAction/CounterAction,
    /// View/Actions 참고)들의 "집합"인 actions 리스트로 정의한다. 리스트에 들어있는 행동만 이 유닛이
    /// 쓸 수 있고, 그 행동에만 필요한 값(사거리, 회복량 등)도 각 항목 자신이 들고 있다 — 값이 속성 하나당
    /// 필드 하나로 흩어져 있던 예전 구조 대신, 값이 그 값을 쓰는 행동 옆에 함께 있다.
    /// AvailableActions는 이 리스트를 훑어 만든 ActionType 비트마스크이고, UnitSpawner가 스폰 시 이
    /// 값들을 EntityWorld의 각 컴포넌트(Core/UnitComponents.cs)로 그대로 옮겨 담는다 — EntityWorld는
    /// 스폰 이후 이 컴포넌트를 다시 참조하지 않는다(Systems는 View/MonoBehaviour를 몰라야 하므로).
    /// </summary>
    public class UnitDefinition : MonoBehaviour
    {
        [Header("Vitals")]
        [SerializeField] private int maxHp;

        [Header("Combat")]
        [Tooltip("공격을 받을 때 항상 적용되는 기본 방어력. Defend 행동(방어 태세)의 추가 보너스와는 별개로, " +
            "actions에 Defend가 없는 유닛도 이 값은 그대로 적용된다.")]
        [SerializeField] private int defense;

        [Header("Actions")]
        [Tooltip("이 유닛 타입이 실제로 쓸 수 있는 행동의 집합. 리스트에 들어있는 항목만 사용 가능하며, " +
            "그 행동에 필요한 값도 항목 자신이 들고 있다. Move/Attack/Defend/Heal/SelfDestruct는 BattleHud " +
            "행동 버튼으로, Counter(반격)는 클릭 버튼이 아니라 자동 발동 패시브 배지로 나타난다.")]
        [SerializeReference] private List<IUnitAction> actions = new List<IUnitAction>
        {
            new MoveAction(),
            new AttackAction(),
        };

        [Header("Appearance")]
        [SerializeField] private Color playerColor = Color.white;
        [SerializeField] private Color enemyColor = Color.white;
        [Tooltip("유닛 모델의 겉감 텍스처. UnitView가 팀 색과 함께 런타임 머티리얼에 입힌다.")]
        [SerializeField] private Texture2D bodyTexture;

        public int MaxHp => maxHp;
        public int Defense => defense;

        /// <summary>이 유닛이 가진 행동들 그대로(읽기 전용). 개별 값이 아니라 행동 자체가 필요한
        /// 곳(예: 커스텀 에디터)에서 쓴다.</summary>
        public IReadOnlyList<IUnitAction> Actions => actions;

        /// <summary>actions 리스트를 ActionType 비트마스크로 합친 값. Systems와 View(BattleHud)는
        /// 여전히 이 값 하나만 보고 "이 유닛이 어떤 행동을 쓸 수 있는가"를 판단한다.</summary>
        public ActionType AvailableActions
        {
            get
            {
                var result = ActionType.None;
                foreach (var action in actions)
                    if (action != null) result |= action.Type;
                return result;
            }
        }

        private T FindAction<T>() where T : class, IUnitAction => actions.OfType<T>().FirstOrDefault();

        public int MoveRange => FindAction<MoveAction>()?.MoveRange ?? 0;
        public bool IgnoreTerrain => FindAction<MoveAction>()?.IgnoreTerrain ?? false;
        public bool IgnoreUnitBlocking => FindAction<MoveAction>()?.IgnoreUnitBlocking ?? false;
        public bool AllowDiagonal => FindAction<MoveAction>()?.AllowDiagonal ?? false;

        public int Attack => FindAction<AttackAction>()?.Attack ?? 0;
        public int AttackRange => FindAction<AttackAction>()?.AttackRange ?? 0;

        public int HealAmount => FindAction<HealAction>()?.HealAmount ?? 0;
        public int HealRange => FindAction<HealAction>()?.HealRange ?? 0;

        public Color ColorFor(Team team) => team == Team.Player ? playerColor : enemyColor;
        public Texture2D BodyTexture => bodyTexture;
    }
}
