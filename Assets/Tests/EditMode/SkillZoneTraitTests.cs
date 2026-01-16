// TODO: Assembly-CSharpへの参照が解決されたら有効化する
// 現状、asmdefの設定ではAssembly-CSharpを参照できないため一時的に無効化
#if ENABLE_SKILL_ZONE_TRAIT_TESTS
using NUnit.Framework;

/// <summary>
/// SkillZoneTrait関連のユニットテスト
/// </summary>
[TestFixture]
public class SkillZoneTraitTests
{
    #region ビット演算ヘルパーのテスト

    [Test]
    public void Add_ShouldAddTraits()
    {
        var current = SkillZoneTrait.CanSelectSingleTarget;
        var result = current.Add(SkillZoneTrait.CanSelectAlly);

        Assert.IsTrue(result.HasAny(SkillZoneTrait.CanSelectSingleTarget));
        Assert.IsTrue(result.HasAny(SkillZoneTrait.CanSelectAlly));
    }

    [Test]
    public void Remove_ShouldRemoveTraits()
    {
        var current = SkillZoneTrait.CanSelectSingleTarget | SkillZoneTrait.CanSelectAlly;
        var result = current.Remove(SkillZoneTrait.CanSelectAlly);

        Assert.IsTrue(result.HasAny(SkillZoneTrait.CanSelectSingleTarget));
        Assert.IsFalse(result.HasAny(SkillZoneTrait.CanSelectAlly));
    }

    [Test]
    public void KeepOnly_ShouldKeepOnlySpecifiedTraits()
    {
        var current = SkillZoneTrait.CanSelectSingleTarget |
                      SkillZoneTrait.CanSelectAlly |
                      SkillZoneTrait.RandomMultiTarget;

        var result = current.KeepOnly(SkillZoneTraitGroups.SubTraits);

        Assert.IsFalse(result.HasAny(SkillZoneTrait.CanSelectSingleTarget));
        Assert.IsTrue(result.HasAny(SkillZoneTrait.CanSelectAlly));
        Assert.IsFalse(result.HasAny(SkillZoneTrait.RandomMultiTarget));
    }

    [Test]
    public void HasAll_ShouldReturnTrueOnlyWhenAllTraitsPresent()
    {
        var current = SkillZoneTrait.CanSelectSingleTarget | SkillZoneTrait.CanSelectAlly;

        Assert.IsTrue(current.HasAll(SkillZoneTrait.CanSelectSingleTarget));
        Assert.IsTrue(current.HasAll(SkillZoneTrait.CanSelectSingleTarget | SkillZoneTrait.CanSelectAlly));
        Assert.IsFalse(current.HasAll(SkillZoneTrait.CanSelectSingleTarget | SkillZoneTrait.RandomMultiTarget));
    }

    [Test]
    public void HasAny_ShouldReturnTrueWhenAnyTraitPresent()
    {
        var current = SkillZoneTrait.CanSelectSingleTarget;

        Assert.IsTrue(current.HasAny(SkillZoneTrait.CanSelectSingleTarget | SkillZoneTrait.RandomMultiTarget));
        Assert.IsFalse(current.HasAny(SkillZoneTrait.RandomMultiTarget | SkillZoneTrait.AllTarget));
    }

    [Test]
    public void HasNone_ShouldReturnTrueWhenNoTraitPresent()
    {
        var current = SkillZoneTrait.CanSelectSingleTarget;

        Assert.IsTrue(current.HasNone(SkillZoneTrait.RandomMultiTarget));
        Assert.IsFalse(current.HasNone(SkillZoneTrait.CanSelectSingleTarget));
    }

    [Test]
    public void Replace_ShouldReplaceTraits()
    {
        var current = SkillZoneTrait.RandomSelectMultiTarget | SkillZoneTrait.CanSelectAlly;
        var result = current.Replace(
            SkillZoneTrait.RandomSelectMultiTarget,
            SkillZoneTrait.RandomMultiTarget);

        Assert.IsFalse(result.HasAny(SkillZoneTrait.RandomSelectMultiTarget));
        Assert.IsTrue(result.HasAny(SkillZoneTrait.RandomMultiTarget));
        Assert.IsTrue(result.HasAny(SkillZoneTrait.CanSelectAlly));
    }

    #endregion

    #region 正規化のテスト

    [Test]
    public void Normalize_SelfSkill_ShouldReturnOnlySelfSkill()
    {
        var traits = SkillZoneTrait.SelfSkill |
                     SkillZoneTrait.CanSelectSingleTarget |
                     SkillZoneTrait.CanSelectAlly;

        var result = SkillZoneTraitNormalizer.Normalize(traits);

        Assert.AreEqual(SkillZoneTrait.SelfSkill, result);
    }

    [Test]
    public void Normalize_SelectOnlyAlly_ShouldRemoveVanguardBacklineTraits()
    {
        var traits = SkillZoneTrait.SelectOnlyAlly |
                     SkillZoneTrait.CanSelectSingleTarget;

        var result = SkillZoneTraitNormalizer.Normalize(traits);

        Assert.IsTrue(result.HasAny(SkillZoneTrait.SelectOnlyAlly));
        Assert.IsFalse(result.HasAny(SkillZoneTrait.CanSelectSingleTarget));
    }

    [Test]
    public void Normalize_SelectOnlyAlly_WithRandomSelectMultiTarget_ShouldConvertToRandomMultiTarget()
    {
        var traits = SkillZoneTrait.SelectOnlyAlly |
                     SkillZoneTrait.RandomSelectMultiTarget;

        var result = SkillZoneTraitNormalizer.Normalize(traits);

        Assert.IsTrue(result.HasAny(SkillZoneTrait.SelectOnlyAlly));
        Assert.IsFalse(result.HasAny(SkillZoneTrait.RandomSelectMultiTarget));
        Assert.IsTrue(result.HasAny(SkillZoneTrait.RandomMultiTarget));
    }

    [Test]
    public void Normalize_NoConflict_ShouldReturnUnchanged()
    {
        var traits = SkillZoneTrait.CanPerfectSelectSingleTarget |
                     SkillZoneTrait.CanSelectAlly;

        var result = SkillZoneTraitNormalizer.Normalize(traits);

        Assert.AreEqual(traits, result);
    }

    [Test]
    public void NormalizeAfterRandomRange_ShouldRemoveMainSelectAndAddResult()
    {
        var original = SkillZoneTrait.RandomTargetMultiOrSingle |
                       SkillZoneTrait.CanSelectAlly |
                       SkillZoneTrait.CanSelectSingleTarget;
        var randomResult = SkillZoneTrait.RandomMultiTarget;

        var result = SkillZoneTraitNormalizer.NormalizeAfterRandomRange(original, randomResult);

        // ランダム分岐性質が除去されている
        Assert.IsFalse(result.HasAny(SkillZoneTraitGroups.RandomBranchTraits));
        // メイン選択性質が除去されている
        Assert.IsFalse(result.HasAny(SkillZoneTrait.CanSelectSingleTarget));
        // ランダム結果が追加されている
        Assert.IsTrue(result.HasAny(SkillZoneTrait.RandomMultiTarget));
        // サブ性質が残っている
        Assert.IsTrue(result.HasAny(SkillZoneTrait.CanSelectAlly));
    }

    #endregion

    #region 検証のテスト

    [Test]
    public void Validate_ValidTraits_ShouldReturnTrue()
    {
        var traits = SkillZoneTrait.CanPerfectSelectSingleTarget |
                     SkillZoneTrait.CanSelectAlly;

        var isValid = SkillZoneTraitNormalizer.Validate(traits, out var message);

        Assert.IsTrue(isValid);
        Assert.IsNull(message);
    }

    [Test]
    public void Validate_SelectOnlyAllyWithVanguardTrait_ShouldReturnFalse()
    {
        var traits = SkillZoneTrait.SelectOnlyAlly |
                     SkillZoneTrait.CanSelectSingleTarget;

        var isValid = SkillZoneTraitNormalizer.Validate(traits, out var message);

        Assert.IsFalse(isValid);
        Assert.IsNotNull(message);
        Assert.IsTrue(message.Contains("SelectOnlyAlly"));
    }

    [Test]
    public void RequiresNormalization_WithConflict_ShouldReturnTrue()
    {
        var traits = SkillZoneTrait.SelectOnlyAlly |
                     SkillZoneTrait.CanSelectSingleTarget;

        Assert.IsTrue(SkillZoneTraitNormalizer.RequiresNormalization(traits));
    }

    [Test]
    public void RequiresNormalization_WithoutConflict_ShouldReturnFalse()
    {
        var traits = SkillZoneTrait.CanPerfectSelectSingleTarget |
                     SkillZoneTrait.CanSelectAlly;

        Assert.IsFalse(SkillZoneTraitNormalizer.RequiresNormalization(traits));
    }

    #endregion

    #region グループ定数のテスト

    [Test]
    public void SingleTargetTraits_ShouldContainExpectedTraits()
    {
        var expected = SkillZoneTrait.CanPerfectSelectSingleTarget |
                       SkillZoneTrait.CanSelectSingleTarget |
                       SkillZoneTrait.RandomSingleTarget |
                       SkillZoneTrait.ControlByThisSituation;

        Assert.AreEqual(expected, SkillZoneTraitGroups.SingleTargetTraits);
    }

    [Test]
    public void MainSelectTraits_ShouldNotOverlapWithSubTraits()
    {
        var overlap = SkillZoneTraitGroups.MainSelectTraits &
                      SkillZoneTraitGroups.SubTraits;

        Assert.AreEqual((SkillZoneTrait)0, overlap);
    }

    #endregion
}
#endif
