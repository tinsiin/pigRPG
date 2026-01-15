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
    [SerializeField] private TextMeshProUGUI tmp;
    [SerializeField] private Button walkbtn;
    [SerializeField] private Button _nextWaitBtn;
    [SerializeField] private SelectButton SelectButtonPrefab;
    [SerializeField] private int SelectBtnSize;
    [SerializeField] private WalkingSystemManager walkingSystemManager;
    public static Walking Instance;
    private int _queuedWalks = 0;
    // Walkボタンの再入防止フラグ（多重起動防止）
    private bool _isWalking = false;
    private bool _isExitSelectionActive = false;
    private bool _allowExitSkip = false;
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
            if (walkingSystemManager == null)
            {
                walkingSystemManager = ResolveWalkingSystemManager();
            }
            if (walkingSystemManager == null)
            {
                Debug.LogError("WalkingSystemManager が見つかりません。");
                return;
            }

            bool exitSkipRequested = TrySkipExitSelection();
            if (exitSkipRequested)
            {
                if (_queuedWalks < 1)
                {
                    _queuedWalks = 1;
                }
                if (_isWalking)
                {
                    return;
                }
            }
            bool skipRequested = walkingSystemManager.TrySkipApproach();
            if (skipRequested)
            {
                if (_queuedWalks < 1)
                {
                    _queuedWalks = 1;
                }
                if (_isWalking)
                {
                    return;
                }
            }

            // 多重起動を無視
            if (_isWalking) return;
            Debug.Log("歩行ボタン押された");

            if (_queuedWalks < 1)
            {
                _queuedWalks = 1;
            }
            _isWalking = true;
            try
            {
                await RunQueuedWalksAsync();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            finally
            {
                _isWalking = false;
        }
    }

    public void BeginExitSelection(bool allowWalkSkip)
    {
        _isExitSelectionActive = true;
        _allowExitSkip = allowWalkSkip;
    }

    public void EndExitSelection()
    {
        _isExitSelectionActive = false;
        _allowExitSkip = false;
    }

    private bool TrySkipExitSelection()
    {
        if (!_isExitSelectionActive || !_allowExitSkip) return false;
        if (AreaResponse != -1) return false;
        AreaResponse = -2;
        return true;
    }

    private WalkingSystemManager ResolveWalkingSystemManager()
    {
        var found = FindObjectOfType<WalkingSystemManager>();
        if (found != null && found.HasRootGraph) return found;
        if (found != null) return found;

        var all = Resources.FindObjectsOfTypeAll<WalkingSystemManager>();
        if (all == null || all.Length == 0) return null;

        WalkingSystemManager fallbackWithGraph = null;
        for (var i = 0; i < all.Length; i++)
        {
            var candidate = all[i];
            if (candidate == null) continue;
            if (candidate.HasRootGraph)
            {
                if (candidate.gameObject.activeInHierarchy) return candidate;
                if (fallbackWithGraph == null) fallbackWithGraph = candidate;
            }
        }

        if (fallbackWithGraph != null) return fallbackWithGraph;
        for (var i = 0; i < all.Length; i++)
        {
            var candidate = all[i];
            if (candidate == null) continue;
            if (candidate.gameObject.activeInHierarchy) return candidate;
        }

        return all[0];
    }

    private async UniTask RunQueuedWalksAsync()
    {
        while (_queuedWalks > 0)
        {
            _queuedWalks--;
            await walkingSystemManager.RunOneStepAsync();
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

    public void BeginBattle(BattleOrchestrator nextOrchestrator, TabState initialState)
    {
        if (nextOrchestrator == null)
        {
            Debug.LogWarning("Walking.BeginBattle: orchestrator is null.");
            return;
        }

        orchestrator = nextOrchestrator;
        USERUI_state.Value = initialState;

        if (_nextWaitBtn == null)
        {
            Debug.LogWarning("Walking.BeginBattle: NextWait button is null.");
            return;
        }

        _nextWaitBtn.onClick.RemoveAllListeners();
        _nextWaitBtn.onClick.AddListener(() => OnClickNextWaitBtn().Forget());
    }

    public BattleOrchestrator orchestrator;
    public IBattleContext BattleContext => orchestrator?.Manager;
    /// <summary>
    ///     次のエリア選択肢のボタンを生成。
    /// </summary>
    /// <param name="selectparams"></param>
    public async UniTask<int> CreateAreaButton(string[] stringParams, string[] idParams)
    {
        var prevWalkEnabled = walkbtn != null && walkbtn.enabled;
        if (walkbtn != null && !_allowExitSkip)
        {
            walkbtn.enabled = false;
        }
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
        if (walkbtn != null)
        {
            walkbtn.enabled = prevWalkEnabled;
        }

        var res = AreaResponse;

        return res;
    }

    // ベンチマーク用: 実処理の1歩分をそのまま実行（A/W/IntroJit を更新）
    public async UniTask RunOneWalkStepForBenchmark()
    {
        if (walkingSystemManager == null)
        {
            walkingSystemManager = ResolveWalkingSystemManager();
        }
        if (walkingSystemManager == null)
        {
            Debug.LogError("WalkingSystemManager が見つかりません。");
            return;
        }
        await walkingSystemManager.RunOneStepAsync();
        if (orchestrator != null)
        {
            await orchestrator.EndBattle();
        }
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
