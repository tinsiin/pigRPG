using UnityEngine;

[CreateAssetMenu(menuName = "Walk/Edge")]
public sealed class EdgeSO : ScriptableObject
{
    [SerializeField] private string fromNodeId;
    [SerializeField] private string toNodeId;
    [SerializeField] private int weight = 1;

    public string FromNodeId => fromNodeId;
    public string ToNodeId => toNodeId;
    public int Weight => weight;
}