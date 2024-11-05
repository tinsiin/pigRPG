

using System.Collections.Generic;
using UnityEngine;
using RandomExtensions;
using System.Linq;
using RandomExtensions.Linq;
using Unity.VisualScripting;
using R3;
using Cysharp.Threading.Tasks;
using UnityEditor.Experimental.GraphView;

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
            AddFirstBattleGroupTurn(allyGroup, enemyGroup);
            //味方が先手
        }
        else if (first == BattleStartSituation.EnemyFirst)
        {
            AddFirstBattleGroupTurn(EnemyGroup, allyGroup);
            Debug.Log("敵が先手");
        }
        else Debug.Log("お互い向き合って戦闘は始まった");
        //noraml、つまり普通に始まったら先約リスト二は何も始まらない

    }
    /// <summary>
    /// BaseStatesを継承したキャラクターのListから死亡者を省いたリストに変換する
    /// </summary>
    /// <param name="Charas"></param>
    /// <returns></returns>
    private List<BaseStates> RemoveDeathCharacters(List<BaseStates> Charas) 
    {
        return Charas.Where(chara => !chara.Death()).ToList();
    }

    /// <summary>
    /// キャラクター行動リストに先手分のリストを入れる。
    /// </summary>
    void AddFirstBattleGroupTurn(BattleGroup _group,BattleGroup _counterGroup)
    {
        var group = RemoveDeathCharacters(_group.Ours);//死者を取り除く

        //死者を取り除く　先手に対する切り返しであるハンターナイトが実行できる可能性のあるキャラクターを選別する
        List<BaseStates> CounterCharas = RemoveDeathCharacters(_counterGroup.GetCharactersFromImpression(SpiritualProperty.kindergarden, SpiritualProperty.godtier));

        for (var i = 0; i < group.Count; i++)//グループの人数分
        {
            if (i == group.Count - 1  && CounterCharas.Count>0 && RandomEx.Shared.NextInt(100) < 40)
            {//もし最後の先手ターンで後手グループにキンダーガーデンかゴッドティアがいて、　40%の確率が当たったら
                //反撃グループにいるそのどちらかの印象を持ったキャラクターのターンが入る。
                Acts.Add(RandomEx.Shared.GetItem(CounterCharas.ToArray()),_counterGroup.which,"ﾊﾝﾀｰﾅｲﾄ▼");
            }
            else
            {
                //グループの中から人数分アクションをいれる
                Acts.Add(RandomEx.Shared.GetItem(group.ToArray<BaseStates>()),_group.which,$"先手{i}☆");
            }

        }
        Debug.Log("actsの数 = " + Acts.Count);

    }

    /// <summary>
    /// ランダムに次の人のターンを選出する。
    /// </summary>
    private BaseStates RandomTurn()
    {
        BaseStates Chara;//選出される人

        List<BaseStates> Charas;//キャラリスト

        if (RandomEx.Shared.NextBool())//キャラリストから選ぶの決める
        {
            Charas = AllyGroup.Ours;
            Faction = WhichGroup.alliy;
        }
        else
        {
            Charas = EnemyGroup.Ours;
            Faction = WhichGroup.Enemyiy;
        }

        Charas = RemoveDeathCharacters(Charas);//死者を取り除く
        Chara = RandomEx.Shared.GetItem(Charas.ToArray<BaseStates>());//キャラリストからランダムで選ぶ

        return Chara;
    }
    /// <summary>
    /// BattleManager内の一時保存要素のリセット？
    /// </summary>
    private void ResetManagerTemp()
    {
        UniqueTopMessage = "";
    }
    /// <summary>
    /// 行動準備 次のボタンを決める
    /// </summary>
    public TabState ACTPop()
    {
        ResetManagerTemp();//一時保存要素をリセット

        //もしすでにキャラクターが居たら、そのリストを先ずは消化する
        if (Acts.Count > 0)
        {
            UniqueTopMessage=Acts.GetAtTopMessage(0);//リストからメッセージとキャラクターをゲット。
            Acter = Acts.GetAtCharacter(0);
            Faction = Acts.GetAtFaction(0);
            Debug.Log("俳優は先約リストから選ばれました");
        }
        else
        {
            //居なかったらランダムに選ぶ
            Acter = RandomTurn();
            Debug.Log("俳優はランダムに選ばれました");
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

    /// <summary>
    /// 発動カウントかスキル実行かで分岐
    /// </summary>
    /// <returns></returns>
    public TabState CharacterActBranching()
    {
        //スキル実行処理
        var skill = Acter.NowUseSkill;
        int count;//メッセージテキスト用のカウント数字
        if ((count = skill.TrigerCount()) >= 0)//発動カウントが0以上ならまだカウント中
        {
            return TriggerACT(count);//発動カウント処理
        }
        else//発動カウントが-1以下　つまりカウントしてないまたは終わったなら
        { 
            return SkillACT();
        }

    }
    /// <summary>
    /// 戦闘系のメッセージ作成
    /// </summary>
    /// <param name="txt"></param>
    private void CreateBattleMessage(string txt)
    {
        MessageDropper.Instance.CreateMessage(UniqueTopMessage + txt);
    }

    /// <summary>
    /// 発動カウントを実行
    /// </summary>
    private TabState TriggerACT(int count)
    {
        Debug.Log("発動カウント実行");
        var skill = Acter.NowUseSkill;
        if (skill.CanCancel == false)//キャンセル不可能の場合。
        {
            Acter.FreezeUseSkill = skill;//このスキルがキャンセル不可能として俳優に凍結される。
        }
        CreateBattleMessage($"{skill.SkillName}の発動カウント！残り{count}回。");//発動カウントのメッセージ

        //発動カウント時はスキルの複数回連続実行がありえないから、普通にターンが進む
        if (Acts.Count > 0)//先約リストでの実行なら削除
        Acts.RemoveAt(0);

        //Turnを進める
        BattleTurnCount++;

        return ACTPop();
    }

    /// <summary>
    /// スキルアクトを実行
    /// </summary>
    /// <returns></returns>
    private TabState SkillACT()
    {
        Debug.Log("スキル行使実行");

        //人数やスキルの攻撃傾向によって、被攻撃者の選別をする


        //単体の敵を対立したグループから狙うなら
        SelectTargetFromOpposingGroup();

        //実行処理
        CreateBattleMessage(Acter.AttackChara(UnderActer));//攻撃の処理からメッセージが返る。

        //スキル実行時に踏み込むのなら、俳優がグループ内の前のめり状態になる
        if (Acter.NowUseSkill.IsAggressiveCommit)
        {
            if(Faction == WhichGroup.alliy)
            {
                AllyGroup.InstantVanguard = Acter;
            }
            else
            {
                EnemyGroup.InstantVanguard = Acter;
            }
        }


        if (Acter.NowUseSkill.NextConsecutiveATK())//まだ連続実行するなら
        {
            if (Acts.Count <= 0)//先約リストに今回の実行者を入れとく
                Acts.Add(Acter, Faction);

            Acter.FreezeSkill();//連続実行の為凍結

        }
        else //複数実行が終わり
        {
           
            if(Acts.Count>0)//先約リストでの実行なら削除
            Acts.RemoveAt(0);

            Acter.FreezeUseSkill = null;//凍結されてもされてなくても空にしておく
            //Turnを進める
            BattleTurnCount++;
        }


        return ACTPop();

    }
    const int FrontGuardPer = 13;
    const int BackLineHITModifier = 70;
    /// <summary>
    /// 対立関係のグループからスキルの実行対象者"単体"を選びUnderActerに入れる関数
    /// 前のめりしてる奴かそうでない奴かを狙う感じ
    /// </summary>
    private void SelectTargetFromOpposingGroup()
    {
        var SelectGroup = new BattleGroup(AllyGroup.Ours,AllyGroup.OurImpression,AllyGroup.which);
        //味方なら敵グループから、敵なら味方グループから選別する ディープコピー。
        if (Faction == WhichGroup.alliy) SelectGroup = new BattleGroup(EnemyGroup.Ours, EnemyGroup.OurImpression, EnemyGroup.which);

        if (!Acter.NowUseSkill.HasType(SkillType.DeathHeal))//スキルが死回復要素がなければ
        {
            SelectGroup.SetCharactersList(RemoveDeathCharacters(SelectGroup.Ours));//死ぬ要素を省いたキャラクターリストに変換
        }

        if (SelectGroup.Ours.Count < 2)//選ばれる対立関係のグループに一人しかいない場合
        {
            UnderActer = SelectGroup.Ours[0];//普通にグループの一人だけを狙う
        }
        else//二人以上いたら前のめりかそうでないかでの分岐処理
        {
            if (Acter.ActWithoutHesitation())//前のめりしてる奴を狙うなら
            {
                UnderActer = SelectGroup.InstantVanguard;
                Debug.Log(Acter.CharacterName + "は前のめりしてる奴を狙った");
            }
            else
            {//後衛を狙おうとしたなら 後衛への命中率は7割補正され　そもそも1.3割の確率で前のめりしてる奴にあたる

                if (RandomEx.Shared.NextInt(100) < FrontGuardPer)//前衛のかばいに引っかかったら
                {
                    UniqueTopMessage += "テラーズヒット";//かばうテキストを追加？
                    UnderActer = SelectGroup.InstantVanguard;
                    Debug.Log(Acter.CharacterName + "は後衛を狙ったが前のめりしてる奴に阻まれた");
                }
                else//引っかかんなかったら前衛を抜いたメンバーから選ぶ
                {
                    List<BaseStates> BackLines;//後衛リスト

                    BackLines = new List<BaseStates>(SelectGroup.Ours.Where(member => member != SelectGroup.InstantVanguard));//前衛を抜いてディープコピーする

                    UnderActer = RandomEx.Shared.GetItem(BackLines.ToArray());//後衛リストからランダムで選択
                    Acter.SetHITPercentageModifier(BackLineHITModifier, "少し遠いよ");//後衛への命中率補正70%を追加。
                    Debug.Log(Acter.CharacterName + "は後衛を狙った");
                }
            }
        }
    }



}