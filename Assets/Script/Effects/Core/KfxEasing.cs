using UnityEngine;

namespace Effects.Core
{
    /// <summary>
    /// KFXキーフレーム補間用イージング関数
    /// </summary>
    public static class KfxEasing
    {
        /// <summary>
        /// 名前付きイージングを適用 (t: 0-1 → 0-1)
        /// </summary>
        public static float Apply(string easeName, float t)
        {
            t = Mathf.Clamp01(t);
            return (easeName?.ToLowerInvariant()) switch
            {
                "easein" => t * t,
                "easeout" => t * (2f - t),
                "easeinout" => t < 0.5f ? 2f * t * t : -1f + (4f - 2f * t) * t,
                "step" => 0f,
                _ => t // linear (default)
            };
        }
    }
}
