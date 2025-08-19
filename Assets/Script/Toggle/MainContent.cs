using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MainContent : TabContents
{
    public override void SwitchContent(TabState state)
    {
        switch (state)
        {
            case TabState.walk://歩きボタン
                WalkObject.SetActive(true);
                TalkObject.SetActive(false);
                NextObject.SetActive(false);
                SkillObject.SetActive(false);
                SelectTargetObject.SetActive(false);
                SelectRangeObject.SetActive(false);
                break;
            case TabState.TalkWindow:
                WalkObject.SetActive(false);
                TalkObject.SetActive(true);
                NextObject.SetActive(false);
                SkillObject.SetActive(false);
                SelectTargetObject.SetActive(false);
                SelectRangeObject.SetActive(false);
                break;
            case TabState.NextWait:
                WalkObject.SetActive(false);
                TalkObject.SetActive(false);
                NextObject.SetActive(true);
                SkillObject.SetActive(false);
                SelectTargetObject.SetActive(false);
                SelectRangeObject.SetActive(false);
                break;
            case TabState.Skill:
                WalkObject.SetActive(false);
                TalkObject.SetActive(false);
                NextObject.SetActive(false);
                SkillObject.SetActive(true);
                SelectTargetObject.SetActive(false);
                SelectRangeObject.SetActive(false);
                break;
            case TabState.SelectTarget:
                WalkObject.SetActive(false);
                TalkObject.SetActive(false);
                NextObject.SetActive(false);
                SkillObject.SetActive(false);
                SelectTargetObject.SetActive(true);
                SelectRangeObject.SetActive(false);
                break;
            case TabState.SelectRange:
                WalkObject.SetActive(false);
                TalkObject.SetActive(false);
                NextObject.SetActive(false);
                SkillObject.SetActive(false);
                SelectTargetObject.SetActive(false);
                SelectRangeObject.SetActive(true);
                break;
        }
    }

}
