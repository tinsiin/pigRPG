using UnityEngine;

[CreateAssetMenu(menuName = "Walk/Conditions/HasTag")]
public sealed class HasTagCondition : ConditionSO, IKeyedCondition
{
    [SerializeField] private string tag;

    public string ConditionKey => tag;
    public ConditionKeyType KeyType => ConditionKeyType.Tag;

    public override bool IsMet(GameContext context)
    {
        if (context == null) return false;
        return context.HasTag(tag);
    }
}
