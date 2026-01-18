using RandomExtensions;
using UnityEngine;

[CreateAssetMenu(menuName = "Walk/Conditions/Chance")]
public sealed class ChanceCondition : ConditionSO
{
    [SerializeField, Range(0f, 1f)] private float probability = 0.5f;

    public override bool IsMet(GameContext context)
    {
        return RandomEx.Shared.NextFloat(0f, 1f) < probability;
    }
}
