using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

/// <summary>
/// SkillEffectChain の順序・条件テスト
/// </summary>
[TestFixture]
public class SkillEffectChainTests
{
    #region Ordering Tests

    [Test]
    public void EffectChain_ExecutesInPriorityOrder()
    {
        var executionOrder = new List<int>();

        var effects = new ISkillEffect[]
        {
            new TestEffect(300, () => executionOrder.Add(300)),
            new TestEffect(100, () => executionOrder.Add(100)),
            new TestEffect(200, () => executionOrder.Add(200)),
        };

        var chain = new SkillEffectChain(effects);
        chain.ExecuteAll(null).GetAwaiter().GetResult();

        Assert.AreEqual(3, executionOrder.Count);
        Assert.AreEqual(100, executionOrder[0], "Priority 100 should execute first");
        Assert.AreEqual(200, executionOrder[1], "Priority 200 should execute second");
        Assert.AreEqual(300, executionOrder[2], "Priority 300 should execute third");
    }

    [Test]
    public void EffectChain_SkipsEffectsWhereShouldApplyReturnsFalse()
    {
        var executionOrder = new List<int>();

        var effects = new ISkillEffect[]
        {
            new TestEffect(100, () => executionOrder.Add(100), shouldApply: true),
            new TestEffect(200, () => executionOrder.Add(200), shouldApply: false),
            new TestEffect(300, () => executionOrder.Add(300), shouldApply: true),
        };

        var chain = new SkillEffectChain(effects);
        chain.ExecuteAll(null).GetAwaiter().GetResult();

        Assert.AreEqual(2, executionOrder.Count);
        Assert.AreEqual(100, executionOrder[0]);
        Assert.AreEqual(300, executionOrder[1]);
        Assert.IsFalse(executionOrder.Contains(200), "Effect with shouldApply=false should be skipped");
    }

    [Test]
    public void EffectChain_AddEffect_MaintainsOrder()
    {
        var executionOrder = new List<int>();

        var chain = new SkillEffectChain(new ISkillEffect[]
        {
            new TestEffect(100, () => executionOrder.Add(100)),
            new TestEffect(300, () => executionOrder.Add(300)),
        });

        // Add effect with priority between existing ones
        chain.AddEffect(new TestEffect(200, () => executionOrder.Add(200)));

        chain.ExecuteAll(null).GetAwaiter().GetResult();

        Assert.AreEqual(3, executionOrder.Count);
        Assert.AreEqual(100, executionOrder[0]);
        Assert.AreEqual(200, executionOrder[1]);
        Assert.AreEqual(300, executionOrder[2]);
    }

    #endregion

    #region Real Effect Priority Tests

    [Test]
    public void FlatRozeEffect_HasLowestPriority()
    {
        var flatRoze = new FlatRozeEffect();
        var helpRecovery = new HelpRecoveryEffect();
        var revengeBonus = new RevengeBonusEffect();

        Assert.Less(flatRoze.Priority, helpRecovery.Priority,
            "FlatRozeEffect should have lower priority than HelpRecoveryEffect");
        Assert.Less(flatRoze.Priority, revengeBonus.Priority,
            "FlatRozeEffect should have lower priority than RevengeBonusEffect");
    }

    [Test]
    public void HelpRecoveryEffect_HasMiddlePriority()
    {
        var flatRoze = new FlatRozeEffect();
        var helpRecovery = new HelpRecoveryEffect();
        var revengeBonus = new RevengeBonusEffect();

        Assert.Greater(helpRecovery.Priority, flatRoze.Priority,
            "HelpRecoveryEffect should have higher priority than FlatRozeEffect");
        Assert.Less(helpRecovery.Priority, revengeBonus.Priority,
            "HelpRecoveryEffect should have lower priority than RevengeBonusEffect");
    }

    [Test]
    public void RevengeBonusEffect_HasHighestPriority()
    {
        var flatRoze = new FlatRozeEffect();
        var helpRecovery = new HelpRecoveryEffect();
        var revengeBonus = new RevengeBonusEffect();

        Assert.Greater(revengeBonus.Priority, flatRoze.Priority,
            "RevengeBonusEffect should have higher priority than FlatRozeEffect");
        Assert.Greater(revengeBonus.Priority, helpRecovery.Priority,
            "RevengeBonusEffect should have higher priority than HelpRecoveryEffect");
    }

    [Test]
    public void EffectPriorities_AreCorrectValues()
    {
        Assert.AreEqual(100, new FlatRozeEffect().Priority);
        Assert.AreEqual(200, new HelpRecoveryEffect().Priority);
        Assert.AreEqual(300, new RevengeBonusEffect().Priority);
    }

    #endregion

    #region Empty Chain Tests

    [Test]
    public void EmptyChain_ExecutesWithoutError()
    {
        var chain = new SkillEffectChain(new ISkillEffect[0]);

        Assert.DoesNotThrow(() => chain.ExecuteAll(null).GetAwaiter().GetResult());
    }

    #endregion

    #region Test Helpers

    private sealed class TestEffect : ISkillEffect
    {
        private readonly int _priority;
        private readonly System.Action _onApply;
        private readonly bool _shouldApply;

        public TestEffect(int priority, System.Action onApply, bool shouldApply = true)
        {
            _priority = priority;
            _onApply = onApply;
            _shouldApply = shouldApply;
        }

        public int Priority => _priority;

        public bool ShouldApply(SkillEffectContext context) => _shouldApply;

        public UniTask Apply(SkillEffectContext context)
        {
            _onApply?.Invoke();
            return UniTask.CompletedTask;
        }
    }

    #endregion
}
