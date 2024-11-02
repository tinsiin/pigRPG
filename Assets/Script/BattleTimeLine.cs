using System.Collections;
using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// 複数のBattleManagerを管理するクラス
/// つまり、USERUIでのボタン操作一つ一つを戦闘時に管理する感じ
/// </summary>
public class BattleTimeLine 
{
    private List<BattleManager> battleManagers;

    public BattleTimeLine(List<BattleManager> battleManagers)
    {
        this.battleManagers = battleManagers;
    }

    public void TimeNext()
    {
        foreach (var one in battleManagers)//
        {

        }
    }
}
