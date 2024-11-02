

using System.Collections.Generic;
using UnityEngine;
using RandomExtensions;
using System.Linq;
using RandomExtensions.Linq;
using Unity.VisualScripting;
using R3;

/// <summary>
/// 戦闘の先手が起ったかどうか
/// </summary>
public enum BattleStartSituation
{
    alliFirst,EnemyFirst,Normal//味方先手、敵先手、ノーマル
}

/// <summary>
/// 行動リスト
/// </summary>
public class ACTList
{
    
    List<BaseStates> CharactorACTList;
    List<string> TopMessage;

    

    public int Count
    {
        get => CharactorACTList.Count;
    }

    public void Add(BaseStates chara,string mes = "")
    {
        CharactorACTList.Add(chara);
        TopMessage.Add(mes);
    }

    public  ACTList()
    {
        CharactorACTList=new List<BaseStates>();
        TopMessage = new List<string>();
    }

    /// <summary>
    /// インデックスで消す。
    /// </summary>
    /// <param name="index"></param>
    public void RemoveAt(int index)
    {
        CharactorACTList.RemoveAt(index);
        TopMessage.RemoveAt(index);
    }

    public string GetAtTopMessage(int index)
    {
        return TopMessage[index];
    }
    public BaseStates GetAtCharacter(int index)
    {
        return CharactorACTList[index];
    }


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

    private ACTList Acts;//行動先約リスト？

    private int BattleTurnCount;//バトルの経過ターン


    /// <summary>
    ///     コンストラクタ
    /// </summary>
    public BattleManager(BattleGroup allyGroup, BattleGroup enemyGroup,BattleStartSituation first)
    {
        AllyGroup = allyGroup;
        EnemyGroup = enemyGroup;
        firstSituation = first;
        Acts = new ACTList();

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
                Acts.Add(RandomEx.Shared.GetItem(CounterCharas),"ﾊﾝﾀｰﾅｲﾄﾉ糸ﾃﾞ彼ﾊ気ｶﾞ付ｲﾀ▼");
            }
            else
            {
                //グループの中から人数分アクションをいれる
                Acts.Add(RandomEx.Shared.GetItem(group.ToArray<BaseStates>()),$"先手{i}☆");
            }

        }

    }

    /// <summary>
    /// ランダムに次の人のターンを選出する。
    /// </summary>
    private BaseStates RandomTurn()
    {
        BaseStates Chara;//選出される人

        var Charas = new List<BaseStates>();//全ての人間の混合リスト
        Charas.AddRange(AllyGroup.Ours);
        Charas.AddRange(EnemyGroup.Ours);


        Chara = RandomEx.Shared.GetItem(Charas.ToArray<BaseStates>());//全ての人間からランダムで選ぶ

        return Chara;
    }

    string UniqueTopMessage;//通常メッセージの冠詞？
    BaseStates Acter;//今回の俳優
    /// <summary>
    /// 行動準備 次のボタンを決める
    /// </summary>
    public TabState ACTPop()
    {
        //もしすでにキャラクターが居たら、そのリストを先ずは消化する
        if (Acts.Count > 0)
        {
            UniqueTopMessage=Acts.GetAtTopMessage(0);//リストからメッセージとキャラクターをゲット。
            Acter = Acts.GetAtCharacter(0);
            Acts.RemoveAt(0);
        }
        else
        {
            //居なかったらランダムに選ぶ
            Acter = RandomTurn();
        }

        if (Acter.CanOprate)//操作するキャラクターなら
        {
            if(Acter.FreezeUseSkill == null)//強制続行中のスキルがなければ
            {
                //スキル選択ボタンを返す
                return TabState.Skill;
            }
        }

        return TabState.NextWait;


    }

}