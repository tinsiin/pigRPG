using UnityEngine;

[CreateAssetMenu(menuName = "Walk/Conditions/And")]
public sealed class AndCondition : ConditionSO
{
    [SerializeField] private ConditionSO[] conditions;

    public override bool IsMet(GameContext context)
    {
        if (conditions == null || conditions.Length == 0) return true;

        foreach (var condition in conditions)
        {
            if (condition == null) continue;
            if (!condition.IsMet(context)) return false;
        }

        return true;
    }
}
