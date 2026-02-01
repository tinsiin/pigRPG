using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class WinGrowthStrategy : IGrowthStrategy
{
    public GrowthStrategyType Type => GrowthStrategyType.Win;

    public void Apply(EnemyGrowthContext context)
    {
        var growSkillsSorted = EnemyGrowthUtils.GetGrowSkillsSortedByDistance(
            context.NotEnabledSkills,
            context.GrowTenDays);

        var available = growSkillsSorted.Count;
        if (available == 0) return;

        var growSkillCount = Mathf.Max(1, Mathf.FloorToInt(available * context.Settings.winRate));
        growSkillCount = Mathf.Min(growSkillCount, available);
        var growSkills = growSkillsSorted.GetRange(0, growSkillCount);

        foreach (var growSkill in growSkills)
        {
            var growPoint = 0f;
            foreach (var skillTenDay in growSkill.TenDayValues())
            {
                if (context.GrowTenDays.TryGetValue(skillTenDay.Key, out var growValue))
                {
                    growPoint += skillTenDay.Value * growValue;
                }
            }
            growSkill.growthPoint -= growPoint;
        }
    }
}
