using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

public sealed class WalkingSystemManager : MonoBehaviour, IPlayersContextConsumer, IExitSelectionUI
{
    [SerializeField] private FlowGraphSO rootGraph;
    [SerializeField] private Walking walking;
    [SerializeField] private MessageDropper messageDropper;
    [SerializeField] private WalkApproachUI approachUI;

    private PlayersContext playersContext;
    private GameContext gameContext;
    private AreaController areaController;
    private SideObjectPresenter sidePresenter;
    private CentralObjectPresenter centralPresenter;
    private EventRunner eventRunner;
    private EventHost eventHost;
    private IEventUI eventUI;
    private IBattleRunner battleRunner;

    public bool HasRootGraph => rootGraph != null;

    private void OnEnable()
    {
        PlayersContextRegistry.Register(this);
        if (gameContext != null)
        {
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

        if (messageDropper == null) messageDropper = ResolveMessageDropper();
        if (approachUI == null) approachUI = ResolveApproachUI();
        if (eventUI == null)
        {
            eventUI = new WalkingEventUI(walking, messageDropper);
        }
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

        EnsureBattleRunner();

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

    private RectTransform ResolveSideRoot()
    {
        var watchUI = WatchUIUpdate.Instance;
        if (watchUI != null && watchUI.SideObjectRoot != null)
        {
            return watchUI.SideObjectRoot;
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

    private async UniTask RunOneStepInternalAsync()
    {
        if (areaController == null)
        {
            InitializeIfReady();
        }
        else
        {
            EnsureBattleRunner();
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
    }

    private void UpdateNodeUI()
    {
        var node = areaController?.CurrentNode;
        if (node == null) return;

        var wui = WatchUIUpdate.Instance;
        if (wui != null)
        {
            wui.ApplyNodeUI(node.DisplayName, node.UiHints);
        }

        var hints = node.UiHints;
        if (hints.UseThemeColors)
        {
            sidePresenter?.SetThemeColors(hints.FrameArtColor, hints.TwoColor);

            var arrowManager = BattleSystemArrowManager.Instance;
            if (arrowManager == null)
            {
                var all = Resources.FindObjectsOfTypeAll<BattleSystemArrowManager>();
                for (var i = 0; i < all.Length; i++)
                {
                    var candidate = all[i];
                    if (candidate == null) continue;
                    if (!candidate.gameObject.scene.IsValid()) continue;
                    arrowManager = candidate;
                    break;
                }
            }
            if (arrowManager != null)
            {
                arrowManager.ApplyStageThemeColors(hints.FrameArtColor, hints.TwoColor);
            }
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
