using UnityEngine;

[CreateAssetMenu(menuName = "Walk/Conditions/HasFlag")]
public sealed class HasFlagCondition : ConditionSO
{
    [SerializeField] private string flagKey;
    [SerializeField] private bool expectedValue = true;

    public override bool IsMet(GameContext context)
    {
        if (string.IsNullOrEmpty(flagKey)) return true;
        var value = context.HasFlag(flagKey);
        return value == expectedValue;
    }
}
