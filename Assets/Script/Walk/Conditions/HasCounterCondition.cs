using UnityEngine;

public enum ComparisonOperator
{
    Equal,
    NotEqual,
    GreaterThan,
    GreaterOrEqual,
    LessThan,
    LessOrEqual
}

[CreateAssetMenu(menuName = "Walk/Conditions/HasCounter")]
public sealed class HasCounterCondition : ConditionSO
{
    [SerializeField] private string counterKey;
    [SerializeField] private ComparisonOperator comparison = ComparisonOperator.GreaterOrEqual;
    [SerializeField] private int value;

    public override bool IsMet(GameContext context)
    {
        if (string.IsNullOrEmpty(counterKey)) return true;
        var counterValue = context.GetCounter(counterKey);

        return comparison switch
        {
            ComparisonOperator.Equal => counterValue == value,
            ComparisonOperator.NotEqual => counterValue != value,
            ComparisonOperator.GreaterThan => counterValue > value,
            ComparisonOperator.GreaterOrEqual => counterValue >= value,
            ComparisonOperator.LessThan => counterValue < value,
            ComparisonOperator.LessOrEqual => counterValue <= value,
            _ => true
        };
    }
}
