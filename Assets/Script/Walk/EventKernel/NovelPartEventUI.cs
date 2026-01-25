using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// INovelEventUIの実装。
/// 各Presenterを統合してノベルパート表示を制御する。
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
    [SerializeField] private GameObject backlogPanel;
    [SerializeField] private GameObject backButton;

    [Header("Reaction")]
    [SerializeField] private ReactionTextHandler reactionTextHandler;

    [Header("Message Dropper")]
    [SerializeField] private MessageDropper messageDropper;

    [Header("Novel Input UI")]
    [SerializeField] private FieldDialogueUI fieldDialogueUI;
    [SerializeField] private EventDialogueUI eventDialogueUI;
    [SerializeField] private NovelChoicePresenter novelChoicePresenter;

    private DisplayMode currentDisplayMode = DisplayMode.Dinoid;
    private NovelInputHub inputHub;
    private bool backRequested;
    private bool backlogRequested;
    private System.Action<ReactionSegment> currentReactionCallback;

    public DisplayMode CurrentDisplayMode => currentDisplayMode;
    public INovelInputProvider InputProvider => inputHub;

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
        }

        if (textBoxPresenter != null)
        {
            textBoxPresenter.SetPortraitDatabase(portraitDatabase);
            textBoxPresenter.Initialize(currentDisplayMode);
        }
    }

    public void SetTabState(TabState state)
    {
        if (UIStateHub.UserState != null)
        {
            UIStateHub.UserState.Value = state;
        }

        // 戻るボタンの有効/無効を状態に応じて設定
        if (eventDialogueUI != null)
        {
            eventDialogueUI.SetPrevButtonEnabled(state == TabState.EventDialogue);
        }
    }

    #region IEventUI Implementation (既存互換)

    public void ShowMessage(string message)
    {
        // TextBoxPresenter経由でメッセージ表示
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

        // TabStateをNovelChoiceに切り替え
        SetTabState(TabState.NovelChoice);

        // 選択肢ボタンを表示
        novelChoicePresenter.ShowChoices(labels);

        // 選択を待つ
        var selectedIndex = await inputHub.WaitForChoiceAsync(labels.Length);

        return selectedIndex;
    }

    #endregion

    #region INovelEventUI Implementation

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
            noisePresenter.Play(entries);
        }
        else if (entries != null)
        {
            foreach (var entry in entries)
            {
                Debug.Log($"[NovelPartEventUI] PlayNoise: [{entry.Speaker}] {entry.Text}");
            }
        }
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

        // ディノイドモード時はMessageDropperにも送信
        if (currentDisplayMode == DisplayMode.Dinoid && messageDropper != null)
        {
            messageDropper.CreateMessage(text);
        }

        await UniTask.Yield();
    }

    public async UniTask SwitchTextBox(DisplayMode mode)
    {
        if (currentDisplayMode == mode) return;

        currentDisplayMode = mode;

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

    #endregion

    #region Backlog and Back Navigation

    public async UniTask ShowBacklog(DialogueBacklog backlog)
    {
        if (backlogPanel != null)
        {
            // TODO: バックログUIにエントリを表示
            backlogPanel.SetActive(true);
        }
        else
        {
            Debug.Log($"[NovelPartEventUI] ShowBacklog: {backlog?.Count ?? 0} entries");
        }
        await UniTask.Yield();
    }

    public void HideBacklog()
    {
        if (backlogPanel != null)
        {
            backlogPanel.SetActive(false);
        }
        else
        {
            Debug.Log("[NovelPartEventUI] HideBacklog");
        }
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
        var result = backlogRequested;
        backlogRequested = false;
        return result;
    }

    public void RestoreState(DialogueStateSnapshot snapshot)
    {
        if (snapshot == null) return;

        // モード復元
        currentDisplayMode = snapshot.DisplayMode;
        textBoxPresenter?.SetModeImmediate(snapshot.DisplayMode);

        // 立ち絵復元（即座に、トランジションなし）
        portraitPresenter?.RestoreImmediate(snapshot.LeftPortrait, snapshot.RightPortrait);

        // 背景復元
        if (snapshot.HasBackground)
        {
            backgroundPresenter?.ShowImmediate(snapshot.BackgroundId);
        }
        else
        {
            backgroundPresenter?.HideImmediate();
        }
    }

    /// <summary>
    /// UIボタンから呼び出される：戻るリクエスト。
    /// </summary>
    public void OnBackButtonClicked()
    {
        backRequested = true;
    }

    /// <summary>
    /// UIボタンから呼び出される：バックログリクエスト。
    /// </summary>
    public void OnBacklogButtonClicked()
    {
        backlogRequested = true;
    }

    #endregion

    #region Utility Methods

    public void ClearAll()
    {
        portraitPresenter?.ClearAll();
        backgroundPresenter?.HideImmediate();
        noisePresenter?.ClearAll();
        textBoxPresenter?.Clear();
    }

    public void HideAll()
    {
        ClearReactions();
        portraitPresenter?.ClearAll();
        backgroundPresenter?.HideImmediate();
        noisePresenter?.ClearAll();
        textBoxPresenter?.Hide();
    }

    public async UniTask HideAllAsync()
    {
        ClearReactions();
        portraitPresenter?.ClearAll();
        backgroundPresenter?.HideImmediate();
        noisePresenter?.ClearAll();
        textBoxPresenter?.Hide();
        await UniTask.Yield();
    }

    #endregion

    #region Reaction System

    public void SetReactionText(string richText, ReactionSegment[] reactions, System.Action<ReactionSegment> onClicked)
    {
        currentReactionCallback = onClicked;

        // TextBoxPresenterにリッチテキストを設定
        if (textBoxPresenter != null)
        {
            textBoxPresenter.SetRichText(richText);

            // ReactionTextHandlerに現在のTMP_Textを設定
            if (reactionTextHandler != null)
            {
                var textComponent = textBoxPresenter.GetCurrentTextComponent();
                reactionTextHandler.SetTextComponent(textComponent);
                reactionTextHandler.Setup(reactions, OnReactionClicked);
            }
            else
            {
                Debug.LogWarning("[NovelPartEventUI] ReactionTextHandler is not assigned");
            }
        }
        else
        {
            Debug.LogWarning("[NovelPartEventUI] TextBoxPresenter is not assigned");
        }
    }

    public void ClearReactions()
    {
        currentReactionCallback = null;
        reactionTextHandler?.Clear();
    }

    private void OnReactionClicked(ReactionSegment segment)
    {
        // コールバックを呼び出す
        currentReactionCallback?.Invoke(segment);
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
