using System.Collections.Generic;
using UnityEngine;

public sealed class BattleStateManager
{
    private readonly BattleState _state;

    public BattleStateManager(BattleState state)
    {
        _state = state;
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

    public NormalEnemy VoluntaryRunOutEnemy
    {
        get => _state.VoluntaryRunOutEnemy;
        set => _state.VoluntaryRunOutEnemy = value;
    }

    public List<NormalEnemy> DominoRunOutEnemies => _state.DominoRunOutEnemies;

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

    public void SetVoluntaryRunOutEnemy(NormalEnemy enemy)
    {
        _state.VoluntaryRunOutEnemy = enemy;
    }

    public void AddDominoRunOutEnemy(NormalEnemy enemy)
    {
        if (enemy == null) return;
        _state.DominoRunOutEnemies.Add(enemy);
    }

    public void ClearDominoRunOutEnemies()
    {
        _state.DominoRunOutEnemies.Clear();
    }

    private void GuardTerminalTransition(string nextState)
    {
        if (_state.Wipeout && nextState != "Wipeout")
        {
            Debug.LogWarning($"BattleStateManager: transition '{nextState}' while Wipeout is already true.");
            return;
        }
        if (_state.AlliesRunOut && nextState != "AlliesRunOut")
        {
            Debug.LogWarning($"BattleStateManager: transition '{nextState}' while AlliesRunOut is already true.");
            return;
        }
        if (_state.EnemyGroupEmpty && nextState != "EnemyGroupEmpty")
        {
            Debug.LogWarning($"BattleStateManager: transition '{nextState}' while EnemyGroupEmpty is already true.");
            return;
        }
    }
}
