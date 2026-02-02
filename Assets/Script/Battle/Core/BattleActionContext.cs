using System;
using System.Collections.Generic;

/// <summary>
/// 戦闘中の状態を集約するコンテキストクラス。
/// BattleManagerの状態を一元管理し、他クラスからの直接参照を減らす。
/// </summary>
public sealed class BattleActionContext
{
    // Core groups
    public BattleGroup AllyGroup { get; }
    public BattleGroup EnemyGroup { get; }

    // State managers
    public BattleStateManager StateManager { get; }
    public TurnScheduler TurnScheduler { get; }

    // Action queue
    public ActionQueue Acts { get; }

    // Current action state
    public BaseStates Acter { get; set; }
    public allyOrEnemy ActerFaction { get; set; }
    public int BattleTurnCount
    {
        get => StateManager.TurnCount;
        set => StateManager.TurnCount = value;
    }

    // Action flags
    public bool DoNothing { get; set; }
    public bool PassiveCancel { get; set; }
    public bool SkillStock { get; set; }
    public bool VoidTurn { get; set; }
    public bool IsRather { get; set; }
    public bool ActSkipBecauseNobodyAct { get; set; }

    // Rather action state
    private readonly List<BaseStates> _ratherTargetList = new();
    private float _ratherDamageAmount;

    // Services
    public TargetingService Targeting { get; }
    public EffectResolver Effects { get; }

    // State shortcuts (delegate to StateManager)
    public bool Wipeout
    {
        get => StateManager.Wipeout;
        set => StateManager.Wipeout = value;
    }
    public bool EnemyGroupEmpty
    {
        get => StateManager.EnemyGroupEmpty;
        set => StateManager.EnemyGroupEmpty = value;
    }
    public bool AlliesRunOut
    {
        get => StateManager.AlliesRunOut;
        set => StateManager.AlliesRunOut = value;
    }
    public NormalEnemy VoluntaryRunOutEnemy
    {
        get => StateManager.VoluntaryRunOutEnemy;
        set => StateManager.VoluntaryRunOutEnemy = value;
    }
    public List<NormalEnemy> DominoRunOutEnemies => StateManager.DominoRunOutEnemies;

    public BattleActionContext(
        BattleGroup allyGroup,
        BattleGroup enemyGroup,
        BattleStateManager stateManager,
        TurnScheduler turnScheduler,
        ActionQueue acts,
        TargetingService targeting,
        EffectResolver effects)
    {
        AllyGroup = allyGroup ?? throw new ArgumentNullException(nameof(allyGroup));
        EnemyGroup = enemyGroup ?? throw new ArgumentNullException(nameof(enemyGroup));
        StateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
        TurnScheduler = turnScheduler ?? throw new ArgumentNullException(nameof(turnScheduler));
        Acts = acts ?? throw new ArgumentNullException(nameof(acts));
        Targeting = targeting ?? throw new ArgumentNullException(nameof(targeting));
        Effects = effects ?? throw new ArgumentNullException(nameof(effects));
    }

    /// <summary>
    /// 全キャラクターのリストを取得
    /// </summary>
    public List<BaseStates> GetAllCharacters()
    {
        var result = new List<BaseStates>(AllyGroup.Ours);
        result.AddRange(EnemyGroup.Ours);
        return result;
    }

    /// <summary>
    /// キャラクターの陣営を取得
    /// </summary>
    public allyOrEnemy GetCharacterFaction(BaseStates chara)
    {
        foreach (var one in AllyGroup.Ours)
        {
            if (one == chara) return allyOrEnemy.alliy;
        }
        foreach (var one in EnemyGroup.Ours)
        {
            if (one == chara) return allyOrEnemy.Enemyiy;
        }
        return 0;
    }

    /// <summary>
    /// 陣営からグループを取得
    /// </summary>
    public BattleGroup FactionToGroup(allyOrEnemy faction)
    {
        return faction switch
        {
            allyOrEnemy.alliy => AllyGroup,
            allyOrEnemy.Enemyiy => EnemyGroup,
            _ => null
        };
    }

    /// <summary>
    /// キャラクターが属するグループを取得
    /// </summary>
    public BattleGroup GetGroupForCharacter(BaseStates chara)
    {
        return FactionToGroup(GetCharacterFaction(chara));
    }

    /// <summary>
    /// キャラクターが前のめり状態かどうか
    /// </summary>
    public bool IsVanguard(BaseStates chara)
    {
        if (chara == AllyGroup.InstantVanguard) return true;
        if (chara == EnemyGroup.InstantVanguard) return true;
        return false;
    }

    /// <summary>
    /// ターンカウントをインクリメント
    /// </summary>
    public void IncrementTurnCount()
    {
        BattleTurnCount++;
    }

    /// <summary>
    /// レイザーアクトの準備
    /// </summary>
    public void PrepareRatherAct(List<BaseStates> targets, float damage)
    {
        if (targets == null) return;
        _ratherTargetList.AddRange(targets);
        _ratherDamageAmount = damage;
        IsRather = true;
    }

    /// <summary>
    /// レイザーアクトのターゲットを取得してクリア
    /// </summary>
    public (List<BaseStates> targets, float damage) ConsumeRatherAct()
    {
        var targets = new List<BaseStates>(_ratherTargetList);
        var damage = _ratherDamageAmount;
        _ratherTargetList.Clear();
        _ratherDamageAmount = 0;
        IsRather = false;
        return (targets, damage);
    }

    /// <summary>
    /// アクションフラグをリセット
    /// </summary>
    public void ResetActionFlags()
    {
        DoNothing = false;
        PassiveCancel = false;
        SkillStock = false;
    }
}
