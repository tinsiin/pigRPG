public sealed class NoOpBattleUiAdapter : IBattleUiAdapter
{
    public void PushMessage(string message)
    {
    }

    public void AddLog(string message, bool important)
    {
    }

    public void DisplayLogs()
    {
    }

    public void NextArrow()
    {
    }

    public void MoveActionMarkToActorScaled(BaseStates acter, bool immediate, bool waitForIntro)
    {
    }

    public void SetSelectedActor(BaseStates acter)
    {
    }

    public void SwitchAllySkillUiState(BaseStates acter, bool hasSingleTargetReservation)
    {
    }
}
