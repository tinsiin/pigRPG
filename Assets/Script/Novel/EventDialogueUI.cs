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
}
