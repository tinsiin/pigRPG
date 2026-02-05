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
    public void AddDominoRunOutEnemy_AddsEnemyToList()
    {
        var enemy = new NormalEnemy
        {
            DefaultImpression = SpiritualProperty.doremis,
            MyType = CharacterType.Life
        };

        _manager.AddDominoRunOutEnemy(enemy);

        Assert.AreEqual(1, _manager.DominoRunOutEnemies.Count);
        Assert.AreSame(enemy, _manager.DominoRunOutEnemies[0]);
    }

    [Test]
    public void AddDominoRunOutEnemy_IgnoresNullEnemy()
    {
        _manager.AddDominoRunOutEnemy(null);

        Assert.AreEqual(0, _manager.DominoRunOutEnemies.Count);
    }

    [Test]
    public void ClearDominoRunOutEnemies_ClearsList()
    {
        var enemy = new NormalEnemy
        {
            DefaultImpression = SpiritualProperty.doremis,
            MyType = CharacterType.Life
        };
        _manager.AddDominoRunOutEnemy(enemy);

        _manager.ClearDominoRunOutEnemies();

        Assert.AreEqual(0, _manager.DominoRunOutEnemies.Count);
    }

    [Test]
    public void TurnCount_CanBeSetAndRetrieved()
    {
        _manager.TurnCount = 5;

        Assert.AreEqual(5, _manager.TurnCount);
    }

    [Test]
    public void SetVoluntaryRunOutEnemy_SetsEnemy()
    {
        var enemy = new NormalEnemy
        {
            DefaultImpression = SpiritualProperty.doremis,
            MyType = CharacterType.Life
        };

        _manager.SetVoluntaryRunOutEnemy(enemy);

        Assert.AreSame(enemy, _manager.VoluntaryRunOutEnemy);
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
