using System.Collections.Generic;

public enum ExitSelectionMode
{
    ShowAll,
    WeightedRandom
}

public readonly struct ResolvedExit
{
    public string Id { get; }
    public string ToNodeId { get; }
    public string UILabel { get; }
    public int Weight { get; }

    public ResolvedExit(string id, string toNodeId, string uiLabel, int weight)
    {
        Id = id;
        ToNodeId = toNodeId;
        UILabel = uiLabel;
        Weight = weight;
    }

    public static ResolvedExit FromCandidate(ExitCandidate candidate)
    {
        return new ResolvedExit(
            candidate.Id,
            candidate.ToNodeId,
            candidate.UILabel,
            candidate.Weight);
    }
}

public sealed class ExitResolver
{
    public List<ResolvedExit> ResolveExits(
        NodeSO node,
        GameContext context,
        ExitSelectionMode mode,
        int maxChoices)
    {
        var candidates = GatherCandidates(node, context);
        if (candidates.Count == 0) return candidates;

        switch (mode)
        {
            case ExitSelectionMode.ShowAll:
                return LimitChoices(candidates, maxChoices);

            case ExitSelectionMode.WeightedRandom:
                return SelectWeightedRandom(candidates, maxChoices, context);

            default:
                return candidates;
        }
    }

    private List<ResolvedExit> GatherCandidates(NodeSO node, GameContext context)
    {
        var result = new List<ResolvedExit>();

        var exits = node.Exits;
        if (exits == null || exits.Length == 0)
            return result;

        foreach (var exit in exits)
        {
            if (exit != null && exit.CheckConditions(context))
            {
                result.Add(ResolvedExit.FromCandidate(exit));
            }
        }

        return result;
    }

    private List<ResolvedExit> LimitChoices(List<ResolvedExit> candidates, int maxChoices)
    {
        if (maxChoices <= 0 || candidates.Count <= maxChoices)
            return candidates;

        return candidates.GetRange(0, maxChoices);
    }

    private List<ResolvedExit> SelectWeightedRandom(List<ResolvedExit> candidates, int count, GameContext context)
    {
        if (count <= 0) count = 1;
        if (candidates.Count <= count) return candidates;

        var result = new List<ResolvedExit>();
        var remaining = new List<ResolvedExit>(candidates);

        for (int i = 0; i < count && remaining.Count > 0; i++)
        {
            int totalWeight = 0;
            foreach (var c in remaining)
                totalWeight += c.Weight;

            if (totalWeight <= 0)
            {
                // 全weight=0なら均等選択
                int index = context.GetRandomInt(0, remaining.Count);
                result.Add(remaining[index]);
                remaining.RemoveAt(index);
            }
            else
            {
                int roll = context.GetRandomInt(0, totalWeight);
                int cumulative = 0;
                for (int j = 0; j < remaining.Count; j++)
                {
                    cumulative += remaining[j].Weight;
                    if (roll < cumulative)
                    {
                        result.Add(remaining[j]);
                        remaining.RemoveAt(j);
                        break;
                    }
                }
            }
        }

        return result;
    }
}
