using System;
using System.Collections.Generic;

/// <summary>
/// 会話履歴（バックログ）エントリ。
/// </summary>
[Serializable]
public sealed class BacklogEntry
{
    public int StepIndex;
    public string Speaker;
    public string Text;
    public string LeftCharacterId;
    public string RightCharacterId;
    public string BackgroundId;
    public DisplayMode DisplayMode;

    public BacklogEntry() { }

    public BacklogEntry(int stepIndex, DialogueStep step)
    {
        StepIndex = stepIndex;
        Speaker = step.Speaker;
        Text = step.Text;
        LeftCharacterId = step.LeftPortrait?.CharacterId;
        RightCharacterId = step.RightPortrait?.CharacterId;
        BackgroundId = step.BackgroundId;
        DisplayMode = step.DisplayMode;
    }
}

/// <summary>
/// 会話履歴（バックログ）管理。
/// 戻る機能とログ表示に使用。
/// </summary>
public sealed class DialogueBacklog
{
    private readonly List<BacklogEntry> entries = new();
    private readonly int maxEntries;

    public DialogueBacklog(int maxEntries = 100)
    {
        this.maxEntries = maxEntries;
    }

    public IReadOnlyList<BacklogEntry> Entries => entries;
    public int Count => entries.Count;

    /// <summary>
    /// エントリを追加する。
    /// </summary>
    public void Add(int stepIndex, DialogueStep step)
    {
        if (step == null) return;

        var entry = new BacklogEntry(stepIndex, step);
        entries.Add(entry);

        // 最大件数を超えたら古いエントリを削除
        while (entries.Count > maxEntries)
        {
            entries.RemoveAt(0);
        }
    }

    /// <summary>
    /// 指定インデックスまでのエントリを保持し、それ以降を削除する。
    /// 戻る機能で使用。
    /// </summary>
    public void TruncateTo(int stepIndex)
    {
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            if (entries[i].StepIndex > stepIndex)
            {
                entries.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// 指定ステップのエントリを取得する。
    /// </summary>
    public BacklogEntry GetEntry(int stepIndex)
    {
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            if (entries[i].StepIndex == stepIndex)
            {
                return entries[i];
            }
        }
        return null;
    }

    /// <summary>
    /// 最新のN件を取得する。
    /// </summary>
    public List<BacklogEntry> GetRecent(int count)
    {
        var result = new List<BacklogEntry>();
        var start = Math.Max(0, entries.Count - count);
        for (int i = start; i < entries.Count; i++)
        {
            result.Add(entries[i]);
        }
        return result;
    }

    /// <summary>
    /// クリアする。
    /// </summary>
    public void Clear()
    {
        entries.Clear();
    }
}
