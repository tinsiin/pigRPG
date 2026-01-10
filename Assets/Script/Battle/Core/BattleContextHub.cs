public static class BattleContextHub
{
    public static IBattleContext Current { get; private set; }

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
