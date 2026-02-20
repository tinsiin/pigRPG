using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;

/// <summary>
/// EnemyRebornManager unit tests (state transitions via reflection).
/// </summary>
[TestFixture]
public class EnemyRebornManagerTests
{
    private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;
    private const BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance;

    private static readonly FieldInfo InfosField = typeof(EnemyRebornManager).GetField("_infos", PrivateInstance);
    private static readonly FieldInfo MaxHpField = typeof(BaseStates).GetField("_maxhp", PrivateInstance);

    [SetUp]
    public void SetUp()
    {
        ClearManager();
    }

    [TearDown]
    public void TearDown()
    {
        ClearManager();
    }

    [Test]
    public void OnBattleEnd_SetsCountingStateForDeadRebornEnemy()
    {
        var enemy = CreateEnemy(0f, 10f, 2, false);

        EnemyRebornManager.Instance.OnBattleEnd(new List<NormalEnemy> { enemy }, 100);

        var info = GetInfo(enemy);
        Assert.IsNotNull(info);
        Assert.AreEqual(2, GetRemainingSteps(info));
        Assert.AreEqual(100, GetLastProgress(info));
        Assert.AreEqual(EnemyRebornState.Counting, GetState(info));
    }

    [Test]
    public void CanReborn_DecrementsRemainingStepsAndRebornsAtZero()
    {
        var enemy = CreateEnemy(0f, 10f, 2, false);

        EnemyRebornManager.Instance.OnBattleEnd(new List<NormalEnemy> { enemy }, 100);

        var first = EnemyRebornManager.Instance.CanReborn(enemy, 101);
        Assert.IsFalse(first);
        var info = GetInfo(enemy);
        Assert.AreEqual(1, GetRemainingSteps(info));
        Assert.AreEqual(101, GetLastProgress(info));
        Assert.AreEqual(EnemyRebornState.Counting, GetState(info));

        var second = EnemyRebornManager.Instance.CanReborn(enemy, 102);
        Assert.IsTrue(second);
        info = GetInfo(enemy);
        Assert.AreEqual(0, GetRemainingSteps(info));
        Assert.AreEqual(EnemyRebornState.Reborned, GetState(info));
    }

    [Test]
    public void CanReborn_ReturnsTrueWhenNoInfoExists()
    {
        var enemy = CreateEnemy(10f, 10f, 2, false);

        var result = EnemyRebornManager.Instance.CanReborn(enemy, 50);

        Assert.IsTrue(result);
        Assert.IsNull(GetInfo(enemy));
    }

    [Test]
    public void Clear_RemovesTrackedEnemy()
    {
        var enemy = CreateEnemy(0f, 10f, 1, false);

        EnemyRebornManager.Instance.OnBattleEnd(new List<NormalEnemy> { enemy }, 10);
        Assert.IsNotNull(GetInfo(enemy));

        EnemyRebornManager.Instance.Clear(enemy);

        Assert.IsNull(GetInfo(enemy));
    }

    private static void ClearManager()
    {
        var infos = GetInfos();
        infos.Clear();
    }

    private static IDictionary GetInfos()
    {
        if (InfosField == null)
        {
            throw new MissingFieldException(typeof(EnemyRebornManager).FullName, "_infos");
        }
        return (IDictionary)InfosField.GetValue(EnemyRebornManager.Instance);
    }

    private static object GetInfo(NormalEnemy enemy)
    {
        var infos = GetInfos();
        return infos.Contains(enemy) ? infos[enemy] : null;
    }

    private static int GetRemainingSteps(object info)
    {
        return (int)GetInfoField("RemainingSteps").GetValue(info);
    }

    private static int GetLastProgress(object info)
    {
        return (int)GetInfoField("LastProgress").GetValue(info);
    }

    private static EnemyRebornState GetState(object info)
    {
        return (EnemyRebornState)GetInfoField("State").GetValue(info);
    }

    private static FieldInfo GetInfoField(string name)
    {
        var infoType = typeof(EnemyRebornManager).GetNestedType("RebornInfo", PrivateInstance);
        if (infoType == null)
        {
            throw new MissingMemberException(typeof(EnemyRebornManager).FullName, "RebornInfo");
        }
        var field = infoType.GetField(name, PublicInstance);
        if (field == null)
        {
            throw new MissingFieldException(infoType.FullName, name);
        }
        return field;
    }

    private static NormalEnemy CreateEnemy(float hp, float maxHp, int recovelySteps, bool broken)
    {
        if (MaxHpField == null)
        {
            throw new MissingFieldException(typeof(BaseStates).FullName, "_maxhp");
        }

        var enemy = new NormalEnemy
        {
            RebornSteps = recovelySteps,
            broken = broken,
            DefaultImpression = SpiritualProperty.Doremis,
            MyType = CharacterType.Life
        };

        MaxHpField.SetValue(enemy, maxHp);
        enemy.HP = hp;

        return enemy;
    }
}
