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

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            PatchUnitPrefabs(hpDisplay, damagePopup);
            AssignToScenes(battleHud, sandboxHud);

            Debug.Log("[UIPrefabSetup] Done: HpDisplay/DamagePopup/EventSystem/PaletteButton/BattleHud/SandboxHud prefabs created under " + UiFolderPath);
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

        private static void AssignToScenes(GameObject battleHudPrefab, GameObject sandboxHudPrefab)
        {
            AssignToScene(ScenePath, battleHudPrefab, sandboxHudPrefab);
            if (AssetDatabase.LoadAssetAtPath<Object>(SandboxScenePath) != null)
                AssignToScene(SandboxScenePath, battleHudPrefab, sandboxHudPrefab);
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
            SetPrivateField(controller, "sandboxHudPrefab", sandboxHudPrefab.GetComponent<SandboxHud>());
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
