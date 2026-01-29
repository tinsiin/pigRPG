using System.Collections.Generic;
using RandomExtensions;
using UnityEngine;

public sealed class PlayersRuntimeConfig
{
    public CharacterDataRegistry CharacterRegistry;
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

    private CharacterDataRegistry characterRegistry;
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
    public CharacterDataRegistry CharacterRegistry => characterRegistry;

    public void Initialize(PlayersUIRefs refs, PlayersRuntimeConfig config)
    {
        if (refs == null)
        {
            Debug.LogError("PlayersRuntime.Initialize: PlayersUIRefs is null.");
            return;
        }

        skillPassiveSelectionUI = new SkillPassiveSelectionUI(refs.SelectSkillPassiveTargetHandle);
        emotionalAttachmentUI = new EmotionalAttachmentUI(roster, refs.EmotionalAttachmentSkillSelectUIArea);
        uiService = new PlayersUIService(
            roster,
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

        context = new PlayersContext(partyService, uiFacade, uiFacade, tuningConfig, roster, composition, characterRegistry);
    }

    public void Shutdown()
    {
        uiEventRouter?.Unbind();
    }

    public void Init()
    {
        Debug.Log("PlayersRuntime.Init");

        CreateDecideValues();

        if (characterRegistry == null)
        {
            Debug.LogError("PlayersRuntime.Init: CharacterDataRegistry が設定されていません");
            return;
        }

        // 初期パーティメンバーのみRoster登録（全キャラではない）
        var initialParty = new List<CharacterId>();
        foreach (var characterData in characterRegistry.GetInitialPartyMembers())
        {
            if (characterData == null)
            {
                Debug.LogWarning("PlayersRuntime.Init: null の CharacterDataSO がスキップされました");
                continue;
            }

            var id = characterData.Id;
            if (!id.IsValid)
            {
                Debug.LogError($"PlayersRuntime.Init: 無効なID '{characterData.name}'");
                continue;
            }

            if (roster.IsUnlocked(id))
            {
                Debug.LogWarning($"PlayersRuntime.Init: {id} は既に登録済みです（重複スキップ）");
                continue;
            }

            var instance = characterData.CreateInstance();
            if (instance == null)
            {
                Debug.LogError($"PlayersRuntime.Init: {id} のインスタンス生成に失敗しました（_templateが未設定？）");
                continue;
            }

            roster.RegisterAlly(id, instance);
            initialParty.Add(id);
            Debug.Log($"PlayersRuntime.Init: {id} を初期パーティとして登録");
        }

        // 初期パーティ編成
        if (initialParty.Count > 0)
        {
            composition.SetMembers(initialParty.ToArray());
        }
        else
        {
            Debug.LogWarning("PlayersRuntime.Init: 初期パーティメンバーが0人です");
        }

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

        characterRegistry = config.CharacterRegistry;
        emotionalAttachmentSkillWeakeningPassive = config.EmotionalAttachmentSkillWeakeningPassive;
        hpToMaxPConversionFactor = config.HpToMaxPConversionFactor;
        mentalHpToPRecoveryConversionFactor = config.MentalHpToPRecoveryConversionFactor;
    }

    private void BindAllyContext()
    {
        var uiRegistry = CharacterUIRegistry.Instance;

        // 全登録済みキャラクター（固定3人 + 新キャラ）にバインド
        foreach (var ally in roster.AllAllies)
        {
            if (ally == null) continue;
            ally.BindTuning(tuningConfig);
            ally.BindSkillUI(uiFacade);

            // BattleIconUIをバインド（バトル中のアイコン・HPバー・エフェクト用）
            if (uiRegistry != null)
            {
                var battleIconUI = uiRegistry.GetBattleIconUI(ally.CharacterId);
                if (battleIconUI != null)
                {
                    ally.BindBattleIconUI(battleIconUI);
                    battleIconUI.Init();
                }
                else
                {
                    Debug.LogWarning($"PlayersRuntime.BindAllyContext: {ally.CharacterId} のBattleIconUIが見つかりません。CharacterUIRegistryで設定してください。");
                }
            }
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
        saveService.Apply(data, roster, characterRegistry, composition);

        // 復元されたキャラクター（新キャラ含む）にTuning/SkillUIをバインド
        BindAllyContext();

        // スキルボタンを再バインド
        ApplySkillButtons();
        UpdateSkillButtonVisibility();
    }
}
