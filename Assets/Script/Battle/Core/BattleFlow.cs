using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 戦闘進行を担当するクラス。
/// ターン進行、勝敗判定、ステート遷移を管理する。
/// </summary>
public sealed class BattleFlow
{
    private readonly BattleActionContext _context;
    private readonly BattlePresentation _presentation;
    private readonly BattleUIBridge _uiBridge;
    private readonly TurnExecutor _turnExecutor;
    private readonly SkillExecutor _skillExecutor;
    private readonly EscapeHandler _escapeHandler;
    private readonly CharacterActExecutor _characterActExecutor;
    private readonly IBattleMetaProvider _metaProvider;

    // OnBattleEnd 用のコールバック（BattleManager から設定）
    private Func<UniTask> _onBattleEndCallback;

    public BattleFlow(
        BattleActionContext context,
        BattlePresentation presentation,
        BattleUIBridge uiBridge,
        TurnExecutor turnExecutor,
        SkillExecutor skillExecutor,
        EscapeHandler escapeHandler,
        CharacterActExecutor characterActExecutor,
        IBattleMetaProvider metaProvider)
    {
        _context = context;
        _presentation = presentation;
        _uiBridge = uiBridge;
        _turnExecutor = turnExecutor;
        _skillExecutor = skillExecutor;
        _escapeHandler = escapeHandler;
        _characterActExecutor = characterActExecutor;
        _metaProvider = metaProvider;
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
        Debug.Log("発動カウント実行");
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
            if (_context.ActerFaction == allyOrEnemy.alliy)
            {
                _uiBridge.PushMessage("死んだ");
                _metaProvider?.OnPlayersLost();
                _context.EnemyGroup.EnemyiesOnWin();
            }
            else
            {
                _uiBridge.PushMessage("勝ち抜いた");
                _metaProvider?.OnPlayersWin();
            }
        }
        if (_context.AlliesRunOut)
        {
            _uiBridge.PushMessage("我々は逃げた");
            _metaProvider?.OnPlayersRunOut();
            _context.EnemyGroup.EnemiesOnAllyRunOut();
        }
        if (_context.EnemyGroupEmpty)
        {
            _uiBridge.PushMessage("敵はいなくなった");
            _metaProvider?.OnPlayersWin();
        }

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
        return await _characterActExecutor.CharacterActBranchingAsync();
    }

    // === Private helper methods ===

    private void BeVanguardTriggerAct()
    {
        var skill = _context.Acter.NowUseSkill;
        if (skill != null && skill.IsReadyTriggerAgressiveCommit)
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
}
