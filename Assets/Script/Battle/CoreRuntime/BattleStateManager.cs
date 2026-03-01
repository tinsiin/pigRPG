using System.Collections.Generic;

public sealed class BattleStateManager
{
    private readonly BattleState _state;
    private readonly IBattleLogger _logger;

    public BattleStateManager(BattleState state, IBattleLogger logger)
    {
        _state = state;
        _logger = logger ?? new NoOpBattleLogger();
    }

    public int TurnCount
    {
        get => _state.TurnCount;
        set => _state.TurnCount = value;
    }

    public bool Wipeout
    {
        get => _state.Wipeout;
        set => _state.Wipeout = value;
    }

    public bool EnemyGroupEmpty
    {
        get => _state.EnemyGroupEmpty;
        set => _state.EnemyGroupEmpty = value;
    }

    public bool AlliesRunOut
    {
        get => _state.AlliesRunOut;
        set => _state.AlliesRunOut = value;
    }

    public void ResetTurnFlags()
    {
        _state.ResetTurnFlags();
    }

    public void MarkWipeout()
    {
        GuardTerminalTransition("Wipeout");
        _state.Wipeout = true;
    }

    public void MarkEnemyGroupEmpty()
    {
        GuardTerminalTransition("EnemyGroupEmpty");
        _state.EnemyGroupEmpty = true;
    }

    public void MarkAlliesRunOut()
    {
        GuardTerminalTransition("AlliesRunOut");
        _state.AlliesRunOut = true;
    }

    private void GuardTerminalTransition(string nextState)
    {
        if (_state.Wipeout && nextState != "Wipeout")
        {
            _logger.LogWarning($"BattleStateManager: transition '{nextState}' while Wipeout is already true.");
            return;
        }
        if (_state.AlliesRunOut && nextState != "AlliesRunOut")
        {
            _logger.LogWarning($"BattleStateManager: transition '{nextState}' while AlliesRunOut is already true.");
            return;
        }
        if (_state.EnemyGroupEmpty && nextState != "EnemyGroupEmpty")
        {
            _logger.LogWarning($"BattleStateManager: transition '{nextState}' while EnemyGroupEmpty is already true.");
            return;
        }
    }
}
