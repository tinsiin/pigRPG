

using System.Collections.Generic;
using UnityEngine;
using RandomExtensions;
using System.Linq;

/// <summary>
/// 戦闘の先手が起ったかどうか
/// </summary>
public enum BattleStartSituation
{
    alliFirst,EnemyFirst,Normal//味方先手、敵先手、ノーマル
}

/// <summary>
///     バトルを、管理するクラス
/// </summary>
public class BattleManager
{
    
    
    /// <summary>
    ///     プレイヤー側のバトルグループ　ここに味方のバトルグループオブジェクトをリンクする？
    /// </summary>
    private BattleGroup AllyGroup;

    /// <summary>
    ///     敵側のバトルグループ　ここに敵グループのバトルグループオブジェクトをリンクする？
    /// </summary>
    private BattleGroup EnemyGroup;

    private List<BaseStates> CharactorATKList;

    /// <summary>
    ///     コンストラクタ
    /// </summary>
    public BattleManager(BattleGroup allyGroup, BattleGroup enemyGroup,BattleStartSituation first)
    {
        AllyGroup = allyGroup;
        EnemyGroup = enemyGroup;

        if(first == BattleStartSituation.alliFirst)
        {
            for(var i = 0; i < 3; i++)
            {
                //味方グループの中から三人分アクションをいれる
                CharactorATKList.Add(RandomEx.Shared.GetItem<BaseStates>(AllyGroup.Ours.ToArray<BaseStates>()));
            }
        }
    }

    /// <summary>
    /// スキル行使をする。
    /// </summary>
    public void BattleTurn()
    {
        

        CharactorATKList.RemoveAt(0);//最初の要素を削除        
    }
}