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
        private static readonly Color PassiveBadgeBg = new Color(0.42f, 0.24f, 0.55f, 0.95f);

        /// <summary>BattleHud의 패시브 배지 표(아이콘/툴팁). BattleHud.cs의 같은 이름 표와 값이 어긋나면
        /// 배지가 안 만들어지거나 툴팁이 틀어지므로, 패시브를 추가할 땐 두 곳 다 갱신해야 한다.</summary>
        private static readonly (ActionType Flag, string Icon, string Tooltip)[] PassiveDefs =
        {
            (ActionType.Counter, "counter", "반격(패시브): 공격을 받으면 자동으로 공격한 대상에게 피해를 되돌려줍니다."),
            (ActionType.Charge, "charge", "돌격(패시브): 이번 턴 이동한 뒤에도 공격할 수 있습니다."),
            (ActionType.Retreat, "retreat", "대피(패시브): 이번 턴 공격한 뒤에도 이동할 수 있습니다."),
            (ActionType.Ambush, "ambush", "기습(패시브): 공격 시 대상의 반격을 발동시키지 않습니다."),
            (ActionType.Infiltrate, "infiltrate", "잠입(패시브): 적 유닛에 의한 이동 방해 페널티가 없습니다."),
            (ActionType.Herd, "herd", "무리(패시브): 주변 1블록 내 아군에게 가속을 부여합니다(이동 거리 +1, 피격 시 해제)."),
            (ActionType.Convert, "convert", "전향(패시브): 공격한 적 유닛을 아군으로 전환합니다."),
            (ActionType.Combo, "combo", "연타(패시브): 적을 처치하면 같은 턴에 추가로 공격할 수 있습니다."),
            (ActionType.Scout, "scout", "정찰(패시브): 시야 +1."),
            (ActionType.Splash, "splash", "스플래시(패시브): 공격한 대상 주변 1블록 내 적 유닛들에게도 광역 피해를 입힙니다."),
            (ActionType.Stiff, "stiff", "뻣뻣함(패시브): 공격받으면 반격을 갖고 있어도 발동시키지 않습니다."),
            (ActionType.Freeze, "freeze", "빙결(패시브): 공격 시 대상을 다음 턴 동안 행동불능으로 만듭니다."),
        };

        /// <summary>BattleHud의 선택적 행동 버튼 표. BattleHud.cs의 같은 이름 표와 값이 어긋나면 버튼
        /// 이름(Find 경로)이 안 맞아 WireActionButtons가 실패한다.</summary>
        private static readonly (ActionType Flag, string Icon, string Tooltip)[] OptionalActionDefs =
        {
            (ActionType.Defend, "guard", "방어 태세: 받는 피해를 줄입니다. (방어력 +" + CombatSystem.GuardDefenseBonus + ")"),
            (ActionType.Heal, "heal", "치유: 사거리 내의 모든 아군 유닛(자신 제외)의 체력을 회복시킵니다."),
            (ActionType.SelfDestruct, "selfdestruct", "자폭: 스스로를 희생해 주위 1칸의 모든 적에게 남은 체력만큼 피해를 입힙니다."),
            (ActionType.Wait, "hp", "대기: 이번 턴 행동을 종료하고 체력을 2(자기 영토 4) 회복합니다."),
        };

        public static void GenerateAll()
        {
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
            var cityResourceBar = GenerateCityResourceBar(font);
            var techTreePanel = GenerateTechTreePanel(font);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            PatchUnitPrefabs(hpDisplay, damagePopup);
            AssignToScenes(battleHud, sandboxHud, cityResourceBar, techTreePanel);

            Debug.Log("[UIPrefabSetup] Done: HpDisplay/DamagePopup/EventSystem/PaletteButton/BattleHud/SandboxHud/CityResourceBar/TechTreePanel prefabs created under " + UiFolderPath);
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

            var bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
            bg.name = "Bar_Bg";
            Object.DestroyImmediate(bg.GetComponent<Collider>());
            bg.transform.SetParent(root.transform, false);
            bg.transform.localPosition = new Vector3(0f, UnitView.HpBarLocalY, 0f);
            bg.transform.localScale = new Vector3(UnitView.HpBarSize.x, UnitView.HpBarSize.y, 1f);
            bg.GetComponent<Renderer>().sharedMaterial = barBackgroundMaterial;

            var fill = GameObject.CreatePrimitive(PrimitiveType.Quad);
            fill.name = "Bar_Fill";
            Object.DestroyImmediate(fill.GetComponent<Collider>());
            fill.transform.SetParent(root.transform, false);
            fill.transform.localPosition = new Vector3(0f, UnitView.HpBarLocalY, -0.001f);
            fill.transform.localScale = new Vector3(UnitView.HpBarSize.x, UnitView.HpBarSize.y, 1f);

            var textGo = new GameObject("Number");
            textGo.transform.SetParent(root.transform, false);
            textGo.transform.localPosition = new Vector3(0f, UnitView.HpNumberLocalY, 0f);
            // 0.3f였던 기존 크기의 절반(사용자 요청: 인게임 체력 라벨을 2배 줄여달라).
            textGo.transform.localScale = Vector3.one * 0.15f;
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

        private static GameObject GenerateBattleHud(GameObject eventSystemPrefab, Font font)
        {
            var root = new GameObject("BattleHud");
            var hud = root.AddComponent<BattleHud>();

            var canvasRoot = CreateCanvas(root.transform);
            BuildTurnBadge(font, canvasRoot);
            BuildUnitRoster(canvasRoot);
            BuildActionLog(canvasRoot);
            BuildUnitPanel(font, canvasRoot);
            BuildActionButtons(canvasRoot);
            BuildTooltip(font, canvasRoot);
            BuildBattleEndPanel(font, canvasRoot);

            SetPrivateField(hud, "eventSystemPrefab", eventSystemPrefab);
            SetPrivateField(hud, "uiFont", font);
            EditorUtility.SetDirty(hud);

            string path = $"{UiFolderPath}/BattleHud.prefab";
            var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved;
        }

        private static void BuildTurnBadge(Font font, Transform root)
        {
            var panel = CreateRect("TurnBadge", root);
            panel.anchorMin = panel.anchorMax = new Vector2(0f, 1f);
            panel.pivot = new Vector2(0f, 1f);
            panel.anchoredPosition = new Vector2(16f, -16f);
            panel.sizeDelta = new Vector2(92f, 44f);
            CreatePanelImage(panel, PlayerAccent);

            CreateIconPlaceholder("Icon", panel, 28f, new Vector2(8f, -8f));
            var number = CreateNumberText(font, "Number", panel, new Vector2(44f, -8f), new Vector2(40f, 32f));
            number.fontSize = 24;
            number.alignment = TextAnchor.MiddleCenter;
        }

        // ---------- 유닛 상태 로스터 (좌상단, TurnBadge 바로 아래) ----------
        // 뼈대(배경/Viewport/Content/Scrollbar)만 여기서 만든다. 행(유닛 하나당 아이콘+숫자)은 유닛 수가
        // 계속 바뀌는 진짜 동적 데이터라 BattleHud.SetRoster가 코드로 그때그때 만든다.

        private const float RosterPanelWidth = 118f;
        /// <summary>BattleHud.RosterContentMinHeight와 같아야 한다.</summary>
        private const float RosterPanelHeight = 140f;
        private const float RosterScrollbarWidth = 5f;

        private static void BuildUnitRoster(Transform root)
        {
            BuildScrollPanel("UnitRoster", root,
                anchor: new Vector2(0f, 1f), pivot: new Vector2(0f, 1f),
                anchoredPos: new Vector2(16f, -68f), // TurnBadge(높이 44, y -16~-60) 바로 아래
                size: new Vector2(RosterPanelWidth, RosterPanelHeight),
                background: PanelBackground, scrollbarWidth: RosterScrollbarWidth);
        }

        // ---------- 행동 로그 (우상단) ----------
        // 스크롤 없이 최근 줄만 유지하는 킬피드 방식(BattleHud.AddLogEntry)이라 Viewport/ScrollRect가
        // 필요 없다 — 배경 패널 + Content(줄이 붙는 곳)만 있으면 된다.

        private const float LogPanelWidth = 260f;
        private const float LogPanelHeight = 190f;

        private static void BuildActionLog(Transform root)
        {
            var panel = CreateRect("ActionLog", root);
            panel.anchorMin = panel.anchorMax = new Vector2(1f, 1f);
            panel.pivot = new Vector2(1f, 1f);
            panel.anchoredPosition = new Vector2(-16f, -16f);
            panel.sizeDelta = new Vector2(LogPanelWidth, LogPanelHeight);
            CreatePanelImage(panel, PanelBackground);
            panel.gameObject.AddComponent<RectMask2D>();

            var content = CreateRect("Content", panel);
            content.anchorMin = Vector2.zero;
            content.anchorMax = Vector2.one;
            content.offsetMin = new Vector2(8f, 6f);
            content.offsetMax = new Vector2(-8f, -6f);
        }

        private static void BuildUnitPanel(Font font, Transform root)
        {
            var panel = CreateRect("UnitPanel", root);
            panel.anchorMin = panel.anchorMax = new Vector2(0f, 0f);
            panel.pivot = new Vector2(0f, 0f);
            panel.anchoredPosition = new Vector2(16f, 16f);
            panel.sizeDelta = new Vector2(150f, 206f);
            CreatePanelImage(panel, PanelBackground);

            var accent = CreateRect("Accent", panel);
            accent.anchorMin = new Vector2(0f, 0f);
            accent.anchorMax = new Vector2(0f, 1f);
            accent.pivot = new Vector2(0f, 0.5f);
            accent.sizeDelta = new Vector2(5f, 0f);
            accent.anchoredPosition = Vector2.zero;
            CreatePanelImage(accent, PlayerAccent);

            const float rowH = 30f;
            float top = -8f;

            CreateIconPlaceholder("HpIcon", panel, 22f, new Vector2(14f, top));
            CreateNumberText(font, "HpText", panel, new Vector2(42f, top + 3f), new Vector2(96f, 24f));

            var hpBarBg = CreateRect("HpBarBg", panel);
            hpBarBg.anchorMin = hpBarBg.anchorMax = new Vector2(0f, 1f);
            hpBarBg.pivot = new Vector2(0f, 1f);
            hpBarBg.anchoredPosition = new Vector2(14f, top - 22f);
            hpBarBg.sizeDelta = new Vector2(122f, 6f);
            CreatePanelImage(hpBarBg, new Color(1f, 1f, 1f, 0.15f));

            var hpBarFill = CreateRect("HpBarFill", hpBarBg);
            hpBarFill.anchorMin = Vector2.zero;
            hpBarFill.anchorMax = new Vector2(1f, 1f);
            hpBarFill.offsetMin = Vector2.zero;
            hpBarFill.offsetMax = Vector2.zero;
            CreatePanelImage(hpBarFill, HpColorScale.ForFraction(1f));

            top -= rowH + 10f;
            CreateIconPlaceholder("AttackIcon", panel, 22f, new Vector2(14f, top));
            CreateNumberText(font, "AttackText", panel, new Vector2(42f, top + 3f), new Vector2(96f, 24f));

            top -= rowH;
            CreateIconPlaceholder("DefenseIcon", panel, 22f, new Vector2(14f, top));
            CreateNumberText(font, "DefenseText", panel, new Vector2(42f, top + 3f), new Vector2(96f, 24f));

            top -= rowH;
            CreateIconPlaceholder("MoveIcon", panel, 22f, new Vector2(14f, top));
            CreateNumberText(font, "MoveText", panel, new Vector2(42f, top + 3f), new Vector2(96f, 24f));

            top -= rowH;
            CreateIconPlaceholder("RangeIcon", panel, 22f, new Vector2(14f, top));
            CreateNumberText(font, "RangeText", panel, new Vector2(42f, top + 3f), new Vector2(96f, 24f));

            // 다음 top(-168)이 곧 패시브 배지 줄 위치다 — BattleHud.PassiveRowTop 상수와 값이 같아야 한다.
            top -= rowH;
            foreach (var def in PassiveDefs)
                CreatePassiveBadgePlaceholder(panel, def.Icon, def.Tooltip);
        }

        private static void CreatePassiveBadgePlaceholder(Transform parent, string iconName, string tooltip)
        {
            var rect = CreateRect("Passive_" + iconName, parent);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(26f, 26f);

            var bg = rect.gameObject.AddComponent<Image>();
            bg.color = PassiveBadgeBg;

            var iconRect = CreateRect("Icon", rect);
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = new Vector2(4f, 4f);
            iconRect.offsetMax = new Vector2(-4f, -4f);
            var icon = iconRect.gameObject.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            AddTooltipTrigger(rect.gameObject, tooltip);
        }

        private static void BuildActionButtons(Transform root)
        {
            CreateIconButton(root, "EndTurnButton", new Vector2(-16f, 16f), ButtonIdle, "턴 종료: 현재 팀의 턴을 마칩니다.");

            foreach (var def in OptionalActionDefs)
                CreateIconButton(root, def.Flag + "Button", Vector2.zero, ButtonIdle, def.Tooltip);

            CreateIconButton(root, "DeselectButton", Vector2.zero, ButtonIdle, "선택 해제: 유닛 선택을 취소합니다.");
        }

        private static void CreateIconButton(Transform root, string name, Vector2 anchoredPosFromBottomRight, Color bg, string tooltip)
        {
            var rect = CreateRect(name, root);
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.sizeDelta = new Vector2(52f, 52f);
            rect.anchoredPosition = anchoredPosFromBottomRight;

            var bgImage = CreatePanelImage(rect, bg);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = bgImage;

            var iconRect = CreateRect("Icon", rect);
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = new Vector2(10f, 10f);
            iconRect.offsetMax = new Vector2(-10f, -10f);
            var icon = iconRect.gameObject.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            AddTooltipTrigger(rect.gameObject, tooltip);
        }

        private static void BuildTooltip(Font font, Transform root)
        {
            var panel = CreateRect("Tooltip", root);
            panel.anchorMin = panel.anchorMax = new Vector2(1f, 0f);
            panel.pivot = new Vector2(1f, 0f);
            panel.anchoredPosition = new Vector2(-16f, 16f + 52f + 10f);
            panel.sizeDelta = new Vector2(360f, 56f);
            CreatePanelImage(panel, PanelBackground);

            var textRect = CreateRect("Text", panel);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10f, 4f);
            textRect.offsetMax = new Vector2(-10f, -4f);
            var text = textRect.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = 15;
            text.alignment = TextAnchor.MiddleRight;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            panel.gameObject.SetActive(false);
        }

        private static void BuildBattleEndPanel(Font font, Transform root)
        {
            var panel = CreateRect("BattleEnd", root);
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = Vector2.one;
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;
            CreatePanelImage(panel, new Color(0f, 0f, 0f, 0.55f));

            var iconRect = CreateRect("Icon", panel);
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0f);
            iconRect.sizeDelta = new Vector2(120f, 120f);
            iconRect.anchoredPosition = new Vector2(0f, 10f);
            var icon = iconRect.gameObject.AddComponent<Image>();
            icon.preserveAspect = true;

            var textRect = CreateRect("Label", panel);
            textRect.anchorMin = textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.pivot = new Vector2(0.5f, 1f);
            textRect.sizeDelta = new Vector2(400f, 50f);
            textRect.anchoredPosition = Vector2.zero;
            var text = textRect.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = 36;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;

            var restartRect = CreateRect("RestartButton", panel);
            restartRect.anchorMin = restartRect.anchorMax = new Vector2(0.5f, 0.5f);
            restartRect.pivot = new Vector2(0.5f, 1f);
            restartRect.sizeDelta = new Vector2(180f, 44f);
            restartRect.anchoredPosition = new Vector2(0f, -60f);
            var restartBg = CreatePanelImage(restartRect, ButtonIdle);
            var restartButton = restartRect.gameObject.AddComponent<Button>();
            restartButton.targetGraphic = restartBg;

            var restartLabelRect = CreateRect("Label", restartRect);
            restartLabelRect.anchorMin = Vector2.zero;
            restartLabelRect.anchorMax = Vector2.one;
            restartLabelRect.offsetMin = Vector2.zero;
            restartLabelRect.offsetMax = Vector2.zero;
            var restartLabel = restartLabelRect.gameObject.AddComponent<Text>();
            restartLabel.font = font;
            restartLabel.fontSize = 18;
            restartLabel.alignment = TextAnchor.MiddleCenter;
            restartLabel.color = Color.white;
            restartLabel.text = "다시 시작";

            panel.gameObject.SetActive(false);
        }

        // ---------- SandboxHud ----------

        private const float SandboxPanelWidth = 220f;
        private const float SandboxPaletteHeight = 300f;
        private const float SandboxScrollbarWidth = 6f;

        private static GameObject GenerateSandboxHud(GameObject paletteButtonPrefab, Font font)
        {
            var root = new GameObject("SandboxHud");
            var hud = root.AddComponent<SandboxHud>();

            var canvasRoot = CreateCanvas(root.transform);
            BuildSandboxToolbar(font, canvasRoot);
            BuildSandboxPalette(canvasRoot);
            BuildSandboxStartButton(font, canvasRoot);

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
            panel.sizeDelta = new Vector2(SandboxPanelWidth, 96f);
            CreatePanelImage(panel, SandboxPanelBackground);

            float halfWidth = (SandboxPanelWidth - 24f) / 2f;
            CreateTextButtonPlaceholder(font, panel, "불러오기", new Vector2(8f, -8f), new Vector2(halfWidth, 28f), ButtonIdle);
            CreateTextButtonPlaceholder(font, panel, "내보내기", new Vector2(8f + halfWidth + 8f, -8f), new Vector2(halfWidth, 28f), ButtonIdle);
            CreateTextButtonPlaceholder(font, panel, "플레이어", new Vector2(8f, -44f), new Vector2(halfWidth, 28f), PlayerAccent);
            CreateTextButtonPlaceholder(font, panel, "적", new Vector2(8f + halfWidth + 8f, -44f), new Vector2(halfWidth, 28f), ButtonIdle);

            var statusRect = CreateRect("Status", panel);
            statusRect.anchorMin = statusRect.anchorMax = new Vector2(0f, 1f);
            statusRect.pivot = new Vector2(0f, 1f);
            statusRect.anchoredPosition = new Vector2(8f, -78f);
            statusRect.sizeDelta = new Vector2(SandboxPanelWidth - 16f, 18f);
            var status = statusRect.gameObject.AddComponent<Text>();
            status.font = font;
            status.fontSize = 13;
            status.color = new Color(1f, 1f, 1f, 0.75f);
            status.horizontalOverflow = HorizontalWrapMode.Wrap;
        }

        private static void CreateTextButtonPlaceholder(Font font, Transform parent, string label, Vector2 anchoredPos, Vector2 size, Color bg)
        {
            var rect = CreateRect(label + "Button", parent);
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
        private static void BuildSandboxPalette(Transform root)
        {
            BuildScrollPanel("Palette", root,
                anchor: new Vector2(0f, 1f), pivot: new Vector2(0f, 1f),
                anchoredPos: new Vector2(16f, -120f),
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
            text.text = "전투 시작";
        }

        // ---------- CityResourceBar (도시 발전도/인구/골드/신앙, 재사용 가능한 독립 프리팹) ----------

        /// <summary>표시 순서 + 툴팁 문구. CityResourceHud.SetResources가 채우는 순서와 일치해야 한다.</summary>
        private static readonly (string Name, string Tooltip)[] CityResourceDefs =
        {
            ("Development", "도시 발전도: 도시 발전에 필요한 자원."),
            ("Population", "인구: 도시가 보유할 수 있는 유닛 총 수량."),
            ("Gold", "골드: 기술 발전/유닛 생산에 쓰는 기본 재화."),
            ("Faith", "신앙: 신앙 펀치(액티브 스킬) 사용에 필요한 재화."),
        };

        private const float CityResourceSlotWidth = 92f;
        private const float CityResourceBarHeight = 40f;

        /// <summary>BattleHud와 독립된 별도 프리팹으로 만든다 — 특정 화면(BattleHud)에 종속되지 않고,
        /// 자원 표시가 필요한 어느 씬/화면에나 그대로 갖다 놓을 수 있게 하기 위함(사용자 요청: 재사용
        /// 가능한 프리팹).</summary>
        private static GameObject GenerateCityResourceBar(Font font)
        {
            var root = new GameObject("CityResourceBar");
            var hud = root.AddComponent<CityResourceHud>();

            var canvasRoot = CreateCanvas(root.transform);
            BuildCityResourceBar(font, canvasRoot);

            SetPrivateField(hud, "uiFont", font);
            EditorUtility.SetDirty(hud);

            string path = $"{UiFolderPath}/CityResourceBar.prefab";
            var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved;
        }

        private static void BuildCityResourceBar(Font font, Transform root)
        {
            var bar = CreateRect("Bar", root);
            bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 1f);
            bar.pivot = new Vector2(0.5f, 1f);
            bar.anchoredPosition = new Vector2(0f, -16f);
            bar.sizeDelta = new Vector2(CityResourceSlotWidth * CityResourceDefs.Length, CityResourceBarHeight);
            CreatePanelImage(bar, PanelBackground);

            for (int i = 0; i < CityResourceDefs.Length; i++)
            {
                var def = CityResourceDefs[i];
                var slot = CreateRect(def.Name, bar);
                slot.anchorMin = slot.anchorMax = new Vector2(0f, 0.5f);
                slot.pivot = new Vector2(0f, 0.5f);
                slot.sizeDelta = new Vector2(CityResourceSlotWidth, CityResourceBarHeight);
                slot.anchoredPosition = new Vector2(i * CityResourceSlotWidth, 0f);

                CreateIconPlaceholder("Icon", slot, 24f, new Vector2(8f, -8f));
                var number = CreateNumberText(font, "Number", slot, new Vector2(38f, -10f), new Vector2(48f, 20f));
                number.fontSize = 16;
            }
        }

        // ---------- TechTreePanel (기술트리, 5갈래 x 1+2+2티어, 재사용 가능한 독립 프리팹) ----------

        // 중앙 허브(시작 노드)에서 5갈래가 방사형으로 퍼져나가는 배치. 반지름은 허브 기준 거리,
        // 같은 갈래의 2/3티어는 TechBranchAngleSpreadDeg만큼 좌우로 벌어진 각도에 놓인다(3티어는 부모
        // 2티어와 같은 각도 — 더 바깥쪽으로 이어지는 방사선처럼 보인다). 다른 프리팹과 달리 이 패널만
        // 기준 해상도를 세로로 더 키운다(TechCanvasReferenceResolution) — 25개 노드 + 라벨이 겹치지
        // 않을 공간이 1280x720으로는 부족해서다.
        private static readonly Vector2 TechCanvasReferenceResolution = new Vector2(1280f, 950f);
        private const float TechHubDiameter = 110f;
        private const float TechTier1Diameter = 80f;
        private const float TechTier2Diameter = 70f;
        private const float TechTier3Diameter = 64f;
        private const float TechR1 = 115f;
        private const float TechR2 = 205f;
        // R2->R3 간격은 R1->R2보다 훨씬 넓다 — 3티어는 부모(2티어)와 같은 각도라(Slot이 같음) 둘을 잇는
        // 연결선이 2티어 라벨과 정확히 같은 직선 위에 놓이는데, 라벨 한 줄(약 40px)이 그 사이에 들어갈
        // 여유가 없으면 연결선이 라벨 글자를 가로지른다. 1티어->2티어는 서로 각도가 달라(Slot로 벌어짐)
        // 이 문제가 없다.
        private const float TechR3 = 315f;
        private const float TechBranchAngleSpreadDeg = 20f;
        private const float TechTreeCenterY = 40f; // Detail 패널 공간을 아래에 남기기 위해 위로.
        private const float TechIconInsetRatio = 0.58f; // 노드 지름 대비 아이콘 크기 비율.
        private const float TechLabelWidth = 110f;
        private const float TechLabelHeight = 30f;
        private const float TechConnectorThickness = 3f;
        private const float TechDetailWidth = 420f;
        private const float TechDetailHeight = 130f;
        private static readonly Color TechConnectorColor = new Color(1f, 1f, 1f, 0.25f);
        private static readonly Color TechHubColor = new Color(1f, 1f, 1f, 0.9f);

        /// <summary>CityResourceBar와 마찬가지로 BattleHud와 독립된 별도 프리팹으로 만든다 — 도시 자원
        /// 화면이 있는 곳이라면 어디든 그대로 갖다 놓을 수 있게 하기 위함이다. 노드 배치(가로=갈래,
        /// 세로=티어)와 부모-자식 연결선은 TechTreeDefinition의 Branch/Tier/Slot/ParentId만 보고 이
        /// 메서드가 전부 계산한다 — 기술을 추가/삭제해도 이 코드는 건드릴 필요가 없다.</summary>
        private static GameObject GenerateTechTreePanel(Font font)
        {
            var root = new GameObject("TechTreePanel");
            var hud = root.AddComponent<TechTreeHud>();

            var canvasRoot = CreateCanvas(root.transform);
            canvasRoot.GetComponent<CanvasScaler>().referenceResolution = TechCanvasReferenceResolution;
            BuildTechTreeToggleButton(font, canvasRoot);
            var panel = BuildTechTreeContent(font, canvasRoot);
            panel.gameObject.SetActive(false);

            SetPrivateField(hud, "uiFont", font);
            EditorUtility.SetDirty(hud);

            string path = $"{UiFolderPath}/TechTreePanel.prefab";
            var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved;
        }

        private static void BuildTechTreeToggleButton(Font font, Transform root)
        {
            var rect = CreateRect("ToggleButton", root);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -64f); // CityResourceBar(높이 40, y -16~-56) 바로 아래
            rect.sizeDelta = new Vector2(96f, 30f);

            var bg = CreatePanelImage(rect, ButtonIdle);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;

            var textRect = CreateRect("Label", rect);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var text = textRect.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = 15;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = "기술트리";
        }

        private static RectTransform BuildTechTreeContent(Font font, Transform root)
        {
            var panel = CreateRect("Panel", root);
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = Vector2.one;
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;
            CreatePanelImage(panel, new Color(0f, 0f, 0f, 0.55f));

            var closeRect = CreateRect("CloseButton", panel);
            closeRect.anchorMin = closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-16f, -16f);
            closeRect.sizeDelta = new Vector2(36f, 36f);
            var closeBg = CreatePanelImage(closeRect, ButtonIdle);
            var closeButton = closeRect.gameObject.AddComponent<Button>();
            closeButton.targetGraphic = closeBg;
            var closeLabelRect = CreateRect("Label", closeRect);
            closeLabelRect.anchorMin = Vector2.zero;
            closeLabelRect.anchorMax = Vector2.one;
            closeLabelRect.offsetMin = Vector2.zero;
            closeLabelRect.offsetMax = Vector2.zero;
            var closeLabel = closeLabelRect.gameObject.AddComponent<Text>();
            closeLabel.font = font;
            closeLabel.fontSize = 20;
            closeLabel.alignment = TextAnchor.MiddleCenter;
            closeLabel.color = Color.white;
            closeLabel.text = "X";

            var tree = CreateRect("Tree", panel);
            tree.anchorMin = tree.anchorMax = new Vector2(0.5f, 0.5f);
            tree.pivot = new Vector2(0.5f, 0.5f);
            tree.anchoredPosition = new Vector2(0f, TechTreeCenterY);
            tree.sizeDelta = new Vector2(700f, 700f); // 실제 배치는 자식들의 anchoredPosition이 결정한다.

            BuildTechTreeNodes(font, tree);
            BuildTechTreeDetail(font, panel);

            return panel;
        }

        /// <summary>중앙 허브(시작 노드)를 원점으로, 갈래마다 72도씩 각도를 나누고 그 안에서 티어별로
        /// 반지름을 늘려가며 배치한다(허브 -> 1티어 -> 2티어(좌우로 벌어짐) -> 3티어(부모와 같은 각도로
        /// 더 바깥쪽)). 노드 위치/연결선 전부 TechTreeDefinition의 Branch/Tier/Slot/ParentId만 보고
        /// 계산하므로, 기술을 추가/삭제해도 이 메서드는 건드릴 필요가 없다(갈래 수가 5가 아니게 바뀌면
        /// 각도 간격만 자동으로 달라진다).</summary>
        private static void BuildTechTreeNodes(Font font, RectTransform tree)
        {
            var positions = new Dictionary<TechId, Vector2>();
            var diameters = new Dictionary<TechId, float>();

            const float startAngleDeg = 90f; // 첫 갈래가 정확히 위쪽을 향하게.
            float branchStepDeg = 360f / TechTreeDefinition.BranchOrder.Length;

            for (int b = 0; b < TechTreeDefinition.BranchOrder.Length; b++)
            {
                float branchAngleDeg = startAngleDeg - b * branchStepDeg;
                var branch = TechTreeDefinition.BranchOrder[b];

                foreach (var node in TechTreeDefinition.Nodes)
                {
                    if (node.Branch != branch) continue;

                    float radius = node.Tier == 1 ? TechR1 : node.Tier == 2 ? TechR2 : TechR3;
                    float angleDeg = node.Tier == 1
                        ? branchAngleDeg
                        : branchAngleDeg + (node.Slot == 0 ? -TechBranchAngleSpreadDeg : TechBranchAngleSpreadDeg);
                    float angleRad = angleDeg * Mathf.Deg2Rad;

                    positions[node.Id] = new Vector2(radius * Mathf.Cos(angleRad), radius * Mathf.Sin(angleRad));
                    diameters[node.Id] = node.Tier == 1 ? TechTier1Diameter : node.Tier == 2 ? TechTier2Diameter : TechTier3Diameter;
                }
            }

            // 연결선을 노드보다 먼저 만들어서 하이어라키상 노드 배경 아래에 깔리게 한다. 1티어는 허브
            // (원점, ParentId=None)에서 바로 이어진다.
            foreach (var node in TechTreeDefinition.Nodes)
            {
                Vector2 from = node.ParentId == TechId.None ? Vector2.zero : positions[node.ParentId];
                CreateTechConnector(tree, from, positions[node.Id]);
            }

            CreateTechHub(tree);

            foreach (var node in TechTreeDefinition.Nodes)
                CreateTechNode(font, tree, node, positions[node.Id], diameters[node.Id]);
        }

        /// <summary>두 점을 잇는 얇은 Image를 회전시켜 직선 커넥터처럼 보이게 만드는 표준 uGUI 트릭.</summary>
        private static void CreateTechConnector(RectTransform parent, Vector2 from, Vector2 to)
        {
            Vector2 delta = to - from;
            float length = delta.magnitude;
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;

            var rect = CreateRect("Connector", parent);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = from + delta * 0.5f;
            rect.sizeDelta = new Vector2(length, TechConnectorThickness);
            rect.localRotation = Quaternion.Euler(0f, 0f, angle);
            CreatePanelImage(rect, TechConnectorColor);
        }

        /// <summary>방사형 배치의 중심에 놓이는 장식용 허브(시작 노드) — 어떤 TechId도 아니고 클릭할 수
        /// 없다(Button 없음). 원형 배경(Bg)/아이콘(Icon) 스프라이트는 여기서 굽지 않는다 — Sprite.Create
        /// 결과는 디스크 에셋이 아니라서 프리팹에 구워두면 참조가 유지되지 않으므로(IconLibrary/
        /// RuntimeSprite 참고), TechTreeHud.Init()이 인스턴스화 직후 채운다.</summary>
        private static void CreateTechHub(RectTransform parent)
        {
            var rect = CreateRect("Hub", parent);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(TechHubDiameter, TechHubDiameter);

            var bg = rect.gameObject.AddComponent<Image>();
            bg.color = TechHubColor;

            CreateTechNodeIcon(rect, TechHubDiameter);
        }

        /// <summary>노드 하나(원형 배경 버튼 + 중앙 아이콘 + 바로 아래 이름 라벨). 이름을 TechId.ToString()
        /// 으로 구워서 TechTreeHud.Init이 TechTreeDefinition을 순회하며 같은 이름으로 찾아 배선한다
        /// (CityResourceHud의 Wire 패턴과 같음). 원형 배경 색/아이콘 스프라이트는 여기서 굽지 않는다 —
        /// 색은 해금 상태에 따라 매번 바뀌므로, 스프라이트는 CreateTechHub와 같은 이유로 TechTreeHud.Init/
        /// SetState가 런타임에 채운다.</summary>
        private static void CreateTechNode(Font font, RectTransform parent, TechNodeData node, Vector2 pos, float diameter)
        {
            var rect = CreateRect(node.Id.ToString(), parent);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(diameter, diameter);

            var bg = rect.gameObject.AddComponent<Image>();
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;

            CreateTechNodeIcon(rect, diameter);

            var labelRect = CreateRect("Label", rect);
            labelRect.anchorMin = labelRect.anchorMax = new Vector2(0.5f, 0f);
            labelRect.pivot = new Vector2(0.5f, 1f);
            labelRect.anchoredPosition = new Vector2(0f, -4f);
            labelRect.sizeDelta = new Vector2(TechLabelWidth, TechLabelHeight);
            var label = labelRect.gameObject.AddComponent<Text>();
            label.font = font;
            label.fontSize = 12;
            label.alignment = TextAnchor.UpperCenter;
            label.color = Color.white;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.raycastTarget = false; // 라벨이 원 바깥에 있어도 클릭은 항상 원(Bg)이 받도록.
            label.text = node.Name;
        }

        /// <summary>노드/허브 원 중앙에 들어가는 아이콘 Image 뼈대만 만든다(sprite는 런타임에 채움).
        /// raycastTarget을 꺼서 클릭이 항상 부모의 Bg(=Button의 targetGraphic)로 가게 한다 — CreateIconButton과
        /// 같은 이유.</summary>
        private static void CreateTechNodeIcon(RectTransform parent, float diameter)
        {
            float inset = diameter * (1f - TechIconInsetRatio) * 0.5f;
            var iconRect = CreateRect("Icon", parent);
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = new Vector2(inset, inset);
            iconRect.offsetMax = new Vector2(-inset, -inset);
            var icon = iconRect.gameObject.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
        }

        /// <summary>선택된 노드의 이름/효과/해금 가능 여부/비용을 보여주는 하단 상세 패널.
        /// TechTreeHud.SelectNode/RefreshDetail이 채운다.</summary>
        private static void BuildTechTreeDetail(Font font, Transform panel)
        {
            var detail = CreateRect("Detail", panel);
            detail.anchorMin = new Vector2(0.5f, 0f);
            detail.anchorMax = new Vector2(0.5f, 0f);
            detail.pivot = new Vector2(0.5f, 0f);
            detail.anchoredPosition = new Vector2(0f, 20f);
            detail.sizeDelta = new Vector2(TechDetailWidth, TechDetailHeight);
            CreatePanelImage(detail, PanelBackground);

            var name = CreateRect("Name", detail);
            name.anchorMin = name.anchorMax = new Vector2(0f, 1f);
            name.pivot = new Vector2(0f, 1f);
            name.anchoredPosition = new Vector2(14f, -10f);
            name.sizeDelta = new Vector2(TechDetailWidth - 28f, 26f);
            var nameText = name.gameObject.AddComponent<Text>();
            nameText.font = font;
            nameText.fontSize = 18;
            nameText.alignment = TextAnchor.MiddleLeft;
            nameText.color = Color.white;

            var effect = CreateRect("Effect", detail);
            effect.anchorMin = effect.anchorMax = new Vector2(0f, 1f);
            effect.pivot = new Vector2(0f, 1f);
            effect.anchoredPosition = new Vector2(14f, -40f);
            effect.sizeDelta = new Vector2(TechDetailWidth - 28f, 48f);
            var effectText = effect.gameObject.AddComponent<Text>();
            effectText.font = font;
            effectText.fontSize = 13;
            effectText.alignment = TextAnchor.UpperLeft;
            effectText.color = new Color(1f, 1f, 1f, 0.85f);
            effectText.horizontalOverflow = HorizontalWrapMode.Wrap;
            effectText.verticalOverflow = VerticalWrapMode.Overflow;

            var status = CreateRect("Status", detail);
            status.anchorMin = status.anchorMax = new Vector2(0f, 0f);
            status.pivot = new Vector2(0f, 0f);
            status.anchoredPosition = new Vector2(14f, 10f);
            status.sizeDelta = new Vector2(230f, 34f);
            var statusText = status.gameObject.AddComponent<Text>();
            statusText.font = font;
            statusText.fontSize = 13;
            statusText.alignment = TextAnchor.MiddleLeft;
            statusText.color = new Color(1f, 0.85f, 0.4f);
            statusText.horizontalOverflow = HorizontalWrapMode.Wrap;

            var unlockRect = CreateRect("UnlockButton", detail);
            unlockRect.anchorMin = unlockRect.anchorMax = new Vector2(1f, 0f);
            unlockRect.pivot = new Vector2(1f, 0f);
            unlockRect.anchoredPosition = new Vector2(-14f, 10f);
            unlockRect.sizeDelta = new Vector2(120f, 34f);
            var unlockBg = CreatePanelImage(unlockRect, PlayerAccent);
            var unlockButton = unlockRect.gameObject.AddComponent<Button>();
            unlockButton.targetGraphic = unlockBg;

            var unlockLabelRect = CreateRect("Label", unlockRect);
            unlockLabelRect.anchorMin = Vector2.zero;
            unlockLabelRect.anchorMax = Vector2.one;
            unlockLabelRect.offsetMin = Vector2.zero;
            unlockLabelRect.offsetMax = Vector2.zero;
            var unlockLabel = unlockLabelRect.gameObject.AddComponent<Text>();
            unlockLabel.font = font;
            unlockLabel.fontSize = 15;
            unlockLabel.alignment = TextAnchor.MiddleCenter;
            unlockLabel.color = Color.white;
            unlockLabel.text = "해금";
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

        private static Image CreateIconPlaceholder(string name, Transform parent, float size, Vector2 anchoredPos)
        {
            var rect = CreateRect(name, parent);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = anchoredPos;

            var img = rect.gameObject.AddComponent<Image>();
            img.preserveAspect = true;
            return img;
        }

        private static Text CreateNumberText(Font font, string name, Transform parent, Vector2 anchoredPos, Vector2 size)
        {
            var rect = CreateRect(name, parent);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPos;

            var text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = 18;
            text.alignment = TextAnchor.MiddleLeft;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        /// <summary>Text(툴팁 문구)만 여기서 굽는다 — OnEnter/OnExit(델리게이트)는 프리팹에 저장할 수
        /// 없으니 BattleHud.Init()이 인스턴스화 직후 런타임에 잇는다.</summary>
        private static void AddTooltipTrigger(GameObject go, string tooltipText)
        {
            var trigger = go.AddComponent<TooltipTrigger>();
            trigger.Text = tooltipText;
        }

        // ---------- 씬 연결 / 공용 유틸 ----------

        /// <summary>cityResourceBarPrefab/techTreePanelPrefab은 커스텀(샌드박스 배치) 화면에만 연결한다 —
        /// 데모 전투 씬(SampleScene)에는 배정하지 않으므로 null로 남는다(BattleController가 null 체크 후
        /// 생성 자체를 건너뛴다).</summary>
        private static void AssignToScenes(GameObject battleHudPrefab, GameObject sandboxHudPrefab, GameObject cityResourceBarPrefab, GameObject techTreePanelPrefab)
        {
            AssignToScene(ScenePath, battleHudPrefab, sandboxHudPrefab, null, null);
            if (AssetDatabase.LoadAssetAtPath<Object>(SandboxScenePath) != null)
                AssignToScene(SandboxScenePath, battleHudPrefab, sandboxHudPrefab, cityResourceBarPrefab, techTreePanelPrefab);
        }

        private static void AssignToScene(string scenePath, GameObject battleHudPrefab, GameObject sandboxHudPrefab, GameObject cityResourceBarPrefab, GameObject techTreePanelPrefab)
        {
            var scene = EditorSceneManager.OpenScene(scenePath);
            var controller = Object.FindFirstObjectByType<BattleController>(FindObjectsInactive.Include);
            if (controller == null)
            {
                Debug.LogError("[UIPrefabSetup] BattleController not found in " + scenePath);
                return;
            }

            SetPrivateField(controller, "hudPrefab", battleHudPrefab.GetComponent<BattleHud>());
            SetPrivateField(controller, "sandboxHudPrefab", sandboxHudPrefab.GetComponent<SandboxHud>());
            if (cityResourceBarPrefab != null)
                SetPrivateField(controller, "cityResourceHudPrefab", cityResourceBarPrefab.GetComponent<CityResourceHud>());
            if (techTreePanelPrefab != null)
                SetPrivateField(controller, "techTreeHudPrefab", techTreePanelPrefab.GetComponent<TechTreeHud>());
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
