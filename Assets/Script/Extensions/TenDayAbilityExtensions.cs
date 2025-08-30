using System;

public static class TenDayAbilityExtensions
{
    /// <summary>
    /// TenDayAbility を表示用テキストへ変換します。
    /// 現状は列挙体名をそのまま返します（後でここを編集して任意の表示名に変更してください）。
    /// 表示を節約するための識別名用途
    /// </summary>
    public static string ToDisplayShortText(this TenDayAbility value)
    {
        switch (value)
        {
            case TenDayAbility.TentVoid: return "テ空";
            case TenDayAbility.NightDarkness: return "夜暗";
            case TenDayAbility.SpringWater: return "泉水";
            case TenDayAbility.StarTersi: return "星テ";
            case TenDayAbility.Raincoat: return "レコ";
            case TenDayAbility.WaterThunderNerve: return "水雷";
            case TenDayAbility.ColdHeartedCalm: return "冷静";
            case TenDayAbility.NightInkKnight: return "夜騎";
            case TenDayAbility.SpringNap: return "春";
            case TenDayAbility.Miza: return "ミザ";
            case TenDayAbility.Baka: return "馬鹿";
            case TenDayAbility.Smiler: return "スマ";
            case TenDayAbility.FaceToHand: return "顔手";
            case TenDayAbility.UnextinguishedPath: return "道";
            case TenDayAbility.Sort: return "ソート";
            case TenDayAbility.HeatHaze: return "陽炎";
            case TenDayAbility.Vail: return "ベル";
            case TenDayAbility.BlazingFire: return "火";
            case TenDayAbility.FlameBreathingWife: return "燃吹";
            case TenDayAbility.Glory: return "威光";
            case TenDayAbility.Rain: return "雨";
            case TenDayAbility.Leisure: return "余裕";
            case TenDayAbility.ElementFaithPower: return "元素";
            case TenDayAbility.CryoniteQuality: return "クリ";
            case TenDayAbility.dokumamusi: return "毒マ";
            case TenDayAbility.JoeTeeth: return "歯";
            case TenDayAbility.Pilmagreatifull: return "ピル";
            case TenDayAbility.Blades: return "刃";
            case TenDayAbility.PersonaDivergence: return "ペル";
            case TenDayAbility.SilentTraining: return "サイ";
            case TenDayAbility.HumanKiller: return "殺";
            case TenDayAbility.Taraiton: return "盥";
            case TenDayAbility.HeavenAndEndWar: return "天";
            case TenDayAbility.Enokunagi: return "エノ";
            case TenDayAbility.Vond: return "ヴォ";
            case TenDayAbility.KereKere: return "ケケ";
            case TenDayAbility.Lucky: return "ラキ";
            default: return value.ToString();
        }
    }

    /// <summary>
    /// 表示テキストから TenDayAbility へ逆変換します。
    /// 現状は Enum 名と一致する場合のみ成功します（表示名を変更した場合はここを調整してください）。
    /// </summary>
    public static bool TryParseDisplayText(string text, out TenDayAbility value)
    {
        return Enum.TryParse(text, out value);
    }
}
