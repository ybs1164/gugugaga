using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TacticsECS.EditorTools
{
    /// <summary>
    /// 아이콘 UI/모바일 레이아웃(docs/UxIconizationPlan.md) 검증 — 배치모드 전용. UIVerification과 같은 패턴으로 Play 모드 없이
    /// 인스턴스화만으로 확인하고 "[MobileUiVerification] ALL PASS"만 보면 된다.
    ///  1. CSV Icon 칸(건물/타일 행동/보상/과업/해금 내역)과 MenuIcons가 돌려주는 아이콘이 전부 Resources/Icons에 실제로 있는지.
    ///  2. ResponsiveCanvas.Attach가 캔버스 자식을 순서 그대로 SafeArea 아래로 옮기는지.
    ///  3. ActionMenuHud가 옵션 수만큼 원형 버튼을 만들고, 막힌 옵션은 누를 수 없고, X가 Closed를 알리는지.
    ///  4. RewardCardHud가 카드를 만들고, 카드를 누르면 onPick(index)을 부르고 스스로 닫히는지.
    /// 사용법: unity run . -- -executeMethod TacticsECS.EditorTools.MobileUiVerification.Run
    /// </summary>
    public static class MobileUiVerification
    {
        private static bool _ok;

        public static void Run()
        {
            GameDataLoader.LoadAll();
            _ok = true;
            VerifyIcons();
            VerifyResponsiveCanvas();
            VerifyActionMenu();
            VerifyRewardCards();
            Debug.Log(_ok ? "[MobileUiVerification] ALL PASS" : "[MobileUiVerification] SOME CHECKS FAILED - see errors above");
        }

        private static void Check(bool condition, string what)
        {
            if (condition) return;
            _ok = false;
            Debug.LogError($"[MobileUiVerification] FAIL: {what}");
        }

        private static void CheckIcon(string icon, string where)
        {
            if (string.IsNullOrEmpty(icon)) { Check(false, $"{where}: empty icon"); return; }
            Check(IconLibrary.Get(icon) != null, $"{where}: icon '{icon}' missing in Resources/Icons");
        }

        private static void VerifyIcons()
        {
            foreach (var b in BuildingDefinition.All) CheckIcon(b.Icon, $"Buildings.csv {b.Id}");
            foreach (var a in TileActionDefinition.All) CheckIcon(a.Icon, $"TileActions.csv {a.Id}");
            foreach (var r in CityRewardDefinition.All) CheckIcon(r.Icon, $"CityRewards.csv {r.Type}");
            foreach (var t in TaskDefinition.All) CheckIcon(t.Icon, $"Tasks.csv {t.Id}");
            foreach (var u in GameTables.TechUnlocks) CheckIcon(u.Icon, $"TechUnlocks.csv {TechGroupSystem.Key(u)}");

            foreach (BlockReason r in System.Enum.GetValues(typeof(BlockReason)))
                if (r != BlockReason.None) { CheckIcon(MenuIcons.ForBlock(r), $"MenuIcons.ForBlock({r})"); Check(MenuIcons.BlockText(r).Length > 0, $"BlockText({r})"); }
            foreach (BattleLogVerb v in System.Enum.GetValues(typeof(BattleLogVerb))) CheckIcon(MenuIcons.ForVerb(v), $"MenuIcons.ForVerb({v})");
            foreach (EconomyLogKind k in System.Enum.GetValues(typeof(EconomyLogKind))) CheckIcon(MenuIcons.ForEconomy(k), $"MenuIcons.ForEconomy({k})");
            foreach (CityRewardType t in System.Enum.GetValues(typeof(CityRewardType))) CheckIcon(MenuIcons.RewardEffect(t).Icon, $"MenuIcons.RewardEffect({t})");
            foreach (var cls in new[] { TileClass.Field, TileClass.Forest, TileClass.Mountain, TileClass.ShallowWater, TileClass.Ocean })
                CheckIcon(MenuIcons.ForTileClass(cls), $"MenuIcons.ForTileClass({cls})");
            foreach (var s in StructureDefinition.All) CheckIcon(MenuIcons.ForStructure(s.Id), $"MenuIcons.ForStructure({s.Id})");
            foreach (var key in new[] { "Build.Farm", "Unit.archer", "Literacy", "Unknown.Key" }) CheckIcon(MenuIcons.ForUnlock(key).Icon, $"MenuIcons.ForUnlock({key})");
            foreach (var name in new[] { "star", "population", "research", "crown", "lock", "check", "reward", "capture", "promote", "disband", "explore" })
                CheckIcon(name, "UI icon");
            Debug.Log("[MobileUiVerification] icons checked");
        }

        private static void VerifyResponsiveCanvas()
        {
            var go = new GameObject("RC_Test", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            try
            {
                var scaler = go.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280f, 720f);
                var a = UiKit.Rect("A", go.transform);
                var b = UiKit.Rect("B", go.transform);
                var rc = ResponsiveCanvas.Attach(go.transform);
                Check(go.transform.childCount == 1 && go.transform.GetChild(0).name == "SafeArea", "Attach leaves only SafeArea under the canvas");
                Check(a.parent == rc.SafeArea && b.parent == rc.SafeArea, "children moved under SafeArea");
                Check(rc.SafeArea.GetChild(0) == a && rc.SafeArea.GetChild(1) == b, "child order preserved");
                Check(ResponsiveCanvas.Attach(go.transform) == rc, "Attach is idempotent");
                var reference = scaler.referenceResolution;
                Check(Mathf.Approximately(Mathf.Max(reference.x, reference.y) / Mathf.Min(reference.x, reference.y), 1280f / 720f), "reference aspect kept (maybe swapped for portrait)");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        private static void VerifyActionMenu()
        {
            var go = new GameObject("ActionMenu_Test");
            try
            {
                var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                var menu = go.AddComponent<ActionMenuHud>();
                menu.Init(font);
                bool closed = false;
                menu.Closed += () => closed = true;
                int clicked = 0;
                var options = new List<ActionMenuOption>
                {
                    new ActionMenuOption { Label = "농장", Icon = "farm", Cost = 5, Affordable = true, Enabled = true, OnClick = () => clicked++ },
                    new ActionMenuOption { Label = "광산", Icon = "mine", Cost = 5, Affordable = false, Enabled = false, Block = BlockReason.NotEnoughGold },
                    new ActionMenuOption { Label = "풍차", Icon = "windmill", Cost = 5, Enabled = false, Block = BlockReason.NeedAdjacent },
                };
                var header = new ActionMenuHeader
                {
                    Icon = "city", Title = "테스트", Level = 2, GaugeFilled = 1, GaugeTotal = 3, GaugeIcon = "population",
                    Chips = new List<ActionMenuChip> { new ActionMenuChip { Icon = "star", Text = "+3" } },
                };
                menu.Show(header, options);
                Check(menu.IsVisible, "menu visible after Show");

                var buttons = go.GetComponentsInChildren<Button>(true);
                // 옵션 3개 + 닫기(X) 1개.
                Check(buttons.Length == 4, $"option buttons (expected 4 incl. close, got {buttons.Length})");
                int interactable = 0;
                Button farm = null, close = null;
                foreach (var b in buttons)
                {
                    if (b.interactable) interactable++;
                    if (b.transform.parent != null && b.transform.parent.name == "Option" && b.transform.Find("Icon")?.GetComponent<Image>().sprite == IconLibrary.Get("farm")) farm = b;
                    if (b.name == "Close") close = b;
                }
                Check(interactable == 2, $"only the enabled option and close are interactable (got {interactable})");
                Check(farm != null, "farm option button found");
                farm?.onClick.Invoke();
                Check(clicked == 1, "option click invokes OnClick");
                Check(go.GetComponentsInChildren<Text>(true).Length > 0, "captions created");

                close?.onClick.Invoke();
                Check(closed && !menu.IsVisible, "close button hides the menu and raises Closed");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        private static void VerifyRewardCards()
        {
            var go = new GameObject("RewardCards_Test");
            try
            {
                var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                var hud = go.AddComponent<RewardCardHud>();
                hud.Init(font);
                int picked = -1;
                var cards = new List<RewardCard>();
                foreach (var t in CitySystem.RewardOptions(2))
                {
                    var e = MenuIcons.RewardEffect(t);
                    cards.Add(new RewardCard { Icon = MenuIcons.ForReward(t), Name = CitySystem.RewardName(t), EffectIcon = e.Icon, EffectText = e.Text, EffectTint = e.Tint });
                }
                Check(cards.Count == 2, $"Lv2 offers two reward cards (got {cards.Count})");
                hud.Show("테스트 도시", 2, BattleHud.PlayerAccent, cards, i => picked = i);
                Check(hud.IsVisible, "reward modal visible");

                Button second = null;
                int cardButtons = 0;
                foreach (var b in go.GetComponentsInChildren<Button>(true))
                    if (b.name == "Card") { cardButtons++; if (cardButtons == 2) second = b; }
                Check(cardButtons == cards.Count, $"one button per card (got {cardButtons})");
                second?.onClick.Invoke();
                Check(picked == 1 && !hud.IsVisible, "picking a card reports its index and closes the modal");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
