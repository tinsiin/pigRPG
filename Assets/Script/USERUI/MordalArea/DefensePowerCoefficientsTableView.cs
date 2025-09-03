using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class DefensePowerCoefficientsTableView : PowerCoefficientsTableViewBase
{
    protected override IReadOnlyDictionary<global::TenDayAbility, float> GetCommonMap()
    {
        return global::DefensePowerConfig.CommonDEF;
    }

    protected override IEnumerable<KeyValuePair<string, IReadOnlyDictionary<global::TenDayAbility, float>>> GetExclusiveGroups()
    {
        foreach (var kv in global::DefensePowerConfig.EnumerateExclusiveDEF())
        {
            var label = global::AimStyleExtensions.ToDisplayShortText(kv.Key);
            yield return new KeyValuePair<string, IReadOnlyDictionary<global::TenDayAbility, float>>(label, kv.Value);
        }
    }
}
