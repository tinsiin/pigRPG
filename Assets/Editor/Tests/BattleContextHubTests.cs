using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// BattleContextHub unit tests for IsInBattle and context management.
/// </summary>
[TestFixture]
public class BattleContextHubTests
{
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

    [Test]
    public void IsInBattle_ReturnsFalseWhenCurrentIsNull()
    {
        BattleContextHub.Set(null);

        Assert.IsFalse(BattleContextHub.IsInBattle);
    }

    [Test]
    public void IsInBattle_ReturnsTrueWhenCurrentIsSet()
    {
        var mockContext = new MockBattleContext();
        BattleContextHub.Set(mockContext);

        Assert.IsTrue(BattleContextHub.IsInBattle);
    }

    [Test]
    public void Clear_RemovesContextWhenSameInstance()
    {
        var mockContext = new MockBattleContext();
        BattleContextHub.Set(mockContext);

        BattleContextHub.Clear(mockContext);

        Assert.IsNull(BattleContextHub.Current);
        Assert.IsFalse(BattleContextHub.IsInBattle);
    }

    [Test]
    public void Clear_DoesNotRemoveContextWhenDifferentInstance()
    {
        var context1 = new MockBattleContext();
        var context2 = new MockBattleContext();
        BattleContextHub.Set(context1);

        BattleContextHub.Clear(context2);

        Assert.AreSame(context1, BattleContextHub.Current);
        Assert.IsTrue(BattleContextHub.IsInBattle);
    }

    private class MockBattleContext : IBattleContext
    {
        public BattleGroup AllyGroup => null;
        public BattleGroup EnemyGroup => null;
        public List<BaseStates> AllCharacters => new List<BaseStates>();
        public BaseStates Acter => null;
        public UnderActersEntryList unders => null;
        public ActionQueue Acts => null;
        public IBattleRandom Random => new SystemBattleRandom();
        public bool SkillStock { get; set; }
        public bool DoNothing { get; set; }
        public bool PassiveCancel { get; set; }
        public int BattleTurnCount => 0;
        public Faction GetCharacterFaction(BaseStates chara) => Faction.Ally;
        public BattleGroup FactionToGroup(Faction faction) => null;
        public BattleGroup MyGroup(BaseStates chara) => null;
        public bool IsFriend(BaseStates chara1, BaseStates chara2) => false;
        public bool IsVanguard(BaseStates chara) => false;
        public void BeVanguard(BaseStates newVanguard) { }
        public List<BaseStates> GetOtherAlliesAlive(BaseStates chara) => new List<BaseStates>();
    }
}
