using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace TacticsECS.EditorTools
{
    /// <summary>
    /// BattleHud/SandboxHud의 uGUI 전체, 유닛 머리 위 체력 표시(HpDisplay), 공용 EventSystem, 샌드박스
    /// 팔레트 버튼을 코드 생성이 아니라 프리팹으로 만들어주는 1회성 배치 도구. Unity CLI(-executeMethod)로만
    /// 실행한다(에디터 GUI 직접 조작 금지 — CLAUDE.md 규칙 1).
    /// 사용법: unity run . -- -executeMethod TacticsECS.EditorTools.UIPrefabSetup.GenerateAll
    ///
    /// 여기서 만드는 프리팹은 구조(RectTransform 앵커/크기/색, 하이어라키, 정적 텍스트)만 담고 있다.
    /// 아이콘 스프라이트(IconLibrary.Get)나 버튼 클릭 이벤트처럼 "코드로만 가능한" 부분은 여전히
    /// BattleHud/SandboxHud.Init()의 Wire* 메서드가 인스턴스화 직후 채운다 — IconLibrary.Get이 만드는
    /// Sprite는 디스크에 저장된 에셋이 아니라서, 프리팹에 미리 구워두면 참조가 유지되지 않기 때문이다
    /// (BattleHud.CircleSprite/IconLibrary 주석 참고).
    /// </summary>
    public static class UIPrefabSetup
    {
        private const string UiFolderPath = "Assets/Prefabs/UI";
        private const string MaterialsFolderPath = "Assets/Materials";
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string SandboxScenePath = "Assets/Scenes/Sandbox.unity";

        /// <summary>로우폴리풍 캐주얼 UI에 맞춘 한글 지원 폰트(Jua, OFL 라이선스 — Assets/Fonts/LICENSE.txt).
        /// 모든 Text/TextMesh가 이 폰트 하나를 공유한다(LoadUiFont).</summary>
        private const string UiFontPath = "Assets/Fonts/Jua-Regular.ttf";

        private static readonly string[] UnitPrefabPaths =
        {
            "Assets/Prefabs/Units/Unit_Melee.prefab",
            "Assets/Prefabs/Units/Unit_Ranged.prefab",
            "Assets/Prefabs/Units/Unit_Guard.prefab",
            "Assets/Prefabs/Units/Unit_RogueHooded.prefab",
            "Assets/Prefabs/Units/Unit_Mage.prefab",
            "Assets/Prefabs/Units/Unit_SkeletonWarrior.prefab",
            "Assets/Prefabs/Units/Unit_SkeletonMage.prefab",
        };

        private static readonly Color PlayerAccent = new Color(0.30f, 0.55f, 0.95f);
        private static readonly Color PanelBackground = new Color(0.08f, 0.09f, 0.11f, 0.85f);
        private static readonly Color SandboxPanelBackground = new Color(0.08f, 0.09f, 0.11f, 0.92f);
        private static readonly Color ButtonIdle = new Color(0.16f, 0.17f, 0.20f, 0.95f);

        /// <summary>프리팹에 구울 글자 — 번역 표 원문(ko). 실행 중에는 각 HUD가 지금 언어로 다시 채운다.</summary>
        private static string Src(string key) => LocalizationSystem.Source(key);

        public static void GenerateAll()
        {
            GameDataLoader.LoadStrings(new System.Collections.Generic.List<string>());
            // Assets/Fonts에 새로 추가된 폰트 파일을 이번 배치 실행에서 바로 인식하게 한다(폴더 생성보다 먼저).
            AssetDatabase.Refresh();

            EnsureFolder("Assets", "Materials");
            EnsureFolder("Assets", "Prefabs");
            EnsureFolder("Assets/Prefabs", "UI");

            var font = LoadUiFont();
            var hpBarBackground = GenerateHpBarBackgroundMaterial();
            var hpDisplay = GenerateHpDisplay(hpBarBackground, font);
            var damagePopup = GenerateDamagePopup(font);
            var eventSystem = GenerateEventSystem();
            var paletteButton = GeneratePaletteButton(font);
            var battleHud = GenerateBattleHud(eventSystem, font);
            var sandboxHud = GenerateSandboxHud(paletteButton, font);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            PatchUnitPrefabs(hpDisplay, damagePopup);
            DeleteRetiredPrefabs();
            AssignToScenes(battleHud, sandboxHud);

            Debug.Log("[UIPrefabSetup] Done: HpDisplay/DamagePopup/EventSystem/PaletteButton/BattleHud/SandboxHud prefabs created under " + UiFolderPath);
        }

        /// <summary>전투 HUD 프리팹만 다시 만든다(계층은 BattleHud.Init이 코드로 만들므로 루트·참조만) — 다른 프리팹은 건드리지 않는다.
        /// unity run . -- -executeMethod TacticsECS.EditorTools.UIPrefabSetup.GenerateBattleHudOnly</summary>
        public static void GenerateBattleHudOnly()
        {
            GameDataLoader.LoadStrings(new System.Collections.Generic.List<string>());
            var battleHud = GenerateBattleHud(AssetDatabase.LoadAssetAtPath<GameObject>(UiFolderPath + "/EventSystem.prefab"), LoadUiFont());
            AssetDatabase.SaveAssets();
            DeleteRetiredPrefabs();
            AssignToScenes(battleHud, AssetDatabase.LoadAssetAtPath<GameObject>(UiFolderPath + "/SandboxHud.prefab"));
            Debug.Log("[UIPrefabSetup] BattleHud regenerated");
        }

        /// <summary>예전에 따로 만들던 자원 바·기술트리 프리팹 — 지금은 BattleHud·TechTreeHud가 코드로 만든다.</summary>
        private static void DeleteRetiredPrefabs()
        {
            foreach (var name in new[] { "CityResourceBar", "TechTreePanel" })
                AssetDatabase.DeleteAsset($"{UiFolderPath}/{name}.prefab");
        }

        public static void GenerateSandboxOnly()
        {
            GameDataLoader.LoadAll();
            GenerateSandboxHud(AssetDatabase.LoadAssetAtPath<GameObject>(UiFolderPath + "/PaletteButton.prefab"), LoadUiFont());
            AssetDatabase.SaveAssets();
            Debug.Log("[UIPrefabSetup] Sandbox CSV panel generated");
        }

        /// <summary>모든 UI Text/TextMesh가 공유하는 폰트. 텍스처 임포트 설정처럼 에디터를 직접 열어
        /// 건드릴 필요가 없다 — TTF는 Assets에 두기만 하면 Unity가 알아서 Font 에셋으로 가져온다.</summary>
        private static Font LoadUiFont()
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(UiFontPath);
            if (font == null)
                Debug.LogError($"[UIPrefabSetup] UI font not found at {UiFontPath}.");
            return font;
        }

        // ---------- HpDisplay (유닛 머리 위 체력 표시) ----------

        private static Material GenerateHpBarBackgroundMaterial()
        {
            const string path = MaterialsFolderPath + "/HpBarBackground.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var mat = RuntimeMaterial.CreateColored(new Color(0f, 0f, 0f, 0.6f));
            RuntimeMaterial.SetDoubleSided(mat);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        /// <summary>UnitView.BuildHpDisplay가 인스턴스화해서 쓰는 프리팹. 배경 쿼드는 모든 유닛이 같은
        /// 색이라 공유 머티리얼(HpBarBackground.mat)을 바로 굽지만, 채우기 쿼드는 유닛마다 다른 색으로
        /// 매번 새로 만들어야 해서(HpColorScale) 머티리얼을 비워둔다 — UnitView가 인스턴스화 직후 씌운다.</summary>
        private static Transform GenerateHpDisplay(Material barBackgroundMaterial, Font font)
        {
            var root = new GameObject("HpDisplay");

            var bg = new GameObject("Bar_Bg", typeof(SpriteRenderer));
            bg.name = "Bar_Bg";
            bg.transform.SetParent(root.transform, false);
            bg.transform.localPosition = new Vector3(0f, UnitView.HpBarLocalY, 0f);
            bg.transform.localScale = new Vector3(UnitView.HpBarSize.x, UnitView.HpBarSize.y, 1f);
            var solid = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Pixel2D/Generated/Pattern_Solid_0_0_16_16.asset");
            bg.GetComponent<SpriteRenderer>().sprite = solid;
            bg.GetComponent<SpriteRenderer>().color = new Color(.12f,.17f,.23f);
            bg.GetComponent<Renderer>().sharedMaterial = PixelSpriteCatalog.Material;
            bg.GetComponent<Renderer>().sortingOrder = 20;

            var fill = new GameObject("Bar_Fill", typeof(SpriteRenderer));
            fill.name = "Bar_Fill";
            fill.transform.SetParent(root.transform, false);
            fill.transform.localPosition = new Vector3(0f, UnitView.HpBarLocalY, -0.001f);
            fill.transform.localScale = new Vector3(UnitView.HpBarSize.x, UnitView.HpBarSize.y, 1f);
            fill.GetComponent<SpriteRenderer>().sprite = solid;
            fill.GetComponent<Renderer>().sharedMaterial = PixelSpriteCatalog.Material;
            fill.GetComponent<Renderer>().sortingOrder = 21;

            var textGo = new GameObject("Number");
            textGo.transform.SetParent(root.transform, false);
            textGo.transform.localPosition = new Vector3(0f, UnitView.HpNumberLocalY, 0f);
            // 0.3f였던 기존 크기의 절반(사용자 요청: 인게임 체력 라벨을 2배 줄여달라).
            textGo.transform.localScale = Vector3.one * 0.11f;
            var textMesh = textGo.AddComponent<TextMesh>();
            ApplyUiFont(textMesh, font);
            textMesh.alignment = TextAlignment.Center;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.fontSize = 48;
            textMesh.color = Color.white;

            string path = $"{UiFolderPath}/HpDisplay.prefab";
            var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved.transform;
        }

        /// <summary>피해 팝업(DamagePopup) 프리팹. HpDisplay의 "Number"와 같은 방식(TextMesh + 공용 폰트)
        /// 이지만, 유닛의 자식으로 두지 않고 매번 독립된 오브젝트로 인스턴스화된다(UnitView.ShowDamagePopup) —
        /// 그래야 맞은 유닛이 그 자리에서 비활성화돼도 라벨이 끊기지 않는다.</summary>
        private static Transform GenerateDamagePopup(Font font)
        {
            var root = new GameObject("DamagePopup");

            var textMesh = root.AddComponent<TextMesh>();
            ApplyUiFont(textMesh, font);
            textMesh.alignment = TextAlignment.Center;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.fontSize = 64;
            textMesh.color = Color.white;
            // 0.35f였던 기존 크기의 절반(사용자 요청: 인게임 데미지 라벨을 2배 줄여달라).
            root.transform.localScale = Vector3.one * 0.175f;

            root.AddComponent<DamagePopup>();

            string path = $"{UiFolderPath}/DamagePopup.prefab";
            var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved.transform;
        }

        /// <summary>TextMesh.font를 코드로 지정할 때는 UI.Text와 달리 렌더러 머티리얼을 직접 맞춰줘야
        /// 글자가 실제로 그 폰트로 그려진다(안 하면 기본 폰트로 남는다) — HpDisplay/DamagePopup이 공유하는
        /// 작은 헬퍼.</summary>
        private static void ApplyUiFont(TextMesh textMesh, Font font)
        {
            textMesh.font = font;
            textMesh.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }

        /// <summary>이미 만들어진 유닛 프리팹(Unit_*.prefab)에 hpDisplayPrefab 필드만 채워 넣는다.
        /// UnitPrefabSetup/ExtraCharacterPrefabSetup처럼 프리팹 전체를 새로 만들지 않는다 — 그러면
        /// Guard의 MaxHp=9처럼 이후 수동으로 조정해둔 값이 되돌아가 버린다(ExtraCharacterPrefabSetup.cs
        /// 주석 참고). 필드 하나만 반사로 덧붙이고 저장한다.</summary>
        private static void PatchUnitPrefabs(Transform hpDisplayPrefab, Transform damagePopupPrefab)
        {
            foreach (var path in UnitPrefabPaths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    Debug.LogWarning($"[UIPrefabSetup] unit prefab not found, skipped: {path}");
                    continue;
                }
                var view = prefab.GetComponent<UnitView>();
                SetPrivateField(view, "hpDisplayPrefab", hpDisplayPrefab);
                SetPrivateField(view, "damagePopupPrefab", damagePopupPrefab);
                EditorUtility.SetDirty(view);
                PrefabUtility.SavePrefabAsset(prefab);
            }
        }

        // ---------- EventSystem ----------

        private static GameObject GenerateEventSystem()
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();

            string path = $"{UiFolderPath}/EventSystem.prefab";
            var saved = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return saved;
        }

        // ---------- PaletteButton (SandboxHud 팔레트 한 줄) ----------

        private static GameObject GeneratePaletteButton(Font font)
        {
            var rect = CreateRect("PaletteButton", null);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(160f, 30f); // SandboxHud.SetPalette가 행마다 실제 크기로 덮어쓴다.

            var bg = CreatePanelImage(rect, ButtonIdle);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;

            var textRect = CreateRect("Label", rect);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(6f, 2f);
            textRect.offsetMax = new Vector2(-6f, -2f);
            var text = textRect.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = 15;
            text.alignment = TextAnchor.MiddleLeft;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;

            string path = $"{UiFolderPath}/PaletteButton.prefab";
            var saved = PrefabUtility.SaveAsPrefabAsset(rect.gameObject, path);
            Object.DestroyImmediate(rect.gameObject);
            return saved;
        }

        // ---------- BattleHud ----------

        /// <summary>루트(BattleHud 컴포넌트 + EventSystem 프리팹·폰트 참조)만 굽는다. 화면 계층은 BattleHud.Init이 코드로 만든다 —
        /// PC·모바일 공용 배치가 화면 크기에 따라 달라지므로(UiScale).</summary>
        private static GameObject GenerateBattleHud(GameObject eventSystemPrefab, Font font)
        {
            var root = new GameObject("BattleHud");
            var hud = root.AddComponent<BattleHud>();
            SetPrivateField(hud, "eventSystemPrefab", eventSystemPrefab);
            SetPrivateField(hud, "uiFont", font);
            EditorUtility.SetDirty(hud);

            string path = $"{UiFolderPath}/BattleHud.prefab";
            var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved;
        }

        // ---------- SandboxHud ----------

        private const float SandboxPanelWidth = 360f;
        private const float SandboxPaletteHeight = 300f;
        private const float SandboxScrollbarWidth = 6f;

        private static GameObject GenerateSandboxHud(GameObject paletteButtonPrefab, Font font)
        {
            var root = new GameObject("SandboxHud");
            var hud = root.AddComponent<SandboxHud>();

            var canvasRoot = CreateCanvas(root.transform);
            BuildSandboxToolbar(font, canvasRoot);
            BuildSandboxCsvFiles(font, canvasRoot);
            BuildStructurePanel(font, canvasRoot);
            BuildSandboxStartButton(font, canvasRoot);
            // 탭은 마지막 형제 — 펼친 드롭다운 목록이 CSV 목록 등 다른 패널 아래로 깔리지 않게.
            BuildGenerationTab(font, canvasRoot);
            BuildTribeTab(font, canvasRoot);

            SetPrivateField(hud, "paletteButtonPrefab", paletteButtonPrefab);
            EditorUtility.SetDirty(hud);

            string path = $"{UiFolderPath}/SandboxHud.prefab";
            var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved;
        }

        private static void BuildSandboxToolbar(Font font, Transform root)
        {
            var panel = CreateRect("Toolbar", root);
            panel.anchorMin = panel.anchorMax = new Vector2(0f, 1f);
            panel.pivot = new Vector2(0f, 1f);
            panel.anchoredPosition = new Vector2(16f, -16f);
            panel.sizeDelta = new Vector2(SandboxPanelWidth, SandboxToolbarHeight);
            CreatePanelImage(panel, SandboxPanelBackground);

            float halfWidth = (SandboxPanelWidth - 24f) / 2f;
            // 첫 인자는 계층 이름(SandboxHud가 이 이름 + "Button"으로 찾는다 — 바꾸지 않는다), 둘째는 구울 글자.
            CreateLabel(font, panel, "Title", Src("UI.SandboxHud.CsvTitle"), new Vector2(8f, -8f), new Vector2(SandboxPanelWidth - 16f, 20f), 16);
            CreateTextButtonPlaceholder(font, panel, "맵크기", "", new Vector2(8f, -36f), new Vector2(halfWidth, 28f), ButtonIdle);
            CreateTextButtonPlaceholder(font, panel, "습도", "", new Vector2(8f + halfWidth + 8f, -36f), new Vector2(halfWidth, 28f), ButtonIdle);
            CreateTextButtonPlaceholder(font, panel, "기술불러오기", Src("UI.SandboxHud.Reload"), new Vector2(8f, -72f), new Vector2(halfWidth, 28f), ButtonIdle);
            CreateTextButtonPlaceholder(font, panel, "기술내보내기", Src("UI.SandboxHud.ResetTables"), new Vector2(8f + halfWidth + 8f, -72f), new Vector2(halfWidth, 28f), ButtonIdle);
            CreateTextButtonPlaceholder(font, panel, "종족", Src("UI.SandboxHud.TribeTabButton"), new Vector2(8f, -108f), new Vector2(SandboxPanelWidth - 16f, 28f), ButtonIdle);
            CreateTextButtonPlaceholder(font, panel, "지형생성", Src("UI.SandboxHud.GenerateTerrain"), new Vector2(8f, -144f), new Vector2(SandboxPanelWidth - 16f, 28f), ButtonIdle);

            var statusRect = CreateRect("Status", panel);
            statusRect.anchorMin = statusRect.anchorMax = new Vector2(0f, 1f);
            statusRect.pivot = new Vector2(0f, 1f);
            statusRect.anchoredPosition = new Vector2(8f, -180f);
            statusRect.sizeDelta = new Vector2(SandboxPanelWidth - 16f, 42f);
            var status = statusRect.gameObject.AddComponent<Text>();
            status.font = font;
            status.fontSize = 13;
            status.color = new Color(1f, 1f, 1f, 0.75f);
            status.horizontalOverflow = HorizontalWrapMode.Wrap;
        }

        // ---------- SandboxHud: 습도 탭(맵 타입 · 1차 지형) / 종족 탭 ----------

        private const float SandboxToolbarHeight = 226f;
        /// <summary>두 탭은 툴바 오른쪽 같은 자리에 뜨고 한 번에 하나만 열린다(SandboxHud.ToggleTab). 배치 단계에서는 상단 자원 바를 숨기므로 그 자리까지 쓴다.</summary>
        private const float GenerationTabWidth = 300f;
        private static readonly Color DropdownListBackground = new Color(0.12f, 0.13f, 0.16f, 0.98f);
        private static readonly Color DropdownItemBackground = new Color(0.20f, 0.22f, 0.27f, 1f);

        /// <summary>툴바 오른쪽에 붙는 "습도 탭" 패널 — 툴바의 습도 버튼이 열고 닫는다(처음엔 닫힘). 맵 타입 드롭다운, 물 비율 슬라이더,
        /// 1차 지형(아웃라인) 버튼. 드롭다운/슬라이더는 uGUI 표준 구성(DefaultControls)으로 만들고 색/폰트만 입힌다. 옵션 목록은 SandboxHud가 런타임에 채운다.</summary>
        private static void BuildGenerationTab(Font font, Transform root)
        {
            var panel = CreateSandboxTab("GenerationTab", root, 138f);

            CreateLabel(font, panel, "Title", Src("UI.SandboxHud.TabTitle"), new Vector2(8f, -6f), new Vector2(GenerationTabWidth - 16f, 20f), 14);

            float labelW = 76f, controlX = 8f + labelW + 4f, controlW = GenerationTabWidth - controlX - 8f;
            CreateLabel(font, panel, "MapTypeLabel", Src("UI.SandboxHud.MapType"), new Vector2(8f, -34f), new Vector2(labelW, 26f), 14);
            CreateDropdown(font, panel, "MapTypeDropdown", new Vector2(controlX, -34f), new Vector2(controlW, 26f));

            CreateLabel(font, panel, "WaterLabel", Src("UI.SandboxHud.Water"), new Vector2(8f, -68f), new Vector2(labelW, 26f), 14);
            CreateSlider(panel, "WaterSlider", new Vector2(controlX, -73f), new Vector2(controlW - 50f, 16f));
            CreateLabel(font, panel, "WaterValue", "55%", new Vector2(GenerationTabWidth - 52f, -68f), new Vector2(44f, 26f), 14);

            CreateTextButtonPlaceholder(font, panel, "1차지형생성", Src("UI.SandboxHud.GenerateOutline"), new Vector2(8f, -102f), new Vector2(GenerationTabWidth - 16f, 28f), ButtonIdle);
        }

        /// <summary>"종족" 탭 — 툴바의 종족 버튼이 열고 닫는다(처음엔 닫힘). 팀별 종족·신 드롭다운, 종족 요약 텍스트. 영역 바이옴은 종족이 정한다.</summary>
        private static void BuildTribeTab(Font font, Transform root)
        {
            var panel = CreateSandboxTab("TribeTab", root, 252f);
            CreateLabel(font, panel, "Title", Src("UI.SandboxHud.TribeTabTitle"), new Vector2(8f, -6f), new Vector2(GenerationTabWidth - 16f, 20f), 14);

            float labelW = 76f, controlX = 8f + labelW + 4f, controlW = GenerationTabWidth - controlX - 8f;
            CreateLabel(font, panel, "PlayerTribeLabel", Src("UI.SandboxHud.PlayerTribe"), new Vector2(8f, -34f), new Vector2(labelW, 26f), 14);
            CreateDropdown(font, panel, "PlayerTribeDropdown", new Vector2(controlX, -34f), new Vector2(controlW, 26f));
            CreateLabel(font, panel, "EnemyTribeLabel", Src("UI.SandboxHud.EnemyTribe"), new Vector2(8f, -68f), new Vector2(labelW, 26f), 14);
            CreateDropdown(font, panel, "EnemyTribeDropdown", new Vector2(controlX, -68f), new Vector2(controlW, 26f));

            CreateLabel(font, panel, "PlayerGodLabel", Src("UI.SandboxHud.PlayerGod"), new Vector2(8f, -102f), new Vector2(labelW, 26f), 14);
            CreateDropdown(font, panel, "PlayerGodDropdown", new Vector2(controlX, -102f), new Vector2(controlW, 26f));
            CreateLabel(font, panel, "EnemyGodLabel", Src("UI.SandboxHud.EnemyGod"), new Vector2(8f, -136f), new Vector2(labelW, 26f), 14);
            CreateDropdown(font, panel, "EnemyGodDropdown", new Vector2(controlX, -136f), new Vector2(controlW, 26f));

            var info = CreateLabel(font, panel, "TribeInfo", "", new Vector2(8f, -170f), new Vector2(GenerationTabWidth - 16f, 78f), 12);
            info.alignment = TextAnchor.UpperLeft;
            info.horizontalOverflow = HorizontalWrapMode.Wrap;
            info.color = new Color(1f, 1f, 1f, 0.75f);
        }

        /// <summary>툴바 오른쪽 탭 자리의 빈 패널(처음엔 닫힘).</summary>
        private static RectTransform CreateSandboxTab(string name, Transform root, float height)
        {
            var panel = CreateRect(name, root);
            panel.anchorMin = panel.anchorMax = new Vector2(0f, 1f);
            panel.pivot = new Vector2(0f, 1f);
            panel.anchoredPosition = new Vector2(16f + SandboxPanelWidth + 8f, -16f);
            panel.sizeDelta = new Vector2(GenerationTabWidth, height);
            CreatePanelImage(panel, SandboxPanelBackground);
            panel.gameObject.SetActive(false);
            return panel;
        }

        private static Text CreateLabel(Font font, Transform parent, string name, string text, Vector2 anchoredPos, Vector2 size, int fontSize)
        {
            var rect = CreateRect(name, parent);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;
            var label = rect.gameObject.AddComponent<Text>();
            label.font = font;
            label.fontSize = fontSize;
            label.alignment = TextAnchor.MiddleLeft;
            label.color = Color.white;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.text = text;
            return label;
        }

        private static void CreateDropdown(Font font, Transform parent, string name, Vector2 anchoredPos, Vector2 size)
        {
            var go = DefaultControls.CreateDropdown(new DefaultControls.Resources());
            go.name = name;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;

            foreach (var text in go.GetComponentsInChildren<Text>(true))
            {
                text.font = font;
                text.fontSize = 13;
                text.color = Color.white;
                // 기본 캡션 영역(높이 - 13px)은 이 폰트 줄 높이보다 작아, Truncate면 글자가 통째로 안 그려진다.
                text.verticalOverflow = VerticalWrapMode.Overflow;
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
            }
            var caption = (RectTransform)go.transform.Find("Label");
            caption.offsetMin = new Vector2(8f, 0f);
            caption.offsetMax = new Vector2(-20f, 0f);
            go.GetComponent<Image>().color = ButtonIdle;
            var template = go.transform.Find("Template");
            template.GetComponent<Image>().color = DropdownListBackground;
            ((RectTransform)template).sizeDelta = new Vector2(0f, 220f);
            var item = template.Find("Viewport/Content/Item");
            item.Find("Item Background").GetComponent<Image>().color = DropdownItemBackground;
            item.Find("Item Checkmark").GetComponent<Image>().color = PlayerAccent;
            var arrow = go.transform.Find("Arrow");
            arrow.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.6f);
            ((RectTransform)arrow).sizeDelta = new Vector2(8f, 8f);
            var scrollbar = template.Find("Scrollbar");
            if (scrollbar != null) scrollbar.GetComponent<Image>().color = DropdownListBackground;
        }

        private static void CreateSlider(Transform parent, string name, Vector2 anchoredPos, Vector2 size)
        {
            var go = DefaultControls.CreateSlider(new DefaultControls.Resources());
            go.name = name;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;
            go.transform.Find("Background").GetComponent<Image>().color = ButtonIdle;
            go.transform.Find("Fill Area/Fill").GetComponent<Image>().color = new Color(0.36f, 0.76f, 0.90f); // 물 색(TileView Water)
            go.transform.Find("Handle Slide Area/Handle").GetComponent<Image>().color = Color.white;
            var slider = go.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0.55f;
        }

        private static void CreateTextButtonPlaceholder(Font font, Transform parent, string name, string label, Vector2 anchoredPos, Vector2 size, Color bg)
        {
            var rect = CreateRect(name + "Button", parent);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPos;

            var bgImage = CreatePanelImage(rect, bg);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = bgImage;

            var textRect = CreateRect("Label", rect);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(6f, 2f);
            textRect.offsetMax = new Vector2(-6f, -2f);
            var text = textRect.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = 15;
            text.alignment = TextAnchor.MiddleLeft;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.text = label;
        }

        /// <summary>목록이 패널 높이를 넘으면 ScrollRect로 스크롤하는 뼈대. BattleHud의 유닛 로스터
        /// (BuildUnitRoster)도 이 구조를 그대로 재사용한다 — SandboxHud.SetPalette/BattleHud.SetRoster
        /// 둘 다 행 수가 계속 바뀌는 진짜 동적 데이터라 Content에 자기 행을 직접 채워 넣는다.</summary>
        private static void BuildSandboxCsvFiles(Font font, Transform root)
        {
            BuildScrollPanel("CsvFiles", root, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(16f, -16f - SandboxToolbarHeight - 8f), new Vector2(SandboxPanelWidth, 344f), SandboxPanelBackground, SandboxScrollbarWidth);
            var content = (RectTransform)root.Find("CsvFiles/Viewport/Content");
            var paths = GameDataLoader.SandboxCsvPaths;
            content.sizeDelta = new Vector2(0f, paths.Length * 84f + 8f);
            for (int i = 0; i < paths.Length; i++)
            {
                var row = CreateRect(paths[i].Replace('/', '_'), content);
                row.anchorMin = row.anchorMax = row.pivot = new Vector2(0f, 1f);
                row.anchoredPosition = new Vector2(8f, -8f - i * 84f);
                row.sizeDelta = new Vector2(SandboxPanelWidth - 24f, 78f);
                CreateLabel(font, row, "Name", paths[i] + ".csv", Vector2.zero, new Vector2(326f, 20f), 14);
                CreateLabel(font, row, "Path", "", new Vector2(0f, -22f), new Vector2(326f, 22f), 11);
                CreateTextButtonPlaceholder(font, row, "Pick", Src("UI.SandboxHud.SelectCsv"), new Vector2(0f, -46f), new Vector2(158f, 26f), ButtonIdle);
                CreateTextButtonPlaceholder(font, row, "Reset", Src("UI.SandboxHud.Default"), new Vector2(166f, -46f), new Vector2(158f, 26f), ButtonIdle);
            }
        }

        private static void BuildSandboxPalette(Transform root)
        {
            BuildScrollPanel("Palette", root,
                anchor: new Vector2(0f, 1f), pivot: new Vector2(0f, 1f),
                anchoredPos: new Vector2(16f, -264f),
                size: new Vector2(SandboxPanelWidth, SandboxPaletteHeight),
                background: SandboxPanelBackground, scrollbarWidth: SandboxScrollbarWidth);
        }

        /// <summary>세로 스크롤 목록(배경 패널 + Viewport(RectMask2D) + Content + 얇은 Scrollbar) 뼈대를
        /// 만드는 표준 Unity UGUI 구성 — 패널(ScrollRect) &gt; Viewport &gt; Content(행이 실제로 붙는 곳),
        /// 패널 오른쪽 가장자리에 얇은 세로 Scrollbar.</summary>
        private static void BuildScrollPanel(string name, Transform root, Vector2 anchor, Vector2 pivot, Vector2 anchoredPos, Vector2 size, Color background, float scrollbarWidth)
        {
            float scrollbarGutter = scrollbarWidth + 2f;

            var panel = CreateRect(name, root);
            panel.anchorMin = panel.anchorMax = anchor;
            panel.pivot = pivot;
            panel.anchoredPosition = anchoredPos;
            panel.sizeDelta = size;
            CreatePanelImage(panel, background);

            var viewport = CreateRect("Viewport", panel);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = new Vector2(-scrollbarGutter, 0f);
            viewport.gameObject.AddComponent<RectMask2D>();

            var content = CreateRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, size.y);

            var scrollbarTrack = CreateRect("Scrollbar", panel);
            scrollbarTrack.anchorMin = new Vector2(1f, 0f);
            scrollbarTrack.anchorMax = Vector2.one;
            scrollbarTrack.pivot = new Vector2(1f, 0.5f);
            scrollbarTrack.sizeDelta = new Vector2(scrollbarWidth, 0f);
            scrollbarTrack.anchoredPosition = Vector2.zero;
            CreatePanelImage(scrollbarTrack, new Color(1f, 1f, 1f, 0.08f));

            // Scrollbar 컴포넌트는 스크롤 방향 축(세로, BottomToTop이면 anchorMin.y/anchorMax.y)의 크기만
            // 콘텐츠 비율에 맞춰 자동으로 조절한다 — 가로 축은 직접 트랙 전체 폭으로 채워야 하는데, 이걸
            // 빠뜨리면 새 RectTransform 기본값(앵커 (0,0)-(0,0), 크기 0)이 그대로 남아 손잡이 가로폭이
            // 0으로 찌그러진다.
            var handle = CreateRect("Handle", scrollbarTrack);
            handle.anchorMin = Vector2.zero;
            handle.anchorMax = Vector2.one;
            handle.sizeDelta = Vector2.zero;
            handle.anchoredPosition = Vector2.zero;
            var handleImage = CreatePanelImage(handle, new Color(1f, 1f, 1f, 0.35f));

            var scrollbar = scrollbarTrack.gameObject.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.targetGraphic = handleImage;
            scrollbar.handleRect = handle;

            var scrollRect = panel.gameObject.AddComponent<ScrollRect>();
            scrollRect.viewport = viewport;
            scrollRect.content = content;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.verticalScrollbar = scrollbar;
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        }

        /// <summary>SandboxHud의 구조물 정보 패널(이름 + 설명 한두 줄, 전투 시작 전 타일 호버). Toolbar/Palette가 왼쪽 위를
        /// 쓰고 있어 좌하단에 둔다. Description이 한 줄 넘게 줄바꿈될 수 있어 폭을 넉넉히(280) 잡았다.</summary>
        private static void BuildStructurePanel(Font font, Transform root)
        {
            var panel = CreateRect("StructurePanel", root);
            panel.anchorMin = panel.anchorMax = new Vector2(0f, 0f);
            panel.pivot = new Vector2(0f, 0f);
            panel.anchoredPosition = new Vector2(16f, 16f);
            panel.sizeDelta = new Vector2(280f, 96f);
            CreatePanelImage(panel, PanelBackground);

            var accent = CreateRect("Accent", panel);
            accent.anchorMin = new Vector2(0f, 0f);
            accent.anchorMax = new Vector2(0f, 1f);
            accent.pivot = new Vector2(0f, 0.5f);
            accent.sizeDelta = new Vector2(5f, 0f);
            accent.anchoredPosition = Vector2.zero;
            CreatePanelImage(accent, PlayerAccent);

            var name = CreateRect("NameText", panel);
            name.anchorMin = new Vector2(0f, 1f);
            name.anchorMax = new Vector2(1f, 1f);
            name.pivot = new Vector2(0f, 1f);
            name.anchoredPosition = new Vector2(16f, -10f);
            name.sizeDelta = new Vector2(-28f, 28f);
            var nameText = name.gameObject.AddComponent<Text>();
            nameText.font = font;
            nameText.fontSize = 22;
            nameText.color = Color.white;
            nameText.alignment = TextAnchor.UpperLeft;
            nameText.horizontalOverflow = HorizontalWrapMode.Overflow;

            var description = CreateRect("DescriptionText", panel);
            description.anchorMin = new Vector2(0f, 0f);
            description.anchorMax = new Vector2(1f, 1f);
            description.offsetMin = new Vector2(16f, 10f);
            description.offsetMax = new Vector2(-12f, -40f);
            var descriptionText = description.gameObject.AddComponent<Text>();
            descriptionText.font = font;
            descriptionText.fontSize = 16;
            descriptionText.color = new Color(1f, 1f, 1f, 0.85f);
            descriptionText.alignment = TextAnchor.UpperLeft;
            descriptionText.horizontalOverflow = HorizontalWrapMode.Wrap;
            descriptionText.verticalOverflow = VerticalWrapMode.Overflow;
        }

        private static void BuildSandboxStartButton(Font font, Transform root)
        {
            var rect = CreateRect("StartBattleButton", root);
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-16f, 16f);
            rect.sizeDelta = new Vector2(140f, 52f);

            var bg = CreatePanelImage(rect, PlayerAccent);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;

            var textRect = CreateRect("Label", rect);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var text = textRect.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = 18;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = Src("UI.SandboxHud.StartBattle");
        }

        // ---------- 공용 빌딩 블록 ----------

        private static Transform CreateCanvas(Transform root)
        {
            var canvasGo = new GameObject("Canvas");
            canvasGo.transform.SetParent(root, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            return canvasGo.transform;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            if (parent != null) go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static Image CreatePanelImage(RectTransform rect, Color color)
        {
            var img = rect.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        // ---------- 씬 연결 / 공용 유틸 ----------

        private static void AssignToScenes(GameObject battleHudPrefab, GameObject sandboxHudPrefab)
        {
            foreach (var scenePath in new[] { ScenePath, SandboxScenePath, MobileSceneSetup.MobileScenePath })
                if (AssetDatabase.LoadAssetAtPath<Object>(scenePath) != null)
                    AssignToScene(scenePath, battleHudPrefab, sandboxHudPrefab);
        }

        private static void AssignToScene(string scenePath, GameObject battleHudPrefab, GameObject sandboxHudPrefab)
        {
            var scene = EditorSceneManager.OpenScene(scenePath);
            var controller = Object.FindFirstObjectByType<BattleController>(FindObjectsInactive.Include);
            if (controller == null)
            {
                Debug.LogError("[UIPrefabSetup] BattleController not found in " + scenePath);
                return;
            }

            SetPrivateField(controller, "hudPrefab", battleHudPrefab.GetComponent<BattleHud>());
            if (sandboxHudPrefab != null) SetPrivateField(controller, "sandboxHudPrefab", sandboxHudPrefab.GetComponent<SandboxHud>());
            EditorUtility.SetDirty(controller);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void EnsureFolder(string parent, string name)
        {
            string path = $"{parent}/{name}";
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, name);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
                throw new System.Exception($"Field '{fieldName}' not found on {target.GetType().Name}");
            field.SetValue(target, value);
        }
    }
}
