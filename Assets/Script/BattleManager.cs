

using System.Collections.Generic;
using UnityEngine;
using RandomExtensions;
using System.Linq;
using RandomExtensions.Linq;
using Unity.VisualScripting;
using R3;
using Cysharp.Threading.Tasks;
using UnityEditor.Experimental.GraphView;
using UnityEngine.Rendering.Universal;

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
    /// 攻撃対象者の一時処方に耐えうる保持リスト
    /// </summary>
    public class UnderActersEntryList
    {
        BattleManager Manager;
        /// <summary>
        /// 対象者キャラクターリスト
        /// </summary>
        List<BaseStates> charas;
        /// <summary>
        /// 割り当てられる"当てられる"スキルの分散値
        /// </summary>
        List<float> spreadPer;

        List<float> _cashSpread;
        List<float> CashSpread
        {
            get
            {
                if(_cashSpread == null)
                {
                    _cashSpread = Manager.Acter.NowUseSkill.PowerSpread.ToList();
                }
                return _cashSpread;
            }
        }

        public int Count => charas.Count;

        public UnderActersEntryList(BattleManager instance)
        {
            charas = new List<BaseStates>();
            spreadPer = new List<float>();
            Manager = instance;
        }

        public BaseStates GetAtCharacter(int index)
        {
            return charas[index];
        }
        public float GetAtSpreadPer(int index)
        {
            return spreadPer[index];
        }

        /// <summary>
        /// 既にある対象者リストをそのまま処理。
        /// </summary>
        public void SetList(List<BaseStates> charas) 
        {
            foreach (var chara in charas)
            {
                CharaAdd(chara);
            }
        }

        /// <summary>
        /// 追加し整理する関数
        /// </summary>
        public void CharaAdd(BaseStates chara)
        {
            var skill = Manager.Acter.NowUseSkill;

            float item = 1;//分散しなかったらデフォルトで100%

            if (skill.PowerSpread.Length > 0)//スキル分散値配列のサイズがゼロより大きかったら分散する
            {
                //爆発的
                if (skill.DistributionType == AttackDistributionType.Explosion)
                {
                    if (Manager.IsVanguard(chara))
                    {
                        item = CashSpread[0];//前のめりなら前の"0"の分散値
                    }
                    else
                    {
                        item = CashSpread[1];//後衛なら後ろの"1"の分散値
                    }
                }
                //放射型、ビーム型
                if(skill.DistributionType == AttackDistributionType.Beam)
                {
                    if (Manager.IsVanguard(chara))
                    {
                        item = CashSpread[0];//最初のを抽出
                        CashSpread.RemoveAt(0);
                    }
                    else
                    {
                        item = CashSpread[CashSpread.Count -1];//末尾から抽出
                        CashSpread.RemoveAt(CashSpread.Count - 1);
                    }
                }
                //投げる型
                if(skill.DistributionType == AttackDistributionType.Throw)
                {
                    if (Manager.IsVanguard(chara))
                    {
                        item = CashSpread[CashSpread.Count - 1];//末尾から抽出
                        CashSpread.RemoveAt(CashSpread.Count - 1);
                    }
                    else
                    {
                        item = CashSpread[0];//最初のを抽出
                        CashSpread.RemoveAt(0);
                    }
                }
                //ランダムの場合
                if(skill.DistributionType == AttackDistributionType.Random)
                {
                        item = CashSpread[CashSpread.Count - 1];//末尾から抽出
                        CashSpread.RemoveAt(CashSpread.Count - 1);
                }

            }
            spreadPer.Add(item);
            charas.Add(chara);//追加

        }



    }


    /// <summary>
    ///     プレイヤー側のバトルグループ　ここに味方のバトルグループオブジェクトをリンクする？
    /// </summary>
    public BattleGroup AllyGroup;

    /// <summary>
    ///     敵側のバトルグループ　ここに敵グループのバトルグループオブジェクトをリンクする？
    /// </summary>
    public BattleGroup EnemyGroup;

    private BattleStartSituation firstSituation;


    string UniqueTopMessage;//通常メッセージの冠詞？
    public BaseStates Acter;//今回の俳優
    /// <summary>
    /// 行動を受ける人 
    /// </summary>
    public UnderActersEntryList unders;
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
        unders = new UnderActersEntryList(this);

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
    public List<BaseStates> RemoveDeathCharacters(List<BaseStates> Charas)
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
                //味方が行動するならば
                if (Walking.disposableCreateTarget != null) Walking.disposableCreateTarget.Dispose();//既に入ってたらnullする。
                Walking.disposableCreateTarget = Walking.USERUI_state.Subscribe(
                    state =>
                    {
                        if (state == TabState.SelectTarget) SelectTargetButtons.Instance.OnCreated(this);
                        //対象者画面に移動したときに生成コールが実行されるようにする

                        if (state == TabState.SelectRange) SelectRangeButtons.Instance.OnCreated(this);
                    });


                switch (Acter)//スキル選択ボタンを各キャラの物にしてから
                {
                    case StairStates:
                        Walking.SKILLUI_state.Value = SkillUICharaState.geino;
                        break;

                    case SateliteProcessStates:
                        Walking.SKILLUI_state.Value = SkillUICharaState.sites;
                        break;
                    case BassJackStates:
                        Walking.SKILLUI_state.Value = SkillUICharaState.normalia;
                        break;
                        
                }
                //スキル選択ボタンを返す
                return TabState.Skill;
            }
            else//スキル強制続行中なら、
            {
                Acter.NowUseSkill = Acter.FreezeUseSkill;//操作の代わりに使用スキルに強制続行スキルを入れとく

                //ここでもスキル選択出来る様にしとく？
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

        //もしランダム範囲ならこの段階で範囲意志にランダム範囲を入れる。
        if (skill.HasZoneTrait(SkillZoneTrait.RandomRange))
        {
            DetermineRangeRandomly();
        }

        //人数やスキルの攻撃傾向によって、被攻撃者の選別をする
        SelectTargetFromWill();

        //実行処理
        skill.SetDeltaTurn(BattleTurnCount);//スキルのdeltaTurnをセット
        CreateBattleMessage(Acter.AttackChara(unders));//攻撃の処理からメッセージが返る。
        unders = new UnderActersEntryList(this);//初期化

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
    /// <summary>
    /// スキルの性質が範囲ランダムだった場合、
    /// 術者の範囲意志として性質通りにランダムで決定させる方法
    /// </summary>
    private void DetermineRangeRandomly()
    {
        var skill = Acter.NowUseSkill;


        //全部　全範囲　単体ランダム　前のめり後衛
        if (skill.HasZoneTrait(SkillZoneTrait.RandomTargetALLSituation))
        {
            switch (RandomEx.Shared.NextInt(3))
            {
                case 0:
                    {
                        Acter.RangeWill |= SkillZoneTrait.AllTarget;//全範囲
                        break;
                    }
                case 1:
                    {
                        Acter.RangeWill |= SkillZoneTrait.RandomSelectMultiTarget;//前のめり後衛ランダム(複数単位)
                        break;
                    }
                case 2:
                    {
                        Acter.RangeWill |= SkillZoneTrait.RandomSingleTarget;//単体ランダム
                        break;
                    }
            }
        }
        if (skill.HasZoneTrait(SkillZoneTrait.RandomTargetALLorMulti))//全範囲か前のめり後衛
        {
            switch (RandomEx.Shared.NextInt(2))
            {
                case 0:
                    {
                        Acter.RangeWill |= SkillZoneTrait.AllTarget;//全範囲
                        break;
                    }
                case 1:
                    {
                        Acter.RangeWill |= SkillZoneTrait.RandomSelectMultiTarget;//前のめり後衛ランダム(複数単位)
                        break;
                    }
            }
        }
        if (skill.HasZoneTrait(SkillZoneTrait.RandomTargetALLorSingle))//全範囲か単体ランダム
        {
            switch (RandomEx.Shared.NextInt(2))
            {
                case 0:
                    {
                        Acter.RangeWill |= SkillZoneTrait.AllTarget;//全範囲
                        break;
                    }
                case 1:
                    {
                        Acter.RangeWill |= SkillZoneTrait.RandomSingleTarget;//単体ランダム
                        break;
                    }
            }
        }
        if (skill.HasZoneTrait(SkillZoneTrait.RandomTargetMultiOrSingle))//前のめり後衛か単体ランダム
        {
            switch (RandomEx.Shared.NextInt(2))
            {
                case 0:
                    {
                        Acter.RangeWill |= SkillZoneTrait.RandomSelectMultiTarget;//前のめり後衛ランダム(複数単位)
                        break;
                    }
                case 1:
                    {
                        Acter.RangeWill |= SkillZoneTrait.RandomSingleTarget;//単体ランダム
                        break;
                    }
            }
        }
    }
    /// <summary>
    /// そのキャラが、敵味方問わずグループにおける前のめり状態かどうかを判別します。
    /// </summary>
    public bool IsVanguard(BaseStates chara)
    {
        if(chara == AllyGroup.InstantVanguard)return true;
        if(chara == EnemyGroup.InstantVanguard)return true;
        return false;
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

            if (Acter.HasRangeWill(SkillZoneTrait.CanSelectAlly))//自陣も対象に選べるなら
            {
                OurGroup = new BattleGroup(AllyGroup.Ours, AllyGroup.OurImpression, AllyGroup.which);//自陣
            }
        }
        else
        {//敵なら味方グループから選別する ディープコピー。
            SelectGroup = new BattleGroup(AllyGroup.Ours, AllyGroup.OurImpression, AllyGroup.which);
            if (Acter.HasRangeWill(SkillZoneTrait.CanSelectAlly))//自陣も対象に選べるなら
            {
                OurGroup = new BattleGroup(EnemyGroup.Ours, EnemyGroup.OurImpression, EnemyGroup.which);//自陣
            }

        }

        //死者は省く
        if (!Acter.HasRangeWill(SkillZoneTrait.CanSelectDeath))//死を選べないのなら　死を省く
        {
            SelectGroup.SetCharactersList(RemoveDeathCharacters(SelectGroup.Ours));
            if (OurGroup != null)
            {
                OurGroup.SetCharactersList(RemoveDeathCharacters(OurGroup.Ours));//自陣もあったら省く
            }
        }

        //行動者決定☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆

        if (Acter.Target == DirectedWill.One)//単体指定をしているのなら
        {
            //対象者選択でUnderActerに入ってるから何もしない(今の所
        }
        else
        {
            //選ばれる対立関係のグループに一人しかいない場合
            if (SelectGroup.Ours.Count < 2)
            {
                Debug.Log("敵に一人しかいません");
                UA.Add(SelectGroup.Ours[0]);//普通にグループの一人だけを狙う
            }
            else//二人以上いたら前のめりかそうでないかでの分岐処理
            {
                if (Acter.HasRangeWill(SkillZoneTrait.CanSelectSingleTarget))//前のめりか後衛(内ランダム単体)で選択する
                {
                    if (SelectGroup.InstantVanguard == null)//対象者グループに前のめりがいない場合。
                    {
                        //一人か二人に当たる
                        var counter = 0;
                        SelectGroup.Ours.Shuffle();//リスト内でシャッフル
                        foreach (var one in SelectGroup.Ours)
                        {
                            UA.Add(one);
                            counter++;
                            if (RandomEx.Shared.NextInt(100) < 77) break;//２３％で二人目にも当たる。
                            if (counter >= 2) break;//二人目を入れたらbreak　二人目まで行かなくてもforEachで勝手に終わる
                        }
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
                else if (Acter.HasRangeWill(SkillZoneTrait.RandomSingleTarget))//完全にランダムの単体対象
                {
                    BaseStates[] selects = SelectGroup.Ours.ToArray();
                    if (OurGroup != null)//自陣グループも選択可能なら
                        selects.AddRange(OurGroup.Ours);

                    UA.Add(RandomEx.Shared.GetItem(selects));//s選別リストからランダムで選択
                }
                else if (Acter.HasRangeWill(SkillZoneTrait.ControlByThisSituation))//状況のみに縛られる。(前のめりにしか当たらないなら
                {
                    if (SelectGroup.InstantVanguard == null)//対象者グループに前のめりがいない場合。事故が起きる
                    {
                        //前のめりしか選べなくても、もし前のめりがいなかったら、その**平坦なグループ**にスキル性質による攻撃が当たる。


                        //前のめりいないことによる事故☆☆☆☆☆☆☆☆☆☆

                        //シングルにあたるなら
                        if (Acter.HasRangeWill(SkillZoneTrait.RandomSingleTarget))
                        {
                            UA.Add(RandomEx.Shared.GetItem(SelectGroup.Ours.ToArray()));//選別リストからランダムで選択
                        }
                        //前のめりがいないんだから、　前のめりか後衛単位での　集団事故は起こらないため　RandomSelectMultiTargetによる場合分けはない。

                        //全範囲事故なら
                        if (Acter.HasRangeWill(SkillZoneTrait.AllTarget))
                        {
                            BaseStates[] selects = SelectGroup.Ours.ToArray();
                            if (OurGroup != null)//自陣グループも選択可能なら
                                selects.AddRange(OurGroup.Ours);

                            UA.AddRange(selects);//対象範囲を全て加える
                        }
                        //ランダム範囲事故なら
                        if (Acter.HasRangeWill(SkillZoneTrait.RandomMultiTarget))
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
                else if (Acter.HasRangeWill(SkillZoneTrait.CanSelectMultiTarget))//前衛、後衛単位の範囲でランダムに狙うなら
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
                else if (Acter.HasRangeWill(SkillZoneTrait.RandomSelectMultiTarget))//前衛または後衛単位をランダムに狙う
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
                else if (Acter.HasRangeWill(SkillZoneTrait.RandomMultiTarget))//ランダムな範囲攻撃の場合
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
                else if (Acter.HasRangeWill(SkillZoneTrait.AllTarget))//全範囲
                {
                    BaseStates[] selects = SelectGroup.Ours.ToArray();
                    if (OurGroup != null)//自陣グループも選択可能なら
                        selects.AddRange(OurGroup.Ours);

                    UA.AddRange(selects);//対象範囲を全て加える

                }
            }

            //underActerがゼロ個でないと、つまりここの意志選択関数以外で直接指定してるなら、入れない。
            if (unders.Count < 1) 
            {
                UA.Shuffle();
                unders.SetList(UA);
            }
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

        //全てのキャラクターの特別な補正をリセットする
        EnemyGroup.ResetCharactersUseThinges();
        AllyGroup.ResetCharactersUseThinges();

        //敵キャラは復活歩数の準備
        EnemyGroup.RecovelyStart(PlayersStates.Instance.NowProgress);
    }

    private void OnBattleStart()
    {
        //全キャラのrecovelyTurnを最大値にセットすることで全員行動可能
        EnemyGroup.PartyRecovelyTurnOK();
        AllyGroup.PartyRecovelyTurnOK();

        //全キャラのキンダーガーデン用の慣れ補正の優先順位のグルーピングの数列を初期化
        foreach (var one in EnemyGroup.Ours)
        {
            one.DecisionKinderAdaptToSkillGrouping();
        }
        foreach (var one in AllyGroup.Ours)
        {
            one.DecisionKinderAdaptToSkillGrouping();
        }



        //全キャラにbmセット
        foreach (var one in EnemyGroup.Ours)
        {
            one.Managed(this);
        }
        foreach(var one in AllyGroup.Ours)
        {
            one.Managed(this);
        }
    }

}