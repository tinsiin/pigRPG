using System.Collections.Generic;

/// <summary>
/// パーティー属性計算のインターフェース。
/// 敵・味方共通で使用可能。
/// </summary>
public interface IPartyPropertyCalculator
{
    /// <summary>
    /// 精神属性のリストからパーティー属性を計算（2人以上用）
    /// </summary>
    PartyProperty CalculateFromImpressions(IReadOnlyList<SpiritualProperty> impressions);

    /// <summary>
    /// 単独メンバーの精神属性からパーティー属性を決定
    /// </summary>
    PartyProperty GetSoloPartyProperty(SpiritualProperty impression);

    /// <summary>
    /// 2つの精神属性の相性値を取得（0-100）
    /// </summary>
    int GetImpressionMatchPercent(SpiritualProperty i, SpiritualProperty you);
}
