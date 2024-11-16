using UnityEngine;

/// <summary>
/// タブの状態 ゲームの状態によってそれぞれのタブも色々変わる
/// </summary>
public enum TabState
{
    walk, TalkWindow, NextWait, Skill, SelectTarget
}
public enum SkillUICharaState
{
    geino, sites, normalia
}


public class TabContents : MonoBehaviour //tabContentsChangerのクラスに登録するMonoBehavior
{
    //複雑な操作するならこのクラスで作る。
    public void SetActive(bool isActive)
    {
        gameObject.SetActive(isActive);
    }

    [SerializeField]
    bool IsContents;//複数のコンテンツを扱うかどうか

    [SerializeField]
    GameObject WalkObject;
    [SerializeField]
    GameObject TalkObject;
    [SerializeField]
    GameObject NextObject;
    [SerializeField]
    TabCharaStateContent SkillObject;
    [SerializeField]
    GameObject SelectTargetObject;

    /// <summary>
    /// キャラ状態によってuiが変わる
    /// </summary>
    public void CharaStateSwitch(SkillUICharaState state)
    {
        SkillObject.SwitchContent(state);//キャラによるui変更は今の所スキル画面のみ
    }


    public void SwitchContent(TabState state)
    {
        if (IsContents)
            switch (state)
            {
                case TabState.walk://歩きボタン
                    WalkObject.SetActive(true);
                    TalkObject.SetActive(false);
                    NextObject.SetActive(false);
                    SkillObject.SetActive(false);
                    SelectTargetObject.SetActive(false);
                    break;
                case TabState.TalkWindow:
                    WalkObject.SetActive(false);
                    TalkObject.SetActive(true);
                    NextObject.SetActive(false);
                    SkillObject.SetActive(false);
                    SelectTargetObject.SetActive(false);
                    break;
                case TabState.NextWait:
                    WalkObject.SetActive(false);
                    TalkObject.SetActive(false);
                    NextObject.SetActive(true);
                    SkillObject.SetActive(false);
                    SelectTargetObject.SetActive(false);
                    break;
                case TabState.Skill:
                    WalkObject.SetActive(false);
                    TalkObject.SetActive(false);
                    NextObject.SetActive(false);
                    SkillObject.SetActive(true);
                    SelectTargetObject.SetActive(false);
                    break;
                case TabState.SelectTarget:
                    WalkObject.SetActive(false);
                    TalkObject.SetActive(false);
                    NextObject.SetActive(false);
                    SkillObject.SetActive(true);
                    SelectTargetObject.SetActive(true);
                    break;
            }
    }
}