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

    // Target list
    public UnderActersEntryList Unders { get; private set; }

    // Current action state
    public BaseStates Acter { get; set; }
    public Faction ActerFaction { get; set; }
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
    public IBattleQueryService QueryService { get; }
    public TargetingService Targeting { get; }
    public EffectResolver Effects { get; }
    public IBattleRandom Random { get; }
    public IBattleLogger Logger { get; }

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
    public BattleActionContext(
        BattleGroup allyGroup,
        BattleGroup enemyGroup,
        BattleStateManager stateManager,
        TurnScheduler turnScheduler,
        ActionQueue acts,
        TargetingService targeting,
        EffectResolver effects,
        IBattleRandom random,
        IBattleLogger logger)
    {
        AllyGroup = allyGroup ?? throw new ArgumentNullException(nameof(allyGroup));
        EnemyGroup = enemyGroup ?? throw new ArgumentNullException(nameof(enemyGroup));
        StateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
        TurnScheduler = turnScheduler ?? throw new ArgumentNullException(nameof(turnScheduler));
        Acts = acts ?? throw new ArgumentNullException(nameof(acts));
        QueryService = new BattleQueryService(allyGroup, enemyGroup);
        Targeting = targeting ?? throw new ArgumentNullException(nameof(targeting));
        Effects = effects ?? throw new ArgumentNullException(nameof(effects));
        Random = random ?? throw new ArgumentNullException(nameof(random));
        Logger = logger ?? new NoOpBattleLogger();
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
    public Faction GetCharacterFaction(BaseStates chara) => QueryService.GetCharacterFaction(chara);

    /// <summary>
    /// 陣営からグループを取得
    /// </summary>
    public BattleGroup FactionToGroup(Faction faction) => QueryService.FactionToGroup(faction);

    /// <summary>
    /// キャラクターが属するグループを取得
    /// </summary>
    public BattleGroup GetGroupForCharacter(BaseStates chara) => QueryService.GetGroupForCharacter(chara);

    /// <summary>
    /// キャラクターが前のめり状態かどうか
    /// </summary>
    public bool IsVanguard(BaseStates chara) => QueryService.IsVanguard(chara);

    /// <summary>
    /// 同じグループの生存している他のキャラクターを取得
    /// </summary>
    public List<BaseStates> GetOtherAlliesAlive(BaseStates chara) => QueryService.GetOtherAlliesAlive(chara);

    /// <summary>
    /// 2つのキャラクターが同じ陣営かどうか
    /// </summary>
    public bool IsFriend(BaseStates chara1, BaseStates chara2) => QueryService.IsFriend(chara1, chara2);

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

    /// <summary>
    /// Unders をリセット
    /// </summary>
    public void ResetUnders()
    {
        Unders = new UnderActersEntryList(QueryService);
    }

    // 前のめり処理のコールバック（BattleManager から設定）
    private System.Action<BaseStates> _beVanguardHandler;

    /// <summary>
    /// BeVanguard ハンドラを設定
    /// </summary>
    public void SetBeVanguardHandler(System.Action<BaseStates> handler)
    {
        _beVanguardHandler = handler;
    }

    /// <summary>
    /// 前のめり状態にする
    /// </summary>
    public void BeVanguard(BaseStates newVanguard)
    {
        _beVanguardHandler?.Invoke(newVanguard);
    }
}
