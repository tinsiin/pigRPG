using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class EyePowerCoefficientsTableView : PowerCoefficientsTableViewBase
{
    protected override IReadOnlyDictionary<global::TenDayAbility, float> GetCommonMap()
    {
        return global::EyePowerConfig.CommonEYE;
    }

    protected override IEnumerable<KeyValuePair<string, IReadOnlyDictionary<global::TenDayAbility, float>>> GetExclusiveGroups()
    {
        // EYE には排他グループは存在しない
        yield break;
    }
}
