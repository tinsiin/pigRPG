using UnityEngine;

/// <summary>
/// タブの状態 ゲームの状態によってそれぞれのタブも色々変わる
/// </summary>
public enum TabState
{
    walk, TalkWindow, NextWait, Skill, SelectTarget, SelectRange
}
public enum SkillUICharaState
{
    geino, sites, normalia
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

    /// <summary>
    /// キャラ状態によってuiが変わる
    /// </summary>
    public void CharaStateSwitch(SkillUICharaState state)
    {
        SkillObject.SwitchContent(state);//キャラによるui変更は今の所スキル画面のみ
    }


    public abstract void SwitchContent(TabState state);
}

