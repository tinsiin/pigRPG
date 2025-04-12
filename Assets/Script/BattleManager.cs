
using System;
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
using Mono.Cecil.Cil;
using TMPro;
using UnityEditor;
using static CommonCalc;
using UnityEditor.UIElements;

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
/// 行動先約リストでのステータス補正予約時の識別用
/// </summary>
public enum whatModify
{
    eye, atk, def, agi
}
/// <summary>
/// 先約リストで扱うステータス補正クラス
/// </summary>
public class ReservationStatesModify
{
    public whatModify eada;//どのステータスの補正なのか。

    public float modify;

    public string memo;
}

/// <summary>
/// 行動リスト
/// </summary>
public class ACTList
{

    List<BaseStates> CharactorACTList;
    List<string> TopMessage;
    List<WhichGroup> FactionList;//陣営
    List<List<ReservationStatesModify>> reservationStatesModifies;//補正リスト
    List<bool> IsFreezeList;//スキルをフリーズ、つまり前のスキルを持続させるかどうか。
    List<BaseStates> SingleTargetList;//単体で狙うのを確定するリスト
    List<float> ExCounterDEFATKList;//割り込みカウンターの防御無視率を保持するリスト


    public int Count
    {
        get => CharactorACTList.Count;
    }

    public void Add(BaseStates chara, WhichGroup charasFac, string mes = "", List<ReservationStatesModify> modifys = null, bool isfreeze = false,BaseStates SingleTarget = null,float ExCounterDEFATK = -1)
    {
        CharactorACTList.Add(chara);
        FactionList.Add(charasFac);
        TopMessage.Add(mes);
        reservationStatesModifies.Add(modifys);
        IsFreezeList.Add(isfreeze);
        SingleTargetList.Add(SingleTarget);
        ExCounterDEFATKList.Add(ExCounterDEFATK);

    }

    public ACTList()
    {
        CharactorACTList = new List<BaseStates>();
        TopMessage = new List<string>();
        FactionList = new List<WhichGroup>();
        reservationStatesModifies = new List<List<ReservationStatesModify>>();
        IsFreezeList = new();
        SingleTargetList = new();
        ExCounterDEFATKList = new();
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
        reservationStatesModifies.RemoveAt(index);
        IsFreezeList.RemoveAt(index);
        SingleTargetList.RemoveAt(index);
        ExCounterDEFATKList.RemoveAt(index);
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
    public List<ReservationStatesModify> GetAtModifyList(int index)
    {
        return reservationStatesModifies[index];
    }
    public bool GetAtIsFreezeBool(int index)
    {
        return IsFreezeList[index];
    }
    public BaseStates GetAtSingleTarget(int index)
    {
        return SingleTargetList[index];
    }
    public float GetAtExCounterDEFATK(int index)
    {
        return ExCounterDEFATKList[index];
    }


}
    /// <summary>
    /// 攻撃対象者の一時処方に耐えうる保持リスト
    /// </summary>
    public class UnderActersEntryList
    {
        BattleManager Manager;
        /// <summary>
        /// 対象者キャラクターリスト
        /// </summary>
        public List<BaseStates> charas;
        /// <summary>
        /// 割り当てられる"当てられる"スキルの分散値
        /// </summary>
        List<float> spreadPer;

        List<float> _cashSpread;
        List<float> CashSpread
        {
            get
            {
                if (_cashSpread == null)
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
        /// スキル対象者を追加し整理する関数
        /// </summary>
        public void CharaAdd(BaseStates chara)
        {
            var skill = Manager.Acter.NowUseSkill;

            float item = 1;//分散しなかったらデフォルトで100%

            if (skill.PowerSpread.Length > 0 && skill.PowerSpread != null)//スキル分散値配列のサイズがゼロより大きかったら分散する
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
                if (skill.DistributionType == AttackDistributionType.Beam)
                {
                    if (Manager.IsVanguard(chara))
                    {
                        item = CashSpread[0];//最初のを抽出
                        CashSpread.RemoveAt(0);
                    }
                    else
                    {
                        item = CashSpread[CashSpread.Count - 1];//末尾から抽出
                        CashSpread.RemoveAt(CashSpread.Count - 1);
                    }
                }
                //投げる型
                if (skill.DistributionType == AttackDistributionType.Throw)
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
                if (skill.DistributionType == AttackDistributionType.Random)
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
///     バトルを、管理するクラス
/// </summary>
public class BattleManager
{
    /// <summary>
    ///     プレイヤー側のバトルグループ　ここに味方のバトルグループオブジェクトをリンクする？
    /// </summary>
    public BattleGroup AllyGroup;

    /// <summary>
    ///     敵側のバトルグループ　ここに敵グループのバトルグループオブジェクトをリンクする？
    /// </summary>
    public BattleGroup EnemyGroup;
    /// <summary>
    ///全キャラクターのリスト
    /// </summary>
    public List<BaseStates> AllCharacters => AllyGroup.Ours.Concat(EnemyGroup.Ours).ToList();

    /// <summary>
    /// factionのグループを返す
    /// </summary>
    BattleGroup FactionToGroup(WhichGroup faction)
    {
        switch(faction)
        {
            case WhichGroup.alliy:
                return AllyGroup;
            case WhichGroup.Enemyiy:
                return EnemyGroup;
        }
        return null;
    }
    /// <summary>
    /// キャラクターのグループを取得
    /// </summary>
    public BattleGroup MyGroup(BaseStates chara) => FactionToGroup(GetCharacterFaction(chara));
    /// <summary>
    /// 同じグループかどうか
    /// </summary>
    public bool IsFriend(BaseStates chara1, BaseStates chara2)
    {
        bool chara1InAlly = AllyGroup.Ours.Contains(chara1);
        bool chara2InAlly = AllyGroup.Ours.Contains(chara2);
        
        // 両方が味方、または両方が敵ならtrue
        return (chara1InAlly && chara2InAlly) || (!chara1InAlly && !chara2InAlly);
    }

    /// <summary>
    /// 誰もアクションを取らなかった場合、ネクストターンをスキップする
    /// </summary>
    bool ACTSkipACTBecauseNobodyACT = false;
    /// <summary>
    /// 渡されたキャラクタのbm内での陣営を表す。
    /// </summary>
    public WhichGroup GetCharacterFaction(BaseStates chara)
    {
        foreach(var one in AllyGroup.Ours)
        {
            if(one == chara)return WhichGroup.alliy;
        }
        foreach(var one in EnemyGroup.Ours)
        {
            if(one == chara)return WhichGroup.Enemyiy;
        }

        return 0;
    }

    /// <summary>
    /// そのキャラクターと同じパーティーの生存者のリストを取得(自分自身を除く)
    /// </summary>
    List<BaseStates> GetOtherAlliesAlive(BaseStates chara) => 
    RemoveDeathCharacters(FactionToGroup(GetCharacterFaction(chara)).Ours).Where(x => x != chara).ToList();

    private BattleStartSituation firstSituation;


    string UniqueTopMessage;//通常メッセージの冠詞？
    public BaseStates Acter;//今回の俳優
    /// <summary>
    /// 行動を受ける人 
    /// </summary>
    public UnderActersEntryList unders;
    WhichGroup ActerFaction;//陣営
    bool Wipeout = false;//全滅したかどうか
    bool EnemyGroupEmpty = false;//敵グループが空っぽ
    bool AlliesRunOut = false;//味方全員逃走
    NormalEnemy VoluntaryRunOutEnemy = null;//敵一人の逃走
    /// <summary>
    /// 連鎖逃走する敵リスト
    /// </summary>
    List<NormalEnemy> DominoRunOutEnemies = new List<NormalEnemy>();
    public bool DoNothing = false;//何もしない
    public bool VoidTurn = false;//そのターンは無かったことに

    /// <summary>
    /// 行動リスト　ここではrecovelyTurnの制約などは存在しません
    /// </summary>
    public ACTList Acts;//行動先約リスト？

    public int BattleTurnCount;//バトルの経過ターン


    /// <summary>
    ///コンストラクタ
    /// </summary>
    public BattleManager(BattleGroup allyGroup, BattleGroup enemyGroup, BattleStartSituation first)
    {
        AllyGroup = allyGroup;
        EnemyGroup = enemyGroup;
        firstSituation = first;
        Acts = new ACTList();
        unders = new UnderActersEntryList(this);

        

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

        //全員の人間状況を初期化
        //AllCharactersを使うよりもallyとenemygroupを使うほうが引数として渡すためのロジックが分かりやすい気がする
        foreach (var chara in AllyGroup.Ours)
        {
            chara.ApplyConditionOnBattleStart(EnemyGroup.OurAvarageTenDayPower());
        }
        foreach (var chara in EnemyGroup.Ours)
        {
            chara.ApplyConditionOnBattleStart(AllyGroup.OurAvarageTenDayPower());
        }
        //初期化コールバックで変化判定用にConditionTransitionが実行され記録されるので、初期化コールバックの前に呼ばなければなりません。

        OnBattleStart();//初期化コールバック

    }
    

    /// <summary>
    /// BaseStatesを継承したキャラクターのListからrecovelyTurnのカウントアップして行動状態に回復出来る奴だけを選別する
    /// </summary>
    private List<BaseStates> RetainActionableCharacters(List<BaseStates> Charas)
    {
        var ActionableCharacters =  Charas.Where(chara => chara.RecovelyBattleField(BattleTurnCount)).ToList();
        return ActionableCharacters;
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
            var acter = RandomEx.Shared.GetItem(group.ToArray<BaseStates>());
            if (i == group.Count - 1 && CounterCharas.Count > 0 && RandomEx.Shared.NextInt(100) < 40)
            {//もし最後の先手ターンで後手グループにキンダーガーデンかゴッドティアがいて、　40%の確率が当たったら
                //反撃グループにいるそのどちらかの印象を持ったキャラクターのターンが入る。
                Acts.Add(acter, _counterGroup.which, "ハンターナイト▼");
            }
            else
            {
                //グループの中から人数分アクションをいれる
                Acts.Add(acter, _group.which, $"先手{i}☆");
            }

            if(i == 0)acter.BattleFirstSurpriseAttacker = true;//もしバトル最初の先手攻撃者ならフラグを立てる

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
            ActerFaction = WhichGroup.alliy;
        }
        else
        {
            Charas = EnemyGroup.Ours;
            ActerFaction = WhichGroup.Enemyiy;
        }

        Charas = RemoveDeathCharacters(Charas);//死者を取り除く
        Charas = RetainActionableCharacters(Charas);//再行動をとれる人間のみに絞る

        if (Charas.Count == 0)
        {
            return null;
        }
        
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
    /// ランダムターンかまたは先約リストからActerを取得する
    /// </summary>
    void CharacterAddFromListOrRandom()
    {
        //もし先約リストにキャラクターが居たら、そのリストを先ずは消化する
        if (Acts.Count > 0)
        {//先約リストにおいて、recovelyTurnは意味をなさないですよ
            UniqueTopMessage = Acts.GetAtTopMessage(0);//リストからメッセージとキャラクターをゲット。
            Acter = Acts.GetAtCharacter(0);
            ActerFaction = Acts.GetAtFaction(0);

            //acterの特別補正の補正予約があるなら入れる
            List<ReservationStatesModify> modList;
            if ((modList = Acts.GetAtModifyList(0)) != null)//リストが存在するなら
            {
                foreach (var mod in modList)//補正リスト内のアイテムで回す
                {
                    switch (mod.eada)
                    {
                        case whatModify.atk:
                            Acter.SetATKPercentageModifier(mod.modify, mod.memo);
                            break;
                        case whatModify.def:
                            Acter.SetDEFPercentageModifier(mod.modify, mod.memo);
                            break;
                        case whatModify.agi:
                            Acter.SetAGIPercentageModifier(mod.modify, mod.memo);
                            break;
                        case whatModify.eye:
                            Acter.SetEYEPercentageModifier(mod.modify, mod.memo);
                            break;
                    }
                }
            }
            //単体固定の狙うべきキャラがいたら
            var singleTarget = Acts.GetAtSingleTarget(0);
            if (singleTarget != null)
            {
                //もし死んでたら、このターンは無かったことになる。
                if (singleTarget.Death())
                {
                    VoidTurn =true;
                }/*else
                {
                    Acter.Target = DirectedWill.One;
                    unders.CharaAdd(singleTarget);
                }*///
            }

            //スキルがフリーズするならする
            if (Acts.GetAtIsFreezeBool(0))
            {
                Acter.FreezeSkill();
            }

            //カウンター用の防御無視率特別補正はシステム上そのまま代入してOK
            //特別補正だからでかくてもターン終わりで消えるし、先約リストで指定されなけば-1が代入つまり絶対本来の防御無視率を超えて指定されないし。
            Acter.SetExCounterDEFATK(Acts.GetAtExCounterDEFATK(0));
            

            Debug.Log("俳優は先約リストから選ばれました");
        }
        else
        {
            //居なかったらランダムに選ぶ
            Acter = RandomTurn();
            Debug.Log("俳優はランダムに選ばれました");
        }

    }
    /// <summary>
    /// 行動準備 次のボタンを決める
    /// </summary>
    public TabState ACTPop()
    {
        ResetManagerTemp();//一時保存要素をリセット
        Acts.RemoveDeathCharacters();//先約リストから死者を取り除く

        //パーティーの死亡判定
        if (AllyGroup.PartyDeathOnBattle())
        {
            Wipeout = true;
            ActerFaction = WhichGroup.alliy;
            return TabState.NextWait;//押して処理
        }
        else if (EnemyGroup.PartyDeathOnBattle())
        {
            Wipeout = true;
            ActerFaction = WhichGroup.Enemyiy;
            return TabState.NextWait;//押して処理
        }
        //もし敵のグループが逃走なんかで空っぽになってたら、
        if (EnemyGroup.Ours.Count == 0)
        {
            EnemyGroupEmpty = true;
            return TabState.NextWait;//押して処理
        }

        //逃走
        if (AlliesRunOut)
        {
            return TabState.NextWait;//押して処理
        }
        //敵の連鎖逃走リストがあるなら
        if(DominoRunOutEnemies.Count > 0)
        {
            return TabState.NextWait;//押して処理
        }

        CharacterAddFromListOrRandom();//Acterが選ばれる

        if(VoidTurn)//これは処理の仕組み上発生する飛ばせないエラーによるターン処理だが、そもそも発生しないと思う。
        {
            VoidTurn = false;//ターン消しとび　エフェクトなし
            NextTurn(false);
            return ACTPop();
        }

        if(Acter == null)//俳優がnullだとランダム選別の際に「再行動できるキャラがいない」とされて処理がキャンセルされたので
        {
            ACTSkipACTBecauseNobodyACT = true;
            return TabState.NextWait;//押して処理
        }

        //スキルと範囲の思考--------------------------------------------------------------------------------------------------------スキルと範囲の思考-----------------------------------------------------------

        //俳優が味方なら
        if (ActerFaction == WhichGroup.alliy)
        {//味方が行動するならば

            if (!Acter.IsFreeze)//強制続行中のスキルがなければ
            {
                //スキル選択ボタンを各キャラの物にしてから
                SwitchAllySkillUiState();

                //スキル選択ボタンを返す
                return TabState.Skill;
            }
            else//スキル強制続行中なら、
            {
                //俳優が自分のFreezeConsecutiveを削除する予約をしているのなら、
                if (Acter.IsDeleteMyFreezeConsecutive)
                {
                    Acter.DeleteConsecutiveATK();//連続実行FreezeConsecutiveを削除
                    DoNothing = true;//何もしない
                    return TabState.NextWait;// nextwait = CharacterACTBranching
                }
                
                var skill = Acter.FreezeUseSkill;
                Acter.RangeWill= Acter.FreezeRangeWill;//強制続行スキルの範囲意志を入れとく
                Acter.NowUseSkill = skill;//操作の代わりに使用スキルに強制続行スキルを入れとく

                //連続攻撃中に操作可能なスキルなら、
                if (skill.NowConsecutiveATKFromTheSecondTimeOnward()
                && skill.HasConsecutiveType(SkillConsecutiveType.CanOprate))
                {
                    //範囲画面と対象者選択画面どちらに向かうかの判定
                    return AllyClass.DetermineNextUIState(skill);
                }
            }
        }

        //敵ならここで思考して決める
        if (ActerFaction == WhichGroup.Enemyiy)
        {
            var ene = Acter as NormalEnemy;
            ene.SkillAI();//ここで決めないとスキル可変オプションが下記の対象者選択で反映されないから
        }
        //スキルと範囲の思考--------------------------------------------------------------------------------------------------------スキルと範囲の思考-----------------------------------------------------------

        


        return TabState.NextWait;// nextwait = CharacterACTBranching


    }
    /// <summary>
    /// スキルボタンのUIを各キャラクターの物にする。
    /// </summary>
    void SwitchAllySkillUiState()
    {
        var ps = PlayersStates.Instance;

        //もしActs,先約リストで単体指定SingleTargetがあるならば、
        var singleTarget = Acts.GetAtSingleTarget(0);
        var OnlyRemainButtonByType = Enum.GetValues(typeof(SkillType))
                                    .Cast<SkillType>()
                                    .Aggregate((current, next) => current | next);
        var OnlyRemainButtonByZoneTrait =(SkillZoneTrait)((1 << 16) - 1);//全てのZoneTraitを代入しておく
        if (singleTarget != null)
        {
            OnlyRemainButtonByZoneTrait = 0;//まず空にする
            OnlyRemainButtonByType =0;
            
            OnlyRemainButtonByZoneTrait |= SkillZoneTrait.CanPerfectSelectSingleTarget//単体系の範囲性質を全て入れる
                                       | SkillZoneTrait.CanSelectSingleTarget
                                       | SkillZoneTrait.RandomSingleTarget
                                       | SkillZoneTrait.ControlByThisSituation;

            OnlyRemainButtonByType |= SkillType.Attack;//攻撃性質を持つもの限定
        }

        switch (Acter)
        {
             case StairStates:
                //ここでスキルを指定した範囲性質を持つもののみinteractable=trueになるようにする。
                ps.OnlyInteractHasZoneTraitSkills_geino(OnlyRemainButtonByZoneTrait,OnlyRemainButtonByType);//ボタンのオンオフをするコールバック
                ps.OnlyInteractHasHasBladeWeaponShowBladeSkill_geino();
                Walking.SKILLUI_state.Value = SkillUICharaState.geino;
                break;

            case SateliteProcessStates:
                ps.OnlyInteractHasZoneTraitSkills_sites(OnlyRemainButtonByZoneTrait,OnlyRemainButtonByType);
                ps.OnlyInteractHasHasBladeWeaponShowBladeSkill_sites();
                Walking.SKILLUI_state.Value = SkillUICharaState.sites;
                break;
            case BassJackStates:
                ps.OnlyInteractHasZoneTraitSkills_normalia(OnlyRemainButtonByZoneTrait,OnlyRemainButtonByType);
                ps.OnlyInteractHasHasBladeWeaponShowBladeSkill_noramlia();
                Walking.SKILLUI_state.Value = SkillUICharaState.normalia;
                break;

        }

    }

    /// <summary>
    /// 俳優の行動の分岐
    /// </summary>
    /// <returns></returns>
    public TabState CharacterActBranching()
    {
        var skill = Acter.NowUseSkill;
        var IsEscape = Acter.SelectedEscape;//逃げる意思

        if (Wipeout || AlliesRunOut || EnemyGroupEmpty) //全滅か主人公達逃走かでダイアログ終了アクトへ
        {
            //Bmは終了へ向かうので、RunOutもWipeOutもfalseにする必要はない。
            return DialogEndACT();
        }
        //敵の連鎖逃走リストがあるなら
        if(DominoRunOutEnemies.Count > 0)
        {
            return DominoEscapeACT();//連鎖逃走の処理へ
        }
        //誰も動けないのでスキップし、時間が進む
        if(ACTSkipACTBecauseNobodyACT)
        {
            ACTSkipACTBecauseNobodyACT = false;
            return ACTPop();
        }

        if(DoNothing)
        {
            //小さなアイコン辺りに無音の灰色円縮小エフェクトを入れる     何もしないエフェクト
            DoNothing = false;
            NextTurn(true);
            return ACTPop();//何もせず行動準備へ
        }
        
        if(IsEscape)
        {
            return EscapeACT();
        }
        if(ACTSkipACTBecauseNobodyACT)
        {
            ACTSkipACTBecauseNobodyACT = false;
            //誰も動けない膠着状態だからスキップされたことを表すエフェクトを入れるといいと思う。

            //誰も動けないのでスキップし、時間が進む
            NextTurn(true);
            return ACTPop();
        }

        int count;//メッセージテキスト用のカウント数字
        if ((count = skill.TrigerCount()) >= 0)//発動カウントが0以上ならまだカウント中
        {
            return TriggerACT(count);//発動カウント処理
        }
        else//発動カウントが-1以下　つまりカウントしてないまたは終わったなら
        {
            skill.ReturnTrigger();//トリガーのカウントを成功したときの戻らせ方させて
            return SkillACT();
        }

    }
    float GetRunOutRateByCharacterImpression(SpiritualProperty property)
    {
        switch(property)
        {
            case SpiritualProperty.liminalwhitetile:
                return 55;
            case SpiritualProperty.kindergarden:
                return 80;
            case SpiritualProperty.sacrifaith:
                return 5;
            case SpiritualProperty.cquiest:
                return 25;
            case SpiritualProperty.devil:
                return 40;
            case SpiritualProperty.doremis:
                return 40;
            case SpiritualProperty.pillar:
                return 10;
            case SpiritualProperty.godtier:
                return 50;
            case SpiritualProperty.baledrival:
                return 60;
            case SpiritualProperty.pysco:
                return 100;
            case SpiritualProperty.none:
                return 0;
        }
        return 0;
    }
    /// <summary>
    /// 連鎖逃走する敵を取得
    /// 連鎖逃走リストに追加しとく
    /// </summary>
    /// <param name="voluntaryRunOutEnemy">逃げた最初の敵</param>
    public void GetRunOutEnemies(NormalEnemy voluntaryRunOutEnemy)
    {
        //敵グループに残った敵で回す
        foreach(var remainingEnemy in EnemyGroup.Ours)
        {
            //逃げた敵に対する相性値が高ければ連鎖逃走
            if(EnemyGroup.CharaCompatibility[(remainingEnemy, voluntaryRunOutEnemy)] >= 77)
            {
                DominoRunOutEnemies.Add(remainingEnemy as NormalEnemy);
                continue;
            }       
            //キャラクター属性による逃走判定
            if(rollper(GetRunOutRateByCharacterImpression(remainingEnemy.MyImpression)))
            {
                DominoRunOutEnemies.Add(remainingEnemy as NormalEnemy);
            }
        }
    }
    /// <summary>
    /// 逃げるACT
    /// </summary>
    /// <returns></returns>
    public TabState EscapeACT()
    {
        //味方の場合はエリアの逃走率判定
        if(ActerFaction == WhichGroup.alliy)
        {
            var Rate = Walking.NowStageCut.EscapeRate;
            //人数により逃走率の分岐
            switch(AllyGroup.Ours.Count)
            {
                case 1:
                    Rate *= 0.5f;//半減
                    break;
                case 2:
                    Rate *= 0.96f;//96%
                    break;
                case 3:
                    //そのまま
                    break;
                default:
                    //そのまま
                    Debug.LogWarning("味方のグループが三人以上_EscapeACTにて検出された");
                    break;
            }

            if(rollper(Rate))
            {
                //逃走成功
                AlliesRunOut = true;//次のACTpopで逃走する
                Debug.Log("逃げた");
            }else{
                //逃げ失敗
                Debug.Log("逃げ失敗");
            }
        }else//敵なら一律50%
        {
            if(rollper(50))
            {
                //逃走成功
                VoluntaryRunOutEnemy = Acter as NormalEnemy;//
                EnemyGroup.EscapeAndRemove(VoluntaryRunOutEnemy);//敵キャラならその場で消す
                Debug.Log("敵は逃げた");

                //連鎖逃走の判断とリストに入れる。
                GetRunOutEnemies(VoluntaryRunOutEnemy);
            }else{
                //逃げ失敗
                Debug.Log("敵は逃げ失敗");
            }
        }
        Acter.SelectedEscape = false;//選択を解除
        NextTurn(true);
        return ACTPop();
    }
    /// <summary>
    /// 連鎖逃走
    /// </summary>
    public TabState DominoEscapeACT()
    {
        //連鎖逃走
        foreach(var enemy in DominoRunOutEnemies)
        {
            EnemyGroup.EscapeAndRemove(enemy);
            Debug.Log("敵は連鎖逃走");
        }

        //連鎖逃走リストクリア
        DominoRunOutEnemies.Clear();
        NextTurn(true);
        return ACTPop();
    }
    /// <summary>
    /// メッセージと共に戦闘を終わらせる
    /// </summary>
    public TabState DialogEndACT()
    {
        if (Wipeout)
        {
            if (ActerFaction == WhichGroup.alliy)
            {
                MessageDropper.Instance.CreateMessage("死んだ");
                PlayersStates.Instance.PlayersOnLost();

                //敵たちの勝利時コールバック。
                EnemyGroup.EnemyiesOnWin();
            }
            else
            {
                MessageDropper.Instance.CreateMessage("勝ち抜いた");
                PlayersStates.Instance.PlayersOnWin();
            }
        }
        if (AlliesRunOut) 
        {
            MessageDropper.Instance.CreateMessage("我々は逃げた");
            PlayersStates.Instance.PlayersOnRunOut();
            EnemyGroup.EnemiesOnAllyRunOut();//敵の主人公達が逃げ出した時のコールバック
        }
        if (EnemyGroupEmpty)
        {
            MessageDropper.Instance.CreateMessage("敵はいなくなった");
           //敵が逃げたときのはそれぞれコールバックしたのでここで敵のコールバックは行わない。

           //一応主人公達は勝った扱い
           PlayersStates.Instance.PlayersOnWin();
        }

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
    /// 発動カウント時にTriggerACTでカウントされるスキル以外のスキルの発動カウントが巻き戻る
    /// </summary>
    void OtherSkillsTriggerRollBack()
    {
        foreach (var skill in Acter.SkillList)
        {
            if (skill.IsTriggering)//トリガーされてる最中なら、
            {
                skill.RollBackTrigger();//巻き戻す
            }
        }
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
        //他のスキルの発動カウントを巻き戻す
        OtherSkillsTriggerRollBack();
        //発動カウントのメッセージ
        CreateBattleMessage($"{skill.SkillName}の発動カウント！残り{count}回。");
        //発動カウント時はスキルの複数回連続実行がありえないから、普通にターンが進む
        NextTurn(true);

        return ACTPop();
    }
    /// <summary>
    /// 慣れフラットロゼの泉水由来の発生確率
    /// </summary>
    /// <returns></returns>
    float GetCoolnessFlatRozeChance()
    {
        var coolPower = Acter.TenDayValues().GetValueOrZero(TenDayAbility.SpringWater);//泉水取得

        return Mathf.Floor(coolPower / 16.7f) * 0.01f;
    }
    /// <summary>
    /// 慣れフラットロゼの泉水由来の命中補正
    /// </summary>
    /// <returns></returns>
    float GetCoolnesFlatRozePower()
    {
        var coolPower = Acter.TenDayValues().GetValueOrZero(TenDayAbility.SpringWater);//泉水取得

        return coolPower * 0.005f;
    }
    /// <summary>
    /// 慣れフラットロゼ用の発生確率の計算　引数にスキル命中率
    /// x in [0..100]。xが50または60に近いほど、イージング曲線で 4.44%→27%
    /// ただし距離>12 なら上限4.44%(さらに bc 超えれば 0%)。
    /// alpha パラメータで曲線の急/緩を調整できる。
    /// </summary>
    /// <param name="x">スキルHitPerなど(0～100)</param>
    /// <param name="alpha">曲線形状(1=線形, >1=徐々に上昇, <1=最初に急上昇)</param>
    /// <returns>最終パーセント(0～27)</returns>
    float Ideal50or60Easing(float x, float alpha = 4.3f)
    {
        float CoolChance = GetCoolnessFlatRozeChance();//泉水による補正
        // 1) 0～100 にクランプ
        x = Mathf.Clamp(x, 0f, 100f);

        // 2) 50,60 の近い方との距離 d
        float dist50 = Mathf.Abs(x - 50f);
        float dist60 = Mathf.Abs(x - 60f);
        float d = Mathf.Min(dist50, dist60);

        // 3) パラメータ設定
        float ab = 12f;   // 距離が [0..12) => イージング(4.44→27)
        float bc = 25f;   // 距離が [12..25) => 4.44→0 (線形)
                          // その先(d>=25) => 0%

        // 4) 区間判定

        if (d >= bc)
        {
            // (C) d >= 25 => 0%
            return 0f + CoolChance;
        }
        else if (d >= ab)
        {
            // (B) d in [12..25)
            //   12 => 4.44
            //   25 => 0
            float t = (d - ab) / (bc - ab); // 0～1
            return Mathf.Lerp(4.44f, 0f, t)+CoolChance;
        }
        else
        {
            // (A) d < 12
            //   12 => 4.44
            //   0  => 27
            // イージング
            //
            // t = (12 - d)/12 => 0～1
            //   d=12 => t=0
            //   d=0 => t=1
            //
            // ease = t^alpha
            //   alpha=1 => 線形
            //   alpha>1 => "ゆっくり始まる" (ease-in)
            //   alpha<1 => "最初に急上昇" (ease-out)
            //

            float baseline = 4.44f;
            float peak = 27f;

            float t = (ab - d) / ab; // 0～1
            float easePortion = Mathf.Pow(t, alpha);

            float val = baseline + (peak - baseline) * easePortion;

            // 必要に応じて clamp
            // val = Mathf.Clamp(val, 0f, 27f);

            return val+CoolChance;
        }
    }
    /// <summary>
    /// スキル実行時に踏み込むのなら、俳優がグループ内の前のめり状態になる
    /// </summary>
    void BeVanguard()
    {
        if (Acter.NowUseSkill.IsAggressiveCommit)
        {
            FactionToGroup(ActerFaction).InstantVanguard = Acter;
        }
    }
    /// <summary>
    /// 相性値由来の被害者への仲間の救済意図での再行動短縮処理
    /// </summary>
    void TryHelpMinusRecovelyTurnByCompatibility()
    {
        for (var i = 0; i < unders.Count; i++)//被害者全員分ループ
        {
            var chara = unders.GetAtCharacter(i);
            var HelpGroup = MyGroup(chara);//所属するBattleGroupを取得
            var LiveAllyGroupList = GetOtherAlliesAlive(chara);//被害者の生きている味方
            if(LiveAllyGroupList.Count < 1)continue;//味方がいなければスキップ

            //60%以上の相性値が被害者に対してある味方のみに絞る　味方→被害者　への相性値
            LiveAllyGroupList = LiveAllyGroupList.Where(ally => 
            HelpGroup.CharaCompatibility.ContainsKey((ally, chara)) && HelpGroup.CharaCompatibility[(ally, chara)] >= 60).ToList();
            if(LiveAllyGroupList.Count < 1)continue;//60以上の相性値を持ってる味方がいなければスキップ
            var data = chara.RecentDamageData;//被害者のダメージ記録
            var DamageRate = data.Damage/chara.MAXHP;//被害者のダメージ率
            foreach(var ally in LiveAllyGroupList)//60%以上の相性値を持ってる味方全員分ループ
            {
                var Compatibility = HelpGroup.CharaCompatibility[(ally, chara)];//味方→被害者への相性値
                var occurrenceProbability = 0f;//発生確率
                var baseChance = 0f;//基本発生確率

                if (Compatibility > 130)
                {
                    baseChance = 0.47f;
                }
                else if (Compatibility < 60)
                {
                    baseChance = 0f;
                }
                else
                {
                    //60以上130以下なら計算

                    // 60 で 0、130 で 70 となる変換
                    float compOffset = Compatibility - 60f;  // 0～70 の範囲
                    // x = A*(compOffset) - C
                    // sigmoid(u)=1/(1+ e^-u)
                    // scale (0.34f) を掛けて 0..0.34 を出力
                    float x = 0.2f * compOffset - 4.0f;   
                    float sig = 1f/(1f+ Mathf.Exp(-x));
                    baseChance = 0.34f * sig;       // 0..0.34
                }
                    var HelpRate = DamageRate;
                    //非攻撃の敵対的行動を取っていた場合、計算用のダメージ割合に加算
                    if(data.IsBadPassiveHit || data.IsBadVitalLayerHit || data.IsGoodPassiveRemove || data.IsGoodVitalLayerRemove) 
                    {
                        HelpRate += RandomEx.Shared.NextFloat(0.07f,0.15f);//7%~15%加算
                    }
                    HelpRate = Mathf.Clamp01(HelpRate);//0~1にクランプ
                    
                    //複合方式　加算と乗算のいいとこどりでダメージ割合と掛け合わせる。
                    float k = 2f;
                    occurrenceProbability = baseChance * (1f + k * HelpRate);
                    occurrenceProbability = Mathf.Min(occurrenceProbability, 1f);//最大値1にクランプ

                //救済意図での再行動短縮を判定    
                if(rollper(occurrenceProbability * 100))
                {
                     //短縮ターンの計算
                    float expectedShorten= occurrenceProbability * 4f; // 最大短縮ターン=4
                    var baseShorten = Mathf.Floor(expectedShorten);//基本短縮ターン
                    var ratio = expectedShorten - baseShorten;//小数点以下の端数
                    var upChance = ratio / 3;//上昇確率
                    float finalShorten = baseShorten;
                    if(RandomEx.Shared.NextFloat(1) < upChance)
                    {
                        finalShorten = baseShorten + 1;
                    }


                    //finalshortenを利用してこのループの仲間キャラのrecovelyturnを短縮する処理。
                    ally.RecovelyTurnTmpMinus((int)finalShorten);
                }
            }
        }
    }
    /// <summary>
    /// 被害者との相性値の高いキャラが攻撃者に対して対象者ボーナスを得るかどうか　復讐ボーナス
    /// </summary>
    void TryAddRevengeBonus()
    {
        for (var i = 0; i < unders.Count; i++)//被害者全員分ループ
        {
            var chara = unders.GetAtCharacter(i);
            var HelpGroup = MyGroup(chara);//所属するBattleGroupを取得
            var LiveAllyGroupList = GetOtherAlliesAlive(chara);//被害者の生きている味方
            if(LiveAllyGroupList.Count < 1)continue;//味方がいなければスキップ

            //特定の相性値%以上の相性値が被害者に対してある味方のみに絞る　味方→被害者　への相性値
            LiveAllyGroupList = LiveAllyGroupList.Where(ally => 
            HelpGroup.CharaCompatibility.ContainsKey((ally, chara)) && HelpGroup.CharaCompatibility[(ally, chara)] >= 86).ToList();
            if(LiveAllyGroupList.Count < 1)continue;//60以上の相性値を持ってる味方がいなければスキップ
            var data = chara.RecentDamageData;//被害者のダメージ記録
            var DamageRate = data.Damage/chara.MAXHP;//被害者のダメージ率

            foreach(var ally in LiveAllyGroupList)//特定の相性値以上の相性値を持ってる味方全員分ループ
            {
                if(ally.NowPower < ThePower.medium)continue;//パワーが普通未満ならスキップ

                // 1. 相性値取得（味方→被害者）
                float compatibility = HelpGroup.CharaCompatibility[(ally, chara)];
                // 有効な相性値は86以上。相性値が高いほど効果が大きくなるよう、線形補正
                // ここでは86で0%、130で1.0とする（130未満は0～1のグラデーション）
                float compatibilityFactor = Mathf.Clamp01((compatibility - 86f) / (130f - 86f));
                // 3. 最大を0.7に制限
                compatibilityFactor = Mathf.Clamp01(compatibilityFactor) * 0.7f; // 0～0.7

                // 2. 気力パワー補正（実行者の気力パワー）
                float powerFactor = 0.5f;//普通なら半分
                if(ally.NowPower > ThePower.medium)powerFactor = 1f;//高いならそのまま

                // 3. 発生確率の算出（複合方式：）
                // 複合係数 k を導入（ダメージ割合の影響度）
                float k = 1.5f; // 大きいほど DamageRate の影響が強くなる

                // 複合方式
                float occurrenceProbability = compatibilityFactor * (1f + k * DamageRate) * powerFactor;
                occurrenceProbability = Mathf.Clamp01(occurrenceProbability);// 0~1 に収める

                // 発生判定：発生確率が一定値以上なら復讐ボーナス発動
                if (rollper(occurrenceProbability * 100)) // rollperは%で判定
                {
                    // 4. 持続ターンの計算（最大12ターン）
                    // ここでは、発生確率に応じて期待値として計算し、離散化
                    float expectedDuration = occurrenceProbability * 12f;
                    int baseDuration = Mathf.FloorToInt(expectedDuration);
                    float extraChance = expectedDuration - baseDuration;
                    int duration = baseDuration;
                    if (RandomEx.Shared.NextFloat(1f) < extraChance/2.3f)//離散化の上振れを2.3で割って半減している
                    {
                        duration++;
                    }

                    // 5. ボーナス倍率の計算
                    // 例として、1.0倍からスタートし、発生確率に応じて上乗せする
                    // ここでは、発生確率が1.0でつまり一番大きいと、右の倍率がフルで上乗せ
                    float bonusMultiplier = 1f + occurrenceProbability * 0.4f;

                    // 6. 敵（攻撃者）に対して復讐ボーナスを適用する
                    ally.TargetBonusDatas.Add(duration+1, bonusMultiplier,data.Attacker);
                    //ほとんどターンの最後の方で指定されるため、持続ターン+1にして入れておく、

                }
            }
        }
    }
    /// <summary>
    /// スキルアクトを実行
    /// </summary>
    /// <returns></returns>
    private TabState SkillACT()
    {
        Debug.Log("スキル行使実行");
        var skill = Acter.NowUseSkill;

        //もしActs,先約リストでsingleTargetを予約しているのなら(死亡チェックはactbranchingのCharacterAddFromListOrRandomで行っています。)
        var singleTarget = Acts.GetAtSingleTarget(0);
        if (singleTarget != null)
        {   
            Acter.Target = DirectedWill.One;//ここで単体選択し、尚且つしたの行動者決定☆の処理を飛ばせる
            unders.CharaAdd(singleTarget);//対象者に追加
        }

        //もしランダム範囲ならこの段階で範囲意志にランダム範囲を入れる。
        if (skill.HasZoneTrait(SkillZoneTrait.RandomRange))
        {
            DetermineRangeRandomly();
        }

        //この時点で範囲意志が決まっていないなら、スキルの性質をそのまま入れる
        //要は範囲選択も対象者選択も発生しない自動決定型　RandomMultiやControlByThisSituationに、
        //ランダム範囲でない範囲系など
        if(Acter.RangeWill == 0)
        {
            Acter.RangeWill = skill.ZoneTrait;
        }

        //人数やスキルの攻撃傾向によって、被攻撃者の選別をする
        SelectTargetFromWill();

        //前のめりになるスキルなら前のめりになる。
        BeVanguard();

        //実行処理
        skill.SetDeltaTurn(BattleTurnCount);//スキルのdeltaTurnをセット
        CreateBattleMessage(Acter.AttackChara(unders));//攻撃の処理からメッセージが返る。

        Acter.CalmDownSet(skill.EvasionModifier,skill.AttackModifier);//スキル回避率と落ち着きカウントをセット
        

        //慣れフラットロゼが起こるかどうかの判定　
        TryAddFlatRoze();

        //相性値による被害者への救済意図での再行動ターン
        TryHelpMinusRecovelyTurnByCompatibility();

        //被害者と相性値の高いキャラが攻撃者に対して対象者ボーナスを得るかどうか 復讐ボーナス的な
        TryAddRevengeBonus();
        


        if (skill.NextConsecutiveATK())//まだ連続実行するなら
        {
            if(skill.HasConsecutiveType(SkillConsecutiveType.SameTurnConsecutive))//同一ターン内での連続攻撃　つまり普通の連続攻撃
            {
                NextTurn(false);

                //先約リストに今回の実行者を入れとく
                //if (Acts.Count <= 0) //このコードいらなくね
                Acts.Add(Acter, ActerFaction);

                Acter.FreezeSkill();//連続実行の為凍結

            }else if(skill.HasConsecutiveType(SkillConsecutiveType.FreezeConsecutive))//ターンをまたいだ連続攻撃
            {
                NextTurn(true);

                //混戦の中に身を任せるイメージなので、次にランダムターンなどで挟んだ連続攻撃だから　先約リストで次回予告はしない。
                //_atkCountupは【連続攻撃実行完了】以外ではBattlemanager単位での戦闘終了時と死亡時にしかリセットされないので、次回引っ掛かってもそのまま連続攻撃の途中と認識される。

                Acter.FreezeSkill();//連続実行の為凍結
                Acter.SetFreezeRangeWill(skill.ZoneTrait);//範囲意志も凍結
            }
        }
        else //複数実行が終わり
        {
            
            Acter.Defrost();//凍結されてもされてなくても空にしておく
            Acter.RecovelyCountTmpAdd(skill.SKillDidWaitCount);//スキルに追加硬直値があるならキャラクターの再行動クールタイムに追加
            Acter.NowUseSkill.ResetStock();//ストックをリセット　stockpileじゃなくても影響ないのでif文とかなくて平気

            NextTurn(true);

            //もし連続攻撃があった場合、それはここでは完了したので、自動で_atkCountUpは0になってる
        }

        unders = new UnderActersEntryList(this);//対象者リスト初期化
        Acter.RangeWill = 0;//範囲意志を初期化
        Acter.Target = 0;//対象者を初期化

        return ACTPop();

    }
    /// <summary>
    /// 慣れフラットロゼの発生判定と処理を行う
    /// 条件：
    /// 1. パッシブ0を持っている
    /// 2. 攻撃タイプのスキル
    /// 3. 前回前のめりでない
    /// 4. 前のめりになったら(今回のisagressivecommitスキルで後衛から前のめりに転じたなら)
    /// 5. スキル使用回数が20回以上
    /// 6. 単回攻撃である
    /// 7. 命中率が50か60に近いほど発生しやすい
    /// </summary>
    /// <returns>フラットロゼが発生したかどうか</returns>
    private bool TryAddFlatRoze()
    {
        if (!Acter.HasPassive(0)) return false;
        if (!Acter.NowUseSkill.HasType(SkillType.Attack)) return false;
        if (Acter._tempVanguard) return false;
        if (!IsVanguard(Acter)) return false;
        if (Acter.NowUseSkill.RecordDoCount <= 20) return false;
        if (Acter.NowUseSkill.NowConsecutiveATKFromTheSecondTimeOnward()) return false;
        
        // 命中率による発生判定
        if (RandomEx.Shared.NextInt(100) >= Ideal50or60Easing(Acter.NowUseSkill.SkillHitPer)) return false;

        // フラットロゼの効果を付与
        Acts.Add(Acter, ActerFaction, "淡々としたロゼ", new List<ReservationStatesModify>()
        {
            new()
            {
                eada = whatModify.eye,
                modify = 1.6f + GetCoolnesFlatRozePower(),
                memo = "ロゼ瞳"
            },
            new()
            {
                eada = whatModify.atk,
                modify = 0.5f,
                memo = "ロゼ威力半減"
            }
        }, true);

        return true;
    }
    /// <summary>
    /// テラーズヒット　後衛を狙っても前のめりがいた場合そいつのプレッシャーによってつい攻撃してしまう
    /// </summary>
    /// <returns></returns>
    private bool ComparePressureAndRedirect(BaseStates Attacker,BaseStates Vanguard)
    {
        var VanguardPressure = Vanguard.TenDayValues().GetValueOrZero(TenDayAbility.Glory);
        var AttackerResilience = Attacker.TenDayValues().GetValueOrZero(TenDayAbility.JoeTeeth) + Attacker.TenDayValues().GetValueOrZero(TenDayAbility.WaterThunderNerve) * 0.5f;

        // 前のめり防衛側のプレッシャー値未満の合計値の乱数が出た場合、テラーズヒット,庇いが発生する
        return VanguardPressure > RandomEx.Shared.NextFloat(VanguardPressure + AttackerResilience);
    }
    /// <summary>
    /// スキルの性質が範囲ランダムだった場合、
    /// 術者の範囲意志として性質通りにランダムで決定させる方法
    /// RandomMultiTargetは含まれない(何故ならランダムに変化する範囲としては曖昧にMultiTargetはすべて包括しているから。)
    /// </summary>
    private void DetermineRangeRandomly()
    {
        var skill = Acter.NowUseSkill;

        //範囲意志かターゲットが既にいたら、35%で既に選んでるのが取り消してランダム範囲が入る。
        //逆に言うと35%に引っかからないと、既に選んでるのでOK = ランダム範囲の処理を切り上げる。
        if(Acter.Target != 0 || Acter.RangeWill != 0)
        {
            if(!rollper(35f)) return;

            //ランダム範囲に切り替わるので、既にあるターゲットと範囲意志を初期化
            Acter.Target = 0;
            Acter.RangeWill = 0;
            //unders = 対象者は初期化しない。　　 
        }

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
        if (chara == AllyGroup.InstantVanguard) return true;
        if (chara == EnemyGroup.InstantVanguard) return true;
        return false;
    }
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
        if (ActerFaction == WhichGroup.alliy)
        {//味方なら敵グループから、
            SelectGroup = new BattleGroup(EnemyGroup.Ours, EnemyGroup.OurImpression, EnemyGroup.which);

            if (skill.HasZoneTrait(SkillZoneTrait.CanSelectAlly))//自陣も対象に選べるなら
            {
                OurGroup = new BattleGroup(AllyGroup.Ours, AllyGroup.OurImpression, AllyGroup.which);//自陣
                if(!skill.HasZoneTrait(SkillZoneTrait.CanSelectMyself))//自分自身を対象にする性質がないなら
                {
                    OurGroup.Ours.Remove(Acter);//自分自身を対象から除く
                }
            }else if(skill.HasZoneTrait(SkillZoneTrait.CanSelectMyself))//自分自身だけを対象にできるなら、
            {
                //自分自身だけを対象にする
                OurGroup = new BattleGroup(new List<BaseStates>{Acter}, AllyGroup.OurImpression, AllyGroup.which);//自陣
            }
        }
        else
        {//敵なら味方グループから選別する ディープコピー。
            SelectGroup = new BattleGroup(AllyGroup.Ours, AllyGroup.OurImpression, AllyGroup.which);
            if (skill.HasZoneTrait(SkillZoneTrait.CanSelectAlly))//自陣も対象に選べるなら
            {
                OurGroup = new BattleGroup(EnemyGroup.Ours, EnemyGroup.OurImpression, EnemyGroup.which);//自陣
                if(!skill.HasZoneTrait(SkillZoneTrait.CanSelectMyself))//自分自身を対象にする性質がないなら
                {
                    OurGroup.Ours.Remove(Acter);//自分自身を対象から除く
                }
            }else if(skill.HasZoneTrait(SkillZoneTrait.CanSelectMyself))//自分自身だけを対象にできるなら、
            {
                OurGroup = new BattleGroup(new List<BaseStates>{Acter}, EnemyGroup.OurImpression, EnemyGroup.which);//自陣
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
                        {//後衛を狙おうとしたなら 後衛への命中率は7割補正され　テラーズヒットが発生した場合前のめりしてる奴にあたる

                            if (ComparePressureAndRedirect(Acter,SelectGroup.InstantVanguard))//前衛のかばいに引っかかったら
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
                                Acter.SetEYEPercentageModifier(0.7f, "少し遠いよ");//後衛への命中率補正70%を追加。
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
                else //他メイン分岐とは違い、意志ではないので、スキルの範囲性質で指定する。
                if (skill.HasZoneTrait(SkillZoneTrait.ControlByThisSituation))//状況のみに縛られる。(前のめりにしか当たらないなら
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
                        {//後衛を狙おうとしたなら 後衛への命中率は9割補正され　テラーズヒットが発生した場合前のめりしてる奴にあたる

                            if (ComparePressureAndRedirect(Acter,SelectGroup.InstantVanguard))//前衛のかばいに引っかかったら
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
                                Acter.SetEYEPercentageModifier(90, "ほんの少し狙いにくい");//後衛への命中率補正を追加。
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
        if (Next)
        {
            //キャラの死によるそのキャラに対する味方の相性値が高ければ、人間状況の変化が起こる処理
            AllyGroup.PartyApplyConditionChangeOnCloseAllyDeath();
            EnemyGroup.PartyApplyConditionChangeOnCloseAllyDeath();

            //ターン数加算
            BattleTurnCount++;

            foreach(var chara in AllCharacters)
            {
                chara._tempVanguard = IsVanguard(chara);//前のめりの前回記録

                //全てのキャラクターの対象者ボーナスの持続ターンの処理
                chara.TargetBonusDatas.AllDecrementDurationTurn();//持続ターンがゼロ以下になったら削除
            }
            AllyGroup.OnPartyNextTurnNoArgument();//次のターンへ行く引数なしコールバック
            EnemyGroup.OnPartyNextTurnNoArgument();

        }
        

        //前のめり者が死亡してたら、nullにする処理
        AllyGroup.VanGuardDeath();
        EnemyGroup.VanGuardDeath();

        Acter.IsActiveCancelInSkillACT = false;//キャンセルフラグをオフ

    }

    /// <summary>
    /// battleManagerを消去するときの処理
    /// </summary>
    private void OnBattleEnd()
    {

        //全てのキャラクターの特別な補正をリセットする
        EnemyGroup.ResetCharactersUseThinges();
        AllyGroup.ResetCharactersUseThinges();        

        //敵キャラは死んだりした該当者のみ選んで復活準備
        EnemyGroup.RecovelyStart(PlayersStates.Instance.NowProgress);

        foreach (var one in AllCharacters)//全てのキャラの引数なし終わりのコールバック
        {
            one.OnBattleEndNoArgument();
        }
    }

    private void OnBattleStart()
    {
        //全キャラのrecovelyTurnを最大値にセットすることで全員行動可能
        EnemyGroup.PartyRecovelyTurnOK();
        AllyGroup.PartyRecovelyTurnOK();

        //引数なしのbmスタート時のコールバック
        foreach (var one in EnemyGroup.Ours)
        {
            one.OnBattleStartNoArgument();
        }
        foreach (var one in AllyGroup.Ours)
        {
            one.OnBattleStartNoArgument();
        }



    }

}