using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace TacticsECS
{
    /// <summary>
    /// 모바일 대응용 화면 판정(상태 없음). 세로 화면인지, 휴대폰처럼 물리적으로 작은 화면인지(→ UI를 키운다), 마지막 입력이
    /// 터치였는지를 한곳에서 판단해 ResponsiveCanvas/HUD/BattleController가 같은 기준을 쓰게 한다.
    /// </summary>
    public static class ScreenLayout
    {
        /// <summary>짧은 변 물리 길이가 이 값(인치)보다 작으면 휴대폰으로 보고 UI를 키운다. 태블릿(짧은 변 ~6인치)은 데스크톱과 같게.</summary>
        public const float CompactShortSideInches = 4.2f;

        /// <summary>휴대폰에서 CanvasScaler 기준 해상도에 곱하는 값(작을수록 UI가 커진다). 0.75 → 약 1.33배.
        /// 52px 행동 버튼이 1080p 휴대폰에서 약 9mm(권장 터치 크기)가 되도록 맞춘 값.</summary>
        public const float CompactReferenceScale = 0.75f;

        public static bool IsPortrait => Screen.height > Screen.width;

        public static bool IsCompact
        {
            get
            {
                float shortPx = Mathf.Min(Screen.width, Screen.height);
                if (Screen.dpi > 1f) return shortPx / Screen.dpi < CompactShortSideInches;
                return Application.isMobilePlatform;
            }
        }

        /// <summary>마지막으로 쓴 포인터가 터치스크린인지(툴팁을 손가락을 뗀 뒤에도 잠시 남겨 둘지 등).</summary>
        public static bool IsTouch => Pointer.current is Touchscreen;

        /// <summary>손가락 기준 드래그 판정 거리(픽셀). 160dpi 기준 10px.</summary>
        public static float DragThresholdPixels => Mathf.Max(8f, 10f * (Screen.dpi > 1f ? Screen.dpi / 160f : 1f));

        /// <summary>화면 좌표가 uGUI 위에 있는지. EventSystem.IsPointerOverGameObject()는 터치에서 지난 프레임 값/포인터 id 문제로
        /// 믿을 수 없어서, 그 좌표로 직접 UI 레이캐스트를 한다(마우스/터치 공통).</summary>
        public static bool IsOverUi(Vector2 screenPos)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            var data = new PointerEventData(es) { position = screenPos };
            UiHits.Clear();
            es.RaycastAll(data, UiHits);
            return UiHits.Count > 0;
        }

        /// <summary>IsOverUi가 매 프레임(배치 단계 호버) 새 리스트를 만들지 않도록 재사용하는 버퍼(표시 전용 임시값).</summary>
        private static readonly System.Collections.Generic.List<RaycastResult> UiHits = new System.Collections.Generic.List<RaycastResult>();
    }
}
