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
    private PartyUISlotManager slotManager;

    private CharacterDataRegistry characterRegistry;
    private BaseSkillPassive emotionalAttachmentSkillWeakeningPassive;
    private int hpToMaxPConversionFactor;
    private int mentalHpToPRecoveryConversionFactor;
    private bool _isInitializing;

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

        slotManager = new PartyUISlotManager(refs.BattleIconSlots);

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

        // パーティー変更時にUIスロットを再バインド
        composition.OnMembershipChanged += OnPartyMembershipChanged;
    }

    public void Shutdown()
    {
        composition.OnMembershipChanged -= OnPartyMembershipChanged;
        uiEventRouter?.Unbind();
    }

    /// <summary>
    /// パーティー構成変更時のコールバック。
    /// UIスロットを再割り当てし、BattleIconUIを再バインドする。
    /// </summary>
    private void OnPartyMembershipChanged()
    {
        // 初期化中はBindAllyContextで処理するためスキップ
        if (_isInitializing) return;
        if (slotManager == null || characterRegistry == null) return;

        // スロット再割り当て
        slotManager.AssignPartyToSlots(composition.ActiveMemberIds, roster, characterRegistry);

        // 全パーティメンバーのBattleIconUIを再バインド
        foreach (var id in composition.ActiveMemberIds)
        {
            var ally = roster.GetAlly(id);
            if (ally == null) continue;

            var battleIconUI = slotManager.GetAssignedSlot(id);
            if (battleIconUI != null)
            {
                ally.BindBattleIconUI(battleIconUI);
                battleIconUI.Init();
            }
        }

        Debug.Log($"PlayersRuntime: パーティー変更を検知、UIスロットを再割り当てしました（{composition.Count}人）");
    }

    public void Init()
    {
        Debug.Log("PlayersRuntime.Init");

        _isInitializing = true;

        CreateDecideValues();

        if (characterRegistry == null)
        {
            Debug.LogError("PlayersRuntime.Init: CharacterDataRegistry が設定されていません");
            _isInitializing = false;
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

        _isInitializing = false;
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
        // スロット配置を実行（オリジナルメンバーは固定位置、新メンバーは空きスロットへ）
        slotManager.AssignPartyToSlots(composition.ActiveMemberIds, roster, characterRegistry);

        // 全登録済みキャラクター（固定3人 + 新キャラ）にバインド
        foreach (var ally in roster.AllAllies)
        {
            if (ally == null) continue;
            ally.BindTuning(tuningConfig);
            ally.BindSkillUI(uiFacade);

            // BattleIconUIをバインド（スロットマネージャーから取得）
            var battleIconUI = slotManager.GetAssignedSlot(ally.CharacterId);
            if (battleIconUI != null)
            {
                ally.BindBattleIconUI(battleIconUI);
                battleIconUI.Init();
            }
            else
            {
                Debug.LogWarning($"PlayersRuntime.BindAllyContext: {ally.CharacterId} のBattleIconUIスロットが割り当てられていません。");
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

    /// <summary>
    /// パーティメンバーのUIスロット配置を管理する内部クラス。
    /// オリジナルメンバー（Geino, Sites, Normlia）は固定位置、新メンバーは空きスロットに配置。
    /// </summary>
    private sealed class PartyUISlotManager
    {
        public enum Slot { Left = 0, Center = 1, Right = 2 }

        private static readonly Dictionary<CharacterId, Slot> OriginalSlots = new()
        {
            { CharacterId.Sites, Slot.Left },
            { CharacterId.Geino, Slot.Center },
            { CharacterId.Noramlia, Slot.Right }
        };

        private readonly BattleIconUI[] _slots;
        private readonly CharacterId[] _assignments = new CharacterId[3];

        public PartyUISlotManager(BattleIconUI[] slots)
        {
            _slots = slots ?? new BattleIconUI[3];
            ClearAllSlots();
        }

        /// <summary>
        /// パーティメンバーをスロットに配置する。
        /// オリジナルメンバーは固定位置、新メンバーは空きスロットに左から配置。
        /// </summary>
        public void AssignPartyToSlots(
            IReadOnlyList<CharacterId> partyMembers,
            IPlayersRoster roster,
            CharacterDataRegistry characterRegistry)
        {
            ClearAllSlots();

            if (partyMembers == null) return;

            // フェーズ1: オリジナルメンバーを固定位置に配置
            foreach (var id in partyMembers)
            {
                if (!id.IsValid) continue;

                if (OriginalSlots.TryGetValue(id, out var fixedSlot))
                {
                    AssignToSlot(id, fixedSlot, roster, characterRegistry);
                }
            }

            // フェーズ2: 新メンバーを空きスロットに配置
            foreach (var id in partyMembers)
            {
                if (!id.IsValid) continue;

                // オリジナルメンバーは既に配置済みなのでスキップ
                if (OriginalSlots.ContainsKey(id)) continue;

                var emptySlot = FindFirstEmptySlot();
                if (emptySlot.HasValue)
                {
                    AssignToSlot(id, emptySlot.Value, roster, characterRegistry);
                }
                else
                {
                    Debug.LogWarning($"PartyUISlotManager: 空きスロットがありません。{id} は配置されませんでした。");
                }
            }
        }

        /// <summary>
        /// 指定キャラクターに割り当てられたBattleIconUIを取得する
        /// </summary>
        public BattleIconUI GetAssignedSlot(CharacterId id)
        {
            for (int i = 0; i < _assignments.Length; i++)
            {
                if (_assignments[i] == id)
                {
                    return _slots[i];
                }
            }
            return null;
        }

        private void AssignToSlot(
            CharacterId id,
            Slot slot,
            IPlayersRoster roster,
            CharacterDataRegistry characterRegistry)
        {
            int index = (int)slot;
            if (index < 0 || index >= _slots.Length || _slots[index] == null)
            {
                Debug.LogWarning($"PartyUISlotManager: スロット {slot} が無効です。");
                return;
            }

            _assignments[index] = id;
            var battleIconUI = _slots[index];

            // アイコンスプライトをAllyClassから取得して設定
            var ally = roster?.GetAlly(id);
            if (ally != null && ally.BattleIconSprite != null)
            {
                battleIconUI.SetIconSprite(ally.BattleIconSprite);
            }

            Debug.Log($"PartyUISlotManager: {id} を {slot} スロットに配置しました。");
        }

        private Slot? FindFirstEmptySlot()
        {
            for (int i = 0; i < _assignments.Length; i++)
            {
                if (!_assignments[i].IsValid)
                {
                    return (Slot)i;
                }
            }
            return null;
        }

        private void ClearAllSlots()
        {
            for (int i = 0; i < _assignments.Length; i++)
            {
                _assignments[i] = default;
            }
        }
    }
}
