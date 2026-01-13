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

    public void OpenEmotionalAttachmentSkillSelectUIArea(AllyId allyId)
    {
        var actor = roster.GetAllyById(allyId) as AllyClass;
        if (actor == null) return;
        emotionalAttachmentSkillSelectUIArea.OpenEmotionalAttachmentSkillSelectUIArea();
        emotionalAttachmentSkillSelectUIArea.ShowSkillsButtons(
            actor.SkillList.Cast<AllySkill>().Where(skill => skill.IsTLOA).ToList(),
            actor.EmotionalAttachmentSkillID,
            actor.OnEmotionalAttachmentSkillIDChange);
    }

    public void OnBattleStart()
    {
        emotionalAttachmentSkillSelectUIArea.CloseEmotionalAttachmentSkillSelectUIArea();
    }
}
