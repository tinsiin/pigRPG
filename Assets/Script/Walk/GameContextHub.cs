public static class GameContextHub
{
    public static GameContext Current { get; private set; }

    public static void Set(GameContext context)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Current != null && Current != context)
        {
            UnityEngine.Debug.Log($"[Walk] GameContextHub replacing: old.Refresh={Current.RequestRefreshWithoutStep}, old.Walking={Current.IsWalkingStep} → new.Refresh={context?.RequestRefreshWithoutStep}, new.Walking={context?.IsWalkingStep}");
        }
        else if (Current == null && context != null)
        {
            UnityEngine.Debug.Log($"[Walk] GameContextHub set: Refresh={context.RequestRefreshWithoutStep}, Walking={context.IsWalkingStep}");
        }
#endif
        Current = context;
    }

    public static void Clear(GameContext context)
    {
        if (ReferenceEquals(Current, context))
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            UnityEngine.Debug.Log("[Walk] GameContextHub cleared");
#endif
            Current = null;
        }
    }
}
