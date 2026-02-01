using UnityEngine;

public sealed class TurnExecutor
{
    private readonly BattleManager _manager;

    public TurnExecutor(BattleManager manager)
    {
        _manager = manager;
    }

    public TabState ACTPop()
    {
        _manager.UiBridge.DisplayLogs();
        _manager.ResetManagerTemp();
        _manager.TurnScheduler.RemoveDeadReservations();

        if (_manager.AllyGroup.PartyDeathOnBattle())
        {
            _manager.ActerFactionValue = allyOrEnemy.alliy;
            _manager.StateManager.MarkWipeout();
            return TabState.NextWait;
        }
        if (_manager.EnemyGroup.PartyDeathOnBattle())
        {
            _manager.ActerFactionValue = allyOrEnemy.Enemyiy;
            _manager.StateManager.MarkWipeout();
            return TabState.NextWait;
        }
        if (_manager.EnemyGroup.Ours.Count == 0)
        {
            _manager.StateManager.MarkEnemyGroupEmpty();
            return TabState.NextWait;
        }
        if (_manager.AlliesRunOut)
        {
            return TabState.NextWait;
        }
        if (_manager.DominoRunOutEnemies.Count > 0)
        {
            return TabState.NextWait;
        }

        CharacterAddFromListOrRandom();

        if (_manager.VoidTurn)
        {
            _manager.VoidTurn = false;
            _manager.NextTurn(false);
            return ACTPop();
        }

        _manager.UiBridge.MoveActionMarkToActorScaled(_manager.Acter, false, true);
        if (_manager.IsRater)
        {
            return TabState.NextWait;
        }
        if (_manager.Acter == null)
        {
            _manager.ActSkipBecauseNobodyAct = true;
            return TabState.NextWait;
        }

        var isFreezeByPassives = _manager.Acter.IsFreezeByPassives;
        var hasCanCancelCantACTPassive = _manager.Acter.HasCanCancelCantACTPassive;

        if (_manager.CurrentActerFaction == allyOrEnemy.alliy)
        {
            Debug.Log(_manager.Acter.CharacterName + "(主人公キャラ)は行動する");
            _manager.UiBridge.SetSelectedActor(_manager.Acter);

            if (!_manager.Acter.IsFreeze)
            {
                Debug.Log(_manager.Acter.CharacterName + "主人公キャラの強制続行スキルがないのでスキル選択へのパッシブ判定処理へと進みます。");
                if (!isFreezeByPassives || hasCanCancelCantACTPassive)
                {
                    var hasSingleTargetReservation = _manager.Acts.TryPeek(out var entry) && entry.SingleTarget != null;
                    _manager.UiBridge.SwitchAllySkillUiState(_manager.Acter, hasSingleTargetReservation);
                    Debug.Log(_manager.Acter.CharacterName + "(主人公キャラ)はスキル選択");
                    return TabState.Skill;
                }

                _manager.DoNothing = true;
            }
            else
            {
                if (_manager.Acter.IsDeleteMyFreezeConsecutive)
                {
                    _manager.Acter.DeleteConsecutiveATK();
                    _manager.DoNothing = true;
                    Debug.Log(_manager.Acter.CharacterName + "（主人公キャラ）は何もしない");
                    return TabState.NextWait;
                }

                var skill = _manager.Acter.FreezeUseSkill;
                _manager.Acter.RangeWill = _manager.Acter.FreezeRangeWill;
                _manager.Acter.NowUseSkill = skill;

                if (skill.NowConsecutiveATKFromTheSecondTimeOnward()
                    && skill.HasConsecutiveType(SkillConsecutiveType.CanOprate))
                {
                    Debug.Log(_manager.Acter.CharacterName + "（主人公キャラ）は連続攻撃中の操作へ");
                    return AllyClass.DetermineNextUIState(skill);
                }
            }
        }

        if (_manager.CurrentActerFaction == allyOrEnemy.Enemyiy)
        {
            var ene = _manager.Acter as NormalEnemy;
            if (isFreezeByPassives && !hasCanCancelCantACTPassive)
            {
                _manager.DoNothing = true;
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
        if (_manager.Acts.Count > 0)
        {
            _manager.Acts.RemoveAt(0);
        }

        if (next)
        {
            _manager.AllyGroup.PartyApplyConditionChangeOnCloseAllyDeath();
            _manager.EnemyGroup.PartyApplyConditionChangeOnCloseAllyDeath();

            _manager.IncrementBattleTurnCount();

            foreach (var chara in _manager.AllCharacters)
            {
                chara._tempVanguard = _manager.IsVanguard(chara);
                chara.TargetBonusDatas.AllDecrementDurationTurn();
            }
            _manager.AllyGroup.OnPartyNextTurnNoArgument();
            _manager.EnemyGroup.OnPartyNextTurnNoArgument();
        }

        _manager.AllyGroup.VanGuardDeath();
        _manager.EnemyGroup.VanGuardDeath();
        _manager.Acter.IsActiveCancelInSkillACT = false;
    }

    private void CharacterAddFromListOrRandom()
    {
        if (_manager.Acts.TryPeek(out var entry))
        {
            _manager.SetUniqueTopMessage(entry?.Message ?? "");
            _manager.Acter = entry?.Actor;
            _manager.ActerFactionValue = entry != null ? entry.Faction : allyOrEnemy.alliy;

            var modList = entry?.Modifiers;
            if (modList != null)
            {
                foreach (var mod in modList)
                {
                    _manager.Acter.CopySpecialModifier(mod);
                }
            }

            var singleTarget = entry?.SingleTarget;
            if (singleTarget != null && singleTarget.Death())
            {
                _manager.VoidTurn = true;
            }

            var ratherTarget = entry?.RatherTargets;
            if (ratherTarget != null)
            {
                _manager.PrepareRatherAct(ratherTarget, entry != null ? entry.RatherDamage : 0f);
            }

            if (entry != null && entry.Freeze)
            {
                _manager.Acter.FreezeSkill();
            }

            _manager.Acter.SetExCounterDEFATK(entry != null ? entry.ExCounterDEFATK : -1f);

            Debug.Log("俳優は先約リストから選ばれました");
        }
        else
        {
            _manager.Acter = _manager.TurnScheduler.SelectRandomActer(out var faction);
            _manager.ActerFactionValue = faction;
            Debug.Log("俳優はランダムに選ばれました");
        }
    }
}
