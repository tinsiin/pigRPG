/// <summary>
/// 範囲性質の競合解消を1箇所に集約
/// RangeWillに入る前に必ず正規化する
/// </summary>
public static class SkillZoneTraitNormalizer
{
    /// <summary>
    /// 範囲性質を正規化する。
    /// 競合する性質を解消し、一貫した状態にする。
    /// </summary>
    /// <remarks>
    /// 正規化ルール:
    /// 1. SelfSkillは最優先、他の性質は全て無視
    /// 2. SelectOnlyAllyなら前のめり/後衛系を無効化
    ///    - RandomSelectMultiTarget → RandomMultiTarget に変換
    ///    - CanSelectSingleTarget, CanSelectMultiTarget を除去
    /// </remarks>
    public static SkillZoneTrait Normalize(SkillZoneTrait traits)
    {
        var result = traits;

        // SelfSkillは最優先、他は無視
        if (result.HasAny(SkillZoneTrait.SelfSkill))
        {
            return SkillZoneTrait.SelfSkill;
        }

        // SelectOnlyAllyなら前のめり/後衛系を無効化
        if (result.HasAny(SkillZoneTrait.SelectOnlyAlly))
        {
            // RandomSelectMultiTarget → RandomMultiTarget に変換
            // (前のめり/後衛の選択が不要になるため)
            if (result.HasAny(SkillZoneTrait.RandomSelectMultiTarget))
            {
                result = result.Replace(
                    SkillZoneTrait.RandomSelectMultiTarget,
                    SkillZoneTrait.RandomMultiTarget);
            }

            // CanSelectSingleTarget/CanSelectMultiTarget を除去
            // (前のめり/後衛の選択が味方には適用されないため)
            result = result.Remove(SkillZoneTraitGroups.VanguardBacklineTraits);
        }

        return result;
    }

    /// <summary>
    /// RandomRange処理後の正規化
    /// メイン系を除去してランダム結果を追加する
    /// </summary>
    /// <param name="original">元のRangeWill</param>
    /// <param name="randomResult">ランダム決定された範囲性質</param>
    /// <returns>正規化されたRangeWill</returns>
    public static SkillZoneTrait NormalizeAfterRandomRange(
        SkillZoneTrait original,
        SkillZoneTrait randomResult)
    {
        var result = original;

        // ランダム分岐用性質を除去
        result = result.Remove(SkillZoneTraitGroups.RandomBranchTraits);

        // メイン選択性質を除去
        result = result.Remove(SkillZoneTraitGroups.MainSelectTraits);

        // ランダム結果を追加
        result = result.Add(randomResult);

        // 最終正規化
        return Normalize(result);
    }

    /// <summary>
    /// スキル選択時の初期RangeWill設定用正規化
    /// </summary>
    /// <param name="skillZoneTrait">スキルの範囲性質</param>
    /// <returns>正規化された初期RangeWill</returns>
    public static SkillZoneTrait NormalizeForInitial(SkillZoneTrait skillZoneTrait)
    {
        return Normalize(skillZoneTrait);
    }

    /// <summary>
    /// 正規化が必要かどうかを判定
    /// </summary>
    public static bool RequiresNormalization(SkillZoneTrait traits)
    {
        return Normalize(traits) != traits;
    }

    /// <summary>
    /// 競合がないかを検証
    /// </summary>
    /// <param name="traits">検証対象の性質</param>
    /// <param name="message">競合がある場合の説明メッセージ</param>
    /// <returns>競合がなければtrue</returns>
    public static bool Validate(SkillZoneTrait traits, out string message)
    {
        message = null;

        // SelectOnlyAlly + 前のめり/後衛系の競合チェック
        if (traits.HasAny(SkillZoneTrait.SelectOnlyAlly))
        {
            var conflicts = traits.KeepOnly(SkillZoneTraitGroups.VanguardBacklineTraits);
            if (conflicts != 0)
            {
                message = $"SelectOnlyAllyと前のめり/後衛系({conflicts})は競合します。" +
                          "CanPerfectSelectSingleTargetまたはRandomMultiTargetを使用してください。";
                return false;
            }
        }

        return true;
    }
}
