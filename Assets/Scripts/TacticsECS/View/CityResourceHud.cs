using UnityEngine;
using UnityEngine.UI;

namespace TacticsECS
{
    /// <summary>
    /// 팀 자원(별/유닛 수용량) 표시 전용 HUD. BattleHud와 완전히 독립된 별도 프리팹
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

        private Text _populationText;
        private Text _starsText;
        private Text _scoreText;

        public void Init()
        {
            var canvas = transform.Find("Canvas");
            if (canvas == null)
            {
                Debug.LogError($"[CityResourceHud] {name}: Canvas 자식이 없습니다. Assets/Prefabs/UI/CityResourceBar.prefab을 통해 인스턴스화하세요.");
                return;
            }

            var bar = canvas.Find("Bar");
            _populationText = WirePopulationSlot(bar.Find("Population"));
            _starsText = bar.Find("Stars/Number").GetComponent<Text>();
            _scoreText = CreateScoreLine(canvas, bar.GetComponent<RectTransform>());
            PixelUISkin.Apply(gameObject);
        }

        /// <summary>자원 바 바로 아래 한 줄짜리 점수 표시(위키 Score). 줄 하나라 프리팹에 굽지 않고 코드로 만든다(BattleHud의
        /// 로스터/로그 행과 같은 방식).</summary>
        private Text CreateScoreLine(Transform canvas, RectTransform bar)
        {
            var go = new GameObject("Score", typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, bar.anchoredPosition.y - bar.sizeDelta.y - 4f);
            rt.sizeDelta = new Vector2(bar.sizeDelta.x, 26f);
            var text = go.AddComponent<Text>();
            text.font = uiFont;
            text.fontSize = 18;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.75f);
            return text;
        }

        /// <summary>점수 줄(예: "점수 1234 · 적 1180"). 빈 문자열이면 숨긴다.</summary>
        public void SetScoreLine(string line)
        {
            if (_scoreText == null) return;
            _scoreText.text = line ?? string.Empty;
            _scoreText.gameObject.SetActive(!string.IsNullOrEmpty(line));
        }

        private static Text WirePopulationSlot(Transform slot)
        {
            var icon = slot.Find("Icon").GetComponent<Image>();
            icon.sprite = IconLibrary.Get("herd");
            icon.color = new Color(0.30f, 0.55f, 0.95f);
            return slot.Find("Number").GetComponent<Text>();
        }

        /// <summary>populationUsed는 EntityWorld 쪽 값(CityResourceSystem.CountPopulation)이라 City
        /// 데이터 자체에는 들어있지 않다 — BattleHud.ShowUnitPanel이 world+id를 직접 조회해 보여주는
        /// 것과 같은 방식으로, 호출자(BattleController)가 계산해서 넘긴다.</summary>
        public void SetResources(CityResourceData city, int populationUsed)
        {
            // 별는 "보유량 (+턴당 생산량)" — 생산량은 도시 목록으로 매번 다시 계산되는 값이라
            // (CityResourceSystem.RefreshProduction) 옆에 같이 보여줘야 건설/점령 효과가 바로 읽힌다.
            _populationText.text = $"{populationUsed}/{city.PopulationCap}";
            _starsText.text = city.StarsProduction > 0 ? $"{city.Stars} (+{city.StarsProduction})" : city.Stars.ToString();
        }
    }
}
