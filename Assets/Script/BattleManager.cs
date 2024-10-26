

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
/// 一個一個の行動を示すクラス USERUIでの一回の操作単位を想定する
/// </summary>
public class ACTpart
{
    string Message;//画面に映るメッセージ

    BaseStates under;
    BaseStates Im;

    //useruiのstateを司る

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

    /// <summary>
    /// 全ての行動を記録するリスト
    /// </summary>
    private List<ACTpart> ALLACTList;



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
                //CharactorATKList.Add(RandomEx.Shared.GetItem<BaseStates>(AllyGroup.Ours.ToArray<BaseStates>()));
            }　　　　　　　　　　　　　　　　　　　　
        }
    }



    /// <summary>
    /// スキル行使をする。
    /// </summary>
    public void BattleTurn(BaseStates UnderAttacker)
    {

        //CharactorATKList[0].AttackChara(UnderAttacker);//攻撃行動をさせる。
        //CharactorATKList.RemoveAt(0);//最初の要素を削除        
    }
}