using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// INovelEventUIの実装。
/// 各Presenterを統合してノベルパート表示を制御する。
/// ズームとリアクションはそれぞれ専用マネージャーに委譲。
/// </summary>
public sealed class NovelPartEventUI : MonoBehaviour, INovelEventUI
{
    [Header("Presenters")]
    [SerializeField] private PortraitPresenter portraitPresenter;
    [SerializeField] private BackgroundPresenter backgroundPresenter;
    [SerializeField] private NoisePresenter noisePresenter;
    [SerializeField] private TextBoxPresenter textBoxPresenter;

    [Header("Databases")]
    [SerializeField] private PortraitDatabase portraitDatabase;
    [SerializeField] private BackgroundDatabase backgroundDatabase;

    [Header("Backlog UI")]
    [SerializeField] private BacklogPageView backlogPageView;
    [SerializeField] private GameObject backButton;

    [Header("Message Dropper")]
    [SerializeField] private MessageDropper messageDropper;

    [Header("Novel Input UI")]
    [SerializeField] private FieldDialogueUI fieldDialogueUI;
    [SerializeField] private EventDialogueUI eventDialogueUI;
    [SerializeField] private NovelChoicePresenter novelChoicePresenter;

    [Header("Zoom")]
    [SerializeField] private NovelZoomConfig zoomConfig;

    [SerializeField] private DisplayMode initialDisplayMode = DisplayMode.Dinoid;
    private NovelInputHub inputHub;
    private bool backRequested;

    // 分離されたマネージャー
    private NovelZoomManager zoomManager;
    private NovelReactionHandler reactionHandler;

    /// <summary>
    /// 現在のDisplayMode。TextBoxPresenterが信頼できる唯一の情報源。
    /// TextBoxPresenter未初期化時はInspector設定値をフォールバックとして返す。
    /// </summary>
    public DisplayMode CurrentDisplayMode =>
        textBoxPresenter != null ? textBoxPresenter.CurrentMode : initialDisplayMode;
    public INovelInputProvider InputProvider => inputHub;

    /// <summary>
    /// ズームマネージャーへのアクセス（NovelDialogueStep等がINovelZoomUIとして使用）。
    /// </summary>
    public NovelZoomManager ZoomManager => zoomManager;

    private void Awake()
    {
        Initialize();
    }

    public void Initialize()
    {
        // 入力ハブの初期化
        inputHub = new NovelInputHub();

        // 各入力UIにハブを設定
        if (fieldDialogueUI != null)
        {
            fieldDialogueUI.SetInputHub(inputHub);
        }
        if (eventDialogueUI != null)
        {
            eventDialogueUI.SetInputHub(inputHub);
        }
        if (novelChoicePresenter != null)
        {
            novelChoicePresenter.SetInputHub(inputHub);
        }

        // Presenterにデータベースを設定
        if (portraitPresenter != null)
        {
            portraitPresenter.SetPortraitDatabase(portraitDatabase);
            portraitPresenter.Initialize();
        }

        if (backgroundPresenter != null)
        {
            backgroundPresenter.SetBackgroundDatabase(backgroundDatabase);
            backgroundPresenter.Initialize();
        }

        if (noisePresenter != null)
        {
            noisePresenter.Initialize();
            noisePresenter.SetPortraitDatabase(portraitDatabase);
        }

        if (textBoxPresenter != null)
        {
            textBoxPresenter.SetPortraitDatabase(portraitDatabase);
            textBoxPresenter.Initialize(initialDisplayMode);
        }

        // 分離マネージャーの初期化
        zoomManager = new NovelZoomManager(zoomConfig);
        reactionHandler = new NovelReactionHandler(textBoxPresenter);
    }

    public void SetTabState(TabState state)
    {
        if (UIStateHub.UserState != null)
        {
            UIStateHub.UserState.Value = state;
        }

        if (eventDialogueUI != null)
        {
            eventDialogueUI.SetPrevButtonEnabled(state == TabState.EventDialogue);
        }
    }

    #region IEventUI Implementation

    public void ShowMessage(string message)
    {
        if (textBoxPresenter != null)
        {
            textBoxPresenter.SetText(null, message);
        }
        else
        {
            Debug.Log($"[NovelPartEventUI] ShowMessage: {message}");
        }
    }

    public async UniTask<int> ShowChoices(string[] labels, string[] ids)
    {
        if (novelChoicePresenter == null || inputHub == null)
        {
            Debug.LogWarning("[NovelPartEventUI] ShowChoices: NovelChoicePresenter or InputHub is null");
            return 0;
        }

        SetTabState(TabState.NovelChoice);
        novelChoicePresenter.ShowChoices(labels);
        var selectedIndex = await inputHub.WaitForChoiceAsync(labels.Length);
        return selectedIndex;
    }

    #endregion

    #region Presentation (Portrait, Background, Text, Noise)

    public async UniTask ShowPortrait(PortraitState left, PortraitState right)
    {
        if (portraitPresenter != null)
        {
            await portraitPresenter.Show(left, right);
        }
        else
        {
            Debug.Log($"[NovelPartEventUI] ShowPortrait: L={left?.CharacterId}, R={right?.CharacterId}");
            await UniTask.Yield();
        }
    }

    public async UniTask HidePortrait(PortraitPosition position)
    {
        if (portraitPresenter != null)
        {
            await portraitPresenter.Hide(position);
        }
        else
        {
            Debug.Log($"[NovelPartEventUI] HidePortrait: {position}");
            await UniTask.Yield();
        }
    }

    public async UniTask ShowBackground(string backgroundId)
    {
        if (backgroundPresenter != null)
        {
            await backgroundPresenter.Show(backgroundId);
        }
        else
        {
            Debug.Log($"[NovelPartEventUI] ShowBackground: {backgroundId}");
            await UniTask.Yield();
        }
    }

    public async UniTask HideBackground()
    {
        if (backgroundPresenter != null)
        {
            await backgroundPresenter.Hide();
        }
        else
        {
            Debug.Log("[NovelPartEventUI] HideBackground");
            await UniTask.Yield();
        }
    }

    public void PlayNoise(NoiseEntry[] entries)
    {
        if (noisePresenter != null)
        {
            noisePresenter.Play(entries, OnNoiseSpawned);
        }
        else if (entries != null)
        {
            foreach (var entry in entries)
            {
                Debug.Log($"[NovelPartEventUI] PlayNoise: [{entry.Speaker}] {entry.Text}");
            }
        }
    }

    private void OnNoiseSpawned(NoiseEntry entry)
    {
        if (string.IsNullOrEmpty(entry.Expression)) return;

        var speaker = entry.Speaker;

        // 左右立ち絵
        if (portraitPresenter != null)
        {
            var leftState = portraitPresenter.CurrentLeftState;
            var rightState = portraitPresenter.CurrentRightState;

            if (leftState != null && leftState.CharacterId == speaker)
                portraitPresenter.SetTemporaryExpression(PortraitPosition.Left, entry.Expression);
            if (rightState != null && rightState.CharacterId == speaker)
                portraitPresenter.SetTemporaryExpression(PortraitPosition.Right, entry.Expression);
        }

        // 中央オブジェクト
        var centralId = zoomManager?.GetCurrentCentralObjectCharacterId();
        if (!string.IsNullOrEmpty(centralId) && centralId == speaker)
        {
            zoomManager?.SetTemporaryCentralExpression(entry.Expression);
        }
    }

    public void ClearTemporaryExpressions()
    {
        portraitPresenter?.ClearTemporaryExpressions();
        zoomManager?.ClearTemporaryCentralExpression();
    }

    public void AccelerateNoises()
    {
        if (noisePresenter != null)
        {
            noisePresenter.Accelerate();
        }
        else
        {
            Debug.Log("[NovelPartEventUI] AccelerateNoises");
        }
    }

    public async UniTask ShowText(string speaker, string text)
    {
        if (textBoxPresenter != null)
        {
            textBoxPresenter.SetText(speaker, text);
        }

        // MessageDropperはフィールド会話のDinoidモード時のみ発火。
        // イベント会話ではバックログがあるため不要。
        if (CurrentDisplayMode == DisplayMode.Dinoid
            && messageDropper != null
            && UIStateHub.UserState?.Value == TabState.FieldDialogue)
        {
            messageDropper.CreateMessage(text);
        }

        await UniTask.Yield();
    }

    public async UniTask SwitchTextBox(DisplayMode mode)
    {
        if (CurrentDisplayMode == mode) return;

        if (textBoxPresenter != null)
        {
            await textBoxPresenter.SwitchMode(mode);
        }
        else
        {
            Debug.Log($"[NovelPartEventUI] SwitchTextBox: -> {mode}");
            await UniTask.Yield();
        }
    }

    public async UniTask FadeOutCurrentTextBox()
    {
        if (textBoxPresenter != null)
            await textBoxPresenter.FadeOutCurrent();
    }

    public async UniTask FadeInNewTextBox(DisplayMode mode)
    {
        if (textBoxPresenter != null)
            await textBoxPresenter.FadeInNew(mode);
    }

    #endregion

    #region Navigation (Back, Backlog)

    public int BacklogLinesPerPage => backlogPageView != null ? backlogPageView.LinesPerPage : 8;
    public int BacklogMaxBacktrackPages => backlogPageView != null ? backlogPageView.MaxBacktrackPages : 10;

    public async UniTask ShowBacklog(DialogueBacklog backlog)
    {
        if (backlogPageView != null)
        {
            using (UIBlocker.Instance?.Acquire(BlockScope.AllContents))
            {
                await backlogPageView.ShowAsync(backlog);
            }
        }
    }

    public void HideBacklog()
    {
        backlogPageView?.ForceClose();
    }

    public void SetBackButtonEnabled(bool enabled)
    {
        if (backButton != null)
        {
            backButton.SetActive(enabled);
        }
    }

    public bool ConsumeBackRequest()
    {
        var result = backRequested;
        backRequested = false;
        return result;
    }

    public bool ConsumeBacklogRequest()
    {
        return inputHub?.ConsumeBacklogRequest() ?? false;
    }

    public void RestoreState(DialogueStateSnapshot snapshot)
    {
        if (snapshot == null) return;

        textBoxPresenter?.SetModeImmediate(snapshot.DisplayMode);
        portraitPresenter?.RestoreImmediate(snapshot.LeftPortrait, snapshot.RightPortrait);

        if (snapshot.HasBackground)
        {
            backgroundPresenter?.ShowImmediate(snapshot.BackgroundId);
        }
        else
        {
            backgroundPresenter?.HideImmediate();
        }

        // 中央オブジェクト復元（キャラクターIDがあれば identity ごと復元）
        if (!string.IsNullOrEmpty(snapshot.CentralObjectCharacterId))
        {
            UpdateCentralObjectSprite(snapshot.CentralObjectSprite,
                snapshot.CentralObjectCharacterId, snapshot.CentralObjectExpression);
        }
        else
        {
            UpdateCentralObjectSprite(snapshot.CentralObjectSprite);
        }
    }

    public void OnBackButtonClicked()
    {
        backRequested = true;
    }

    #endregion

    #region State Management (HideAll, ClearAll)

    public void ClearAll()
    {
        portraitPresenter?.ClearAll();
        backgroundPresenter?.HideImmediate();
        noisePresenter?.ClearAll();
        textBoxPresenter?.Clear();
        zoomManager?.UpdateCentralObjectSprite(null);
    }

    public void HideAll()
    {
        ClearReactions();
        portraitPresenter?.ClearAll();
        backgroundPresenter?.HideImmediate();
        noisePresenter?.ClearAll();
        textBoxPresenter?.Hide();
        zoomManager?.UpdateCentralObjectSprite(null);
    }

    public async UniTask HideAllAsync()
    {
        ClearReactions();
        portraitPresenter?.ClearAll();
        backgroundPresenter?.HideImmediate();
        noisePresenter?.ClearAll();
        textBoxPresenter?.Hide();
        zoomManager?.UpdateCentralObjectSprite(null);
        await UniTask.Yield();
    }

    #endregion

    #region INovelZoomUI (委譲)

    public UniTask ZoomToCentralAsync(RectTransform centralObjectRT, FocusArea focusArea)
        => zoomManager.ZoomToCentralAsync(centralObjectRT, focusArea);

    public UniTask ExitZoomAsync()
        => zoomManager.ExitZoomAsync();

    public void RestoreZoomImmediate()
        => zoomManager.RestoreZoomImmediate();

    public void UpdateCentralObjectSprite(Sprite sprite, string characterId = null, string expression = null)
        => zoomManager.UpdateCentralObjectSprite(sprite, characterId, expression);

    public Sprite GetCurrentCentralObjectSprite()
        => zoomManager.GetCurrentCentralObjectSprite();

    public string GetCurrentCentralObjectCharacterId()
        => zoomManager.GetCurrentCentralObjectCharacterId();

    public string GetCurrentCentralObjectExpression()
        => zoomManager.GetCurrentCentralObjectExpression();

    /// <summary>
    /// CentralObjectPresenterを設定する。
    /// WalkingSystemManager初期化時に呼び出す。
    /// </summary>
    public void SetCentralObjectPresenter(CentralObjectPresenter presenter)
    {
        zoomManager?.SetCentralObjectPresenter(presenter);
        zoomManager?.SetPortraitDatabase(portraitDatabase);
    }

    #endregion

    #region INovelReactionUI (委譲)

    public void SetReactionText(string richText, ReactionSegment[] reactions, System.Action<ReactionSegment> onClicked)
        => reactionHandler.SetReactionText(richText, reactions, onClicked);

    public void ClearReactions()
        => reactionHandler.ClearReactions();

    #endregion

    #region Spiritual Property Display

    public void SetProtagonistSpiritualProperty(SpiritualProperty? property)
    {
        if (textBoxPresenter != null)
        {
            textBoxPresenter.SetSpiritualProperty(property);
        }
    }

    #endregion

    #region Extended Utility Methods

    public async UniTask ExitPortrait(PortraitPosition position)
    {
        if (portraitPresenter != null)
        {
            await portraitPresenter.Exit(position);
        }
    }

    public async UniTask SlideInBackground(string backgroundId)
    {
        if (backgroundPresenter != null)
        {
            await backgroundPresenter.SlideIn(backgroundId);
        }
    }

    #endregion
}
