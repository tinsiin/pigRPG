using System;
using System.Collections.Generic;

[Serializable]
public sealed class VarietyHistory
{
    private readonly Queue<string> history = new();
    private int maxDepth;

    public int MaxDepth
    {
        get => maxDepth;
        set => maxDepth = value < 0 ? 0 : value;
    }

    public VarietyHistory(int depth = 0)
    {
        maxDepth = depth < 0 ? 0 : depth;
    }

    public void Record(string sideObjectId)
    {
        if (string.IsNullOrEmpty(sideObjectId)) return;
        if (maxDepth <= 0) return;

        history.Enqueue(sideObjectId);
        while (history.Count > maxDepth)
        {
            history.Dequeue();
        }
    }

    public bool Contains(string sideObjectId)
    {
        if (string.IsNullOrEmpty(sideObjectId)) return false;
        if (maxDepth <= 0) return false;

        foreach (var id in history)
        {
            if (id == sideObjectId) return true;
        }
        return false;
    }

    public void Clear()
    {
        history.Clear();
    }

    public List<string> ToList()
    {
        return new List<string>(history);
    }

    public void FromList(List<string> list)
    {
        history.Clear();
        if (list == null) return;
        foreach (var id in list)
        {
            if (maxDepth > 0 && history.Count >= maxDepth) break;
            history.Enqueue(id);
        }
    }
}
