using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;

/// <summary>
/// ActionQueue unit tests for TryPeek, Add, RemoveAt, and RemoveDeathCharacters behavior.
/// </summary>
[TestFixture]
public class ActionQueueTests
{
    private static readonly FieldInfo MaxHpField = typeof(BaseStates).GetField("_maxhp", BindingFlags.NonPublic | BindingFlags.Instance);

    [Test]
    public void TryPeek_ReturnsFalseWhenEmpty()
    {
        var queue = new ActionQueue();

        var result = queue.TryPeek(out var entry);

        Assert.IsFalse(result);
        Assert.IsNull(entry);
    }

    [Test]
    public void TryPeek_ReturnsFirstEntryWithoutRemoving()
    {
        var queue = new ActionQueue();
        queue.RatherAdd("first");
        queue.Add(null, allyOrEnemy.alliy, "second");

        var result = queue.TryPeek(out var entry);

        Assert.IsTrue(result);
        Assert.IsNotNull(entry);
        Assert.AreEqual(ActionType.Rather, entry.Type);
        Assert.AreEqual("first", entry.Message);
        Assert.AreEqual(2, queue.Count);
    }

    [Test]
    public void Add_CreatesActTypeEntry()
    {
        var queue = new ActionQueue();
        var actor = CreateEnemy(10f, 10f);

        queue.Add(actor, allyOrEnemy.Enemyiy, "test message");

        Assert.AreEqual(1, queue.Count);
        queue.TryPeek(out var entry);
        Assert.AreEqual(ActionType.Act, entry.Type);
        Assert.AreSame(actor, entry.Actor);
        Assert.AreEqual(allyOrEnemy.Enemyiy, entry.Faction);
        Assert.AreEqual("test message", entry.Message);
    }

    [Test]
    public void RatherAdd_CreatesRatherTypeEntry()
    {
        var queue = new ActionQueue();
        var targets = new List<BaseStates> { CreateEnemy(10f, 10f) };

        queue.RatherAdd("rather message", targets, 50f);

        Assert.AreEqual(1, queue.Count);
        queue.TryPeek(out var entry);
        Assert.AreEqual(ActionType.Rather, entry.Type);
        Assert.IsNull(entry.Actor);
        Assert.AreEqual("rather message", entry.Message);
        Assert.AreSame(targets, entry.RatherTargets);
        Assert.AreEqual(50f, entry.RatherDamage);
    }

    [Test]
    public void RemoveAt_RemovesEntryAtIndex()
    {
        var queue = new ActionQueue();
        queue.RatherAdd("first");
        queue.RatherAdd("second");
        queue.RatherAdd("third");

        queue.RemoveAt(1);

        Assert.AreEqual(2, queue.Count);
        queue.TryPeek(out var entry);
        Assert.AreEqual("first", entry.Message);
    }

    [Test]
    public void RemoveAt_RemovesFirstEntry()
    {
        var queue = new ActionQueue();
        queue.RatherAdd("first");
        queue.RatherAdd("second");

        queue.RemoveAt(0);

        Assert.AreEqual(1, queue.Count);
        queue.TryPeek(out var entry);
        Assert.AreEqual("second", entry.Message);
    }

    [Test]
    public void RemoveDeathCharacters_RemovesEntriesWithDeadActors()
    {
        var queue = new ActionQueue();
        var alive = CreateEnemy(10f, 10f);
        var dead = CreateEnemy(0f, 10f);

        queue.Add(alive, allyOrEnemy.alliy, "alive");
        queue.Add(dead, allyOrEnemy.alliy, "dead");
        queue.Add(alive, allyOrEnemy.alliy, "alive2");

        queue.RemoveDeathCharacters();

        Assert.AreEqual(2, queue.Count);
        queue.TryPeek(out var entry);
        Assert.AreEqual("alive", entry.Message);
    }

    [Test]
    public void RemoveDeathCharacters_KeepsEntriesWithNullActors()
    {
        var queue = new ActionQueue();
        var dead = CreateEnemy(0f, 10f);

        queue.RatherAdd("rather");
        queue.Add(dead, allyOrEnemy.alliy, "dead");

        queue.RemoveDeathCharacters();

        Assert.AreEqual(1, queue.Count);
        queue.TryPeek(out var entry);
        Assert.AreEqual("rather", entry.Message);
    }

    [Test]
    public void Count_ReturnsCorrectNumberOfEntries()
    {
        var queue = new ActionQueue();

        Assert.AreEqual(0, queue.Count);

        queue.RatherAdd("a");
        Assert.AreEqual(1, queue.Count);

        queue.Add(null, allyOrEnemy.alliy, "b");
        Assert.AreEqual(2, queue.Count);

        queue.RemoveAt(0);
        Assert.AreEqual(1, queue.Count);
    }

    private static NormalEnemy CreateEnemy(float hp, float maxHp)
    {
        var enemy = new NormalEnemy
        {
            DefaultImpression = SpiritualProperty.doremis,
            MyType = CharacterType.Life
        };
        MaxHpField?.SetValue(enemy, maxHp);
        enemy.HP = hp;
        return enemy;
    }
}
