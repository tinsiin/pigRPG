using System.Collections.Generic;
using RandomExtensions;

public sealed class CentralObjectSelector
{
    private readonly VarietyHistory varietyHistory = new();
    private readonly CentralObjectCooldownTracker cooldownTracker = new();

    public void Configure(int varietyDepth)
    {
        varietyHistory.MaxDepth = varietyDepth;
    }

    /// <summary>
    /// 中央オブジェクトを1つ抽選する。
    /// </summary>
    public CentralObjectEntry Roll(
        CentralObjectTableSO table,
        NodeSO node,
        GameContext context,
        bool isNodeEntry = false)
    {
        if (table == null) return null;

        // 通常の重み付き抽選
        return PickWithFilters(table, context);
    }

    public void OnCentralObjectSelected(CentralObjectEntry selected)
    {
        if (selected?.CentralObject == null) return;

        var id = selected.CentralObject.Id;
        varietyHistory.Record(id);

        var cooldownSteps = selected.CooldownSteps;
        if (cooldownSteps > 0)
        {
            cooldownTracker.StartCooldown(id, cooldownSteps);
        }
    }

    public void AdvanceStep()
    {
        cooldownTracker.AdvanceStep();
    }

    public void Reset()
    {
        varietyHistory.Clear();
        cooldownTracker.Clear();
    }

    public CentralObjectState ExportState()
    {
        return new CentralObjectState(varietyHistory, cooldownTracker);
    }

    public void ImportState(CentralObjectState state)
    {
        if (state == null)
        {
            Reset();
            return;
        }

        varietyHistory.FromList(state.VarietyHistory);
        cooldownTracker.Import(state.Cooldowns);
    }

    private CentralObjectEntry PickWithFilters(CentralObjectTableSO table, GameContext context)
    {
        var entries = table.Entries;
        if (entries == null || entries.Length == 0) return null;

        var varietyBias = table.VarietyBias;
        var candidates = new List<(CentralObjectEntry entry, float weight)>();
        var totalWeight = 0f;

        for (var i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            if (entry?.CentralObject == null) continue;

            var id = entry.CentralObject.Id;

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

    private bool EvaluateConditions(CentralObjectEntry entry, GameContext context)
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

}
