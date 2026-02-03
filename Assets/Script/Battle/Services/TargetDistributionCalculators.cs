/// <summary>
/// 爆発型分散計算。前のめりなら[0]、後衛なら[1]を使用。
/// インデックスは消費しない（位置ベースの固定分散）。
/// </summary>
public sealed class ExplosionDistributionCalculator : ITargetDistributionCalculator
{
    public (float ratio, int nextFrontIndex, int nextBackIndex) Calculate(
        float[] spreadValues, int frontIndex, int backIndex, bool isVanguard, int totalTargets)
    {
        if (spreadValues == null || spreadValues.Length == 0)
            return (1f, frontIndex, backIndex);

        if (isVanguard)
        {
            return (spreadValues[0], frontIndex, backIndex);
        }
        else
        {
            if (spreadValues.Length > 1)
                return (spreadValues[1], frontIndex, backIndex);
            return (spreadValues[0], frontIndex, backIndex);
        }
    }
}

/// <summary>
/// ビーム型分散計算。前のめりなら先頭から、後衛なら末尾から順次消費。
/// 両端を独立して追跡する。
/// </summary>
public sealed class BeamDistributionCalculator : ITargetDistributionCalculator
{
    public (float ratio, int nextFrontIndex, int nextBackIndex) Calculate(
        float[] spreadValues, int frontIndex, int backIndex, bool isVanguard, int totalTargets)
    {
        if (spreadValues == null || spreadValues.Length == 0)
            return (1f, frontIndex, backIndex);

        // 両端から消費した合計が配列長を超えていないかチェック
        var totalConsumed = frontIndex + backIndex;
        if (totalConsumed >= spreadValues.Length)
            return (1f, frontIndex, backIndex);

        if (isVanguard)
        {
            // 先頭から消費（frontIndex から）
            var ratio = spreadValues[frontIndex];
            return (ratio, frontIndex + 1, backIndex);
        }
        else
        {
            // 末尾から消費（backIndex を使って末尾からの位置を計算）
            var lastAvailable = spreadValues.Length - 1 - backIndex;
            if (lastAvailable < frontIndex)
                return (1f, frontIndex, backIndex); // 前後が衝突
            var ratio = spreadValues[lastAvailable];
            return (ratio, frontIndex, backIndex + 1);
        }
    }
}

/// <summary>
/// 投げ型分散計算。ビームの逆で前のめりなら末尾から、後衛なら先頭から。
/// 両端を独立して追跡する。
/// </summary>
public sealed class ThrowDistributionCalculator : ITargetDistributionCalculator
{
    public (float ratio, int nextFrontIndex, int nextBackIndex) Calculate(
        float[] spreadValues, int frontIndex, int backIndex, bool isVanguard, int totalTargets)
    {
        if (spreadValues == null || spreadValues.Length == 0)
            return (1f, frontIndex, backIndex);

        // 両端から消費した合計が配列長を超えていないかチェック
        var totalConsumed = frontIndex + backIndex;
        if (totalConsumed >= spreadValues.Length)
            return (1f, frontIndex, backIndex);

        if (isVanguard)
        {
            // 末尾から消費（backIndex を使って末尾からの位置を計算）
            var lastAvailable = spreadValues.Length - 1 - backIndex;
            if (lastAvailable < frontIndex)
                return (1f, frontIndex, backIndex); // 前後が衝突
            var ratio = spreadValues[lastAvailable];
            return (ratio, frontIndex, backIndex + 1);
        }
        else
        {
            // 先頭から消費（frontIndex から）
            var ratio = spreadValues[frontIndex];
            return (ratio, frontIndex + 1, backIndex);
        }
    }
}

/// <summary>
/// ランダム型分散計算。常に末尾から順次消費。
/// </summary>
public sealed class RandomDistributionCalculator : ITargetDistributionCalculator
{
    public (float ratio, int nextFrontIndex, int nextBackIndex) Calculate(
        float[] spreadValues, int frontIndex, int backIndex, bool isVanguard, int totalTargets)
    {
        if (spreadValues == null || spreadValues.Length == 0)
            return (1f, frontIndex, backIndex);

        var lastAvailable = spreadValues.Length - 1 - backIndex;
        if (lastAvailable < 0)
            return (1f, frontIndex, backIndex);

        var ratio = spreadValues[lastAvailable];
        return (ratio, frontIndex, backIndex + 1);
    }
}

/// <summary>
/// デフォルト分散計算。分散なし（100%）。
/// </summary>
public sealed class DefaultDistributionCalculator : ITargetDistributionCalculator
{
    public (float ratio, int nextFrontIndex, int nextBackIndex) Calculate(
        float[] spreadValues, int frontIndex, int backIndex, bool isVanguard, int totalTargets)
    {
        return (1f, frontIndex, backIndex);
    }
}
