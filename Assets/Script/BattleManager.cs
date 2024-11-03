

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
///敵味方どっち
/// </summary>
public enum WhichGroup
{
    alliy, Enemyiy
}



/// <summary>
/// 行動リスト
/// </summary>
public class ACTList
{

    List<BaseStates> CharactorACTList;
    List<string> TopMessage;
    List<WhichGroup> FactionList;//陣営


    public int Count
    {
        get => CharactorACTList.Count;
    }

    public void Add(BaseStates chara,WhichGroup fac,string mes = "")
    {
        CharactorACTList.Add(chara);
        FactionList.Add(fac);
        TopMessage.Add(mes);
        
    }

    public  ACTList()
    {
        CharactorACTList=new List<BaseStates>();
        TopMessage = new List<string>();
        FactionList = new List<WhichGroup>();
    }

    /// <summary>
    /// インデックスで消す。
    /// </summary>
    /// <param name="index"></param>
    public void RemoveAt(int index)
    {
        CharactorACTList.RemoveAt(index);
        TopMessage.RemoveAt(index);
        FactionList.RemoveAt(index);
    }

    public string GetAtTopMessage(int index)
    {
        return TopMessage[index];
    }
    public BaseStates GetAtCharacter(int index)
    {
        return CharactorACTList[index];
    }
    public WhichGroup GetAtFaction(int index)
    {
        return FactionList[index];
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


    string UniqueTopMessage;//通常メッセージの冠詞？
    BaseStates Acter;//今回の俳優
    BaseStates UnderActer;//行動を受ける人
    WhichGroup Faction;//陣営



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
            AddFirstBattleGroupTurn(allyGroup,enemyGroup);
        }
        else if(first == BattleStartSituation.EnemyFirst)
        {
            AddFirstBattleGroupTurn(EnemyGroup,allyGroup);
        }
    }

    /// <summary>
    /// キャラクター行動リストに先手分のリストを入れる。
    /// </summary>
    void AddFirstBattleGroupTurn(BattleGroup _group,BattleGroup _counterGroup)
    {
        var group = _group.Ours;
        BaseStates[] CounterCharas = _counterGroup.GetCharactersFromImpression(SpiritualProperty.kindergarden, SpiritualProperty.godtier);
        for (var i = 0; i < group.Count; i++)//グループの人数分
        {
            if (i == group.Count - 1  && CounterCharas.Length>0 && RandomEx.Shared.NextInt(100) < 40)
            {//もし最後の先手ターンで後手グループにキンダーガーデンかゴッドティアがいて、　40%の確率が当たったら
                //反撃グループにいるそのどちらかの印象を持ったキャラクターのターンが入る。
                Acts.Add(RandomEx.Shared.GetItem(CounterCharas),_counterGroup.which,"ﾊﾝﾀｰﾅｲﾄ▼");
            }
            else
            {
                //グループの中から人数分アクションをいれる
                Acts.Add(RandomEx.Shared.GetItem(group.ToArray<BaseStates>()),_group.which,$"先手{i}☆");
            }

        }

    }

    /// <summary>
    /// ランダムに次の人のターンを選出する。
    /// </summary>
    private BaseStates RandomTurn()
    {
        BaseStates Chara;//選出される人

        var Charas = new List<BaseStates>();//キャラリスト

        if (RandomEx.Shared.NextBool())//キャラリストから選ぶの決める
        {
            Charas.AddRange(AllyGroup.Ours);
            Faction = WhichGroup.alliy;
        }
        else
        {
            Charas.AddRange(EnemyGroup.Ours);
            Faction = WhichGroup.Enemyiy;
        }


        Chara = RandomEx.Shared.GetItem(Charas.ToArray<BaseStates>());//キャラリストからランダムで選ぶ

        return Chara;
    }
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
            Faction = Acts.GetAtFaction(0);
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
            else//スキル強制続行中なら、
            {
                Acter.NowUseSkill = Acter.FreezeUseSkill;//操作の代わりに使用スキルに強制続行スキルを入れとく
            }
        }

        return TabState.NextWait;


    }

    const int FrontGuardPer = 20;
    const int BackLineHITModifier = 70;
    /// <summary>
    /// characterが行動すると次のボタンのあれが出る
    /// </summary>
    /// <returns></returns>
    public TabState CharacterACT()
    {
        //スキル実行処理
        var skill = Acter.NowUseSkill;
        int count;//メッセージテキスト用のカウント数字
        if ((count = skill.TrigerCount()) >= 0)//発動カウントが0以上ならまだカウント中
        {
            if(skill.CanCancel == false)//キャンセル不可能の場合。
            {
                Acter.FreezeUseSkill = skill;//このスキルがキャンセル不可能として俳優に凍結される。
            }
            MessageDropper.Instance.CreateMessage($"{skill.SkillName}の発動カウント！残り{count}回。");//発動カウントのメッセージ

            //発動カウント時はスキルの複数回連続実行がありえないから、普通に消す
            Acts.RemoveAt(0);

            //Turnを進める
            BattleTurnCount++;
            return ACTPop();
        }
        else//発動カウントが-1以下　つまりカウントしてないまたは終わったなら
        {//この後の被害者選別の実行を行ってからスキル実行を行う。

            Acter.FreezeUseSkill= null;//凍結されてもされてなくても空にしておく

        }


        if (Acter.ActWithoutHesitation())//前のめりしてる奴を狙うなら
        {

            if (Faction == WhichGroup.alliy)//味方なら敵を
            {
                UnderActer = EnemyGroup.InstantVanguard;
            }
            else UnderActer = AllyGroup.InstantVanguard;//敵なら味方を
        }
        else
        {//後衛を狙おうとしたなら 後衛への命中率は7割補正され　そもそも2割の確率で前のめりしてる奴にあたる

            if (RandomEx.Shared.NextInt(100) < FrontGuardPer)//前衛のかばいに引っかかったら
            {
                if (Faction == WhichGroup.alliy)//味方なら敵の前のめりを
                {
                    UnderActer = EnemyGroup.InstantVanguard;
                }
                else//敵なら味方の前のめりを
                {
                    UnderActer = AllyGroup.InstantVanguard;
                }
            }
            else//引っかかんなかったら前衛を抜いたメンバーから選ぶ
            {
                List<BaseStates> BackLines;//後衛リスト
                if (Faction == WhichGroup.alliy)//味方なら敵を
                {
                    BackLines = new List<BaseStates>(EnemyGroup.Ours.Where(member => member != EnemyGroup.InstantVanguard));//前衛を抜いてディープコピーする
                }
                else//敵なら味方を
                {
                    BackLines = new List<BaseStates>(AllyGroup.Ours.Where(member => member != AllyGroup.InstantVanguard));
                }
                UnderActer = RandomEx.Shared.GetItem(BackLines.ToArray());//後衛リストからランダムで選択
                Acter.SetHITPercentageModifier(BackLineHITModifier,"少し遠いよ");//後衛への命中率補正70%を追加。
            }
        }

        //攻撃回数に応じた実行処理

        //複数実行が終わり、今回の俳優を消す。
        Acts.RemoveAt(0);
        //Turnを進める
        BattleTurnCount++;

        return TabState.NextWait;
    }


}