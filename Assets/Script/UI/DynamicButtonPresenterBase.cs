using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 動的ボタン生成の共通基底クラス。
/// SelectRangeButtons等と同じレイアウトパターンを提供。
/// </summary>
public abstract class DynamicButtonPresenterBase : MonoBehaviour
{
    [SerializeField]
    protected Button buttonPrefab;

    [SerializeField]
    protected RectTransform parentRect;

    [Header("Layout Settings")]
    [SerializeField]
    protected float horizontalPadding = 10f;

    [SerializeField]
    protected float verticalPadding = 10f;

    protected Vector2 buttonSize;
    protected Vector2 parentSize;
    protected float startX;
    protected float startY;
    protected readonly List<Button> buttonList = new();

    protected virtual void Awake()
    {
        if (buttonPrefab != null)
        {
            buttonSize = buttonPrefab.GetComponent<RectTransform>().sizeDelta;
        }
        if (parentRect != null)
        {
            parentSize = parentRect.rect.size;
            startX = -parentSize.x / 2 + buttonSize.x / 2 + horizontalPadding;
            startY = parentSize.y / 2 - buttonSize.y / 2 - verticalPadding;
        }
    }

    /// <summary>
    /// 全ボタンを削除してリストをクリア。
    /// </summary>
    public virtual void ClearAllButtons()
    {
        foreach (var button in buttonList)
        {
            if (button != null)
            {
                Destroy(button.gameObject);
            }
        }
        buttonList.Clear();
    }

    /// <summary>
    /// ボタンを生成し、横並びでレイアウト。
    /// 親オブジェクトの右端を超えたら次の行に移動。
    /// </summary>
    protected Button CreateButtonHorizontal(string text, ref float currentX, ref float currentY)
    {
        var button = Instantiate(buttonPrefab, parentRect);
        var rect = button.GetComponent<RectTransform>();

        // 親オブジェクトの右端を超える場合は次の行に移動
        if (currentX + buttonSize.x / 2 > parentSize.x / 2)
        {
            currentX = startX;
            currentY -= buttonSize.y + verticalPadding;
        }

        rect.anchoredPosition = new Vector2(currentX, currentY);
        currentX += buttonSize.x + horizontalPadding;

        var tmpText = button.GetComponentInChildren<TMP_Text>();
        if (tmpText != null)
        {
            tmpText.text = text;
        }

        buttonList.Add(button);
        return button;
    }

    /// <summary>
    /// ボタンを生成し、縦並びでレイアウト。
    /// </summary>
    protected Button CreateButtonVertical(string text, ref float currentY)
    {
        var button = Instantiate(buttonPrefab, parentRect);
        var rect = button.GetComponent<RectTransform>();

        // 中央揃えで縦に並べる
        rect.anchoredPosition = new Vector2(0, currentY);
        currentY -= buttonSize.y + verticalPadding;

        var tmpText = button.GetComponentInChildren<TMP_Text>();
        if (tmpText != null)
        {
            tmpText.text = text;
        }

        buttonList.Add(button);
        return button;
    }

    /// <summary>
    /// 横並びレイアウト位置をリセット。
    /// </summary>
    protected void ResetHorizontalLayout(out float currentX, out float currentY)
    {
        currentX = startX;
        currentY = startY;
    }

    /// <summary>
    /// 縦並びレイアウト位置をリセット。
    /// </summary>
    protected void ResetVerticalLayout(out float currentY)
    {
        currentY = startY;
    }

    protected virtual void OnDestroy()
    {
        ClearAllButtons();
    }
}
