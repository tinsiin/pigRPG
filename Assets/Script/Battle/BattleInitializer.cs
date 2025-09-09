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
    
    /// <summary>
    /// 戦闘を初期化し、BattleManagerを生成
    /// </summary>
    public async UniTask<BattleSetupResult> InitializeBattle(
        StageCut nowStageCut,
        PlayersStates playersStates,
        int enemyNumber = 2)
    {
        var result = new BattleSetupResult();
        
        // 敵グループ生成
        result.EnemyGroup = nowStageCut.EnemyCollectAI(enemyNumber);
        if (result.EnemyGroup == null)
        {
            result.EncounterOccurred = false;
            return result;
        }
        
        result.EncounterOccurred = true;
        
        // 味方グループ選出
        result.AllyGroup = DetermineAllyGroup(result.EnemyGroup, playersStates);
        
        // BattleManager生成
        result.BattleManager = new BattleManager(
            result.AllyGroup, 
            result.EnemyGroup,
            BattleStartSituation.Normal, 
            _messageDropper
        );
        
        // BattleTimeLine生成
        result.TimeLine = new BattleTimeLine(new List<BattleManager> { result.BattleManager });
        
        // プレイヤー状態を戦闘開始に更新
        PlayersStates.Instance.OnBattleStart();
        
        // ズーム演出実行
        if (_watchUIUpdate != null)
        {
            await _watchUIUpdate.FirstImpressionZoomImproved();
        }
        
        return result;
    }
    
    /// <summary>
    /// 敵グループに応じた味方グループを決定
    /// </summary>
    private BattleGroup DetermineAllyGroup(BattleGroup enemyGroup, PlayersStates playersStates)
    {
        // 将来的な拡張ポイント：
        // 敵グループの構成によって味方の人選を変更する処理
        // 例：特定の敵には特定のキャラクターのみで戦う
        
        // 現在はフルパーティを返す
        return playersStates.GetParty();
    }
    
    /// <summary>
    /// 戦闘UI の初期状態を設定
    /// </summary>
    public TabState SetupInitialBattleUI(BattleManager battleManager)
    {
        if (battleManager == null)
            return TabState.walk;
        
        return battleManager.ACTPop();
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
    public BattleManager BattleManager { get; set; }
    public BattleTimeLine TimeLine { get; set; }
}