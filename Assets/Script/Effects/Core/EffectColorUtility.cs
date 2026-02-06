using System;
using UnityEngine;

namespace Effects.Core
{
    /// <summary>
    /// 色関連のユーティリティ
    /// </summary>
    public static class EffectColorUtility
    {
        /// <summary>
        /// "#RRGGBB" または "#RRGGBBAA" 形式の文字列をColorに変換
        /// </summary>
        public static Color ParseColor(string hex)
        {
            if (string.IsNullOrEmpty(hex))
                return Color.white;

            // #を除去
            hex = hex.TrimStart('#');

            if (hex.Length < 6)
            {
                Debug.LogWarning($"[EffectColorUtility] Invalid color format: {hex}");
                return Color.white;
            }

            try
            {
                byte r = Convert.ToByte(hex.Substring(0, 2), 16);
                byte g = Convert.ToByte(hex.Substring(2, 2), 16);
                byte b = Convert.ToByte(hex.Substring(4, 2), 16);
                byte a = hex.Length >= 8
                    ? Convert.ToByte(hex.Substring(6, 2), 16)
                    : (byte)255;

                return new Color32(r, g, b, a);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[EffectColorUtility] Failed to parse color '{hex}': {e.Message}");
                return Color.white;
            }
        }

        /// <summary>
        /// Color を "#RRGGBBAA" 形式の文字列に変換
        /// </summary>
        public static string ColorToHex(Color color)
        {
            Color32 c32 = color;
            return $"#{c32.r:X2}{c32.g:X2}{c32.b:X2}{c32.a:X2}";
        }

        /// <summary>
        /// PenDefinitionからColorを取得
        /// </summary>
        public static Color? GetPenColor(PenDefinition pen)
        {
            if (pen == null || string.IsNullOrEmpty(pen.Color))
                return null;
            return ParseColor(pen.Color);
        }

        /// <summary>
        /// BrushDefinitionからBrushContextを生成
        /// </summary>
        public static BrushContext? CreateBrushContext(BrushDefinition brush, float cx, float cy, float boundsSize)
        {
            if (brush == null) return null;

            string type = brush.Type?.ToLowerInvariant();

            switch (type)
            {
                case "radial":
                    if (string.IsNullOrEmpty(brush.Center) || string.IsNullOrEmpty(brush.Edge))
                        return null;
                    return BrushContext.Radial(
                        ParseColor(brush.Center),
                        ParseColor(brush.Edge),
                        cx, cy, boundsSize
                    );

                case "linear":
                    if (string.IsNullOrEmpty(brush.Start) || string.IsNullOrEmpty(brush.End))
                        return null;
                    return BrushContext.Linear(
                        ParseColor(brush.Start),
                        ParseColor(brush.End),
                        cx, cy, boundsSize,
                        brush.Angle
                    );

                default:
                    // flat or unspecified → single color
                    if (string.IsNullOrEmpty(brush.Color))
                        return null;
                    return BrushContext.Flat(ParseColor(brush.Color));
            }
        }

        /// <summary>
        /// アルファブレンディング（SrcOver）
        /// </summary>
        public static Color BlendPixel(Color dst, Color src)
        {
            float srcA = src.a;
            float dstA = dst.a * (1f - srcA);
            float outA = srcA + dstA;

            if (outA < 0.001f)
                return Color.clear;

            return new Color(
                (src.r * srcA + dst.r * dstA) / outA,
                (src.g * srcA + dst.g * dstA) / outA,
                (src.b * srcA + dst.b * dstA) / outA,
                outA
            );
        }

        /// <summary>
        /// 加算ブレンディング（Additive）
        /// </summary>
        public static Color BlendPixelAdditive(Color dst, Color src)
        {
            return new Color(
                Mathf.Min(1f, dst.r + src.r * src.a),
                Mathf.Min(1f, dst.g + src.g * src.a),
                Mathf.Min(1f, dst.b + src.b * src.a),
                Mathf.Min(1f, dst.a + src.a)
            );
        }
    }
}
