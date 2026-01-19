using System.Collections.Generic;

public sealed class WalkConditionCollector
{
    public Dictionary<string, int> UsedTags { get; } = new();
    public Dictionary<string, int> UsedFlags { get; } = new();
    public Dictionary<string, int> UsedCounters { get; } = new();

    public void CollectFromFlowGraph(FlowGraphSO graph)
    {
        UsedTags.Clear();
        UsedFlags.Clear();
        UsedCounters.Clear();

        if (graph == null) return;

        // Collect from all nodes (guard against null array for newly created graphs)
        var nodes = graph.Nodes;
        if (nodes != null)
        {
            foreach (var node in nodes)
            {
                if (node == null) continue;
                CollectFromNode(node);
            }
        }

        // Collect from all edges (guard against null array for newly created graphs)
        var edges = graph.Edges;
        if (edges != null)
        {
            foreach (var edge in edges)
            {
                if (edge == null) continue;
                CollectFromConditions(edge.Conditions);
            }
        }
    }

    private void CollectFromNode(NodeSO node)
    {
        // Exit conditions
        var exits = node.Exits;
        if (exits != null)
        {
            foreach (var exit in exits)
            {
                if (exit != null)
                {
                    CollectFromConditions(exit.Conditions);
                }
            }
        }

        // Gate passConditions
        var gates = node.Gates;
        if (gates != null)
        {
            foreach (var gate in gates)
            {
                if (gate != null)
                {
                    CollectFromConditions(gate.PassConditions);
                }
            }
        }

        // SideObjectTable entry conditions
        var sideTable = node.SideObjectTable;
        if (sideTable != null && sideTable.Entries != null)
        {
            foreach (var entry in sideTable.Entries)
            {
                if (entry != null)
                {
                    CollectFromConditions(entry.Conditions);
                }
            }
        }

        // EncounterTable entry conditions
        var encounterTable = node.EncounterTable;
        if (encounterTable != null && encounterTable.Entries != null)
        {
            foreach (var entry in encounterTable.Entries)
            {
                if (entry != null)
                {
                    CollectFromConditions(entry.Conditions);
                }
            }
        }
    }

    private void CollectFromConditions(ConditionSO[] conditions)
    {
        if (conditions == null) return;
        foreach (var condition in conditions)
        {
            CollectFromCondition(condition);
        }
    }

    private void CollectFromCondition(ConditionSO condition)
    {
        if (condition == null) return;

        // IKeyedCondition interface for safe key retrieval
        if (condition is IKeyedCondition keyed && !string.IsNullOrEmpty(keyed.ConditionKey))
        {
            var dict = keyed.KeyType switch
            {
                ConditionKeyType.Tag => UsedTags,
                ConditionKeyType.Flag => UsedFlags,
                ConditionKeyType.Counter => UsedCounters,
                _ => null
            };
            if (dict != null)
            {
                dict.TryGetValue(keyed.ConditionKey, out var count);
                dict[keyed.ConditionKey] = count + 1;
            }
        }

        // Recursively traverse composite conditions
        if (condition is AndCondition and && and.Conditions != null)
        {
            foreach (var child in and.Conditions)
            {
                CollectFromCondition(child);
            }
        }
        else if (condition is OrCondition or && or.Conditions != null)
        {
            foreach (var child in or.Conditions)
            {
                CollectFromCondition(child);
            }
        }
        else if (condition is NotCondition not)
        {
            CollectFromCondition(not.Condition);
        }
    }
}
