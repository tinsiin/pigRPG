using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Unity.Profiling;

public class Walking : MonoBehaviour
{
    WatchUIUpdate wui;
    [SerializeField] private TextMeshProUGUI tmp;
    [SerializeField] private Button walkbtn;
    [SerializeField] private Button _nextWaitBtn;
    [SerializeField] private SelectButton SelectButtonPrefab;
    [SerializeField] private int SelectBtnSize;
    [SerializeField] private MessageDropper MessageDropper;
    public static Walking Instance;
    // NextWaitボタンの再入防止フラグ
    private bool _isProcessingNext = false;
    // 処理中に押された NextWait を次回に繰り越すフラグ
    private bool _pendingNextClick = false;
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(this);
        }
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
    public StageCut NowStageCut;
    private Stages stages;

    //現在のステージとエリアのデータを保存する関数
    public StageData NowStageData;
    private PlayersStates ps;
    private  void Start()
    {
        ps = PlayersStates.Instance;//変数にキャッシュして使用
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

    /// <summary>
    ///     歩行するボタン
    /// </summary>
    public async void OnWalkBtn()
    {
        if(stages == null)Debug.LogError("stagesが認識されていない");
        if(ps == null)Debug.LogError("psが認識されていない");
        if (stages && ps != null)
        {
            await Walk(1);
        }
    }

    private async UniTask OnClickNextWaitBtn()
    {
        //USERUI_state.Value = await bm.CharacterActBranching();
        if (_isProcessingNext)
        {
            // 処理中に押された場合は次回繰り越し
            _pendingNextClick = true;
            return; // 再入防止
        }
        _isProcessingNext = true;
        // UIの有効/無効はここでは切り替えない（ガードで防止）

        try
        {
            var next = await bm.CharacterActBranching();
            USERUI_state.Value = next;
        }
        finally
        {
            _isProcessingNext = false;
            // 処理中にクリックがあった場合、NextWait 画面なら自動的に次を進める
            if (_pendingNextClick && USERUI_state.Value == TabState.NextWait)
            {
                _pendingNextClick = false;
                OnClickNextWaitBtn().Forget();
            }
            else
            {
                _pendingNextClick = false; // いずれにせよクリア
            }
        }
    }

    public  BattleManager bm;
    private  void Encount()
    {
        var enemyNumber = 2;//エンカウント人数を指定できる。　-1が普通の自動調整モード
        //Debug.Log("エンカウント処理WalkingのEncountメソッド");
        BattleGroup enemyGroup = null; //敵グループ
        BattleGroup allyGroup = null; //味方グループ
        if ((enemyGroup = NowStageCut.EnemyCollectAI(enemyNumber)) != null) //nullでないならエンカウントし、敵グループ
        {
            //敵グループが返ってきてエンカウント
            Debug.Log("encount");


            //enemyGroupにいる敵によってallyGroupの人選が変わる処理、
            //例　ホッチキスでサテライトの単体戦になるとか。　
            //だから、allygroupがフルで人選されるとき以外は、他の味方アイコンがそそくさと逃げる演出をする。
            //死んでる味方がいるのとはまた違う。その場合でも死亡状態で戦いの場に選出はされるからだ。

            allyGroup = ps.GetParty(); //何もなければフルの味方グループ
            //敵視人選の処理終わり-------------------------------------


            //BattleManagerを生成
        bm = new BattleManager(allyGroup, enemyGroup,BattleStartSituation.Normal, MessageDropper); //バトルを管理するクラス
        //battleTimeLineを生成
        var TimeLine = new BattleTimeLine(new List<BattleManager>{bm}); //バトルのタイムラインを管理するクラス

        // 敵UI配置システムのテスト実行（戦闘参加敵のみ）
        TestEnemyUIPlacement(enemyGroup).Forget();
        
        PlayersStates.Instance.OnBattleStart();
        USERUI_state.Value = bm.ACTPop();//一番最初のUSERUIの状態を変更させるのと戦闘ループの最初の準備処理。
            // リスナーの重複登録を防止
            _nextWaitBtn.onClick.RemoveAllListeners();
            _nextWaitBtn.onClick.AddListener(()=>OnClickNextWaitBtn().Forget()); //ボタンにbmの処理を追加
            //非同期なのでボタン処理自体は非同期で実行されるが、例えばUI側での他のボタンや、このボタン自体の処理を防ぐってのはないけど、
            //そこは内部でのUI処理で対応してるから平気
        }
        else
        {
            //エンカウントしなかった場合の処理

            Debug.Log("No encounter");
        }
    }

    //メインループ☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆
    //☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆
    private async UniTask Walk(int footnumber) //リストの内容を反映
    {
        //ps.AddProgress(footnumber); //進行度を増やす。
        StageDataUpdate();

        //主人公達の歩行時の処理
        ps.PlayersOnWalks(1);//footnumber関係なしに一歩分の効果(テスト用)

        //エンカウント
        Encount();

        if (NowAreaData.Rest) //休憩地点なら
            Debug.Log("ここは休憩地点");

        if (!string.IsNullOrEmpty(NowAreaData.NextID)) //次のエリア選択肢
        {
            var arr = NowAreaData.NextIDString.Split(","); //選択肢文章を小分け
            var arr2 = NowAreaData.NextID.Split(","); //選択肢のIDを小分け

            ps.SetArea(await CreateAreaButton(arr, arr2));
            ps.ProgressReset();
        }

        if (string.IsNullOrEmpty(NowAreaData.NextStageID)) //次のステージへ(選択肢なし)
        {
        }

        TestProgressUIUpdate(); //テスト用進行度ui更新

        
    }

    /// <summary>
    ///     ステージデータの更新　uiの更新も行う
    /// </summary>
    private void StageDataUpdate()
    {
        NowStageData = stages.RunTimeStageDates[ps.NowStageID]; //現在のステージデータ
        NowStageCut = NowStageData.CutArea[ps.NowAreaID]; //現在のエリアデータ
        NowAreaData = NowStageCut.AreaDates[ps.NowProgress]; //現在地点

        wui.WalkUIUpdate(NowStageData, NowStageCut, ps); //ui更新
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


    /// <summary>
    /// 敵UI配置システムのテスト用メソッド（戦闘参加敵のみ）
    /// </summary>
    private async UniTask TestEnemyUIPlacement(BattleGroup enemyGroup)
    {
        try
        {
            Debug.Log("=== 敵UI配置システムテスト開始（戦闘参加敵のみ） ===");
            Debug.Log($"戦闘参加敵数: {enemyGroup?.Ours?.Count ?? 0}体");
            
            // 改良版ズーム実行（敵UI生成も内部で並行実行される）
            await wui.FirstImpressionZoomImproved();
            
            Debug.Log("=== 敵UI配置システムテスト完了 ===");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"敵UI配置システムテストでエラーが発生: {e.Message}");
        }
    }

    //最終的にeyearea側で一気にeyeareaのUIを処理するのを作って、そっちにデータを渡すようにする。
    private void TestProgressUIUpdate() //テスト用
    {
        tmp.text = "" + ps.NowProgress;
    }
}