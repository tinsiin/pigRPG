using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

public sealed class PlayersUIFacade : IPlayersUIControl, IPlayersSkillUI
{
    public event Action<bool> AllyAlliesUISetActiveRequested;
    public event Action<SkillZoneTrait, SkillType, int> OnlySelectActsRequested;
    public event Action<int> OnSkillSelectionScreenTransitionRequested;
    public event Func<List<BaseSkill>, int, UniTask<List<BaseSkill>>> SelectSkillPassiveTargetRequested;
    public event Action ReturnSelectSkillPassiveTargetRequested;
    public event Action<int> OpenEmotionalAttachmentSkillSelectUIAreaRequested;
    public event Action OnBattleStartRequested;
    public event Action<int> GoToCancelPassiveFieldRequested;
    public event Action<int> ReturnCancelPassiveToDefaultAreaRequested;

    public void AllyAlliesUISetActive(bool isActive)
    {
        AllyAlliesUISetActiveRequested?.Invoke(isActive);
    }

    public void OnlySelectActs(SkillZoneTrait trait, SkillType type, int index)
    {
        OnlySelectActsRequested?.Invoke(trait, type, index);
    }

    public void OnSkillSelectionScreenTransition(int index)
    {
        OnSkillSelectionScreenTransitionRequested?.Invoke(index);
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

    public void OpenEmotionalAttachmentSkillSelectUIArea(int index)
    {
        OpenEmotionalAttachmentSkillSelectUIAreaRequested?.Invoke(index);
    }

    public void OnBattleStart()
    {
        OnBattleStartRequested?.Invoke();
    }

    public void GoToCancelPassiveField(int index)
    {
        GoToCancelPassiveFieldRequested?.Invoke(index);
    }

    public void ReturnCancelPassiveToDefaultArea(int index)
    {
        ReturnCancelPassiveToDefaultAreaRequested?.Invoke(index);
    }
}
