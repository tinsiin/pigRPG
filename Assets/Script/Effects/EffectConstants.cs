namespace Effects
{
    /// <summary>
    /// エフェクトシステムで使用する定数定義
    /// </summary>
    public static class EffectConstants
    {
        // === FPS ===
        public const int FpsMin = 1;
        public const int FpsMax = 120;
        public const int FpsDefault = 12;

        // === Canvas ===
        public const int CanvasMin = 1;
        public const int CanvasMax = 512;
        public const int CanvasDefault = 100;

        // === Pen ===
        public const int PenWidthDefault = 1;
        public const int PenWidthMin = 1;

        // === Duration ===
        public const float DurationMin = 0.001f;

        // === Paths ===
        public const string EffectsResourcePath = "Effects/";
        public const string AudioResourcePath = "Audio/";

        // === Validation ===
        public const int PolygonMinPoints = 3;
        public const int BezierMinPoints = 3;

        /// <summary>
        /// FPS値をクランプする
        /// </summary>
        public static int ClampFps(int fps)
        {
            if (fps <= 0) return FpsDefault;
            if (fps > FpsMax) return FpsMax;
            return fps;
        }

        /// <summary>
        /// Canvas値を検証する（範囲外はエラー）
        /// </summary>
        public static bool ValidateCanvas(int canvas, out string error)
        {
            if (canvas <= 0)
            {
                error = $"Canvas size must be positive, got {canvas}";
                return false;
            }
            if (canvas > CanvasMax)
            {
                error = $"Canvas size exceeds maximum ({CanvasMax}), got {canvas}";
                return false;
            }
            error = null;
            return true;
        }

        /// <summary>
        /// Duration値を補正する
        /// </summary>
        public static float ClampDuration(float duration, float defaultDuration)
        {
            if (duration <= 0) return defaultDuration;
            return duration;
        }
    }
}
