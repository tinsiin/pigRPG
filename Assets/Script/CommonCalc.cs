using RandomExtensions;
public static class CommonCalc
{
    /// <summary>
    /// RandomExtensionsを利用して、確率を判定する
    /// </summary>
    public static bool rollper(float percentage)
    {
        return RandomEx.Shared.NextFloat(100) < percentage;
    }
}