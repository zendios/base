using UnityEngine;

namespace Base
{
    /// <summary>
    /// Physical sizes on the running screen: millimetres, dp and canvas units. Uses UnityEngine.Device so the editor's
    /// Device Simulator reports the simulated phone. 48 dp (Android) ≈ 44 pt (iOS) ≈ 7.6 mm is the minimum touch target.
    /// </summary>
    public static class ScreenMetrics
    {
        public const float MinTouchDp = 48f;

        /// <summary>Screen DPI; 160 (Android mdpi baseline) when the platform reports none.</summary>
        public static float Dpi => UnityEngine.Device.Screen.dpi > 0 ? UnityEngine.Device.Screen.dpi : 160f;

        public static bool IsPortrait => UnityEngine.Device.Screen.height >= UnityEngine.Device.Screen.width;

        public static float PixelsPerMm => Dpi / 25.4f;
        public static float MmToPixels(float mm) => mm * PixelsPerMm;
        public static float PixelsToMm(float px) => px / PixelsPerMm;
        public static float DpToPixels(float dp) => dp * Dpi / 160f;

        public static float ShortSideMm => PixelsToMm(Mathf.Min(UnityEngine.Device.Screen.width, UnityEngine.Device.Screen.height));

        /// <summary>Canvas units for a physical size on this canvas (scaleFactor = screen pixels per unit).</summary>
        public static float MmToUnits(float mm, Canvas canvas) => MmToPixels(mm) / Scale(canvas);
        public static float DpToUnits(float dp, Canvas canvas) => DpToPixels(dp) / Scale(canvas);
        public static float UnitsToMm(float units, Canvas canvas) => PixelsToMm(units * Scale(canvas));

        private static float Scale(Canvas canvas) => canvas != null && canvas.rootCanvas.scaleFactor > 0 ? canvas.rootCanvas.scaleFactor : 1f;
    }
}
