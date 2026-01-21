using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// ProgressCalculator and related progress UI classes unit tests
/// </summary>
[TestFixture]
public class ProgressCalculatorTests
{
    #region ProgressEntry Tests

    [Test]
    public void ProgressEntry_CreateGate_SetsCorrectValues()
    {
        var entry = ProgressEntry.CreateGate(1, "gate1", 50, isCleared: false, isActive: true, isCoolingDown: false);

        Assert.AreEqual(ProgressEntry.EntryType.Gate, entry.Type);
        Assert.AreEqual(1, entry.Order);
        Assert.AreEqual("gate1", entry.Id);
        Assert.AreEqual(50, entry.Position);
        Assert.IsFalse(entry.IsCleared);
        Assert.IsTrue(entry.IsActive);
        Assert.IsFalse(entry.IsCoolingDown);
        Assert.AreEqual("1", entry.DisplayLabel);
    }

    [Test]
    public void ProgressEntry_CreateExit_SetsCorrectValues()
    {
        var entry = ProgressEntry.CreateExit(3, 100);

        Assert.AreEqual(ProgressEntry.EntryType.Exit, entry.Type);
        Assert.AreEqual(3, entry.Order);
        Assert.AreEqual("exit", entry.Id);
        Assert.AreEqual(100, entry.Position);
        Assert.IsFalse(entry.IsCleared);
        Assert.IsFalse(entry.IsActive);
        Assert.AreEqual("Exit", entry.DisplayLabel);
    }

    #endregion

    #region ProgressSnapshot Tests

    [Test]
    public void ProgressSnapshot_Empty_HasNoEntries()
    {
        var snapshot = ProgressSnapshot.Empty;

        Assert.IsNotNull(snapshot.Entries);
        Assert.AreEqual(0, snapshot.Entries.Count);
        Assert.IsTrue(snapshot.AllGatesCleared);
        Assert.IsFalse(snapshot.HasNextEntry);
    }

    [Test]
    public void ProgressSnapshot_GetNextEntry_ReturnsCorrectEntry()
    {
        var entries = new[]
        {
            ProgressEntry.CreateGate(1, "gate1", 10, false, false, false),
            ProgressEntry.CreateGate(2, "gate2", 20, false, false, false),
            ProgressEntry.CreateExit(3, 50)
        };

        var snapshot = new ProgressSnapshot(
            entries,
            currentTrackProgress: 5,
            nextEntryIndex: 0,
            stepsToNextEntry: 5,
            probabilityOfNextEntry: null,
            allGatesCleared: false,
            exitMode: ExitSpawnMode.Steps,
            remainingGateCount: 2);

        Assert.IsTrue(snapshot.HasNextEntry);
        var next = snapshot.GetNextEntry();
        Assert.IsTrue(next.HasValue);
        Assert.AreEqual("gate1", next.Value.Id);
    }

    [Test]
    public void ProgressSnapshot_GetNextEntry_ReturnsNullWhenNoNextEntry()
    {
        var snapshot = new ProgressSnapshot(
            System.Array.Empty<ProgressEntry>(),
            currentTrackProgress: 0,
            nextEntryIndex: null,
            stepsToNextEntry: null,
            probabilityOfNextEntry: null,
            allGatesCleared: true,
            exitMode: ExitSpawnMode.None,
            remainingGateCount: 0);

        Assert.IsFalse(snapshot.HasNextEntry);
        Assert.IsNull(snapshot.GetNextEntry());
    }

    #endregion

    #region ProgressCalculator Tests

    [Test]
    public void ProgressCalculator_Calculate_ReturnsEmptyWhenNoNode()
    {
        var calculator = new ProgressCalculator();
        var resolver = new GateResolver();
        var counters = new WalkCounters();

        var result = calculator.Calculate(null, resolver, counters);

        Assert.AreEqual(0, result.Entries.Count);
        Assert.IsTrue(result.AllGatesCleared);
    }

    [Test]
    public void ProgressCalculator_Calculate_ReturnsEmptyWhenNoResolver()
    {
        var calculator = new ProgressCalculator();
        var counters = new WalkCounters();

        var result = calculator.Calculate(null, null, counters);

        Assert.AreEqual(0, result.Entries.Count);
    }

    [Test]
    public void ProgressCalculator_Calculate_HandlesNodeWithNoGates()
    {
        var calculator = new ProgressCalculator();
        var resolver = new GateResolver();
        var counters = new WalkCounters();

        // Resolver with no gates initialized
        var result = calculator.Calculate(CreateEmptyNode(), resolver, counters);

        // Should only have exit entry or be empty depending on ExitSpawn
        Assert.IsTrue(result.AllGatesCleared);
        Assert.AreEqual(0, result.RemainingGateCount);
    }

    [Test]
    public void ProgressCalculator_Calculate_OrdersGatesByOrder()
    {
        var calculator = new ProgressCalculator();
        var resolver = new GateResolver();
        var counters = new WalkCounters();

        // Setup gates in resolver
        var snapshot = new Dictionary<string, GateRuntimeState>
        {
            ["gate2"] = new GateRuntimeState("gate2", 20),
            ["gate1"] = new GateRuntimeState("gate1", 10),
            ["gate3"] = new GateRuntimeState("gate3", 30)
        };
        resolver.RestoreFromSnapshot(snapshot);

        var node = CreateNodeWithGates(new[]
        {
            CreateGateMarker("gate2", 2, 20),
            CreateGateMarker("gate1", 1, 10),
            CreateGateMarker("gate3", 3, 30)
        });

        var result = calculator.Calculate(node, resolver, counters);

        Assert.GreaterOrEqual(result.Entries.Count, 3);
        Assert.AreEqual("gate1", result.Entries[0].Id);
        Assert.AreEqual("gate2", result.Entries[1].Id);
        Assert.AreEqual("gate3", result.Entries[2].Id);
    }

    [Test]
    public void ProgressCalculator_Calculate_IdentifiesActiveGate()
    {
        var calculator = new ProgressCalculator();
        var resolver = new GateResolver();
        var counters = new WalkCounters();
        counters.Advance(15);

        var snapshot = new Dictionary<string, GateRuntimeState>
        {
            ["gate1"] = new GateRuntimeState("gate1", 10) { IsCleared = false }
        };
        resolver.RestoreFromSnapshot(snapshot);

        var node = CreateNodeWithGates(new[]
        {
            CreateGateMarker("gate1", 1, 10)
        });

        var result = calculator.Calculate(node, resolver, counters);

        Assert.GreaterOrEqual(result.Entries.Count, 1);
        Assert.IsTrue(result.Entries[0].IsActive);
    }

    [Test]
    public void ProgressCalculator_Calculate_CalculatesStepsToNextGate()
    {
        var calculator = new ProgressCalculator();
        var resolver = new GateResolver();
        var counters = new WalkCounters();
        counters.Advance(5);

        var snapshot = new Dictionary<string, GateRuntimeState>
        {
            ["gate1"] = new GateRuntimeState("gate1", 15)
        };
        resolver.RestoreFromSnapshot(snapshot);

        var node = CreateNodeWithGates(new[]
        {
            CreateGateMarker("gate1", 1, 15)
        });

        var result = calculator.Calculate(node, resolver, counters);

        Assert.IsTrue(result.HasNextEntry);
        Assert.AreEqual(10, result.StepsToNextEntry);
    }

    [Test]
    public void ProgressCalculator_Calculate_CountsRemainingGates()
    {
        var calculator = new ProgressCalculator();
        var resolver = new GateResolver();
        var counters = new WalkCounters();

        var snapshot = new Dictionary<string, GateRuntimeState>
        {
            ["gate1"] = new GateRuntimeState("gate1", 10) { IsCleared = true },
            ["gate2"] = new GateRuntimeState("gate2", 20) { IsCleared = false },
            ["gate3"] = new GateRuntimeState("gate3", 30) { IsCleared = false }
        };
        resolver.RestoreFromSnapshot(snapshot);

        var node = CreateNodeWithGates(new[]
        {
            CreateGateMarker("gate1", 1, 10),
            CreateGateMarker("gate2", 2, 20),
            CreateGateMarker("gate3", 3, 30)
        });

        var result = calculator.Calculate(node, resolver, counters);

        Assert.AreEqual(2, result.RemainingGateCount);
    }

    #endregion

    #region Helper Methods

    private static NodeSO CreateEmptyNode()
    {
        return UnityEngine.ScriptableObject.CreateInstance<NodeSO>();
    }

    private static NodeSO CreateNodeWithGates(GateMarker[] gates)
    {
        var node = UnityEngine.ScriptableObject.CreateInstance<NodeSO>();

        // Use reflection to set private fields
        var gatesField = typeof(NodeSO).GetField("gates",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        gatesField?.SetValue(node, gates);

        return node;
    }

    private static GateMarker CreateGateMarker(string gateId, int order, int position)
    {
        var marker = new GateMarker();

        var idField = typeof(GateMarker).GetField("gateId",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var orderField = typeof(GateMarker).GetField("order",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        idField?.SetValue(marker, gateId);
        orderField?.SetValue(marker, order);

        // Set position spec
        var posSpec = CreatePositionSpec(position);
        var posField = typeof(GateMarker).GetField("positionSpec",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        posField?.SetValue(marker, posSpec);

        return marker;
    }

    private static GatePositionSpec CreatePositionSpec(int absSteps)
    {
        var spec = new GatePositionSpec();

        var typeField = typeof(GatePositionSpec).GetField("type",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var absField = typeof(GatePositionSpec).GetField("absSteps",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        object boxed = spec;
        typeField?.SetValue(boxed, GatePositionSpec.PositionType.AbsSteps);
        absField?.SetValue(boxed, absSteps);
        return (GatePositionSpec)boxed;
    }

    #endregion
}
