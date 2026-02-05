using System;
using System.Collections.Generic;

/// <summary>
/// Data-driven rule catalog (IDs only).
/// JsonUtility-friendly: public fields only.
/// </summary>
[Serializable]
public sealed class BattleRuleCatalog
{
    public List<string> TargetingPolicies = new();
    public List<string> SkillEffects = new();
    public List<string> ComboRules = new();

    public static BattleRuleCatalog CreateDefault()
    {
        return new BattleRuleCatalog
        {
            TargetingPolicies = new List<string>
            {
                BattleRuleIds.TargetingSingle,
                BattleRuleIds.TargetingAll,
                BattleRuleIds.TargetingRandomSingle,
                BattleRuleIds.TargetingRandomMulti
            },
            SkillEffects = new List<string>
            {
                BattleRuleIds.EffectFlatRoze,
                BattleRuleIds.EffectHelpRecovery,
                BattleRuleIds.EffectRevengeBonus
            }
        };
    }
}
