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
/// スキルIDが必要なラジオボタン処理用のコントローラー
/// スキルIDなどが必要のない、例えば「キャラ自体の設定用」などは直接ToggleGroupControllerを使う。
/// </summary>
[Serializable]
public class RadioButtonsAndSkillIDHold
{
    public ToggleGroupController Controller;
    public int skillID;

    // UnityAction<int, int>に変更 - 第1引数：どのトグルが選ばれたか、第2引数：skillID
    public void AddRadioFunc(UnityAction<int, int> call)
    {
        // nullチェック
        if (Controller == null)
        {
            Debug.LogError("toggleGroupがnullです！ skillID: " + skillID);
        }

        if (call == null)
        {
            Debug.LogError("callがnullです！ skillID: " + skillID);
        }
        // 両方の情報を渡す
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
    [Header("前のめり選択が可能なスキル用に選択できるラジオボタン用リスト")]
    /// <summary>
    /// 前のめり選択ラジオリスト
    /// </summary>
    public List<RadioButtonsAndSkillIDHold> aggressiveCommitRadios = new();
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
    public SelectCancelPassiveButtons CancelPassiveButtonField;
    public Button GoToCancelPassiveFieldButton;
    public Button ReturnCancelPassiveToDefaultAreaButton;
}
