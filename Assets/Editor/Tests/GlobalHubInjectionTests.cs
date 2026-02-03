using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;

/// <summary>
/// Tests for Hub dependency injection in base classes (BaseStates, BasePassive, BaseSkill).
/// </summary>
[TestFixture]
public class GlobalHubInjectionTests
{
    private static readonly BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

    [SetUp]
    public void SetUp()
    {
        BattleContextHub.Set(null);
    }

    [TearDown]
    public void TearDown()
    {
        BattleContextHub.Set(null);
    }

    // ===========================================
    // BaseStates Tests
    // ===========================================

    [Test]
    public void BaseStates_ManagerProperty_ReturnsInjectedContext()
    {
        var enemy = CreateNormalEnemy();
        var mockContext = new MockBattleContext();

        enemy.BindBattleContext(mockContext);

        var manager = GetManagerProperty(enemy);
        Assert.AreSame(mockContext, manager);
    }

    [Test]
    public void BaseStates_ManagerProperty_FallsBackToHubWhenNotInjected()
    {
        var enemy = CreateNormalEnemy();
        var hubContext = new MockBattleContext();
        BattleContextHub.Set(hubContext);

        var manager = GetManagerProperty(enemy);
        Assert.AreSame(hubContext, manager);
    }

    [Test]
    public void BaseStates_ManagerProperty_PrefersInjectedOverHub()
    {
        var enemy = CreateNormalEnemy();
        var injectedContext = new MockBattleContext();
        var hubContext = new MockBattleContext();

        enemy.BindBattleContext(injectedContext);
        BattleContextHub.Set(hubContext);

        var manager = GetManagerProperty(enemy);
        Assert.AreSame(injectedContext, manager);
    }

    // ===========================================
    // BasePassive Tests
    // ===========================================

    [Test]
    public void BasePassive_ManagerProperty_ReturnsInjectedContext()
    {
        var passive = new TestPassive();
        var mockContext = new MockBattleContext();

        passive.BindBattleContext(mockContext);

        var manager = GetPassiveManagerProperty(passive);
        Assert.AreSame(mockContext, manager);
    }

    [Test]
    public void BasePassive_ManagerProperty_FallsBackToHubWhenNotInjected()
    {
        var passive = new TestPassive();
        var hubContext = new MockBattleContext();
        BattleContextHub.Set(hubContext);

        var manager = GetPassiveManagerProperty(passive);
        Assert.AreSame(hubContext, manager);
    }

    // ===========================================
    // BaseSkill Tests
    // ===========================================

    [Test]
    public void BaseSkill_ManagerProperty_ReturnsInjectedContext()
    {
        var skill = new TestSkill();
        var mockContext = new MockBattleContext();

        skill.BindBattleContext(mockContext);

        var manager = GetSkillManagerProperty(skill);
        Assert.AreSame(mockContext, manager);
    }

    [Test]
    public void BaseSkill_ManagerProperty_FallsBackToHubWhenNotInjected()
    {
        var skill = new TestSkill();
        var hubContext = new MockBattleContext();
        BattleContextHub.Set(hubContext);

        var manager = GetSkillManagerProperty(skill);
        Assert.AreSame(hubContext, manager);
    }

    // ===========================================
    // Helper Methods
    // ===========================================

    private static NormalEnemy CreateNormalEnemy()
    {
        return new NormalEnemy
        {
            DefaultImpression = SpiritualProperty.doremis,
            MyType = CharacterType.Life
        };
    }

    private static IBattleContext GetManagerProperty(BaseStates states)
    {
        var prop = typeof(BaseStates).GetProperty("manager", PrivateInstance);
        return (IBattleContext)prop?.GetValue(states);
    }

    private static IBattleContext GetPassiveManagerProperty(BasePassive passive)
    {
        var prop = typeof(BasePassive).GetProperty("manager", PrivateInstance);
        return (IBattleContext)prop?.GetValue(passive);
    }

    private static IBattleContext GetSkillManagerProperty(BaseSkill skill)
    {
        var prop = typeof(BaseSkill).GetProperty("manager", PrivateInstance);
        return (IBattleContext)prop?.GetValue(skill);
    }

    // ===========================================
    // Test Doubles
    // ===========================================

    private class MockBattleContext : IBattleContext
    {
        public BattleGroup AllyGroup => null;
        public BattleGroup EnemyGroup => null;
        public List<BaseStates> AllCharacters => new List<BaseStates>();
        public BaseStates Acter => null;
        public UnderActersEntryList unders => null;
        public ActionQueue Acts => null;
        public bool SkillStock { get; set; }
        public bool DoNothing { get; set; }
        public bool PassiveCancel { get; set; }
        public int BattleTurnCount => 0;
        public allyOrEnemy GetCharacterFaction(BaseStates chara) => allyOrEnemy.alliy;
        public BattleGroup FactionToGroup(allyOrEnemy faction) => null;
        public BattleGroup MyGroup(BaseStates chara) => null;
        public bool IsFriend(BaseStates chara1, BaseStates chara2) => false;
        public bool IsVanguard(BaseStates chara) => false;
        public void BeVanguard(BaseStates newVanguard) { }
        public List<BaseStates> GetOtherAlliesAlive(BaseStates chara) => new List<BaseStates>();
    }

    private class TestPassive : BasePassive { }

    private class TestSkill : BaseSkill { }
}
