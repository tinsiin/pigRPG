using Cysharp.Threading.Tasks;

public sealed class BattleOrchestrator
{
    public BattleManager Manager { get; }

    public BattleOrchestrator(
        BattleGroup allyGroup,
        BattleGroup enemyGroup,
        BattleStartSituation first,
        MessageDropper messageDropper,
        float escapeRate,
        IBattleMetaProvider metaProvider)
    {
        Manager = new BattleManager(allyGroup, enemyGroup, first, messageDropper, escapeRate, metaProvider);
    }

    public TabState StartBattle()
    {
        return Manager.ACTPop();
    }

    public UniTask<TabState> Step()
    {
        return Manager.CharacterActBranching();
    }

    public UniTask EndBattle()
    {
        return Manager.OnBattleEnd();
    }
}
