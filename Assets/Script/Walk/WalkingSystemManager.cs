using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

public sealed class WalkingSystemManager : MonoBehaviour, IPlayersContextConsumer, IExitSelectionUI
{
    [SerializeField] private FlowGraphSO rootGraph;
    [SerializeField] private Walking walking;
    [SerializeField] private MessageDropper messageDropper;
    [SerializeField] private WalkApproachUI approachUI;
    [SerializeField] private ProgressIndicatorUI progressUI;

    private PlayersContext playersContext;
    private GameContext gameContext;
    private AreaController areaController;
    private SideObjectPresenter sidePresenter;
    private CentralObjectPresenter centralPresenter;
    private EventRunner eventRunner;
    private EventHost eventHost;
    private IEventUI eventUI;
    private IBattleRunner battleRunner;
    private IDialogueRunner dialogueRunner;
    private IWalkSFXPlayer sfxPlayer;

    // Phase 1: DI注入されたコントローラー
    private IWalkingUIController _walkingUI;

    // Phase 3d: ArrowManager DI注入
    private IArrowManager _arrowManager;

    public bool HasRootGraph => rootGraph != null;
    public GameContext GameContext => gameContext;

    private void OnEnable()
    {
        PlayersContextRegistry.Register(this);
        if (gameContext != null)
        {
            // Clear stale refresh so the first step advances normally after re-enable.
            gameContext.RequestRefreshWithoutStep = false;
            GameContextHub.Set(gameContext);
        }
    }

    private void OnDisable()
    {
        PlayersContextRegistry.Unregister(this);
        GameContextHub.Clear(gameContext);
        CleanupSpawnedObjects();
    }

    private void OnDestroy()
    {
        GameContextHub.Clear(gameContext);
        CleanupSpawnedObjects();
    }

    private void Start()
    {
        InitializeIfReady();
    }

    public void InjectPlayersContext(PlayersContext context)
    {
        playersContext = context;
        if (gameContext != null)
        {
            gameContext.SetPlayersContext(playersContext);
            eventHost?.SetContext(gameContext);
        }
        InitializeIfReady();
    }

    public void RunOneStep()
    {
        RunOneStepAsync().Forget();
    }

    public UniTask RunOneStepAsync()
    {
        return RunOneStepInternalAsync();
    }

    public bool ApplyCurrentNodeUI()
    {
        if (areaController == null)
        {
            InitializeIfReady();
        }
        if (areaController == null) return false;
        UpdateNodeUI();
        return true;
    }

    public async UniTask<int> ShowExitChoices(string[] labels, string[] ids)
    {
        var targetWalking = ResolveWalking();
        if (targetWalking == null)
        {
            Debug.LogError("WalkingSystemManager: Walking reference is missing.");
            return -1;
        }

        targetWalking.BeginExitSelection(true);
        try
        {
            return await targetWalking.CreateAreaButton(labels, ids);
        }
        finally
        {
            targetWalking.EndExitSelection();
        }
    }

    public bool TrySkipApproach()
    {
        if (approachUI == null)
        {
            approachUI = ResolveApproachUI();
        }
        if (approachUI == null) return false;
        return approachUI.TrySkip();
    }

    private void InitializeIfReady()
    {
        if (rootGraph == null) return;
        if (walking == null) walking = ResolveWalking();
        if (playersContext == null)
        {
            playersContext = PlayersContextRegistry.Context;
        }
        if (gameContext == null)
        {
            gameContext = new GameContext(playersContext);
            GameContextHub.Set(gameContext);
        }
        else
        {
            gameContext.SetPlayersContext(playersContext);
        }
        // Don't carry refresh requests across initialization.
        gameContext.RequestRefreshWithoutStep = false;

        if (messageDropper == null) messageDropper = ResolveMessageDropper();
        if (approachUI == null) approachUI = ResolveApproachUI();
        // Phase 1: WatchUIUpdate.InstanceはここでのみIWalkingUIControllerを取得
        if (_walkingUI == null) _walkingUI = WatchUIUpdate.Instance?.WalkingUICtrl;
        // Phase 3d: ArrowManager取得
        if (_arrowManager == null) _arrowManager = BattleSystemArrowManager.Instance;
        if (eventUI == null)
        {
            eventUI = new WalkingEventUI(walking, messageDropper);
        }
        // Wire EventUI to GameContext for ShowMessageEffect
        gameContext.EventUI = eventUI;

        if (eventRunner == null)
        {
            eventRunner = new EventRunner();
        }
        if (eventHost == null)
        {
            eventHost = new EventHost(eventRunner, gameContext, eventUI);
        }
        else
        {
            eventHost.SetContext(gameContext);
            eventHost.SetUI(eventUI);
        }

        if (sidePresenter == null)
        {
            sidePresenter = new SideObjectPresenter();
        }
        if (centralPresenter == null)
        {
            centralPresenter = new CentralObjectPresenter();
        }

        // Initialize SFX player and wire to presenter
        if (sfxPlayer == null)
        {
            sfxPlayer = ResolveSFXPlayer();
        }
        centralPresenter.SetSFXPlayer(sfxPlayer);

        EnsureBattleRunner();
        EnsureDialogueRunner();

        if (areaController != null)
        {
            areaController.SetApproachUI(approachUI);
            return;
        }

        var sideRoot = ResolveSideRoot();
        sidePresenter.SetRoot(sideRoot);
        centralPresenter.SetRoot(sideRoot);
        sidePresenter.ClearImmediate();
        centralPresenter.ClearImmediate();

        areaController = new AreaController(
            rootGraph,
            gameContext,
            this,
            approachUI,
            eventHost,
            sidePresenter,
            centralPresenter);

        if (progressUI != null)
        {
            areaController.SetProgressCallback(OnProgressChanged);
            // 初期状態をUIに反映
            var initialSnapshot = areaController.GetCurrentProgress();
            progressUI.UpdateDisplay(initialSnapshot);
        }
    }

    private void OnProgressChanged(ProgressSnapshot snapshot)
    {
        if (progressUI != null)
        {
            progressUI.UpdateDisplay(snapshot);
        }
    }

    private void CleanupSpawnedObjects()
    {
        sidePresenter?.ClearImmediate();
        centralPresenter?.ClearImmediate();
    }

    private void EnsureBattleRunner()
    {
        if (walking == null) walking = ResolveWalking();
        if (messageDropper == null) messageDropper = ResolveMessageDropper();
        if (battleRunner == null && walking != null)
        {
            battleRunner = new UnityBattleRunner(walking, messageDropper);
        }
        if (gameContext != null)
        {
            gameContext.BattleRunner = battleRunner;
        }
    }

    private void EnsureDialogueRunner()
    {
        if (dialogueRunner == null)
        {
            var novelEventUI = ResolveNovelEventUI();
            if (novelEventUI != null)
            {
                dialogueRunner = new NovelPartDialogueRunner(novelEventUI);

                // NovelPartEventUIはIEventUIも実装しているので、EventUIとしても設定
                // これによりHidePortraitEffectなどのノベル専用Effectが動作する
                if (gameContext != null)
                {
                    gameContext.EventUI = novelEventUI;
                }
            }
        }
        if (gameContext != null)
        {
            gameContext.DialogueRunner = dialogueRunner;
        }
    }

    private INovelEventUI ResolveNovelEventUI()
    {
        // Try to find NovelPartEventUI in the scene
        var found = FindObjectOfType<NovelPartEventUI>();
        if (found != null)
        {
            return found;
        }

        var all = Resources.FindObjectsOfTypeAll<NovelPartEventUI>();
        for (var i = 0; i < all.Length; i++)
        {
            var candidate = all[i];
            if (candidate == null) continue;
            if (!candidate.gameObject.scene.IsValid()) continue;
            return candidate;
        }

        // No NovelPartEventUI available
        return null;
    }

    private RectTransform ResolveSideRoot()
    {
        // Phase 1: 注入されたコントローラーを優先
        if (_walkingUI?.SideObjectRoot != null)
        {
            return _walkingUI.SideObjectRoot;
        }

        var bg = GameObject.Find("BackGround");
        if (bg != null)
        {
            return bg.GetComponent<RectTransform>();
        }

        var eye = GameObject.Find("EyeArea");
        if (eye != null)
        {
            return eye.GetComponent<RectTransform>();
        }

        return walking != null ? walking.GetComponent<RectTransform>() : null;
    }

    private Walking ResolveWalking()
    {
        if (walking != null) return walking;
        if (Walking.Instance != null)
        {
            walking = Walking.Instance;
            return walking;
        }

        var found = FindObjectOfType<Walking>();
        if (found != null)
        {
            walking = found;
            return walking;
        }

        var all = Resources.FindObjectsOfTypeAll<Walking>();
        for (var i = 0; i < all.Length; i++)
        {
            var candidate = all[i];
            if (candidate == null) continue;
            if (!candidate.gameObject.scene.IsValid()) continue;
            walking = candidate;
            return walking;
        }

        return null;
    }

    private WalkApproachUI ResolveApproachUI()
    {
        if (approachUI != null) return approachUI;

        var found = FindObjectOfType<WalkApproachUI>();
        if (found != null)
        {
            approachUI = found;
            return approachUI;
        }

        var all = Resources.FindObjectsOfTypeAll<WalkApproachUI>();
        for (var i = 0; i < all.Length; i++)
        {
            var candidate = all[i];
            if (candidate == null) continue;
            if (!candidate.gameObject.scene.IsValid()) continue;
            approachUI = candidate;
            return approachUI;
        }

        return null;
    }

    private MessageDropper ResolveMessageDropper()
    {
        if (messageDropper != null) return messageDropper;

        var found = FindObjectOfType<MessageDropper>();
        if (found != null)
        {
            messageDropper = found;
            return messageDropper;
        }

        var all = Resources.FindObjectsOfTypeAll<MessageDropper>();
        for (var i = 0; i < all.Length; i++)
        {
            var candidate = all[i];
            if (candidate == null) continue;
            if (!candidate.gameObject.scene.IsValid()) continue;
            messageDropper = candidate;
            return messageDropper;
        }

        return null;
    }

    private IWalkSFXPlayer ResolveSFXPlayer()
    {
        // Try to find an AudioSource in the scene for SFX playback
        var audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = FindObjectOfType<AudioSource>();
        }

        if (audioSource != null)
        {
            return new WalkSFXPlayer(audioSource);
        }

        // No AudioSource available, use null implementation (SFX disabled)
        return NullWalkSFXPlayer.Instance;
    }

    private async UniTask RunOneStepInternalAsync()
    {
        if (areaController == null)
        {
            InitializeIfReady();
        }
        else
        {
            EnsureBattleRunner();
            EnsureDialogueRunner();
        }

        if (areaController == null)
        {
            Debug.LogError("WalkingSystemManager.RunOneStep: AreaController is not ready.");
            return;
        }

        try
        {
            if (approachUI == null) approachUI = ResolveApproachUI();
            areaController.SetApproachUI(approachUI);
            UpdateNodeUI();
            if (gameContext != null)
            {
                gameContext.IsWalkingStep = true;
            }
            await areaController.WalkStep();
            UpdateNodeUI();
        }
        catch (OperationCanceledException)
        {
            // Play停止時などのキャンセルは無視
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
        finally
        {
            if (gameContext != null)
            {
                gameContext.IsWalkingStep = false;
            }
        }
    }

    private void UpdateNodeUI()
    {
        var node = areaController?.CurrentNode;
        if (node == null) return;

        // Phase 1: 注入されたコントローラーを優先
        _walkingUI?.ApplyNodeUI(node.DisplayName, node.UiHints);

        var hints = node.UiHints;
        if (hints.UseThemeColors)
        {
            sidePresenter?.SetThemeColors(hints.FrameArtColor, hints.TwoColor);

            // Phase 3d: DI注入を優先
            _arrowManager?.ApplyStageThemeColors(hints.FrameArtColor, hints.TwoColor);
        }

        if (hints.UseActionMarkColor)
        {
            var actionMark = FindObjectOfType<ActionMarkUI>();
            if (actionMark == null)
            {
                var all = Resources.FindObjectsOfTypeAll<ActionMarkUI>();
                for (var i = 0; i < all.Length; i++)
                {
                    var candidate = all[i];
                    if (candidate == null) continue;
                    if (!candidate.gameObject.scene.IsValid()) continue;
                    actionMark = candidate;
                    break;
                }
            }
            if (actionMark != null)
            {
                actionMark.SetStageThemeColor(hints.ActionMarkColor);
            }
        }
    }
}
