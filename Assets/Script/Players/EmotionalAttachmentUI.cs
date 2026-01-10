using System.Linq;

public sealed class EmotionalAttachmentUI
{
    private readonly PlayersRoster roster;
    private readonly SelectEmotionalAttachmentSkillButtons emotionalAttachmentSkillSelectUIArea;

    public EmotionalAttachmentUI(
        PlayersRoster roster,
        SelectEmotionalAttachmentSkillButtons emotionalAttachmentSkillSelectUIArea)
    {
        this.roster = roster;
        this.emotionalAttachmentSkillSelectUIArea = emotionalAttachmentSkillSelectUIArea;
    }

    public void OpenEmotionalAttachmentSkillSelectUIArea(int index)
    {
        emotionalAttachmentSkillSelectUIArea.OpenEmotionalAttachmentSkillSelectUIArea();
        emotionalAttachmentSkillSelectUIArea.ShowSkillsButtons(
            roster.Allies[index].SkillList.Cast<AllySkill>().Where(skill => skill.IsTLOA).ToList(),
            roster.Allies[index].EmotionalAttachmentSkillID,
            roster.Allies[index].OnEmotionalAttachmentSkillIDChange);
    }

    public void OnBattleStart()
    {
        emotionalAttachmentSkillSelectUIArea.CloseEmotionalAttachmentSkillSelectUIArea();
    }
}
