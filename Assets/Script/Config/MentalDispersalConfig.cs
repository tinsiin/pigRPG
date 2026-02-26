using System.Collections.Generic;

/// <summary>
/// 精神分散防御に関する係数を一元管理する設定。
/// DEFで防ぎきれなかった物理ダメージの一部を精神HPに分散させるメカニクス。
/// 特定の人間状況×精神属性の組み合わせでのみ発動する。
/// 詳細: doc/精神分散防御仕様書.md
/// </summary>
public static class MentalDispersalConfig
{
    // ── 分散率の重み係数 ──
    // 分散率 = min(1.0, Σ(十日能力 × 重み) / NormalizationDivisor)
    private static readonly Dictionary<TenDayAbility, float> s_DispersalWeights = new Dictionary<TenDayAbility, float>
    {
        { TenDayAbility.Smiler, 2.0f },          // 原動（主役）: いい顔をする＝精神で処理して平気なふり
        { TenDayAbility.Taraiton, 1.4f },         // 中枢基盤（準主役）: 地味な基礎体術が精神回路を支える
        { TenDayAbility.HeatHaze, 1.0f },         // 精神側の補助: 揺らめいて拡散
        { TenDayAbility.ColdHeartedCalm, 1.0f },  // 精神側の補助: 痛みを精神に流す制御力
    };

    private static readonly IReadOnlyDictionary<TenDayAbility, float> s_ReadOnlyDispersalWeights = s_DispersalWeights;

    /// <summary>
    /// 正規化定数。4能力が均等配分（各30、合計120）のとき重み付き合計が162になる。
    /// 162で割ると0〜1.0に収まり、合計120で分散率100%に到達する。
    /// </summary>
    public const float NormalizationDivisor = 162f;

    // ── 精神変換係数 ──
    // 精神変換係数 = max(0, Random(ConversionBaseMin, ConversionBaseMax) - テント空洞 × TentVoidCoefficient)

    /// <summary> 精神変換係数のベース下限（テント空洞なし時） </summary>
    public const float ConversionBaseMin = 0.95f;

    /// <summary> 精神変換係数のベース上限（テント空洞なし時） </summary>
    public const float ConversionBaseMax = 1.0f;

    /// <summary>
    /// テント空洞の精神変換係数への影響。テント空洞100(カンスト)で係数≈0.3（7割が虚無に消える）。
    /// </summary>
    public const float TentVoidCoefficient = 0.00675f;

    // ── 発動条件: 人間状況 ──
    // 精神分散は高揚/混乱/辛いでのみ発動。係数は全て1.0（トリガーのみ、量の差なし）。

    private static readonly HashSet<Demeanor> s_ActiveDemeanors = new HashSet<Demeanor>
    {
        Demeanor.Elated,    // 高揚
        Demeanor.Confused,  // 混乱
        Demeanor.Painful,   // 辛い
    };

    // ── 発動条件: 人間状況×精神属性の対応表 ──
    // SpiritualProperty は [Flags] なのでビットOR結合で保持

    private static readonly Dictionary<Demeanor, SpiritualProperty> s_DemeanorSpiritualMap = new Dictionary<Demeanor, SpiritualProperty>
    {
        // 高揚 × リーミナルホワイトタイル / デビル / ゴッドティア
        { Demeanor.Elated,   SpiritualProperty.LiminalWhiteTile | SpiritualProperty.Devil | SpiritualProperty.GodTier },
        // 混乱 × リーミナルホワイトタイル / キンダーガーデン
        { Demeanor.Confused, SpiritualProperty.LiminalWhiteTile | SpiritualProperty.Kindergarten },
        // 辛い × デビル / ベールドライヴァル
        { Demeanor.Painful,  SpiritualProperty.Devil | SpiritualProperty.BaleDrival },
    };

    // ── 公開API ──

    /// <summary> 分散率の重み係数（読み取り専用） </summary>
    public static IReadOnlyDictionary<TenDayAbility, float> DispersalWeights => s_ReadOnlyDispersalWeights;

    /// <summary>
    /// 指定した人間状況で精神分散が発動するか判定。
    /// </summary>
    public static bool IsActiveDemeanor(Demeanor demeanor)
    {
        return s_ActiveDemeanors.Contains(demeanor);
    }

    /// <summary>
    /// 指定した人間状況と精神属性の組み合わせで精神分散が発動するか判定。
    /// SpiritualProperty は [Flags] なので、キャラの精神属性と対応表のビットANDで判定。
    /// </summary>
    public static bool IsActiveCondition(Demeanor demeanor, SpiritualProperty spiritual)
    {
        if (!s_DemeanorSpiritualMap.TryGetValue(demeanor, out var allowedSpiritual))
            return false;

        return (spiritual & allowedSpiritual) != 0;
    }
}
