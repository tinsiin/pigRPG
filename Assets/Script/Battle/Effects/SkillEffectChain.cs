using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;

/// <summary>
/// スキル効果をチェーンで実行するクラス。
/// 登録された効果を優先度順に実行する。
/// </summary>
public sealed class SkillEffectChain
{
    private readonly List<ISkillEffect> _effects;

    public SkillEffectChain(IEnumerable<ISkillEffect> effects)
    {
        _effects = effects.OrderBy(e => e.Priority).ToList();
    }

    /// <summary>
    /// 登録された全効果を順次実行
    /// </summary>
    public async UniTask ExecuteAll(SkillEffectContext context)
    {
        foreach (var effect in _effects)
        {
            if (effect.ShouldApply(context))
            {
                await effect.Apply(context);
            }
        }
    }

    /// <summary>
    /// 効果を追加
    /// </summary>
    public void AddEffect(ISkillEffect effect)
    {
        _effects.Add(effect);
        _effects.Sort((a, b) => a.Priority.CompareTo(b.Priority));
    }
}
