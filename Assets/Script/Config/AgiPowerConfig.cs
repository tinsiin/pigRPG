using System.Collections.Generic;

/// <summary>
/// 俊敏(AGI)に関する係数を一元管理する設定。
/// ・共通加算係数（全プロトコル/スタイル共通）
/// を保持します。
/// </summary>
public static class AgiPowerConfig
{
    // 共通AGI係数（BaseStates.b_AGI の TenDayAdd 群を移設）
    private static readonly Dictionary<TenDayAbility, float> s_CommonAGI = new Dictionary<TenDayAbility, float>
    {
        { TenDayAbility.FlameBreathingWife, 0.3f },
        { TenDayAbility.Taraiton, 0.3f },
        { TenDayAbility.BlazingFire, 0.9f },
        { TenDayAbility.HeavenAndEndWar, 1.0f },
        { TenDayAbility.FaceToHand, 0.2f },
        { TenDayAbility.Vail, 0.1f },
        { TenDayAbility.Vond, 0.4f },
        { TenDayAbility.HeatHaze, 0.6f },
        { TenDayAbility.WaterThunderNerve, 0.6f },
        { TenDayAbility.PersonaDivergence, 0.2f },
        { TenDayAbility.SilentTraining, 0.02f },
        { TenDayAbility.Pilmagreatifull, 0.2f },
        { TenDayAbility.SpringNap, 0.03f },
        { TenDayAbility.NightDarkness, 0.1f },
        { TenDayAbility.ElementFaithPower, 0.04f },
        { TenDayAbility.ColdHeartedCalm, 0.1f },
        { TenDayAbility.UnextinguishedPath, 0.14f },
        { TenDayAbility.Raincoat, 0.1f },
        { TenDayAbility.Baka, 2.0f },
    };

    private static readonly IReadOnlyDictionary<TenDayAbility, float> s_ReadOnlyCommonAGI = s_CommonAGI;

    /// <summary>
    /// 共通AGI係数（読み取り専用）
    /// </summary>
    public static IReadOnlyDictionary<TenDayAbility, float> CommonAGI => s_ReadOnlyCommonAGI;
}
