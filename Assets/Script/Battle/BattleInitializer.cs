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
    
    public BattleInitializer(MessageDropper messageDropper)
    {
        _messageDropper = messageDropper;
        _watchUIUpdate = WatchUIUpdate.Instance;
    }
    
    public UniTask<BattleSetupResult> InitializeBattle(
        IReadOnlyList<NormalEnemy> enemies,
        int nowProgress,
        IPlayersParty playersParty,
        IPlayersProgress playersProgress,
        IPlayersUIControl playersUIControl,
        IPlayersSkillUI playersSkillUI,
        IPlayersRoster playersRoster,
        IPlayersTuning playersTuning,
        float escapeRate,
        int enemyNumber = 2,
        IBattleMetaProvider metaProviderOverride = null)
    {
        var enemyGroup = EncounterEnemySelector.SelectGroup(enemies, nowProgress, enemyNumber);
        return InitializeBattleFromGroup(
            enemyGroup,
            playersParty,
            playersProgress,
            playersUIControl,
            playersSkillUI,
            playersRoster,
            playersTuning,
            escapeRate,
            metaProviderOverride);
    }

    private async UniTask<BattleSetupResult> InitializeBattleFromGroup(
        BattleGroup enemyGroup,
        IPlayersParty playersParty,
        IPlayersProgress playersProgress,
        IPlayersUIControl playersUIControl,
        IPlayersSkillUI playersSkillUI,
        IPlayersRoster playersRoster,
        IPlayersTuning playersTuning,
        float escapeRate,
        IBattleMetaProvider metaProviderOverride)
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

        var metaProvider = metaProviderOverride ?? new PlayersStatesBattleMetaProvider(playersProgress, playersParty, playersUIControl);
        result.Orchestrator = new BattleOrchestrator(
            result.AllyGroup,
            result.EnemyGroup,
            BattleStartSituation.Normal,
            _messageDropper,
            escapeRate,
            metaProvider,
            playersSkillUI,
            playersRoster
        );
        BattleOrchestratorHub.Set(result.Orchestrator);
        result.BattleContext = result.Orchestrator.Manager;

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
            await _watchUIUpdate.FirstImpressionZoomImproved();
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
    
    /// <summary>
    /// 戦闘UI の初期状態を設定
    /// </summary>
    public TabState SetupInitialBattleUI(BattleOrchestrator orchestrator)
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
}
