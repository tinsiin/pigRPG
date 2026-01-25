using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// フィールド会話用UI。
/// 全面タップで進むだけのシンプルな入力。
/// </summary>
public class FieldDialogueUI : MonoBehaviour
{
    [SerializeField]
    private Button tapButton;

    private NovelInputHub inputHub;

    private void Awake()
    {
        if (tapButton != null)
        {
            tapButton.onClick.AddListener(OnTapButtonClicked);
        }
    }

    private void OnDestroy()
    {
        if (tapButton != null)
        {
            tapButton.onClick.RemoveListener(OnTapButtonClicked);
        }
    }

    /// <summary>
    /// 入力ハブを設定する。
    /// </summary>
    public void SetInputHub(NovelInputHub hub)
    {
        inputHub = hub;
    }

    private void OnTapButtonClicked()
    {
        inputHub?.NotifyNext();
    }
}
