using System;
using System.Collections.Generic;

public readonly struct BattleReplayVerificationResult
{
    public bool HasExpected { get; }
    public bool IsMatch { get; }
    public int ExpectedCount { get; }
    public int ActualCount { get; }
    public int FirstMismatchIndex { get; }
    public string Message { get; }

    private BattleReplayVerificationResult(
        bool hasExpected,
        bool isMatch,
        int expectedCount,
        int actualCount,
        int firstMismatchIndex,
        string message)
    {
        HasExpected = hasExpected;
        IsMatch = isMatch;
        ExpectedCount = expectedCount;
        ActualCount = actualCount;
        FirstMismatchIndex = firstMismatchIndex;
        Message = message ?? "";
    }

    public static BattleReplayVerificationResult NotVerified(string message)
    {
        return new BattleReplayVerificationResult(false, false, 0, 0, -1, message);
    }

    public static BattleReplayVerificationResult Match(int count)
    {
        return new BattleReplayVerificationResult(true, true, count, count, -1, "Replay events match.");
    }

    public static BattleReplayVerificationResult Mismatch(int expectedCount, int actualCount, int index, string message)
    {
        return new BattleReplayVerificationResult(true, false, expectedCount, actualCount, index, message);
    }
}

public sealed class BattleEventCollector : IBattleEventSink
{
    private readonly List<BattleEvent> _events = new();
    public IReadOnlyList<BattleEvent> Events => _events;

    public void OnBattleEvent(BattleEvent battleEvent)
    {
        _events.Add(battleEvent);
    }
}

public static class BattleReplayVerifier
{
    public static BattleReplayVerificationResult Verify(
        IReadOnlyList<BattleEvent> expected,
        IReadOnlyList<BattleEvent> actual)
    {
        if (expected == null || expected.Count == 0)
        {
            return BattleReplayVerificationResult.NotVerified("Expected events are empty.");
        }

        var actualList = actual ?? Array.Empty<BattleEvent>();
        var min = Math.Min(expected.Count, actualList.Count);
        for (var i = 0; i < min; i++)
        {
            if (!EqualsForReplay(expected[i], actualList[i]))
            {
                return BattleReplayVerificationResult.Mismatch(
                    expected.Count,
                    actualList.Count,
                    i,
                    $"Replay event mismatch at index {i}.");
            }
        }

        if (expected.Count != actualList.Count)
        {
            return BattleReplayVerificationResult.Mismatch(
                expected.Count,
                actualList.Count,
                min,
                "Replay event count mismatch.");
        }

        return BattleReplayVerificationResult.Match(expected.Count);
    }

    private static bool EqualsForReplay(BattleEvent a, BattleEvent b)
    {
        return a.Type == b.Type
            && a.Important == b.Important
            && a.TurnCount == b.TurnCount
            && a.Immediate == b.Immediate
            && a.WaitForIntro == b.WaitForIntro
            && a.HasSingleTargetReservation == b.HasSingleTargetReservation
            && string.Equals(a.ActorName ?? "", b.ActorName ?? "", StringComparison.Ordinal)
            && string.Equals(a.Message ?? "", b.Message ?? "", StringComparison.Ordinal);
    }
}
