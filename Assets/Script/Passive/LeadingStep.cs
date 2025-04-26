using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// リードステップ 回避上昇用のムーヴィング支援敵側面 機能で分ける為の意味合いでの汎用パッシブ
/// </summary>
public class LeadingStep : BasePassive
{
    public override float AGIFixedValueEffect()
    {
        return base.AGIFixedValueEffect() * Mathf.Max(1,PassivePower * 1.2f);
    }
    public override float AGIPercentageModifier()
    {
        return base.AGIPercentageModifier() * Mathf.Max(1,1f + PassivePower * 0.06f);
    }
}
