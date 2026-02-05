using System.Linq;
using UnityEngine;

public sealed class AllyRunOutGrowthStrategy : IGrowthStrategy
{
    public GrowthStrategyType Type => GrowthStrategyType.AllyRunOut;

    public void Apply(EnemyGrowthContext context)
    {
        var growSkillsSorted = EnemyGrowthUtils.GetGrowSkillsSortedByDistance(
            context.NotEnabledSkills,
            context.GrowTenDays);

        var available = growSkillsSorted.Count;
        if (available == 0) return;

        var growSkillCount = Mathf.Max(1, Mathf.CeilToInt(available * context.Settings.allyRunOutRate));
        growSkillCount = Mathf.Min(growSkillCount, available);
        var growSkills = growSkillsSorted.GetRange(0, growSkillCount);

        var allGrowTenDays = context.GrowTenDays.Sum(kvp => kvp.Value);
        var growAmount = allGrowTenDays * context.Random.NextFloat(
            context.Settings.allyRunOutMinFactor,
            context.Settings.allyRunOutMaxFactor);

        foreach (var growSkill in growSkills)
        {
            growSkill.growthPoint -= growAmount;
        }
    }
}
