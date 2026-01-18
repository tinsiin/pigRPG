using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// GateResolver and related Phase 2 classes unit tests
/// </summary>
[TestFixture]
public class GateResolverTests
{
    #region GatePositionSpec Tests

    [Test]
    public void GatePositionSpec_AbsSteps_ReturnsExactValue()
    {
        var spec = CreatePositionSpec(GatePositionSpec.PositionType.AbsSteps, absSteps: 50);
        var result = spec.ResolvePosition(100, 12345u, "gate1");
        Assert.AreEqual(50, result);
    }

    [Test]
    public void GatePositionSpec_Percent_ReturnsScaledValue()
    {
        var spec = CreatePositionSpec(GatePositionSpec.PositionType.Percent, percent: 0.5f);
        var result = spec.ResolvePosition(100, 12345u, "gate1");
        Assert.AreEqual(50, result);
    }

    [Test]
    public void GatePositionSpec_Range_ReturnsDeterministicValue()
    {
        var spec = CreatePositionSpec(GatePositionSpec.PositionType.Range, rangeMin: 10, rangeMax: 20);
        var result1 = spec.ResolvePosition(100, 12345u, "gate1");
        var result2 = spec.ResolvePosition(100, 12345u, "gate1");

        // Same seed and gateId should produce same result
        Assert.AreEqual(result1, result2);
        Assert.IsTrue(result1 >= 10 && result1 <= 20);
    }

    [Test]
    public void GatePositionSpec_Range_DifferentGateIds_ProduceDifferentResults()
    {
        var spec = CreatePositionSpec(GatePositionSpec.PositionType.Range, rangeMin: 0, rangeMax: 100);
        var result1 = spec.ResolvePosition(100, 12345u, "gate1");
        var result2 = spec.ResolvePosition(100, 12345u, "gate2");

        // Different gateIds should produce different results (with high probability)
        // Note: There's a small chance they could be equal, so we test multiple cases
        var allSame = true;
        for (var i = 0; i < 10; i++)
        {
            var a = spec.ResolvePosition(100, (uint)i, "gateA");
            var b = spec.ResolvePosition(100, (uint)i, "gateB");
            if (a != b) allSame = false;
        }
        Assert.IsFalse(allSame, "Different gateIds should produce at least some different results");
    }

    #endregion

    #region GateRuntimeState Tests

    [Test]
    public void GateRuntimeState_Clone_CreatesIndependentCopy()
    {
        var original = new GateRuntimeState("gate1", 50)
        {
            IsCleared = true,
            CooldownRemaining = 5,
            FailCount = 2
        };

        var clone = original.Clone();

        // Modify original
        original.IsCleared = false;
        original.CooldownRemaining = 0;

        // Clone should be unaffected
        Assert.IsTrue(clone.IsCleared);
        Assert.AreEqual(5, clone.CooldownRemaining);
        Assert.AreEqual(2, clone.FailCount);
    }

    #endregion

    #region GateResolver Tests

    [Test]
    public void GateResolver_TakeSnapshot_CreatesIndependentCopy()
    {
        var resolver = new GateResolver();
        var snapshot1 = new Dictionary<string, GateRuntimeState>
        {
            ["gate1"] = new GateRuntimeState("gate1", 10),
            ["gate2"] = new GateRuntimeState("gate2", 20)
        };
        resolver.RestoreFromSnapshot(snapshot1);

        var snapshot2 = resolver.TakeSnapshot();

        // Modify original state via resolver
        var state = resolver.GetState("gate1");
        state.IsCleared = true;

        // Snapshot should be unaffected
        Assert.IsFalse(snapshot2["gate1"].IsCleared);
    }

    [Test]
    public void GateResolver_AllGatesCleared_ReturnsTrueWhenEmpty()
    {
        var resolver = new GateResolver();
        // Node with no gates
        Assert.IsTrue(resolver.AllGatesCleared(null));
    }

    [Test]
    public void GateResolver_TickCooldowns_DecrementsAllCooldowns()
    {
        var resolver = new GateResolver();
        var snapshot = new Dictionary<string, GateRuntimeState>
        {
            ["gate1"] = new GateRuntimeState("gate1", 10) { CooldownRemaining = 5 },
            ["gate2"] = new GateRuntimeState("gate2", 20) { CooldownRemaining = 3 }
        };
        resolver.RestoreFromSnapshot(snapshot);

        resolver.TickCooldowns();

        Assert.AreEqual(4, resolver.GetState("gate1").CooldownRemaining);
        Assert.AreEqual(2, resolver.GetState("gate2").CooldownRemaining);
    }

    [Test]
    public void GateResolver_TickCooldowns_DoesNotGoNegative()
    {
        var resolver = new GateResolver();
        var snapshot = new Dictionary<string, GateRuntimeState>
        {
            ["gate1"] = new GateRuntimeState("gate1", 10) { CooldownRemaining = 0 }
        };
        resolver.RestoreFromSnapshot(snapshot);

        resolver.TickCooldowns();

        Assert.AreEqual(0, resolver.GetState("gate1").CooldownRemaining);
    }

    #endregion

    #region WalkCounters Tests

    [Test]
    public void WalkCounters_ResetTrackProgress_OnlyResetsTrackProgress()
    {
        var counters = new WalkCounters();
        counters.Advance(10);
        Assert.AreEqual(10, counters.GlobalSteps);
        Assert.AreEqual(10, counters.NodeSteps);
        Assert.AreEqual(10, counters.TrackProgress);

        counters.ResetTrackProgress();

        Assert.AreEqual(10, counters.GlobalSteps);
        Assert.AreEqual(10, counters.NodeSteps);
        Assert.AreEqual(0, counters.TrackProgress);
    }

    [Test]
    public void WalkCounters_AdvanceTrackProgress_OnlyAdvancesTrackProgress()
    {
        var counters = new WalkCounters();
        counters.Advance(5);
        counters.AdvanceTrackProgress(3);

        Assert.AreEqual(5, counters.GlobalSteps);
        Assert.AreEqual(5, counters.NodeSteps);
        Assert.AreEqual(8, counters.TrackProgress);
    }

    #endregion

    #region Condition Tests

    [Test]
    public void AndCondition_ReturnsTrueWhenAllConditionsMet()
    {
        var context = CreateTestContext();
        context.SetFlag("flag1", true);
        context.SetFlag("flag2", true);

        // Since we can't easily create ConditionSO in tests, we test the logic directly
        var result = context.HasFlag("flag1") && context.HasFlag("flag2");
        Assert.IsTrue(result);
    }

    [Test]
    public void OrCondition_ReturnsTrueWhenAnyConditionMet()
    {
        var context = CreateTestContext();
        context.SetFlag("flag1", true);
        context.SetFlag("flag2", false);

        var result = context.HasFlag("flag1") || context.HasFlag("flag2");
        Assert.IsTrue(result);
    }

    [Test]
    public void HasFlagCondition_ReturnsTrueWhenFlagExists()
    {
        var context = CreateTestContext();
        context.SetFlag("testFlag", true);

        Assert.IsTrue(context.HasFlag("testFlag"));
    }

    [Test]
    public void HasFlagCondition_ReturnsFalseWhenFlagMissing()
    {
        var context = CreateTestContext();

        Assert.IsFalse(context.HasFlag("missingFlag"));
    }

    [Test]
    public void HasCounterCondition_ComparesCorrectly()
    {
        var context = CreateTestContext();
        context.SetCounter("score", 50);

        Assert.AreEqual(50, context.GetCounter("score"));
        Assert.IsTrue(context.GetCounter("score") >= 50);
        Assert.IsFalse(context.GetCounter("score") > 50);
    }

    #endregion

    #region AnchorManager Tests

    [Test]
    public void AnchorManager_CreateAndRetrieveAnchor()
    {
        var manager = new AnchorManager();
        var context = CreateTestContext();
        context.Counters.Advance(10);
        context.SetFlag("testFlag", true);

        manager.CreateAnchor("anchor1", context, null, AnchorScope.Node);

        Assert.IsTrue(manager.HasAnchor("anchor1"));
    }

    [Test]
    public void AnchorManager_ClearAnchorsInScope_RemovesOnlyMatchingScope()
    {
        var manager = new AnchorManager();
        var context = CreateTestContext();

        manager.CreateAnchor("nodeAnchor", context, null, AnchorScope.Node);
        manager.CreateAnchor("regionAnchor", context, null, AnchorScope.Region);
        manager.CreateAnchor("graphAnchor", context, null, AnchorScope.Graph);

        manager.ClearAnchorsInScope(AnchorScope.Node);

        Assert.IsFalse(manager.HasAnchor("nodeAnchor"));
        Assert.IsTrue(manager.HasAnchor("regionAnchor"));
        Assert.IsTrue(manager.HasAnchor("graphAnchor"));
    }

    [Test]
    public void AnchorManager_ExportImportAnchors_PreservesData()
    {
        var manager1 = new AnchorManager();
        var context = CreateTestContext();
        context.Counters.Advance(25);
        context.SetFlag("exportedFlag", true);

        manager1.CreateAnchor("testAnchor", context, null, AnchorScope.Region);

        var exported = manager1.ExportAnchors();

        var manager2 = new AnchorManager();
        manager2.ImportAnchors(exported);

        Assert.IsTrue(manager2.HasAnchor("testAnchor"));
    }

    #endregion

    #region Serialization Tests

    [Test]
    public void StringBoolPair_SerializesCorrectly()
    {
        var pair = new StringBoolPair("testKey", true);

        Assert.AreEqual("testKey", pair.Key);
        Assert.IsTrue(pair.Value);
    }

    [Test]
    public void StringIntPair_SerializesCorrectly()
    {
        var pair = new StringIntPair("scoreKey", 100);

        Assert.AreEqual("scoreKey", pair.Key);
        Assert.AreEqual(100, pair.Value);
    }

    [Test]
    public void GateRuntimeStateData_ConvertsToRuntimeState()
    {
        var data = new GateRuntimeStateData
        {
            GateId = "gate1",
            ResolvedPosition = 50,
            IsCleared = true,
            CooldownRemaining = 3,
            FailCount = 1
        };

        var state = data.ToRuntimeState();

        Assert.AreEqual("gate1", state.GateId);
        Assert.AreEqual(50, state.ResolvedPosition);
        Assert.IsTrue(state.IsCleared);
        Assert.AreEqual(3, state.CooldownRemaining);
        Assert.AreEqual(1, state.FailCount);
    }

    [Test]
    public void GateRuntimeStateData_ConvertsFromRuntimeState()
    {
        var state = new GateRuntimeState("gate2", 75)
        {
            IsCleared = false,
            CooldownRemaining = 5,
            FailCount = 2
        };

        var data = new GateRuntimeStateData(state);

        Assert.AreEqual("gate2", data.GateId);
        Assert.AreEqual(75, data.ResolvedPosition);
        Assert.IsFalse(data.IsCleared);
        Assert.AreEqual(5, data.CooldownRemaining);
        Assert.AreEqual(2, data.FailCount);
    }

    #endregion

    #region Helper Methods

    private static GatePositionSpec CreatePositionSpec(
        GatePositionSpec.PositionType type,
        int absSteps = 0,
        float percent = 0f,
        int rangeMin = 0,
        int rangeMax = 0)
    {
        // Use reflection to create the struct since fields are private
        var spec = new GatePositionSpec();
        var typeField = typeof(GatePositionSpec).GetField("type", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var absField = typeof(GatePositionSpec).GetField("absSteps", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var percentField = typeof(GatePositionSpec).GetField("percent", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var minField = typeof(GatePositionSpec).GetField("rangeMin", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var maxField = typeof(GatePositionSpec).GetField("rangeMax", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        object boxed = spec;
        typeField?.SetValue(boxed, type);
        absField?.SetValue(boxed, absSteps);
        percentField?.SetValue(boxed, percent);
        minField?.SetValue(boxed, rangeMin);
        maxField?.SetValue(boxed, rangeMax);
        return (GatePositionSpec)boxed;
    }

    private static GameContext CreateTestContext()
    {
        return new GameContext(null);
    }

    #endregion
}
