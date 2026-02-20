using System;
using Cysharp.Threading.Tasks;

/// <summary>
/// 戦闘進行を担当するクラス。
/// ターン進行、勝敗判定、ステート遷移を管理する。
/// </summary>
public sealed class BattleFlow
{
    public event Action BattleStarted;
    public event Action<int> TurnAdvanced;
    public event Action BattleEnded;

    private readonly BattleActionContext _context;
    private readonly BattlePresentation _presentation;
    private readonly BattleEventBus _eventBus;
    private readonly TurnExecutor _turnExecutor;
    private readonly SkillExecutor _skillExecutor;
    private readonly EscapeHandler _escapeHandler;
    private readonly ActionSkipExecutor _actionSkipExecutor;
    private readonly IBattleMetaProvider _metaProvider;

    // OnBattleEnd 用のコールバック（BattleManager から設定）
    private Func<UniTask> _onBattleEndCallback;

    public BattleFlow(
        BattleActionContext context,
        BattlePresentation presentation,
        TurnExecutor turnExecutor,
        SkillExecutor skillExecutor,
        EscapeHandler escapeHandler,
        ActionSkipExecutor actionSkipExecutor,
        IBattleMetaProvider metaProvider,
        BattleEventBus eventBus)
    {
        _context = context;
        _presentation = presentation;
        _turnExecutor = turnExecutor;
        _skillExecutor = skillExecutor;
        _escapeHandler = escapeHandler;
        _actionSkipExecutor = actionSkipExecutor;
        _metaProvider = metaProvider;
        _eventBus = eventBus;
        _turnExecutor.TurnAdvanced += count =>
        {
            TurnAdvanced?.Invoke(count);
            _eventBus?.Publish(BattleEvent.TurnAdvanced(count));
        };
    }

    /// <summary>
    /// OnBattleEnd コールバックを設定
    /// </summary>
    public void SetOnBattleEndCallback(Func<UniTask> callback)
    {
        _onBattleEndCallback = callback;
    }

    /// <summary>
    /// 次のアクターを選択し、適切な状態を返す（ACTPop相当）
    /// </summary>
    public TabState SelectNextActor()
    {
        return _turnExecutor.ACTPop();
    }

    public void NotifyBattleStarted()
    {
        BattleStarted?.Invoke();
        _eventBus?.Publish(BattleEvent.Started(_context.BattleTurnCount));
    }

    /// <summary>
    /// ターンを進める
    /// </summary>
    public void NextTurn(bool advance)
    {
        _turnExecutor.NextTurn(advance);
    }

    /// <summary>
    /// スキルを実行する（SkillACT相当）
    /// </summary>
    public async UniTask<TabState> ExecuteSkillAsync()
    {
        return await _skillExecutor.SkillACT();
    }

    /// <summary>
    /// 発動カウントを実行
    /// </summary>
    public TabState TriggerAct(int count)
    {
        _context.Logger.Log("発動カウント実行");
        var acter = _context.Acter;
        var skill = acter.NowUseSkill;

        if (skill != null && skill.CanCancelTrigger == false)
        {
            acter.FreezeSkill();
        }

        BeVanguardTriggerAct();
        OtherSkillsTriggerRollBack();

        _presentation.CreateBattleMessage($"{skill.SkillName}の発動カウント！残り{count}回。");
        NextTurn(true);

        return SelectNextActor();
    }

    /// <summary>
    /// 逃走を実行
    /// </summary>
    public TabState EscapeAct()
    {
        return _escapeHandler.EscapeACT();
    }

    /// <summary>
    /// 連鎖逃走を実行
    /// </summary>
    public TabState DominoEscapeAct()
    {
        return _escapeHandler.DominoEscapeACT();
    }

    /// <summary>
    /// レイザーダメージを実行
    /// </summary>
    public TabState RatherAct()
    {
        var (targets, damage) = _context.ConsumeRatherAct();
        _context.Effects.ApplyRatherDamage(targets, damage);
        NextTurn(true);
        return SelectNextActor();
    }

    /// <summary>
    /// 戦闘終了メッセージを表示
    /// </summary>
    public TabState DialogEndAct()
    {
        if (_context.Wipeout)
        {
            if (_context.ActerFaction == Faction.Ally)
            {
                _eventBus?.Publish(BattleEvent.MessageOnly("死んだ", true, _context.BattleTurnCount));
                _metaProvider?.OnPlayersLost();
                _context.EnemyGroup.EnemyiesOnWin();
            }
            else
            {
                _eventBus?.Publish(BattleEvent.MessageOnly("勝ち抜いた", true, _context.BattleTurnCount));
                _metaProvider?.OnPlayersWin();
            }
        }
        if (_context.AlliesRunOut)
        {
            _eventBus?.Publish(BattleEvent.MessageOnly("我々は逃げた", true, _context.BattleTurnCount));
            _metaProvider?.OnPlayersRunOut();
            _context.EnemyGroup.EnemiesOnAllyRunOut();
        }
        if (_context.EnemyGroupEmpty)
        {
            _eventBus?.Publish(BattleEvent.MessageOnly("敵はいなくなった", true, _context.BattleTurnCount));
            _metaProvider?.OnPlayersWin();
        }

        _eventBus?.Publish(BattleEvent.Ended(_context.BattleTurnCount));
        BattleEnded?.Invoke();
        if (_onBattleEndCallback != null)
        {
            _onBattleEndCallback.Invoke().Forget();
        }
        return TabState.walk;
    }

    /// <summary>
    /// キャラクターの行動分岐
    /// </summary>
    public async UniTask<TabState> CharacterActBranchingAsync()
    {
        _context.Logger.Log("俳優の行動の分岐-NextWaitボタンが押されました。");
        _eventBus?.Publish(BattleEvent.UiNextArrow());
        if (_context.IsRather)
        {
            _context.IsRather = false;
            return RatherAct();
        }
        if (_context.Acter == null)
        {
            _context.Logger.LogError("俳優が認識されていない-エンカウントロジックなどに問題あり");
            return TabState.walk;
        }
        var skill = _context.Acter.NowUseSkill;
        if (skill == null && !_context.DoNothing)
        {
            _context.Logger.LogError($"NowUseSkillがnullです。俳優:{_context.Acter.CharacterName} の行動をスキップします。");
            return _actionSkipExecutor.DoNothingACT();
        }
        var isEscape = _context.Acter.SelectedEscape;

        if (_context.Wipeout || _context.AlliesRunOut || _context.EnemyGroupEmpty)
        {
            _eventBus?.Publish(BattleEvent.LogOnly("全滅か主人公達逃走かでダイアログ終了アクトへ", true, _context.BattleTurnCount));
            return DialogEndAct();
        }
        if (_context.DominoRunOutEnemies.Count > 0)
        {
            return _escapeHandler.DominoEscapeACT();
        }

        if (_context.SkillStock)
        {
            return _actionSkipExecutor.SkillStockACT();
        }
        if (_context.PassiveCancel)
        {
            return _actionSkipExecutor.PassiveCancelACT();
        }

        if (_context.DoNothing)
        {
            return _actionSkipExecutor.DoNothingACT();
        }
        if (isEscape)
        {
            return _escapeHandler.EscapeACT();
        }
        if (_context.ActSkipBecauseNobodyAct)
        {
            _context.ActSkipBecauseNobodyAct = false;
            NextTurn(true);
            return SelectNextActor();
        }

        var count = skill.TrigerCount();
        if (count >= 0)
        {
            return TriggerAct(count);
        }

        skill.ReturnTrigger();

        if (CheckPassivesSkillActivation())
        {
            return await ExecuteSkillAsync();
        }
        return _actionSkipExecutor.DoNothingACT();
    }

    // === Private helper methods ===

    private void BeVanguardTriggerAct()
    {
        var skill = _context.Acter.NowUseSkill;
        if (skill != null && skill.AggressiveOnTrigger.isAggressiveCommit)
        {
            _context.BeVanguard(_context.Acter);
        }
    }

    private void OtherSkillsTriggerRollBack()
    {
        foreach (var skill in _context.Acter.SkillList)
        {
            if (skill.IsTriggering)
            {
                skill.RollBackTrigger();
            }
        }
    }

    private bool CheckPassivesSkillActivation()
    {
        var acter = _context.Acter;
        if (acter == null) return false;
        return RollPercent(acter.PassivesSkillActivationRate());
    }

    private bool RollPercent(float percentage)
    {
        if (percentage < 0) percentage = 0;
        return _context.Random.NextFloat(100) < percentage;
    }
}
