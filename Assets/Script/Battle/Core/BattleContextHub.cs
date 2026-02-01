public static class BattleContextHub
{
    public static IBattleContext Current { get; private set; }

    /// <summary>
    /// 戦闘中かどうか（Current != null）
    /// </summary>
    public static bool IsInBattle => Current != null;

    public static void Set(IBattleContext context)
    {
        Current = context;
    }

    public static void Clear(IBattleContext context)
    {
        if (ReferenceEquals(Current, context))
        {
            Current = null;
        }
    }
}
