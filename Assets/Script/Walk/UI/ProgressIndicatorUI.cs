using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// ゲートと出口の進捗を表示するUI
/// </summary>
public sealed class ProgressIndicatorUI : MonoBehaviour
{
    [SerializeField] private TMP_Text progressText;
    [SerializeField] private TMP_Text stepsRemainingText;

    private readonly StringBuilder sb = new StringBuilder();
    private ProgressSnapshot lastSnapshot;

    public void UpdateDisplay(ProgressSnapshot snapshot)
    {
        lastSnapshot = snapshot;

        if (progressText != null)
        {
            progressText.text = BuildProgressSequence(snapshot);
        }

        if (stepsRemainingText != null)
        {
            stepsRemainingText.text = BuildStepsRemaining(snapshot);
        }
    }

    public void Clear()
    {
        if (progressText != null) progressText.text = string.Empty;
        if (stepsRemainingText != null) stepsRemainingText.text = string.Empty;
    }

    private string BuildProgressSequence(ProgressSnapshot snapshot)
    {
        if (snapshot.Entries == null || snapshot.Entries.Count == 0)
        {
            return string.Empty;
        }

        sb.Clear();

        for (int i = 0; i < snapshot.Entries.Count; i++)
        {
            if (i > 0) sb.Append(" \u2192 ");

            var entry = snapshot.Entries[i];

            if (entry.Type == ProgressEntry.EntryType.Gate)
            {
                if (entry.IsCleared)
                {
                    sb.Append('[');
                    sb.Append(entry.DisplayLabel);
                    sb.Append(']');
                }
                else if (entry.IsCoolingDown)
                {
                    sb.Append('(');
                    sb.Append(entry.DisplayLabel);
                    sb.Append(')');
                }
                else
                {
                    sb.Append(entry.DisplayLabel);
                }
            }
            else
            {
                // Exit
                sb.Append("(Exit)");
            }
        }

        return sb.ToString();
    }

    private string BuildStepsRemaining(ProgressSnapshot snapshot)
    {
        if (!snapshot.HasNextEntry)
        {
            return string.Empty;
        }

        var nextEntry = snapshot.GetNextEntry();
        if (!nextEntry.HasValue)
        {
            return string.Empty;
        }

        var entry = nextEntry.Value;

        // ゲートがクリアされていないためブロック中（RequireAllGatesCleared=trueの場合のみ）
        if (entry.Type == ProgressEntry.EntryType.Exit &&
            snapshot.ExitRequiresAllGatesCleared &&
            snapshot.RemainingGateCount > 0)
        {
            return $"Exit: Gate\u6b8b\u308a{snapshot.RemainingGateCount}";
        }

        // 歩数モード
        if (snapshot.StepsToNextEntry.HasValue)
        {
            var steps = snapshot.StepsToNextEntry.Value;
            if (entry.Type == ProgressEntry.EntryType.Gate)
            {
                return steps > 0
                    ? $"Gate {entry.DisplayLabel}: {steps}\u6b69\u5148"
                    : $"Gate {entry.DisplayLabel}: \u5230\u9054";
            }
            else
            {
                return steps > 0
                    ? $"Exit: {steps}\u6b69\u5148"
                    : "Exit: \u5230\u9054";
            }
        }

        // 確率モード
        if (snapshot.ProbabilityOfNextEntry.HasValue)
        {
            var percent = Mathf.RoundToInt(snapshot.ProbabilityOfNextEntry.Value * 100f);
            return $"Exit: {percent}%";
        }

        return string.Empty;
    }
}
