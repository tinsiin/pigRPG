using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// リアクションテキストのクリック検出。
/// TextMeshProのlinkタグを使用してクリック位置を特定する。
/// </summary>
public sealed class ReactionTextHandler : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private TMP_Text textComponent;

    private ReactionSegment[] currentReactions;
    private Action<ReactionSegment> onReactionClicked;

    /// <summary>
    /// リアクションを設定する。
    /// </summary>
    /// <param name="reactions">リアクションセグメント配列</param>
    /// <param name="callback">クリック時コールバック</param>
    public void Setup(ReactionSegment[] reactions, Action<ReactionSegment> callback)
    {
        currentReactions = reactions;
        onReactionClicked = callback;
    }

    /// <summary>
    /// リアクション設定をクリアする。
    /// </summary>
    public void Clear()
    {
        currentReactions = null;
        onReactionClicked = null;
    }

    /// <summary>
    /// TextMeshProコンポーネントを設定する。
    /// </summary>
    public void SetTextComponent(TMP_Text text)
    {
        textComponent = text;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (textComponent == null || currentReactions == null || currentReactions.Length == 0)
        {
            return;
        }

        // クリック位置からリンクインデックスを取得
        var linkIndex = TMP_TextUtilities.FindIntersectingLink(
            textComponent,
            eventData.position,
            eventData.pressEventCamera);

        if (linkIndex < 0)
        {
            // リンク外をクリック
            return;
        }

        // リンク情報を取得
        var linkInfo = textComponent.textInfo.linkInfo[linkIndex];
        var linkId = linkInfo.GetLinkID();

        // linkIdはstartIndexを格納している
        if (int.TryParse(linkId, out var startIndex))
        {
            var segment = FindSegmentByStartIndex(startIndex);
            if (segment != null)
            {
                Debug.Log($"[ReactionTextHandler] Clicked reaction: '{segment.Text}' (type={segment.Type})");
                onReactionClicked?.Invoke(segment);
            }
        }
        else
        {
            Debug.LogWarning($"[ReactionTextHandler] Failed to parse linkId: {linkId}");
        }
    }

    private ReactionSegment FindSegmentByStartIndex(int startIndex)
    {
        if (currentReactions == null) return null;

        foreach (var segment in currentReactions)
        {
            if (segment != null && segment.StartIndex == startIndex)
            {
                return segment;
            }
        }

        return null;
    }

    private void OnValidate()
    {
        if (textComponent == null)
        {
            textComponent = GetComponent<TMP_Text>();
        }
    }
}
