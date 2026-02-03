using Cysharp.Threading.Tasks;

/// <summary>
/// スキル効果のインターフェース。
/// 攻撃後に発動する派生効果を統一的に扱う。
/// </summary>
public interface ISkillEffect
{
    /// <summary>
    /// 効果の優先度（小さいほど先に実行）
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// 効果を適用すべきか判定
    /// </summary>
    bool ShouldApply(SkillEffectContext context);

    /// <summary>
    /// 効果を適用
    /// </summary>
    UniTask Apply(SkillEffectContext context);
}
