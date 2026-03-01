using System.Collections.Generic;

public sealed class BattleState
{
    public int TurnCount;
    public bool Wipeout;
    public bool EnemyGroupEmpty;
    public bool AlliesRunOut;
    public void ResetTurnFlags()
    {
        EnemyGroupEmpty = false;
    }
}