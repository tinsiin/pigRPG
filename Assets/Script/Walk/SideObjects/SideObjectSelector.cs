using System.Collections.Generic;
using RandomExtensions;

public sealed class SideObjectSelector
{
    private readonly VarietyHistory varietyHistory = new();
    private readonly SideObjectCooldownTracker cooldownTracker = new();
    private string pendingLeftId;
    private string pendingRightId;

    public void Configure(int varietyDepth)
    {
        varietyHistory.MaxDepth = varietyDepth;
    }

    public SideObjectEntry[] RollPair(
        SideObjectTableSO table,
        NodeSO node,
        GameContext context,
        bool isNodeEntry = false)
    {
        if (table == null) return null;

        // Check for retained (pending) side objects
        if (node != null && node.RetainUnselectedSide && HasPending())
        {
            var leftEntry = !string.IsNullOrEmpty(pendingLeftId)
                ? FindEntryById(table, pendingLeftId)
                : PickWithFilters(table, context, excludeId: pendingRightId);

            var rightEntry = !string.IsNullOrEmpty(pendingRightId)
                ? FindEntryById(table, pendingRightId)
                : PickWithFilters(table, context, excludeId: pendingLeftId);

            ClearPending();
            return new[] { leftEntry, rightEntry };
        }

        ClearPending();

        // Normal roll with filters
        var entries = table.Entries;
        if (entries == null || entries.Length == 0) return null;

        // Try side-specific pair first
        if (TryGetSideSpecificPair(table, context, out var pair))
        {
            return pair;
        }

        var left = PickWithFilters(table, context, excludeId: null);
        var right = PickWithFilters(table, context, excludeId: left?.SideObject?.Id);
        return new[] { left, right };
    }

    public void OnSideObjectSelected(SideObjectEntry selected, int cooldownSteps)
    {
        if (selected?.SideObject == null) return;

        var id = selected.SideObject.Id;
        varietyHistory.Record(id);

        if (cooldownSteps > 0)
        {
            cooldownTracker.StartCooldown(id, cooldownSteps);
        }
    }

    public void SetPending(string leftId, string rightId)
    {
        pendingLeftId = leftId;
        pendingRightId = rightId;
    }

    public void ClearPending()
    {
        pendingLeftId = null;
        pendingRightId = null;
    }

    public bool HasPending()
    {
        return !string.IsNullOrEmpty(pendingLeftId) || !string.IsNullOrEmpty(pendingRightId);
    }

    public void AdvanceStep()
    {
        cooldownTracker.AdvanceStep();
    }

    public void Reset()
    {
        varietyHistory.Clear();
        cooldownTracker.Clear();
        ClearPending();
    }

    public SideObjectState ExportState()
    {
        return new SideObjectState(
            varietyHistory,
            cooldownTracker,
            pendingLeftId,
            pendingRightId);
    }

    public void ImportState(SideObjectState state)
    {
        if (state == null)
        {
            Reset();
            return;
        }

        varietyHistory.FromList(state.VarietyHistory);
        cooldownTracker.Import(state.Cooldowns);
        pendingLeftId = state.PendingLeftId;
        pendingRightId = state.PendingRightId;
    }

    private SideObjectEntry PickWithFilters(SideObjectTableSO table, GameContext context, string excludeId)
    {
        var entries = table.Entries;
        if (entries == null || entries.Length == 0) return null;

        var varietyBias = table.VarietyBias;
        var candidates = new List<(SideObjectEntry entry, float weight)>();
        var totalWeight = 0f;

        for (var i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            if (entry?.SideObject == null) continue;

            var id = entry.SideObject.Id;

            // Skip if excluded
            if (!string.IsNullOrEmpty(excludeId) && id == excludeId) continue;

            // Skip if on cooldown
            if (cooldownTracker.IsOnCooldown(id)) continue;

            // Skip if conditions not met
            if (!EvaluateConditions(entry, context)) continue;

            // Calculate weight with variety bias
            var weight = entry.Weight > 0f ? entry.Weight : 0f;
            if (varietyHistory.Contains(id))
            {
                weight *= varietyBias;
            }

            if (weight > 0f)
            {
                candidates.Add((entry, weight));
                totalWeight += weight;
            }
        }

        if (candidates.Count == 0) return null;

        if (totalWeight <= 0f)
        {
            return candidates[RandomEx.Shared.NextInt(0, candidates.Count)].entry;
        }

        var roll = RandomEx.Shared.NextFloat(0f, totalWeight);
        var acc = 0f;
        for (var i = 0; i < candidates.Count; i++)
        {
            acc += candidates[i].weight;
            if (roll <= acc) return candidates[i].entry;
        }

        return candidates[candidates.Count - 1].entry;
    }

    private bool EvaluateConditions(SideObjectEntry entry, GameContext context)
    {
        var conditions = entry.Conditions;
        if (conditions == null || conditions.Length == 0) return true;

        for (var i = 0; i < conditions.Length; i++)
        {
            var condition = conditions[i];
            if (condition == null) continue;
            if (!condition.IsMet(context)) return false;
        }
        return true;
    }

    private bool TryGetSideSpecificPair(SideObjectTableSO table, GameContext context, out SideObjectEntry[] pair)
    {
        pair = null;
        var entries = table.Entries;
        if (entries == null || entries.Length == 0) return false;

        var leftOnly = new List<(SideObjectEntry entry, float weight)>();
        var rightOnly = new List<(SideObjectEntry entry, float weight)>();
        var varietyBias = table.VarietyBias;

        for (var i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            var obj = entry?.SideObject;
            if (obj == null) continue;

            // Skip if on cooldown or conditions not met
            if (cooldownTracker.IsOnCooldown(obj.Id)) continue;
            if (!EvaluateConditions(entry, context)) continue;

            var hasLeft = obj.PrefabLeft != null;
            var hasRight = obj.PrefabRight != null;

            var weight = entry.Weight > 0f ? entry.Weight : 0f;
            if (varietyHistory.Contains(obj.Id))
            {
                weight *= varietyBias;
            }

            if (weight <= 0f) continue;

            if (hasLeft && !hasRight)
            {
                leftOnly.Add((entry, weight));
            }
            else if (hasRight && !hasLeft)
            {
                rightOnly.Add((entry, weight));
            }
        }

        if (leftOnly.Count == 0 || rightOnly.Count == 0) return false;

        var left = PickFromWeightedList(leftOnly);
        var right = PickFromWeightedList(rightOnly);
        pair = new[] { left, right };
        return true;
    }

    private SideObjectEntry PickFromWeightedList(List<(SideObjectEntry entry, float weight)> list)
    {
        if (list.Count == 0) return null;

        var total = 0f;
        for (var i = 0; i < list.Count; i++)
        {
            total += list[i].weight;
        }

        if (total <= 0f)
        {
            return list[RandomEx.Shared.NextInt(0, list.Count)].entry;
        }

        var roll = RandomEx.Shared.NextFloat(0f, total);
        var acc = 0f;
        for (var i = 0; i < list.Count; i++)
        {
            acc += list[i].weight;
            if (roll <= acc) return list[i].entry;
        }

        return list[list.Count - 1].entry;
    }

    private static SideObjectEntry FindEntryById(SideObjectTableSO table, string id)
    {
        if (table == null || string.IsNullOrEmpty(id)) return null;
        var entries = table.Entries;
        if (entries == null) return null;

        for (var i = 0; i < entries.Length; i++)
        {
            if (entries[i]?.SideObject?.Id == id) return entries[i];
        }
        return null;
    }
}
