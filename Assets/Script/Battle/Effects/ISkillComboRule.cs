using Cysharp.Threading.Tasks;

/// <summary>
/// コンビ/連携系の派生効果を差し込むためのフック。
/// </summary>
public interface ISkillComboRule
{
    /// <summary>
    /// 優先度（小さいほど先に実行）
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// コンビ効果を適用すべきか判定
    /// </summary>
    bool ShouldApply(SkillEffectContext context);

    /// <summary>
    /// コンビ効果を適用
    /// </summary>
    UniTask Apply(SkillEffectContext context);
}
