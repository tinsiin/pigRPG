using RandomExtensions;
using UnityEngine;

public sealed class PlayersRuntimeConfig
{
    public StairStates InitGeino;
    public BassJackStates InitNoramlia;
    public SateliteProcessStates InitSites;
    public BaseSkillPassive EmotionalAttachmentSkillWeakeningPassive;
    public int HpToMaxPConversionFactor = 80;
    public int MentalHpToPRecoveryConversionFactor = 120;
}

public sealed class PlayersRuntime
{
    private readonly PlayersProgressTracker progress = new PlayersProgressTracker();
    private readonly PlayersTuningConfig tuningConfig = new PlayersTuningConfig();
    private readonly PlayersRoster roster = new PlayersRoster();
    private readonly PlayersSaveService saveService = new PlayersSaveService();
    private PlayersUIService uiService;
    private PlayersUIFacade uiFacade;
    private PlayersUIEventRouter uiEventRouter;
    private PartyBuilder partyBuilder;
    private WalkLoopService walkLoopService;
    private PlayersBattleCallbacks battleCallbacks;
    private PlayersPartyService partyService;
    private SkillPassiveSelectionUI skillPassiveSelectionUI;
    private EmotionalAttachmentUI emotionalAttachmentUI;
    private PlayersContext context;

    private StairStates initGeino;
    private BassJackStates initNoramlia;
    private SateliteProcessStates initSites;
    private BaseSkillPassive emotionalAttachmentSkillWeakeningPassive;
    private int hpToMaxPConversionFactor;
    private int mentalHpToPRecoveryConversionFactor;

    public PlayersContext Context => context;
    public IPlayersProgress Progress => progress;
    public IPlayersParty Party => partyService;
    public IPlayersUIControl UIControl => uiFacade;
    public IPlayersSkillUI SkillUI => uiFacade;
    public IPlayersTuning Tuning => tuningConfig;
    public IPlayersRoster Roster => roster;
    public PlayersUIFacade UIFacade => uiFacade;

    public void Initialize(PlayersUIRefs refs, PlayersRuntimeConfig config)
    {
        if (refs == null)
        {
            Debug.LogError("PlayersRuntime.Initialize: PlayersUIRefs is null.");
            return;
        }

        refs.EnsureAllyUISets();

        skillPassiveSelectionUI = new SkillPassiveSelectionUI(refs.SelectSkillPassiveTargetHandle);
        emotionalAttachmentUI = new EmotionalAttachmentUI(roster, refs.EmotionalAttachmentSkillSelectUIArea);
        uiService = new PlayersUIService(
            roster,
            refs.AllyUISets,
            skillPassiveSelectionUI,
            emotionalAttachmentUI);
        uiFacade = new PlayersUIFacade();
        uiEventRouter = new PlayersUIEventRouter(uiFacade, uiService);
        partyBuilder = new PartyBuilder(roster, uiFacade);
        walkLoopService = new WalkLoopService(roster);
        battleCallbacks = new PlayersBattleCallbacks(roster);
        partyService = new PlayersPartyService(roster, partyBuilder, battleCallbacks, walkLoopService);

        ApplyConfig(config);
        RefreshTuningConfig();

        context = new PlayersContext(progress, partyService, uiFacade, uiFacade, tuningConfig, roster);
    }

    public void Shutdown()
    {
        uiEventRouter?.Unbind();
    }

    public void Init()
    {
        Debug.Log("Init");

        CreateDecideValues();

        progress.SetProgress(0);
        progress.SetStage(0);
        progress.SetArea(0);

        BindTemplateContext();
        var allies = new AllyClass[]
        {
            initGeino != null ? initGeino.DeepCopy() : null,
            initNoramlia != null ? initNoramlia.DeepCopy() : null,
            initSites != null ? initSites.DeepCopy() : null
        };
        roster.SetAllies(allies);
        BindAllyContext();
        foreach (var ally in allies)
        {
            if (ally == null) continue;
            ally.OnInitializeSkillsAndChara();
            ally.DecideDefaultMyImpression();
        }

        ApplySkillButtons();
        UpdateSkillButtonVisibility();
    }

    private void ApplyConfig(PlayersRuntimeConfig config)
    {
        if (config == null)
        {
            Debug.LogError("PlayersRuntime: config is null.");
            return;
        }

        initGeino = config.InitGeino;
        initNoramlia = config.InitNoramlia;
        initSites = config.InitSites;
        emotionalAttachmentSkillWeakeningPassive = config.EmotionalAttachmentSkillWeakeningPassive;
        hpToMaxPConversionFactor = config.HpToMaxPConversionFactor;
        mentalHpToPRecoveryConversionFactor = config.MentalHpToPRecoveryConversionFactor;
    }

    private void BindTemplateContext()
    {
        initGeino?.BindTuning(tuningConfig);
        initGeino?.BindSkillUI(uiFacade);
        initNoramlia?.BindTuning(tuningConfig);
        initNoramlia?.BindSkillUI(uiFacade);
        initSites?.BindTuning(tuningConfig);
        initSites?.BindSkillUI(uiFacade);
    }

    private void BindAllyContext()
    {
        var allies = roster.Allies;
        if (allies == null) return;
        foreach (var ally in allies)
        {
            ally?.BindTuning(tuningConfig);
            ally?.BindSkillUI(uiFacade);
        }
    }

    private void ApplySkillButtons()
    {
        uiService?.BindSkillButtons();
    }

    private void UpdateSkillButtonVisibility()
    {
        uiService?.UpdateSkillButtonVisibility();
    }

    private void CreateDecideValues()
    {
        RefreshTuningConfig();
        tuningConfig.SetExplosionVoid(RandomEx.Shared.NextFloat(10, 61));
    }

    private void RefreshTuningConfig()
    {
        tuningConfig.Initialize(
            hpToMaxPConversionFactor,
            mentalHpToPRecoveryConversionFactor,
            emotionalAttachmentSkillWeakeningPassive);
    }

    public PlayersSaveData CreateSaveData()
    {
        return saveService.Build(progress, roster);
    }

    public void ApplySaveData(PlayersSaveData data)
    {
        saveService.Apply(data, progress, roster);
        UpdateSkillButtonVisibility();
    }
}
