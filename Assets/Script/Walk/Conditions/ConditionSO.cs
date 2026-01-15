using UnityEngine;

public abstract class ConditionSO : ScriptableObject
{
    public abstract bool IsMet(GameContext context);
}