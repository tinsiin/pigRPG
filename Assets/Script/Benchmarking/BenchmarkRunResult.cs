public sealed class BenchmarkRunResult
{
    public double IntroAvgMs;   // JIT avg
    public double IntroP95Ms;   // JIT p95
    public double IntroMaxMs;   // JIT max
    public double ActualMs;     // A
    public double WalkTotalMs;  // W
    public double LoopMs;       // Runner.Loop total duration per run
    public bool Success;
    public string ErrorMessage;
}
