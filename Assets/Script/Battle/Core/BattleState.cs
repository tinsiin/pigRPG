using System.Collections.Generic;

public sealed class BattleState
{
    public int TurnCount;
    public bool Wipeout;
    public bool EnemyGroupEmpty;
    public bool AlliesRunOut;
    public NormalEnemy VoluntaryRunOutEnemy;
    public List<NormalEnemy> DominoRunOutEnemies = new();

    public void ResetTurnFlags()
    {
        EnemyGroupEmpty = false;
        VoluntaryRunOutEnemy = null;
        DominoRunOutEnemies.Clear();
    }
}