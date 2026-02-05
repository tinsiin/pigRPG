public interface IBattleUiBridgeAccessor
{
    BattleUIBridge Active { get; }
    void SetActive(BattleUIBridge bridge);
}

public sealed class BattleUiBridgeAccessor : IBattleUiBridgeAccessor
{
    public BattleUIBridge Active => BattleUIBridge.Active;
    public void SetActive(BattleUIBridge bridge) => BattleUIBridge.SetActive(bridge);
}
