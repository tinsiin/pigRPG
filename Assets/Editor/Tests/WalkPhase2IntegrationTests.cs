using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// Phase 2 Walk system integration tests
/// Tests interaction between GateResolver, AnchorManager, and GameContext
/// </summary>
[TestFixture]
public class WalkPhase2IntegrationTests
{
    #region Gate + Context Integration

    [Test]
    public void GateResolver_WithGameContext_TracksStateCorrectly()
    {
        var context = new GameContext(null);
        var resolver = new GateResolver();
        context.GateResolver = resolver;

        // Simulate gate states
        var snapshot = new Dictionary<string, GateRuntimeState>
        {
            ["gate1"] = new GateRuntimeState("gate1", 10),
            ["gate2"] = new GateRuntimeState("gate2", 20)
        };
        resolver.RestoreFromSnapshot(snapshot);

        // Clear first gate
        resolver.GetState("gate1").IsCleared = true;

        // Take snapshot and restore to new resolver
        var savedSnapshot = resolver.TakeSnapshot();
        var newResolver = new GateResolver();
        newResolver.RestoreFromSnapshot(savedSnapshot);

        Assert.IsTrue(newResolver.GetState("gate1").IsCleared);
        Assert.IsFalse(newResolver.GetState("gate2").IsCleared);
    }

    #endregion

    #region Anchor + Gate State Integration

    [Test]
    public void AnchorManager_PreservesGateStateOnRewind()
    {
        var context = new GameContext(null);
        var resolver = new GateResolver();
        var anchorManager = new AnchorManager();
        context.GateResolver = resolver;
        context.AnchorManager = anchorManager;

        // Setup initial gate state
        var initialSnapshot = new Dictionary<string, GateRuntimeState>
        {
            ["gate1"] = new GateRuntimeState("gate1", 10) { IsCleared = false }
        };
        resolver.RestoreFromSnapshot(initialSnapshot);

        // Create anchor before clearing gate
        anchorManager.CreateAnchor("checkpoint", context, resolver, AnchorScope.Region);

        // Clear the gate
        resolver.GetState("gate1").IsCleared = true;
        Assert.IsTrue(resolver.GetState("gate1").IsCleared);

        // Rewind to anchor with PositionAndState mode
        anchorManager.RewindToAnchor("checkpoint", context, resolver, RewindMode.PositionAndState);

        // Gate should be restored to uncleared state
        Assert.IsFalse(resolver.GetState("gate1").IsCleared);
    }

    [Test]
    public void AnchorManager_PositionOnlyMode_DoesNotRestoreGateState()
    {
        var context = new GameContext(null);
        var resolver = new GateResolver();
        var anchorManager = new AnchorManager();
        context.GateResolver = resolver;
        context.AnchorManager = anchorManager;

        // Setup initial gate state
        var initialSnapshot = new Dictionary<string, GateRuntimeState>
        {
            ["gate1"] = new GateRuntimeState("gate1", 10) { IsCleared = false }
        };
        resolver.RestoreFromSnapshot(initialSnapshot);

        // Create anchor
        anchorManager.CreateAnchor("checkpoint", context, resolver, AnchorScope.Region);

        // Clear the gate
        resolver.GetState("gate1").IsCleared = true;

        // Rewind with PositionOnly mode
        anchorManager.RewindToAnchor("checkpoint", context, resolver, RewindMode.PositionOnly);

        // Gate should remain cleared (state not restored)
        Assert.IsTrue(resolver.GetState("gate1").IsCleared);
    }

    #endregion

    #region WalkProgressData Integration

    [Test]
    public void WalkProgressData_SavesAndRestoresGateStates()
    {
        var context = new GameContext(null);
        var resolver = new GateResolver();
        var anchorManager = new AnchorManager();
        context.GateResolver = resolver;
        context.AnchorManager = anchorManager;

        // Setup state
        context.Counters.Advance(50);
        context.SetFlag("testFlag", true);
        context.SetCounter("score", 100);

        var gateSnapshot = new Dictionary<string, GateRuntimeState>
        {
            ["gate1"] = new GateRuntimeState("gate1", 10) { IsCleared = true },
            ["gate2"] = new GateRuntimeState("gate2", 20) { FailCount = 2 }
        };
        resolver.RestoreFromSnapshot(gateSnapshot);

        // Save progress
        var saveData = WalkProgressData.FromContext(context);

        // Verify save data
        Assert.AreEqual(50, saveData.GlobalSteps);
        Assert.AreEqual(2, saveData.GateStates.Count);

        var savedGate1 = saveData.GateStates.Find(g => g.GateId == "gate1");
        Assert.IsNotNull(savedGate1);
        Assert.IsTrue(savedGate1.IsCleared);

        var savedGate2 = saveData.GateStates.Find(g => g.GateId == "gate2");
        Assert.IsNotNull(savedGate2);
        Assert.AreEqual(2, savedGate2.FailCount);
    }

    [Test]
    public void WalkProgressData_RestoresFromSaveData()
    {
        // Create save data
        var saveData = new WalkProgressData
        {
            GlobalSteps = 75,
            CurrentNodeId = "node1",
            NodeSeed = 12345,
            VarietyHistoryIndex = 5,
            Flags = new List<FlagEntry> { new FlagEntry("flag1", true) },
            Counters = new List<CounterEntry> { new CounterEntry("counter1", 42) },
            GateStates = new List<GateRuntimeStateData>
            {
                new GateRuntimeStateData { GateId = "gate1", ResolvedPosition = 10, IsCleared = true }
            },
            Anchors = new List<WalkAnchorData>()
        };

        // Create fresh context
        var context = new GameContext(null);
        var resolver = new GateResolver();
        var anchorManager = new AnchorManager();
        context.GateResolver = resolver;
        context.AnchorManager = anchorManager;

        // Restore
        saveData.ApplyToContext(context);

        // Verify restoration
        Assert.AreEqual(75, context.Counters.GlobalSteps);
        Assert.AreEqual("node1", context.WalkState.CurrentNodeId);
        Assert.AreEqual(12345u, context.WalkState.NodeSeed);
        Assert.IsTrue(context.HasFlag("flag1"));
        Assert.AreEqual(42, context.GetCounter("counter1"));
        Assert.IsTrue(resolver.GetState("gate1").IsCleared);
    }

    #endregion

    #region Counter Reset Integration

    [Test]
    public void GateReset_NodeStepsOnly_ResetsCorrectly()
    {
        var context = new GameContext(null);
        context.Counters.Advance(20);

        // Verify initial state
        Assert.AreEqual(20, context.Counters.GlobalSteps);
        Assert.AreEqual(20, context.Counters.NodeSteps);
        Assert.AreEqual(20, context.Counters.TrackProgress);

        // Reset only node steps
        context.Counters.ResetNodeSteps();

        Assert.AreEqual(20, context.Counters.GlobalSteps);
        Assert.AreEqual(0, context.Counters.NodeSteps);
        Assert.AreEqual(0, context.Counters.TrackProgress); // ResetNodeSteps also resets TrackProgress
    }

    [Test]
    public void GateReset_TrackProgressOnly_ResetsCorrectly()
    {
        var context = new GameContext(null);
        context.Counters.Advance(20);

        context.Counters.ResetTrackProgress();

        Assert.AreEqual(20, context.Counters.GlobalSteps);
        Assert.AreEqual(20, context.Counters.NodeSteps);
        Assert.AreEqual(0, context.Counters.TrackProgress);
    }

    [Test]
    public void GateReset_ProgressKeyOnly_ResetsCorrectCounter()
    {
        var context = new GameContext(null);
        context.SetCounter("dungeonProgress", 50);
        context.SetCounter("otherCounter", 100);

        // Simulate ProgressKeyOnly reset
        context.SetCounter("dungeonProgress", 0);

        Assert.AreEqual(0, context.GetCounter("dungeonProgress"));
        Assert.AreEqual(100, context.GetCounter("otherCounter"));
    }

    #endregion

    #region ExitSpawnRule Integration

    [Test]
    public void ExitSpawnRule_RequiresAllGatesCleared_BlocksWhenNotCleared()
    {
        // Create rule that requires all gates cleared
        var rule = CreateExitSpawnRule(ExitSpawnMode.Steps, 5, requireAllGatesCleared: true);

        var counters = new WalkCountersSnapshot(10, 10, 10);

        // Gates not cleared - should not spawn
        Assert.IsFalse(rule.ShouldSpawn(counters, allGatesCleared: false));

        // Gates cleared - should spawn
        Assert.IsTrue(rule.ShouldSpawn(counters, allGatesCleared: true));
    }

    [Test]
    public void ExitSpawnRule_NotRequiringGates_SpawnsRegardlessOfGateStatus()
    {
        var rule = CreateExitSpawnRule(ExitSpawnMode.Steps, 5, requireAllGatesCleared: false);

        var counters = new WalkCountersSnapshot(10, 10, 10);

        // Should spawn regardless of gate status
        Assert.IsTrue(rule.ShouldSpawn(counters, allGatesCleared: false));
        Assert.IsTrue(rule.ShouldSpawn(counters, allGatesCleared: true));
    }

    #endregion

    #region WalkState Seed Integration

    [Test]
    public void WalkState_PreservesNodeSeedAndVarietyIndex()
    {
        var context = new GameContext(null);

        context.WalkState.NodeSeed = 54321u;
        context.WalkState.VarietyHistoryIndex = 7;

        Assert.AreEqual(54321u, context.WalkState.NodeSeed);
        Assert.AreEqual(7, context.WalkState.VarietyHistoryIndex);
    }

    #endregion

    #region Helper Methods

    private static ExitSpawnRule CreateExitSpawnRule(ExitSpawnMode mode, int steps, bool requireAllGatesCleared)
    {
        var rule = new ExitSpawnRule();
        var modeField = typeof(ExitSpawnRule).GetField("mode", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var stepsField = typeof(ExitSpawnRule).GetField("steps", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var requireField = typeof(ExitSpawnRule).GetField("requireAllGatesCleared", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        modeField?.SetValue(rule, mode);
        stepsField?.SetValue(rule, steps);
        requireField?.SetValue(rule, requireAllGatesCleared);

        return rule;
    }

    #endregion
}
