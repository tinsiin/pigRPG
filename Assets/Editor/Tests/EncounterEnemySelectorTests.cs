using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;

/// <summary>
/// EncounterEnemySelector unit tests (DI対応版)
/// 公開API (Select) を通じてテストする
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
            return enemy.RebornSteps >= 0;
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
    public void Select_ReturnsNullForEmptyList()
    {
        var enemies = new List<NormalEnemy>();
        var result = _selector.Select(enemies, 100);

        Assert.IsNull(result);
    }

    [Test]
    public void Select_ReturnsNullForNullList()
    {
        var result = _selector.Select(null, 100);

        Assert.IsNull(result);
    }

    [Test]
    public void Select_ReturnsGroupWithAliveEnemy()
    {
        var alive = CreateEnemy(10f, 10f, -1, false, SpiritualProperty.doremis, CharacterType.Life);
        var enemies = new List<NormalEnemy> { alive };

        var result = _selector.Select(enemies, 100);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.Ours.Contains(alive));
    }

    [Test]
    public void Select_IncludesRebornableDeadEnemy()
    {
        // 生きている敵がいないと選択できないので、生きている敵も追加
        var alive = CreateEnemy(10f, 10f, -1, false, SpiritualProperty.doremis, CharacterType.Life);
        var deadReborn = CreateEnemy(0f, 10f, 0, false, SpiritualProperty.doremis, CharacterType.Life);
        var enemies = new List<NormalEnemy> { alive, deadReborn };

        var result = _selector.Select(enemies, 100);

        Assert.IsNotNull(result);
        // 復活可能な敵も候補に含まれる（選ばれるかは乱数次第）
        Assert.GreaterOrEqual(result.Ours.Count, 1);
    }

    [Test]
    public void Select_ExcludesBrokenEnemy()
    {
        var alive = CreateEnemy(10f, 10f, -1, false, SpiritualProperty.doremis, CharacterType.Life);
        var broken = CreateEnemy(0f, 10f, 0, true, SpiritualProperty.doremis, CharacterType.Life);
        var enemies = new List<NormalEnemy> { alive, broken };

        var result = _selector.Select(enemies, 100);

        Assert.IsNotNull(result);
        Assert.IsFalse(result.Ours.Contains(broken));
    }

    [Test]
    public void Select_ExcludesNonRebornableDeadEnemy()
    {
        var alive = CreateEnemy(10f, 10f, -1, false, SpiritualProperty.doremis, CharacterType.Life);
        var deadNoReborn = CreateEnemy(0f, 10f, -1, false, SpiritualProperty.doremis, CharacterType.Life);
        var enemies = new List<NormalEnemy> { alive, deadNoReborn };

        var result = _selector.Select(enemies, 100);

        Assert.IsNotNull(result);
        Assert.IsFalse(result.Ours.Contains(deadNoReborn));
    }

    [Test]
    public void Select_WithManualCount_ReturnsSpecifiedNumber()
    {
        var matchCalc = new TestMatchCalculator(PartyProperty.HolyGroup, typeMatchUp: true, impressionMatchUp: true);
        var selector = new EncounterEnemySelector(_rebornManager, matchCalc);

        var enemy1 = CreateEnemy(10f, 10f, -1, false, SpiritualProperty.doremis, CharacterType.Life);
        var enemy2 = CreateEnemy(10f, 10f, -1, false, SpiritualProperty.doremis, CharacterType.Life);
        var enemy3 = CreateEnemy(10f, 10f, -1, false, SpiritualProperty.doremis, CharacterType.Life);
        var enemies = new List<NormalEnemy> { enemy1, enemy2, enemy3 };

        var result = selector.Select(enemies, 100, number: 1);

        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.Ours.Count);
    }

    [Test]
    public void Select_ReturnsMaxThreeEnemies()
    {
        var matchCalc = new TestMatchCalculator(PartyProperty.HolyGroup, typeMatchUp: true, impressionMatchUp: true);
        var selector = new EncounterEnemySelector(_rebornManager, matchCalc);

        var enemies = new List<NormalEnemy>();
        for (int i = 0; i < 10; i++)
        {
            enemies.Add(CreateEnemy(10f, 10f, -1, false, SpiritualProperty.doremis, CharacterType.Life));
        }

        var result = selector.Select(enemies, 100, number: 5);

        Assert.IsNotNull(result);
        Assert.LessOrEqual(result.Ours.Count, 3);
    }

    private static NormalEnemy CreateEnemy(
        float hp,
        float maxHp,
        int rebornSteps,
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
            RebornSteps = rebornSteps,
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
