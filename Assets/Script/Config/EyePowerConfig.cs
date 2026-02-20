using System.Collections.Generic;

/// <summary>
/// 視(命中/看破/EYE)に関する係数を一元管理する設定。
/// ・共通加算係数（全プロトコル/スタイル共通）
/// を保持します。
/// </summary>
public static class EyePowerConfig
{
    // 共通EYE係数（BaseStates.b_EYE の TenDayAdd 群を移設）
    private static readonly Dictionary<TenDayAbility, float> s_CommonEYE = new Dictionary<TenDayAbility, float>
    {
        { TenDayAbility.FlameBreathingWife, 0.2f },
        { TenDayAbility.Taraiton, 0.2f },
        { TenDayAbility.Rain, 0.1f },
        { TenDayAbility.FaceToHand, 0.8f },
        { TenDayAbility.Vail, 0.25f },
        { TenDayAbility.StarTersi, 0.6f },
        { TenDayAbility.SpringWater, 0.04f },
        { TenDayAbility.Dokumamusi, 0.1f },
        { TenDayAbility.WaterThunderNerve, 1.0f },
        { TenDayAbility.Leisure, 0.1f },
        { TenDayAbility.PersonaDivergence, 0.02f },
        { TenDayAbility.TentVoid, 0.3f },
        { TenDayAbility.Sort, 0.6f },
        { TenDayAbility.Pilmagreatifull, 0.01f },
        { TenDayAbility.SpringNap, 0.04f },
        { TenDayAbility.ElementFaithPower, 0.001f },
        { TenDayAbility.Miza, 0.5f },
        { TenDayAbility.JoeTeeth, 0.03f },
        { TenDayAbility.ColdHeartedCalm, 0.2f },
        { TenDayAbility.NightInkKnight, 1.0f },
        { TenDayAbility.HumanKiller, 0.2f },
        { TenDayAbility.CryoniteQuality, 0.3f },
        { TenDayAbility.Enokunagi, -0.5f },
    };

    private static readonly IReadOnlyDictionary<TenDayAbility, float> s_ReadOnlyCommonEYE = s_CommonEYE;

    /// <summary>
    /// 共通EYE係数（読み取り専用）
    /// </summary>
    public static IReadOnlyDictionary<TenDayAbility, float> CommonEYE => s_ReadOnlyCommonEYE;
}
