public sealed class BenchmarkSummary
{
    public int RequestCount;
    public int SuccessCount;
    public int FailCount;

    public double AvgIntroAvgMs;
    public double AvgIntroP95Ms;
    public double AvgIntroMaxMs;

    public double AvgActualMs;
    public double AvgWalkTotalMs;
}
