using System;
using UnityEngine;
using UnityEngine.UI;

namespace TacticsECS
{
    /// <summary>
    /// HUD 캔버스 하나를 모바일 화면에 맞추는 컴포넌트(각 HUD의 Init이 Attach로 붙인다).
    ///  1. 세이프 에어리어: 캔버스 바로 아래에 "SafeArea" RectTransform을 만들고 기존 자식을 전부 그 안으로 옮긴 뒤,
    ///     Screen.safeArea(노치/홈 인디케이터를 뺀 영역)에 맞춰 앵커를 갱신한다 — 모서리에 붙은 버튼이 노치에 가리지 않는다.
    ///  2. CanvasScaler: 세로 화면이면 기준 해상도의 가로/세로를 바꾸고(1280x720 → 720x1280) 폭 기준으로 맞춘다. 휴대폰처럼
    ///     물리적으로 작은 화면이면 기준 해상도를 줄여 UI를 키운다(ScreenLayout.CompactReferenceScale).
    ///  3. 화면 크기/방향/세이프 에어리어가 바뀌면 LayoutChanged(portrait)를 알린다 — HUD가 세로/가로 배치를 바꾼다.
    /// 프리팹 계층은 건드리지 않고 런타임에만 감싸므로, 프리팹을 다시 생성(UIPrefabSetup)할 필요가 없다.
    /// </summary>
    public class ResponsiveCanvas : MonoBehaviour
    {
        /// <summary>세로 화면이면 true. HUD는 이 이벤트로 요소 위치를 다시 잡는다(처음 Attach할 때도 한 번 불린다).</summary>
        public event Action<bool> LayoutChanged;

        public RectTransform SafeArea { get; private set; }
        public bool Portrait { get; private set; }
        public bool Compact { get; private set; }

        /// <summary>현재 캔버스 좌표계 기준 SafeArea 크기(세로 배치 계산용).</summary>
        public Vector2 Size => SafeArea != null ? SafeArea.rect.size : Vector2.zero;

        private CanvasScaler _scaler;
        private Vector2 _baseReference;
        private float _baseMatch;
        private Vector2Int _lastScreen;
        private Rect _lastSafe;

        /// <summary>canvasTransform(Canvas 컴포넌트가 붙은 오브젝트)에 붙이고 자식을 SafeArea로 옮긴다. 이미 붙어 있으면 그대로 돌려준다.</summary>
        public static ResponsiveCanvas Attach(Transform canvasTransform)
        {
            var existing = canvasTransform.GetComponent<ResponsiveCanvas>();
            if (existing != null) return existing;

            var rc = canvasTransform.gameObject.AddComponent<ResponsiveCanvas>();
            rc._scaler = canvasTransform.GetComponent<CanvasScaler>();
            if (rc._scaler != null)
            {
                rc._baseReference = rc._scaler.referenceResolution;
                rc._baseMatch = rc._scaler.matchWidthOrHeight;
            }

            var safe = UiKit.Stretch(UiKit.Rect("SafeArea", canvasTransform));
            for (int i = canvasTransform.childCount - 1; i >= 0; i--)
            {
                var child = canvasTransform.GetChild(i);
                if (child == safe) continue;
                child.SetParent(safe, false);
                child.SetSiblingIndex(0);
            }
            rc.SafeArea = safe;
            rc.Apply(force: true);
            return rc;
        }

        private void Update() => Apply(force: false);

        /// <summary>다음 프레임을 기다리지 않고 지금 상태로 다시 맞춘다(구독 직후 첫 배치용).</summary>
        public void Refresh() => Apply(force: true);

        private void Apply(bool force)
        {
            var screen = new Vector2Int(Screen.width, Screen.height);
            var safeRect = Screen.safeArea;
            if (!force && screen == _lastScreen && safeRect == _lastSafe) return;
            _lastScreen = screen;
            _lastSafe = safeRect;

            Portrait = ScreenLayout.IsPortrait;
            Compact = ScreenLayout.IsCompact;

            if (_scaler != null && _baseReference.x > 0f)
            {
                var reference = Portrait ? new Vector2(_baseReference.y, _baseReference.x) : _baseReference;
                if (Compact) reference *= ScreenLayout.CompactReferenceScale;
                _scaler.referenceResolution = reference;
                // 세로 화면은 폭이 좁은 쪽이라 폭 기준으로 맞춰야 좌우가 잘리지 않는다.
                _scaler.matchWidthOrHeight = Portrait ? 0f : _baseMatch;
            }

            if (SafeArea != null && screen.x > 0 && screen.y > 0)
            {
                SafeArea.anchorMin = new Vector2(safeRect.xMin / screen.x, safeRect.yMin / screen.y);
                SafeArea.anchorMax = new Vector2(safeRect.xMax / screen.x, safeRect.yMax / screen.y);
                SafeArea.offsetMin = SafeArea.offsetMax = Vector2.zero;
            }

            // 스케일러 값이 바뀐 뒤의 캔버스 크기로 배치를 계산하도록 즉시 갱신한다.
            Canvas.ForceUpdateCanvases();
            LayoutChanged?.Invoke(Portrait);
        }
    }
}
