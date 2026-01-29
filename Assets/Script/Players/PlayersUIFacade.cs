using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

public sealed class PlayersUIFacade : IPlayersUIControl, IPlayersSkillUI
{
    // === AllyId ベースのイベント（互換性用） ===
    public event Action<bool> AllyAlliesUISetActiveRequested;
    public event Action<SkillZoneTrait, SkillType, AllyId> OnlySelectActsRequested;
    public event Action<AllyId> OnSkillSelectionScreenTransitionRequested;
    public event Func<List<BaseSkill>, int, UniTask<List<BaseSkill>>> SelectSkillPassiveTargetRequested;
    public event Action ReturnSelectSkillPassiveTargetRequested;
    public event Action<AllyId> OpenEmotionalAttachmentSkillSelectUIAreaRequested;
    public event Action OnBattleStartRequested;
    public event Action<AllyId> GoToCancelPassiveFieldRequested;
    public event Action<AllyId> ReturnCancelPassiveToDefaultAreaRequested;

    // === CharacterId ベースのイベント（新規） ===
    public event Action<SkillZoneTrait, SkillType, CharacterId> OnlySelectActsByCharacterIdRequested;
    public event Action<CharacterId> OnSkillSelectionScreenTransitionByCharacterIdRequested;
    public event Action<CharacterId> OpenEmotionalAttachmentSkillSelectUIAreaByCharacterIdRequested;
    public event Action<CharacterId> GoToCancelPassiveFieldByCharacterIdRequested;
    public event Action<CharacterId> ReturnCancelPassiveToDefaultAreaByCharacterIdRequested;
    public event Action<CharacterId> BindNewCharacterRequested;

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

    // === CharacterId ベースのメソッド実装 ===

    public void OnlySelectActs(SkillZoneTrait trait, SkillType type, CharacterId id)
    {
        // CharacterIdベースのイベントがあればそちらを優先
        if (OnlySelectActsByCharacterIdRequested != null)
        {
            OnlySelectActsByCharacterIdRequested.Invoke(trait, type, id);
            return;
        }

        // フォールバック: 固定メンバーならAllyId版を呼ぶ
        if (id.IsOriginalMember)
        {
            OnlySelectActsRequested?.Invoke(trait, type, id.ToAllyId());
        }
        else
        {
            Debug.LogWarning($"PlayersUIFacade.OnlySelectActs: 新キャラクター {id} のハンドラが未登録です");
        }
    }

    public void OnSkillSelectionScreenTransition(CharacterId id)
    {
        // CharacterIdベースのイベントがあればそちらを優先
        if (OnSkillSelectionScreenTransitionByCharacterIdRequested != null)
        {
            OnSkillSelectionScreenTransitionByCharacterIdRequested.Invoke(id);
            return;
        }

        // フォールバック: 固定メンバーならAllyId版を呼ぶ
        if (id.IsOriginalMember)
        {
            OnSkillSelectionScreenTransitionRequested?.Invoke(id.ToAllyId());
        }
        else
        {
            Debug.LogWarning($"PlayersUIFacade.OnSkillSelectionScreenTransition: 新キャラクター {id} のハンドラが未登録です");
        }
    }

    public void OpenEmotionalAttachmentSkillSelectUIArea(CharacterId id)
    {
        // CharacterIdベースのイベントがあればそちらを優先
        if (OpenEmotionalAttachmentSkillSelectUIAreaByCharacterIdRequested != null)
        {
            OpenEmotionalAttachmentSkillSelectUIAreaByCharacterIdRequested.Invoke(id);
            return;
        }

        // フォールバック: 固定メンバーならAllyId版を呼ぶ
        if (id.IsOriginalMember)
        {
            OpenEmotionalAttachmentSkillSelectUIAreaRequested?.Invoke(id.ToAllyId());
        }
        else
        {
            Debug.LogWarning($"PlayersUIFacade.OpenEmotionalAttachmentSkillSelectUIArea: 新キャラクター {id} のハンドラが未登録です");
        }
    }

    public void GoToCancelPassiveField(CharacterId id)
    {
        // CharacterIdベースのイベントがあればそちらを優先
        if (GoToCancelPassiveFieldByCharacterIdRequested != null)
        {
            GoToCancelPassiveFieldByCharacterIdRequested.Invoke(id);
            return;
        }

        // フォールバック: 固定メンバーならAllyId版を呼ぶ
        if (id.IsOriginalMember)
        {
            GoToCancelPassiveFieldRequested?.Invoke(id.ToAllyId());
        }
        else
        {
            Debug.LogWarning($"PlayersUIFacade.GoToCancelPassiveField: 新キャラクター {id} のハンドラが未登録です");
        }
    }

    public void ReturnCancelPassiveToDefaultArea(CharacterId id)
    {
        // CharacterIdベースのイベントがあればそちらを優先
        if (ReturnCancelPassiveToDefaultAreaByCharacterIdRequested != null)
        {
            ReturnCancelPassiveToDefaultAreaByCharacterIdRequested.Invoke(id);
            return;
        }

        // フォールバック: 固定メンバーならAllyId版を呼ぶ
        if (id.IsOriginalMember)
        {
            ReturnCancelPassiveToDefaultAreaRequested?.Invoke(id.ToAllyId());
        }
        else
        {
            Debug.LogWarning($"PlayersUIFacade.ReturnCancelPassiveToDefaultArea: 新キャラクター {id} のハンドラが未登録です");
        }
    }

    /// <summary>
    /// 新キャラクターのUIバインディングを要求する。
    /// </summary>
    public void BindNewCharacter(CharacterId id)
    {
        BindNewCharacterRequested?.Invoke(id);
    }
}
