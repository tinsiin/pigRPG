#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// スキルのTrait整合性をチェックするエディタツール
/// 破綻した組み合わせを事前検出する
/// </summary>
public static class SkillTraitValidator
{
    /// <summary>
    /// 単一のSkillZoneTraitを検証する
    /// EditorメニューからSkillZoneTrait値を直接入力して検証できる
    /// </summary>
    [MenuItem("Tools/pigRPG/Validate SkillZoneTrait Value")]
    public static void ValidateTraitValueMenu()
    {
        // サンプルとしていくつかの組み合わせを検証
        var testCases = new (SkillZoneTrait traits, string name)[]
        {
            (SkillZoneTrait.CanPerfectSelectSingleTarget | SkillZoneTrait.CanSelectAlly, "正常: 完全単体+味方選択可"),
            (SkillZoneTrait.SelectOnlyAlly | SkillZoneTrait.CanSelectSingleTarget, "競合: SelectOnlyAlly+前のめり選択"),
            (SkillZoneTrait.SelfSkill | SkillZoneTrait.AllTarget, "競合: SelfSkill+全体攻撃"),
            (SkillZoneTrait.SelectOnlyAlly | SkillZoneTrait.RandomSelectMultiTarget, "変換: SelectOnlyAlly+RandomSelectMulti"),
        };

        var issues = new List<string>();
        foreach (var (traits, name) in testCases)
        {
            var result = ValidateSkillTrait(traits, name);
            if (!result.IsValid)
            {
                issues.Add(result.Message);
            }
        }

        if (issues.Count > 0)
        {
            Debug.LogWarning($"Trait検証: {issues.Count}件の検出\n" + string.Join("\n", issues));
        }
        else
        {
            Debug.Log("Trait検証: テストケース全てが正常に正規化可能です");
        }

        EditorUtility.DisplayDialog(
            "Trait検証結果",
            $"{testCases.Length}件中{issues.Count}件が正規化を必要とします。\n詳細はConsoleを確認してください。",
            "OK");
    }

    /// <summary>
    /// 単一スキルの検証
    /// </summary>
    public static ValidationResult ValidateSkill(BaseSkill skill, string assetName = null)
    {
        if (skill == null)
        {
            return new ValidationResult(false, "スキルがnullです");
        }

        return ValidateSkillTrait(skill.ZoneTrait, assetName ?? skill.SkillName ?? "Unknown");
    }

    /// <summary>
    /// SkillZoneTraitの検証
    /// </summary>
    public static ValidationResult ValidateSkillTrait(SkillZoneTrait traits, string name)
    {
        // SkillZoneTraitNormalizerの検証を使用
        if (!SkillZoneTraitNormalizer.Validate(traits, out var message))
        {
            return new ValidationResult(false, $"[{name}] {message}");
        }

        // 正規化前後で変化があれば警告
        var normalized = SkillZoneTraitNormalizer.Normalize(traits);
        if (normalized != traits)
        {
            return new ValidationResult(false,
                $"[{name}] 正規化により変更されます: {traits} → {normalized}");
        }

        // 追加の検証: SelfSkillと他の範囲性質の組み合わせ
        if (traits.HasAny(SkillZoneTrait.SelfSkill))
        {
            var otherRangeTraits = traits.Remove(SkillZoneTrait.SelfSkill);
            if (otherRangeTraits.HasAny(SkillZoneTraitGroups.MainSelectTraits))
            {
                return new ValidationResult(false,
                    $"[{name}] SelfSkillと他の範囲性質({otherRangeTraits})が同居しています。" +
                    "SelfSkillのみにするか、他の範囲性質を使用してください。");
            }
        }

        // 追加の検証: 範囲性質が全くない場合
        if (traits == 0)
        {
            return new ValidationResult(false,
                $"[{name}] 範囲性質(ZoneTrait)が設定されていません。");
        }

        return new ValidationResult(true, "");
    }

    /// <summary>
    /// 検証結果
    /// </summary>
    public readonly struct ValidationResult
    {
        public readonly bool IsValid;
        public readonly string Message;

        public ValidationResult(bool isValid, string message)
        {
            IsValid = isValid;
            Message = message;
        }
    }
}
#endif
