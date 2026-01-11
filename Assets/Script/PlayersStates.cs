using RandomExtensions;
using RandomExtensions.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using static CommonCalc;
using Cysharp.Threading.Tasks;   // UniTask
    [Serializable]
    public class ButtonAndSkillIDHold
    {
        public Button button;
        public int skillID;
        public void AddButtonFunc(UnityAction<int> call)
        {
            Debug.Log("AddButtonFunc" + skillID);
            button.onClick.AddListener(() => call(skillID));
        }
    }
    /// <summary>
    /// スキルIDが必要なラジオボタン処理用のコントローラー
    /// スキルIDなどが必要のない、例えば「キャラ自体の設定用」などは直接ToggleGroupControllerを使う。
    /// </summary>
    [Serializable]
    public class RadioButtonsAndSkillIDHold
    {
        public ToggleGroupController Controller;
        public int skillID;
        
        // UnityAction<int, int>に変更 - 第1引数：どのトグルが選ばれたか、第2引数：skillID
        public void AddRadioFunc(UnityAction<int, int> call)
        {
            // nullチェック
            if (Controller == null)
            {
                Debug.LogError("toggleGroupがnullです！ skillID: " + skillID);
            }
            
            if (call == null)
            {
                Debug.LogError("callがnullです！ skillID: " + skillID);
            }
            // 両方の情報を渡す
            Controller.AddListener((int toggleIndex) => call(toggleIndex, skillID));
        }

        public void Interactable(bool interactable)
        {
            Controller.interactable = interactable;
        }
    }
    /// <summary>
    /// 主人公キャラ達のスキルボタンなどのUIリスト
    /// </summary>
    [Serializable]
    public class AllySkillUILists
    {
        [Header("スキルボタンリスト")]
        /// <summary>
        /// スキルボタンリスト
        /// </summary>
        public List<ButtonAndSkillIDHold> skillButtons = new();
        [Header("ストックボタンリスト")]
        /// <summary>
        /// ストックボタンリスト
        /// </summary>
        public List<ButtonAndSkillIDHold> stockButtons = new();
        [Header("前のめり選択が可能なスキル用に選択できるラジオボタン用リスト")]
        /// <summary>
        /// 前のめり選択ラジオリスト
        /// </summary>
        public List<RadioButtonsAndSkillIDHold> aggressiveCommitRadios = new();
    }

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
    private PartyBuilder partyBuilder;
    private WalkLoopService walkLoopService;
    private PlayersBattleCallbacks battleCallbacks;
    private PlayersPartyService partyService;
    private SkillPassiveSelectionUI skillPassiveSelectionUI;
    private EmotionalAttachmentUI emotionalAttachmentUI;

    public GameObject EyeArea;
    public ActionMarkUI ActionMar;

    private void Awake()
    {
        if (Instance == null)//シングルトン
        {
            Instance = this;
            skillPassiveSelectionUI = new SkillPassiveSelectionUI(SelectSkillPassiveTargetHandle);
            emotionalAttachmentUI = new EmotionalAttachmentUI(roster, EmotionalAttachmentSkillSelectUIArea);
            uiService = new PlayersUIService(
                roster,
                skillUILists,
                DefaultButtonArea,
                DoNothingButton,
                CancelPassiveButtonField,
                skillPassiveSelectionUI,
                emotionalAttachmentUI);
            partyBuilder = new PartyBuilder(roster, uiService);
            walkLoopService = new WalkLoopService(roster);
            battleCallbacks = new PlayersBattleCallbacks(roster);
            partyService = new PlayersPartyService(roster, partyBuilder, battleCallbacks, walkLoopService);
            PlayersStatesHub.Bind(progress, partyService, uiService, uiService, tuningConfig, roster);
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
    [Header(AllyIndexHeader)]
    [SerializeField]
    ///複数存在するスキル用のUIリスト
    private AllySkillUILists[] skillUILists = new AllySkillUILists[3];

    /// <summary>
    /// スキル選択画面へ遷移する際のコールバック 引数無しのものをここで処理
    /// indexで指定されたキャラのみ　　
    /// </summary>
    public void OnSkillSelectionScreenTransition(int index) 
    {
        uiService.OnSkillSelectionScreenTransition(index);
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
        uiService.OnlySelectActs(trait, type, index);
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
    [Header("スキル選択画面のデフォルトのエリア")]
    /// <summary>
    /// スキル選択画面のデフォルトのエリア
    /// </summary>
    [SerializeField] GameObject[] DefaultButtonArea = new GameObject[3];
    [Header("何もしないボタン")]
    /// <summary>
    /// 何もしないボタン
    /// </summary>
    [SerializeField] Button[] DoNothingButton = new Button[3];
    [Header("パッシブをキャンセルするボタンのエリア")]
    /// <summary>
    /// パッシブをキャンセルするボタンのエリア
    /// </summary>
    [SerializeField] SelectCancelPassiveButtons[] CancelPassiveButtonField = new SelectCancelPassiveButtons[3];
    [Header("スキル選択デフォルト画面からパッシブキャンセルエリアへ進むボタン")]
    /// <summary>
    /// スキル選択デフォルト画面からパッシブキャンセルエリアへ進むボタン
    /// </summary>
    [SerializeField] Button[] GoToCancelPassiveFieldButton = new Button[3];
    [Header("パッシブをキャンセルするエリアからデフォルトのスキル選択のエリアまで戻るボタン")]
    /// <summary>
    /// パッシブをキャンセルするエリアからデフォルトのスキル選択のエリアまで戻るボタン
    /// </summary>
    [SerializeField] Button[] ReturnCancelPassiveToDefaultAreaButton = new Button[3];
    /// <summary>
    /// デフォルトのスキル選択のエリアからキャンセルパッシブのエリアまで進むボタン処理
    /// indexで指定したキャラのみ
    /// </summary>
    public void GoToCancelPassiveField(int index)
    {
        uiService.GoToCancelPassiveField(index);
    }
    /// <summary>
    /// キャンセルパッシブのエリアからデフォルトのスキル選択のエリアまで戻る
    /// indexで指定したキャラのみ
    /// </summary>
    public void ReturnCancelPassiveToDefaultArea(int index)
    {
        uiService.ReturnCancelPassiveToDefaultArea(index);
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
        uiService.AllyAlliesUISetActive(isActive);
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
    [Header("モーダルエリア")]
    /// <summary>
    /// モーダルエリア
    /// </summary>
    public GameObject ModalArea;

    [Header("スキルパッシブ対象スキル選択ボタン管理エリア")]
    /// <summary>
    /// スキルパッシブ対象スキル選択ボタン管理エリア
    /// </summary>
    [SerializeField]SelectSkillPassiveTargetSkillButtons SelectSkillPassiveTargetHandle;
    /// <summary>
    /// スキルパッシブ対象スキル選択画面へ行く
    /// </summary>
    public async UniTask<List<BaseSkill>> GoToSelectSkillPassiveTargetSkillButtonsArea
    (List<BaseSkill> skills, int selectCount)
    {
        return await uiService.GoToSelectSkillPassiveTargetSkillButtonsArea(skills, selectCount);
    }
    /// <summary>
    /// スキルパッシブ対象スキル選択画面から戻る
    /// </summary>
    public void ReturnSelectSkillPassiveTargetSkillButtonsArea()
    {
        uiService.ReturnSelectSkillPassiveTargetSkillButtonsArea();
    }
    [Header("思い入れスキル選択UI")]
    /// <summary>
    /// 思い入れスキル選択UI
    /// </summary>
    [SerializeField]SelectEmotionalAttachmentSkillButtons EmotionalAttachmentSkillSelectUIArea;
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
        uiService.OpenEmotionalAttachmentSkillSelectUIArea(index);
    }
    

    /// <summary>
    /// バトルスタート時のUI管理
    /// </summary>
    public void OnBattleStart()
    {
        uiService.OnBattleStart();
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
[Serializable]
public class AllyClass : BaseStates
{
    /// <summary>
    /// 主人公キャラはUIコントローラーを直接参照
    /// </summary>
    [SerializeField] UIController _uic;


    /// <summary>
    /// 主人公達の全所持スキルリスト
    /// </summary>
    [SerializeReference,SelectableSerializeReference]
    List<AllySkill> _skillALLList = new();
    /// <summary>
    /// IDでスキルを取得する
    /// </summary>
    AllySkill SkillByID(int ID)
    {
        return _skillALLList.Find(skill => skill.ID == ID);
    }

    /// <summary>
    /// 有効なスキルリスト
    /// </summary>
    public List<int> ValidSkillIDList = new();
    public override IReadOnlyList<BaseSkill> SkillList => _skillALLList.Where(skill => ValidSkillIDList.Contains(skill.ID)).ToList();

    public override void OnInitializeSkillsAndChara()
    {
        foreach (var skill in _skillALLList)
        {
            skill.OnInitialize(this);
        }
    }
    /// <summary>
    /// 現在のTLOAの思い入れスキルID
    /// </summary>
    public int EmotionalAttachmentSkillID = 0;
    /// <summary>
    /// 現在のTLOAの思い入れスキル
    /// </summary>
    public AllySkill EmotionalAttachmentSkill => SkillByID(EmotionalAttachmentSkillID);
    /// <summary>
    /// 思い入れスキルの思い入れ量 (0~800)
    /// </summary>
    private float _emotionalAttachmentSkillQuantity = 0;
    /// <summary>
    /// 思い入れスキルの思い入れ量を取得・設定する
    /// </summary>
    public float EmotionalAttachmentSkillQuantity 
    { 
        get => _emotionalAttachmentSkillQuantity;
        set => _emotionalAttachmentSkillQuantity = Mathf.Clamp(value, 0, 800);
    }

    /// <summary>
    /// 思い入れスキルIDを変更する関数
    /// 思い入れスキル変更ボタンUIに渡す用のコールバック
    /// </summary>
    public void OnEmotionalAttachmentSkillIDChange(int NewSkillID)
    {
        var oldSkill = SkillByID(EmotionalAttachmentSkillID);
        var newSkill = SkillByID(NewSkillID);//まず入れ替えの前に

        EmotionalAttachmentSkillID = NewSkillID;//ID変更
        Debug.Log(NewSkillID + " :思い入れスキルIDを記録");

        //思い入れ量の弱体処理
        EmotionalAttachmentSkillQuantityChangeWeakening(oldSkill, newSkill);
        EmotionalAttachmentSkillQuantityChangeSkillWeakening(oldSkill);
    }
    /// <summary>
    /// 思い入れ量から熟練度倍率を計算 (1.2~12倍)
    /// </summary>
    /// <returns>熟練度倍率 (1.2~12)</returns>
    public float GetProficiencyMultiplierFromQuantity()
    {
        // 0~800を0~1に正規化
        float normalizedQuantity = Mathf.Clamp01(EmotionalAttachmentSkillQuantity / 800f);
        
        // 1.2~12の範囲にスケーリング
        return Mathf.Lerp(1.2f, 12f, normalizedQuantity);
    }
    /// <summary>
    /// 思い入れ量から現在HP固定加算倍率を計算 (0.01~0.3)
    /// </summary>
    /// <returns>現在HP固定加算倍率 (0.01~0.3)</returns>
    public float GetCurrentHPFixedAdditionMultiplier()
    {
        // 0~800を0~1に正規化
        float normalizedQuantity = Mathf.Clamp01(EmotionalAttachmentSkillQuantity / 800f);
        
        // 0.01~0.3の範囲にスケーリング
        return Mathf.Lerp(0.01f, 0.3f, normalizedQuantity);
    }
    /// <summary>
    /// 思い入れ量の弱体処理
    /// </summary>
    void EmotionalAttachmentSkillQuantityChangeWeakening(AllySkill oldSkill, AllySkill newSkill)
    {
        if(oldSkill.MotionFlavor == newSkill.MotionFlavor)
        {
            EmotionalAttachmentSkillQuantity *= 0.7f;//動作的雰囲気が一致してたら0.7倍
        }else
        {
            EmotionalAttachmentSkillQuantity = 0;//それ以外は0で最初から
        }
    }
    /// <summary>
    /// 思い入れスキルが変更された際、前に選ばれてたスキルに弱体化スキルパッシブを付与する
    /// 専用付与関数で二つ重複しない
    /// </summary>
    void EmotionalAttachmentSkillQuantityChangeSkillWeakening(AllySkill oldSkill)
    {
        var tuning = PlayersStatesHub.Tuning;
        if (tuning == null || tuning.EmotionalAttachmentSkillWeakeningPassiveRef == null)
        {
            Debug.LogError("EmotionalAttachmentSkillQuantityChangeSkillWeakening: Tuning が未設定です");
            return;
        }
        oldSkill.ApplyEmotionalAttachmentSkillQuantityChangeSkillWeakeningPassive//弱体化スキルパッシブ専用の付与関数
        (tuning.EmotionalAttachmentSkillWeakeningPassiveRef);
    }
    
    /// <summary>
    /// キャラクターのデフォルト精神属性を決定する関数　十日能力が変動するたびに決まる。
    /// </summary>
    public void DecideDefaultMyImpression()
    {
        //1~4の範囲で合致しきい値が決まる。
        var Threshold = RandomEx.Shared.NextInt(1,5);
        Debug.Log($"{CharacterName}の今回のデフォルト精神属性の合致しきい値:{Threshold}");

        //キャラクターの持つ十日能力を多い順に重み付き抽選リストに入れ、処理をする。
        var AbilityList = new WeightedList<TenDayAbility>();
        //linqで値の多い順にゲット
        foreach(var ability in TenDayValues(false).OrderByDescending(x => x.Value))
        {
            // 重みは整数として扱う前提のため、小数は切り上げし、0はスキップ
            var weight = Mathf.CeilToInt(ability.Value);
            if (weight > 0)
            {
                AbilityList.Add(ability.Key, weight);//キーが十日能力の列挙体　重みの部分に能力の値が入る。
            }
        }
        //Debug.Log($"[DecideDefault] {CharacterName} 重み>0 の十日能力数:{AbilityList.Count}");

        var SpiritualMatchCounts = new Dictionary<SpiritualProperty,int>()//一時保存用データ
        {
            {SpiritualProperty.doremis, 0},
            {SpiritualProperty.pillar, 0},
            {SpiritualProperty.kindergarden, 0},
            {SpiritualProperty.liminalwhitetile, 0},
            {SpiritualProperty.sacrifaith, 0},
            {SpiritualProperty.cquiest, 0},
            {SpiritualProperty.pysco, 0},
            {SpiritualProperty.godtier, 0},
            {SpiritualProperty.baledrival, 0},
            {SpiritualProperty.devil, 0}
        };
        TenDayAbility selectedAbility;//重み付き抽選リストから抜き出す"恐らく多い順に"出てくるであろうキャラクターの十日能力変数
        
        //ここからwhileループ
        var loopCount = 0;
        while(true)
        {
            if(AbilityList.Count <= 0)//十日能力の重み付き抽選リストが空　つまり十日能力がないなら
            {
                DefaultImpression = SpiritualProperty.none;
                Debug.Log($"{CharacterName}のDefaultImpressionが決定:{DefaultImpression}\n(十日能力がないため-DecideDefaultMyImpression)");
                if(loopCount>0)Debug.Log($"十日能力がないわけではなく、尽きるまでにデフォルト精神属性が決まり切らなかったため、noneとなった。\n(DecideDefaultMyImpression)");
                break;
            }
            AbilityList.RemoveRandom(out selectedAbility);//比較用能力値変数に重み付き抽選リストから消しながら抜き出し

            


            foreach(var map in SpritualTenDayAbilitysMap)
            {
                //現在回してる互換表の、「十日能力値の必要合致リスト」の添え字に一時保存している各精神属性の合致数を渡し、必要な十日能力を抜き出す。
                //順序による厳密一致ではなく、「含まれているか」で合致を判定する。合致リストの能力と今回の多い順から数えた能力値が合ってるかを比較。
                if (map.Value.Contains(selectedAbility))
                {
                    SpiritualMatchCounts[map.Key]++; // 合致したら合致数を1増やす
                }
            }

            //合致しきい値を超えた精神属性があるかどうかを確認する。
            //あるならその精神属性のデータをリストにまとめる。
            
            var SpOkList = new List<SpiritualProperty>();

            foreach(var sp in SpiritualMatchCounts)
            {
                if(sp.Value >= Threshold)
                {
                    SpOkList.Add(sp.Key);//超えている精神属性を記録。
                }
            }

            if(SpOkList.Count > 0)
            {
                //複数ダブっても、どれか一つをランダムで選ぶ
                DefaultImpression = RandomEx.Shared.GetItem(SpOkList.ToArray());
                Debug.Log($"{CharacterName}の今回のデフォルト精神属性が決定:{DefaultImpression}");
                break;
            }
            loopCount++;
        }
        

    }
    
    /// <summary>
    /// 隙だらけ補正
    /// 攻撃相手のターゲット率を引数に入れ、それに自分の十日能力補正を掛ける形で命中パーセンテージ補正を算出する
    /// 0未満なら使われない。
    /// </summary>
    public float GetExposureAccuracyPercentageBonus(float EneTargetProbability)
    {
        if(NowPower == ThePower.lowlow)return -1f;//パワーがたるい　だとそもそも発生しない。

        //ジョー歯÷4による基礎能力係数
        var BaseCoefficient = TenDayValues(false).GetValueOrZero(TenDayAbility.JoeTeeth) / 4;
        //馬鹿と烈火の乗算　された補正要素
        var BakaBlazeFireCoef = TenDayValues(false).GetValueOrZero(TenDayAbility.Baka) * 
        TenDayValues(false).GetValueOrZero(TenDayAbility.BlazingFire) / 30;
        //レインコートによる補正要素
        var RaincoatCoef = TenDayValues(false).GetValueOrZero(TenDayAbility.Raincoat) / 20;

        //レインコートと馬鹿烈火補正はパワーによって分岐
        switch(NowPower)
        {
            case ThePower.low://低いとなし
                BakaBlazeFireCoef = 0;
                RaincoatCoef = 0;
                break;
            case ThePower.medium://普通なら0.5倍
                BakaBlazeFireCoef *= 0.5f;
                RaincoatCoef *= 0.5f;
                break;
        }

        var finalTenDaysCoef = BaseCoefficient ;//最終的な十日能力補正にまず基礎の係数を

        //精神属性で分岐する
        switch(MyImpression)
        {
            case SpiritualProperty.pysco://サイコパス、キンダー、リーミナルホワイトはレインコート
            case SpiritualProperty.liminalwhitetile:
            case SpiritualProperty.kindergarden:
                finalTenDaysCoef += RaincoatCoef;
                break;
            case SpiritualProperty.doremis://ドレミスは二つとも
                finalTenDaysCoef += BakaBlazeFireCoef + RaincoatCoef;
                break;
            case SpiritualProperty.pillar:
            case SpiritualProperty.none:
                //加算なし
                break;
            default:
                //それ以外の精神属性は馬鹿烈火補正
                finalTenDaysCoef += BakaBlazeFireCoef;
                break;
        }

        //最終的な十日能力補正をターゲット率÷10 と掛ける　
        var rawModifier = finalTenDaysCoef * (EneTargetProbability / 10) / 10;

        //正か負かによって　1.〇倍か、-1.〇倍かにする
        //eyeModifier = eyeModifier > 0 ? 1 + eyeModifier :  eyeModifier - 1;
        //return eyeModifier * 0.67f;

        // 累乗 0.5（平方根）で増加に減衰をかけ、最低１倍を保証
        var finalModifier = Mathf.Max(1f, Mathf.Pow(rawModifier, 0.5f));
        return finalModifier;

    }
    
    /// <summary>
    /// スキルボタンからそのスキルの範囲や対象者の画面に移る
    /// </summary>
    /// <param name="skillListIndex"></param>
    public void OnSkillBtnCallBack(int skillListIndex)
    {
        var skill = SkillList[skillListIndex];
        var orchestrator = BattleOrchestratorHub.Current;
        if (orchestrator != null)
        {
            var input = new ActionInput
            {
                Kind = ActionInputKind.SkillSelect,
                RequestId = orchestrator.CurrentChoiceRequest.RequestId,
                Actor = this,
                Skill = skill
            };
            var state = orchestrator.ApplyInput(input);
            var uiBridge = BattleUIBridge.Active;
            if (uiBridge != null)
            {
                uiBridge.SetUserUiState(state, false);
            }
            else
            {
                Debug.LogError("PlayersStates.OnSkillBtnCallBack: BattleUIBridge が null です");
            }
            return;
        }

        SKillUseCall(skill);//スキル使用

        //もし先約リストによる単体指定ならば、範囲や対象者選択画面にはいかず、直接actbranchiへ移行
        //スキルの性質によるボタンの行く先の分岐
        var nextState = manager.Acts.GetAtSingleTarget(0) != null
            ? TabState.NextWait
            : DetermineNextUIState(NowUseSkill);

        var fallbackBridge = BattleUIBridge.Active;
        if (fallbackBridge != null)
        {
            fallbackBridge.SetUserUiState(nextState);
        }
        else
        {
            Debug.LogError("PlayersStates.OnSkillBtnCallBack: BattleUIBridge が null です");
        }
    }
    /// <summary>
    /// スキル攻撃回数ストックボタンからはそのまま次のターンへ移行する(対象者選択や範囲選択などはない。)
    /// </summary>
    /// <param name="skillListIndex"></param>
    public void OnSkillStockBtnCallBack(int skillListIndex)
    {
        var skill = SkillList[skillListIndex];
        var orchestrator = BattleOrchestratorHub.Current;
        if (orchestrator != null)
        {
            var input = new ActionInput
            {
                Kind = ActionInputKind.StockSkill,
                RequestId = orchestrator.CurrentChoiceRequest.RequestId,
                Actor = this,
                Skill = skill
            };
            var state = orchestrator.ApplyInput(input);
            var inputBridge = BattleUIBridge.Active;
            if (inputBridge != null)
            {
                inputBridge.SetUserUiState(state, false);
            }
            else
            {
                Debug.LogError("PlayersStates.OnSkillStockBtnCallBack: BattleUIBridge が null です");
            }
            return;
        }
        if(skill.IsFullStock())
        {
            Debug.Log(skill.SkillName + "をストックが満杯。");
            return;//ストックが満杯なら何もしない
        } 
        skill.ATKCountStock();;//該当のスキルをストックする。
        Debug.Log(skill.SkillName + "をストックしました。");

        
        
        //今回選んだストックスキル以外のストックが減る。
        var list = SkillList.Where((skill,index) => index != skillListIndex && skill.HasConsecutiveType(SkillConsecutiveType.Stockpile)).ToList();
        foreach(var stockSkill in list)
        {
            stockSkill.ForgetStock();
        }

        var uiBridge = BattleUIBridge.Active;
        var battle = uiBridge?.BattleContext;
        if (battle != null)
        {
            battle.SkillStock = true;//ACTBranchingでストックboolをtrueに。
        }

        if (uiBridge != null)
        {
            uiBridge.SetUserUiState(TabState.NextWait);//CharacterACTBranchingへ
        }
        else
        {
            Debug.LogError("PlayersStates.OnSkillStockBtnCallBack: BattleUIBridge が null です");
        }
        
    }

    /// <summary>
    /// 前のめりを選択できるスキルで選択したときのコールバック関数
    /// </summary>
    public void OnSkillSelectAgressiveCommitBtnCallBack(int toggleIndex, int skillID)
    {
        bool isAgrresiveCommit;
        var skill = SkillList[skillID];
        
        if(toggleIndex == 0) 
        {
            isAgrresiveCommit = true;
            Debug.Log("前のめりして攻撃する" );
        }
        else
        {
            isAgrresiveCommit = false;
            Debug.Log("そのままの位置から攻撃" );
        }
        skill.IsAggressiveCommit = isAgrresiveCommit;//スキルの前のめり性に代入すべ
    }
    /// <summary>
    /// UI処理用の割り込みカウンターのオンオフのbool
    /// </summary>
    bool IsInterruptCounterActive_UI = true;
    /// <summary>
    /// AllyClassの割り込みカウンターのオンオフのbool
    /// UI処理boolを受け取るために継承
    /// </summary>
    public override bool IsInterruptCounterActive => IsInterruptCounterActive_UI;
    /// <summary>
    /// キャラの標準のロジックの割り込みカウンターを行うかどうかのラジオボタン選択時のコールバック関数
    /// </summary>
    /// <param name="toggleIndex"></param>
    public void OnSelectInterruptCounterActiveBtnCallBack(int toggleIndex)
    {
        //割り込みカウンターをする
        if(toggleIndex == 0)
        {
            IsInterruptCounterActive_UI = true;
        }//しない
        else
        {
            IsInterruptCounterActive_UI = false;
        }
    }    
    /// <summary>
    /// 直接ボタンを押して、何もしないを選ぶボタンのコールバック
    /// </summary>
    public void OnSkillDoNothingBtnCallBack()
    {
        Debug.Log("何もしない");
        var orchestrator = BattleOrchestratorHub.Current;
        if (orchestrator != null)
        {
            var input = new ActionInput
            {
                Kind = ActionInputKind.DoNothing,
                RequestId = orchestrator.CurrentChoiceRequest.RequestId,
                Actor = this
            };
            var state = orchestrator.ApplyInput(input);
            var inputBridge = BattleUIBridge.Active;
            if (inputBridge != null)
            {
                inputBridge.SetUserUiState(state, false);
            }
            else
            {
                Debug.LogError("PlayersStates.OnSkillDoNothingBtnCallBack: BattleUIBridge が null です");
            }
            return;
        }
        var uiBridge = BattleUIBridge.Active;
        var battle = uiBridge?.BattleContext;
        if (battle != null)
        {
            battle.DoNothing = true;//ACTBranchingで何もしないようにするboolをtrueに。
        }

        if (uiBridge != null)
        {
            uiBridge.SetUserUiState(TabState.NextWait);//CharacterACTBranchingへ
        }
        else
        {
            Debug.LogError("PlayersStates.OnSkillDoNothingBtnCallBack: BattleUIBridge が null です");
        }
    }
    /// <summary>
    /// スキルの性質に基づいて、次に遷移すべき画面状態を判定する
    /// </summary>
    /// <param name="skill">判定対象のスキル</param>
    /// <returns>遷移先のTabState</returns>
    public static TabState DetermineNextUIState(BaseSkill skill)
    {
        //var acter = Walking.Instance.BattleContext?.Acter;

        //範囲を選べるのなら　　 (自分だけのスキルなら範囲選択の性質があってもできない、本来できないもの)
        if (skill.HasZoneTrait(SkillZoneTrait.CanSelectRange) && !skill.HasZoneTrait(SkillZoneTrait.SelfSkill))
        {
            return TabState.SelectRange;//範囲選択画面へ飛ぶ
        }
        else if ((skill.HasZoneTrait(SkillZoneTrait.CanPerfectSelectSingleTarget) || 
                skill.HasZoneTrait(SkillZoneTrait.CanSelectSingleTarget) || 
                skill.HasZoneTrait(SkillZoneTrait.CanSelectMultiTarget))&& !skill.HasZoneTrait(SkillZoneTrait.SelfSkill))
        {//選択できる系なら (自分だけのスキルなら範囲選択の性質があってもできない、本来なら範囲性質に含めてないはず)
            return TabState.SelectTarget;//選択画面へ飛ぶ
        }
        else if (skill.HasZoneTrait(SkillZoneTrait.ControlByThisSituation))
        {
            //~~実行意志ではないので、RangeWillに入れない。~~
            //普通にSelectTargetWillの直前で範囲意志に入ります。
            return TabState.NextWait;//何もないなら事象ボタンへ
        }

        Debug.Log("範囲選択も対象者選択も起こらないControlByThisSituation以外のスキル性質: " + skill.ZoneTrait);
        //acter.RangeWill = skill.ZoneTrait;//実行者の範囲意志にそのままスキルの範囲性質を入れる。
        return TabState.NextWait; // デフォルトの遷移先
    }

    // ================================
    // 歩行ストリーク（1歩=+1）。OnWalkCallBackは1歩単位の契約。
    // ================================
    private int _walkCounter = 0;

    /// <summary>
    /// 歩行ストリークのリセット。
    /// </summary>
    public void ResetWalkCounter()
    {
        _walkCounter = 0;
        _walkPointRecoveryCounter = 0;//歩行のポイント回復用カウンターをゼロに
        _walkCountForTransitionToDefaultImpression = 0;//歩行の精神属性変化用カウンターをゼロに
    }


    /// <summary>
    /// 2歩ごとに回復するポイントカウンター
    /// </summary>
    private int _walkPointRecoveryCounter = 0;

    
    public override void OnBattleEndNoArgument()
    {
        base.OnBattleEndNoArgument();
        ResetWalkCounter();
    }
    public override void OnBattleStartNoArgument()
    {
        base.OnBattleStartNoArgument();
        
    }
     /// <summary>
    /// 歩行時にポイントを回復する処理
    /// この回復は外でスキルを使用する際の制限を表現する物。
    /// </summary>
    void RecoverPointOnWalk()
    {
        // 2歩ごとに処理
        _walkPointRecoveryCounter++;
        if (_walkPointRecoveryCounter >= 2)
        {
            _walkPointRecoveryCounter = 0;
            
            // 精神HPがマックスであることを前提に回復
            if (MentalHP >= MentalMaxHP)
            {
                // ポイント回復（回復量は調整可能）
                MentalNaturalRecovelyPont();
                
            }
        }
    }
    /// <summary>
    /// 歩行時に精神HPを回復する
    /// </summary>
    void RecoverMentalHPOnWalk()
    {
        if(MentalHP < MentalMaxHP)
        {
            MentalHP += TenDayValues(false).GetValueOrZero(TenDayAbility.Rain) + MentalMaxHP * 0.16f;
        }
        //ポイント回復用で結局は戦闘開始時にmaxになるんだし、こんぐらいの割合で丁度いいと思う
    }
    const int FULL_NEEDED_TRANSITION_TODEFAULTIMPREEION_WALK_COUNT = 12;
    /// <summary>
    /// 歩行時に精神属性をデフォルトに戻す用歩行カウンター変数
    /// </summary>
    int _walkCountForTransitionToDefaultImpression = 0;
    /// <summary>
    /// 精神属性は歩くとデフォルト精神属性に戻っていく処理。
    /// </summary>
    void ImpressionToDefaultTransition()
    {
        //既にデフォルト精神属性なら戻らない
        if(MyImpression == DefaultImpression) return;

        //思えの値の割合
        var ratio = NowResonanceValue / ResonanceValue;
        //必要歩数
        var neededWalkCount = (1-ratio) * FULL_NEEDED_TRANSITION_TODEFAULTIMPREEION_WALK_COUNT;
        //思えの値が削れてる = 思ってるほど、戻りにくい　= 必要歩数が増える

        _walkCountForTransitionToDefaultImpression++;//一歩進んだ
        if(_walkCountForTransitionToDefaultImpression >= neededWalkCount)
        {
            MyImpression = DefaultImpression;
            _walkCountForTransitionToDefaultImpression = 0;
        }
    }


    /// <summary>
    /// 味方キャラの歩く際に呼び出されるコールバック（1歩＝1回の契約）。
    /// 複数歩を処理したい場合は呼び出し側（PlayersOnWalks）で回数分呼ぶこと。
    /// </summary>
    public void OnWalkStepCallBack()
    {
        // 常に1歩分として扱う（契約の一貫性のため）。
        _walkCounter++;
        Debug.Log("歩数: " + _walkCounter);

        // 1. パッシブ/生存/状態遷移（逐次）
        AllPassiveWalkEffect();//全パッシブの歩行効果を呼ぶ
        UpdateWalkAllPassiveSurvival();
        UpdateAllSkillPassiveWalkSurvival();//スキルパッシブの歩行残存処理
        TransitionPowerOnWalkByCharacterImpression();

        // 2. 回復・共鳴（逐次）
        RecoverMentalHPOnWalk();//歩行時精神HP回復
        RecoverPointOnWalk();//歩行時ポイント回復　味方のみ（周期化するなら内部で閾値判定に変更）
        ResonanceHealingOnWalking();//歩行時思えの値回復

        // 3. 属性ポイントの歩行減衰（確率が歩数依存のため逐次）
        ApplyWalkingAttrPDecayStep(_walkCounter);

        // 4. ブーストのフェード（増分1歩）
        FadeConfidenceBoostByWalking();//歩行によって自信ブーストがフェードアウトする

        // 5. 精神属性をデフォルトへ戻す（逐次）
        ImpressionToDefaultTransition();//歩行によって精神属性がデフォルトに戻っていく

    }
    public void OnAllyWinCallBack()
    {
        TransitionPowerOnBattleWinByCharacterImpression();//パワー変化
        HP += MaxHP * 0.3f;//HPの自然回復
        AllyVictoryBoost();//勝利時の十日能力ブースト
        ResolveDivergentSkillOutcome();//乖離スキル使用により、十日能力値減少
    }
    public void OnAllyLostCallBack()
    {
        //敗北時パワー変化
        TransitionPowerOnBattleLostByCharacterImpression();
    }
    public void OnAllyRunOutCallBack()
    {
        //主人公達が逃げ出した時のパワー変化
        TransitionPowerOnBattleRunOutByCharacterImpression();
    }
    /// <summary>
    /// 勝利時の十日能力ブースト倍化処理
    /// </summary>
    public void AllyVictoryBoost()
    {
        //まず主人公グループと敵グループの強さの倍率
        var battle = BattleUIBridge.Active?.BattleContext;
        if (battle == null) return;
        var ratio = battle.EnemyGroup.OurTenDayPowerSum(false) / battle.AllyGroup.OurTenDayPowerSum(false);
        VictoryBoost(ratio);

    }

    

    /// <summary>
    /// キャラクターのパワーが勝利時にどう変化するか
    /// </summary>
    void TransitionPowerOnBattleWinByCharacterImpression()
    {
        switch(MyImpression)
        {
            case SpiritualProperty.doremis:
                switch(NowPower)
                {
                    case ThePower.lowlow:
                        NowPower = ThePower.medium;
                        break;
                }
                break;
            case SpiritualProperty.pillar:
                switch(NowPower)
                {
                    case ThePower.low:
                        NowPower =ThePower.medium;
                        break;
                    default:
                        NowPower = ThePower.high;
                        break;
                }
                break;
            case SpiritualProperty.kindergarden:
                NowPower = RandomEx.Shared.GetItem(new ThePower[]{ThePower.high, ThePower.medium,ThePower.low,ThePower.lowlow,
                ThePower.high, ThePower.medium,ThePower.low,});//←三つは、lowlowの確率を下げるため
                break;
            case SpiritualProperty.liminalwhitetile:
            case SpiritualProperty.sacrifaith:
            case SpiritualProperty.cquiest:
                switch(NowPower)
                {
                    case ThePower.low:
                        NowPower = ThePower.medium;
                        break;
                    case ThePower.lowlow:
                        NowPower = ThePower.low;
                        break;
                }
                break;
            case SpiritualProperty.godtier:
                switch(NowPower)
                {
                    case ThePower.lowlow:
                        NowPower = ThePower.medium;
                        break;
                    default:
                        NowPower = ThePower.high;
                    break;
                }
                break;
            case SpiritualProperty.baledrival:
                NowPower = ThePower.high;
                break;
            case SpiritualProperty.devil:
                switch(NowPower)
                {
                    case ThePower.medium:
                        NowPower = ThePower.high;
                        break;
                    case ThePower.low:
                        NowPower = RandomEx.Shared.GetItem(new ThePower[]{ThePower.high, ThePower.medium});
                        break;
                    case ThePower.lowlow:
                        NowPower = ThePower.medium;
                        break;
                }
                break;
        }
    }
    /// <summary>
    /// キャラクターのパワーが負けたときに(死んだときに)変化する関数
    /// </summary>
    void TransitionPowerOnBattleLostByCharacterImpression()
    {
        switch(MyImpression)
        {
            case SpiritualProperty.pillar:
                if (NowPower != ThePower.low)
                {
                    NowPower = ThePower.high;
                }
                break;
            case SpiritualProperty.kindergarden:
                NowPower = RandomEx.Shared.GetItem(new ThePower[]{ThePower.high, ThePower.medium,ThePower.low,ThePower.lowlow,
                ThePower.high, ThePower.medium,ThePower.low,});//←三つは、lowlowの確率を下げるため
                break;
            case SpiritualProperty.liminalwhitetile:
                switch(NowPower)
                {
                    case ThePower.high:
                    case ThePower.medium:
                        NowPower = ThePower.low;
                        break;
                    case ThePower.low:
                        NowPower = RandomEx.Shared.GetItem(new ThePower[]{ThePower.lowlow, ThePower.low});
                        break;
                    case ThePower.lowlow:
                        NowPower = ThePower.low;
                    break;
                }
                break;
            case SpiritualProperty.sacrifaith:
                NowPower = RandomEx.Shared.GetItem(new ThePower[]{ThePower.high, ThePower.medium});
                break;
            case SpiritualProperty.cquiest:
                switch(NowPower)
                {
                    case ThePower.high:
                        NowPower = ThePower.lowlow;
                        break;
                }
                break;
            case SpiritualProperty.baledrival:
                NowPower = ThePower.lowlow;
                break;
            case SpiritualProperty.devil:
                switch(NowPower)
                {
                    case ThePower.high:
                        NowPower = RandomEx.Shared.GetItem(new ThePower[]{ThePower.high, ThePower.medium});
                        break;
                    case ThePower.medium:
                        NowPower = RandomEx.Shared.GetItem(new ThePower[]{ThePower.low, ThePower.medium,ThePower.lowlow});
                        break;
                    case ThePower.low:
                        NowPower = ThePower.lowlow;
                        break;
                    case ThePower.lowlow:
                        NowPower = RandomEx.Shared.GetItem(new ThePower[]{ThePower.high, ThePower.lowlow});
                        break;
                }
                break;
        }
    }
    /// <summary>
    /// キャラクターのパワーが戦闘から逃げ出したときに変化する関数
    /// </summary>
    void TransitionPowerOnBattleRunOutByCharacterImpression()
    {
        switch(MyImpression)
        {
            case SpiritualProperty.pillar:
                NowPower =ThePower.medium;
                break;
            case SpiritualProperty.kindergarden:
                NowPower = RandomEx.Shared.GetItem(new ThePower[]{ThePower.high, ThePower.medium,ThePower.low,ThePower.lowlow,
                ThePower.high, ThePower.medium,ThePower.low,});//←三つは、lowlowの確率を下げるため
                break;
            case SpiritualProperty.sacrifaith:
                NowPower = ThePower.high;
                break;
            case SpiritualProperty.godtier:
                switch(NowPower)
                {
                    case ThePower.medium:
                        NowPower = ThePower.low;
                        break;
                    case ThePower.low:
                        NowPower = ThePower.lowlow;
                        break;
                }
                break;
            case SpiritualProperty.devil:
                switch(NowPower)
                {
                    case ThePower.medium:
                        NowPower = ThePower.high;
                        break;
                    case ThePower.low:
                        NowPower = RandomEx.Shared.GetItem(new ThePower[]{ThePower.high, ThePower.medium});
                        break;
                    case ThePower.lowlow:
                        NowPower = ThePower.medium;
                        break;
                }
                break;
        }
    }
    /// <summary>
    /// AllyClassのディープコピー
    /// 初期化の処理もawake代わりにここでやろう
    /// </summary>
    /// <param name="dst"></param>
    public void DeepCopy(AllyClass dst)
    {

        // 2. BaseStates のフィールドをコピー
        InitBaseStatesDeepCopy(dst);

        // 3. AllyClass 独自フィールドをコピー
        dst._skillALLList = new List<AllySkill>();
        foreach(var skill in _skillALLList)
        {
            dst._skillALLList.Add(skill.InitAllyDeepCopy());
        }
        dst.ValidSkillIDList = new List<int>(ValidSkillIDList);  //主人公達の初期有効化スキルIDをランタイム用リストにセット
        dst.EmotionalAttachmentSkillID = EmotionalAttachmentSkillID;//思い入れスキルIDをコピー
        //dst._uic = _uic;//参照なのでそのまま渡す 主人公キャラの参照UIオブジェクト
        if(_uic != null)
        {
            dst.BindUIController(_uic);//一元化された基本クラスのフィールドに設定する
        }
        if(dst.UI == null)Debug.LogError("UIがnullです");
        if(dst.UI.arrowGrowAndVanish == null)Debug.LogError("arrowGrowAndVanishがnullです");
        dst.UI.Init();

        Debug.Log("AllyClassディープコピー完了");
    }


}

[Serializable]
public class BassJackStates : AllyClass //共通ステータスにプラスでそれぞれのキャラの独自ステータスとかその処理
{
    public BassJackStates DeepCopy()
    {
        var clone = new BassJackStates();
        DeepCopy(clone);
        Debug.Log("BassJackStatesディープコピー完了");
        return clone;
    }
}
[Serializable]
public class SateliteProcessStates : AllyClass //共通ステータスにプラスでそれぞれのキャラの独自ステータスとかその処理
{
    public SateliteProcessStates DeepCopy()
    {
        var clone = new SateliteProcessStates();
        DeepCopy(clone);
        Debug.Log("SateliteProcessStatesディープコピー完了");
        return clone;
    }
}
[Serializable]
public class StairStates : AllyClass //共通ステータスにプラスでそれぞれのキャラの独自ステータスとかその処理
{
    public StairStates DeepCopy()
    {
        var clone = new StairStates();
        DeepCopy(clone);
        Debug.Log("StairStatesディープコピー完了");
        return clone;
    }
}
[Serializable]
public class AllySkill : BaseSkill
{
    [Header("スキルのidは必ず設定、また、下にあるValidSkillIDListにIDが入ってないと有効にならない\n(実際のプレイで成長するって要素があるから、これは初期値とデバック用の話ね)")]
    [SerializeField]
    int _iD;
    /// <summary>
    /// id　主に有効化されてるかどうか
    /// </summary>
    public int ID => _iD;

    /// <summary>
    /// allySkillにおける行使者はAllyClassなのでキャストして返す
    /// </summary>
    public new AllyClass Doer => (AllyClass)base.Doer;
    [Header("スキルの熟練度はプレイで成長するから、基本的にはゼロだけど、\n特殊なフレーバー要素で上げとくのもあり。")]
    /// <summary>
    /// スキル熟練度　単純な数のプロパティで、標準のロジックはスキル内ではないからここでは数だけ
    /// </summary>
    public float Proficiency;

    /// <summary>
    /// スキル使用回数をカウントアップする
    /// allySkillでは熟練度が加算される
    /// </summary>
    public override void DoSkillCountUp()
    {
        base.DoSkillCountUp();
        AddProficiency();
        AddEmotionalAttachmentSkillQuantity();
    }

    /// <summary>
    /// 熟練度を加算する（思い入れ量に応じてスケーリング）
    /// </summary>
    void AddProficiency()
    {
        // このスキルが思い入れスキルかどうかチェック
        if (Doer.EmotionalAttachmentSkillID == ID)
        {
            // 思い入れスキルの場合、思い入れ量に応じてスケーリング
            float multiplier = Doer.GetProficiencyMultiplierFromQuantity();
            Proficiency += multiplier;
            Debug.Log($"思い入れスキル熟練度加算: +{multiplier:F2} (思い入れ量: {Doer.EmotionalAttachmentSkillQuantity})");
        }
        else
        {
            // 通常スキルは1.0倍
            Proficiency += 1.0f;
        }
    }
    /// <summary>
    /// allySkillで継承したスキルパワー関数
    /// </summary>
    protected override float _skillPower(bool IsCradle)
    {
        //スキルの思い入れ由来のhp固定加算値
        return base._skillPower(IsCradle) + CurrentHPFixedAdditionSkillPower();
    }
    /// <summary>
    /// 現在HP固定加算をスキルパワーに加算する用の関数
    /// スキルの思い入れ由来の固定加算値
    /// </summary>
    float CurrentHPFixedAdditionSkillPower()
    {
        if(Doer.EmotionalAttachmentSkillID == ID)//思い入れスキルならhp固定加算が入る
        {
            // 思い入れ量から倍率を取得
            float multiplier = Doer.GetCurrentHPFixedAdditionMultiplier();
            
            // 基礎値×現在HP = SkillPower加算値
            float currentHP = Doer.HP;
            float additionPower = multiplier * currentHP;
            
            Debug.Log($"思い入れスキルHP固定加算: +{additionPower:F2} (倍率: {multiplier:F3}, 現在HP: {currentHP}, 思い入れ量: {Doer.EmotionalAttachmentSkillQuantity})");
            return additionPower;
        }
        return 0;
    }
    /// <summary>
    /// 思い入れ量を加算する用の関数
    /// </summary>
    void AddEmotionalAttachmentSkillQuantity()
    {
        Doer.EmotionalAttachmentSkillQuantity++;
    }
    /// <summary>
    /// TLOAの思い入れスキル用弱体化スキルパッシブを付与する専用の関数
    /// 重複を防ぐため既にあった場合、同名のパッシブを削除する
    /// </summary>
    /// <param name="passive">ここに渡すのは弱体化スキルパッシブ(スキルの思い入れ由来)専用</param>
    public void ApplyEmotionalAttachmentSkillQuantityChangeSkillWeakeningPassive(BaseSkillPassive passive)
    {
        var tuning = PlayersStatesHub.Tuning;
        var weakeningPassive = tuning?.EmotionalAttachmentSkillWeakeningPassiveRef;
        if (weakeningPassive == null)
        {
            Debug.LogError("ApplyEmotionalAttachmentSkillQuantityChangeSkillWeakeningPassive: Tuning が未設定です");
            return;
        }
        if(passive.Name == weakeningPassive.Name)
        {
            //弱体化スキルパッシブに該当するものが一つでもあったら、それを消す
            for (int i = ReactiveSkillPassiveList.Count - 1; i >= 0; i--)//回してるリストから削除するのでfor文で逆順に回すと安心
            {
                if(ReactiveSkillPassiveList[i].Name == passive.Name)
                {
                    ReactiveSkillPassiveList.RemoveAt(i);
                }
            }
            //思い入れ弱体化スキルパッシブを付与する
            ApplySkillPassive(passive);
        }else
        {
            Debug.LogError("思い入れ弱体化スキルパッシブ付与関数に\n渡されたパッシブがスキルの思い入れ専用の弱体化スキルパッシブではありません");
        }
    }
    
    public AllySkill InitAllyDeepCopy()
    {
        var clone = new AllySkill();
        InitDeepCopy(clone);

        clone._iD = _iD;
        clone.Proficiency = Proficiency;//熟練度
        return clone;
    }
}
