public sealed class BattleServices
{
    public MessageDropper MessageDropper { get; }
    public IPlayersSkillUI SkillUi { get; }
    public IPlayersRoster Roster { get; }
    public IActionMarkController ActionMarkController { get; }
    public IEnemyPlacementController EnemyPlacementController { get; }
    public IIntroOrchestratorFacade IntroOrchestrator { get; }
    public SchizoLog SchizoLog { get; }
    public IArrowManager ArrowManager { get; }
    public TargetingPolicyRegistry TargetingPolicies { get; }
    public ISkillEffectPipeline EffectPipeline { get; }
    public IBattleRandom Random { get; }
    public IBattleLogger Logger { get; }
    public int? RandomSeed { get; }
    public IBattleContextAccessor ContextAccessor { get; }
    public IBattleUiBridgeAccessor UiBridgeAccessor { get; }

    public BattleServices(
        MessageDropper messageDropper,
        IPlayersSkillUI skillUi,
        IPlayersRoster roster,
        IActionMarkController actionMarkController = null,
        IEnemyPlacementController enemyPlacementController = null,
        IIntroOrchestratorFacade introOrchestrator = null,
        SchizoLog schizoLog = null,
        IArrowManager arrowManager = null,
        TargetingPolicyRegistry targetingPolicies = null,
        ISkillEffectPipeline effectPipeline = null,
        IBattleRandom random = null,
        IBattleLogger logger = null,
        int? randomSeed = null,
        IBattleContextAccessor contextAccessor = null,
        IBattleUiBridgeAccessor uiBridgeAccessor = null)
    {
        MessageDropper = messageDropper;
        SkillUi = skillUi;
        Roster = roster;
        ActionMarkController = actionMarkController;
        EnemyPlacementController = enemyPlacementController;
        IntroOrchestrator = introOrchestrator;
        SchizoLog = schizoLog;
        ArrowManager = arrowManager;
        TargetingPolicies = targetingPolicies ?? TargetingPolicyRegistry.CreateDefault();
        EffectPipeline = effectPipeline;
        RandomSeed = randomSeed;
        Random = random ?? (randomSeed.HasValue ? new SystemBattleRandom(randomSeed.Value) : new SystemBattleRandom());
        Logger = logger ?? new NoOpBattleLogger();
        ContextAccessor = contextAccessor ?? new BattleContextHubAccessor();
        UiBridgeAccessor = uiBridgeAccessor ?? new BattleUiBridgeAccessor();
    }
}
