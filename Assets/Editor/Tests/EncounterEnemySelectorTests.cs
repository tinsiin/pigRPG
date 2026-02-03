using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;

/// <summary>
/// EncounterEnemySelector unit tests (DI対応版)
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

    private sealed class TestRebornManager : IEnemyRebornManager
    {
        public bool CanReborn(NormalEnemy enemy, int globalSteps)
        {
            if (enemy == null) return false;
            if (enemy.broken) return false;
            return enemy.RecovelySteps >= 0;
        }

        public void OnBattleEnd(IReadOnlyList<NormalEnemy> enemies, int globalSteps)
        {
            // テスト用: 何もしない
        }

        public void PrepareReborn(NormalEnemy enemy, int globalSteps)
        {
            // テスト用: 何もしない
        }

        public void Clear(NormalEnemy enemy)
        {
            // テスト用: 何もしない
        }
    }

    private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;
    private const BindingFlags InternalInstance = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;
    private const BindingFlags InternalStatic = BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public;

    private static readonly FieldInfo MaxHpField = typeof(BaseStates).GetField("_maxhp", PrivateInstance);

    private EncounterEnemySelector _selector;
    private TestMatchCalculator _matchCalc;
    private TestRebornManager _rebornManager;

    [SetUp]
    public void SetUp()
    {
        _matchCalc = new TestMatchCalculator(PartyProperty.HolyGroup);
        _rebornManager = new TestRebornManager();
        _selector = new EncounterEnemySelector(_rebornManager, _matchCalc);
    }

    [Test]
    public void FilterEligibleEnemies_IncludesAliveAndRebornCandidates()
    {
        var alive = CreateEnemy(10f, 10f, -1, false, SpiritualProperty.doremis, CharacterType.Life);
        var deadNoReborn = CreateEnemy(0f, 10f, -1, false, SpiritualProperty.doremis, CharacterType.Life);
        var deadReborn = CreateEnemy(0f, 10f, 0, false, SpiritualProperty.doremis, CharacterType.Life);
        var deadBroken = CreateEnemy(0f, 10f, 0, true, SpiritualProperty.doremis, CharacterType.Life);

        var enemies = new List<NormalEnemy> { alive, deadNoReborn, deadReborn, deadBroken, null };
        var result = _selector.FilterEligibleEnemies(enemies, 100);

        Assert.IsTrue(result.Contains(alive));
        Assert.IsTrue(result.Contains(deadReborn));
        Assert.IsFalse(result.Contains(deadNoReborn));
        Assert.IsFalse(result.Contains(deadBroken));
    }

    [Test]
    public void TryResolveManualEnd_ReturnsTrueWhenTargetReached()
    {
        var resultList = new List<NormalEnemy>
        {
            CreateEnemy(10f, 10f, -1, false, SpiritualProperty.doremis, CharacterType.Life),
            CreateEnemy(10f, 10f, -1, false, SpiritualProperty.doremis, CharacterType.Life)
        };
        var validEnemies = new List<NormalEnemy>
        {
            CreateEnemy(10f, 10f, -1, false, SpiritualProperty.doremis, CharacterType.Life)
        };

        var resolved = _selector.TryResolveManualEnd(true, 2, resultList, validEnemies, out var impression);

        Assert.IsTrue(resolved);
        Assert.AreEqual(PartyProperty.HolyGroup, impression);
    }

    [Test]
    public void TryResolveManualEnd_ReturnsFalseWhenTargetNotReached()
    {
        var resultList = new List<NormalEnemy>
        {
            CreateEnemy(10f, 10f, -1, false, SpiritualProperty.doremis, CharacterType.Life)
        };
        var validEnemies = new List<NormalEnemy>
        {
            CreateEnemy(10f, 10f, -1, false, SpiritualProperty.doremis, CharacterType.Life)
        };

        var resolved = _selector.TryResolveManualEnd(true, 2, resultList, validEnemies, out var impression);

        Assert.IsFalse(resolved);
        Assert.AreEqual(default(PartyProperty), impression);
    }

    [Test]
    public void TryResolveAutoEnd_ReturnsTrueWhenThreeMembers()
    {
        var selector = new EncounterEnemySelector(_rebornManager, new TestMatchCalculator(PartyProperty.MelaneGroup));
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

        var resolved = selector.TryResolveAutoEnd(false, resultList, validEnemies, out var impression);

        Assert.IsTrue(resolved);
        Assert.AreEqual(PartyProperty.MelaneGroup, impression);
    }

    [Test]
    public void TryAddCompatibleTarget_AddsEnemyWhenAllMatch()
    {
        var selector = new EncounterEnemySelector(
            _rebornManager,
            new TestMatchCalculator(PartyProperty.HolyGroup, typeMatchUp: true, impressionMatchUp: true));
        var resultList = new List<NormalEnemy>
        {
            CreateEnemy(10f, 10f, -1, false, SpiritualProperty.doremis, CharacterType.Life)
        };
        var validEnemies = new List<NormalEnemy>
        {
            CreateEnemy(10f, 10f, -1, false, SpiritualProperty.doremis, CharacterType.Life)
        };

        selector.TryAddCompatibleTarget(resultList, validEnemies);

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

        var sympathy = EncounterEnemySelector.HasSympathy(resultList, target);

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

        var sympathy = EncounterEnemySelector.HasSympathy(resultList, target);

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
}
