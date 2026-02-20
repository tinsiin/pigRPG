using System;
using System.Collections.Generic;
using System.Linq;
using RandomExtensions;
using UnityEngine;

/// <summary>
/// パーティー属性計算の共通ロジック（敵・味方共用）。
/// EnemyCollectManager から抽出。ランダム要素あり。
///
/// 使用箇所:
/// - 敵: 常にこのロジックを使用
/// - 味方: 固定メンバー1人以下、または新キャラのみの場合に使用
/// </summary>
[CreateAssetMenu(menuName = "pigRPG/PartyPropertyCalculator")]
public class PartyPropertyCalculatorSO : ScriptableObject, IPartyPropertyCalculator
{
    /// <summary>
    /// EnemyCollectManager のインスタンスへの参照。
    /// 相性値テーブルを EnemyCollectManager から取得するため。
    /// </summary>
    private IEnemyMatchCalculator MatchCalculator => EnemyCollectManager.Instance;

    /// <summary>
    /// 精神属性リストからパーティー属性を決定（ランダム要素あり）
    /// EnemyCollectManager.calculatePartyProperty() と同等のロジック。
    /// </summary>
    public PartyProperty CalculateFromImpressions(IReadOnlyList<SpiritualProperty> impressions)
    {
        if (impressions == null || impressions.Count == 0)
            return PartyProperty.MelaneGroup;

        if (impressions.Count == 1)
            return GetSoloPartyProperty(impressions[0]);

        // 相性値を全ペア取得（インデックスで自己比較のみ除外）
        var matchPercentages = new List<int>();
        for (int i = 0; i < impressions.Count; i++)
        {
            for (int j = 0; j < impressions.Count; j++)
            {
                if (i != j) // 自分自身との比較のみ除外（同属性でも比較する）
                {
                    matchPercentages.Add(GetImpressionMatchPercent(impressions[i], impressions[j]));
                }
            }
        }

        // 空の場合のフォールバック
        if (matchPercentages.Count == 0)
            return PartyProperty.MelaneGroup;

        // 全ての値が70以上なら聖戦
        if (matchPercentages.All(p => p >= 70))
            return PartyProperty.HolyGroup;

        // 全ての値が30以下ならオドラデクス
        if (matchPercentages.All(p => p <= 30))
            return PartyProperty.Odradeks;

        var average = matchPercentages.Average();

        // 平均70以上ならメレーンズ
        if (average >= 70)
            return PartyProperty.MelaneGroup;

        // 標準偏差を計算
        var variance = matchPercentages.Select(x => Math.Pow(x - average, 2)).Average();
        var standardDeviation = Math.Sqrt(variance);

        // 標準偏差20以上ならオドラデクス
        if (standardDeviation >= 20)
            return PartyProperty.Odradeks;

        // 平均57以上なら花樹
        if (average > 57)
            return PartyProperty.Flowerees;

        // ランダム要素（敵と同じロジック）
        if (RandomEx.Shared.NextInt(100) < 67)
        {
            if (RandomEx.Shared.NextInt(100) < 50)
                return PartyProperty.TrashGroup;
            return PartyProperty.MelaneGroup;
        }

        // どの条件にも当てはまらない場合、完全ランダム
        return GetRandomPartyProperty();
    }

    /// <summary>
    /// 単独メンバーのパーティー属性（EnemyLonelyPartyImpressionと同等）
    /// pyscoのみランダム。
    /// </summary>
    public PartyProperty GetSoloPartyProperty(SpiritualProperty impression)
    {
        return impression switch
        {
            SpiritualProperty.Doremis => PartyProperty.Flowerees,
            SpiritualProperty.Pillar => PartyProperty.Odradeks,
            SpiritualProperty.Kindergarten => PartyProperty.TrashGroup,
            SpiritualProperty.LiminalWhiteTile => PartyProperty.MelaneGroup,
            SpiritualProperty.Sacrifaith => PartyProperty.HolyGroup,
            SpiritualProperty.Cquiest => PartyProperty.MelaneGroup,
            SpiritualProperty.Psycho => GetRandomPartyProperty(), // サイコのみランダム
            SpiritualProperty.GodTier => PartyProperty.Flowerees,
            SpiritualProperty.BaleDrival => PartyProperty.TrashGroup,
            SpiritualProperty.Devil => PartyProperty.HolyGroup,
            _ => PartyProperty.MelaneGroup
        };
    }

    /// <summary>
    /// 2つの精神属性の相性値を取得
    /// </summary>
    public int GetImpressionMatchPercent(SpiritualProperty i, SpiritualProperty you)
    {
        if (MatchCalculator != null)
        {
            return MatchCalculator.GetImpressionMatchPercent(i, you);
        }
        // フォールバック: EnemyCollectManager がない場合
        return 50;
    }

    /// <summary>
    /// ランダムなパーティー属性を返す
    /// </summary>
    private PartyProperty GetRandomPartyProperty()
    {
        var values = Enum.GetValues(typeof(PartyProperty));
        return (PartyProperty)values.GetValue(RandomEx.Shared.NextInt(0, values.Length));
    }
}
