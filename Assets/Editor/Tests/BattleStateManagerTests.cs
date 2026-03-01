using NUnit.Framework;

/// <summary>
/// BattleStateManager unit tests for state transitions and guards.
/// </summary>
[TestFixture]
public class BattleStateManagerTests
{
    private BattleState _state;
    private BattleStateManager _manager;

    [SetUp]
    public void SetUp()
    {
        _state = new BattleState();
        _manager = new BattleStateManager(_state, new NoOpBattleLogger());
    }

    [Test]
    public void MarkWipeout_SetsWipeoutToTrue()
    {
        _manager.MarkWipeout();

        Assert.IsTrue(_manager.Wipeout);
    }

    [Test]
    public void MarkEnemyGroupEmpty_SetsEnemyGroupEmptyToTrue()
    {
        _manager.MarkEnemyGroupEmpty();

        Assert.IsTrue(_manager.EnemyGroupEmpty);
    }

    [Test]
    public void MarkAlliesRunOut_SetsAlliesRunOutToTrue()
    {
        _manager.MarkAlliesRunOut();

        Assert.IsTrue(_manager.AlliesRunOut);
    }

    [Test]
    public void TurnCount_CanBeSetAndRetrieved()
    {
        _manager.TurnCount = 5;

        Assert.AreEqual(5, _manager.TurnCount);
    }

    [Test]
    public void MultipleTerminalStates_AllCanBeSetIndependently()
    {
        // 各終了状態は独立して設定可能（ガードはログ警告のみ）
        _manager.MarkWipeout();
        _manager.MarkEnemyGroupEmpty();
        _manager.MarkAlliesRunOut();

        Assert.IsTrue(_manager.Wipeout);
        Assert.IsTrue(_manager.EnemyGroupEmpty);
        Assert.IsTrue(_manager.AlliesRunOut);
    }
}
