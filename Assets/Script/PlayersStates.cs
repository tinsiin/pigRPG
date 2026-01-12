using RandomExtensions;
using System;
using System.Collections.Generic;
using UnityEngine;
using static CommonCalc;
using Cysharp.Threading.Tasks;   // UniTask

/// <summary>
///セーブでセーブされるような事柄とかメインループで操作するためのステータス太刀　シングルトン
/// </summary>
[DefaultExecutionOrder(-800)]
public class PlayersStates : MonoBehaviour
{
    //staticなインスタンス
    public static PlayersStates Instance { get; private set; }

    private readonly PlayersProgressTracker progress = new PlayersProgressTracker();
    private readonly PlayersTuningConfig tuningConfig = new PlayersTuningConfig();
    private readonly PlayersRoster roster = new PlayersRoster();
    private PlayersUIService uiService;
    private PlayersUIFacade uiFacade;
    private PlayersUIEventRouter uiEventRouter;
    private PartyBuilder partyBuilder;
    private WalkLoopService walkLoopService;
    private PlayersBattleCallbacks battleCallbacks;
    private PlayersPartyService partyService;
    private SkillPassiveSelectionUI skillPassiveSelectionUI;
    private EmotionalAttachmentUI emotionalAttachmentUI;
    private PlayersUIRefs uiRefs;

    private PlayersUIRefs ResolveUIRefs()
    {
        if (uiRefs == null) uiRefs = GetComponent<PlayersUIRefs>();
        return uiRefs;
    }

    private void Awake()
    {
        if (Instance == null)//シングルトン
        {
            Instance = this;
            var refs = ResolveUIRefs();
            if (refs == null)
            {
                Debug.LogError("PlayersUIRefs not found on PlayersStates.");
                Instance = null;
                Destroy(gameObject);
                return;
            }
            refs.EnsureAllyUISets();

            skillPassiveSelectionUI = new SkillPassiveSelectionUI(refs.SelectSkillPassiveTargetHandle);
            emotionalAttachmentUI = new EmotionalAttachmentUI(roster, refs.EmotionalAttachmentSkillSelectUIArea);
            uiService = new PlayersUIService(
                roster,
                refs.AllyUISets,
                skillPassiveSelectionUI,
                emotionalAttachmentUI);
            uiFacade = new PlayersUIFacade();
            uiEventRouter = new PlayersUIEventRouter(uiFacade, uiService);
            partyBuilder = new PartyBuilder(roster, uiFacade);
            walkLoopService = new WalkLoopService(roster);
            battleCallbacks = new PlayersBattleCallbacks(roster);
            partyService = new PlayersPartyService(roster, partyBuilder, battleCallbacks, walkLoopService);
            PlayersStatesHub.Bind(progress, partyService, uiFacade, uiFacade, tuningConfig, roster);
            RefreshTuningConfig();
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

    }
    
    private void Start()
    {
        // Awake で全シングルトンの初期化が完了した後に初期化処理を行う
        Init();
    }

    private void OnDestroy()
    {
        if (ReferenceEquals(Instance, this))
        {
            uiEventRouter?.Unbind();
            PlayersStatesHub.ClearAll();
        }
    }
    /// <summary>
    /// ゲームの値や、主人公達のステータスの初期化
    /// </summary>
    public void Init()
    {
        Debug.Log("Init");

        CreateDecideValues();//中央決定値をゲーム開始時一回だけ生成

        NowProgress = 0;//ステージ関連のステータス初期化
        NowStageID = 0;
        NowAreaID = 0;

        //初期データをランタイム用にセット
        Allies = new AllyClass[] 
        {
            Init_geino.DeepCopy(), Init_noramlia.DeepCopy(), Init_sites.DeepCopy() 
        };
        foreach(var ally in Allies)
        {
            ally.OnInitializeSkillsAndChara();//スキル初期化
            ally.DecideDefaultMyImpression();//デフォルト精神属性の初回設定
        }

        ApplySkillButtons();//ボタンの結びつけ処理

        //現在の有効化リストIDの分だけスキルボタンを見えるようにする。
        UpdateSkillButtonVisibility();
 
       
    }
    public StairStates Init_geino;
    public BassJackStates Init_noramlia;
    public SateliteProcessStates Init_sites;
    public StairStates geino => Allies[(int)AllyId.Geino] as StairStates;
    public BassJackStates noramlia => Allies[(int)AllyId.Noramlia] as BassJackStates;
    public SateliteProcessStates sites => Allies[(int)AllyId.Sites] as SateliteProcessStates;
    //一旦リファクタリングの為にコメントアウト

    /// <summary>
    /// 0=Geino, 1=Noramlia, 2=Sites
    /// 処理のループ化のための配列ID列挙体
    /// </summary>
    public enum AllyId { Geino = 0, Noramlia = 1, Sites = 2 }
    /// <summary>
    /// 0=Geino, 1=Noramlia, 2=Sites
    /// 味方ランタイムデータの配列　処理のループ化のための
    /// </summary>
    private AllyClass[] Allies
    {
        get => roster.Allies;
        set => roster.SetAllies(value);
    }
    public int AllyCount => roster.AllyCount;

    // Inspector 表示用ヘッダ文言（コンパイル時定数）
    public const string AllyIndexHeader = "0: Geino, 1: Noramlia, 2: Sites (PlayersStates.AllyId 準拠)";

    public bool TryGetAllyIndex(BaseStates actor, out int index)
    {
        return roster.TryGetAllyIndex(actor, out index);
    }

    public bool TryGetAllyId(BaseStates actor, out AllyId id)
    {
        return roster.TryGetAllyId(actor, out id);
    }

    /// <summary>
    /// ボタンと全てのスキルを結びつける。
    /// </summary>
    void ApplySkillButtons()
    {
        uiService.BindSkillButtons();
    }
    /// <summary>
    /// スキル選択画面へ遷移する際のコールバック 引数無しのものをここで処理
    /// indexで指定されたキャラのみ　　
    /// </summary>
    public void OnSkillSelectionScreenTransition(int index) 
    {
        uiFacade.OnSkillSelectionScreenTransition(index);
    }

    

    
    /// <summary>
    ///「使用可能な」スキルのボタンのみを有効化しそれ以外を無効化するコールバック
    /// 指定したキャラのみ
    /// </summary>
    /// <param name="trait">範囲性質</param>
    /// <param name="type">スキル攻撃性質</param>
    /// <param name="OnlyCantACTPassiveCancel">キャンセル可能な行動可能パッシブを消せるかどうか</param>
    /// <param name="index">キャラクターのインデックス</param>
    public void OnlySelectActs(SkillZoneTrait trait,SkillType type,int index)
    {
        uiFacade.OnlySelectActs(trait, type, index);
    }
    /// <summary>
    /// スキルボタンの使いを有効化する処理　可視化
    /// 三人分行う
    /// </summary>
    void UpdateSkillButtonVisibility()
    {
        uiService.UpdateSkillButtonVisibility();
    }




    /// <summary>
    /// FreezeConsecutive 停止予約（UI非依存のビジネスロジック）
    /// 指定 index のキャラのみ対象
    /// </summary>
    public void RequestStopFreezeConsecutive(int index)
    {
        partyService.RequestStopFreezeConsecutive(index);
    }
    /// <summary>
    /// デフォルトのスキル選択のエリアからキャンセルパッシブのエリアまで進むボタン処理
    /// indexで指定したキャラのみ
    /// </summary>
    public void GoToCancelPassiveField(int index)
    {
        uiFacade.GoToCancelPassiveField(index);
    }
    /// <summary>
    /// キャンセルパッシブのエリアからデフォルトのスキル選択のエリアまで戻る
    /// indexで指定したキャラのみ
    /// </summary>
    public void ReturnCancelPassiveToDefaultArea(int index)
    {
        uiFacade.ReturnCancelPassiveToDefaultArea(index);
    }



    /// <summary>
    ///     現在進行度
    /// </summary>
    public int NowProgress
    {
        get => progress.NowProgress;
        private set => progress.SetProgress(value);
    }

    /// <summary>
    ///     現在のステージ
    /// </summary>
    public int NowStageID
    {
        get => progress.NowStageID;
        private set => progress.SetStage(value);
    }

    /// <summary>
    ///     現在のステージ内のエリア
    /// </summary>
    public int NowAreaID
    {
        get => progress.NowAreaID;
        private set => progress.SetArea(value);
    }
    /// <summary>
    ///     味方のキャラクター全員分のUIを表示するか非表示する
    /// </summary>
    public void AllyAlliesUISetActive(bool isActive)
    {
        uiFacade.AllyAlliesUISetActive(isActive);
    }

    public BattleGroup GetParty()
    {
        return partyService.GetParty();
    }




    /// <summary>
    ///     進行度を増やす
    /// </summary>
    /// <param name="addPoint"></param>
    public void AddProgress(int addPoint)
    {
        progress.AddProgress(addPoint);
    }

    /// <summary>
    ///     現在進行度をゼロにする
    /// </summary>
    public void ProgressReset()
    {
        progress.ResetProgress();
    }

    /// <summary>
    ///     エリアをセットする。
    /// </summary>
    public void SetArea(int id)
    {
        progress.SetArea(id);
        Debug.Log(id + "をPlayerStatesに記録");
    }
    /// <summary>
    /// 主人公達の勝利時のコールバック
    /// </summary>
    public void PlayersOnWin()
    {
        partyService.PlayersOnWin();
    }
    /// <summary>
    /// 主人公達の負けたときのコールバック
    /// </summary>
    public void PlayersOnLost()
    {
        partyService.PlayersOnLost();
    }
    /// <summary>
    /// 主人公達の逃げ出した時のコールバック
    /// </summary>
    public void PlayersOnRunOut()
    {
        partyService.PlayersOnRunOut();
    }

    public void PlayersOnWalks(int walkCount)
    {
        partyService.PlayersOnWalks(walkCount);
    }
    /// <summary>
    /// スキルパッシブ対象スキル選択画面へ行く
    /// </summary>
    public async UniTask<List<BaseSkill>> GoToSelectSkillPassiveTargetSkillButtonsArea
    (List<BaseSkill> skills, int selectCount)
    {
        return await uiFacade.GoToSelectSkillPassiveTargetSkillButtonsArea(skills, selectCount);
    }
    /// <summary>
    /// スキルパッシブ対象スキル選択画面から戻る
    /// </summary>
    public void ReturnSelectSkillPassiveTargetSkillButtonsArea()
    {
        uiFacade.ReturnSelectSkillPassiveTargetSkillButtonsArea();
    }
    [Header("思い入れスキル弱体化用スキルパッシブ")]
    /// <summary>
    /// 思い入れスキル弱体化用スキルパッシブ
    /// </summary>
    public BaseSkillPassive EmotionalAttachmentSkillWeakeningPassive;
    /// <summary>
    /// 思い入れスキル選択UI表示ボタン
    /// indexでキャラ指定
    /// </summary>
    public void OpenEmotionalAttachmentSkillSelectUIArea(int index)
    {
        uiFacade.OpenEmotionalAttachmentSkillSelectUIArea(index);
    }
    

    /// <summary>
    /// バトルスタート時のUI管理
    /// </summary>
    public void OnBattleStart()
    {
        uiFacade.OnBattleStart();
    }


    //中央決定値など---------------------------------------------------------中央決定値
    /// <summary>
    /// 中央決定値　空洞爆発の値　割り込みカウンター用
    /// </summary>
    void CreateDecideValues()
    {
        RefreshTuningConfig();
        tuningConfig.SetExplosionVoid(RandomEx.Shared.NextFloat(10,61));
    }
    public int HP_TO_MaxP_CONVERSION_FACTOR = 80;
    public int MentalHP_TO_P_Recovely_CONVERSION_FACTOR = 120;

    private void RefreshTuningConfig()
    {
        tuningConfig.Initialize(
            HP_TO_MaxP_CONVERSION_FACTOR,
            MentalHP_TO_P_Recovely_CONVERSION_FACTOR,
            EmotionalAttachmentSkillWeakeningPassive
        );
    }

    public float ExplosionVoidValue => tuningConfig.ExplosionVoid;
    public int HpToMaxPConversionFactor => tuningConfig.HpToMaxPConversionFactor;
    public int MentalHpToPRecoveryConversionFactor => tuningConfig.MentalHpToPRecoveryConversionFactor;
    public BaseSkillPassive EmotionalAttachmentSkillWeakeningPassiveRef => tuningConfig.EmotionalAttachmentSkillWeakeningPassive;

    public BaseStates GetAllyByIndex(int index)
    {
        return roster.GetAllyByIndex(index);
    }

}
