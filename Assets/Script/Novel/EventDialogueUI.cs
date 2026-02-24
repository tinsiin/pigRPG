using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// イベント会話用UI。
/// 左右ボタンで戻る/進むを選択できる。
/// </summary>
public class EventDialogueUI : MonoBehaviour
{
    [SerializeField]
    private Button prevButton;

    [SerializeField]
    private Button nextButton;

    [SerializeField]
    private Button backlogButton;

    [SerializeField]
    private Button closeButton;

    private NovelInputHub inputHub;

    private void Awake()
    {
        if (prevButton != null)
        {
            prevButton.onClick.AddListener(OnPrevButtonClicked);
        }
        if (nextButton != null)
        {
            nextButton.onClick.AddListener(OnNextButtonClicked);
        }
        if (backlogButton != null)
        {
            backlogButton.onClick.AddListener(OnBacklogButtonClicked);
        }
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(OnCloseButtonClicked);
            closeButton.gameObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (prevButton != null)
        {
            prevButton.onClick.RemoveListener(OnPrevButtonClicked);
        }
        if (nextButton != null)
        {
            nextButton.onClick.RemoveListener(OnNextButtonClicked);
        }
        if (backlogButton != null)
        {
            backlogButton.onClick.RemoveListener(OnBacklogButtonClicked);
        }
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(OnCloseButtonClicked);
        }
    }

    /// <summary>
    /// 入力ハブを設定する。
    /// </summary>
    public void SetInputHub(NovelInputHub hub)
    {
        inputHub = hub;
    }

    /// <summary>
    /// 戻るボタンの有効/無効を設定する。
    /// </summary>
    public void SetPrevButtonEnabled(bool enabled)
    {
        if (prevButton != null)
        {
            prevButton.interactable = enabled;
        }
    }

    /// <summary>
    /// 次へボタンのinteractableを設定する。
    /// </summary>
    public void SetNextButtonInteractable(bool interactable)
    {
        if (nextButton != null)
        {
            nextButton.interactable = interactable;
        }
    }

    /// <summary>
    /// 閉じるボタンの表示/非表示を設定する。
    /// </summary>
    public void SetCloseButtonVisible(bool visible)
    {
        if (closeButton != null)
        {
            closeButton.gameObject.SetActive(visible);
        }
    }

    private void OnPrevButtonClicked()
    {
        inputHub?.NotifyBack();
    }

    private void OnNextButtonClicked()
    {
        inputHub?.NotifyNext();
    }

    private void OnCloseButtonClicked()
    {
        inputHub?.NotifyNext();
    }

    private void OnBacklogButtonClicked()
    {
        inputHub?.NotifyBacklog();
    }
}
