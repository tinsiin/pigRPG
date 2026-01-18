using UnityEngine;

[CreateAssetMenu(menuName = "Walk/Conditions/Not")]
public sealed class NotCondition : ConditionSO
{
    [SerializeField] private ConditionSO condition;

    public override bool IsMet(GameContext context)
    {
        if (condition == null) return true;
        return !condition.IsMet(context);
    }
}
