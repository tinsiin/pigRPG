public static class BattleExtensionRegistryHub
{
    public static BattleExtensionRegistry Current { get; private set; }
    public static BattleExtensionCompatibilityPolicy CompatibilityPolicy { get; private set; } = BattleExtensionCompatibilityPolicy.Default;

    public static void Set(BattleExtensionRegistry registry, BattleExtensionCompatibilityPolicy policy = null)
    {
        Current = registry;
        if (policy != null)
        {
            CompatibilityPolicy = policy;
        }
    }

    public static void Clear(BattleExtensionRegistry registry)
    {
        if (ReferenceEquals(Current, registry))
        {
            Current = null;
        }
    }
}
