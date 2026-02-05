public enum BattleEventType
{
    BattleStarted,
    TurnAdvanced,
    BattleEnded,
    Message,
    Log,
    UiDisplayLogs,
    UiNextArrow,
    UiMoveActionMark,
    UiSetSelectedActor,
    UiSwitchAllySkillUiState,
    BattleInputApplied
}

public readonly struct BattleEvent
{
    public BattleEventType Type { get; }
    public string Message { get; }
    public bool Important { get; }
    public int TurnCount { get; }
    public string ActorName { get; }
    public BaseStates Actor { get; }
    public bool Immediate { get; }
    public bool WaitForIntro { get; }
    public bool HasSingleTargetReservation { get; }

    public BattleEvent(
        BattleEventType type,
        string message = "",
        bool important = false,
        int turnCount = 0,
        string actorName = null,
        BaseStates actor = null,
        bool immediate = false,
        bool waitForIntro = false,
        bool hasSingleTargetReservation = false)
    {
        Type = type;
        Message = message ?? "";
        Important = important;
        TurnCount = turnCount;
        ActorName = actorName ?? "";
        Actor = actor;
        Immediate = immediate;
        WaitForIntro = waitForIntro;
        HasSingleTargetReservation = hasSingleTargetReservation;
    }

    public static BattleEvent Started(int turnCount)
    {
        return new BattleEvent(BattleEventType.BattleStarted, "戦闘開始", true, turnCount);
    }

    public static BattleEvent TurnAdvanced(int turnCount)
    {
        return new BattleEvent(BattleEventType.TurnAdvanced, $"ターン{turnCount}", false, turnCount);
    }

    public static BattleEvent Ended(int turnCount)
    {
        return new BattleEvent(BattleEventType.BattleEnded, "戦闘終了", true, turnCount);
    }

    public static BattleEvent MessageOnly(string message, bool important = false, int turnCount = 0, string actorName = null)
    {
        return new BattleEvent(BattleEventType.Message, message, important, turnCount, actorName);
    }

    public static BattleEvent LogOnly(string message, bool important = false, int turnCount = 0, string actorName = null)
    {
        return new BattleEvent(BattleEventType.Log, message, important, turnCount, actorName);
    }

    public static BattleEvent UiDisplayLogs()
    {
        return new BattleEvent(BattleEventType.UiDisplayLogs);
    }

    public static BattleEvent UiNextArrow()
    {
        return new BattleEvent(BattleEventType.UiNextArrow);
    }

    public static BattleEvent UiMoveActionMark(BaseStates actor, bool immediate, bool waitForIntro)
    {
        var actorName = actor != null ? actor.CharacterName : "";
        return new BattleEvent(BattleEventType.UiMoveActionMark, actorName: actorName, actor: actor, immediate: immediate, waitForIntro: waitForIntro);
    }

    public static BattleEvent UiSetSelectedActor(BaseStates actor)
    {
        var actorName = actor != null ? actor.CharacterName : "";
        return new BattleEvent(BattleEventType.UiSetSelectedActor, actorName: actorName, actor: actor);
    }

    public static BattleEvent UiSwitchAllySkillUiState(BaseStates actor, bool hasSingleTargetReservation)
    {
        var actorName = actor != null ? actor.CharacterName : "";
        return new BattleEvent(
            BattleEventType.UiSwitchAllySkillUiState,
            actorName: actorName,
            actor: actor,
            hasSingleTargetReservation: hasSingleTargetReservation);
    }

    public static BattleEvent InputApplied(string message, int turnCount = 0, string actorName = null)
    {
        return new BattleEvent(BattleEventType.BattleInputApplied, message, false, turnCount, actorName);
    }
}

public interface IBattleEventSink
{
    void OnBattleEvent(BattleEvent battleEvent);
}
