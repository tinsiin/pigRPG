using System.Collections.Generic;

public interface IBattleRandom
{
    int NextInt(int maxExclusive);
    int NextInt(int minInclusive, int maxExclusive);
    bool NextBool();
    float NextFloat(float maxExclusive);
    float NextFloat(float minInclusive, float maxExclusive);
    float NextFloat();
    void Shuffle<T>(IList<T> list);
    T GetItem<T>(IReadOnlyList<T> list);
}
