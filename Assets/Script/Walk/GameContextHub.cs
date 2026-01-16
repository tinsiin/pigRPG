public static class GameContextHub
{
    public static GameContext Current { get; private set; }

    public static void Set(GameContext context)
    {
        Current = context;
    }

    public static void Clear(GameContext context)
    {
        if (ReferenceEquals(Current, context))
        {
            Current = null;
        }
    }
}
