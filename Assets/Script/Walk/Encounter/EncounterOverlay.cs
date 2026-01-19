using System;
using System.Collections.Generic;

[Serializable]
public sealed class EncounterOverlay
{
    public string Id;
    public float Multiplier = 1f;
    public int RemainingSteps = -1;
    public bool Persistent;

    public EncounterOverlay() { }

    public EncounterOverlay(string id, float multiplier, int remainingSteps, bool persistent)
    {
        Id = id;
        Multiplier = multiplier;
        RemainingSteps = remainingSteps;
        Persistent = persistent;
    }

    public bool IsExpired => RemainingSteps == 0;
    public bool IsInfinite => RemainingSteps < 0;
}

[Serializable]
public sealed class EncounterOverlayData
{
    public string Id;
    public float Multiplier;
    public int RemainingSteps;

    public EncounterOverlayData() { }

    public EncounterOverlayData(EncounterOverlay overlay)
    {
        Id = overlay.Id;
        Multiplier = overlay.Multiplier;
        RemainingSteps = overlay.RemainingSteps;
    }

    public EncounterOverlay ToOverlay()
    {
        return new EncounterOverlay(Id, Multiplier, RemainingSteps, true);
    }
}

public sealed class EncounterOverlayStack
{
    private readonly List<EncounterOverlay> overlays = new();

    public void Push(EncounterOverlay overlay)
    {
        if (overlay == null) return;
        // steps=0 means immediately expired, don't add
        if (overlay.RemainingSteps == 0) return;
        overlays.Add(overlay);
    }

    public void Push(string id, float multiplier, int steps, bool persistent)
    {
        // steps=0 means immediately expired, don't add
        if (steps == 0) return;
        overlays.Add(new EncounterOverlay(id, multiplier, steps, persistent));
    }

    public void Remove(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        overlays.RemoveAll(o => o.Id == id);
    }

    public void AdvanceStep()
    {
        for (int i = overlays.Count - 1; i >= 0; i--)
        {
            var overlay = overlays[i];
            if (overlay.RemainingSteps > 0)
            {
                overlay.RemainingSteps--;
                if (overlay.RemainingSteps == 0)
                {
                    overlays.RemoveAt(i);
                }
            }
        }
    }

    public float GetCombinedMultiplier()
    {
        float result = 1f;
        foreach (var overlay in overlays)
        {
            result *= overlay.Multiplier;
        }
        return result;
    }

    public void Clear()
    {
        overlays.Clear();
    }

    public List<EncounterOverlayData> ExportPersistent()
    {
        var result = new List<EncounterOverlayData>();
        foreach (var overlay in overlays)
        {
            if (overlay.Persistent)
            {
                result.Add(new EncounterOverlayData(overlay));
            }
        }
        return result;
    }

    /// <summary>
    /// デバッグ用: 全てのアクティブなオーバーレイを取得（永続/非永続を問わず）
    /// </summary>
    public IReadOnlyList<EncounterOverlay> GetAllActive()
    {
        return overlays;
    }

    public void ImportPersistent(List<EncounterOverlayData> dataList)
    {
        // 既存のオーバーレイをクリアしてから復元（累積防止）
        overlays.Clear();
        if (dataList == null) return;
        foreach (var data in dataList)
        {
            overlays.Add(data.ToOverlay());
        }
    }
}
