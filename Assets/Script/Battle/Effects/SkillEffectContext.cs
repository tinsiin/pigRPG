using System.Collections.Generic;

/// <summary>
/// スキル効果の実行に必要なコンテキスト情報。
/// </summary>
public sealed class SkillEffectContext
{
    public BaseStates Acter { get; }
    public Faction ActerFaction { get; }
    public UnderActersEntryList Targets { get; }
    public BattleGroup AllyGroup { get; }
    public BattleGroup EnemyGroup { get; }
    public ActionQueue Acts { get; }
    public int BattleTurnCount { get; }
    public IBattleQueryService QueryService { get; }
    public IBattleRandom Random { get; }

    /// <summary>
    /// 効果間でデータを受け渡すための辞書
    /// </summary>
    public Dictionary<string, object> SharedData { get; } = new();

    public SkillEffectContext(
        BaseStates acter,
        Faction acterFaction,
        UnderActersEntryList targets,
        BattleGroup allyGroup,
        BattleGroup enemyGroup,
        ActionQueue acts,
        int battleTurnCount,
        IBattleQueryService queryService,
        IBattleRandom random)
    {
        Acter = acter;
        ActerFaction = acterFaction;
        Targets = targets;
        AllyGroup = allyGroup;
        EnemyGroup = enemyGroup;
        Acts = acts;
        BattleTurnCount = battleTurnCount;
        QueryService = queryService;
        Random = random ?? new SystemBattleRandom();
    }
}
