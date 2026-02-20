using System;

public static class PowerLevelExtensions
{
    /// <summary>
    /// PowerLevel を日本語の表示用テキストへ変換します。
    /// 例: VeryLow -> "たるい"
    /// </summary>
    public static string ToDisplayText(this PowerLevel value)
    {
        switch (value)
        {
            case PowerLevel.VeryLow:
                return "たるい";
            case PowerLevel.Low:
                return "低い";
            case PowerLevel.Medium:
                return "普通";
            case PowerLevel.High:
                return "高い";
            default:
                return value.ToString();
        }
    }

    /// <summary>
    /// 日本語の表示テキストから PowerLevel へ逆変換します。失敗時は false を返します。
    /// </summary>
    public static bool TryParseDisplayText(string text, out PowerLevel value)
    {
        switch (text)
        {
            case "たるい":
                value = PowerLevel.VeryLow; return true;
            case "低い":
                value = PowerLevel.Low; return true;
            case "普通":
                value = PowerLevel.Medium; return true;
            case "高い":
                value = PowerLevel.High; return true;
        }
        value = default;
        return false;
    }
}
