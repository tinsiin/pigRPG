public interface IBattleUiAdapter
{
    void PushMessage(string message);
    void AddLog(string message, bool important);
    void DisplayLogs();
    void NextArrow();
    void MoveActionMarkToActorScaled(BaseStates acter, bool immediate, bool waitForIntro);
    void SetSelectedActor(BaseStates acter);
    void SwitchAllySkillUiState(BaseStates acter, bool hasSingleTargetReservation);
}
