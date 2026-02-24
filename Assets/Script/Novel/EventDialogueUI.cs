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

    private void OnPrevButtonClicked()
    {
        inputHub?.NotifyBack();
    }

    private void OnNextButtonClicked()
    {
        inputHub?.NotifyNext();
    }

    private void OnBacklogButtonClicked()
    {
        inputHub?.NotifyBacklog();
    }
}
