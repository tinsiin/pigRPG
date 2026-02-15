using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[Serializable]
public class ButtonAndSkillIDHold
{
    public Button button;
    public int skillID;

    /// <summary>
    /// ボタンにコールバックを追加する。
    /// </summary>
    public void AddButtonFunc(UnityAction<int> call)
    {
        Debug.Log("AddButtonFunc" + skillID);
        button.onClick.AddListener(() => call(skillID));
    }

    /// <summary>
    /// ボタンのリスナーをクリアする（再バインド前に呼ぶ）。
    /// </summary>
    public void ClearButtonFunc()
    {
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
        }
    }
}
/// <summary>
/// スキルIDと紐づくトグルボタン用のラッパー。
/// スキルIDが不要な場面（キャラ単位の設定等）は直接ToggleSingleControllerを使う。
/// </summary>
[Serializable]
public class ToggleSingleAndSkillIDHold
{
    public ToggleSingleController Controller;
    public int skillID;

    /// <summary>
    /// コールバックを設定。第1引数: 0(ON)/1(OFF)、第2引数: skillID
    /// </summary>
    public void AddToggleFunc(UnityAction<int, int> call)
    {
        if (Controller == null)
        {
            Debug.LogError("ToggleSingleControllerがnullです！ skillID: " + skillID);
            return;
        }
        if (call == null)
        {
            Debug.LogError("callがnullです！ skillID: " + skillID);
            return;
        }
        Controller.AddListener((int toggleIndex) => call(toggleIndex, skillID));
    }

    public void Interactable(bool interactable)
    {
        Controller.interactable = interactable;
    }
}
/// <summary>
/// 主人公キャラ達のスキルボタンなどのUIリスト
/// </summary>
[Serializable]
public class AllySkillUILists
{
    [Header("スキルボタンリスト")]
    /// <summary>
    /// スキルボタンリスト
    /// </summary>
    public List<ButtonAndSkillIDHold> skillButtons = new();
    [Header("ストックボタンリスト")]
    /// <summary>
    /// ストックボタンリスト
    /// </summary>
    public List<ButtonAndSkillIDHold> stockButtons = new();
    [Header("前のめり選択トグルボタンリスト（実行時）")]
    /// <summary>
    /// 前のめり選択トグルリスト・実行時（スキルIDとToggleSingleControllerのペア）
    /// </summary>
    public List<ToggleSingleAndSkillIDHold> aggressiveCommitToggles = new();

    [Header("前のめり選択トグルボタンリスト（トリガー時）")]
    /// <summary>
    /// 前のめり選択トグルリスト・トリガー時（スキルIDとToggleSingleControllerのペア）
    /// </summary>
    public List<ToggleSingleAndSkillIDHold> aggressiveTriggerToggles = new();

    [Header("前のめり選択トグルボタンリスト（ストック時）")]
    /// <summary>
    /// 前のめり選択トグルリスト・ストック時（スキルIDとToggleSingleControllerのペア）
    /// </summary>
    public List<ToggleSingleAndSkillIDHold> aggressiveStockToggles = new();
}

/// <summary>
/// 味方1人分のUI参照まとめ
/// </summary>
[Serializable]
public class AllyUISet
{
    public AllySkillUILists SkillUILists;
    public GameObject DefaultButtonArea;
    public Button DoNothingButton;
    [Header("武器スキルボタン")]
    public Button WeaponSkillButton;
    public SelectCancelPassiveButtons CancelPassiveButtonField;
    public Button GoToCancelPassiveFieldButton;
    public Button ReturnCancelPassiveToDefaultAreaButton;
}
