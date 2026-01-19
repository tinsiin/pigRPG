using UnityEngine;

[CreateAssetMenu(menuName = "Walk/Conditions/HasFlag")]
public sealed class HasFlagCondition : ConditionSO, IKeyedCondition
{
    [SerializeField] private string flagKey;
    [SerializeField] private bool expectedValue = true;

    public string ConditionKey => flagKey;
    public ConditionKeyType KeyType => ConditionKeyType.Flag;

    public override bool IsMet(GameContext context)
    {
        if (string.IsNullOrEmpty(flagKey)) return true;
        var value = context.HasFlag(flagKey);
        return value == expectedValue;
    }
}
