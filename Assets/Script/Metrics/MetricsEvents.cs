using System;

public sealed class IntroMetricsEvent
{
    public double PlannedMs;
    public double ActualMs;
    public double DelayMs;
    public double EnemyPlacementMs;
    public DateTime Timestamp;
    public int AllyCount;
    public int EnemyCount;
    public double IntroFrameAvgMs;
    public double IntroFrameP95Ms;
    public double IntroFrameMaxMs;
    public MetricsContext Context;
}

public sealed class WalkMetricsEvent
{
    public double TotalMs;
    public DateTime Timestamp;
    public MetricsContext Context;
}

public sealed class SpanMetricsEvent
{
    public string Name;
    public double DurationMs;
    public DateTime Timestamp; // end time
    public MetricsContext Context;
}

public sealed class JitterCompletedEvent
{
    public string Name;
    public int Count;
    public double AvgMs;
    public double P95Ms;
    public double MaxMs;
    public DateTime StartTime;
    public DateTime EndTime;
    public MetricsContext Context;
}
