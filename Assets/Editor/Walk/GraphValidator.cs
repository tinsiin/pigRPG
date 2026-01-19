using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public sealed class GraphValidator
{
    public struct ValidationResult
    {
        public List<string> Errors;
        public List<string> Warnings;
        public bool IsValid => Errors.Count == 0;

        public ValidationResult(bool _)
        {
            Errors = new List<string>();
            Warnings = new List<string>();
        }
    }

    public ValidationResult Validate(FlowGraphSO graph)
    {
        var result = new ValidationResult(true);

        if (graph == null)
        {
            result.Errors.Add("Graph is null.");
            return result;
        }

        var nodes = graph.Nodes;
        if (nodes == null || nodes.Count == 0)
        {
            result.Errors.Add("Graph has no nodes.");
            return result;
        }

        // Build node ID set
        var nodeIds = new HashSet<string>();
        foreach (var node in nodes)
        {
            if (node == null) continue;
            if (string.IsNullOrEmpty(node.NodeId))
            {
                result.Errors.Add($"Node '{node.name}' has empty NodeId.");
                continue;
            }
            if (!nodeIds.Add(node.NodeId))
            {
                result.Errors.Add($"Duplicate NodeId: {node.NodeId}");
            }
        }

        // Check start node
        if (string.IsNullOrEmpty(graph.StartNodeId))
        {
            result.Errors.Add("Graph has no StartNodeId.");
        }
        else if (!nodeIds.Contains(graph.StartNodeId))
        {
            result.Errors.Add($"StartNodeId '{graph.StartNodeId}' does not exist in graph.");
        }

        // Check each node
        foreach (var node in nodes)
        {
            if (node == null) continue;
            ValidateNode(node, nodeIds, graph, result);
        }

        // Check reachability
        ValidateReachability(graph, nodeIds, result);

        return result;
    }

    private void ValidateNode(NodeSO node, HashSet<string> nodeIds, FlowGraphSO graph, ValidationResult result)
    {
        var exits = node.Exits;
        var hasExits = exits != null && exits.Length > 0;
        var edges = graph.GetEdgesFrom(node.NodeId);
        var hasEdges = edges.Count > 0;

        // Check for exit-less node
        if (!hasExits && !hasEdges)
        {
            result.Warnings.Add($"Node '{node.NodeId}' has no exits and no outgoing edges (dead end).");
        }

        // Validate exit destinations
        if (hasExits)
        {
            foreach (var exit in exits)
            {
                if (exit == null) continue;
                if (string.IsNullOrEmpty(exit.ToNodeId))
                {
                    result.Errors.Add($"Node '{node.NodeId}' exit '{exit.Id}' has empty ToNodeId.");
                }
                else if (!nodeIds.Contains(exit.ToNodeId))
                {
                    result.Errors.Add($"Node '{node.NodeId}' exit '{exit.Id}' references non-existent node '{exit.ToNodeId}'.");
                }
            }
        }

        // Validate edge destinations
        foreach (var edge in edges)
        {
            if (edge == null) continue;
            if (!nodeIds.Contains(edge.ToNodeId))
            {
                result.Errors.Add($"Edge from '{node.NodeId}' to '{edge.ToNodeId}' references non-existent node.");
            }
        }
    }

    private void ValidateReachability(FlowGraphSO graph, HashSet<string> nodeIds, ValidationResult result)
    {
        if (string.IsNullOrEmpty(graph.StartNodeId)) return;
        if (!nodeIds.Contains(graph.StartNodeId)) return;

        var reachable = new HashSet<string>();
        var queue = new Queue<string>();
        queue.Enqueue(graph.StartNodeId);
        reachable.Add(graph.StartNodeId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!graph.TryGetNode(current, out var node)) continue;

            // Follow exits
            var exits = node.Exits;
            if (exits != null)
            {
                foreach (var exit in exits)
                {
                    if (exit == null || string.IsNullOrEmpty(exit.ToNodeId)) continue;
                    if (reachable.Add(exit.ToNodeId))
                    {
                        queue.Enqueue(exit.ToNodeId);
                    }
                }
            }

            // Follow edges
            var edges = graph.GetEdgesFrom(current);
            foreach (var edge in edges)
            {
                if (edge == null || string.IsNullOrEmpty(edge.ToNodeId)) continue;
                if (reachable.Add(edge.ToNodeId))
                {
                    queue.Enqueue(edge.ToNodeId);
                }
            }
        }

        // Find unreachable nodes
        foreach (var nodeId in nodeIds)
        {
            if (!reachable.Contains(nodeId))
            {
                result.Warnings.Add($"Node '{nodeId}' is unreachable from start node.");
            }
        }
    }
}

public static class GraphValidatorWindow
{
    [MenuItem("Walk/Validate Selected Graph")]
    public static void ValidateSelectedGraph()
    {
        var graph = Selection.activeObject as FlowGraphSO;
        if (graph == null)
        {
            EditorUtility.DisplayDialog("Graph Validator", "Please select a FlowGraphSO asset.", "OK");
            return;
        }

        var validator = new GraphValidator();
        var result = validator.Validate(graph);

        var message = new System.Text.StringBuilder();
        message.AppendLine($"Validation of: {graph.name}");
        message.AppendLine();

        if (result.IsValid && result.Warnings.Count == 0)
        {
            message.AppendLine("No issues found.");
        }
        else
        {
            if (result.Errors.Count > 0)
            {
                message.AppendLine($"Errors ({result.Errors.Count}):");
                foreach (var error in result.Errors)
                {
                    message.AppendLine($"  - {error}");
                }
                message.AppendLine();
            }

            if (result.Warnings.Count > 0)
            {
                message.AppendLine($"Warnings ({result.Warnings.Count}):");
                foreach (var warning in result.Warnings)
                {
                    message.AppendLine($"  - {warning}");
                }
            }
        }

        Debug.Log(message.ToString());
        EditorUtility.DisplayDialog(
            result.IsValid ? "Validation Passed" : "Validation Failed",
            message.ToString(),
            "OK");
    }
}
