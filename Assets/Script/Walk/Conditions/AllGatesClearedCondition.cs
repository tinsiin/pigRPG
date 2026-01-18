using UnityEngine;

[CreateAssetMenu(menuName = "Walk/Conditions/AllGatesCleared")]
public sealed class AllGatesClearedCondition : ConditionSO
{
    public override bool IsMet(GameContext context)
    {
        if (context?.GateResolver == null || context.CurrentNode == null)
            return true;

        return context.GateResolver.AllGatesCleared(context.CurrentNode);
    }
}
