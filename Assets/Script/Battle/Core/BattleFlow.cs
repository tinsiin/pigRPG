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

    // BattleManager への参照（移行期間用、将来的に削除）
    private readonly BattleManager _manager;

    public BattleFlow(
        BattleManager manager,
        BattleActionContext context,
        BattlePresentation presentation,
        BattleUIBridge uiBridge,
        TurnExecutor turnExecutor,
        SkillExecutor skillExecutor)
    {
        _manager = manager;
        _context = context;
        _presentation = presentation;
        _uiBridge = uiBridge;
        _turnExecutor = turnExecutor;
        _skillExecutor = skillExecutor;
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
        return _manager.EscapeACT();
    }

    /// <summary>
    /// 連鎖逃走を実行
    /// </summary>
    public TabState DominoEscapeAct()
    {
        return _manager.DominoEscapeACT();
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
        return _manager.DialogEndACT();
    }

    /// <summary>
    /// キャラクターの行動分岐
    /// </summary>
    public async UniTask<TabState> CharacterActBranchingAsync()
    {
        return await _manager.CharacterActBranching();
    }

    // === Private helper methods ===

    private void BeVanguardTriggerAct()
    {
        var skill = _context.Acter.NowUseSkill;
        if (skill != null && skill.IsReadyTriggerAgressiveCommit)
        {
            _manager.BeVanguard(_context.Acter);
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
