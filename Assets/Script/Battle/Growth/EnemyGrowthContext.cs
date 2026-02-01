using System.Collections.Generic;

public sealed class EnemyGrowthContext
{
    public NormalEnemy Enemy { get; }
    public GrowthSettings Settings { get; }
    public TenDayAbilityDictionary GrowTenDays { get; }
    public List<EnemySkill> NotEnabledSkills { get; }
    public float AverageSkillTenDays { get; }
    public int DistanceTraveled { get; }

    public EnemyGrowthContext(
        NormalEnemy enemy,
        GrowthSettings settings,
        TenDayAbilityDictionary growTenDays,
        List<EnemySkill> notEnabledSkills,
        float averageSkillTenDays,
        int distanceTraveled)
    {
        Enemy = enemy;
        Settings = settings;
        GrowTenDays = growTenDays;
        NotEnabledSkills = notEnabledSkills;
        AverageSkillTenDays = averageSkillTenDays;
        DistanceTraveled = distanceTraveled;
    }
}
