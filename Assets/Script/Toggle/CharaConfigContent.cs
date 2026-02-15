using UnityEngine;

public class CharaConfigContent : TabContents
{
    [SerializeField] private GameObject BattleObject;

    public override void SwitchContent(TabState state)
    {
        switch (state)
        {
            case TabState.walk:
                if (WalkObject != null) WalkObject.SetActive(true);
                if (BattleObject != null) BattleObject.SetActive(false);
                break;

            case TabState.TalkWindow:
            case TabState.NextWait:
            case TabState.Skill:
            case TabState.SelectTarget:
            case TabState.SelectRange:
                if (WalkObject != null) WalkObject.SetActive(false);
                if (BattleObject != null) BattleObject.SetActive(true);
                break;

            case TabState.FieldDialogue:
            case TabState.EventDialogue:
            case TabState.NovelChoice:
                if (WalkObject != null) WalkObject.SetActive(false);
                if (BattleObject != null) BattleObject.SetActive(false);
                break;
        }
    }
}
