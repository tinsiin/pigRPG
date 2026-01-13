using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Unity.Profiling;

public class Walking : MonoBehaviour, IPlayersContextConsumer
{
    WatchUIUpdate wui;
    [SerializeField] private TextMeshProUGUI tmp;
    [SerializeField] private Button walkbtn;
    [SerializeField] private Button _nextWaitBtn;
    [SerializeField] private SelectButton SelectButtonPrefab;
    [SerializeField] private int SelectBtnSize;
    [SerializeField] private MessageDropper MessageDropper;
    public static Walking Instance;
    // Walkボタンの再入防止フラグ（多重起動防止）
    private bool _isWalking = false;
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            UIStateHub.Bind(USERUI_state, SKILLUI_state);
        }
        else
        {
            Destroy(this);
        }
    }

    private void OnEnable()
    {
        PlayersContextRegistry.Register(this);
    }

    private void OnDisable()
    {
        PlayersContextRegistry.Unregister(this);
    }


    /// <summary>
    /// USERUIの状態
    /// </summary>
    public ReactiveProperty<TabState> USERUI_state = new();

    /// <summary>
    /// スキルUIで誰のスキルが映っているか
    /// </summary>
    public ReactiveProperty<SkillUICharaState> SKILLUI_state = new();



    /// <summary>
    ///     選択肢ボタンを入れる親オブジェクト取得
    /// </summary>
    [SerializeField] private RectTransform SelectButtonArea;

    /// <summary>
    ///     エリア選択ボタンが押されると返ってくる。-1はpush待ち
    /// </summary>
    private int AreaResponse;

    /// <summary>
    ///     選択肢ボタンのリスト
    /// </summary>
    private List<SelectButton> buttons;

    private AreaDate NowAreaData;
    [NonSerialized]
    public StageCut NowStageCut;
    private Stages stages;

    //現在のステージとエリアのデータを保存する関数
    [NonSerialized]
    public StageData NowStageData;
    private IPlayersProgress playersProgress;
    private IPlayersParty playersParty;
    private IPlayersUIControl playersUIControl;
    private IPlayersSkillUI playersSkillUI;
    private IPlayersRoster playersRoster;
    private IPlayersTuning playersTuning;
    private  void Start()
    {
        if (playersProgress == null) Debug.LogError("playersProgress が null です");
        if (playersParty == null) Debug.LogError("playersParty が null です");
        if (playersUIControl == null) Debug.LogError("playersUIControl が null です");
        if (playersSkillUI == null) Debug.LogError("playersSkillUI が null です");
        stages = Stages.Instance;
        wui = WatchUIUpdate.Instance;
        BaseStates.CsvLoad();
        
        //初期UI更新　最適化のため最終開発の段階で初期UIの更新だけをするようにする。
        TestProgressUIUpdate();

        // キャラコンフィグ選択時の処理は CharaconfigController 側で RefreshUI を購読して行うため、ここでの処理は不要

        //USERUIの状態のsubscribe
        USERUI_state.Subscribe(
            state =>
            {
                if (state == TabState.SelectTarget) SelectTargetButtons.Instance.OnCreated();
                //それぞれ画面に移動したときに生成コールが実行されるようにする

                if (state == TabState.SelectRange) SelectRangeButtons.Instance.OnCreated();
                // NextWaitボタンの interactable 制御はここでは行わない（タイミング競合回避のため）
            });

        //USERUIの初期状態
        //USERUI_state.Value = TabState.walk;
    }

    public void InjectPlayersContext(PlayersContext context)
    {
        playersProgress = context?.Progress;
        playersParty = context?.Party;
        playersUIControl = context?.UIControl;
        playersSkillUI = context?.SkillUI;
        playersRoster = context?.Roster;
        playersTuning = context?.Tuning;
    }

    /// <summary>
    ///     歩行するボタン
    /// </summary>
    public async void OnWalkBtn()
    {
        BusyOverlay.Instance.Show();
            // 多重起動を無視
            if (_isWalking) return;
            Debug.Log("歩行ボタン押された");

            // 事前検証（null時は実行しない）
            if (stages == null) { Debug.LogError("stagesが認識されていない"); return; }
            if (playersProgress == null) { Debug.LogError("playersProgress が認識されていない"); return; }
            if (playersParty == null)    { Debug.LogError("playersParty が認識されていない");    return; }

            _isWalking = true;
            bool? prevInteractable = null;
            try
            {
                if (walkbtn != null)
                {
                    prevInteractable = walkbtn.interactable;
                    walkbtn.interactable = false; // 実行中は押せない状態に
                }
                await Walk(1);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            finally
            {
                if (walkbtn != null && prevInteractable.HasValue)
                {
                    walkbtn.interactable = prevInteractable.Value; // UIを元に戻す
                }
                _isWalking = false;
            }
    }

    private async UniTask OnClickNextWaitBtn()
    {
        // Kモードがアクティブなら即時解除（アニメなし）
        WatchUIUpdate.Instance?.ForceExitKImmediate();
        //USERUI_state.Value = await orchestrator.Step();
        if (orchestrator == null || orchestrator.Phase == BattlePhase.Completed)
        {
            return;
        }
        await orchestrator.RequestAdvance();
        USERUI_state.Value = orchestrator.CurrentUiState;
    }

    public BattleOrchestrator orchestrator;
    public IBattleContext BattleContext => orchestrator?.Manager;
    private async UniTask Encount()
    {
        var enemyNumber = 2;//エンカウント人数を指定できる。　-1が普通の自動調整モード
        var initializer = new BattleInitializer(MessageDropper);
        var result = await initializer.InitializeBattle(NowStageCut, playersParty, playersProgress, playersUIControl, playersSkillUI, playersRoster, playersTuning, enemyNumber);
        if (!result.EncounterOccurred)
        {
            Debug.Log("No encounter");
            BusyOverlay.Instance.Hide();
            return;
        }

        orchestrator = result.Orchestrator;

        USERUI_state.Value = initializer.SetupInitialBattleUI(orchestrator);//一番最初のUSERUIの状態を変更させるのと戦闘ループの最初の準備処理。
        // リスナーの重複登録を防止
        _nextWaitBtn.onClick.RemoveAllListeners();
        _nextWaitBtn.onClick.AddListener(() => OnClickNextWaitBtn().Forget()); //ボタンにbmの処理を追加
        //非同期なのでボタン処理自体は非同期で実行されるが、例えばUI側での他のボタンや、このボタン自体の処理を防ぐってのはないけど、
        //そこは内部でのUI処理で対応してるから平気

        BusyOverlay.Instance.Hide();
    }

    //メインループ☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆
    //☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆
    private async UniTask Walk(int footnumber) //リストの内容を反映
    {
        WatchUIUpdate.Instance?.BeginWalkCycleTiming();
        orchestrator = null;//歩くたびに消しとく
        try
        {
            //ps.AddProgress(footnumber); //進行度を増やす。
            StageDataUpdate();

            //主人公達の歩行時の処理
            playersParty.PlayersOnWalks(1);//footnumber関係なしに一歩分の効果(テスト用)

            //EYEArea歩行の更新
            wui.StageDataUIUpdate(NowStageData, NowStageCut, playersProgress); 
            //Encount前にサイドオブジェクト動かさないと、ズーム前のdelay処理で後から出てくる感じが気持ち悪いからここに。

            //エンカウント
            await Encount();




            /*/*if (NowAreaData.Rest) //休憩地点なら
                Debug.Log("ここは休憩地点");

            if (!string.IsNullOrEmpty(NowAreaData.NextID)) //次のエリア選択肢
            {
                var arr = NowAreaData.NextIDString.Split(","); //選択肢文章を小分け
                var arr2 = NowAreaData.NextID.Split(","); //選択肢のIDを小分け

                playersProgress.SetArea(await CreateAreaButton(arr, arr2));
                playersProgress.ProgressReset();
            }

            if (string.IsNullOrEmpty(NowAreaData.NextStageID)) //次のステージへ(選択肢なし)
            {
            }*/

            TestProgressUIUpdate(); //テスト用進行度ui更新

            
        }
        finally
        {
            WatchUIUpdate.Instance?.EndWalkCycleTiming();
        }
    }

    /// <summary>
    ///     ステージデータの更新　uiの更新も行う
    /// </summary>
    private void StageDataUpdate()
    {
        NowStageData = stages.RunTimeStageDates[playersProgress.NowStageID]; //現在のステージデータ
        NowStageCut = NowStageData.CutArea[playersProgress.NowAreaID]; //現在のエリアデータ
        NowAreaData = NowStageCut.AreaDates[playersProgress.NowProgress]; //現在地点
    }

    /// <summary>
    ///     次のエリア選択肢のボタンを生成。
    /// </summary>
    /// <param name="selectparams"></param>
    public async UniTask<int> CreateAreaButton(string[] stringParams, string[] idParams)
    {
        walkbtn.enabled = false;
        AreaResponse = -1; //ボタン解答が進まないよう無効化
        var index = 0;
        //var tasks = new List<UniTask>();

        buttons = new List<SelectButton>();
        foreach (var s in stringParams)
        {
            var button = Instantiate(SelectButtonPrefab, SelectButtonArea);
            buttons.Add(button);
            button.OnCreateButton(index, s, OnAnyClickSelectButton, int.Parse(idParams[index]), SelectBtnSize);
            index++;
        }

        //何かしらボタンが押されて返されるまで待つ　:cancellationTokenは複数あるオプションのうち一つを選ぶ構文
        await UniTask.WaitUntil(() => AreaResponse != -1, cancellationToken: this.GetCancellationTokenOnDestroy());

        AreaButtonClose();
        walkbtn.enabled = true;

        var res = AreaResponse;

        return res;
    }

    // ベンチマーク用: 実処理の1歩分をそのまま実行（A/W/IntroJit を更新）
    public async UniTask RunOneWalkStepForBenchmark()
    {
        await Walk(1);
        await orchestrator.EndBattle();//後処理しないとUIが増え続けて重くなって、適切なベンチマーク測定不可能になる。
    }

    /// <summary>
    ///     エリア選択肢ボタンを閉じる
    /// </summary>
    private void AreaButtonClose()
    {
        foreach (var button in buttons) button.Close();
    }

    /// <summary>
    ///     選択肢ボタンに渡し選択肢の結果を記録するための関数
    /// </summary>
    private void OnAnyClickSelectButton(int returnid)
    {
        Debug.Log(returnid + "のエリアIDを記録");
        AreaResponse = returnid; //ここで0～の数字を渡されることでボタン選択処理の非同期待ちが進行
    }

    //最終的にeyearea側で一気にeyeareaのUIを処理するのを作って、そっちにデータを渡すようにする。
    private void TestProgressUIUpdate() //テスト用
    {
        tmp.text = "" + playersProgress.NowProgress;
    }
}
