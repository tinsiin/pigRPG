using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;

/// <summary>
/// コンビ/連携系の効果をチェーンで実行するクラス。
/// </summary>
public sealed class SkillComboChain
{
    private readonly List<ISkillComboRule> _rules;

    public SkillComboChain(IEnumerable<ISkillComboRule> rules)
    {
        _rules = rules?.OrderBy(rule => rule.Priority).ToList() ?? new List<ISkillComboRule>();
    }

    public async UniTask ExecuteAll(SkillEffectContext context)
    {
        foreach (var rule in _rules)
        {
            if (rule.ShouldApply(context))
            {
                await rule.Apply(context);
            }
        }
    }

    public void AddRule(ISkillComboRule rule)
    {
        if (rule == null) return;
        _rules.Add(rule);
        _rules.Sort((a, b) => a.Priority.CompareTo(b.Priority));
    }
}
