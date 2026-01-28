using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public sealed class PlayersUIService
{
    private readonly PlayersRoster roster;
    private readonly AllyUISet[] allyUISets;
    private readonly SkillPassiveSelectionUI skillPassiveSelectionUI;
    private readonly EmotionalAttachmentUI emotionalAttachmentUI;

    public PlayersUIService(
        PlayersRoster roster,
        AllyUISet[] allyUISets,
        SkillPassiveSelectionUI skillPassiveSelectionUI,
        EmotionalAttachmentUI emotionalAttachmentUI)
    {
        this.roster = roster;
        this.allyUISets = allyUISets;
        this.skillPassiveSelectionUI = skillPassiveSelectionUI;
        this.emotionalAttachmentUI = emotionalAttachmentUI;
    }

    public void BindSkillButtons()
    {
        Debug.Log("ApplySkillButtons");

        var allies = roster.Allies;
        for (int i = 0; i < allies.Length; i++)
        {
            var actor = allies[i];
            if (actor == null) continue;

            var uiSet = GetUISet((AllyId)i);

            if (uiSet?.SkillUILists != null)
            {
                foreach (var button in uiSet.SkillUILists.skillButtons)
                {
                    button.AddButtonFunc(actor.OnSkillBtnCallBack);
                }
            }

            if (uiSet?.SkillUILists != null)
            {
                foreach (var button in uiSet.SkillUILists.stockButtons)
                {
                    button.AddButtonFunc(actor.OnSkillStockBtnCallBack);
                }
            }

            if (uiSet?.SkillUILists != null)
            {
                foreach (var radio in uiSet.SkillUILists.aggressiveCommitRadios)
                {
                    radio.AddRadioFunc(actor.OnSkillSelectAgressiveCommitBtnCallBack);
                }
            }

            if (uiSet?.DoNothingButton != null)
            {
                uiSet.DoNothingButton.onClick.AddListener(actor.OnSkillDoNothingBtnCallBack);
            }
        }

        Debug.Log("ボタンとコールバックを結び付けました - ApplySkillButtons");
    }

    public void UpdateSkillButtonVisibility()
    {
        var allies = roster.Allies;
        for (int i = 0; i < allies.Length; i++)
        {
            var actor = allies[i];
            if (actor == null) continue;

            var uiSet = GetUISet((AllyId)i);
            if (uiSet?.SkillUILists == null) continue;
            var activeSkillIds = new HashSet<int>(actor.SkillList.Cast<AllySkill>().Select(skill => skill.ID));
            foreach (var hold in uiSet.SkillUILists.skillButtons)
            {
                hold.button.interactable = activeSkillIds.Contains(hold.skillID);
            }
            foreach (var hold in uiSet.SkillUILists.stockButtons)
            {
                hold.button.interactable = activeSkillIds.Contains(hold.skillID);
            }
            foreach (var hold in uiSet.SkillUILists.aggressiveCommitRadios)
            {
                hold.Interactable(activeSkillIds.Contains(hold.skillID));
            }
        }
    }

    public void OnSkillSelectionScreenTransition(AllyId allyId)
    {
        var index = (int)allyId;
        var uiSet = GetUISet(allyId);
        if (uiSet?.SkillUILists == null) return;
        var allies = roster.Allies;
        foreach (var radio in uiSet.SkillUILists.aggressiveCommitRadios.Where(rad => allies[index].ValidSkillIDList.Contains(rad.skillID)))
        {
            BaseSkill skill = allies[index].SkillList[radio.skillID];
            if (skill == null) Debug.LogError("スキルがありません");
            radio.Controller.SetOnWithoutNotify(skill.IsAggressiveCommit ? 0 : 1);
        }
    }

    public void OnlySelectActs(SkillZoneTrait trait, SkillType type, AllyId allyId)
    {
        var index = (int)allyId;
        var uiSet = GetUISet(allyId);
        if (uiSet?.SkillUILists == null) return;
        var allies = roster.Allies;
        foreach (var skill in allies[index].SkillList.Cast<AllySkill>())
        {
            var hold = uiSet.SkillUILists.skillButtons.Find(hold => hold.skillID == skill.ID);
            if (allies[index].HasCanCancelCantACTPassive)
            {
                if (hold != null)
                {
                    hold.button.interactable = false;
                }
            }
            else
            {
                Debug.Log("スキルのボタンを有効化します" + skill.ID);
                if (hold != null)
                {
                    hold.button.interactable =
                        ZoneTraitAndTypeSkillMatchesUIFilter(skill, trait, type)
                        && CanCastNow(allies[index], skill);
                    Debug.Log(ZoneTraitAndTypeSkillMatchesUIFilter(skill, trait, type) + " |" + hold.skillID + "| " + CanCastNow(allies[index], skill));
                }
            }
        }
        if (uiSet.CancelPassiveButtonField == null) Debug.LogError("CancelPassiveButtonFieldがnullです");
        uiSet.CancelPassiveButtonField?.ShowPassiveButtons(allies[index]);
    }

    public void GoToCancelPassiveField(AllyId allyId)
    {
        var uiSet = GetUISet(allyId);
        if (uiSet?.DefaultButtonArea == null || uiSet.CancelPassiveButtonField == null) return;
        uiSet.DefaultButtonArea.gameObject.SetActive(false);
        uiSet.CancelPassiveButtonField.gameObject.SetActive(true);
    }

    public void ReturnCancelPassiveToDefaultArea(AllyId allyId)
    {
        var uiSet = GetUISet(allyId);
        if (uiSet?.DefaultButtonArea == null || uiSet.CancelPassiveButtonField == null) return;
        uiSet.CancelPassiveButtonField.gameObject.SetActive(false);
        uiSet.DefaultButtonArea.gameObject.SetActive(true);
    }

    public void AllyAlliesUISetActive(bool isActive)
    {
        var allies = roster.Allies;
        for (int i = 0; i < allies.Length; i++)
        {
            if (allies[i]?.UI != null) allies[i].UI.SetActive(isActive);
        }
    }

    public UniTask<List<BaseSkill>> GoToSelectSkillPassiveTargetSkillButtonsArea(List<BaseSkill> skills, int selectCount)
    {
        return skillPassiveSelectionUI.Open(skills, selectCount);
    }

    public void ReturnSelectSkillPassiveTargetSkillButtonsArea()
    {
        skillPassiveSelectionUI.Close();
    }

    public void OpenEmotionalAttachmentSkillSelectUIArea(AllyId allyId)
    {
        emotionalAttachmentUI.OpenEmotionalAttachmentSkillSelectUIArea(allyId);
    }

    public void OnBattleStart()
    {
        emotionalAttachmentUI.OnBattleStart();
    }

    private bool ZoneTraitAndTypeSkillMatchesUIFilter(BaseSkill skill, SkillZoneTrait traitMask, SkillType typeMask)
    {
        if (skill == null) return false;
        return skill.HasZoneTraitAny(traitMask) && skill.HasTypeAny(typeMask);
    }

    private bool CanCastNow(BaseStates actor, BaseSkill skill)
    {
        return SkillResourceFlow.CanCastSkill(actor, skill);
    }

    private AllyUISet GetUISet(AllyId allyId)
    {
        var index = (int)allyId;
        if (allyUISets == null || index < 0 || index >= allyUISets.Length) return null;
        return allyUISets[index];
    }
}
