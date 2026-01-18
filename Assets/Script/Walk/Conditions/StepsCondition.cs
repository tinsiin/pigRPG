using UnityEngine;

public enum StepsTarget
{
    GlobalSteps,
    NodeSteps,
    TrackProgress
}

[CreateAssetMenu(menuName = "Walk/Conditions/Steps")]
public sealed class StepsCondition : ConditionSO
{
    [SerializeField] private StepsTarget target = StepsTarget.NodeSteps;
    [SerializeField] private ComparisonOperator comparison = ComparisonOperator.GreaterOrEqual;
    [SerializeField] private int value;

    public override bool IsMet(GameContext context)
    {
        var steps = target switch
        {
            StepsTarget.GlobalSteps => context.Counters.GlobalSteps,
            StepsTarget.NodeSteps => context.Counters.NodeSteps,
            StepsTarget.TrackProgress => context.Counters.TrackProgress,
            _ => 0
        };

        return comparison switch
        {
            ComparisonOperator.Equal => steps == value,
            ComparisonOperator.NotEqual => steps != value,
            ComparisonOperator.GreaterThan => steps > value,
            ComparisonOperator.GreaterOrEqual => steps >= value,
            ComparisonOperator.LessThan => steps < value,
            ComparisonOperator.LessOrEqual => steps <= value,
            _ => true
        };
    }
}
