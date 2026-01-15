using System.Collections.Generic;
using RandomExtensions;
using UnityEngine;

public readonly struct EncounterRollResult
{
    public bool Triggered { get; }
    public EncounterSO Encounter { get; }

    public EncounterRollResult(bool triggered, EncounterSO encounter)
    {
        Triggered = triggered;
        Encounter = encounter;
    }

    public static EncounterRollResult None => new EncounterRollResult(false, null);
}

public sealed class EncounterResolver
{
    private static bool IsLogEnabled(EncounterTableSO table)
    {
        return table != null && table.EnableDebugLog;
    }

    private static void Log(EncounterTableSO table, GameContext context, string message)
    {
        if (!IsLogEnabled(table)) return;
        var nodeId = context?.WalkState?.CurrentNodeId ?? "null";
        Debug.Log($"[Encounter] {message} (table={table.name}, node={nodeId})");
    }

    public EncounterRollResult Resolve(EncounterTableSO table, GameContext context, bool skipRoll)
    {
        if (table == null || context == null) return EncounterRollResult.None;
        if (skipRoll)
        {
            Log(table, context, "skip: skipRoll");
            return EncounterRollResult.None;
        }

        var state = context.GetEncounterState(table);
        if (state == null)
        {
            Log(table, context, "skip: state null");
            return EncounterRollResult.None;
        }

        if (!state.IsInitialized)
        {
            state.IsInitialized = true;
            state.GraceRemaining = Mathf.Max(0, table.GraceSteps);
            Log(table, context, $"init: grace={state.GraceRemaining} cooldown={state.CooldownRemaining}");
        }

        TickState(table, state);
        if (state.CooldownRemaining > 0 || state.GraceRemaining > 0)
        {
            Log(table, context, $"skip: cooldown={state.CooldownRemaining} grace={state.GraceRemaining} misses={state.Misses}");
            return EncounterRollResult.None;
        }

        var rate = Mathf.Clamp01(table.BaseRate + state.Misses * table.PityIncrement);
        if (table.PityMax > 0f)
        {
            rate = Mathf.Min(rate, table.PityMax);
        }

        if (rate <= 0f)
        {
            state.Misses++;
            Log(table, context, $"miss: rate<=0 base={table.BaseRate:0.###} pity={table.PityIncrement:0.###} misses={state.Misses}");
            return EncounterRollResult.None;
        }

        var roll = RandomEx.Shared.NextFloat(0f, 1f);
        if (roll > rate)
        {
            state.Misses++;
            Log(table, context, $"miss: roll={roll:0.###} rate={rate:0.###} base={table.BaseRate:0.###} misses={state.Misses}");
            return EncounterRollResult.None;
        }

        var encounter = PickEncounter(table, context, out var validCount, out var totalWeight);
        if (encounter == null)
        {
            state.Misses++;
            var entryCount = table.Entries != null ? table.Entries.Length : 0;
            Log(table, context, $"miss: no encounter entries={entryCount} valid={validCount} totalWeight={totalWeight:0.###}");
            return EncounterRollResult.None;
        }

        state.CooldownRemaining = Mathf.Max(0, table.CooldownSteps);
        state.Misses = 0;
        Log(table, context, $"hit: roll={roll:0.###} rate={rate:0.###} encounter={encounter.name}");
        return new EncounterRollResult(true, encounter);
    }

    private static void TickState(EncounterTableSO table, EncounterState state)
    {
        if (state.CooldownRemaining > 0)
        {
            state.CooldownRemaining = Mathf.Max(0, state.CooldownRemaining - 1);
        }
        if (state.GraceRemaining > 0)
        {
            state.GraceRemaining = Mathf.Max(0, state.GraceRemaining - 1);
        }
    }

    private static EncounterSO PickEncounter(EncounterTableSO table, GameContext context, out int validCount, out float totalWeight)
    {
        validCount = 0;
        totalWeight = 0f;
        var entries = table.Entries;
        if (entries == null || entries.Length == 0) return null;

        var valid = new List<EncounterEntry>();
        for (var i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            if (entry == null || entry.Encounter == null) continue;
            if (!AreConditionsMet(entry.Conditions, context)) continue;
            valid.Add(entry);
        }

        validCount = valid.Count;
        if (validCount == 0) return null;

        var total = 0f;
        for (var i = 0; i < valid.Count; i++)
        {
            total += Mathf.Max(0f, valid[i].Weight);
        }
        totalWeight = total;

        if (total <= 0f)
        {
            var index = RandomEx.Shared.NextInt(0, valid.Count);
            return valid[index].Encounter;
        }

        var roll = RandomEx.Shared.NextFloat(0f, total);
        var acc = 0f;
        for (var i = 0; i < valid.Count; i++)
        {
            acc += Mathf.Max(0f, valid[i].Weight);
            if (roll <= acc) return valid[i].Encounter;
        }

        return valid[valid.Count - 1].Encounter;
    }

    private static bool AreConditionsMet(ConditionSO[] conditions, GameContext context)
    {
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
