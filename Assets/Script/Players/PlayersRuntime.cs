using RandomExtensions;
using UnityEngine;

public sealed class PlayersRuntimeConfig
{
    public AllyClass InitGeino;
    public AllyClass InitNoramlia;
    public AllyClass InitSites;
    public BaseSkillPassive EmotionalAttachmentSkillWeakeningPassive;
    public int HpToMaxPConversionFactor = 80;
    public int MentalHpToPRecoveryConversionFactor = 120;
}

public sealed class PlayersRuntime
{
    private readonly PlayersTuningConfig tuningConfig = new PlayersTuningConfig();
    private readonly PlayersRoster roster = new PlayersRoster();
    private readonly PartyComposition composition = new PartyComposition();
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

    private AllyClass initGeino;
    private AllyClass initNoramlia;
    private AllyClass initSites;
    private BaseSkillPassive emotionalAttachmentSkillWeakeningPassive;
    private int hpToMaxPConversionFactor;
    private int mentalHpToPRecoveryConversionFactor;

    public PlayersContext Context => context;
    public IPlayersParty Party => partyService;
    public IPlayersUIControl UIControl => uiFacade;
    public IPlayersSkillUI SkillUI => uiFacade;
    public IPlayersTuning Tuning => tuningConfig;
    public IPlayersRoster Roster => roster;
    public IPartyComposition Composition => composition;
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

        // PartyPropertyCalculator（将来SOから取得するが、今は直接生成）
        var propertyCalculator = ScriptableObject.CreateInstance<PartyPropertyCalculatorSO>();
        partyBuilder = new PartyBuilder(roster, composition, uiFacade, propertyCalculator);

        walkLoopService = new WalkLoopService(roster);
        battleCallbacks = new PlayersBattleCallbacks(roster);
        partyService = new PlayersPartyService(roster, partyBuilder, battleCallbacks, walkLoopService);

        ApplyConfig(config);
        RefreshTuningConfig();

        context = new PlayersContext(partyService, uiFacade, uiFacade, tuningConfig, roster, composition);
    }

    public void Shutdown()
    {
        uiEventRouter?.Unbind();
    }

    public void Init()
    {
        Debug.Log("Init");

        CreateDecideValues();

        BindTemplateContext();

        // キャラクターを Roster に登録
        if (initGeino != null)
        {
            var geino = initGeino.DeepCopy();
            roster.RegisterAlly(CharacterId.Geino, geino);
        }
        if (initNoramlia != null)
        {
            var noramlia = initNoramlia.DeepCopy();
            roster.RegisterAlly(CharacterId.Noramlia, noramlia);
        }
        if (initSites != null)
        {
            var sites = initSites.DeepCopy();
            roster.RegisterAlly(CharacterId.Sites, sites);
        }

        // 初期パーティー編成（3人全員）
        composition.SetMembers(CharacterId.Geino, CharacterId.Noramlia, CharacterId.Sites);

        BindAllyContext();
        foreach (var ally in roster.AllAllies)
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
        return saveService.Build(roster, composition);
    }

    public void ApplySaveData(PlayersSaveData data)
    {
        saveService.Apply(data, roster, composition);
        UpdateSkillButtonVisibility();
    }
}
