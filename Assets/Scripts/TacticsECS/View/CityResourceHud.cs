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
        private Text _scoreText;

        // 발전도/인구/골드 전용 아이콘(research = 전구, population = 사람, star = 별 — Polytopia 상단 바의 별처럼 골드는 별로
        // 표시한다, docs/UxIconizationPlan.md 2.3). 각 자원 색으로 틴트해서 서로 구분한다.
        // (신앙은 쓰는 곳이 없는 프로젝트 고유 자원이라 2026-09-29에 뺐다 — 위키에도 없음.)
        private static readonly (string Icon, Color Tint)[] SlotDefs =
        {
            ("research", UiKit.ResearchColor),
            ("population", UiKit.PopulationColor),
            ("star", UiKit.GoldColor),
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
            _scoreText = CreateScoreLine(canvas, bar.GetComponent<RectTransform>());
            ResponsiveCanvas.Attach(canvas);
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

            // "점수" 단어 대신 트로피 아이콘(Polytopia 상단 바의 점수 표시).
            var trophy = UiKit.Image(go.transform, "Icon", IconLibrary.Get("victory"), UiKit.GoldColor);
            trophy.rectTransform.anchorMin = trophy.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            trophy.rectTransform.pivot = new Vector2(0f, 0.5f);
            trophy.rectTransform.sizeDelta = new Vector2(20f, 20f);
            trophy.rectTransform.anchoredPosition = new Vector2(bar.sizeDelta.x * 0.5f - 90f, 0f);

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            UiKit.Stretch((RectTransform)textGo.transform);
            var text = textGo.AddComponent<Text>();
            text.font = uiFont;
            text.fontSize = 18;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            text.supportRichText = true;
            var outline = textGo.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.75f);
            return text;
        }

        /// <summary>점수 줄: [트로피] 내 점수 · 적 점수(적 색). hasScore가 false면 숨긴다.</summary>
        public void SetScore(bool hasScore, int mine, int enemy)
        {
            if (_scoreText == null) return;
            _scoreText.text = hasScore ? $"{mine}  <size=14><color=#{ColorUtility.ToHtmlStringRGB(BattleHud.EnemyAccent)}>{enemy}</color></size>" : string.Empty;
            _scoreText.transform.parent.gameObject.SetActive(hasScore);
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
            // 발전도/골드는 "보유량 (+턴당 생산량)" — 생산량은 도시 목록으로 매번 다시 계산되는 값이라
            // (CityResourceSystem.RefreshProduction) 옆에 같이 보여줘야 건설/점령 효과가 바로 읽힌다.
            _developmentText.supportRichText = _goldText.supportRichText = true;
            _developmentText.text = WithIncome(city.Development, city.DevelopmentProduction);
            _populationText.text = $"{populationUsed}/{city.PopulationCap}";
            _goldText.text = WithIncome(city.Gold, city.GoldProduction);
        }

        /// <summary>"12 +3" — 턴당 수입은 괄호 대신 작은 초록 숫자(Polytopia 상단 별 옆 "+5").</summary>
        private static string WithIncome(int value, int income) =>
            income > 0 ? $"{value} <size=13><color=#{ColorUtility.ToHtmlStringRGB(UiKit.GainColor)}>+{income}</color></size>" : value.ToString();
    }
}
