using UnityEngine;

/// <summary>
/// タブの状態 ゲームの状態によってそれぞれのタブも色々変わる
/// </summary>
public enum TabState
{
    walk, TalkWindow, NextWait, Skill, SelectTarget, SelectRange,

    // ノベルパート用
    FieldDialogue,  // フィールド会話（タップで進むのみ、戻れない）
    EventDialogue,  // イベント会話（左右ボタンで戻れる）
    NovelChoice,    // 選択肢表示中（選択肢ボタンのみ）
}
public abstract class TabContents : MonoBehaviour //tabContentsChangerのクラスに登録するMonoBehavior
{
    //複雑な操作するならこのクラスで作る。
    public void SetActive(bool isActive)
    {
        gameObject.SetActive(isActive);
    }

    [SerializeField]
    protected GameObject WalkObject;
    [SerializeField]
    protected GameObject TalkObject;
    [SerializeField]
    protected GameObject NextObject;
    [SerializeField]
    protected TabCharaStateContent SkillObject;
    [SerializeField]
    protected GameObject SelectTargetObject;
    [SerializeField]
    protected GameObject SelectRangeObject;

    // ノベルパート用
    [SerializeField]
    protected GameObject FieldDialogueObject;   // タップ領域のみ（進むだけ）
    [SerializeField]
    protected GameObject EventDialogueObject;   // 左右ボタン（進む/戻る）
    [SerializeField]
    protected GameObject NovelChoiceObject;     // 選択肢ボタン群

    /// <summary>
    /// キャラクターIDによってUIが変わる。
    /// </summary>
    public void SwitchCharacter(CharacterId id)
    {
        SkillObject?.SwitchContent(id);
    }

    public abstract void SwitchContent(TabState state);
}

