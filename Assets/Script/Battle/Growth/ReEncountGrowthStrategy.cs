using System.Collections.Generic;
using System.Linq;
using RandomExtensions.Linq;
using UnityEngine;

public sealed class ReEncountGrowthStrategy : IGrowthStrategy
{
    public GrowthStrategyType Type => GrowthStrategyType.ReEncount;

    public void Apply(EnemyGrowthContext context)
    {
        foreach (var unLockSkill in context.NotEnabledSkills)
        {
            if (unLockSkill.growthPoint == 0)
            {
                unLockSkill.growthPoint = -1;
            }
        }

        var growSkills = new List<EnemySkill>(context.NotEnabledSkills);
        growSkills.Shuffle();
        var growSkillCount = Mathf.Max(1, Mathf.CeilToInt(growSkills.Count * context.Settings.reEncountRate));
        growSkills = growSkills.Take(growSkillCount).ToList();

        var growPoint = context.AverageSkillTenDays / context.Settings.reEncountDivisor;
        growPoint *= context.DistanceTraveled;
        foreach (var growSkill in growSkills)
        {
            growSkill.growthPoint -= growPoint;
        }
    }
}
