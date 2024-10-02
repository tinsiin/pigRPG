using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 複数のBattleManagerを管理するクラス
/// </summary>
public class BattleTimeLine : MonoBehaviour
{
    private List<BattleManager> battleManagers;

    public BattleTimeLine(List<BattleManager> battleManagers)
    {
        this.battleManagers = battleManagers;
    }
}
