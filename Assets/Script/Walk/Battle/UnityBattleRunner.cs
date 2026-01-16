using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public sealed class UnityBattleRunner : IBattleRunner
{
    private readonly Walking walking;
    private readonly MessageDropper messageDropper;

    public UnityBattleRunner(Walking walking, MessageDropper messageDropper)
    {
        this.walking = walking;
        this.messageDropper = messageDropper;
    }

    public async UniTask<BattleResult> RunBattleAsync(EncounterContext context)
    {
        if (context == null || context.Encounter == null) return BattleResult.None;

        var players = context.GameContext != null ? context.GameContext.Players : null;
        if (players == null)
        {
            Debug.LogError("UnityBattleRunner: PlayersContext is missing.");
            return BattleResult.None;
        }

        var initializer = new BattleInitializer(messageDropper);
        var metaProvider = new WalkBattleMetaProvider(players.Party, players.UIControl);
        var tracker = new BattleOutcomeTracker(metaProvider);

        var enemies = context.GameContext != null
            ? context.GameContext.GetRuntimeEnemies(context.Encounter)
            : context.Encounter.EnemyList;

        var setup = await initializer.InitializeBattle(
            enemies,
            context.GlobalSteps,
            players.Party,
            players.UIControl,
            players.SkillUI,
            players.Roster,
            players.Tuning,
            context.Encounter.EscapeRate,
            context.Encounter.EnemyCount,
            tracker);

        if (setup == null || !setup.EncounterOccurred || setup.Orchestrator == null)
        {
            return BattleResult.None;
        }

        if (walking != null)
        {
            var initialState = initializer.SetupInitialBattleUI(setup.Orchestrator);
            walking.BeginBattle(setup.Orchestrator, initialState);
        }

        await WaitForBattleEnd(setup.Orchestrator);
        return new BattleResult(true, tracker.Outcome);
    }

    private async UniTask WaitForBattleEnd(BattleOrchestrator orchestrator)
    {
        if (orchestrator == null) return;
        var token = walking != null ? walking.GetCancellationTokenOnDestroy() : CancellationToken.None;
        await UniTask.WaitUntil(() => orchestrator.Phase == BattlePhase.Completed, cancellationToken: token);
    }
}
