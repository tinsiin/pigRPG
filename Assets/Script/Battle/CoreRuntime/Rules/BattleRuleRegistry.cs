using System;
using System.Collections.Generic;

public sealed class BattleRuleRegistry
{
    private readonly Dictionary<string, Func<ITargetingPolicy>> _targetingFactories = new();
    private readonly Dictionary<string, Func<ISkillEffect>> _effectFactories = new();
    private readonly Dictionary<string, Func<ISkillComboRule>> _comboRuleFactories = new();

    public static BattleRuleRegistry CreateDefault()
    {
        var registry = new BattleRuleRegistry();
        registry.RegisterTargetingPolicy(BattleRuleIds.TargetingSingle, () => new SingleCandidateTargetingPolicy());
        registry.RegisterTargetingPolicy(BattleRuleIds.TargetingAll, () => new AllTargetingPolicy());
        registry.RegisterTargetingPolicy(BattleRuleIds.TargetingRandomSingle, () => new RandomSingleTargetingPolicy());
        registry.RegisterTargetingPolicy(BattleRuleIds.TargetingRandomMulti, () => new RandomMultiTargetingPolicy());

        registry.RegisterEffect(BattleRuleIds.EffectFlatRoze, () => new FlatRozeEffect());
        registry.RegisterEffect(BattleRuleIds.EffectHelpRecovery, () => new HelpRecoveryEffect());
        registry.RegisterEffect(BattleRuleIds.EffectRevengeBonus, () => new RevengeBonusEffect());

        return registry;
    }

    public void RegisterTargetingPolicy(string id, Func<ITargetingPolicy> factory)
    {
        if (string.IsNullOrWhiteSpace(id) || factory == null) return;
        _targetingFactories[id] = factory;
    }

    public void RegisterEffect(string id, Func<ISkillEffect> factory)
    {
        if (string.IsNullOrWhiteSpace(id) || factory == null) return;
        _effectFactories[id] = factory;
    }

    public void RegisterComboRule(string id, Func<ISkillComboRule> factory)
    {
        if (string.IsNullOrWhiteSpace(id) || factory == null) return;
        _comboRuleFactories[id] = factory;
    }

    public TargetingPolicyRegistry BuildTargetingPolicies(BattleRuleCatalog catalog)
    {
        if (catalog == null || catalog.TargetingPolicies == null || catalog.TargetingPolicies.Count == 0)
        {
            return TargetingPolicyRegistry.CreateDefault();
        }

        var registry = new TargetingPolicyRegistry();
        var applied = 0;
        for (var i = 0; i < catalog.TargetingPolicies.Count; i++)
        {
            var id = catalog.TargetingPolicies[i];
            if (string.IsNullOrWhiteSpace(id)) continue;
            if (_targetingFactories.TryGetValue(id, out var factory))
            {
                registry.Register(factory());
                applied++;
            }
        }

        return applied > 0 ? registry : TargetingPolicyRegistry.CreateDefault();
    }

    public ISkillEffectPipeline BuildEffectPipeline(BattleRuleCatalog catalog)
    {
        if (catalog == null || catalog.SkillEffects == null || catalog.SkillEffects.Count == 0)
        {
            return SkillEffectPipeline.CreateDefault();
        }

        var effects = new List<ISkillEffect>();
        for (var i = 0; i < catalog.SkillEffects.Count; i++)
        {
            var id = catalog.SkillEffects[i];
            if (string.IsNullOrWhiteSpace(id)) continue;
            if (_effectFactories.TryGetValue(id, out var factory))
            {
                effects.Add(factory());
            }
        }

        var comboRules = new List<ISkillComboRule>();
        if (catalog.ComboRules != null)
        {
            for (var i = 0; i < catalog.ComboRules.Count; i++)
            {
                var id = catalog.ComboRules[i];
                if (string.IsNullOrWhiteSpace(id)) continue;
                if (_comboRuleFactories.TryGetValue(id, out var factory))
                {
                    comboRules.Add(factory());
                }
            }
        }

        if (effects.Count == 0)
        {
            return SkillEffectPipeline.CreateDefault();
        }

        return new SkillEffectPipeline(effects, comboRules);
    }
}
