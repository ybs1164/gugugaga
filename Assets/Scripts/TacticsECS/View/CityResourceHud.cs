using UnityEngine;
using UnityEngine.UI;

namespace TacticsECS
{
    /// <summary>
    /// 도시 발전 자원(발전도/인구/골드/신앙) 표시 전용 HUD. BattleHud와 완전히 독립된 별도 프리팹
    /// (Assets/Prefabs/UI/CityResourceBar.prefab, UIPrefabSetup.GenerateCityResourceBar 참고, Unity CLI
    /// -executeMethod로만 생성)이라 필요한 화면에만 골라서 배치할 수 있다 — 지금은 BattleController의
    /// cityResourceHudPrefab 필드를 통해 샌드박스 커스텀 화면(Sandbox.unity)에만 연결돼 있다.
    ///
    /// BattleHud와 마찬가지로 값을 스스로 판단하지 않고, 넘겨받은 CityResourceData를 그대로 그리기만
    /// 하는 View다. Init()은 프리팹 안의 자식을 이름으로 찾아(Wire) 참조를 캐싱한다.
    /// </summary>
    public class CityResourceHud : MonoBehaviour
    {
        [Tooltip("유닛 로스터/행동 로그와 같은 공용 한글 폰트. Assets/Fonts/Jua-Regular.ttf. UIPrefabSetup.GenerateAll이 채운다.")]
        [SerializeField] private Font uiFont;

        private Text _developmentText;
        private Text _populationText;
        private Text _goldText;
        private Text _faithText;

        // 발전도/인구/골드/신앙 전용 아이콘 아트는 아직 없어(Assets/Art/GameIcons/LICENSE.txt에 그 4종이
        // 없음), 기존 IconLibrary 세트 중 의미가 가장 비슷한 아이콘을 대신 가져다 쓴다 — combo(상승하는
        // 화살표 3개 = 성장/발전), herd(겹친 원 3개 = 무리/보유 수), victory(트로피 = 재화/보상),
        // splash(별 모양 광원 = 신앙/기운). 각 자원 색으로 틴트(Image.color)해서 서로 구분한다. 전용
        // 아이콘이 추가되면 이 표만 교체하면 된다.
        private static readonly (string Icon, Color Tint)[] SlotDefs =
        {
            ("combo", new Color(0.75f, 0.75f, 0.80f)),
            ("herd", new Color(0.30f, 0.55f, 0.95f)),
            ("victory", new Color(0.95f, 0.80f, 0.25f)),
            ("splash", new Color(0.65f, 0.35f, 0.85f)),
        };

        public void Init()
        {
            var canvas = transform.Find("Canvas");
            if (canvas == null)
            {
                Debug.LogError($"[CityResourceHud] {name}: Canvas 자식이 없습니다. Assets/Prefabs/UI/CityResourceBar.prefab을 통해 인스턴스화하세요.");
                return;
            }

            var bar = canvas.Find("Bar");
            _developmentText = WireSlot(bar.Find("Development"), SlotDefs[0]);
            _populationText = WireSlot(bar.Find("Population"), SlotDefs[1]);
            _goldText = WireSlot(bar.Find("Gold"), SlotDefs[2]);
            _faithText = WireSlot(bar.Find("Faith"), SlotDefs[3]);
        }

        private static Text WireSlot(Transform slot, (string Icon, Color Tint) def)
        {
            var icon = slot.Find("Icon").GetComponent<Image>();
            icon.sprite = IconLibrary.Get(def.Icon);
            icon.color = def.Tint;
            return slot.Find("Number").GetComponent<Text>();
        }

        /// <summary>populationUsed는 EntityWorld 쪽 값(CityResourceSystem.CountPopulation)이라 City
        /// 데이터 자체에는 들어있지 않다 — BattleHud.ShowUnitPanel이 world+id를 직접 조회해 보여주는
        /// 것과 같은 방식으로, 호출자(BattleController)가 계산해서 넘긴다.</summary>
        public void SetResources(CityResourceData city, int populationUsed)
        {
            _developmentText.text = city.Development.ToString();
            _populationText.text = $"{populationUsed}/{city.PopulationCap}";
            _goldText.text = city.Gold.ToString();
            _faithText.text = $"{city.Faith}/{city.MaxFaith}";
        }
    }
}
