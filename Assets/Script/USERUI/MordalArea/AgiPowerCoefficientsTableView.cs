using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class AgiPowerCoefficientsTableView : PowerCoefficientsTableViewBase
{
    protected override IReadOnlyDictionary<global::TenDayAbility, float> GetCommonMap()
    {
        return global::AgiPowerConfig.CommonAGI;
    }

    protected override IEnumerable<KeyValuePair<string, IReadOnlyDictionary<global::TenDayAbility, float>>> GetExclusiveGroups()
    {
        // AGI には排他グループは存在しない
        yield break;
    }
}
