using System.Linq;
using System.Text;
using UnityEngine;

/// <summary>
/// リアクションセグメントを含むテキストをTMPリッチテキストに変換する。
/// </summary>
public static class ReactionTextBuilder
{
    /// <summary>
    /// 平文とリアクションセグメントからTMPリッチテキストを生成。
    /// リアクション部分は色付き + linkタグ付きになる。
    /// </summary>
    /// <param name="plainText">元のテキスト</param>
    /// <param name="reactions">リアクションセグメント配列</param>
    /// <returns>TMPリッチテキスト</returns>
    public static string Build(string plainText, ReactionSegment[] reactions)
    {
        if (string.IsNullOrEmpty(plainText)) return string.Empty;
        if (reactions == null || reactions.Length == 0) return plainText;

        // セグメントを開始位置でソート
        var sorted = reactions
            .Where(r => r != null && !string.IsNullOrEmpty(r.Text))
            .OrderBy(r => r.StartIndex)
            .ToArray();

        if (sorted.Length == 0) return plainText;

        var sb = new StringBuilder();
        var currentIndex = 0;

        foreach (var segment in sorted)
        {
            // 範囲チェック
            if (segment.StartIndex < 0 || segment.StartIndex >= plainText.Length)
            {
                Debug.LogWarning($"ReactionTextBuilder: Invalid startIndex {segment.StartIndex} for text length {plainText.Length}");
                continue;
            }

            // リアクション前の通常テキスト
            if (segment.StartIndex > currentIndex)
            {
                var normalLength = Mathf.Min(segment.StartIndex - currentIndex, plainText.Length - currentIndex);
                if (normalLength > 0)
                {
                    sb.Append(plainText.Substring(currentIndex, normalLength));
                }
            }

            // リアクションテキスト（色付き + リンクタグ）
            // linkのIDにはstartIndexを使用（クリック時の特定用）
            var colorHex = ColorUtility.ToHtmlStringRGB(segment.Color);
            sb.Append($"<link=\"{segment.StartIndex}\"><color=#{colorHex}>{segment.Text}</color></link>");

            currentIndex = segment.EndIndex;
        }

        // 残りの通常テキスト
        if (currentIndex < plainText.Length)
        {
            sb.Append(plainText.Substring(currentIndex));
        }

        return sb.ToString();
    }

    /// <summary>
    /// リアクションセグメントが有効かチェック。
    /// </summary>
    public static bool ValidateSegments(string plainText, ReactionSegment[] reactions)
    {
        if (string.IsNullOrEmpty(plainText) || reactions == null) return true;

        foreach (var segment in reactions)
        {
            if (segment == null) continue;

            // 開始位置チェック
            if (segment.StartIndex < 0 || segment.StartIndex >= plainText.Length)
            {
                Debug.LogError($"ReactionSegment startIndex {segment.StartIndex} is out of range [0, {plainText.Length - 1}]");
                return false;
            }

            // 終了位置チェック
            if (segment.EndIndex > plainText.Length)
            {
                Debug.LogError($"ReactionSegment endIndex {segment.EndIndex} exceeds text length {plainText.Length}");
                return false;
            }

            // テキスト一致チェック
            var expectedText = plainText.Substring(segment.StartIndex, segment.Text.Length);
            if (expectedText != segment.Text)
            {
                Debug.LogWarning($"ReactionSegment text mismatch: expected '{expectedText}', got '{segment.Text}'");
            }
        }

        return true;
    }
}
