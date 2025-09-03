using System;
using System.Collections.Generic;
using UnityEngine;

[UnityEngine.DisallowMultipleComponent]
public class AttackPowerCoefficientsTableView : PowerCoefficientsTableViewBase
{
    protected override System.Collections.Generic.IReadOnlyDictionary<global::TenDayAbility, float> GetCommonMap()
    {
        return global::AttackPowerConfig.CommonATK;
    }

    protected override System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<string, System.Collections.Generic.IReadOnlyDictionary<global::TenDayAbility, float>>> GetExclusiveGroups()
    {
        foreach (var kv in global::AttackPowerConfig.EnumerateExclusiveATK())
        {
            var label = global::BattleProtocolExtensions.ToDisplayShortText(kv.Key);
            yield return new System.Collections.Generic.KeyValuePair<string, System.Collections.Generic.IReadOnlyDictionary<global::TenDayAbility, float>>(label, kv.Value);
        }
    }
}
