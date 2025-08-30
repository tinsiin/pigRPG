using System;

public static class AimStyleExtensions
{
    public static string ToDisplayText(this AimStyle style)
    {
        switch (style)
        {
            case AimStyle.AcrobatMinor: return "アクロバマイナ";
            case AimStyle.Doublet: return "ダブレット";
            case AimStyle.QuadStrike: return "四弾差し込み";
            case AimStyle.Duster: return "ダスター";
            case AimStyle.PotanuVolf: return "ポタヌヴォルフ";
            case AimStyle.CentralHeavenStrike: return "中天一弾";
            case AimStyle.none: return "なし";
            default: return style.ToString();
        }
    }

    public static string ToDisplayShortText(this AimStyle style)
    {
        switch (style)
        {
            case AimStyle.AcrobatMinor: return "アクロ";
            case AimStyle.Doublet: return "ダブ";
            case AimStyle.QuadStrike: return "四弾";
            case AimStyle.Duster: return "ダス";
            case AimStyle.PotanuVolf: return "ポタヌ";
            case AimStyle.CentralHeavenStrike: return "中天";
            case AimStyle.none: return "無";
            default: return style.ToString();
        }
    }
}
