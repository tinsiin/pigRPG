using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;

public sealed class PlayersUIService
{
    private readonly PlayersRoster roster;
    private readonly SkillPassiveSelectionUI skillPassiveSelectionUI;
    private readonly EmotionalAttachmentUI emotionalAttachmentUI;

    /// <summary>
    /// CharacterUIRegistryへのアクセス。
    /// シーンに配置されたCharacterUIRegistryコンポーネントを参照する。
    /// </summary>
    private CharacterUIRegistry UIRegistry => CharacterUIRegistry.Instance;

    public PlayersUIService(
        PlayersRoster roster,
        SkillPassiveSelectionUI skillPassiveSelectionUI,
        EmotionalAttachmentUI emotionalAttachmentUI)
    {
        this.roster = roster;
        this.skillPassiveSelectionUI = skillPassiveSelectionUI;
        this.emotionalAttachmentUI = emotionalAttachmentUI;

        // CharacterUIRegistryはシーンに配置されたMonoBehaviourを使用
        if (CharacterUIRegistry.Instance == null)
        {
            Debug.LogWarning("PlayersUIService: CharacterUIRegistry.Instance が null です。シーンにCharacterUIRegistryを配置してください。");
        }
    }

    public void BindSkillButtons()
    {
        Debug.Log("ApplySkillButtons");

        if (UIRegistry == null)
        {
            Debug.LogError("PlayersUIService.BindSkillButtons: CharacterUIRegistry.Instance が null です");
            return;
        }

        // 全登録済みCharacterIdに対してバインド（新キャラにも対応）
        foreach (var characterId in UIRegistry.AllCharacterIds)
        {
            var actor = roster.GetAlly(characterId);
            if (actor == null) continue;

            var uiSet = UIRegistry.GetUISet(characterId);
            BindSkillButtonsForCharacter(actor, uiSet);
        }

        Debug.Log("ボタンとコールバックを結び付けました - ApplySkillButtons");
    }

    /// <summary>
    /// 単一キャラクターのスキルボタンをバインドする。
    /// 既存リスナーをクリアしてから追加するので、再バインドしても重複しない。
    /// </summary>
    private void BindSkillButtonsForCharacter(AllyClass actor, AllyUISet uiSet)
    {
        if (actor == null || uiSet == null) return;

        if (uiSet.SkillUILists != null)
        {
            // スキルボタン: クリアしてから追加
            foreach (var button in uiSet.SkillUILists.skillButtons)
            {
                button.ClearButtonFunc();
                button.AddButtonFunc(actor.OnSkillBtnCallBack);
            }

            // ストックボタン: クリアしてから追加
            foreach (var button in uiSet.SkillUILists.stockButtons)
            {
                button.ClearButtonFunc();
                button.AddButtonFunc(actor.OnSkillStockBtnCallBack);
            }

            // ラジオボタン: ToggleGroupController.AddListener 内でクリアされる
            foreach (var radio in uiSet.SkillUILists.aggressiveCommitRadios)
            {
                radio.AddRadioFunc(actor.OnSkillSelectAgressiveCommitBtnCallBack);
            }
        }

        // DoNothingボタン: クリアしてから追加
        if (uiSet.DoNothingButton != null)
        {
            uiSet.DoNothingButton.onClick.RemoveAllListeners();
            uiSet.DoNothingButton.onClick.AddListener(actor.OnSkillDoNothingBtnCallBack);
        }
    }

    /// <summary>
    /// 新たに解放されたキャラクターのスキルボタンをバインドする。
    /// CharacterUnlockEffect等から呼び出す用。
    /// </summary>
    public void BindSkillButtonsForNewCharacter(CharacterId id)
    {
        if (UIRegistry == null)
        {
            Debug.LogWarning($"PlayersUIService.BindSkillButtonsForNewCharacter: CharacterUIRegistry.Instance が null です。UIバインドを延期します。");
            return;
        }

        var actor = roster.GetAlly(id);
        var uiSet = UIRegistry.GetUISet(id);

        if (actor == null)
        {
            Debug.LogWarning($"PlayersUIService.BindSkillButtonsForNewCharacter: キャラクター '{id}' が見つかりません");
            return;
        }

        if (uiSet == null)
        {
            Debug.LogWarning($"PlayersUIService.BindSkillButtonsForNewCharacter: UISet '{id}' が見つかりません");
            return;
        }

        BindSkillButtonsForCharacter(actor, uiSet);
        Debug.Log($"PlayersUIService: '{id}' のスキルボタンをバインドしました");
    }

    public void UpdateSkillButtonVisibility()
    {
        if (UIRegistry == null)
        {
            Debug.LogError("PlayersUIService.UpdateSkillButtonVisibility: CharacterUIRegistry.Instance が null です");
            return;
        }

        // 全登録済みCharacterIdに対して更新（新キャラにも対応）
        foreach (var characterId in UIRegistry.AllCharacterIds)
        {
            var actor = roster.GetAlly(characterId);
            if (actor == null) continue;

            var uiSet = UIRegistry.GetUISet(characterId);
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

    /// <summary>
    /// 特定キャラクターのスキルボタン可視性を更新する。
    /// </summary>
    public void UpdateSkillButtonVisibilityForCharacter(CharacterId id)
    {
        var actor = roster.GetAlly(id);
        if (actor == null) return;

        var uiSet = UIRegistry.GetUISet(id);
        if (uiSet?.SkillUILists == null) return;

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

    public void AllyAlliesUISetActive(bool isActive)
    {
        // 全登録済みキャラクター（固定3人 + 新キャラ）のUI表示/非表示を制御
        foreach (var ally in roster.AllAllies)
        {
            if (ally?.UI != null) ally.UI.SetActive(isActive);
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

    /// <summary>
    /// CharacterIdでUIセットを取得する。
    /// </summary>
    public AllyUISet GetUISet(CharacterId id)
    {
        return UIRegistry?.GetUISet(id);
    }

    public void OnSkillSelectionScreenTransition(CharacterId id)
    {
        var ally = roster.GetAlly(id);
        if (ally == null) return;

        var uiSet = UIRegistry.GetUISet(id);
        if (uiSet?.SkillUILists == null) return;

        foreach (var radio in uiSet.SkillUILists.aggressiveCommitRadios.Where(rad => ally.ValidSkillIDList.Contains(rad.skillID)))
        {
            BaseSkill skill = ally.SkillList[radio.skillID];
            if (skill == null) Debug.LogError("スキルがありません");
            radio.Controller.SetOnWithoutNotify(skill.IsAggressiveCommit ? 0 : 1);
        }
    }

    public void OnlySelectActs(SkillZoneTrait trait, SkillType type, CharacterId id)
    {
        var ally = roster.GetAlly(id);
        if (ally == null) return;

        var uiSet = UIRegistry.GetUISet(id);
        if (uiSet?.SkillUILists == null) return;

        foreach (var skill in ally.SkillList.Cast<AllySkill>())
        {
            var hold = uiSet.SkillUILists.skillButtons.Find(hold => hold.skillID == skill.ID);
            if (ally.HasCanCancelCantACTPassive)
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
                        && CanCastNow(ally, skill);
                    Debug.Log(ZoneTraitAndTypeSkillMatchesUIFilter(skill, trait, type) + " |" + hold.skillID + "| " + CanCastNow(ally, skill));
                }
            }
        }
        if (uiSet.CancelPassiveButtonField == null) Debug.LogError("CancelPassiveButtonFieldがnullです");
        uiSet.CancelPassiveButtonField?.ShowPassiveButtons(ally);
    }

    public void GoToCancelPassiveField(CharacterId id)
    {
        var uiSet = UIRegistry.GetUISet(id);
        if (uiSet?.DefaultButtonArea == null || uiSet.CancelPassiveButtonField == null) return;
        uiSet.DefaultButtonArea.gameObject.SetActive(false);
        uiSet.CancelPassiveButtonField.gameObject.SetActive(true);
    }

    public void ReturnCancelPassiveToDefaultArea(CharacterId id)
    {
        var uiSet = UIRegistry.GetUISet(id);
        if (uiSet?.DefaultButtonArea == null || uiSet.CancelPassiveButtonField == null) return;
        uiSet.CancelPassiveButtonField.gameObject.SetActive(false);
        uiSet.DefaultButtonArea.gameObject.SetActive(true);
    }

    public void OpenEmotionalAttachmentSkillSelectUIArea(CharacterId id)
    {
        emotionalAttachmentUI.OpenEmotionalAttachmentSkillSelectUIArea(id);
    }
}
