using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 戦闘の管理クラス
/// </summary>
public class BatlleManager
{
    /// <summary>
    /// 味方グループ
    /// </summary>
    BattleGroup Alliy;
    /// <summary>
    /// 敵グループ
    /// </summary>
    BattleGroup Enemy;
    public BatlleManager(BattleGroup ali,BattleGroup ene)
    {
         Alliy = ali;
        Enemy = ene;
    }
}
