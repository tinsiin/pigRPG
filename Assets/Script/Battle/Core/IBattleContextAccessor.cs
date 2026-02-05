public interface IBattleContextAccessor
{
    IBattleContext Current { get; }
    bool IsInBattle { get; }
    void Set(IBattleContext context);
    void Clear(IBattleContext context);
}

public sealed class BattleContextHubAccessor : IBattleContextAccessor
{
    public IBattleContext Current => BattleContextHub.Current;
    public bool IsInBattle => BattleContextHub.IsInBattle;
    public void Set(IBattleContext context) => BattleContextHub.Set(context);
    public void Clear(IBattleContext context) => BattleContextHub.Clear(context);
}
