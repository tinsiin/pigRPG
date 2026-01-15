using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Walk/FlowGraph")]
public sealed class FlowGraphSO : ScriptableObject
{
    [SerializeField] private NodeSO[] nodes;
    [SerializeField] private EdgeSO[] edges;
    [SerializeField] private string startNodeId;

    public IReadOnlyList<NodeSO> Nodes => nodes;
    public IReadOnlyList<EdgeSO> Edges => edges;
    public string StartNodeId => startNodeId;

    public bool TryGetNode(string nodeId, out NodeSO node)
    {
        node = null;
        if (string.IsNullOrEmpty(nodeId) || nodes == null) return false;
        for (var i = 0; i < nodes.Length; i++)
        {
            var candidate = nodes[i];
            if (candidate == null) continue;
            if (candidate.NodeId == nodeId)
            {
                node = candidate;
                return true;
            }
        }
        return false;
    }
}