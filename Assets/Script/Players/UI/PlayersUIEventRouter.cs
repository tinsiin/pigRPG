using System;

public sealed class PlayersUIEventRouter
{
    private readonly PlayersUIFacade facade;
    private readonly PlayersUIService service;

    public PlayersUIEventRouter(PlayersUIFacade facade, PlayersUIService service)
    {
        this.facade = facade;
        this.service = service;
        Bind();
    }

    private void Bind()
    {
        if (facade == null || service == null) return;

        // === AllyId ベースのイベント（互換性用） ===
        facade.AllyAlliesUISetActiveRequested += service.AllyAlliesUISetActive;
        facade.OnlySelectActsRequested += service.OnlySelectActs;
        facade.OnSkillSelectionScreenTransitionRequested += service.OnSkillSelectionScreenTransition;
        facade.SelectSkillPassiveTargetRequested += service.GoToSelectSkillPassiveTargetSkillButtonsArea;
        facade.ReturnSelectSkillPassiveTargetRequested += service.ReturnSelectSkillPassiveTargetSkillButtonsArea;
        facade.OpenEmotionalAttachmentSkillSelectUIAreaRequested += service.OpenEmotionalAttachmentSkillSelectUIArea;
        facade.OnBattleStartRequested += service.OnBattleStart;
        facade.GoToCancelPassiveFieldRequested += service.GoToCancelPassiveField;
        facade.ReturnCancelPassiveToDefaultAreaRequested += service.ReturnCancelPassiveToDefaultArea;

        // === CharacterId ベースのイベント（新規） ===
        facade.OnlySelectActsByCharacterIdRequested += service.OnlySelectActs;
        facade.OnSkillSelectionScreenTransitionByCharacterIdRequested += service.OnSkillSelectionScreenTransition;
        facade.OpenEmotionalAttachmentSkillSelectUIAreaByCharacterIdRequested += service.OpenEmotionalAttachmentSkillSelectUIArea;
        facade.GoToCancelPassiveFieldByCharacterIdRequested += service.GoToCancelPassiveField;
        facade.ReturnCancelPassiveToDefaultAreaByCharacterIdRequested += service.ReturnCancelPassiveToDefaultArea;
        facade.BindNewCharacterRequested += service.BindSkillButtonsForNewCharacter;
    }

    public void Unbind()
    {
        if (facade == null || service == null) return;

        // === AllyId ベースのイベント（互換性用） ===
        facade.AllyAlliesUISetActiveRequested -= service.AllyAlliesUISetActive;
        facade.OnlySelectActsRequested -= service.OnlySelectActs;
        facade.OnSkillSelectionScreenTransitionRequested -= service.OnSkillSelectionScreenTransition;
        facade.SelectSkillPassiveTargetRequested -= service.GoToSelectSkillPassiveTargetSkillButtonsArea;
        facade.ReturnSelectSkillPassiveTargetRequested -= service.ReturnSelectSkillPassiveTargetSkillButtonsArea;
        facade.OpenEmotionalAttachmentSkillSelectUIAreaRequested -= service.OpenEmotionalAttachmentSkillSelectUIArea;
        facade.OnBattleStartRequested -= service.OnBattleStart;
        facade.GoToCancelPassiveFieldRequested -= service.GoToCancelPassiveField;
        facade.ReturnCancelPassiveToDefaultAreaRequested -= service.ReturnCancelPassiveToDefaultArea;

        // === CharacterId ベースのイベント（新規） ===
        facade.OnlySelectActsByCharacterIdRequested -= service.OnlySelectActs;
        facade.OnSkillSelectionScreenTransitionByCharacterIdRequested -= service.OnSkillSelectionScreenTransition;
        facade.OpenEmotionalAttachmentSkillSelectUIAreaByCharacterIdRequested -= service.OpenEmotionalAttachmentSkillSelectUIArea;
        facade.GoToCancelPassiveFieldByCharacterIdRequested -= service.GoToCancelPassiveField;
        facade.ReturnCancelPassiveToDefaultAreaByCharacterIdRequested -= service.ReturnCancelPassiveToDefaultArea;
        facade.BindNewCharacterRequested -= service.BindSkillButtonsForNewCharacter;
    }
}
