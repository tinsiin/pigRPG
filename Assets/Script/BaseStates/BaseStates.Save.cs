using System.Collections.Generic;

public abstract partial class BaseStates
{
    internal Dictionary<SkillImpression, float> ExportPersistentAdaptationMemories()
    {
        var result = new Dictionary<SkillImpression, float>();
        if (PersistentAdaptSkillImpressionMemories == null) return result;
        foreach (var kv in PersistentAdaptSkillImpressionMemories)
        {
            result[kv.Key] = kv.Value;
        }
        return result;
    }

    internal void ImportPersistentAdaptationMemories(Dictionary<SkillImpression, float> source)
    {
        if (PersistentAdaptSkillImpressionMemories == null)
        {
            PersistentAdaptSkillImpressionMemories = new Dictionary<SkillImpression, float>();
        }
        else
        {
            PersistentAdaptSkillImpressionMemories.Clear();
        }

        if (source == null) return;
        foreach (var kv in source)
        {
            PersistentAdaptSkillImpressionMemories[kv.Key] = kv.Value;
        }
    }

    internal void SetImpressions(SpiritualProperty current, SpiritualProperty defaultImpression)
    {
        _myImpression = current;
        DefaultImpression = defaultImpression;
    }

    internal void ImportBaseTenDayValues(TenDayAbilityDictionary values)
    {
        BaseTenDayValues.Clear();
        if (values == null) return;
        foreach (var kv in values)
        {
            BaseTenDayValues[kv.Key] = kv.Value;
        }
    }
}
