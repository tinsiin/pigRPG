using System;

public static class ThePowerExtensions
{
    /// <summary>
    /// ThePower を日本語の表示用テキストへ変換します。
    /// 例: lowlow -> "たるい"
    /// </summary>
    public static string ToDisplayText(this ThePower value)
    {
        switch (value)
        {
            case ThePower.lowlow:
                return "たるい";
            case ThePower.low:
                return "低い";
            case ThePower.medium:
                return "普通";
            case ThePower.high:
                return "高い";
            default:
                return value.ToString();
        }
    }

    /// <summary>
    /// 日本語の表示テキストから ThePower へ逆変換します。失敗時は false を返します。
    /// </summary>
    public static bool TryParseDisplayText(string text, out ThePower value)
    {
        switch (text)
        {
            case "たるい":
                value = ThePower.lowlow; return true;
            case "低い":
                value = ThePower.low; return true;
            case "普通":
                value = ThePower.medium; return true;
            case "高い":
                value = ThePower.high; return true;
        }
        value = default;
        return false;
    }
}
