using System.Collections.Generic;

/// <summary>
/// 攻撃力(ATK)に関する係数を一元管理する設定。
/// ・共通加算係数（全プロトコル共通）
/// ・プロトコル排他（プロトコル固有）加算係数
/// を保持します。
/// </summary>
public static class AttackPowerConfig
{
    // 共通ATK係数（BaseStates.b_ATK の共通 TenDayAdd 群を移設）
    private static readonly Dictionary<TenDayAbility, float> s_CommonATK = new Dictionary<TenDayAbility, float>
    {
        { TenDayAbility.FlameBreathingWife, 0.5f },
        { TenDayAbility.BlazingFire, 0.8f },
        { TenDayAbility.HeavenAndEndWar, 0.3f },
        { TenDayAbility.Rain, 0.058f },
        { TenDayAbility.FaceToHand, 0.01f },
        { TenDayAbility.StarTersi, 0.02f },
        { TenDayAbility.Dokumamusi, 0.4f },
        { TenDayAbility.HeatHaze, 0.0666f },
        { TenDayAbility.Leisure, 0.01f },
        { TenDayAbility.SilentTraining, 0.2f },
        { TenDayAbility.Pilmagreatifull, 0.56f },
        { TenDayAbility.NightDarkness, 0.09f },
        { TenDayAbility.NightInkKnight, 0.45f },
        { TenDayAbility.ElementFaithPower, 0.04f },
        { TenDayAbility.JoeTeeth, 0.5f },
        { TenDayAbility.Blades, 1.0f },
        { TenDayAbility.Glory, 0.1f },
        { TenDayAbility.Smiler, 0.02f },
        { TenDayAbility.ColdHeartedCalm, 0.23f },
        { TenDayAbility.Enokunagi, 3.0f },
        { TenDayAbility.Raincoat, 22.0f },
        { TenDayAbility.Baka, -11.0f },
    };

    private static readonly IReadOnlyDictionary<TenDayAbility, float> s_ReadOnlyCommonATK = s_CommonATK;
    private static readonly IReadOnlyDictionary<TenDayAbility, float> s_EmptyReadOnly = new Dictionary<TenDayAbility, float>();

    // プロトコル排他ATK係数（BaseStates.b_ATK の switch 群を移設）
    private static readonly Dictionary<BattleProtocol, Dictionary<TenDayAbility, float>> s_ExclusiveATK
        = new Dictionary<BattleProtocol, Dictionary<TenDayAbility, float>>
    {
        {
            BattleProtocol.LowKey,
            new Dictionary<TenDayAbility, float>
            {
                { TenDayAbility.Taraiton, 0.9f },
                { TenDayAbility.SpringWater, 1.7f },
                { TenDayAbility.HumanKiller, 1.0f },
                { TenDayAbility.UnextinguishedPath, 0.3f },
            }
        },
        {
            BattleProtocol.Tricky,
            new Dictionary<TenDayAbility, float>
            {
                { TenDayAbility.Miza, 1.2f },
                { TenDayAbility.PersonaDivergence, 0.8f },
                { TenDayAbility.Vond, 0.7f },
                { TenDayAbility.Enokunagi, 0.5f },
                { TenDayAbility.Rain, 0.6f },
            }
        },
        {
            BattleProtocol.Showey,
            new Dictionary<TenDayAbility, float>
            {
                { TenDayAbility.Vail, 1.11f },
                { TenDayAbility.WaterThunderNerve, 0.2f },
                { TenDayAbility.HumanKiller, 1.0f },
            }
        },
    };

    /// <summary>
    /// 共通ATK係数（読み取り専用）
    /// </summary>
    public static IReadOnlyDictionary<TenDayAbility, float> CommonATK => s_ReadOnlyCommonATK;

    /// <summary>
    /// 指定プロトコルの排他ATK係数。存在しなければ空辞書を返す。
    /// </summary>
    public static IReadOnlyDictionary<TenDayAbility, float> GetExclusiveATK(BattleProtocol protocol)
    {
        if (s_ExclusiveATK.TryGetValue(protocol, out var dict))
        {
            return dict;
        }
        return s_EmptyReadOnly;
    }

    /// <summary>
    /// すべての排他ATK係数を列挙（プロトコル->辞書）
    /// </summary>
    public static IEnumerable<KeyValuePair<BattleProtocol, IReadOnlyDictionary<TenDayAbility, float>>> EnumerateExclusiveATK()
    {
        foreach (var kv in s_ExclusiveATK)
        {
            yield return new KeyValuePair<BattleProtocol, IReadOnlyDictionary<TenDayAbility, float>>(kv.Key, kv.Value);
        }
    }
}
