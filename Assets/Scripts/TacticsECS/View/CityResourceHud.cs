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

        private static Sprite _developmentIconSprite;
        private static Sprite _populationIconSprite;
        private static Sprite _goldIconSprite;
        private static Sprite _faithIconSprite;

        public void Init()
        {
            var canvas = transform.Find("Canvas");
            if (canvas == null)
            {
                Debug.LogError($"[CityResourceHud] {name}: Canvas 자식이 없습니다. Assets/Prefabs/UI/CityResourceBar.prefab을 통해 인스턴스화하세요.");
                return;
            }

            var bar = canvas.Find("Bar");
            _developmentText = WireSlot(bar.Find("Development"), DevelopmentIconSprite);
            _populationText = WireSlot(bar.Find("Population"), PopulationIconSprite);
            _goldText = WireSlot(bar.Find("Gold"), GoldIconSprite);
            _faithText = WireSlot(bar.Find("Faith"), FaithIconSprite);
        }

        private static Text WireSlot(Transform slot, Sprite iconSprite)
        {
            slot.Find("Icon").GetComponent<Image>().sprite = iconSprite;
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

        // 발전도/인구/골드/신앙 아이콘 아트가 아직 없어(Assets/Art/GameIcons에 없음), BattleHud.CircleSprite
        // 와 같은 이유로 런타임에 색만 다른 동그라미를 그려 임시 아이콘으로 쓴다 — Sprite.Create 결과는
        // 디스크 에셋이 아니라서 프리팹에 미리 구워둘 수 없다. 실제 아이콘이 추가되면 IconLibrary.Get으로
        // 바꾸면 된다.
        private static Sprite DevelopmentIconSprite
        {
            get { return _developmentIconSprite != null ? _developmentIconSprite : (_developmentIconSprite = MakeColoredCircle(new Color(0.55f, 0.55f, 0.60f))); }
        }

        private static Sprite PopulationIconSprite
        {
            get { return _populationIconSprite != null ? _populationIconSprite : (_populationIconSprite = MakeColoredCircle(new Color(0.30f, 0.55f, 0.95f))); }
        }

        private static Sprite GoldIconSprite
        {
            get { return _goldIconSprite != null ? _goldIconSprite : (_goldIconSprite = MakeColoredCircle(new Color(0.95f, 0.80f, 0.25f))); }
        }

        private static Sprite FaithIconSprite
        {
            get { return _faithIconSprite != null ? _faithIconSprite : (_faithIconSprite = MakeColoredCircle(new Color(0.65f, 0.35f, 0.85f))); }
        }

        private static Sprite MakeColoredCircle(Color color)
        {
            const int res = 64;
            var tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
            var center = new Vector2((res - 1) * 0.5f, (res - 1) * 0.5f);
            float radius = res * 0.5f;
            var pixels = new Color32[res * res];
            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    float alpha = Mathf.Clamp01(radius - dist + 0.5f);
                    pixels[y * res + x] = new Color(color.r, color.g, color.b, alpha);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();

            return Sprite.Create(tex, new Rect(0f, 0f, res, res), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
