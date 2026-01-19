using UnityEngine;

[CreateAssetMenu(menuName = "Walk/Conditions/HasTag")]
public sealed class HasTagCondition : ConditionSO
{
    [SerializeField] private string tag;

    public override bool IsMet(GameContext context)
    {
        if (context == null) return false;
        return context.HasTag(tag);
    }
}
