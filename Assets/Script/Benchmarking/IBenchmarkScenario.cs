using System.Threading;
using Cysharp.Threading.Tasks;

/// <summary>
/// 1回分の実行（Walk(1) など）をカプセル化するシナリオ。
/// </summary>
public interface IBenchmarkScenario
{
    string Name { get; }
    UniTask<BenchmarkRunResult> RunOnceAsync(CancellationToken ct);
}
