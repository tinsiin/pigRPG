using System;
using System.Collections.Generic;
using UnityEngine;
using RandomExtensions;
using System.Linq;
using RandomExtensions.Linq;
using RandomExtensions.Collections;
using Unity.VisualScripting;
using R3;
using Cysharp.Threading.Tasks;
using static CommonCalc;

/// <summary>
/// 戦闘の先手が起ったかどうか
/// </summary>
public enum BattleStartSituation
{
    alliFirst, EnemyFirst, Normal//味方先手、敵先手、ノーマル
}
/// <summary>
/// 敵味方どっち
/// </summary>
public enum allyOrEnemy
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
        public List<BaseStates> GetCharacterList()
        {
            return charas;
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

            if (skill.PowerSpread != null && skill.PowerSpread.Length > 0)//スキル分散値配列のサイズがゼロより大きかったら分散する
            {
                //爆発的
                if (skill.DistributionType == AttackDistributionType.Explosion)
                {
                    if (Manager.IsVanguard(chara))
                    {
                        if (CashSpread.Count > 0)
                        {
                            item = CashSpread[0];//前のめりなら前の"0"の分散値
                        }
                    }
                    else
                    {
                        if (CashSpread.Count > 1)
                        {
                            item = CashSpread[1];//後衛なら後ろの"1"の分散値
                        }
                        else if (CashSpread.Count > 0)
                        {
                            item = CashSpread[0];
                        }
                    }
                }
                //放射型、ビーム型
                if (skill.DistributionType == AttackDistributionType.Beam)
                {
                    if (CashSpread.Count > 0)
                    {
                        if (Manager.IsVanguard(chara))
                        {
                            item = CashSpread[0];//最初のを抽出
                            CashSpread.RemoveAt(0);
                        }
                        else
                        {
                            var lastIndex = CashSpread.Count - 1;
                            item = CashSpread[lastIndex];//末尾から抽出
                            CashSpread.RemoveAt(lastIndex);
                        }
                    }
                }
                //投げる型
                if (skill.DistributionType == AttackDistributionType.Throw)
                {
                    if (CashSpread.Count > 0)
                    {
                        if (Manager.IsVanguard(chara))
                        {
                            var lastIndex = CashSpread.Count - 1;
                            item = CashSpread[lastIndex];//末尾から抽出
                            CashSpread.RemoveAt(lastIndex);
                        }
                        else
                        {
                            item = CashSpread[0];//最初のを抽出
                            CashSpread.RemoveAt(0);
                        }
                    }
                }
                //ランダムの場合
                if (skill.DistributionType == AttackDistributionType.Random)
                {
                    if (CashSpread.Count > 0)
                    {
                        var lastIndex = CashSpread.Count - 1;
                        item = CashSpread[lastIndex];//末尾から抽出
                        CashSpread.RemoveAt(lastIndex);
                    }
                }

            }
            spreadPer.Add(item);
            charas.Add(chara);//追加

        }



    }



/// <summary>
///     バトルを、管理するクラス
/// </summary>
public class BattleManager : IBattleContext
{

    /// <summary>
    ///     プレイヤー側のバトルグループ　ここに味方のバトルグループオブジェクトをリンクする？
    /// </summary>
    public BattleGroup AllyGroup { get; set; }

    /// <summary>
    ///     敵側のバトルグループ　ここに敵グループのバトルグループオブジェクトをリンクする？
    /// </summary>
    public BattleGroup EnemyGroup { get; set; }
    /// <summary>
    ///全キャラクターのリスト
    /// </summary>
    public List<BaseStates> AllCharacters => AllyGroup.Ours.Concat(EnemyGroup.Ours).ToList();

    /// <summary>
    /// factionのグループを返す
    /// </summary>
    public BattleGroup FactionToGroup(allyOrEnemy faction)
    {
        switch(faction)
        {
            case allyOrEnemy.alliy:
                return AllyGroup;
            case allyOrEnemy.Enemyiy:
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
    public allyOrEnemy GetCharacterFaction(BaseStates chara)
    {
        foreach(var one in AllyGroup.Ours)
        {
            if(one == chara)return allyOrEnemy.alliy;
        }
        foreach(var one in EnemyGroup.Ours)
        {
            if(one == chara)return allyOrEnemy.Enemyiy;
        }

        return 0;
    }

    /// <summary>
    /// そのキャラクターと同じパーティーの生存者のリストを取得(自分自身を除く)
    /// </summary>
    public List<BaseStates> GetOtherAlliesAlive(BaseStates chara) => 
    RemoveDeathCharacters(FactionToGroup(GetCharacterFaction(chara)).Ours).Where(x => x != chara).ToList();

    private BattleStartSituation firstSituation;


    string UniqueTopMessage;//通常メッセージの冠詞？
    public BaseStates Acter { get; set; }//今回の俳優
    /// <summary>
    /// 行動を受ける人 
    /// </summary>
    public UnderActersEntryList unders { get; private set; }
    List<BaseStates> RatherTargetList;//レイザーアクトの対象リスト
    float RatherDamageAmount;
    
    allyOrEnemy ActerFaction;//陣営
    private readonly BattleState battleState = new BattleState();
    private readonly TurnScheduler turnScheduler;
    private readonly TargetingService targetingService = new TargetingService();
    private readonly EffectResolver effectResolver = new EffectResolver();
    private readonly BattleUIBridge uiBridge;
    private readonly IBattleMetaProvider metaProvider;
    bool Wipeout { get => battleState.Wipeout; set => battleState.Wipeout = value; }//全滅したかどうか
    bool IsRater = false;//レイザーダメージのターンかどうか
    bool EnemyGroupEmpty { get => battleState.EnemyGroupEmpty; set => battleState.EnemyGroupEmpty = value; }//敵グループが空っぽ
    bool AlliesRunOut { get => battleState.AlliesRunOut; set => battleState.AlliesRunOut = value; }//味方全員逃走
    NormalEnemy VoluntaryRunOutEnemy { get => battleState.VoluntaryRunOutEnemy; set => battleState.VoluntaryRunOutEnemy = value; }//敵一人の逃走
    /// <summary>
    /// 連鎖逃走する敵リスト
    /// </summary>
    List<NormalEnemy> DominoRunOutEnemies => battleState.DominoRunOutEnemies;
    public bool DoNothing { get; set; } = false;//何もしない
    public bool PassiveCancel { get; set; } = false;//パッシブキャンセル
    public bool SkillStock { get; set; } = false;//スキルストック
    public bool VoidTurn = false;//そのターンは無かったことに
    private readonly float stageEscapeRate;

    /// <summary>
    /// 行動リスト　ここではrecovelyTurnの制約などは存在しません
    /// </summary>
    public ActionQueue Acts { get; private set; }//行動先約リスト？

    public int BattleTurnCount
    {
        get => battleState.TurnCount;
        private set => battleState.TurnCount = value;
    }//バトルの経過ターン

    /// <summary>
    ///コンストラクタ
    /// </summary>
    public BattleManager(BattleGroup allyGroup, BattleGroup enemyGroup, BattleStartSituation first, MessageDropper messageDropper, float escapeRate, IBattleMetaProvider metaProvider, IPlayersSkillUI skillUi, IPlayersRoster roster)
    {
        AllyGroup = allyGroup;
        EnemyGroup = enemyGroup;
        firstSituation = first;
        Acts = new ActionQueue();
        turnScheduler = new TurnScheduler(AllyGroup, EnemyGroup, Acts, battleState);
        unders = new UnderActersEntryList(this);
        // Phase 1: WatchUIUpdate.Instanceはここでのみ使用し、各コントローラーを注入
        var wui = WatchUIUpdate.Instance;
        uiBridge = new BattleUIBridge(
            messageDropper,
            skillUi,
            roster,
            wui?.ActionMarkCtrl,
            wui?.EnemyPlacementCtrl,
            wui?.IntroOrchestrator,  // Intro Orchestrator Facade（文脈込み）
            SchizoLog.Instance,  // Phase 3a: SchizoLog注入
            BattleSystemArrowManager.Instance);  // Phase 3d: ArrowManager注入
        uiBridge.BindBattleContext(this);
        BattleUIBridge.SetActive(uiBridge);
        stageEscapeRate = escapeRate;
        BattleContextHub.Set(this);
        this.metaProvider = metaProvider;

        

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
            chara.ApplyConditionOnBattleStart(EnemyGroup.OurAvarageTenDayPower(false));
        }
        foreach (var chara in EnemyGroup.Ours)
        {
            chara.ApplyConditionOnBattleStart(AllyGroup.OurAvarageTenDayPower(false));
        }
        //初期化コールバックで変化判定用にConditionTransitionが実行され記録されるので、初期化コールバックの前に呼ばなければなりません。

        OnBattleStart();//初期化コールバック

    }
    

    /// <summary>
    /// キャラクター行動リストに先手分のリストを入れる。
    /// 先手後手システムのobsidianを参照
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
    /// BattleManager内の一時保存要素のリセット？
    /// </summary>
    private void ResetManagerTemp()
    {
        UniqueTopMessage = "";
    }
    private void AppendUniqueTopMessage(string message)
    {
        if (string.IsNullOrEmpty(message)) return;
        UniqueTopMessage += message;
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
            List<ModifierPart> modList;
            if ((modList = Acts.GetAtModifyList(0)) != null)//リストが存在するなら
            {
                foreach (var mod in modList)//補正リスト内のアイテムで回す
                {
                    Acter.CopySpecialModifier(mod);//そのままコピー
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

            //レイザーダメージの対象者がいるならレイザーアクト
            var ratherTarget = Acts.GetAtRaterTargets(0);
            if (ratherTarget != null)
            {
                RatherTargetList.AddRange(ratherTarget);
                RatherDamageAmount = Acts.GetAtRaterDamage(0);
                IsRater = true;
                Debug.Log("レイザーアクト");
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
            Acter = turnScheduler.SelectRandomActer(out ActerFaction);
            Debug.Log("俳優はランダムに選ばれました");
        }

    }
    /// <summary>
    /// 行動準備 次のボタンを決める
    /// </summary>
    public TabState ACTPop()
    {
        uiBridge.DisplayLogs();//ログの更新
        ResetManagerTemp();//一時保存要素をリセット
        turnScheduler.RemoveDeadReservations();//先約リストから死者を取り除く

        //パーティーの死亡判定
        if (AllyGroup.PartyDeathOnBattle())
        {
            Wipeout = true;
            ActerFaction = allyOrEnemy.alliy;
            return TabState.NextWait;//押して処理
        }
        else if (EnemyGroup.PartyDeathOnBattle())
        {
            Wipeout = true;
            ActerFaction = allyOrEnemy.Enemyiy;
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
        
        // 俳優が確定したので、アクションマークをそのアイコンへ移動させる（サイズはActionMarkUI側で自動）
        // VoidTurnでスキップされるケースを除外した直後に実行
        uiBridge.MoveActionMarkToActorScaled(Acter, false, true);
        //レイザーダメージアクトにはこの後のスキルの選択がいらないので
        if(IsRater)
        {// => CharacterActBranching
            return TabState.NextWait;
        }

        if(Acter == null)//俳優がnullだとランダム選別の際に「再行動できるキャラがいない」とされて処理がキャンセルされたので
        {
            ACTSkipACTBecauseNobodyACT = true;
            return TabState.NextWait;//押して処理
        }

        //スキルと範囲の思考--------------------------------------------------------------------------------------------------------スキルと範囲の思考-----------------------------------------------------------

        //俳優が味方なら
        bool isFreezeByPassives = Acter.IsFreezeByPassives;//パッシブ由来で行動できないかどうか。
        bool hasCanCancelCantACTPassive = Acter.HasCanCancelCantACTPassive;//キャンセル可能な行動不能パッシブがあるかどうか。
        if (ActerFaction == allyOrEnemy.alliy)
        {//味方が行動するならば
            Debug.Log(Acter.CharacterName + "(主人公キャラ)は行動する");
            // Characonfig の選択キャラを現在の主人公アクターに同期
            uiBridge.SetSelectedActor(Acter);
            
            if (!Acter.IsFreeze)//強制続行中のスキルがなければ
            {
                Debug.Log(Acter.CharacterName + "主人公キャラの強制続行スキルがないのでスキル選択へのパッシブ判定処理へと進みます。");
                if (!isFreezeByPassives || hasCanCancelCantACTPassive)
                {
                    // パッシブによる行動不能でない、または
                    // キャンセル可能なパッシブを持っている場合
                    uiBridge.SwitchAllySkillUiState(Acter, Acts.GetAtSingleTarget(0) != null);
                    Debug.Log(Acter.CharacterName + "(主人公キャラ)はスキル選択");
                    return TabState.Skill;
                }else//パッシブ由来で行動不能ならば
                {
                    DoNothing = true;//何もしない(できない)で飛ばす
                }
            }
            else//スキル強制続行中なら、
            {
                //俳優が自分のFreezeConsecutiveを削除する予約をしているのなら、
                if (Acter.IsDeleteMyFreezeConsecutive)
                {
                    Acter.DeleteConsecutiveATK();//連続実行FreezeConsecutiveを削除
                    DoNothing = true;//何もしない
                    Debug.Log(Acter.CharacterName + "（主人公キャラ）は何もしない");
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
                    Debug.Log(Acter.CharacterName + "（主人公キャラ）は連続攻撃中の操作へ");
                    return AllyClass.DetermineNextUIState(skill);
                }
            }
        }

        //敵ならここで思考して決める
        if (ActerFaction == allyOrEnemy.Enemyiy)
        {
            var ene = Acter as NormalEnemy;
            if(isFreezeByPassives && !hasCanCancelCantACTPassive)//パッシブ由来で行動できず、キャンセル可能&行動不能パッシブがないならば
            {
                DoNothing = true;//行動不能
            }else
            {
                ene.SkillAI();//ここで決めないとスキル可変オプションが下記の対象者選択で反映されないから
            }
        }
        //スキルと範囲の思考--------------------------------------------------------------------------------------------------------スキルと範囲の思考-----------------------------------------------------------

        


        return TabState.NextWait;// nextwait = CharacterACTBranching


    }

    /// <summary>
    /// 俳優の行動の分岐
    /// </summary>
    public async UniTask<TabState> CharacterActBranching()
    {
        Debug.Log("俳優の行動の分岐-NextWaitボタンが押されました。");
        uiBridge.NextArrow();//システム矢印を進める
        if (IsRater)
        {        //パッシブ等のレイザーダメージアクト acter=nullに弾かれるのでここに移動
            IsRater = false;
            return RatherACT();
        }
        if(Acter == null)
        {
            Debug.LogError("俳優が認識されていない-エンカウントロジックなどに問題あり");
            return TabState.walk;
        }
        var skill = Acter.NowUseSkill;
        if (skill == null && !DoNothing)
        {
            Debug.LogError($"NowUseSkillがnullです。俳優:{Acter.CharacterName} の行動をスキップします。");
            return DoNothingACT();
        }
        var IsEscape = Acter.SelectedEscape;//逃げる意思

        if (Wipeout || AlliesRunOut || EnemyGroupEmpty) //全滅か主人公達逃走かでダイアログ終了アクトへ
        {
            uiBridge.AddLog("全滅か主人公達逃走かでダイアログ終了アクトへ",true);
            //Bmは終了へ向かうので、RunOutもWipeOutもfalseにする必要はない。
            return DialogEndACT();
        }
        //敵の連鎖逃走リストがあるなら
        if(DominoRunOutEnemies.Count > 0)
        {
            return DominoEscapeACT();//連鎖逃走の処理へ
        }
        /*
        //誰も動けないのでスキップし、時間が進む
        if(ACTSkipACTBecauseNobodyACT)
        {
            ACTSkipACTBecauseNobodyACT = false;
            return ACTPop();
        }*/ //ここ間違ってる？　下に同じ処理あるけど　よく分からんから残しとく

        if(SkillStock)//スキルストック
        {
            return SkillStockACT();
        }
        if(PassiveCancel)//パッシブキャンセル
        {
            return PassiveCancelACT();
        }

        // FreezeConsecutiveの削除予約実行ターン/何もしない、のボタンを押した。/パッシブで行動不能
        if(DoNothing)
        {
            return DoNothingACT();
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

            if(CheckPassivesSkillActivation())
            {
                return await SkillACT();
            }
            //ここに　スキル発動失敗のエフェクトを入れる
            return DoNothingACT();//発動失敗したら何もせず行動準備へ
        }
        
    }
    /// <summary>
    /// パッシブによる発動率判定
    /// </summary>
    bool CheckPassivesSkillActivation() 
    {
        //if(Acter.PassivesSkillActivationRate() >= 100) return true;//別にこの行要らないか
        return rollper(Acter.PassivesSkillActivationRate());
    }
    TabState SkillStockACT()
    {
        BeVanguard_SkillStockACT();//前のめりになるかならないか
        //スキルストックのエフェクト
        SkillStock= false;
        NextTurn(true);
        return ACTPop();//何もせず行動準備へ

    }
    TabState PassiveCancelACT()
    {
        PassiveCancel = false;//パッシブキャンセルのエフェクトでも入れる
        NextTurn(true);
        return ACTPop();//何もせず行動準備へ
    }
    TabState DoNothingACT()
    {
        //小さなアイコン辺りに無音の灰色円縮小エフェクトを入れる     何もしないエフェクト
        DoNothing = false;
        NextTurn(true);
        return ACTPop();//何もせず行動準備へ
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
        if(ActerFaction == allyOrEnemy.alliy)
        {
            var Rate = stageEscapeRate;
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
    /// レイザーダメージのACT
    /// </summary>
    /// <returns></returns>
    public TabState RatherACT()
    {
        //レイザーダメージの処理
        effectResolver.ApplyRatherDamage(RatherTargetList, RatherDamageAmount);
        RatherDamageAmount = 0;//レイザー系初期化
        RatherTargetList.Clear();
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
            if (ActerFaction == allyOrEnemy.alliy)
            {
                uiBridge.PushMessage("死んだ");
                metaProvider?.OnPlayersLost();

                //敵たちの勝利時コールバック。
                EnemyGroup.EnemyiesOnWin();
            }
            else
            {
                uiBridge.PushMessage("勝ち抜いた");
                metaProvider?.OnPlayersWin();
            }
        }
        if (AlliesRunOut) 
        {
            uiBridge.PushMessage("我々は逃げた");
            metaProvider?.OnPlayersRunOut();
            EnemyGroup.EnemiesOnAllyRunOut();//敵の主人公達が逃げ出した時のコールバック
        }
        if (EnemyGroupEmpty)
        {
            uiBridge.PushMessage("敵はいなくなった");
           //敵が逃げたときのはそれぞれコールバックしたのでここで敵のコールバックは行わない。

           //一応主人公達は勝った扱い
           metaProvider?.OnPlayersWin();
        }

        OnBattleEnd().Forget();
        return TabState.walk;
    }
    
    /// <summary>
    /// 戦闘系のメッセージ作成
    /// </summary>
    /// <param name="txt"></param>
    private void CreateBattleMessage(string txt)
    {
        uiBridge.PushMessage(UniqueTopMessage + txt);
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
        if (skill != null && skill.CanCancelTrigger == false)//キャンセル不可能の場合。
        {
            Acter.FreezeSkill();//このスキルがキャンセル不可能として俳優に凍結される。
        }
        BeVanguard_TriggerACT();//前のめりになるかどうか
        //他のスキルの発動カウントを巻き戻す
        OtherSkillsTriggerRollBack();
        //発動カウントのメッセージ
        CreateBattleMessage($"{skill.SkillName}の発動カウント！残り{count}回。");
        //発動カウント時はスキルの複数回連続実行がありえないから、普通にターンが進む
        NextTurn(true);

        return ACTPop();
    }
    /// <summary>
    /// 前のめりになるかどうか
    /// </summary>
    /// <param name="newVanguard"></param>
    public void BeVanguard(BaseStates newVanguard)
    {
        if(!TryBlockVanruard(newVanguard))
        {
            var oldVanguard = MyGroup(newVanguard).InstantVanguard ;
            //もし前の前のめりと異なるなら　新しいキャラに前のめりエフェクト
            if(oldVanguard!= newVanguard)
            {
                uiBridge.ApplyVanguardEffect(newVanguard, oldVanguard);
            }
            MyGroup(newVanguard).InstantVanguard = newVanguard;
        }
    }
    /// <summary>
    /// スキル実行時に踏み込むのなら、俳優がグループ内の前のめり状態になる
    /// </summary>
    void BeVanguard_SkillACT()
    {
        var skill = Acter.NowUseSkill;
        if (skill != null && skill.IsAggressiveCommit)
        {
            BeVanguard(Acter);
        }
    }
    /// <summary>
    /// 発動カウント実行時に踏み込むのなら、俳優がグループ内の前のめり状態になる
    /// </summary>
    void BeVanguard_TriggerACT()
    {
        var skill = Acter.NowUseSkill;
        if (skill != null && skill.IsReadyTriggerAgressiveCommit)
        {
            BeVanguard(Acter);
        }
    }
    /// <summary>
    /// スキルストック時に踏み込むのなら、俳優がグループ内の前のめり状態になる
    /// </summary>
    void BeVanguard_SkillStockACT()
    {
        var skill = Acter.NowUseSkill;
        if (skill != null && skill.IsStockAgressiveCommit)
        {
            BeVanguard(Acter);
        }
    }
    /// <summary>
    /// 前のめりの交代阻止パッシブ用の判定関数
    /// </summary>
    /// <returns></returns>
    public bool TryBlockVanruard(BaseStates newVanguard)
    {
        var group = MyGroup(newVanguard);

        //前のめり者がいなければ終わり
        if (group.InstantVanguard == null) return false;
        //前のめり者が交代阻止のパッシブを持っていなければ終わり
        if (!group.InstantVanguard.HasBlockVanguardByAlly_IfImVanguard()) return false;

        var nowVanguard = group.InstantVanguard;

        // パーティー属性係数
        float attrRate = group.OurImpression == PartyProperty.HolyGroup ? 0.7f : 1f;

        // 前のめり引き留め側合算
        float defendSum =
            nowVanguard.TenDayValues(false).GetValueOrZero(TenDayAbility.Leisure) +
            nowVanguard.TenDayValues(false).GetValueOrZero(TenDayAbility.SpringNap) +
            nowVanguard.TenDayValues(false).GetValueOrZero(TenDayAbility.SpringWater) +
            nowVanguard.TenDayValues(false).GetValueOrZero(TenDayAbility.FlameBreathingWife);

        // 前のめりになろうとする側取得
        float blaze = newVanguard.TenDayValues(false).GetValueOrZero(TenDayAbility.BlazingFire);
        float pilma = newVanguard.TenDayValues(false).GetValueOrZero(TenDayAbility.Pilmagreatifull);
        float miza  = newVanguard.TenDayValues(false).GetValueOrZero(TenDayAbility.Miza);

        // 冷酷冷静による個別減算
        float coldCalm = newVanguard.TenDayValues(false).GetValueOrZero(TenDayAbility.ColdHeartedCalm);
        blaze = Mathf.Max(blaze - coldCalm * 0.8f, 0f);
        pilma = Mathf.Max(pilma - coldCalm * 0.2f, 0f);

        // エノクナギによる全体減算
        float enok = newVanguard.TenDayValues(false).GetValueOrZero(TenDayAbility.Enokunagi);

        float attackSum = blaze + pilma + miza - enok;
        attackSum = Mathf.Max(attackSum, 0f);

        float NowVanguardScore = defendSum * attrRate;
        float WantBeVanguardScore = attackSum * (1f - attrRate);
        if (NowVanguardScore + WantBeVanguardScore <= 0f) return false;//そもそも比較できるスコアがないなら終わり

        // 合計値＝NowVanguardScore+WantBeVanguardScore の範囲で一様乱数を取り、
        // WantBeVanguardScore 以下なら “入れ替え成功”（防止失敗）になる。
        return RandomEx.Shared.NextFloat(NowVanguardScore + WantBeVanguardScore) <= NowVanguardScore;
    }
    /// <summary>
    /// スキルアクトを実行
    /// </summary>
    /// <returns></returns>
    private async UniTask<TabState> SkillACT()
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
            // スキルの範囲性質を正規化してから代入（競合解消）
            Acter.RangeWill = SkillZoneTraitNormalizer.NormalizeForInitial(skill.ZoneTrait);
        }

        //人数やスキルの攻撃傾向によって、被攻撃者の選別をする
        targetingService.SelectTargets(Acter, ActerFaction, AllyGroup, EnemyGroup, unders, AppendUniqueTopMessage);

        //前のめりになるスキルなら前のめりになる。
        BeVanguard_SkillACT();

        if(unders.Count < 1)
        {
            Debug.LogError("AttackChara寸前なのに、対象者(ACに渡すunders)がいません、いないということわあり得るのかな？");
        }

        await effectResolver.ResolveSkillEffectsAsync(
            Acter,
            ActerFaction,
            unders,
            AllyGroup,
            EnemyGroup,
            Acts,
            BattleTurnCount,
            CreateBattleMessage);
        


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
                // 範囲性質を正規化してから凍結（競合解消）
                Acter.SetFreezeRangeWill(SkillZoneTraitNormalizer.NormalizeForInitial(skill.ZoneTrait));
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
    /// スキルの性質が範囲ランダムだった場合、
    /// 術者の範囲意志として性質通りにランダムで決定させる方法
    /// RandomMultiTargetは含まれない(何故ならランダムに変化する範囲としては曖昧にMultiTargetはすべて包括しているから。)
    /// </summary>
    private void DetermineRangeRandomly()//この段階ではFromWill前の「範囲意志代入」は行われていない。
    {
        var skill = Acter.NowUseSkill;

        if(skill.HasZoneTrait(SkillZoneTrait.SelfSkill))return;
        //セルフスキルなら、CanSelectMySelfと違い、選択性を排除した優先的性質なので、RandomRangeが間違って含まれても絶対に反応しない。

        //範囲意志かターゲットが既にいたら、35%で既に選んでるのが取り消してランダム範囲が入る。
        //逆に言うと35%に引っかからないと、既に選んでるのでOK = ランダム範囲の処理を切り上げる。
        if(Acter.Target != 0 || Acter.RangeWill != 0)
        {
            var RandomCaculatedPer = 35f;
            if(skill.HasZoneTrait(SkillZoneTrait.ControlByThisSituation))
            {
                RandomCaculatedPer = 14f;//CotrolByThisSituationなら、更にランダム範囲が選ばれる確率を下げる
            }
            if(!rollper(RandomCaculatedPer)) return;

            //ランダム範囲に切り替わるので、既にあるターゲットと範囲意志を初期化
            Acter.Target = 0;
            Acter.RangeWill = 0;
            //unders = 対象者は初期化しない。　　 つまり完全単体選択/対象者ボーナスの場合はランダム範囲と同居が可能
        }
        Acter.SkillCalculatedRandomRange = true;//範囲計算フラグ

        // スキルの全性質をまず範囲意志に代入（サブ的性質も含むために）
        Acter.RangeWill = skill.ZoneTrait;

        // SkillZoneTraitGroupsを使用して性質を除去（詳しくはメモ、範囲性質の仕様書を参照）
        // ランダム分岐用性質を除去（これらは分岐のための性質なので実行時には不要）
        Acter.RangeWill = Acter.RangeWill.Remove(SkillZoneTraitGroups.RandomBranchTraits);

        // 実際の範囲性質も除去（これから適切なものを選択して追加するため）
        Acter.RangeWill = Acter.RangeWill.Remove(SkillZoneTraitGroups.ActualRangeTraits);

        // メイン選択性質を除去（SelectTargetFromWillで処理されるため、競合を避ける）
        Acter.RangeWill = Acter.RangeWill.Remove(SkillZoneTraitGroups.MainSelectTraits);

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
                        if(Acter.HasRangeWill(SkillZoneTrait.SelectOnlyAlly))
                        {
                            Acter.RangeWill |= SkillZoneTrait.RandomMultiTarget;//完全複数ランダムに変化(味方だけに前のめり区別とかないから。)
                        }else
                        {
                            Acter.RangeWill |= SkillZoneTrait.RandomSelectMultiTarget;//前のめり後衛ランダム(複数単位)
                        }
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
                        if(Acter.HasRangeWill(SkillZoneTrait.SelectOnlyAlly))
                        {
                            Acter.RangeWill |= SkillZoneTrait.RandomMultiTarget;//完全複数ランダムに変化(味方だけに前のめり区別とかないから。)
                        }else
                        {
                            Acter.RangeWill |= SkillZoneTrait.RandomSelectMultiTarget;//前のめり後衛ランダム(複数単位)
                        }
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
                        if(Acter.HasRangeWill(SkillZoneTrait.SelectOnlyAlly))
                        {
                            Acter.RangeWill |= SkillZoneTrait.RandomMultiTarget;//完全複数ランダムに変化(味方だけに前のめり区別とかないから。)
                        }else
                        {
                            Acter.RangeWill |= SkillZoneTrait.RandomSelectMultiTarget;//前のめり後衛ランダム(複数単位)
                        }
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
    public async UniTask OnBattleEnd()
    {

        //全てのキャラクターの特別な補正をリセットする
        EnemyGroup.ResetCharactersUseThinges();
        AllyGroup.ResetCharactersUseThinges();        

        //敵キャラは死んだりした該当者のみ選んで復活準備
        var progress = metaProvider != null ? metaProvider.GlobalSteps : 0;
        EnemyGroup.RecovelyStart(progress);

        //敵グループの終了時のスキルAI
        EnemyGroup.EnemiesBattleEndSkillAI();

        foreach (var one in AllCharacters)//全てのキャラの引数なし終わりのコールバック
        {
            one.OnBattleEndNoArgument();
        }

        // ActionMark を表示
        if (!uiBridge.PrepareBattleEnd())
        {
            return;
        }

        if (metaProvider != null)
        {
            metaProvider.SetAlliesUIActive(false);//全味方UI非表示
        }
        uiBridge.ClearArrows();//矢印を消す
        uiBridge.HardStopAndClearLogs();
        await uiBridge.RestoreZoomViaOrchestrator(animated: true, duration: 0.4f);//ズームの処理

        // ズームアウト完了後にEyeAreaStateをWalkに戻す
        if (UIStateHub.EyeState != null)
        {
            UIStateHub.EyeState.Value = EyeAreaState.Walk;
        }

        //schizoLog.AddLog("戦闘を終わらせた",true);
        //schizoLog.DisplayAllAsync().Forget();//ACTPOPが呼ばれないのでここで呼ぶ
        //そもそも戦闘終わりはschizologではなくMessageDropperで行われるのが前提だけど、デバック用にね

        BattleContextHub.Clear(this);
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

        // ActionMark を表示
        uiBridge.ShowActionMarkFromSpawn();
        uiBridge.SetArrowColorsFromStage();//矢印にステージテーマ色を適用
    }

}
