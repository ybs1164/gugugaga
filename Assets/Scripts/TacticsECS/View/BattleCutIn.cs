using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TacticsECS
{
    /// <summary>컷인의 한쪽(값만).</summary>
    public struct CutInUnit
    {
        public string Name;
        public Sprite Body;
        public bool Player;
        public int HpBefore, HpAfter, MaxHp;
        /// <summary>서 있는 칸 이름.</summary>
        public string Terrain;
        /// <summary>배경 그림 이름(Resources/Hud/CutIn).</summary>
        public string Background;
        public bool Boat, Big;
    }

    public enum CutInParticle { None, Leaf, Stone, Water, Brick }

    /// <summary>공격 한 번의 연출 재료(값만). 판정은 이미 끝난 결과다.</summary>
    public struct CutInData
    {
        public CutInUnit Attacker, Target;
        public int Damage, Counter;
        public bool Ranged, Charge, Fly, Splash, Guard, Wall, Freeze, Poison, Convert, Combo;
        /// <summary>대상이 살아남았는데 반격하지 못했다.</summary>
        public bool NoCounter;
        /// <summary>대상의 위치 방어 배수(1이면 꼬리표 없음).</summary>
        public float DefenseMultiplier;
        public CutInParticle Particle;
        public List<int> SplashDamages;
    }

    /// <summary>
    /// 전투 컷인: 화면 가운데 띠에 공격자(왼쪽)와 대상(오른쪽)을 같은 배율로 세우고, 각자 서 있는 칸의 배경을 깔고,
    /// 두 유닛의 속성에 따라 연출을 바꾼다. 값만 받아 그린다 — 판정은 모른다. 화면을 누르면 건너뛴다.
    /// 규칙: docs/spec/game/session.md#전투-컷인
    /// </summary>
    public class BattleCutIn : MonoBehaviour
    {
        public const int SortingOrder = 60; // 대화 상자(BattleHud.DialogSortingOrder)보다 앞 — 결과·보상 창은 컷인이 끝난 뒤에 보인다
        /// <summary>띠 높이(UI 단위)와 그림 배율(그림 1픽셀 = Px UI 단위) — 배경과 유닛이 같은 배율이다.</summary>
        public const float BandHeight = 340f, Px = 4f;
        private const float GroundY = 40f, FeetPad = 5f * Px, Slant = 17f, BgWidth = 768f, PlateWidth = 252f, Pad = 12f;
        private static readonly Color Ink = new Color(0.047f, 0.031f, 0.039f);
        private static readonly Color Amber = new Color(0.94f, 0.647f, 0.227f);
        private static readonly Color Danger = new Color(0.85f, 0.227f, 0.29f);
        private static readonly Color Ice = new Color(0.62f, 0.86f, 1f);
        private static readonly Color Venom = new Color(0.72f, 0.5f, 0.95f);

        public bool IsPlaying { get; private set; }

        private RectTransform _root, _safe, _band, _stage, _attacker, _target;
        private RawImage _attackerImage, _targetImage;
        private RectTransform _attackerFill, _targetFill;
        private Text _attackerHp, _targetHp;
        private Image _border;
        private bool _skip;
        /// <summary>띠 배율: 낮은 화면(휴대폰 가로)에서는 그림 1픽셀을 Px보다 한 단계 작게(정수 배율 유지) 그려 위아래 판이 들어가게 한다.</summary>
        private float _scale = 1f;
        private const float ShortScreen = 540f, ShortScale = 0.75f;
        private RectTransform _forecast;
        private float _fitHeight;
        private CutInData _d;
        private float _gap;

        public static BattleCutIn Create(Transform parent)
        {
            var go = new GameObject("BattleCutIn", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            var cutIn = go.AddComponent<BattleCutIn>();
            cutIn._root = (RectTransform)go.transform;
            go.SetActive(false);
            return cutIn;
        }

        /// <summary>연출을 건너뛴다(화면 누름).</summary>
        public void Skip() => _skip = true;

        public void Hide()
        {
            IsPlaying = false;
            Canvas.willRenderCanvases -= FitPlates;
            _attackerPlate = _targetPlate = null;
            if (_root != null) { UiKit.ClearChildren(_root); gameObject.SetActive(false); }
        }

        /// <summary>움직임 없이 맞는 순간의 모습만 세운다(화면 캡처용).</summary>
        public void ShowStill(CutInData data)
        {
            Build(data);
            ApplyImpact();
        }

        public IEnumerator Play(CutInData data)
        {
            Build(data);
            IsPlaying = true;
            _skip = false;
            var d = _d;
            float home = _attacker.anchoredPosition.x, reach = _target.anchoredPosition.x - _gap;
            var lines = d.Charge ? SpeedLines() : null;

            // 들어오기
            yield return Tween(0.12f, t => _band.localScale = new Vector3(_scale, _scale * Mathf.Max(0.05f, t), 1f));
            // 공격: 근접은 달려들고, 원거리는 제자리에서 쏘고, 비행은 위에서 내리꽂는다.
            if (d.Ranged)
            {
                var shot = Dot(_stage, _attacker.anchoredPosition.x + 40f, GroundY + 70f, 16f, 4f, Color.white);
                yield return Tween(0.22f, t => shot.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(home + 40f, _target.anchoredPosition.x - 30f, t), GroundY + 70f));
                Destroy(shot.gameObject);
            }
            else
            {
                float lift = d.Fly ? 96f : 0f;
                yield return Tween(0.2f, t => _attacker.anchoredPosition = new Vector2(Snap(Mathf.Lerp(home, reach, t * t)), GroundY - FeetPad + Snap(lift * (1f - t))));
            }
            if (lines != null) Destroy(lines);

            ApplyImpact();
            if (d.Target.Big || d.Attacker.Big || d.Damage >= 8) yield return Shake();
            yield return Wait(0.5f);

            if (d.Target.HpAfter <= 0 && !d.Convert) yield return Blink(_targetImage);
            if (!d.Ranged) yield return Tween(0.12f, t => _attacker.anchoredPosition = new Vector2(Snap(Mathf.Lerp(reach, home, t)), GroundY - FeetPad));

            if (d.Counter > 0)
            {
                bool heavy = d.Counter * 2 >= d.Attacker.HpBefore;
                if (heavy) { _border.enabled = true; yield return Wait(0.2f); } // 큰 반격: 잠깐 멈추고 띠에 빨간 테두리
                float back = _target.anchoredPosition.x;
                yield return Tween(0.16f, t => _target.anchoredPosition = new Vector2(Snap(Mathf.Lerp(back, home + _gap, t * t)), _target.anchoredPosition.y));
                Number(_attacker.anchoredPosition.x, "-" + d.Counter, d.Counter >= 8, Danger);
                SetHp(_attackerFill, _attackerHp, d.Attacker.HpAfter, d.Attacker.MaxHp);
                Slash(_attacker.anchoredPosition.x, true);
                yield return Wait(0.45f);
                if (d.Attacker.HpAfter <= 0) yield return Blink(_attackerImage);
                yield return Tween(0.12f, t => _target.anchoredPosition = new Vector2(Snap(Mathf.Lerp(home + _gap, back, t)), _target.anchoredPosition.y));
            }
            yield return Wait(0.15f);
            yield return Tween(0.1f, t => _band.localScale = new Vector3(_scale, _scale * Mathf.Max(0.05f, 1f - t), 1f));
            Hide();
        }

        // ---------- 장면 세우기 ----------

        private void Build(CutInData d)
        {
            _d = d;
            gameObject.SetActive(true);
            UiKit.ClearChildren(_root);
            PixelUIScaler.Attach(gameObject);
            _gap = 176f;
            float spread = d.Ranged ? 260f : _gap; // 세로 휴대폰 폭에도 들어가는 간격

            // 바탕: 지도를 어둡게 덮고, 누르면 건너뛴다.
            var dim = Box(_root, "Dim", new Color(0.08f, 0.1f, 0.15f, 0.72f));
            UiKit.Stretch(dim.rectTransform);
            dim.raycastTarget = true;
            var skip = dim.gameObject.AddComponent<Button>();
            skip.transition = Selectable.Transition.None;
            skip.onClick.AddListener(Skip);

            // 띠
            _band = Node(_root, "Band");
            _band.anchorMin = _band.anchorMax = _band.pivot = new Vector2(0.5f, 0.5f);
            _band.sizeDelta = new Vector2(6000f, BandHeight); // 폭은 화면보다 넉넉하게 — 배율(_scale)을 줄여도 양끝까지 찬다
            _band.anchoredPosition = Vector2.zero;
            _band.gameObject.AddComponent<RectMask2D>();
            UiKit.Stretch(Box(_band, "Back", Ink).rectTransform);
            // 오른쪽(대상의 칸) 배경을 먼저 깔고, 왼쪽(공격자의 칸)을 비스듬한 가림막 안에 그린다.
            Background(_band, d.Target.Background, false);
            var mask = Box(_band, "LeftMask", Color.white);
            mask.rectTransform.anchorMin = mask.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            mask.rectTransform.pivot = new Vector2(1f, 0.5f);
            mask.rectTransform.sizeDelta = new Vector2(4000f, BandHeight * 3f);
            mask.rectTransform.anchoredPosition = Vector2.zero;
            mask.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -Slant);
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var upright = Node(mask.rectTransform, "Upright");
            upright.anchorMin = upright.anchorMax = new Vector2(1f, 0.5f);
            upright.pivot = new Vector2(0.5f, 0.5f);
            upright.sizeDelta = new Vector2(0f, BandHeight);
            upright.anchoredPosition = Vector2.zero;
            upright.localRotation = Quaternion.Euler(0f, 0f, Slant);
            Background(upright, d.Attacker.Background, true);
            var seam = Box(mask.rectTransform, "Seam", Ink);
            seam.rectTransform.anchorMin = new Vector2(1f, 0f); seam.rectTransform.anchorMax = new Vector2(1f, 1f);
            seam.rectTransform.pivot = new Vector2(1f, 0.5f);
            seam.rectTransform.sizeDelta = new Vector2(Px, 0f);
            seam.rectTransform.anchoredPosition = Vector2.zero;

            // 무대: 띠 가운데 아래가 원점.
            _stage = Node(_band, "Stage");
            _stage.anchorMin = _stage.anchorMax = new Vector2(0.5f, 0f);
            _stage.pivot = new Vector2(0.5f, 0f);
            _stage.sizeDelta = Vector2.zero;
            _stage.anchoredPosition = Vector2.zero;
            _attacker = Stand(d.Attacker, -spread / 2f, false, out _attackerImage);
            _target = Stand(d.Target, spread / 2f, true, out _targetImage);
            if (d.Fly) _attacker.anchoredPosition += new Vector2(0f, 96f);
            if (d.Attacker.Boat) Waves(_attacker);
            if (d.Target.Boat) Waves(_target);
            if (d.Wall) // 성벽: 대상 앞에 벽이 선다
            {
                var wall = Dot(_stage, _target.anchoredPosition.x - 56f, GroundY - 8f, 24f, 88f, new Color(0.6f, 0.57f, 0.54f));
                wall.rectTransform.pivot = new Vector2(0.5f, 0f);
                Dot(_stage, _target.anchoredPosition.x - 56f, GroundY + 80f, 32f, 8f, new Color(0.81f, 0.78f, 0.75f)).rectTransform.pivot = new Vector2(0.5f, 0f);
            }
            if (d.Guard) Mark(_target.anchoredPosition.x - 52f, GroundY + 56f, "shield");

            _border = Box(_band, "Border", Danger);
            UiKit.Stretch(_border.rectTransform);
            _border.sprite = FrameSprite();
            _border.type = Image.Type.Sliced;
            _border.fillCenter = false;
            _border.enabled = false;

            // 안전 영역 안: 위 예상 결과, 아래 이름판, 오른쪽 위 건너뛰기.
            _safe = Node(_root, PixelUIScaler.SafeAreaName);
            UiKit.Stretch(_safe);
            Forecast(d);
            _attackerPlate = Plate(d.Attacker, PlateWidth, false, null, out _attackerFill, out _attackerHp);
            _targetPlate = Plate(d.Target, PlateWidth, true, DefenseTag(d), out _targetFill, out _targetHp);
            Canvas.willRenderCanvases -= FitPlates; // 화면 폭은 그리기 직전에야 정해진다(에디트 모드 캡처 포함)
            Canvas.willRenderCanvases += FitPlates;
            _scale = 1f; _fitHeight = 0f;
            FitPlates();
            var ff = Box(_safe, "Skip", UiKit.PanelColor);
            UiKit.Place(ff.rectTransform, new Vector2(1f, 1f), new Vector2(-UiKit.Gap, -UiKit.Gap), new Vector2(UiKit.TouchSize, UiKit.TouchSize));
            ff.raycastTarget = false;
            var ffIcon = Box(ff.rectTransform, "Icon", UiKit.TextColor);
            ffIcon.sprite = HudArt.Icon("ff");
            UiKit.Place(ffIcon.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(28f, 28f));
            PixelUIScaler.Attach(gameObject);
        }

        private static string DefenseTag(CutInData d)
        {
            if (d.DefenseMultiplier <= 1f) return null;
            string value = d.DefenseMultiplier.ToString("0.#");
            return d.Wall ? LocalizationSystem.F("UI.CutIn.Wall", value) : LocalizationSystem.F("UI.CutIn.Defense", d.Target.Terrain, value);
        }

        /// <summary>배경 한쪽: 띠 가운데에서 바깥쪽으로 그림 세 장 폭만큼 이어 붙인다(화면 폭을 몰라도 가장자리까지 찬다). 가운데 쪽으로는 비스듬한 경계를 덮을 만큼만 넘는다.</summary>
        private void Background(RectTransform parent, string name, bool left)
        {
            const float overlap = 128f, copies = 3f;
            float width = BgWidth * copies, anchorX = 0.5f;
            var texture = HudArt.Texture("CutIn/" + name) ?? HudArt.Texture("CutIn/field");
            var go = new GameObject("Background", typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<RawImage>();
            image.raycastTarget = false;
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(anchorX, 0.5f);
            rect.pivot = new Vector2(left ? 1f : 0f, 0.5f);
            rect.sizeDelta = new Vector2(width, BandHeight);
            rect.anchoredPosition = new Vector2(left ? overlap : -overlap, 0f);
            if (texture == null) { image.color = new Color(0.45f, 0.6f, 0.4f); return; }
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Repeat;
            image.texture = texture;
            image.uvRect = new Rect(0f, 0f, copies, 1f);
        }

        private RectTransform Stand(CutInUnit unit, float x, bool faceLeft, out RawImage image)
        {
            var go = new GameObject(faceLeft ? "Target" : "Attacker", typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(_stage, false);
            image = go.GetComponent<RawImage>();
            image.raycastTarget = false;
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(x, GroundY - FeetPad);
            if (unit.Body != null)
            {
                var r = unit.Body.textureRect; var t = unit.Body.texture;
                image.texture = t;
                image.uvRect = new Rect(r.x / t.width, r.y / t.height, r.width / t.width, r.height / t.height);
                rect.sizeDelta = r.size * Px;
            }
            else rect.sizeDelta = new Vector2(32f, 40f) * Px;
            if (faceLeft) rect.localScale = new Vector3(-1f, 1f, 1f);
            return rect;
        }

        private void Forecast(CutInData d)
        {
            var pill = Box(_safe, "Forecast", UiKit.PanelColor);
            var damage = Label(pill.rectTransform, d.Damage.ToString(), 24, UiKit.TextColor, true);
            var counter = Label(pill.rectTransform, d.Counter.ToString(), 24, d.Counter > 0 ? Danger : UiKit.MutedText, true);
            float dw = Mathf.Ceil(damage.preferredWidth), cw = Mathf.Ceil(counter.preferredWidth);
            float width = 8f + 28f + 4f + dw + 12f + 2f + 8f + 28f + 4f + cw + 12f;
            var rect = pill.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(width, UiKit.TouchSize);
            rect.anchoredPosition = new Vector2(0f, BandHeight / 2f + UiKit.Gap);
            _forecast = rect;
            float x = 8f;
            PlaceIcon(pill.rectTransform, "sword", x); x += 32f;
            Left(damage.rectTransform, x, dw); x += dw + 12f;
            var line = Box(pill.rectTransform, "Line", UiKit.LineColor);
            UiKit.Place(line.rectTransform, new Vector2(0f, 0.5f), new Vector2(x, 0f), new Vector2(2f, 20f)); x += 10f;
            PlaceIcon(pill.rectTransform, "shield", x); x += 32f;
            Left(counter.rectTransform, x, cw);
        }

        private static void Left(RectTransform rect, float x, float width)
        {
            rect.anchorMin = new Vector2(0f, 0f); rect.anchorMax = new Vector2(0f, 1f); rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(width + 2f, 0f);
            rect.anchoredPosition = new Vector2(x, 0f);
        }

        private static void PlaceIcon(RectTransform parent, string icon, float x)
        {
            var image = Box(parent, icon, UiKit.TextColor);
            image.sprite = HudArt.Icon(icon);
            UiKit.Place(image.rectTransform, new Vector2(0f, 0.5f), new Vector2(x, 0f), new Vector2(28f, 28f));
        }

        private RectTransform _attackerPlate, _targetPlate;

        private void OnDestroy() => Canvas.willRenderCanvases -= FitPlates;

        /// <summary>화면 크기에 맞춘다: 이름판 폭(넓으면 PlateWidth, 세로 휴대폰에서는 화면 절반씩)과 띠 배율(낮은 화면에서는 작게), 그에 따른 위아래 판 자리.</summary>
        private void FitPlates()
        {
            if (_attackerPlate == null || _targetPlate == null || _safe == null || _safe.rect.width < 1f) return;
            float width = Mathf.Min(PlateWidth, (_safe.rect.width - UiKit.Gap * 3f) / 2f);
            float scale = _safe.rect.height < ShortScreen ? ShortScale : 1f;
            if (Mathf.Abs(_attackerPlate.sizeDelta.x - width) < 0.5f && Mathf.Approximately(scale, _scale) && Mathf.Approximately(_fitHeight, _safe.rect.height)) return;
            _fitHeight = _safe.rect.height;
            _attackerPlate.sizeDelta = new Vector2(width, _attackerPlate.sizeDelta.y);
            _targetPlate.sizeDelta = new Vector2(width, _targetPlate.sizeDelta.y);
            float open = _scale > 0f ? _band.localScale.y / _scale : 1f; // 열리고 닫히는 중이면 그 비율을 지킨다
            _scale = scale;
            _band.localScale = new Vector3(scale, scale * open, 1f);
            float half = BandHeight * scale / 2f + UiKit.Gap;
            // 이름판이 화면 아래로 넘치면(낮은 화면) 띠의 아래 모서리에 걸쳐 올린다 — 유닛은 가운데라 가리지 않는다.
            float plateTop = Mathf.Min(half, _safe.rect.height / 2f - UiKit.Gap - _attackerPlate.sizeDelta.y);
            _attackerPlate.anchoredPosition = new Vector2(_attackerPlate.anchoredPosition.x, -plateTop);
            _targetPlate.anchoredPosition = new Vector2(_targetPlate.anchoredPosition.x, -plateTop);
            if (_forecast != null) _forecast.anchoredPosition = new Vector2(0f, half);
        }

        private RectTransform Plate(CutInUnit unit, float width, bool right, string tag, out RectTransform fill, out Text hp)
        {
            var plate = Box(_safe, right ? "TargetPlate" : "AttackerPlate", UiKit.PanelColor);
            var rect = plate.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(right ? 1f : 0f, 0.5f);
            rect.pivot = new Vector2(right ? 1f : 0f, 1f);
            rect.sizeDelta = new Vector2(width, Pad * 2f + 24f + 8f + 24f + 8f + 18f);
            rect.anchoredPosition = new Vector2(right ? -UiKit.Gap : UiKit.Gap, -BandHeight / 2f - UiKit.Gap);
            var team = unit.Player ? UiKit.PlayerColor : UiKit.EnemyColor;

            var name = Label(rect, unit.Name, 20, unit.Player ? UiKit.TextColor : Danger, false);
            UiKit.Row(name.rectTransform, Pad, 24f, Pad, Pad);
            // 체력: 막대(팀 색, 5칸 눈금) + 현재/최대
            hp = Label(rect, string.Empty, 24, UiKit.TextColor, true);
            hp.alignment = TextAnchor.MiddleRight;
            UiKit.Row(hp.rectTransform, Pad + 32f, 24f, Pad, Pad);
            var bar = Box(rect, "Bar", UiKit.LineColor);
            UiKit.Row(bar.rectTransform, Pad + 40f, 8f, Pad, Pad + 76f);
            var fillImage = Box(bar.rectTransform, "Fill", team);
            fill = fillImage.rectTransform;
            fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(1f, 1f); fill.offsetMin = fill.offsetMax = Vector2.zero;
            for (int i = 1; i < UnitView.HpPips; i++)
            {
                var tick = Box(bar.rectTransform, "Tick", UiKit.PanelColor).rectTransform;
                tick.anchorMin = new Vector2((float)i / UnitView.HpPips, 0f); tick.anchorMax = new Vector2((float)i / UnitView.HpPips, 1f);
                tick.pivot = new Vector2(0.5f, 0.5f); tick.sizeDelta = new Vector2(2f, 0f); tick.anchoredPosition = Vector2.zero;
            }
            SetHp(fill, hp, unit.HpBefore, unit.MaxHp);
            var terrain = Label(rect, unit.Terrain, 14, UiKit.MutedText, false);
            UiKit.Row(terrain.rectTransform, Pad + 64f, 18f, Pad, Pad);
            if (string.IsNullOrEmpty(tag)) return rect;
            var chip = Box(rect, "DefenseTag", Amber);
            var chipText = Label(chip.rectTransform, tag, 12, UiKit.TextColor, false);
            chipText.alignment = TextAnchor.MiddleCenter;
            UiKit.Stretch(chipText.rectTransform);
            UiKit.Place(chip.rectTransform, new Vector2(1f, 1f), new Vector2(-Pad, -Pad - 64f), new Vector2(Mathf.Ceil(chipText.preferredWidth) + 12f, 18f));
            return rect;
        }

        private static void SetHp(RectTransform fill, Text text, int hp, int max)
        {
            if (fill == null) return;
            fill.anchorMax = new Vector2(max > 0 ? Mathf.Clamp01((float)hp / max) : 0f, 1f);
            string muted = ColorUtility.ToHtmlStringRGB(UiKit.MutedText);
            text.text = $"{Mathf.Max(0, hp)}<size=12><color=#{muted}>/{max}</color></size>";
        }

        // ---------- 맞는 순간 ----------

        private void ApplyImpact()
        {
            var d = _d;
            float x = _target.anchoredPosition.x;
            Number(x, "-" + d.Damage, d.Damage >= 8, Color.white, d.Guard || d.DefenseMultiplier > 1f);
            SetHp(_targetFill, _targetHp, d.Target.HpAfter, d.Target.MaxHp);
            if (!d.Ranged) Slash(x, false);
            if (d.Particle != CutInParticle.None) Particles(x, d.Particle);
            if (d.Splash)
            {
                Frame(x, GroundY - 16f, 168f, 152f);
                if (d.SplashDamages != null)
                    for (int i = 0; i < d.SplashDamages.Count && i < 4; i++)
                        Small(x + (i % 2 == 0 ? 80f : -80f), GroundY + 110f - 28f * (i / 2), "-" + d.SplashDamages[i]);
            }
            string tag = null;
            if (d.Freeze) { _targetImage.color = Ice; tag = "UI.CutIn.Frozen"; }
            if (d.Poison) { _targetImage.color = Venom; tag = "UI.CutIn.Poisoned"; Drops(x); }
            if (d.Convert) { _targetImage.color = d.Attacker.Player ? UiKit.PlayerColor : UiKit.EnemyColor; tag = "UI.CutIn.Converted"; }
            else if (d.Target.HpAfter <= 0) tag = d.Combo ? "UI.CutIn.Again" : "UI.CutIn.Defeated";
            else if (d.NoCounter && tag == null) tag = "UI.CutIn.NoCounter";
            if (tag != null) Tag(x, GroundY + 216f, LocalizationSystem.T(tag));
        }

        private void Number(float x, string text, bool big, Color color, bool small = false)
        {
            int size = big ? 56 : small ? 28 : 40;
            var label = Label(_stage, text, size, color, false);
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            var shadow = label.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.12f, 0.14f, 0.19f, 1f);
            shadow.effectDistance = new Vector2(0f, -2f);
            UiKit.Place(label.rectTransform, new Vector2(0.5f, 0f), new Vector2(x, GroundY + 148f), new Vector2(160f, 56f)); // 머리 위
            label.rectTransform.pivot = new Vector2(0.5f, 0f);
        }

        private void Small(float x, float y, string text)
        {
            var label = Label(_stage, text, 24, Color.white, false);
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            var shadow = label.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.12f, 0.14f, 0.19f, 1f);
            shadow.effectDistance = new Vector2(0f, -2f);
            UiKit.Place(label.rectTransform, new Vector2(0.5f, 0f), new Vector2(x, y), new Vector2(80f, 32f));
        }

        private void Tag(float x, float y, string text)
        {
            var chip = Box(_stage, "Tag", Amber);
            var label = Label(chip.rectTransform, text, 12, UiKit.TextColor, false);
            label.alignment = TextAnchor.MiddleCenter;
            UiKit.Stretch(label.rectTransform);
            UiKit.Place(chip.rectTransform, new Vector2(0.5f, 0f), new Vector2(x, y), new Vector2(Mathf.Ceil(label.preferredWidth) + 12f, 18f));
        }

        /// <summary>베는 자리: 흰 점 한 줄.</summary>
        private void Slash(float x, bool mirrored)
        {
            for (int i = 0; i < 6; i++)
                Dot(_stage, x + (mirrored ? 1 : -1) * (i - 2.5f) * 16f, GroundY + 24f + i * 16f, 12f, 12f, Color.white);
        }

        private void Particles(float x, CutInParticle kind)
        {
            var color = kind == CutInParticle.Leaf ? new Color(0.61f, 0.85f, 0.43f)
                : kind == CutInParticle.Water ? new Color(0.71f, 0.9f, 0.94f)
                : kind == CutInParticle.Brick ? new Color(0.81f, 0.78f, 0.75f) : new Color(0.6f, 0.57f, 0.54f);
            var offsets = new[] { new Vector2(-44f, 96f), new Vector2(-60f, 60f), new Vector2(-28f, 124f), new Vector2(-72f, 108f), new Vector2(-52f, 136f) };
            foreach (var o in offsets) Dot(_stage, x + o.x, GroundY + o.y, 12f, 12f, color);
        }

        private void Drops(float x)
        {
            for (int i = 0; i < 3; i++) Dot(_stage, x - 24f + i * 24f, GroundY + 150f + (i % 2) * 12f, 12f, 12f, Venom);
        }

        /// <summary>범위(스플래시·짓밟기): 흰 테두리.</summary>
        private void Frame(float x, float y, float width, float height)
        {
            Dot(_stage, x, y, width, Px, Color.white);
            Dot(_stage, x, y + height, width, Px, Color.white);
            Dot(_stage, x - width / 2f, y + height / 2f, Px, height, Color.white);
            Dot(_stage, x + width / 2f, y + height / 2f, Px, height, Color.white);
        }

        private void Mark(float x, float y, string icon)
        {
            var image = Box(_stage, icon, Color.white);
            image.sprite = HudArt.Icon(icon);
            UiKit.Place(image.rectTransform, new Vector2(0.5f, 0f), new Vector2(x, y), new Vector2(28f, 28f));
        }

        /// <summary>배: 발밑 물결 두 줄.</summary>
        private void Waves(RectTransform unit)
        {
            float x = unit.anchoredPosition.x;
            Dot(_stage, x - 12f, GroundY - 4f, 96f, Px, Color.white);
            Dot(_stage, x + 16f, GroundY - 16f, 64f, Px, Color.white);
        }

        /// <summary>돌격: 뒤로 속도선 세 줄.</summary>
        private GameObject SpeedLines()
        {
            var group = Node(_stage, "SpeedLines");
            group.anchorMin = group.anchorMax = new Vector2(0.5f, 0f);
            for (int i = 0; i < 3; i++)
                Dot(group, _attacker.anchoredPosition.x - 96f - i * 12f, GroundY + 36f + i * 28f, 64f - i * 12f, Px, Color.white);
            return group.gameObject;
        }

        // ---------- 그리기·시간 도우미 ----------

        private static float Snap(float v) => Mathf.Round(v / Px) * Px;

        private IEnumerator Wait(float seconds)
        {
            for (float t = 0f; t < seconds && !_skip; t += Time.deltaTime) yield return null;
        }

        private IEnumerator Tween(float seconds, System.Action<float> apply)
        {
            for (float t = 0f; t < seconds && !_skip; t += Time.deltaTime) { apply(t / seconds); yield return null; }
            apply(1f);
        }

        private IEnumerator Shake()
        {
            for (int i = 0; i < 4 && !_skip; i++)
            {
                _stage.anchoredPosition = new Vector2(0f, i % 2 == 0 ? -2f * Px : 0f);
                yield return Wait(0.04f);
            }
            _stage.anchoredPosition = Vector2.zero;
        }

        private IEnumerator Blink(RawImage image)
        {
            for (int i = 0; i < 6 && !_skip; i++)
            {
                image.enabled = i % 2 == 1;
                yield return Wait(0.06f);
            }
            image.enabled = false;
        }

        private static RectTransform Node(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static Image Box(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Image Dot(Transform parent, float x, float y, float width, float height, Color color)
        {
            var image = Box(parent, "Fx", color);
            UiKit.Place(image.rectTransform, new Vector2(0.5f, 0f), new Vector2(x, y), new Vector2(width, height));
            image.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            return image;
        }

        private static Text Label(Transform parent, string text, int size, Color color, bool number)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var label = go.GetComponent<Text>();
            label.font = number && HudArt.Number != null ? HudArt.Number : HudArt.Body;
            if (number) label.fontStyle = FontStyle.Bold;
            label.fontSize = size;
            label.color = color;
            label.alignment = TextAnchor.MiddleLeft;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.supportRichText = true;
            label.raycastTarget = false;
            label.text = text;
            return label;
        }

        private static Sprite _frame;
        /// <summary>가운데가 빈 테두리 그림(9-slice, 두께 4텍셀).</summary>
        private static Sprite FrameSprite()
        {
            if (_frame != null) return _frame;
            var texture = new Texture2D(12, 12, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = "CutInFrame" };
            var pixels = new Color32[144];
            for (int i = 0; i < 144; i++) pixels[i] = new Color32(255, 255, 255, 255);
            texture.SetPixels32(pixels);
            texture.Apply();
            _frame = Sprite.Create(texture, new Rect(0, 0, 12, 12), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(4, 4, 4, 4));
            return _frame;
        }
    }
}
