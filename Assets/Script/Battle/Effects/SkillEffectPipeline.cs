using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

public interface ISkillEffectPipeline
{
    UniTask ExecuteAll(SkillEffectContext context);
    void AddEffect(ISkillEffect effect);
    void AddComboRule(ISkillComboRule rule);
}

/// <summary>
/// 派生効果チェーン + コンビ効果チェーンの実行パイプライン。
/// </summary>
public sealed class SkillEffectPipeline : ISkillEffectPipeline
{
    private readonly SkillEffectChain _effectChain;
    private readonly SkillComboChain _comboChain;

    public SkillEffectPipeline(IEnumerable<ISkillEffect> effects, IEnumerable<ISkillComboRule> comboRules = null)
    {
        _effectChain = new SkillEffectChain(effects ?? Array.Empty<ISkillEffect>());
        _comboChain = new SkillComboChain(comboRules);
    }

    public static SkillEffectPipeline CreateDefault()
    {
        return new SkillEffectPipeline(new ISkillEffect[]
        {
            new FlatRozeEffect(),
            new HelpRecoveryEffect(),
            new RevengeBonusEffect(),
        });
    }

    public async UniTask ExecuteAll(SkillEffectContext context)
    {
        await _effectChain.ExecuteAll(context);
        await _comboChain.ExecuteAll(context);
    }

    public void AddEffect(ISkillEffect effect)
    {
        _effectChain.AddEffect(effect);
    }

    public void AddComboRule(ISkillComboRule rule)
    {
        _comboChain.AddRule(rule);
    }
}
