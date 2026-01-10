using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public sealed class PlayersUIService : IPlayersUIControl, IPlayersSkillUI
{
    private readonly PlayersRoster roster;
    private readonly AllySkillUILists[] skillUILists;
    private readonly GameObject[] defaultButtonArea;
    private readonly Button[] doNothingButton;
    private readonly SelectCancelPassiveButtons[] cancelPassiveButtonField;
    private readonly SkillPassiveSelectionUI skillPassiveSelectionUI;
    private readonly EmotionalAttachmentUI emotionalAttachmentUI;

    public PlayersUIService(
        PlayersRoster roster,
        AllySkillUILists[] skillUILists,
        GameObject[] defaultButtonArea,
        Button[] doNothingButton,
        SelectCancelPassiveButtons[] cancelPassiveButtonField,
        SkillPassiveSelectionUI skillPassiveSelectionUI,
        EmotionalAttachmentUI emotionalAttachmentUI)
    {
        this.roster = roster;
        this.skillUILists = skillUILists;
        this.defaultButtonArea = defaultButtonArea;
        this.doNothingButton = doNothingButton;
        this.cancelPassiveButtonField = cancelPassiveButtonField;
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

            if (skillUILists != null && i < skillUILists.Length && skillUILists[i] != null)
            {
                foreach (var button in skillUILists[i].skillButtons)
                {
                    button.AddButtonFunc(actor.OnSkillBtnCallBack);
                }
            }

            if (skillUILists != null && i < skillUILists.Length && skillUILists[i] != null)
            {
                foreach (var button in skillUILists[i].stockButtons)
                {
                    button.AddButtonFunc(actor.OnSkillStockBtnCallBack);
                }
            }

            if (skillUILists != null && i < skillUILists.Length && skillUILists[i] != null)
            {
                foreach (var radio in skillUILists[i].aggressiveCommitRadios)
                {
                    radio.AddRadioFunc(actor.OnSkillSelectAgressiveCommitBtnCallBack);
                }
            }

            if (doNothingButton != null && i < doNothingButton.Length && doNothingButton[i] != null)
            {
                doNothingButton[i].onClick.AddListener(actor.OnSkillDoNothingBtnCallBack);
            }
        }

        Debug.Log("ボタンとコールバックを結び付けました - ApplySkillButtons");
    }

    public void UpdateSkillButtonVisibility()
    {
        var allies = roster.Allies;
        for (int i = 0; i < allies.Length; i++)
        {
            var activeSkillIds = new HashSet<int>(allies[i].SkillList.Cast<AllySkill>().Select(skill => skill.ID));
            foreach (var hold in skillUILists[i].skillButtons)
            {
                hold.button.interactable = activeSkillIds.Contains(hold.skillID);
            }
            foreach (var hold in skillUILists[i].stockButtons)
            {
                hold.button.interactable = activeSkillIds.Contains(hold.skillID);
            }
            foreach (var hold in skillUILists[i].aggressiveCommitRadios)
            {
                hold.Interactable(activeSkillIds.Contains(hold.skillID));
            }
        }
    }

    public void OnSkillSelectionScreenTransition(int index)
    {
        var allies = roster.Allies;
        foreach (var radio in skillUILists[index].aggressiveCommitRadios.Where(rad => allies[index].ValidSkillIDList.Contains(rad.skillID)))
        {
            BaseSkill skill = allies[index].SkillList[radio.skillID];
            if (skill == null) Debug.LogError("スキルがありません");
            radio.Controller.SetOnWithoutNotify(skill.IsAggressiveCommit ? 0 : 1);
        }
    }

    public void OnlySelectActs(SkillZoneTrait trait, SkillType type, int index)
    {
        var allies = roster.Allies;
        foreach (var skill in allies[index].SkillList.Cast<AllySkill>())
        {
            var hold = skillUILists[index].skillButtons.Find(hold => hold.skillID == skill.ID);
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
        if (cancelPassiveButtonField[index] == null) Debug.LogError("CancelPassiveButtonFieldがnullです");
        cancelPassiveButtonField[index].ShowPassiveButtons(allies[index]);
    }

    public void GoToCancelPassiveField(int index)
    {
        defaultButtonArea[index].gameObject.SetActive(false);
        cancelPassiveButtonField[index].gameObject.SetActive(true);
    }

    public void ReturnCancelPassiveToDefaultArea(int index)
    {
        cancelPassiveButtonField[index].gameObject.SetActive(false);
        defaultButtonArea[index].gameObject.SetActive(true);
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

    public void OpenEmotionalAttachmentSkillSelectUIArea(int index)
    {
        emotionalAttachmentUI.OpenEmotionalAttachmentSkillSelectUIArea(index);
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
}
