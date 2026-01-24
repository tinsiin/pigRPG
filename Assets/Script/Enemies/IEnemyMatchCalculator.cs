using System.Collections.Generic;

/// <summary>
/// 敵の相性計算を抽象化するインターフェース。
/// Phase 3c: EnemyCollectManager.Instanceへの直接依存を解消するための準備。
/// </summary>
public interface IEnemyMatchCalculator
{
    /// <summary>
    /// 一人の敵の属性からパーティー属性への変換辞書
    /// </summary>
    Dictionary<SpiritualProperty, PartyProperty> EnemyLonelyPartyImpression { get; set; }

    /// <summary>
    /// 一人で終わる確率判定
    /// </summary>
    bool LonelyMatchUp(SpiritualProperty impression);

    /// <summary>
    /// 種別同士の敵集まりの相性判定
    /// </summary>
    bool TypeMatchUp(CharacterType a, CharacterType b);

    /// <summary>
    /// 属性同士の敵集まりの相性判定
    /// </summary>
    bool ImpressionMatchUp(SpiritualProperty a, SpiritualProperty b, bool sympathy);

    /// <summary>
    /// 属性同士の相性パーセント取得（0-100の整数）
    /// </summary>
    int GetImpressionMatchPercent(SpiritualProperty a, SpiritualProperty b);

    /// <summary>
    /// パーティー属性を計算
    /// </summary>
    PartyProperty calculatePartyProperty(List<NormalEnemy> list);
}
