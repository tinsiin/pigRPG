using System.Threading;
using Cysharp.Threading.Tasks;

public interface IWalkInputProvider
{
    UniTask WaitForWalkButtonAsync(CancellationToken ct);
    bool IsWalkButtonPressed { get; }
}
