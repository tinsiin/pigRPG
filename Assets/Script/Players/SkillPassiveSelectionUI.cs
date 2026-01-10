using System.Collections.Generic;
using Cysharp.Threading.Tasks;

public sealed class SkillPassiveSelectionUI
{
    private readonly SelectSkillPassiveTargetSkillButtons selectSkillPassiveTargetHandle;

    public SkillPassiveSelectionUI(SelectSkillPassiveTargetSkillButtons selectSkillPassiveTargetHandle)
    {
        this.selectSkillPassiveTargetHandle = selectSkillPassiveTargetHandle;
    }

    public async UniTask<List<BaseSkill>> Open(List<BaseSkill> skills, int selectCount)
    {
        ModalAreaController.Instance?.ShowSingle(selectSkillPassiveTargetHandle.gameObject);
        return await selectSkillPassiveTargetHandle.ShowSkillsButtons(skills, selectCount);
    }

    public void Close()
    {
        ModalAreaController.Instance?.CloseFor(selectSkillPassiveTargetHandle.gameObject);
    }
}
