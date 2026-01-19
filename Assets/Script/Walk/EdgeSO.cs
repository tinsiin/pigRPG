using UnityEngine;

[CreateAssetMenu(menuName = "Walk/Edge")]
public sealed class EdgeSO : ScriptableObject
{
    [SerializeField] private string fromNodeId;
    [SerializeField] private string toNodeId;
    [SerializeField] private int weight = 1;
    [SerializeField] private ConditionSO[] conditions;

    public string FromNodeId => fromNodeId;
    public string ToNodeId => toNodeId;
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