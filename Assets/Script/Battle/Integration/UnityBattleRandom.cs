using System.Collections.Generic;
using System.Linq;
using RandomExtensions;
using RandomExtensions.Linq;

public sealed class UnityBattleRandom : IBattleRandom
{
    public int NextInt(int maxExclusive)
    {
        return RandomEx.Shared.NextInt(maxExclusive);
    }

    public int NextInt(int minInclusive, int maxExclusive)
    {
        return RandomEx.Shared.NextInt(minInclusive, maxExclusive);
    }

    public bool NextBool()
    {
        return RandomEx.Shared.NextBool();
    }

    public float NextFloat(float maxExclusive)
    {
        return RandomEx.Shared.NextFloat(maxExclusive);
    }

    public float NextFloat(float minInclusive, float maxExclusive)
    {
        return RandomEx.Shared.NextFloat(minInclusive, maxExclusive);
    }

    public float NextFloat()
    {
        return RandomEx.Shared.NextFloat();
    }

    public void Shuffle<T>(IList<T> list)
    {
        if (list == null) return;
        list.Shuffle();
    }

    public T GetItem<T>(IReadOnlyList<T> list)
    {
        if (list == null || list.Count == 0) return default;
        return RandomEx.Shared.GetItem(list.ToArray());
    }
}
