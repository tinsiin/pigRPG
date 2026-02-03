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
        private readonly IBattleQueryService _queryService;
        private BaseSkill _currentSkill;
        private int _frontIndex;
        private int _backIndex;

        /// <summary>
        /// 対象者キャラクターリスト
        /// </summary>
        public List<BaseStates> charas;
        /// <summary>
        /// 割り当てられる"当てられる"スキルの分散値
        /// </summary>
        List<float> spreadPer;

        public int Count => charas.Count;

        public UnderActersEntryList(IBattleQueryService queryService)
        {
            charas = new List<BaseStates>();
            spreadPer = new List<float>();
            _queryService = queryService;
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
        /// 現在のスキルを設定（CharaAdd前に呼び出す）
        /// </summary>
        public void SetCurrentSkill(BaseSkill skill)
        {
            _currentSkill = skill;
            _frontIndex = 0;
            _backIndex = 0;
        }

        /// <summary>
        /// 既にある対象者リストをそのまま処理。
        /// SetCurrentSkillを先に呼んでおくこと。
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
            if (_currentSkill == null)
            {
                spreadPer.Add(1f);
                charas.Add(chara);
                return;
            }

            var spreadValues = _currentSkill.PowerSpread;
            float item = 1f;

            if (spreadValues != null && spreadValues.Length > 0)
            {
                var calculator = TargetDistributionService.GetCalculator(_currentSkill.DistributionType);
                var isVanguard = _queryService?.IsVanguard(chara) ?? false;
                var (ratio, nextFront, nextBack) = calculator.Calculate(
                    spreadValues, _frontIndex, _backIndex, isVanguard, charas.Count + 1);
                item = ratio;
                _frontIndex = nextFront;
                _backIndex = nextBack;
            }

            spreadPer.Add(item);
            charas.Add(chara);
        }

        /// <summary>
        /// リストをクリア
        /// </summary>
        public void Clear()
        {
            charas.Clear();
            spreadPer.Clear();
            _currentSkill = null;
            _frontIndex = 0;
            _backIndex = 0;
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
    public BattleGroup AllyGroup { get; private set; }

    /// <summary>
    ///     敵側のバトルグループ　ここに敵グループのバトルグループオブジェクトをリンクする？
    /// </summary>
    public BattleGroup EnemyGroup { get; private set; }
    /// <summary>
    ///全キャラクターのリスト
    /// </summary>
    public List<BaseStates> AllCharacters => AllyGroup.Ours.Concat(EnemyGroup.Ours).ToList();

    /// <summary>
    /// factionのグループを返す
    /// </summary>
    public BattleGroup FactionToGroup(allyOrEnemy faction) => actionContext.FactionToGroup(faction);

    /// <summary>
    /// キャラクターのグループを取得
    /// </summary>
    public BattleGroup MyGroup(BaseStates chara) => actionContext.GetGroupForCharacter(chara);

    /// <summary>
    /// 同じグループかどうか
    /// </summary>
    public bool IsFriend(BaseStates chara1, BaseStates chara2) => actionContext.IsFriend(chara1, chara2);

    /// <summary>
    /// 渡されたキャラクタのbm内での陣営を表す。
    /// </summary>
    public allyOrEnemy GetCharacterFaction(BaseStates chara) => actionContext.GetCharacterFaction(chara);

    /// <summary>
    /// そのキャラクターと同じパーティーの生存者のリストを取得(自分自身を除く)
    /// </summary>
    public List<BaseStates> GetOtherAlliesAlive(BaseStates chara) => actionContext.GetOtherAlliesAlive(chara);

    private BattleStartSituation firstSituation;


    private readonly BattlePresentation presentation;
    private readonly BattleActionContext actionContext;

    /// <summary>
    /// ActionContext への参照（Orchestrator等からのアクセス用）
    /// </summary>
    internal BattleActionContext ActionContext => actionContext;

    // ActionContext への委譲プロパティ
    public BaseStates Acter
    {
        get => actionContext.Acter;
        set => actionContext.Acter = value;
    }
    /// <summary>
    /// 行動を受ける人 (actionContext.Unders への委譲)
    /// </summary>
    public UnderActersEntryList unders => actionContext.Unders;
    // ActionContext への委譲プロパティ (ActerFaction)
    internal allyOrEnemy ActerFactionValue
    {
        get => actionContext.ActerFaction;
        set => actionContext.ActerFaction = value;
    }
    internal allyOrEnemy CurrentActerFaction => actionContext.ActerFaction;
    private readonly BattleState battleState = new BattleState();
    private readonly BattleStateManager battleStateManager;
    private readonly TurnScheduler turnScheduler;
    private readonly TargetingService targetingService = new TargetingService();
    private readonly EffectResolver effectResolver = new EffectResolver();
    private readonly BattleUIBridge uiBridge;
    private readonly IBattleMetaProvider metaProvider;
    private readonly EscapeHandler escapeHandler;
    private readonly SkillExecutor skillExecutor;
    private readonly TurnExecutor turnExecutor;
    private readonly CharacterActExecutor characterActExecutor;
    private readonly ActionSkipExecutor actionSkipExecutor;
    private readonly BattleFlow battleFlow;
    internal BattleUIBridge UiBridge => uiBridge;
    internal TurnScheduler TurnScheduler => actionContext.TurnScheduler;
    internal BattleStateManager StateManager => actionContext.StateManager;

    // ActionContext への委譲プロパティ (State flags)
    internal bool Wipeout { get => actionContext.Wipeout; set => actionContext.Wipeout = value; }
    internal bool IsRater { get => actionContext.IsRather; set => actionContext.IsRather = value; }
    internal bool EnemyGroupEmpty { get => actionContext.EnemyGroupEmpty; set => actionContext.EnemyGroupEmpty = value; }
    internal bool AlliesRunOut { get => actionContext.AlliesRunOut; set => actionContext.AlliesRunOut = value; }
    internal NormalEnemy VoluntaryRunOutEnemy { get => actionContext.VoluntaryRunOutEnemy; set => actionContext.VoluntaryRunOutEnemy = value; }
    internal List<NormalEnemy> DominoRunOutEnemies => actionContext.DominoRunOutEnemies;

    // ActionContext への委譲プロパティ (Action flags)
    public bool DoNothing { get => actionContext.DoNothing; set => actionContext.DoNothing = value; }
    public bool PassiveCancel { get => actionContext.PassiveCancel; set => actionContext.PassiveCancel = value; }
    public bool SkillStock { get => actionContext.SkillStock; set => actionContext.SkillStock = value; }
    public bool VoidTurn { get => actionContext.VoidTurn; set => actionContext.VoidTurn = value; }
    internal bool ActSkipBecauseNobodyAct { get => actionContext.ActSkipBecauseNobodyAct; set => actionContext.ActSkipBecauseNobodyAct = value; }
    private readonly float stageEscapeRate;
    internal float StageEscapeRate => stageEscapeRate;

    /// <summary>
    /// 行動リスト　ここではrecovelyTurnの制約などは存在しません
    /// </summary>
    public ActionQueue Acts => actionContext.Acts;

    public int BattleTurnCount
    {
        get => actionContext.BattleTurnCount;
        private set => actionContext.BattleTurnCount = value;
    }

    /// <summary>
    ///コンストラクタ
    /// </summary>
    public BattleManager(BattleGroup allyGroup, BattleGroup enemyGroup, BattleStartSituation first, MessageDropper messageDropper, float escapeRate, IBattleMetaProvider metaProvider, IPlayersSkillUI skillUi, IPlayersRoster roster)
    {
        AllyGroup = allyGroup;
        EnemyGroup = enemyGroup;
        firstSituation = first;

        // ActionContext を先に生成（状態を集約）
        var acts = new ActionQueue();
        turnScheduler = new TurnScheduler(AllyGroup, EnemyGroup, acts, battleState);
        battleStateManager = new BattleStateManager(battleState);
        actionContext = new BattleActionContext(
            AllyGroup,
            EnemyGroup,
            battleStateManager,
            turnScheduler,
            acts,
            targetingService,
            effectResolver);

        // QueryService を EffectResolver に設定
        effectResolver.SetQueryService(actionContext.QueryService);

        actionContext.ResetUnders();
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
        presentation = new BattlePresentation(uiBridge);
        stageEscapeRate = escapeRate;
        BattleContextHub.Set(this);
        this.metaProvider = metaProvider;
        escapeHandler = new EscapeHandler(this);
        skillExecutor = new SkillExecutor(this, actionContext, presentation);
        turnExecutor = new TurnExecutor(this, actionContext, presentation, uiBridge);
        characterActExecutor = new CharacterActExecutor(this);
        actionSkipExecutor = new ActionSkipExecutor(this);
        battleFlow = new BattleFlow(
            actionContext,
            presentation,
            uiBridge,
            turnExecutor,
            skillExecutor,
            escapeHandler,
            characterActExecutor,
            metaProvider);
        battleFlow.SetOnBattleEndCallback(OnBattleEnd);
        actionContext.SetBeVanguardHandler(BeVanguard);

        

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
    internal void ResetManagerTemp()
    {
        presentation.ResetTopMessage();
    }
    internal void SetUniqueTopMessage(string message)
    {
        presentation.SetTopMessage(message);
    }
    internal void ResetUnders()
    {
        actionContext.ResetUnders();
    }
    internal void PrepareRatherAct(List<BaseStates> targets, float damage)
    {
        actionContext.PrepareRatherAct(targets, damage);
        UnityEngine.Debug.Log("レイザーアクト");
    }
    internal void IncrementBattleTurnCount()
    {
        BattleTurnCount++;
    }
    /// <summary>
    /// 行動準備 次のボタンを決める
    /// </summary>
    public TabState ACTPop()
    {
        return turnExecutor.ACTPop();
    }


    /// <summary>
    /// 俳優の行動の分岐
    /// </summary>
    public async UniTask<TabState> CharacterActBranching()
    {
        return await characterActExecutor.CharacterActBranchingAsync();
    }

    /// <summary>
    /// パッシブによる発動率判定
    /// </summary>
    internal bool CheckPassivesSkillActivation() 
    {
        //if(Acter.PassivesSkillActivationRate() >= 100) return true;//別にこの行要らないか
        return rollper(Acter.PassivesSkillActivationRate());
    }
    internal TabState SkillStockACT()
    {
        return actionSkipExecutor.SkillStockACT();
    }
    internal TabState PassiveCancelACT()
    {
        return actionSkipExecutor.PassiveCancelACT();
    }
    internal TabState DoNothingACT()
    {
        return actionSkipExecutor.DoNothingACT();
    }
    /// <summary>
    /// 逃げるACT
    /// </summary>
    /// <returns></returns>
    public TabState EscapeACT()
    {
        return escapeHandler.EscapeACT();
    }
    /// <summary>
    /// 連鎖逃走
    /// </summary>
    public TabState DominoEscapeACT()
    {
        return escapeHandler.DominoEscapeACT();
    }
    /// <summary>
    /// レイザーダメージのACT
    /// </summary>
    /// <returns></returns>
    public TabState RatherACT()
    {
        return battleFlow.RatherAct();
    }
    /// <summary>
    /// メッセージと共に戦闘を終わらせる
    /// </summary>
    public TabState DialogEndACT()
    {
        if (Wipeout)
        {
            if (CurrentActerFaction == allyOrEnemy.alliy)
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
    private void CreateBattleMessage(string txt)
    {
        presentation.CreateBattleMessage(txt);
    }
    /// <summary>
    /// 発動カウントを実行
    /// </summary>
    internal TabState TriggerACT(int count)
    {
        return battleFlow.TriggerAct(count);
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
    internal async UniTask<TabState> SkillACT()
    {
        return await skillExecutor.SkillACT();
    }

    /// <summary>
    /// そのキャラが、敵味方問わずグループにおける前のめり状態かどうかを判別します。
    /// </summary>
    public bool IsVanguard(BaseStates chara) => actionContext.IsVanguard(chara);
    
    internal void NextTurn(bool Next)
    {
        turnExecutor.NextTurn(Next);
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
            // 早期returnでも必ずクリーンアップを実行
            CleanupBattleState();
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

        CleanupBattleState();
    }

    /// <summary>
    /// 戦闘終了時のクリーンアップ処理
    /// </summary>
    private void CleanupBattleState()
    {
        BattleContextHub.Clear(this);
        BattleUIBridge.SetActive(null);
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
