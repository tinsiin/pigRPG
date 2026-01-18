using System;

public readonly struct WalkCountersSnapshot
{
    public int GlobalSteps { get; }
    public int NodeSteps { get; }
    public int TrackProgress { get; }

    public WalkCountersSnapshot(int globalSteps, int nodeSteps, int trackProgress)
    {
        GlobalSteps = globalSteps;
        NodeSteps = nodeSteps;
        TrackProgress = trackProgress;
    }
}

public sealed class WalkCounters
{
    public int GlobalSteps { get; private set; }
    public int NodeSteps { get; private set; }
    public int TrackProgress { get; private set; }

    public WalkCountersSnapshot PeekNext(int stepDelta)
    {
        return new WalkCountersSnapshot(
            GlobalSteps + stepDelta,
            NodeSteps + stepDelta,
            TrackProgress + stepDelta);
    }

    public void Advance(int stepDelta)
    {
        GlobalSteps += stepDelta;
        NodeSteps += stepDelta;
        TrackProgress += stepDelta;
    }

    public void Rewind(int stepDelta)
    {
        if (stepDelta <= 0) return;
        GlobalSteps = Math.Max(0, GlobalSteps - stepDelta);
        NodeSteps = Math.Max(0, NodeSteps - stepDelta);
        TrackProgress = Math.Max(0, TrackProgress - stepDelta);
    }

    public void ResetNodeSteps()
    {
        NodeSteps = 0;
        TrackProgress = 0;
    }

    public void ResetTrackProgress()
    {
        TrackProgress = 0;
    }

    public void AdvanceTrackProgress(int stepDelta)
    {
        TrackProgress += stepDelta;
    }

    public void SetGlobalSteps(int value)
    {
        GlobalSteps = Math.Max(0, value);
    }

    public void SetNodeSteps(int value)
    {
        NodeSteps = Math.Max(0, value);
    }

    public void SetTrackProgress(int value)
    {
        TrackProgress = Math.Max(0, value);
    }
}
