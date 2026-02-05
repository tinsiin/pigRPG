using System;
using Cysharp.Threading.Tasks;

public static class BattleReplayRunner
{
    public static UniTask ReplayAsync(BattleReplayData data, IBattleSession session)
    {
        if (data == null || session == null)
        {
            return UniTask.CompletedTask;
        }

        var inputs = data.ToInputRecords();
        var replayer = new BattleEventReplayer(session, inputs);
        return replayer.ReplayAsync();
    }

    public static UniTask ReplayAsync(BattleReplayData data, BattleOrchestrator orchestrator)
    {
        if (data == null || orchestrator == null)
        {
            return UniTask.CompletedTask;
        }

        return orchestrator.ReplayInputsAsync(data.ToInputRecords());
    }

    public static async UniTask ReplayWithFactoryAsync(
        BattleReplayData data,
        Func<int?, UniTask<IBattleSession>> sessionFactory)
    {
        if (data == null || sessionFactory == null)
        {
            return;
        }

        var session = await sessionFactory(data.GetRandomSeed());
        await ReplayAsync(data, session);
    }

    public static async UniTask ReplayWithFactoryAsync(
        BattleReplayData data,
        Func<int?, UniTask<BattleOrchestrator>> orchestratorFactory)
    {
        if (data == null || orchestratorFactory == null)
        {
            return;
        }

        var orchestrator = await orchestratorFactory(data.GetRandomSeed());
        await ReplayAsync(data, orchestrator);
    }

    public static UniTask ReplayDefaultAsync(IBattleSession session, string fileName = BattleReplayIO.DefaultFileName)
    {
        if (session == null)
        {
            return UniTask.CompletedTask;
        }

        if (!BattleReplayIO.TryLoadDefault(out var data, fileName) || data == null)
        {
            return UniTask.CompletedTask;
        }

        return ReplayAsync(data, session);
    }

    public static UniTask ReplayDefaultAsync(BattleOrchestrator orchestrator, string fileName = BattleReplayIO.DefaultFileName)
    {
        if (orchestrator == null)
        {
            return UniTask.CompletedTask;
        }

        if (!BattleReplayIO.TryLoadDefault(out var data, fileName) || data == null)
        {
            return UniTask.CompletedTask;
        }

        return ReplayAsync(data, orchestrator);
    }

    public static UniTask ReplayDefaultWithFactoryAsync(
        Func<int?, UniTask<IBattleSession>> sessionFactory,
        string fileName = BattleReplayIO.DefaultFileName)
    {
        if (sessionFactory == null)
        {
            return UniTask.CompletedTask;
        }

        if (!BattleReplayIO.TryLoadDefault(out var data, fileName) || data == null)
        {
            return UniTask.CompletedTask;
        }

        return ReplayWithFactoryAsync(data, sessionFactory);
    }

    public static UniTask ReplayDefaultWithFactoryAsync(
        Func<int?, UniTask<BattleOrchestrator>> orchestratorFactory,
        string fileName = BattleReplayIO.DefaultFileName)
    {
        if (orchestratorFactory == null)
        {
            return UniTask.CompletedTask;
        }

        if (!BattleReplayIO.TryLoadDefault(out var data, fileName) || data == null)
        {
            return UniTask.CompletedTask;
        }

        return ReplayWithFactoryAsync(data, orchestratorFactory);
    }

    public static async UniTask<BattleReplayVerificationResult> ReplayAndVerifyAsync(
        BattleReplayData data,
        IBattleSession session)
    {
        if (data == null || session == null)
        {
            return BattleReplayVerificationResult.NotVerified("Replay data or session is null.");
        }

        var collector = new BattleEventCollector();
        var bus = session.EventBus;
        bus?.Register(collector);
        try
        {
            await ReplayAsync(data, session);
        }
        finally
        {
            bus?.Unregister(collector);
        }

        return BattleReplayVerifier.Verify(data.ToEvents(), collector.Events);
    }

    public static UniTask<BattleReplayVerificationResult> ReplayAndVerifyAsync(
        BattleReplayData data,
        BattleOrchestrator orchestrator)
    {
        if (orchestrator == null)
        {
            return UniTask.FromResult(BattleReplayVerificationResult.NotVerified("Orchestrator is null."));
        }

        return ReplayAndVerifyAsync(data, orchestrator.Session);
    }

    public static async UniTask<BattleReplayVerificationResult> ReplayDefaultAndVerifyWithFactoryAsync(
        Func<int?, UniTask<IBattleSession>> sessionFactory,
        string fileName = BattleReplayIO.DefaultFileName)
    {
        if (sessionFactory == null)
        {
            return BattleReplayVerificationResult.NotVerified("Session factory is null.");
        }

        if (!BattleReplayIO.TryLoadDefault(out var data, fileName) || data == null)
        {
            return BattleReplayVerificationResult.NotVerified("Replay data not found.");
        }

        var session = await sessionFactory(data.GetRandomSeed());
        return await ReplayAndVerifyAsync(data, session);
    }

    public static async UniTask<BattleReplayVerificationResult> ReplayDefaultAndVerifyWithFactoryAsync(
        Func<int?, UniTask<BattleOrchestrator>> orchestratorFactory,
        string fileName = BattleReplayIO.DefaultFileName)
    {
        if (orchestratorFactory == null)
        {
            return BattleReplayVerificationResult.NotVerified("Orchestrator factory is null.");
        }

        if (!BattleReplayIO.TryLoadDefault(out var data, fileName) || data == null)
        {
            return BattleReplayVerificationResult.NotVerified("Replay data not found.");
        }

        var orchestrator = await orchestratorFactory(data.GetRandomSeed());
        return await ReplayAndVerifyAsync(data, orchestrator);
    }
}
