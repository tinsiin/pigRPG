using System;

public sealed class BattleUiEventAdapter : IBattleEventSink
{
    private readonly IBattleUiAdapter _uiAdapter;

    public BattleUiEventAdapter(IBattleUiAdapter uiAdapter)
    {
        _uiAdapter = uiAdapter ?? throw new ArgumentNullException(nameof(uiAdapter));
    }

    public void OnBattleEvent(BattleEvent battleEvent)
    {
        switch (battleEvent.Type)
        {
            case BattleEventType.Message:
                if (!string.IsNullOrEmpty(battleEvent.Message))
                {
                    _uiAdapter.PushMessage(battleEvent.Message);
                }
                break;
            case BattleEventType.Log:
                if (!string.IsNullOrEmpty(battleEvent.Message))
                {
                    _uiAdapter.AddLog(battleEvent.Message, battleEvent.Important);
                }
                break;
            case BattleEventType.UiDisplayLogs:
                _uiAdapter.DisplayLogs();
                break;
            case BattleEventType.UiNextArrow:
                _uiAdapter.NextArrow();
                break;
            case BattleEventType.UiMoveActionMark:
                _uiAdapter.MoveActionMarkToActorScaled(battleEvent.Actor, battleEvent.Immediate, battleEvent.WaitForIntro);
                break;
            case BattleEventType.UiSetSelectedActor:
                _uiAdapter.SetSelectedActor(battleEvent.Actor);
                break;
            case BattleEventType.UiSwitchAllySkillUiState:
                _uiAdapter.SwitchAllySkillUiState(battleEvent.Actor, battleEvent.HasSingleTargetReservation);
                break;
        }
    }
}
