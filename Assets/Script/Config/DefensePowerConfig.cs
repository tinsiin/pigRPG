using System.Collections.Generic;

/// <summary>
/// 防御力(DEF)に関する係数を一元管理する設定。
/// ・共通加算係数（全スタイル共通）
/// ・AimStyle排他（スタイル固有）加算係数
/// を保持します。
/// </summary>
public static class DefensePowerConfig
{
    // 共通DEF係数（BaseStates.CalcBaseDefenseForAimStyle の共通 TenDayAdd 群を移設）
    private static readonly Dictionary<TenDayAbility, float> s_CommonDEF = new Dictionary<TenDayAbility, float>
    {
        { TenDayAbility.FlameBreathingWife, 1.0f },
        { TenDayAbility.NightInkKnight, 1.3f },
        { TenDayAbility.Raincoat, 1.0f },
        { TenDayAbility.JoeTeeth, 0.8f },
        { TenDayAbility.HeavenAndEndWar, 0.3f },
        { TenDayAbility.Vond, 0.34f },
        { TenDayAbility.HeatHaze, 0.23f },
        { TenDayAbility.Pilmagreatifull, 0.38f },
        { TenDayAbility.Leisure, 0.47f },
        { TenDayAbility.Blades, 0.3f },
        { TenDayAbility.BlazingFire, 0.01f },
        { TenDayAbility.Rain, 0.2f },
        { TenDayAbility.FaceToHand, 0.013f },
        { TenDayAbility.Vail, 0.02f },
        { TenDayAbility.StarTersi, 0.04f },
        { TenDayAbility.SpringWater, 0.035f },
        { TenDayAbility.SilentTraining, 0.09f },
        { TenDayAbility.NightDarkness, 0.01f },
        { TenDayAbility.HumanKiller, 0.07f },
        { TenDayAbility.Baka, -0.1f },
    };

    private static readonly IReadOnlyDictionary<TenDayAbility, float> s_ReadOnlyCommonDEF = s_CommonDEF;
    private static readonly IReadOnlyDictionary<TenDayAbility, float> s_EmptyReadOnly = new Dictionary<TenDayAbility, float>();

    // AimStyle排他DEF係数（BaseStates.CalcBaseDefenseForAimStyle の switch 群を移設）
    private static readonly Dictionary<AimStyle, Dictionary<TenDayAbility, float>> s_ExclusiveDEF
        = new Dictionary<AimStyle, Dictionary<TenDayAbility, float>>
    {
        {
            AimStyle.CentralHeavenStrike, // 中天一弾
            new Dictionary<TenDayAbility, float>
            {
                { TenDayAbility.Smiler, 0.78f },
                { TenDayAbility.CryoniteQuality, 1.0f },
                { TenDayAbility.SilentTraining, 0.4f },
                { TenDayAbility.Vail, 0.5f },
                { TenDayAbility.JoeTeeth, 0.9f },
                { TenDayAbility.ElementFaithPower, 0.3f },
                { TenDayAbility.NightDarkness, 0.1f },
                { TenDayAbility.BlazingFire, 0.6f },
                { TenDayAbility.SpringNap, -0.3f },
            }
        },
        {
            AimStyle.AcrobatMinor, // アクロバマイナ体術1
            new Dictionary<TenDayAbility, float>
            {
                { TenDayAbility.ColdHeartedCalm, 1.0f },
                { TenDayAbility.Taraiton, 0.1f },
                { TenDayAbility.Blades, 1.1f },
                { TenDayAbility.StarTersi, 0.1f },
                { TenDayAbility.NightDarkness, 0.3f },
                { TenDayAbility.WaterThunderNerve, 0.6f },
            }
        },
        {
            AimStyle.Doublet, // ダブレット
            new Dictionary<TenDayAbility, float>
            {
                { TenDayAbility.HeatHaze, 0.7f },
                { TenDayAbility.Sort, 0.3f },
                { TenDayAbility.SpringNap, 0.4f },
                { TenDayAbility.NightInkKnight, 0.3f },
                { TenDayAbility.BlazingFire, 1.0f },
                { TenDayAbility.Vond, 0.2f },
            }
        },
        {
            AimStyle.QuadStrike, // 四弾差し込み
            new Dictionary<TenDayAbility, float>
            {
                { TenDayAbility.SpringNap, 1.0f },
                { TenDayAbility.Rain, 0.2f },
                { TenDayAbility.SpringWater, 0.3f },
                // Vond は元コードで 0.6f と 0.17f の二重加算。辞書集約では 0.77f に合算して管理する
                { TenDayAbility.Vond, 0.77f },
                { TenDayAbility.Enokunagi, 0.5f },
                { TenDayAbility.TentVoid, 0.4f },
                { TenDayAbility.NightDarkness, -0.2f },
                { TenDayAbility.ColdHeartedCalm, -1.0f },
            }
        },
        {
            AimStyle.Duster, // ダスター
            new Dictionary<TenDayAbility, float>
            {
                { TenDayAbility.Miza, 0.6f },
                { TenDayAbility.Glory, 0.8f },
                { TenDayAbility.TentVoid, -0.2f },
                { TenDayAbility.WaterThunderNerve, -0.2f },
                { TenDayAbility.Raincoat, 0.4f },
                { TenDayAbility.Sort, 0.1f },
                { TenDayAbility.SilentTraining, 0.4f },
            }
        },
        {
            AimStyle.PotanuVolf, // ポタヌヴォルフのほうき術
            new Dictionary<TenDayAbility, float>
            {
                { TenDayAbility.Taraiton, 0.4f },
                { TenDayAbility.NightDarkness, 0.2f },
                { TenDayAbility.Pilmagreatifull, 1.4f },
                { TenDayAbility.WaterThunderNerve, 0.2f },
                { TenDayAbility.BlazingFire, -0.2f },
                { TenDayAbility.StarTersi, 0.3f },
                { TenDayAbility.Vond, -0.2f },
            }
        },
    };

    /// <summary>
    /// 共通DEF係数（読み取り専用）
    /// </summary>
    public static IReadOnlyDictionary<TenDayAbility, float> CommonDEF => s_ReadOnlyCommonDEF;

    /// <summary>
    /// 指定AimStyleの排他DEF係数。存在しなければ空辞書を返す。
    /// </summary>
    public static IReadOnlyDictionary<TenDayAbility, float> GetExclusiveDEF(AimStyle style)
    {
        if (s_ExclusiveDEF.TryGetValue(style, out var dict))
        {
            return dict;
        }
        return s_EmptyReadOnly;
    }

    /// <summary>
    /// すべての排他DEF係数を列挙（AimStyle->辞書）
    /// </summary>
    public static IEnumerable<KeyValuePair<AimStyle, IReadOnlyDictionary<TenDayAbility, float>>> EnumerateExclusiveDEF()
    {
        foreach (var kv in s_ExclusiveDEF)
        {
            yield return new KeyValuePair<AimStyle, IReadOnlyDictionary<TenDayAbility, float>>(kv.Key, kv.Value);
        }
    }
}
