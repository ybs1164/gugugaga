using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 모든 HUD 캔버스가 함께 쓰는 배율 규칙(PC·모바일 공용). UI 1단위가 화면 몇 픽셀인지(ScaleFactor)를
    /// "1280x720 화면에 맞춘 배율"과 "실제 크기 배율(1단위 ≈ 1dp = 1/160인치)" 중 큰 값으로 정한다 —
    /// PC 모니터는 앞의 값(1080p = 1.5, 예전 CanvasScaler와 같다), 휴대폰은 뒤의 값이 이겨서 버튼이 손가락 크기가 된다.
    /// 그래서 논리 캔버스는 PC 1280x720, 휴대폰 가로 약 900x400이 되고, 레이아웃은 그 둘을 모두 견디도록 짠다(BattleHud).
    /// 에디터 캡처는 모니터 dpi로 계산되므로 DpiOverride·SafeAreaOverride로 휴대폰을 흉내 낸다(MobileCapture).
    /// </summary>
    public static class UiScale
    {
        /// <summary>논리 해상도 기준(PC). 이 크기 화면에서 배율 1.</summary>
        public static readonly Vector2 Reference = new Vector2(1280f, 720f);

        /// <summary>dpi를 모를 때(Screen.dpi = 0) 쓰는 값 — 데스크톱 모니터 기준.</summary>
        private const float FallbackDpi = 96f;

        /// <summary>안드로이드 dp 기준 dpi. UI 1단위 = 1dp.</summary>
        private const float DpBaseDpi = 160f;

        /// <summary>도트 그림 1텍셀이 UI 몇 단위인지(1280x720 기준 화면 높이 540 = 텍셀 1). 예전 규칙(화면 높이/540)과 같은 값을 배율에서 구한다.</summary>
        private const float TexelPerScale = 720f / 540f;

        /// <summary>캡처·검증용 dpi 덮어쓰기(null이면 Screen.dpi).</summary>
        public static float? DpiOverride;

        /// <summary>캡처·검증용 안전 영역 덮어쓰기(화면 비율 0..1, null이면 Screen.safeArea).</summary>
        public static Rect? SafeAreaOverride;

        public static float Dpi => DpiOverride ?? (Screen.dpi > 0f ? Screen.dpi : FallbackDpi);

        public static float ScaleFactor(float pixelWidth, float pixelHeight)
        {
            float fit = Mathf.Sqrt(pixelWidth / Reference.x * (pixelHeight / Reference.y));
            float physical = Dpi / DpBaseDpi;
            return Mathf.Max(0.5f, fit, physical);
        }

        /// <summary>도트 그림 1텍셀을 몇 화면 픽셀로 그릴지(정수) — 배율이 커지면 프레임·아이콘도 같이 커진다.</summary>
        public static int TexelSize(float scaleFactor) => Mathf.Max(1, Mathf.RoundToInt(scaleFactor * TexelPerScale));

        /// <summary>안전 영역(노치·제스처 바 제외)을 화면 비율(0..1)로. 캔버스가 화면 전체가 아닐 때(캡처의 RenderTexture)도 같은 비율을 쓴다.</summary>
        public static Rect SafeAreaNormalized()
        {
            if (SafeAreaOverride.HasValue) return SafeAreaOverride.Value;
            if (Screen.width <= 0 || Screen.height <= 0) return new Rect(0f, 0f, 1f, 1f);
            var s = Screen.safeArea;
            return new Rect(s.x / Screen.width, s.y / Screen.height, s.width / Screen.width, s.height / Screen.height);
        }

        /// <summary>지도 한 칸이 화면에서 이 크기(인치) 이상이 되도록 시작 줌을 고른다(손가락으로 집을 수 있는 크기 ≈ 8mm).</summary>
        public const float MinTileInches = 0.32f;
    }
}
