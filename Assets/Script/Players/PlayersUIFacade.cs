using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

public sealed class PlayersUIFacade : IPlayersUIControl, IPlayersSkillUI
{
    // === CharacterId ベースのイベント ===
    public event Action<bool> AllyAlliesUISetActiveRequested;
    public event Action<SkillZoneTrait, SkillType, CharacterId> OnlySelectActsRequested;
    public event Action<CharacterId> OnSkillSelectionScreenTransitionRequested;
    public event Func<List<BaseSkill>, int, UniTask<List<BaseSkill>>> SelectSkillPassiveTargetRequested;
    public event Action ReturnSelectSkillPassiveTargetRequested;
    public event Action<CharacterId> OpenEmotionalAttachmentSkillSelectUIAreaRequested;
    public event Action OnBattleStartRequested;
    public event Action<CharacterId> GoToCancelPassiveFieldRequested;
    public event Action<CharacterId> ReturnCancelPassiveToDefaultAreaRequested;
    public event Action<CharacterId> BindNewCharacterRequested;

    public void AllyAlliesUISetActive(bool isActive)
    {
        AllyAlliesUISetActiveRequested?.Invoke(isActive);
    }

    public void OnlySelectActs(SkillZoneTrait trait, SkillType type, CharacterId id)
    {
        OnlySelectActsRequested?.Invoke(trait, type, id);
    }

    public void OnSkillSelectionScreenTransition(CharacterId id)
    {
        OnSkillSelectionScreenTransitionRequested?.Invoke(id);
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

    public void OpenEmotionalAttachmentSkillSelectUIArea(CharacterId id)
    {
        OpenEmotionalAttachmentSkillSelectUIAreaRequested?.Invoke(id);
    }

    public void OnBattleStart()
    {
        OnBattleStartRequested?.Invoke();
    }

    public void GoToCancelPassiveField(CharacterId id)
    {
        GoToCancelPassiveFieldRequested?.Invoke(id);
    }

    public void ReturnCancelPassiveToDefaultArea(CharacterId id)
    {
        ReturnCancelPassiveToDefaultAreaRequested?.Invoke(id);
    }

    /// <summary>
    /// 新キャラクターのUIバインディングを要求する。
    /// </summary>
    public void BindNewCharacter(CharacterId id)
    {
        BindNewCharacterRequested?.Invoke(id);
    }
}
