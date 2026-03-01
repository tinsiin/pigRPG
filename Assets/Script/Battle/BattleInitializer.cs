using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 戦闘初期化の責務を分離したクラス
/// Walking.csのEncountメソッドから戦闘関連処理を抽出
/// </summary>
public class BattleInitializer
{
    private readonly MessageDropper _messageDropper;
    private readonly WatchUIUpdate _watchUIUpdate;

    /// <summary>
    /// Phase 1: WatchUIUpdateをコンストラクタで注入
    /// </summary>
    public BattleInitializer(MessageDropper messageDropper, WatchUIUpdate watchUIUpdate = null)
    {
        _messageDropper = messageDropper;
        // フォールバックでWatchUIUpdate.Instanceを使用（段階的移行のため）
        _watchUIUpdate = watchUIUpdate ?? WatchUIUpdate.Instance;
    }
    
    public UniTask<BattleSetupResult> InitializeBattle(
        IReadOnlyList<NormalEnemy> enemies,
        int globalSteps,
        IPlayersParty playersParty,
        IPlayersUIControl playersUIControl,
        IPlayersSkillUI playersSkillUI,
        IPlayersRoster playersRoster,
        IPlayersTuning playersTuning,
        float escapeRate,
        int enemyNumber = 2,
        IBattleMetaProvider metaProviderOverride = null,
        int? randomSeed = null,
        BattleRuleCatalog ruleCatalogOverride = null,
        BattleRuleRegistry ruleRegistryOverride = null,
        FriendshipComboRegistry comboRegistry = null)
    {
        var enemyGroup = EncounterEnemySelector.SelectGroup(enemies, globalSteps, enemyNumber,
            comboRegistry: comboRegistry);
        return InitializeBattleFromGroup(
            enemyGroup,
            playersParty,
            playersUIControl,
            playersSkillUI,
            playersRoster,
            playersTuning,
            escapeRate,
            metaProviderOverride,
            randomSeed,
            ruleCatalogOverride,
            ruleRegistryOverride,
            comboRegistry);
    }

    private async UniTask<BattleSetupResult> InitializeBattleFromGroup(
        BattleGroup enemyGroup,
        IPlayersParty playersParty,
        IPlayersUIControl playersUIControl,
        IPlayersSkillUI playersSkillUI,
        IPlayersRoster playersRoster,
        IPlayersTuning playersTuning,
        float escapeRate,
        IBattleMetaProvider metaProviderOverride,
        int? randomSeed,
        BattleRuleCatalog ruleCatalogOverride,
        BattleRuleRegistry ruleRegistryOverride,
        FriendshipComboRegistry comboRegistry = null)
    {
        var result = new BattleSetupResult();
        if (playersParty == null)
        {
            Debug.LogError("BattleInitializer.InitializeBattleFromGroup: playersParty が null です");
            result.EncounterOccurred = false;
            return result;
        }

        if (enemyGroup == null)
        {
            result.EncounterOccurred = false;
            return result;
        }

        result.EncounterOccurred = true;
        result.EnemyGroup = enemyGroup;

        result.AllyGroup = DetermineAllyGroup(result.EnemyGroup, playersParty);

        BindTuning(result.AllyGroup, playersTuning);
        BindTuning(result.EnemyGroup, playersTuning);
        BindSkillUi(result.AllyGroup, playersSkillUI);

        var metaProvider = metaProviderOverride ?? new WalkBattleMetaProvider(playersParty, playersUIControl);
        var services = CreateBattleServices(playersSkillUI, playersRoster, randomSeed, ruleCatalogOverride, ruleRegistryOverride);
        result.Orchestrator = new BattleOrchestrator(
            result.AllyGroup,
            result.EnemyGroup,
            BattleStartSituation.Normal,
            services,
            escapeRate,
            metaProvider
        );
        BattleOrchestratorHub.Set(result.Orchestrator);
        Debug.Assert(BattleOrchestratorHub.Current != null,
            "BattleOrchestrator initialization failed - BattleOrchestratorHub.Current is null after Set()");
        result.BattleContext = result.Orchestrator.Manager;
        result.Orchestrator.Manager.ComboRegistry = comboRegistry;
        result.Session = result.Orchestrator.Session;

        if (playersSkillUI != null)
        {
            playersSkillUI.OnBattleStart();
        }
        else
        {
            Debug.LogError("BattleInitializer: SkillUI が null です");
        }

        if (_watchUIUpdate != null)
        {
            // ズーム前にEyeAreaStateをBattleに変更（BattleContentを先にactiveにする）
            if (UIStateHub.EyeState != null)
            {
                UIStateHub.EyeState.Value = EyeAreaState.Battle;
            }

            using (UIBlocker.Instance?.Acquire(BlockScope.AllContents))
            {
                await _watchUIUpdate.FirstImpressionZoomImproved();
            }
        }

        return result;
    }
    
    /// <summary>
    /// 敵グループに応じた味方グループを決定
    /// </summary>
    private BattleGroup DetermineAllyGroup(BattleGroup enemyGroup, IPlayersParty playersParty)
    {
        // 将来的な拡張ポイント：
        // 敵グループの構成によって味方の人選を変更する処理
        // 例：特定の敵には特定のキャラクターのみで戦う
        
        // 現在はフルパーティを返す
        return playersParty.GetParty();
    }

    private void BindTuning(BattleGroup group, IPlayersTuning tuning)
    {
        if (group == null || tuning == null) return;
        foreach (var actor in group.Ours)
        {
            actor?.BindTuning(tuning);
        }
    }

    private void BindSkillUi(BattleGroup group, IPlayersSkillUI skillUi)
    {
        if (group == null || skillUi == null) return;
        foreach (var actor in group.Ours)
        {
            actor?.BindSkillUI(skillUi);
        }
    }

    private BattleServices CreateBattleServices(
        IPlayersSkillUI skillUi,
        IPlayersRoster roster,
        int? randomSeed,
        BattleRuleCatalog ruleCatalogOverride,
        BattleRuleRegistry ruleRegistryOverride)
    {
        var actionMark = _watchUIUpdate?.ActionMarkCtrl;
        var enemyPlacement = _watchUIUpdate?.EnemyPlacementCtrl;
        var introOrchestrator = _watchUIUpdate?.IntroOrchestrator;
        IBattleRandom random = randomSeed.HasValue ? new SystemBattleRandom(randomSeed.Value) : new UnityBattleRandom();
        var ruleCatalog = ruleCatalogOverride;
        if (ruleCatalog == null)
        {
            BattleRuleCatalogIO.TryLoadDefault(out ruleCatalog);
        }

        var extensionRegistry = BattleExtensionRegistryHub.Current;
        var extensionPolicy = BattleExtensionRegistryHub.CompatibilityPolicy;
        TargetingPolicyRegistry targetingPolicies = null;
        ISkillEffectPipeline effectPipeline = null;
        if (ruleCatalog != null)
        {
            var registry = ruleRegistryOverride ?? BattleRuleRegistry.CreateDefault();
            extensionRegistry?.ApplyTo(registry, extensionPolicy);
            targetingPolicies = registry.BuildTargetingPolicies(ruleCatalog);
            effectPipeline = registry.BuildEffectPipeline(ruleCatalog);
        }

        return new BattleServices(
            _messageDropper,
            skillUi,
            roster,
            actionMark,
            enemyPlacement,
            introOrchestrator,
            SchizoLog.Instance,
            BattleSystemArrowManager.Instance,
            targetingPolicies: targetingPolicies,
            effectPipeline: effectPipeline,
            random: random,
            logger: new UnityBattleLogger(),
            randomSeed: randomSeed);
    }
    
    /// <summary>
    /// 戦闘UI の初期状態を設定
    /// </summary>
    public TabState SetupInitialBattleUI(IBattleLifecycle orchestrator)
    {
        if (orchestrator == null)
            return TabState.walk;

        orchestrator.StartBattle();
        return orchestrator.CurrentUiState;
    }
}

/// <summary>
/// 戦闘初期化の結果
/// </summary>
public class BattleSetupResult
{
    public bool EncounterOccurred { get; set; }
    public BattleGroup EnemyGroup { get; set; }
    public BattleGroup AllyGroup { get; set; }
    public BattleOrchestrator Orchestrator { get; set; }
    public IBattleContext BattleContext { get; set; }
    public IBattleSession Session { get; set; }
    public IBattleLifecycle Lifecycle => Orchestrator;
}
