using System.Linq;
using UnityEngine;

public sealed class RunOutGrowthStrategy : IGrowthStrategy
{
    public GrowthStrategyType Type => GrowthStrategyType.RunOut;

    public void Apply(EnemyGrowthContext context)
    {
        var growSkillsSorted = EnemyGrowthUtils.GetGrowSkillsSortedByDistance(
            context.NotEnabledSkills,
            context.GrowTenDays);

        var available = growSkillsSorted.Count;
        if (available == 0) return;

        var growSkillCount = Mathf.Max(1, Mathf.FloorToInt(available * context.Settings.runOutRate));
        growSkillCount = Mathf.Min(growSkillCount, available);
        var growSkills = growSkillsSorted.GetRange(0, growSkillCount);

        var growAmount = context.GrowTenDays.Sum(kvp => kvp.Value) / context.Settings.runOutTotalDivisor;
        foreach (var growSkill in growSkills)
        {
            growSkill.growthPoint -= growAmount;
        }
    }
}
