using System.Collections.Generic;
using static CommonCalc;

public static class EnemyGrowthUtils
{
    public static List<EnemySkill> GetGrowSkillsSortedByDistance(
        IEnumerable<EnemySkill> notEnabledSkills,
        TenDayAbilityDictionary growTenDays)
    {
        var growSkills = new List<EnemySkill>(notEnabledSkills);
        growSkills.Sort((s1, s2) =>
        {
            var distance1 = CalculateTenDaysDistance(s1.TenDayValues(), growTenDays);
            var distance2 = CalculateTenDaysDistance(s2.TenDayValues(), growTenDays);
            return distance1.CompareTo(distance2);
        });
        return growSkills;
    }
}
