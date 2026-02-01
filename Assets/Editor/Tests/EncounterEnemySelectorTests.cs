using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;

/// <summary>
/// EncounterEnemySelector unit tests (private helper logic via reflection)
/// </summary>
[TestFixture]
public class EncounterEnemySelectorTests
{
    private sealed class TestMatchCalculator : IEnemyMatchCalculator
    {
        private readonly bool _lonelyMatchUp;
        private readonly bool _typeMatchUp;
        private readonly bool _impressionMatchUp;
        private readonly int _matchPercent;
        private readonly PartyProperty _partyProperty;

        public TestMatchCalculator(
            PartyProperty partyProperty,
            bool lonelyMatchUp = false,
            bool typeMatchUp = true,
            bool impressionMatchUp = true,
            int matchPercent = 75)
        {
            _partyProperty = partyProperty;
            _lonelyMatchUp = lonelyMatchUp;
            _typeMatchUp = typeMatchUp;
            _impressionMatchUp = impressionMatchUp;
            _matchPercent = matchPercent;
            EnemyLonelyPartyImpression = new Dictionary<SpiritualProperty, PartyProperty>
            {
                { SpiritualProperty.doremis, partyProperty },
                { SpiritualProperty.pillar, partyProperty }
            };
        }

        public Dictionary<SpiritualProperty, PartyProperty> EnemyLonelyPartyImpression { get; set; }
        public bool LonelyMatchUp(SpiritualProperty impression) => _lonelyMatchUp;
        public bool TypeMatchUp(CharacterType a, CharacterType b) => _typeMatchUp;
        public bool ImpressionMatchUp(SpiritualProperty a, SpiritualProperty b, bool sympathy) => _impressionMatchUp;
        public int GetImpressionMatchPercent(SpiritualProperty a, SpiritualProperty b) => _matchPercent;
        public PartyProperty calculatePartyProperty(List<NormalEnemy> list) => _partyProperty;
    }

    private const BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;
    private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

    private static readonly FieldInfo MaxHpField = typeof(BaseStates).GetField("_maxhp", PrivateInstance);

    [Test]
    public void FilterEligibleEnemies_IncludesAliveAndRebornCandidates()
    {
        var alive = CreateEnemy(10f, 10f, -1, false, SpiritualProperty.doremis, CharacterType.Life);
        var deadNoReborn = CreateEnemy(0f, 10f, -1, false, SpiritualProperty.doremis, CharacterType.Life);
        var deadReborn = CreateEnemy(0f, 10f, 0, false, SpiritualProperty.doremis, CharacterType.Life);
        var deadBroken = CreateEnemy(0f, 10f, 0, true, SpiritualProperty.doremis, CharacterType.Life);

        var enemies = new List<NormalEnemy> { alive, deadNoReborn, deadReborn, deadBroken, null };
        var result = InvokeFilterEligibleEnemies(enemies, 100);

        Assert.IsTrue(result.Contains(alive));
        Assert.IsTrue(result.Contains(deadReborn));
        Assert.IsFalse(result.Contains(deadNoReborn));
        Assert.IsFalse(result.Contains(deadBroken));
    }

    [Test]
    public void TryResolveManualEnd_ReturnsTrueWhenTargetReached()
    {
        var calc = new TestMatchCalculator(PartyProperty.HolyGroup);
        var resultList = new List<NormalEnemy>
        {
            CreateEnemy(10f, 10f, -1, false, SpiritualProperty.doremis, CharacterType.Life),
            CreateEnemy(10f, 10f, -1, false, SpiritualProperty.doremis, CharacterType.Life)
        };
        var validEnemies = new List<NormalEnemy>
        {
            CreateEnemy(10f, 10f, -1, false, SpiritualProperty.doremis, CharacterType.Life)
        };

        var resolved = InvokeTryResolveManualEnd(true, 2, resultList, validEnemies, calc, out var impression);

        Assert.IsTrue(resolved);
        Assert.AreEqual(PartyProperty.HolyGroup, impression);
    }

    [Test]
    public void TryResolveManualEnd_ReturnsFalseWhenTargetNotReached()
    {
        var calc = new TestMatchCalculator(PartyProperty.HolyGroup);
        var resultList = new List<NormalEnemy>
        {
            CreateEnemy(10f, 10f, -1, false, SpiritualProperty.doremis, CharacterType.Life)
        };
        var validEnemies = new List<NormalEnemy>
        {
            CreateEnemy(10f, 10f, -1, false, SpiritualProperty.doremis, CharacterType.Life)
        };

        var resolved = InvokeTryResolveManualEnd(true, 2, resultList, validEnemies, calc, out var impression);

        Assert.IsFalse(resolved);
        Assert.AreEqual(default(PartyProperty), impression);
    }

    [Test]
    public void TryResolveAutoEnd_ReturnsTrueWhenThreeMembers()
    {
        var calc = new TestMatchCalculator(PartyProperty.MelaneGroup);
        var resultList = new List<NormalEnemy>
        {
            CreateEnemy(10f, 10f, -1, false, SpiritualProperty.doremis, CharacterType.Life),
            CreateEnemy(10f, 10f, -1, false, SpiritualProperty.doremis, CharacterType.Life),
            CreateEnemy(10f, 10f, -1, false, SpiritualProperty.doremis, CharacterType.Life)
        };
        var validEnemies = new List<NormalEnemy>
        {
            CreateEnemy(10f, 10f, -1, false, SpiritualProperty.doremis, CharacterType.Life)
        };

        var resolved = InvokeTryResolveAutoEnd(false, resultList, validEnemies, calc, out var impression);

        Assert.IsTrue(resolved);
        Assert.AreEqual(PartyProperty.MelaneGroup, impression);
    }

    [Test]
    public void TryAddCompatibleTarget_AddsEnemyWhenAllMatch()
    {
        var calc = new TestMatchCalculator(PartyProperty.HolyGroup, typeMatchUp: true, impressionMatchUp: true);
        var resultList = new List<NormalEnemy>
        {
            CreateEnemy(10f, 10f, -1, false, SpiritualProperty.doremis, CharacterType.Life)
        };
        var validEnemies = new List<NormalEnemy>
        {
            CreateEnemy(10f, 10f, -1, false, SpiritualProperty.doremis, CharacterType.Life)
        };

        InvokeTryAddCompatibleTarget(resultList, validEnemies, calc);

        Assert.AreEqual(2, resultList.Count);
        Assert.AreEqual(0, validEnemies.Count);
    }

    [Test]
    public void HasSympathy_ReturnsTrueWhenAnyBelowHalf()
    {
        var resultList = new List<NormalEnemy>
        {
            CreateEnemy(5f, 10f, -1, false, SpiritualProperty.doremis, CharacterType.Life)
        };
        var target = CreateEnemy(10f, 10f, -1, false, SpiritualProperty.doremis, CharacterType.Life);

        var sympathy = InvokeHasSympathy(resultList, target);

        Assert.IsTrue(sympathy);
    }

    [Test]
    public void HasSympathy_ReturnsFalseWhenAllAboveHalf()
    {
        var resultList = new List<NormalEnemy>
        {
            CreateEnemy(10f, 10f, -1, false, SpiritualProperty.doremis, CharacterType.Life)
        };
        var target = CreateEnemy(10f, 10f, -1, false, SpiritualProperty.doremis, CharacterType.Life);

        var sympathy = InvokeHasSympathy(resultList, target);

        Assert.IsFalse(sympathy);
    }

    private static NormalEnemy CreateEnemy(
        float hp,
        float maxHp,
        int recovelySteps,
        bool broken,
        SpiritualProperty impression,
        CharacterType type)
    {
        if (MaxHpField == null)
        {
            throw new MissingFieldException(typeof(BaseStates).FullName, "_maxhp");
        }

        var enemy = new NormalEnemy
        {
            RecovelySteps = recovelySteps,
            broken = broken,
            DefaultImpression = impression,
            MyType = type
        };

        MaxHpField.SetValue(enemy, maxHp);
        enemy.HP = hp;
        enemy.InitializeMyImpression();

        return enemy;
    }

    private static List<NormalEnemy> InvokeFilterEligibleEnemies(IReadOnlyList<NormalEnemy> enemies, int globalSteps)
    {
        var method = GetSelectorMethod("FilterEligibleEnemies");
        return (List<NormalEnemy>)method.Invoke(null, new object[] { enemies, globalSteps });
    }

    private static bool InvokeTryResolveManualEnd(
        bool manualCount,
        int targetCount,
        List<NormalEnemy> resultList,
        List<NormalEnemy> validEnemies,
        IEnemyMatchCalculator calc,
        out PartyProperty impression)
    {
        var method = GetSelectorMethod("TryResolveManualEnd");
        var args = new object[] { manualCount, targetCount, resultList, validEnemies, calc, default(PartyProperty) };
        var result = (bool)method.Invoke(null, args);
        impression = (PartyProperty)args[5];
        return result;
    }

    private static bool InvokeTryResolveAutoEnd(
        bool manualCount,
        List<NormalEnemy> resultList,
        List<NormalEnemy> validEnemies,
        IEnemyMatchCalculator calc,
        out PartyProperty impression)
    {
        var method = GetSelectorMethod("TryResolveAutoEnd");
        var args = new object[] { manualCount, resultList, validEnemies, calc, default(PartyProperty) };
        var result = (bool)method.Invoke(null, args);
        impression = (PartyProperty)args[4];
        return result;
    }

    private static void InvokeTryAddCompatibleTarget(
        List<NormalEnemy> resultList,
        List<NormalEnemy> validEnemies,
        IEnemyMatchCalculator calc)
    {
        var method = GetSelectorMethod("TryAddCompatibleTarget");
        method.Invoke(null, new object[] { resultList, validEnemies, calc });
    }

    private static bool InvokeHasSympathy(List<NormalEnemy> resultList, NormalEnemy target)
    {
        var method = GetSelectorMethod("HasSympathy");
        return (bool)method.Invoke(null, new object[] { resultList, target });
    }

    private static MethodInfo GetSelectorMethod(string name)
    {
        var method = typeof(EncounterEnemySelector).GetMethod(name, PrivateStatic);
        if (method == null)
        {
            throw new MissingMethodException(typeof(EncounterEnemySelector).FullName, name);
        }
        return method;
    }
}
