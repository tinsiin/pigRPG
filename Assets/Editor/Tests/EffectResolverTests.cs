using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;

/// <summary>
/// EffectResolver のワイヤリングテスト
/// </summary>
[TestFixture]
public class EffectResolverTests
{
    private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

    private static readonly FieldInfo EffectChainField =
        typeof(EffectResolver).GetField("_effectChain", PrivateInstance);

    private static readonly FieldInfo QueryServiceField =
        typeof(EffectResolver).GetField("_queryService", PrivateInstance);

    [Test]
    public void SetQueryService_InitializesEffectChain()
    {
        var resolver = new EffectResolver();
        var queryService = new MockBattleQueryService();

        // Before SetQueryService
        var chainBefore = EffectChainField?.GetValue(resolver);
        Assert.IsNull(chainBefore, "Effect chain should be null before SetQueryService");

        // Call SetQueryService
        resolver.SetQueryService(queryService);

        // After SetQueryService
        var chainAfter = EffectChainField?.GetValue(resolver);
        Assert.IsNotNull(chainAfter, "Effect chain should be initialized after SetQueryService");
    }

    [Test]
    public void SetQueryService_StoresQueryService()
    {
        var resolver = new EffectResolver();
        var queryService = new MockBattleQueryService();

        resolver.SetQueryService(queryService);

        var storedService = QueryServiceField?.GetValue(resolver);
        Assert.AreSame(queryService, storedService, "QueryService should be stored");
    }

    [Test]
    public void NewEffectResolver_HasNullEffectChain()
    {
        var resolver = new EffectResolver();

        var chain = EffectChainField?.GetValue(resolver);

        Assert.IsNull(chain, "New EffectResolver should have null effect chain");
    }

    [Test]
    public void NewEffectResolver_HasNullQueryService()
    {
        var resolver = new EffectResolver();

        var service = QueryServiceField?.GetValue(resolver);

        Assert.IsNull(service, "New EffectResolver should have null query service");
    }

    /// <summary>
    /// テスト用のモック QueryService
    /// </summary>
    private sealed class MockBattleQueryService : IBattleQueryService
    {
        public bool IsVanguard(BaseStates chara) => false;
        public BattleGroup GetGroupForCharacter(BaseStates chara) => null;
        public allyOrEnemy GetCharacterFaction(BaseStates chara) => allyOrEnemy.alliy;
        public List<BaseStates> GetOtherAlliesAlive(BaseStates chara) => new List<BaseStates>();
        public bool IsFriend(BaseStates chara1, BaseStates chara2) => true;
        public BattleGroup FactionToGroup(allyOrEnemy faction) => null;
    }
}
