using System;
using System.Collections.Generic;

public sealed class SystemBattleRandom : IBattleRandom
{
    private readonly Random _random;

    public SystemBattleRandom(int? seed = null)
    {
        _random = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    public int NextInt(int maxExclusive)
    {
        if (maxExclusive <= 0) return 0;
        return _random.Next(maxExclusive);
    }

    public int NextInt(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive) return minInclusive;
        return _random.Next(minInclusive, maxExclusive);
    }

    public bool NextBool()
    {
        return _random.Next(0, 2) == 0;
    }

    public float NextFloat(float maxExclusive)
    {
        if (maxExclusive <= 0) return 0f;
        return (float)_random.NextDouble() * maxExclusive;
    }

    public float NextFloat(float minInclusive, float maxExclusive)
    {
        if (maxExclusive <= minInclusive) return minInclusive;
        return minInclusive + (float)_random.NextDouble() * (maxExclusive - minInclusive);
    }

    public float NextFloat()
    {
        return (float)_random.NextDouble();
    }

    public void Shuffle<T>(IList<T> list)
    {
        if (list == null || list.Count <= 1) return;
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = _random.Next(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    public T GetItem<T>(IReadOnlyList<T> list)
    {
        if (list == null || list.Count == 0) return default;
        var index = _random.Next(0, list.Count);
        return list[index];
    }
}
