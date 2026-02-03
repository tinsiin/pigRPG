using UnityEngine;

public sealed class TurnExecutor
{
    private readonly BattleManager _manager;
    private readonly BattleActionContext _context;
    private readonly BattlePresentation _presentation;
    private readonly BattleUIBridge _uiBridge;

    public TurnExecutor(
        BattleManager manager,
        BattleActionContext context,
        BattlePresentation presentation,
        BattleUIBridge uiBridge)
    {
        _manager = manager;
        _context = context;
        _presentation = presentation;
        _uiBridge = uiBridge;
    }

    public TabState ACTPop()
    {
        _uiBridge.DisplayLogs();
        _presentation.ResetTopMessage();
        _context.TurnScheduler.RemoveDeadReservations();

        if (_context.AllyGroup.PartyDeathOnBattle())
        {
            _context.ActerFaction = allyOrEnemy.alliy;
            _context.StateManager.MarkWipeout();
            return TabState.NextWait;
        }
        if (_context.EnemyGroup.PartyDeathOnBattle())
        {
            _context.ActerFaction = allyOrEnemy.Enemyiy;
            _context.StateManager.MarkWipeout();
            return TabState.NextWait;
        }
        if (_context.EnemyGroup.Ours.Count == 0)
        {
            _context.StateManager.MarkEnemyGroupEmpty();
            return TabState.NextWait;
        }
        if (_context.AlliesRunOut)
        {
            return TabState.NextWait;
        }
        if (_context.DominoRunOutEnemies.Count > 0)
        {
            return TabState.NextWait;
        }

        CharacterAddFromListOrRandom();

        if (_context.VoidTurn)
        {
            _context.VoidTurn = false;
            NextTurn(false);
            return ACTPop();
        }

        _uiBridge.MoveActionMarkToActorScaled(_context.Acter, false, true);
        if (_context.IsRather)
        {
            return TabState.NextWait;
        }
        if (_context.Acter == null)
        {
            _context.ActSkipBecauseNobodyAct = true;
            return TabState.NextWait;
        }

        var isFreezeByPassives = _context.Acter.IsFreezeByPassives;
        var hasCanCancelCantACTPassive = _context.Acter.HasCanCancelCantACTPassive;

        if (_context.ActerFaction == allyOrEnemy.alliy)
        {
            Debug.Log(_context.Acter.CharacterName + "(主人公キャラ)は行動する");
            _uiBridge.SetSelectedActor(_context.Acter);

            if (!_context.Acter.IsFreeze)
            {
                Debug.Log(_context.Acter.CharacterName + "主人公キャラの強制続行スキルがないのでスキル選択へのパッシブ判定処理へと進みます。");
                if (!isFreezeByPassives || hasCanCancelCantACTPassive)
                {
                    var hasSingleTargetReservation = _context.Acts.TryPeek(out var entry) && entry.SingleTarget != null;
                    _uiBridge.SwitchAllySkillUiState(_context.Acter, hasSingleTargetReservation);
                    Debug.Log(_context.Acter.CharacterName + "(主人公キャラ)はスキル選択");
                    return TabState.Skill;
                }

                _context.DoNothing = true;
            }
            else
            {
                if (_context.Acter.IsDeleteMyFreezeConsecutive)
                {
                    _context.Acter.DeleteConsecutiveATK();
                    _context.DoNothing = true;
                    Debug.Log(_context.Acter.CharacterName + "（主人公キャラ）は何もしない");
                    return TabState.NextWait;
                }

                var skill = _context.Acter.FreezeUseSkill;
                _context.Acter.RangeWill = _context.Acter.FreezeRangeWill;
                _context.Acter.NowUseSkill = skill;

                if (skill.NowConsecutiveATKFromTheSecondTimeOnward()
                    && skill.HasConsecutiveType(SkillConsecutiveType.CanOprate))
                {
                    Debug.Log(_context.Acter.CharacterName + "（主人公キャラ）は連続攻撃中の操作へ");
                    return AllyClass.DetermineNextUIState(skill);
                }
            }
        }

        if (_context.ActerFaction == allyOrEnemy.Enemyiy)
        {
            var ene = _context.Acter as NormalEnemy;
            if (isFreezeByPassives && !hasCanCancelCantACTPassive)
            {
                _context.DoNothing = true;
            }
            else
            {
                ene.SkillAI();
            }
        }

        return TabState.NextWait;
    }

    public void NextTurn(bool next)
    {
        if (_context.Acts.Count > 0)
        {
            _context.Acts.RemoveAt(0);
        }

        if (next)
        {
            _context.AllyGroup.PartyApplyConditionChangeOnCloseAllyDeath();
            _context.EnemyGroup.PartyApplyConditionChangeOnCloseAllyDeath();

            _context.IncrementTurnCount();

            foreach (var chara in _context.GetAllCharacters())
            {
                chara._tempVanguard = _context.IsVanguard(chara);
                chara.TargetBonusDatas.AllDecrementDurationTurn();
            }
            _context.AllyGroup.OnPartyNextTurnNoArgument();
            _context.EnemyGroup.OnPartyNextTurnNoArgument();
        }

        _context.AllyGroup.VanGuardDeath();
        _context.EnemyGroup.VanGuardDeath();
        _context.Acter.IsActiveCancelInSkillACT = false;
    }

    private void CharacterAddFromListOrRandom()
    {
        if (_context.Acts.TryPeek(out var entry))
        {
            _presentation.SetTopMessage(entry?.Message ?? "");
            _context.Acter = entry?.Actor;
            _context.ActerFaction = entry != null ? entry.Faction : allyOrEnemy.alliy;

            var modList = entry?.Modifiers;
            if (modList != null)
            {
                foreach (var mod in modList)
                {
                    _context.Acter.CopySpecialModifier(mod);
                }
            }

            var singleTarget = entry?.SingleTarget;
            if (singleTarget != null && singleTarget.Death())
            {
                _context.VoidTurn = true;
            }

            var ratherTarget = entry?.RatherTargets;
            if (ratherTarget != null)
            {
                _context.PrepareRatherAct(ratherTarget, entry != null ? entry.RatherDamage : 0f);
            }

            if (entry != null && entry.Freeze)
            {
                _context.Acter.FreezeSkill();
            }

            _context.Acter.SetExCounterDEFATK(entry != null ? entry.ExCounterDEFATK : -1f);

            Debug.Log("俳優は先約リストから選ばれました");
        }
        else
        {
            _context.Acter = _context.TurnScheduler.SelectRandomActer(out var faction);
            _context.ActerFaction = faction;
            Debug.Log("俳優はランダムに選ばれました");
        }
    }
}
