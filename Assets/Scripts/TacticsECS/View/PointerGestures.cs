using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace TacticsECS
{
    /// <summary>
    /// 마우스 왼쪽 버튼과 터치를 한 가지 제스처로 읽는 입력 보조 도구(MonoBehaviour 아님, BattleController가 매 프레임 Update를 부른다).
    /// 한 손가락(또는 왼쪽 버튼)을 거의 움직이지 않고 떼면 탭, 일정 거리 이상 끌면 팬, 두 손가락은 팬 + 핀치 줌이다.
    /// UI 위에서 시작한 누름은 탭·팬을 만들지 않는다 — uGUI 버튼이 받으므로. 판정은 누른 순간에 직접 UI 레이캐스트로 한다
    /// (IsPointerOverGameObject는 터치에서 EventSystem 갱신 순서에 따라 한 프레임 어긋날 수 있다).
    /// </summary>
    public class PointerGestures
    {
        /// <summary>탭으로 인정하는 최대 이동 거리(인치). 화면 DPI를 모르면 DefaultDpi로 환산한다.</summary>
        private const float TapSlopInches = 0.08f;
        private const float DefaultDpi = 160f;

        public bool Tapped { get; private set; }
        public Vector2 TapPosition { get; private set; }
        public Vector2 PanDelta { get; private set; }
        /// <summary>이번 프레임 두 손가락 거리 비(지금/직전). 1보다 크면 벌림(확대).</summary>
        public float PinchRatio { get; private set; } = 1f;
        /// <summary>마우스 커서 또는 첫 손가락 위치(호버 표시용). 둘 다 없으면 false.</summary>
        public bool HasPointer { get; private set; }
        public Vector2 PointerPosition { get; private set; }

        private int _lastCount;     // 직전 프레임에 누르고 있던 손가락 수(마우스 왼쪽 버튼 = 1)
        private bool _overUi;
        private bool _dragging;     // 탭이 아니게 된 누름(끌었거나 두 손가락을 썼음)
        private Vector2 _start;
        private Vector2 _last;
        private float _lastPinchDistance;
        private readonly List<Vector2> _touches = new List<Vector2>(2);
        private static readonly List<RaycastResult> s_uiHits = new List<RaycastResult>();

        public void Update()
        {
            Tapped = false;
            PanDelta = Vector2.zero;
            PinchRatio = 1f;

            _touches.Clear();
            if (Touchscreen.current != null)
                foreach (var touch in Touchscreen.current.touches)
                    if (touch.press.isPressed) _touches.Add(touch.position.ReadValue());

            int count = _touches.Count;
            if (count == 0 && Mouse.current != null && Mouse.current.leftButton.isPressed) count = 1;
            HasPointer = _touches.Count > 0 || Mouse.current != null;
            if (_touches.Count > 0) PointerPosition = _touches[0];
            else if (Mouse.current != null) PointerPosition = Mouse.current.position.ReadValue();

            if (count >= 2)
            {
                var mid = (_touches[0] + _touches[1]) * .5f;
                float distance = Vector2.Distance(_touches[0], _touches[1]);
                if (_lastCount >= 2)
                {
                    PanDelta = mid - _last;
                    if (_lastPinchDistance > 0f && distance > 0f) PinchRatio = distance / _lastPinchDistance;
                }
                else
                {
                    _dragging = true;
                    _overUi = false;
                }
                _last = mid;
                _lastPinchDistance = distance;
                _lastCount = 2;
                return;
            }

            if (count == 1)
            {
                var pos = PointerPosition;
                if (_lastCount == 0)
                {
                    _dragging = false;
                    _overUi = IsOverUi(pos);
                    _start = pos;
                }
                else if (_lastCount >= 2)
                {
                    // 두 손가락 중 하나만 뗐다 — 남은 손가락 위치부터 팬을 이어 간다(이번 프레임은 기준점만 옮긴다).
                }
                else
                {
                    if (!_dragging && (pos - _start).magnitude > TapSlopPixels()) _dragging = true;
                    if (_dragging && !_overUi) PanDelta = pos - _last;
                }
                _last = pos;
                _lastCount = 1;
                return;
            }

            if (_lastCount > 0 && !_dragging && !_overUi)
            {
                Tapped = true;
                TapPosition = _last;
            }
            _lastCount = 0;
            _lastPinchDistance = 0f;
        }

        private static float TapSlopPixels() => TapSlopInches * (Screen.dpi > 0f ? Screen.dpi : DefaultDpi);

        private static bool IsOverUi(Vector2 screenPos)
        {
            if (EventSystem.current == null) return false;
            s_uiHits.Clear();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = screenPos }, s_uiHits);
            return s_uiHits.Count > 0;
        }
    }
}
