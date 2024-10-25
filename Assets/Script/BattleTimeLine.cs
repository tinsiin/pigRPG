using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ACTpart
{

}

/// <summary>
/// 複数のBattleManagerを管理するクラス
/// つまり、USERUIでのボタン操作一つ一つを戦闘時に管理する感じ
/// </summary>
public class BattleTimeLine : MonoBehaviour
{
    private List<BattleManager> battleManagers;

    /// <summary>
    /// 全ての行動を記録するリスト
    /// </summary>
    private List<ACTpart> ALLACTList;

    public BattleTimeLine(List<BattleManager> battleManagers)
    {
        this.battleManagers = battleManagers;
    }

    public void TimeNext()
    {
        foreach (var one in battleManagers)//
        {
            one.BattleTurn();
        }
    }
}
