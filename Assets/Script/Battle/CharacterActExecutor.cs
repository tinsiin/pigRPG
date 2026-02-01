using Cysharp.Threading.Tasks;
using UnityEngine;

public sealed class CharacterActExecutor
{
    private readonly BattleManager _manager;

    public CharacterActExecutor(BattleManager manager)
    {
        _manager = manager;
    }

    public async UniTask<TabState> CharacterActBranchingAsync()
    {
        Debug.Log("俳優の行動の分岐-NextWaitボタンが押されました。");
        _manager.UiBridge.NextArrow();
        if (_manager.IsRater)
        {
            _manager.IsRater = false;
            return _manager.RatherACT();
        }
        if (_manager.Acter == null)
        {
            Debug.LogError("俳優が認識されていない-エンカウントロジックなどに問題あり");
            return TabState.walk;
        }
        var skill = _manager.Acter.NowUseSkill;
        if (skill == null && !_manager.DoNothing)
        {
            Debug.LogError($"NowUseSkillがnullです。俳優:{_manager.Acter.CharacterName} の行動をスキップします。");
            return _manager.DoNothingACT();
        }
        var isEscape = _manager.Acter.SelectedEscape;

        if (_manager.Wipeout || _manager.AlliesRunOut || _manager.EnemyGroupEmpty)
        {
            _manager.UiBridge.AddLog("全滅か主人公達逃走かでダイアログ終了アクトへ", true);
            return _manager.DialogEndACT();
        }
        if (_manager.DominoRunOutEnemies.Count > 0)
        {
            return _manager.DominoEscapeACT();
        }

        if (_manager.SkillStock)
        {
            return _manager.SkillStockACT();
        }
        if (_manager.PassiveCancel)
        {
            return _manager.PassiveCancelACT();
        }

        if (_manager.DoNothing)
        {
            return _manager.DoNothingACT();
        }
        if (isEscape)
        {
            return _manager.EscapeACT();
        }
        if (_manager.ActSkipBecauseNobodyAct)
        {
            _manager.ActSkipBecauseNobodyAct = false;
            _manager.NextTurn(true);
            return _manager.ACTPop();
        }

        var count = skill.TrigerCount();
        if (count >= 0)
        {
            return _manager.TriggerACT(count);
        }

        skill.ReturnTrigger();

        if (_manager.CheckPassivesSkillActivation())
        {
            return await _manager.SkillACT();
        }
        return _manager.DoNothingACT();
    }
}
