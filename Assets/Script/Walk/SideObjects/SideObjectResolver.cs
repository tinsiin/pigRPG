using System.Collections.Generic;
using RandomExtensions;

public sealed class SideObjectResolver
{
    public SideObjectEntry[] RollPair(SideObjectTableSO table)
    {
        if (table == null) return null;
        var entries = table.Entries;
        if (entries == null || entries.Length == 0) return null;

        if (TryGetSideSpecificPair(entries, out var fixedPair))
        {
            return fixedPair;
        }

        var left = Pick(entries);
        var right = Pick(entries);
        return new[] { left, right };
    }

    private static SideObjectEntry Pick(SideObjectEntry[] entries)
    {
        var total = 0f;
        for (var i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            if (entry == null) continue;
            total += entry.Weight > 0f ? entry.Weight : 0f;
        }

        if (total <= 0f)
        {
            return entries[RandomEx.Shared.NextInt(0, entries.Length)];
        }

        var roll = RandomEx.Shared.NextFloat(0f, total);
        var acc = 0f;
        for (var i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            if (entry == null) continue;
            var weight = entry.Weight > 0f ? entry.Weight : 0f;
            acc += weight;
            if (roll <= acc) return entry;
        }

        return entries[entries.Length - 1];
    }

    private static bool TryGetSideSpecificPair(SideObjectEntry[] entries, out SideObjectEntry[] pair)
    {
        pair = null;
        if (entries == null || entries.Length == 0) return false;

        var leftOnly = new List<SideObjectEntry>();
        var rightOnly = new List<SideObjectEntry>();

        for (var i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            var obj = entry?.SideObject;
            if (obj == null) continue;

            var hasLeft = obj.PrefabLeft != null;
            var hasRight = obj.PrefabRight != null;

            if (hasLeft && !hasRight)
            {
                leftOnly.Add(entry);
            }
            else if (hasRight && !hasLeft)
            {
                rightOnly.Add(entry);
            }
        }

        if (leftOnly.Count == 0 || rightOnly.Count == 0) return false;

        var left = Pick(leftOnly.ToArray());
        var right = Pick(rightOnly.ToArray());
        pair = new[] { left, right };
        return true;
    }
}
