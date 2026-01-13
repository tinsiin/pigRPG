using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

public sealed class PlayersUIFacade : IPlayersUIControl, IPlayersSkillUI
{
    public event Action<bool> AllyAlliesUISetActiveRequested;
    public event Action<SkillZoneTrait, SkillType, AllyId> OnlySelectActsRequested;
    public event Action<AllyId> OnSkillSelectionScreenTransitionRequested;
    public event Func<List<BaseSkill>, int, UniTask<List<BaseSkill>>> SelectSkillPassiveTargetRequested;
    public event Action ReturnSelectSkillPassiveTargetRequested;
    public event Action<AllyId> OpenEmotionalAttachmentSkillSelectUIAreaRequested;
    public event Action OnBattleStartRequested;
    public event Action<AllyId> GoToCancelPassiveFieldRequested;
    public event Action<AllyId> ReturnCancelPassiveToDefaultAreaRequested;

    public void AllyAlliesUISetActive(bool isActive)
    {
        AllyAlliesUISetActiveRequested?.Invoke(isActive);
    }

    public void OnlySelectActs(SkillZoneTrait trait, SkillType type, AllyId allyId)
    {
        OnlySelectActsRequested?.Invoke(trait, type, allyId);
    }

    public void OnSkillSelectionScreenTransition(AllyId allyId)
    {
        OnSkillSelectionScreenTransitionRequested?.Invoke(allyId);
    }

    public UniTask<List<BaseSkill>> GoToSelectSkillPassiveTargetSkillButtonsArea(List<BaseSkill> skills, int selectCount)
    {
        var handler = SelectSkillPassiveTargetRequested;
        if (handler == null)
        {
            Debug.LogError("PlayersUIFacade: SelectSkillPassiveTargetRequested is not bound.");
            return UniTask.FromResult(new List<BaseSkill>());
        }
        return handler.Invoke(skills, selectCount);
    }

    public void ReturnSelectSkillPassiveTargetSkillButtonsArea()
    {
        ReturnSelectSkillPassiveTargetRequested?.Invoke();
    }

    public void OpenEmotionalAttachmentSkillSelectUIArea(AllyId allyId)
    {
        OpenEmotionalAttachmentSkillSelectUIAreaRequested?.Invoke(allyId);
    }

    public void OnBattleStart()
    {
        OnBattleStartRequested?.Invoke();
    }

    public void GoToCancelPassiveField(AllyId allyId)
    {
        GoToCancelPassiveFieldRequested?.Invoke(allyId);
    }

    public void ReturnCancelPassiveToDefaultArea(AllyId allyId)
    {
        ReturnCancelPassiveToDefaultAreaRequested?.Invoke(allyId);
    }
}
