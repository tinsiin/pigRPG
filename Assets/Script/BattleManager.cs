

using System.Collections.Generic;
using UnityEngine;
using RandomExtensions;
using System.Linq;
using RandomExtensions.Linq;

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

    private BattleStartSituation firstSituation;

    private List<BaseStates> CharactorACTList;

    private int BattleTurnCount;//バトルの経過ターン


    /// <summary>
    ///     コンストラクタ
    /// </summary>
    public BattleManager(BattleGroup allyGroup, BattleGroup enemyGroup,BattleStartSituation first)
    {
        AllyGroup = allyGroup;
        EnemyGroup = enemyGroup;
        firstSituation = first;

        //敵か味方どちらかが先手を取ったかによって、
        if (first == BattleStartSituation.alliFirst)
        {
            AddFirstBattleGroupTurn(allyGroup);
        }
        else if(first == BattleStartSituation.EnemyFirst)
        {
            AddFirstBattleGroupTurn(EnemyGroup);
        }
    }

    /// <summary>
    /// キャラクター行動リストに先手分のリストを入れる。
    /// </summary>
    void AddFirstBattleGroupTurn(BattleGroup _group)
    {
        var group = _group.Ours;
        BaseStates[] CounterCharas = _group.GetCharactersFromImpression(SpiritualProperty.kindergarden, SpiritualProperty.godtier);
        for (var i = 0; i < group.Count; i++)//グループの人数分
        {
            if (i == group.Count - 1  && CounterCharas.Length>0 && RandomEx.Shared.NextInt(100) < 40)
            {//もし最後の先手ターンで敵グループにキンダーガーデンかゴッドティアがいて、　40%の確率が当たったら
                //反撃グループにいるそのどちらかの印象を持ったキャラクターのターンが入る。
                CharactorACTList.Add(RandomEx.Shared.GetItem<BaseStates>(CounterCharas));
            }
            else
            {
                //グループの中から人数分アクションをいれる
                CharactorACTList.Add(RandomEx.Shared.GetItem<BaseStates>(group.ToArray<BaseStates>()));
            }

        }

    }

    /// <summary>
    /// ランダムに次の人のターンを選出する。
    /// </summary>
    private void RandomTurn()
    {
        //もしすでにキャラクターが居たら、そのリストを先ずは消化する

        //いなければランダムで行動者が選ばれる
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