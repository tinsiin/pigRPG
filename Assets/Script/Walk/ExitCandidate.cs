using System;
using UnityEngine;

[Serializable]
public sealed class ExitCandidate
{
    [SerializeField] private string id;
    [SerializeField] private string toNodeId;
    [SerializeField] private string uiLabel;
    [SerializeField] private int weight = 1;
    [SerializeField] private ConditionSO[] conditions;

    public string Id => id;
    public string ToNodeId => toNodeId;
    public string UILabel => uiLabel;
    public int Weight => weight;
    public ConditionSO[] Conditions => conditions;

    public bool CheckConditions(GameContext context)
    {
        if (conditions == null || conditions.Length == 0) return true;
        foreach (var condition in conditions)
        {
            if (condition != null && !condition.IsMet(context))
                return false;
        }
        return true;
    }
}