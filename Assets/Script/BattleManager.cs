

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
    alliFirst, EnemyFirst, Normal//味方先手、敵先手、ノーマル
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

    public void Add(BaseStates chara, WhichGroup fac, string mes = "")
    {
        CharactorACTList.Add(chara);
        FactionList.Add(fac);
        TopMessage.Add(mes);

    }

    public ACTList()
    {
        CharactorACTList = new List<BaseStates>();
        TopMessage = new List<string>();
        FactionList = new List<WhichGroup>();
    }
    /// <summary>
    /// 先約リスト内から死者を取り除く
    /// </summary>
    public void RemoveDeathCharacters()
    {
        CharactorACTList = CharactorACTList.Where(chara => !chara.Death()).ToList();
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
    /// <summary>
    /// 行動を受ける人 これの順番によってスキルの三割合が当てはまる
    /// </summary>
    List<BaseStates> UnderActer;
    WhichGroup Faction;//陣営
    bool Wipeout = false;//全滅したかどうか
    bool RunOut = false;//逃走


    /// <summary>
    /// 行動リスト　ここではrecovelyTurnの制約などは存在しません
    /// </summary>
    private ACTList Acts;//行動先約リスト？

    private int BattleTurnCount;//バトルの経過ターン


    /// <summary>
    ///     コンストラクタ
    /// </summary>
    public BattleManager(BattleGroup allyGroup, BattleGroup enemyGroup, BattleStartSituation first)
    {
        AllyGroup = allyGroup;
        EnemyGroup = enemyGroup;
        firstSituation = first;
        Acts = new ACTList();

        OnBattleStart();//初期化コールバック

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
    private List<BaseStates> RemoveDeathCharacters(List<BaseStates> Charas)
    {
        return Charas.Where(chara => !chara.Death()).ToList();
    }

    /// <summary>
    /// BaseStatesを継承したキャラクターのListからrecovelyTurnのカウントアップして行動状態に回復出来る奴だけを選別する
    /// </summary>
    private List<BaseStates> RetainActionableCharacters(List<BaseStates> Charas)
    {
        //少なくとも一人のキャラクターが RecovelyBattleField(BattleTurnCount) を満たすと、Any() が true を返し、while ループが終了します。
        while (!(Charas = Charas.Where(chara => chara.RecovelyBattleField(BattleTurnCount)).ToList()).Any())
        {
            BattleTurnCount++;//全員再行動までのクールタイム中なら、全員硬直したまま時間が進む
        }
        return Charas;
    }
    /// <summary>
    /// キャラクター行動リストに先手分のリストを入れる。
    /// </summary>
    void AddFirstBattleGroupTurn(BattleGroup _group, BattleGroup _counterGroup)
    {
        var group = RemoveDeathCharacters(_group.Ours);//死者を取り除く

        //死者を取り除く　先手に対する切り返しであるハンターナイトが実行できる可能性のあるキャラクターを選別する
        List<BaseStates> CounterCharas = RemoveDeathCharacters(_counterGroup.GetCharactersFromImpression(SpiritualProperty.kindergarden, SpiritualProperty.godtier));

        for (var i = 0; i < group.Count; i++)//グループの人数分
        {
            if (i == group.Count - 1 && CounterCharas.Count > 0 && RandomEx.Shared.NextInt(100) < 40)
            {//もし最後の先手ターンで後手グループにキンダーガーデンかゴッドティアがいて、　40%の確率が当たったら
                //反撃グループにいるそのどちらかの印象を持ったキャラクターのターンが入る。
                Acts.Add(RandomEx.Shared.GetItem(CounterCharas.ToArray()), _counterGroup.which, "ﾊﾝﾀｰﾅｲﾄ▼");
            }
            else
            {
                //グループの中から人数分アクションをいれる
                Acts.Add(RandomEx.Shared.GetItem(group.ToArray<BaseStates>()), _group.which, $"先手{i}☆");
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
        Charas = RetainActionableCharacters(Charas);//再行動をとれる人間のみに絞る
        Chara = RandomEx.Shared.GetItem(Charas.ToArray<BaseStates>());//キャラリストからランダムで選ぶ
        Chara.RecovelyWaitStart();//選ばれたので次に行動できるまでまた再カウント開始

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
        Acts.RemoveDeathCharacters();//先約リストから死者を取り除く

        //パーティーの死亡判定
        if (AllyGroup.PartyDeath())
        {
            Wipeout = true;
            Faction = WhichGroup.alliy;
            return TabState.NextWait;//押して処理
        }
        else if (EnemyGroup.PartyDeath())
        {
            Wipeout = true;
            Faction = WhichGroup.Enemyiy;
            return TabState.NextWait;//押して処理
        }

        //もし先約リストにキャラクターが居たら、そのリストを先ずは消化する
        if (Acts.Count > 0)
        {
            UniqueTopMessage = Acts.GetAtTopMessage(0);//リストからメッセージとキャラクターをゲット。
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

        //俳優が味方なら
        var ps = PlayersStates.Instance;
        if (Acter == ps.geino || Acter == ps.sites || Acter == ps.noramlia)
        {
            if (Acter.FreezeUseSkill == null)//強制続行中のスキルがなければ
            {
                switch (Acter)
                {
                    case StairStates:
                        break;

                    case SateliteProcessStates:
                        break;
                    case BaseStates: 
                        break;
                        
                }
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
        if (Wipeout || RunOut) //全滅か逃走かで終了アクトへ
        {
            return DialogEndACT();
        }

        //スキル実行処理
        var skill = Acter.NowUseSkill;
        int count;//メッセージテキスト用のカウント数字
        if ((count = skill.TrigerCount()) >= 0)//発動カウントが0以上ならまだカウント中
        {
            return TriggerACT(count);//発動カウント処理
        }
        else//発動カウントが-1以下　つまりカウントしてないまたは終わったなら
        {
            skill.DoneTrigger();//トリガーのカウントを成功したときの戻らせ方させて
            return SkillACT();
        }

    }
    /// <summary>
    /// メッセージと共に終わらせる
    /// </summary>
    public TabState DialogEndACT()
    {
        if (Wipeout)
        {
            if (Faction == WhichGroup.alliy)
            {
                MessageDropper.Instance.CreateMessage("死んだ");
            }
            else
            {
                MessageDropper.Instance.CreateMessage("勝ち抜いた");
            }
        }
        if (RunOut) MessageDropper.Instance.CreateMessage("逃げた");

        OnBattleEnd();
        return TabState.walk;
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
            Acter.FreezeSkill();//このスキルがキャンセル不可能として俳優に凍結される。
        }
        CreateBattleMessage($"{skill.SkillName}の発動カウント！残り{count}回。");//発動カウントのメッセージ

        //発動カウント時はスキルの複数回連続実行がありえないから、普通にターンが進む
        NextTurn(true);

        return ACTPop();
    }

    /// <summary>
    /// スキルアクトを実行
    /// </summary>
    /// <returns></returns>
    private TabState SkillACT()
    {
        Debug.Log("スキル行使実行");
        var skill = Acter.NowUseSkill;

        if (Faction == WhichGroup.Enemyiy)//敵ならここで思考して決める
        {
            var ene = Acter as NormalEnemy;
            ene.SkillAI();//ここで決めないとスキル可変オプションが下記の対象者選択で反映されないから
        }

        //人数やスキルの攻撃傾向によって、被攻撃者の選別をする

        SelectTargetFromWill();


        //実行処理
        skill.SetDeltaTurn(BattleTurnCount);//スキルのdeltaTurnをセット
        CreateBattleMessage(Acter.AttackChara(UnderActer));//攻撃の処理からメッセージが返る。

        //スキル実行時に踏み込むのなら、俳優がグループ内の前のめり状態になる
        if (skill.IsAggressiveCommit)
        {
            if (Faction == WhichGroup.alliy)
            {
                AllyGroup.InstantVanguard = Acter;
            }
            else
            {
                EnemyGroup.InstantVanguard = Acter;
            }
        }


        if (skill.NextConsecutiveATK())//まだ連続実行するなら
        {
            NextTurn(false);

            if (Acts.Count <= 0)//先約リストに今回の実行者を入れとく
                Acts.Add(Acter, Faction);

            Acter.FreezeSkill();//連続実行の為凍結

        }
        else //複数実行が終わり
        {
            Acter.Defrost();//凍結されてもされてなくても空にしておく
            Acter.RecovelyCountTmpAdd(skill.SKillDidWaitCount);//スキルに追加硬直値があるならキャラクターの再行動クールタイムに追加

            NextTurn(true);
        }


        return ACTPop();

    }
    const int FrontGuardPer = 13;
    const int BackLineHITModifier = 70;
    /// <summary>
    /// スキルの実行者をunderActerに入れる処理　意思が実際の選別に状況を伴って変換される処理
    /// </summary>
    private void SelectTargetFromWill()
    {
        BattleGroup SelectGroup;//我々から見た敵陣
        BattleGroup OurGroup = null;//我々自陣          nullの場合はスキルの範囲性質に味方選択がないということ
        var skill = Acter.NowUseSkill;
        List<BaseStates> UA = null;

        //選抜グループ決定する処理☆☆☆☆☆☆☆☆☆☆☆
        if (Faction == WhichGroup.alliy)
        {//味方なら敵グループから、
            SelectGroup = new BattleGroup(EnemyGroup.Ours, EnemyGroup.OurImpression, EnemyGroup.which);

            if (skill.HasZoneTrait(SkillZoneTrait.CanSelectAlly))//自陣も対象に選べるなら
            {
                OurGroup = new BattleGroup(AllyGroup.Ours, AllyGroup.OurImpression, AllyGroup.which);//自陣
            }
        }
        else
        {//敵なら味方グループから選別する ディープコピー。
            SelectGroup = new BattleGroup(AllyGroup.Ours, AllyGroup.OurImpression, AllyGroup.which);
            if (skill.HasZoneTrait(SkillZoneTrait.CanSelectAlly))//自陣も対象に選べるなら
            {
                OurGroup = new BattleGroup(EnemyGroup.Ours, EnemyGroup.OurImpression, EnemyGroup.which);//自陣
            }

        }

        //死者は省く
        if (!skill.HasZoneTrait(SkillZoneTrait.CanSelectDeath))//死を選べないのなら　死を省く
        {
            SelectGroup.SetCharactersList(RemoveDeathCharacters(SelectGroup.Ours));
            if (OurGroup != null)
            {
                OurGroup.SetCharactersList(RemoveDeathCharacters(OurGroup.Ours));//自陣もあったら省く
            }
        }

        //行動者決定☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆

        if (skill.HasZoneTrait(SkillZoneTrait.CanPerfectSelectSingleTarget))//完全に一人一人を選ぶのなら
        {

        }
        else
        {
            //選ばれる対立関係のグループに一人しかいない場合
            if (SelectGroup.Ours.Count < 2)
            {
                Debug.Log(AllyGroup.Ours.Count + "←allyGroup EnemyGroup→" + EnemyGroup.Ours.Count + " SelectGroup→" + SelectGroup.Ours.Count + "陣営は" + Acter.CharacterName);
                UA.Add(SelectGroup.Ours[0]);//普通にグループの一人だけを狙う
            }
            else//二人以上いたら前のめりかそうでないかでの分岐処理
            {
                if (skill.HasZoneTrait(SkillZoneTrait.CanSelectSingleTarget))//前のめりか後衛(内ランダム単体)で選択する
                {
                    if (SelectGroup.InstantVanguard == null)//対象者グループに前のめりがいない場合。
                    {
                        UA.Add(RandomEx.Shared.GetItem(SelectGroup.Ours.ToArray()));//s選別リストからランダムで選択
                    }
                    else//前のめりがいるなら
                    {
                        if (Acter.Target == DirectedWill.InstantVanguard)//前のめりしてる奴を狙うなら
                        {
                            UA.Add(SelectGroup.InstantVanguard);
                            Debug.Log(Acter.CharacterName + "は前のめりしてる奴を狙った");
                        }
                        else if (Acter.Target == DirectedWill.BacklineOrAny)
                        {//後衛を狙おうとしたなら 後衛への命中率は7割補正され　そもそも1.3割の確率で前のめりしてる奴にあたる

                            if (RandomEx.Shared.NextInt(100) < FrontGuardPer)//前衛のかばいに引っかかったら
                            {
                                UniqueTopMessage += "テラーズヒット";//かばうテキストを追加？
                                UA.Add(SelectGroup.InstantVanguard);
                                Debug.Log(Acter.CharacterName + "は後衛を狙ったが前のめりしてる奴に阻まれた");
                            }
                            else//引っかかんなかったら前衛を抜いたメンバーから選ぶ
                            {
                                List<BaseStates> BackLines;//後衛リスト

                                //前衛を抜いてディープコピーする
                                BackLines = new List<BaseStates>(SelectGroup.Ours.Where(member => member != SelectGroup.InstantVanguard));

                                UA.Add(RandomEx.Shared.GetItem(BackLines.ToArray()));//後衛リストからランダムで選択
                                Acter.SetHITPercentageModifier(BackLineHITModifier, "少し遠いよ");//後衛への命中率補正70%を追加。
                                Debug.Log(Acter.CharacterName + "は後衛を狙った");
                            }
                        }
                        else Debug.LogError("CanSelectSingleTargetの処理では前のめりか後衛以外の意志を受け付けていません。");
                    }

                }
                else if (skill.HasZoneTrait(SkillZoneTrait.RandomSingleTarget))//完全にランダムの単体対象
                {
                    BaseStates[] selects = SelectGroup.Ours.ToArray();
                    if (OurGroup != null)//自陣グループも選択可能なら
                        selects.AddRange(OurGroup.Ours);

                    UA.Add(RandomEx.Shared.GetItem(selects));//s選別リストからランダムで選択
                }
                else if (skill.HasZoneTrait(SkillZoneTrait.ControlByThisSituation))//状況のみに縛られる。(前のめりにしか当たらないなら
                {
                    if (SelectGroup.InstantVanguard == null)//対象者グループに前のめりがいない場合。事故が起きる
                    {
                        //前のめりしか選べなくても、もし前のめりがいなかったら、その**平坦なグループ**にスキル性質による攻撃が当たる。


                        //前のめりいないことによる事故☆☆☆☆☆☆☆☆☆☆

                        //シングルにあたるなら
                        if (skill.HasZoneTrait(SkillZoneTrait.RandomSingleTarget))
                        {
                            UA.Add(RandomEx.Shared.GetItem(SelectGroup.Ours.ToArray()));//選別リストからランダムで選択
                        }
                        //前のめりがいないんだから、　前のめりか後衛単位での　集団事故は起こらないため　RandomSelectMultiTargetによる場合分けはない。

                        //全範囲事故なら
                        if (skill.HasZoneTrait(SkillZoneTrait.AllTarget))
                        {
                            BaseStates[] selects = SelectGroup.Ours.ToArray();
                            if (OurGroup != null)//自陣グループも選択可能なら
                                selects.AddRange(OurGroup.Ours);

                            UA.AddRange(selects);//対象範囲を全て加える
                        }
                        //ランダム範囲事故なら
                        if (skill.HasZoneTrait(SkillZoneTrait.RandomMultiTarget))
                        {
                            List<BaseStates> selects = SelectGroup.Ours;
                            if (OurGroup != null)//自陣グループも選択可能なら
                                selects.AddRange(OurGroup.Ours);

                            var count = selects.Count;//群体の数を取得
                            count = RandomEx.Shared.NextInt(1, count + 1);//取得する数もランダム

                            for (int i = 0; i < count; i++) //ランダムな数分引き抜く
                            {
                                var item = RandomEx.Shared.GetItem(selects.ToArray());
                                UA.Add(item);//選別リストからランダムで選択
                                selects.Remove(item);//選択したから除去
                            }
                        }
                    }
                    else
                    {
                        UA.Add(SelectGroup.InstantVanguard);//前のめりしか狙えない
                    }

                }
                else if (skill.HasZoneTrait(SkillZoneTrait.CanSelectMultiTarget))//前衛、後衛単位の範囲でランダムに狙うなら
                {
                    if (SelectGroup.InstantVanguard == null)//対象者グループに前のめりがいない場合。最大二人範囲で攻撃
                    {
                        //グループ全員のリストで回すが、もし三人目に行きそうになったら、止める
                        var counter = 0;
                        SelectGroup.Ours.Shuffle();//リスト内でシャッフル
                        foreach (var one in SelectGroup.Ours)
                        {
                            UA.Add(one);
                            counter++;
                            if (counter >= 2) break;//二人目を入れたらbreak　二人目まで行かなくてもforEachで勝手に終わる
                        }
                    }
                    else//前のめりいたら
                    {
                        if (Acter.Target == DirectedWill.InstantVanguard)//前のめりしてる奴を狙うなら
                        {
                            UA.Add(SelectGroup.InstantVanguard);
                            Debug.Log(Acter.CharacterName + "は前のめりしてる奴を狙った");
                        }
                        else if (Acter.Target == DirectedWill.BacklineOrAny)
                        {//後衛を狙おうとしたなら 後衛への命中率は9割補正され　そもそも1.2割の確率で前のめりしてる奴にあたる

                            if (RandomEx.Shared.NextInt(100) < 12)//前衛のかばいに引っかかったら
                            {
                                UniqueTopMessage += "テラーズヒット";//かばうテキストを追加？
                                UA.Add(SelectGroup.InstantVanguard);
                                Debug.Log(Acter.CharacterName + "は後衛を狙ったが前のめりしてる奴に阻まれた");
                            }
                            else//引っかかんなかったら後衛を丸々攻撃
                            {
                                List<BaseStates> BackLines;//後衛リスト

                                //前衛を抜いてディープコピーする
                                BackLines = new List<BaseStates>(SelectGroup.Ours.Where(member => member != SelectGroup.InstantVanguard));

                                UA.AddRange(BackLines);//後衛をそのまま入れる
                                Acter.SetHITPercentageModifier(90, "ほんの少し狙いにくい");//後衛への命中率補正を追加。
                                Debug.Log(Acter.CharacterName + "は後衛を狙った");
                            }
                        }
                        else Debug.LogError("CanSelectMultiTargetの処理では前のめりか後衛以外の意志を受け付けていません。");

                    }
                }
                else if (skill.HasZoneTrait(SkillZoneTrait.RandomSelectMultiTarget))//前衛または後衛単位をランダムに狙う
                {
                    var selectVanguard = RandomEx.Shared.NextBool();//前衛を選ぶかどうか

                    //前のめりがいなかったら
                    if (SelectGroup.InstantVanguard == null)//対象者グループに前のめりがいない場合。最大二人範囲で攻撃
                    {
                        //グループ全員のリストで回すが、もし三人目に行きそうになったら、止める
                        var counter = 0;
                        SelectGroup.Ours.Shuffle();//リスト内でシャッフル
                        foreach (var one in SelectGroup.Ours)
                        {
                            UA.Add(one);
                            counter++;
                            if (counter >= 2) break;//二人目を入れたらbreak　二人目まで行かなくてもforEachで勝手に終わる
                        }
                    }
                    else//前のめりいたら
                    {
                        if (selectVanguard) //前のめりを選ぶなら
                        {
                            UA.Add(SelectGroup.InstantVanguard);
                            Debug.Log(Acter.CharacterName + "の技は前のめりしてる奴に向いた");
                        }
                        else//後衛単位を狙うなら
                        {
                            List<BaseStates> BackLines;//後衛リスト

                            //前衛を抜いてディープコピーする
                            BackLines = new List<BaseStates>(SelectGroup.Ours.Where(member => member != SelectGroup.InstantVanguard));

                            UA.AddRange(BackLines);//後衛をそのまま入れる
                            Debug.Log(Acter.CharacterName + "の技は後衛に向いた");

                        }
                    }
                }
                else if (skill.HasZoneTrait(SkillZoneTrait.RandomMultiTarget))//ランダムな範囲攻撃の場合
                {
                    List<BaseStates> selects = SelectGroup.Ours;
                    if (OurGroup != null)//自陣グループも選択可能なら
                        selects.AddRange(OurGroup.Ours);

                    var count = selects.Count;//群体の数を取得
                    count = RandomEx.Shared.NextInt(1, count + 1);//取得する数もランダム

                    for (int i = 0; i < count; i++) //ランダムな数分引き抜く
                    {
                        var item = RandomEx.Shared.GetItem(selects.ToArray());
                        UA.Add(item);//選別リストからランダムで選択
                        selects.Remove(item);//選択したから除去
                    }
                }
                else if (skill.HasZoneTrait(SkillZoneTrait.AllTarget))//全範囲
                {
                    BaseStates[] selects = SelectGroup.Ours.ToArray();
                    if (OurGroup != null)//自陣グループも選択可能なら
                        selects.AddRange(OurGroup.Ours);

                    UA.AddRange(selects);//対象範囲を全て加える

                }
            }

            UnderActer = UA;
        }
    }
    private void NextTurn(bool Next)
    {
        if (Acts.Count > 0)//先約リストでの実行なら削除
            Acts.RemoveAt(0);

        //Turnを進める
        if (!Next)
            BattleTurnCount++;

        //前のめり者が死亡してたら、nullにする処理
        AllyGroup.VanGuardDeath();
        EnemyGroup.VanGuardDeath();

    }

    /// <summary>
    /// battleManagerを消去するときの処理
    /// </summary>
    private void OnBattleEnd()
    {
        //全てのキャラクターのスキルのTurn系プロパティをリセットする
        EnemyGroup.ResetCharactersSkillsProperty();
        AllyGroup.ResetCharactersSkillsProperty();

        //全てのキャラクターの追加硬直値をリセットする
        EnemyGroup.ResetCharactersRecovelyStepTmpToAdd();
        AllyGroup.ResetCharactersRecovelyStepTmpToAdd();

        //敵キャラは復活歩数の準備
        EnemyGroup.RecovelyStart(PlayersStates.Instance.NowProgress);
    }

    private void OnBattleStart()
    {
        //全キャラのrecovelyTurnを最大値にセットすることで全員行動可能
        EnemyGroup.PartyRecovelyTurnOK();
        AllyGroup.PartyRecovelyTurnOK();
    }

}